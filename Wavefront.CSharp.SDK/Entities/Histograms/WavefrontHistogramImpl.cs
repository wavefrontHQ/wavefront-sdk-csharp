using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using StatsLib;

namespace Wavefront.CSharp.SDK.Entities.Histograms
{
    public class WavefrontHistogramImpl
    {
        // The accuracy of the TDigest distribution.
        private static readonly double Accuracy = 1.0 / 100;

        // The compression constant of the TDigest distribution.
        private static readonly double Compression = 20;

        /*
         * If a thread's bin queue has exceeded MaxBins number of bins (e.g., the thread has data
         * that has yet to be reported for more than MaxBins number of minutes), delete the oldest
         * bin. Defaulted to 10 because we can expect the histogram to be reported at least once
         * every 10 minutes.
         */
        private static readonly int MaxBins = 10;

        private readonly Func<long> clockMillis;

        /*
         * Global concurrent list of thread local histogramBinsList wrapped in WeakReference.
         * This list holds all the thread local List of Minute Bins.
         * This is a ConcurrentMinuteBinList so that we can lock and modify the queue while
         * enumerating in order to prevent InvalidOperationExceptions.
         * The MinuteBin itself is not thread safe and can change but it is still thread safe since
         * we don’t ever update a bin that’s old or flush a bin that’s within the current minute.
         */
        private readonly List<WeakReference<ConcurrentMinuteBinList>> globalHistogramBinsList =
            new List<WeakReference<ConcurrentMinuteBinList>>();

        // Protects read and write access to globalHistogramBinsList
        private readonly ReaderWriterLockSlim globalHistogramBinsLock = new ReaderWriterLockSlim();

        /*
         * ThreadLocal histogramBinsList where the initial value set is also added to a
         * global list of thread local histogramBinsList wrapped in WeakReference.
         */
        private readonly ThreadLocal<ConcurrentMinuteBinList> histogramBinsList;

        /// <summary>
        /// Initializes a new instance of the <see cref="WavefrontHistogramImpl"/> class
        /// with timestamps taken as current UTC time. 
        /// </summary>
        public WavefrontHistogramImpl() : this(() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WavefrontHistogramImpl"/> class
        /// with a parameter for a custom clock.
        /// </summary>
        /// <param name="clockMillis">A delegate function for a clock in milliseconds.</param>
        public WavefrontHistogramImpl(Func<long> clockMillis)
        {
            this.clockMillis = clockMillis;
            histogramBinsList = new ThreadLocal<ConcurrentMinuteBinList>(() =>
            {
                var sharedBinsInstance = new ConcurrentMinuteBinList();
                globalHistogramBinsLock.EnterWriteLock();
                try
                {
                    globalHistogramBinsList.Add(
                        new WeakReference<ConcurrentMinuteBinList>(sharedBinsInstance));
                }
                finally
                {
                    globalHistogramBinsLock.ExitWriteLock();
                }
                return sharedBinsInstance;
            });
        }

        /// <summary>
        /// Adds a value to the histogram.
        /// </summary>
        /// <param name="value">The value to be added.</param>
        public void Update(int value)
        {
            Update((double)value);
        }

        /// <summary>
        /// Adds a value to the histogram.
        /// </summary>
        /// <param name="value">The value to be added.</param>
        public void Update(long value)
        {
            Update((double)value);
        }

        /// <summary>
        /// Adds a value to the histogram.
        /// </summary>
        /// <param name="value">The value to be added.</param>
        public void Update(double value)
        {
            GetCurrentBin().Distribution.Add(value);
        }

        /// <summary>
        /// Bulk update this histogram with a set of centroids.
        /// </summary>
        /// <param name="means">The centroid values.</param>
        /// <param name="counts">The centroid weights/sample counts.</param>
        public void BulkUpdate(IList<double> means, IList<int> counts)
        {
            if (means != null && counts != null)
            {
                int n = Math.Min(means.Count, counts.Count);
                var currentBin = GetCurrentBin();
                for (int i = 0; i < n; ++i)
                {
                    currentBin.Distribution.Add(means[i], counts[i]);
                }
            }
        }

        /// <summary>
        /// Aggregates all the minute bins prior to the current minute (because threads might be
        /// updating the current minute bin while the method is invoked) and returns a list of the
        /// distributions held within each bin. Note that invoking this method will also clear all
        /// data from the aggregated bins, thereby changing the state of the system and preventing
        /// data from being flushed more than once.
        /// </summary>
        /// <returns>
        /// A list of distributions, each a <see cref="Distribution"/> holding a timestamp as well
        /// as a list of centroids. Each centroid is a tuple containing the centroid value and
        /// count.
        /// </returns>
        public IList<Distribution> FlushDistributions()
        {
            long cutOffMillis = CurrentMinuteMillis();
            globalHistogramBinsLock.EnterWriteLock();
            try
            {
                return ProcessGlobalHistogramBinsList(cutOffMillis);
            }
            finally
            {
                globalHistogramBinsLock.ExitWriteLock();
            }
        }

        private IList<Distribution> ProcessGlobalHistogramBinsList(long cutOffMillis)
        {
            var distributions = new List<Distribution>();

            for (int i = globalHistogramBinsList.Count - 1; i >= 0; --i)
            {
                if (globalHistogramBinsList[i].TryGetTarget(out var sharedBinsInstance))
                {
                    sharedBinsInstance.Lock.EnterWriteLock();
                    try
                    {
                        for (int j = sharedBinsInstance.MinuteBins.Count - 1; j >= 0; --j)
                        {
                            var bin = sharedBinsInstance.MinuteBins[j];
                            if (bin.MinuteMillis < cutOffMillis)
                            {
                                var centroids = bin
                                    .Distribution.GetDistribution()
                                    .Select(centroid => new KeyValuePair<double, int>(
                                        centroid.Value, (int)centroid.Count
                                    ))
                                    .ToList();
                                distributions.Add(new Distribution(bin.MinuteMillis, centroids));
                                sharedBinsInstance.MinuteBins.RemoveAt(j);
                            }
                        }
                    }
                    finally
                    {
                        sharedBinsInstance.Lock.ExitWriteLock();
                    }

                }
                else
                {
                    globalHistogramBinsList.RemoveAt(i);
                }
            }

            return distributions;
        }

        /// <summary>
        /// Gets a <see cref="Snapshot" /> of the current contents of the histogram.
        /// </summary>
        /// <returns>The snapshot.</returns>
        public Snapshot GetSnapshot()
        {
            var snapshot = new TDigest(Accuracy, Compression);
            globalHistogramBinsLock.EnterReadLock();
            try
            {
                foreach (var weakRef in globalHistogramBinsList)
                {
                    if (weakRef.TryGetTarget(out var sharedBinsInstance))
                    {
                        sharedBinsInstance.Lock.EnterReadLock();
                        try
                        {
                            foreach (var bin in sharedBinsInstance.MinuteBins)
                            {
                                foreach (var centroid in bin.Distribution.GetDistribution())
                                {
                                    snapshot.Add(centroid.Value, centroid.Count);
                                }
                            }
                        }
                        finally
                        {
                            sharedBinsInstance.Lock.ExitReadLock();
                        }
                    }
                }
            }
            finally
            {
                globalHistogramBinsLock.ExitReadLock();
            }

            return new Snapshot(snapshot);
        }

        /// <summary>
        /// Serializes a list of <see cref="Distribution" /> objects into a
        /// <see cref="KeyValuePair{string, string}"/>, where the key is a string that holds
        /// all the timestamps in the distribution while the value is a string that holds all
        /// the centroids in the distribution.
        /// </summary>
        /// <returns>The serialized distributions as a pair of strings.</returns>
        /// <param name="distributions">The list of distributions.</param>
        public static KeyValuePair<string, string> Serialize(IList<Distribution> distributions)
        {
            StringBuilder timestampsBuilder = new StringBuilder();
            StringBuilder centroidsBuilder = new StringBuilder();

            for (int i = 0; i < distributions.Count; ++i)
            {
                var distribution = distributions[i];

                if (distribution.Centroids.Count == 0)
                {
                    continue;
                }

                if (i > 0)
                {
                    timestampsBuilder.Append(';');
                    centroidsBuilder.Append(';');
                }

                timestampsBuilder.Append(distribution.Timestamp);
                for (int j = 0; j < distribution.Centroids.Count; ++j)
                {
                    if (j > 0)
                    {
                        centroidsBuilder.Append(',');
                    }

                    var centroid = distribution.Centroids[j];
                    centroidsBuilder.Append(centroid.Key).Append(' ').Append(centroid.Value);
                }
            }

            return new KeyValuePair<string, string>(
                timestampsBuilder.ToString(), centroidsBuilder.ToString());
        }

        /// <summary>
        /// Deserializes a <see cref="KeyValuePair{string, string}"/> into a list of
        /// <see cref="Distribution"/> objects.
        /// </summary>
        /// <returns>The deserialized list of distributions.</returns>
        /// <param name="pair">The <see cref="KeyValuePair{string, string}"/>.</param>
        public static IList<Distribution> Deserialize(KeyValuePair<string, string> pair)
        {
            var distributions = new List<Distribution>();

            var serializedTimestamps = pair.Key.Split(';');
            var serializedCentroids = pair.Value.Split(';');
            int n = Math.Min(serializedTimestamps.Length, serializedCentroids.Length);

            for (int i = 0; i < n; ++i)
            {
                long timestamp = Convert.ToInt64(serializedTimestamps[i]);
                var centroids = new List<KeyValuePair<double, int>>();
                foreach (string centroid in serializedCentroids[i].Split(','))
                {
                    var components = centroid.Split(' ');
                    centroids.Add(new KeyValuePair<double, int>(
                        Convert.ToDouble(components[0]),
                        Convert.ToInt32(components[1])
                    ));
                }
                distributions.Add(new Distribution(timestamp, centroids));
            }

            return distributions;
        }

        /// <summary>
        /// Helper to retrieve the current bin. Will be invoked on the thread local
        /// histogramBinsList.
        /// </summary>
        /// <returns>The current bin.</returns>
        private MinuteBin GetCurrentBin()
        {
            var sharedBinsInstance = histogramBinsList.Value;
            long currMinuteMillis = CurrentMinuteMillis();

            /*
             * We'll upgrade to a write lock only when necessary because on most occasions,
             * the condition for updating the list of bins will not be satisfied.
             */
            sharedBinsInstance.Lock.EnterUpgradeableReadLock();
            try
            {
                int n = sharedBinsInstance.MinuteBins.Count;
                if (n == 0 || sharedBinsInstance.MinuteBins[n - 1].MinuteMillis != currMinuteMillis)
                {
                    sharedBinsInstance.Lock.EnterWriteLock();
                    try
                    {
                        sharedBinsInstance.MinuteBins.Add(
                            new MinuteBin(Accuracy, Compression, currMinuteMillis));
                        if (sharedBinsInstance.MinuteBins.Count > MaxBins)
                        {
                            sharedBinsInstance.MinuteBins.RemoveAt(0);
                        }
                    }
                    finally
                    {
                        sharedBinsInstance.Lock.ExitWriteLock();
                    }
                }
                return sharedBinsInstance.MinuteBins[sharedBinsInstance.MinuteBins.Count - 1];
            }
            finally
            {
                sharedBinsInstance.Lock.ExitUpgradeableReadLock();
            }
        }

        private long CurrentMinuteMillis()
        {
            return (clockMillis() / 60000L) * 60000L;
        }

        /// <summary>
        /// A snapshot of the current state of the histogram. Backed by a TDigest distribution.
        /// </summary>
        public class Snapshot {
            private readonly TDigest distribution;

            internal Snapshot(TDigest distribution)
            {
                this.distribution = distribution;
            }

            /// <summary>
            /// The number of values in the distribution.
            /// </summary>
            /// <value>A count of the number of values.</value>
            public long Count => (long)distribution.Count;

            /// <summary>
            /// The largest value in the distribution.
            /// </summary>
            /// <value>The maximum value.</value>
            public double Max => distribution.Max;

            /// <summary>
            /// The average value in the distribution.
            /// </summary>
            /// <value>The average value.</value>
            public double Mean => distribution.Average;

            /// <summary>
            /// The smallest value in the distribution.
            /// </summary>
            /// <value>The minimum value.</value>
            public double Min => distribution.Min;

            /// <summary>
            /// The number of centroids in the distribution.
            /// </summary>
            /// <value>The number of centroids.</value>
            public int Size => distribution.CentroidCount;

            /// <summary>
            /// Not supported, so NaN is returned.
            /// </summary>
            /// <value>NaN</value>
            public double StdDev => Double.NaN;

            /// <summary>
            /// The sum of all the values in the distribution.
            /// </summary>
            /// <value>The sum of all the values.</value>
            public double Sum => distribution.GetDistribution()
                                             .Sum(centroid => centroid.Value * centroid.Count);

            /// <summary>
            /// Gets all the values in the distribution.
            /// </summary>
            /// <value>The values in the distribution.</value>
            public IEnumerable<double> Values => distribution
                .GetDistribution()
                .SelectMany(centroid => Enumerable.Repeat(centroid.Value, (int)centroid.Count));

            /// <summary>
            /// Gets an estimate of the specified quantile of the distribution.
            /// </summary>
            /// <returns>The estimated value of the quantile.</returns>
            /// <param name="quantile">The quantile.</param>
            public double GetValue(double quantile)
            {
                return Count == 0 ? Double.NaN : distribution.Quantile(quantile);
            }
        }

        /// <summary>
        /// Representation of a histogram distribution, containing a timestamp and a list of
        /// centroids.
        /// </summary>
        public class Distribution
        {
            /// <summary>
            /// Gets the timestamp.
            /// </summary>
            /// <value>The timestamp in milliseconds since the epoch.</value>
            public long Timestamp { get; }

            /// <summary>
            /// Gets the list of histogram points, each a <see cref="KeyValuePair{double, int}"/>
            /// where the key is the mean value of the centroid and the value is the count of
            /// points in that centroid.
            /// </summary>
            /// <value>The list of centroids.</value>
            public IList<KeyValuePair<double, int>> Centroids { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="Distribution"/> class.
            /// </summary>
            /// <param name="timestamp">The timestamp in milliseconds since the epoch.</param>
            /// <param name="centroids">The list of centroids.</param>
            public Distribution(long timestamp, IList<KeyValuePair<double, int>> centroids)
            {
                Timestamp = timestamp;
                Centroids = centroids;
            }
        }

        /// <summary>
        /// Representation of a bin that holds histogram data for a particular minute in time.
        /// </summary>
        private class MinuteBin
        {
            /// <summary>
            /// Gets the histogram data for the minute bin, represented as a <see cref="TDigest"/>.
            /// </summary>
            /// <value>The <see cref="TDigest"/> distribution.</value>
            public TDigest Distribution { get; }

            /// <summary>
            /// Gets the timestamp at the start of the minute.
            /// </summary>
            /// <value>The timestamp in milliseconds.</value>
            public long MinuteMillis { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="MinuteBin"/> class.
            /// </summary>
            /// <param name="minuteMillis">The start of the minute in milliseconds.</param>
            public MinuteBin(double accuracy, double compression, long minuteMillis)
            {
                Distribution = new TDigest(accuracy, compression);
                MinuteMillis = minuteMillis;
            }
        }

        /// <summary>
        /// A class that holds a list of minute bins along with a lock to handle concurrent
        /// read/write access to the list. This is a workaround to get around the fact
        /// that C# lacks a built-in concurrent collection that is ordered, can handle lookups
        /// at both ends, and can handle element removal while enumerating.
        /// </summary>
        private class ConcurrentMinuteBinList
        {
            public List<MinuteBin> MinuteBins { get; } = new List<MinuteBin>();
            public ReaderWriterLockSlim Lock { get; } = new ReaderWriterLockSlim();
        }
    }
}
