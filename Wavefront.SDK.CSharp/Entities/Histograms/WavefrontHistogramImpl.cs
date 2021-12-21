using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Wavefront.SDK.CSharp.Entities.StatsLib;
using Wavefront.SDK.CSharp.Common;

namespace Wavefront.SDK.CSharp.Entities.Histograms
{
    public class WavefrontHistogramImpl
    {
        // The accuracy of the TDigest distribution.
        private static readonly double Accuracy = 1.0 / 32;

        // The compression constant of the TDigest distribution.
        private static readonly double Compression = 20;

        /*
         * Re-compress the centroids when their number exceeeds this bloat factor divided by
         * Accuracy.
         */
        private static readonly double Recompression_Threshold_Factor = 2;

        /*
         * If a thread's bin queue has exceeded MaxBins number of bins (e.g., the thread has data
         * that has yet to be reported for more than MaxBins number of minutes), delete the oldest
         * bin. Defaulted to 10 because we can expect the histogram to be reported at least once
         * every 10 minutes.
         */
        private static readonly int MaxBins = 10;

        private readonly Func<long> clockMillis;

        // Global list of ThreadMinuteBin.
        private readonly IList<ThreadMinuteBin> globalHistogramBinsList =
            new List<ThreadMinuteBin>();

        // Protects read and write access to globalHistogramBinsList
        private readonly ReaderWriterLockSlim globalHistogramBinsLock = new ReaderWriterLockSlim();

        /*
         * Current Minute Histogram Bin.
         * Update functions will only update data inside currentMinuteBin, which contains
         * TimeStamp and the ConcurrentDictionary of ThreadId and TDigest distribution.
         */
        private ThreadMinuteBin currentMinuteBin;

        /// <summary>
        /// Initializes a new instance of the <see cref="WavefrontHistogramImpl"/> class
        /// with timestamps taken as current UTC time. 
        /// </summary>
        public WavefrontHistogramImpl() :
            this(() => DateTimeUtils.UnixTimeMilliseconds(DateTime.UtcNow))
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
            currentMinuteBin = new ThreadMinuteBin(CurrentMinuteMillis());
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
            GetCurrentBin().UpdateByThreadId(Thread.CurrentThread.ManagedThreadId, value);
        }

        /// <summary>
        /// Bulk update this histogram with a set of centroids.
        /// </summary>
        /// <param name="means">The centroid values.</param>
        /// <param name="counts">The centroid weights/sample counts.</param>
        public void BulkUpdate(IList<double> means, IList<int> counts)
        {
            GetCurrentBin().BulkUpdateByThreadId(
                Thread.CurrentThread.ManagedThreadId, means, counts);
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
            var distributions = new List<Distribution>();
            globalHistogramBinsLock.EnterUpgradeableReadLock();
            try
            {
                var globalHistogramBinsList = GetGlobalHistogramBinsList();
                globalHistogramBinsLock.EnterWriteLock();
                try
                {
                    for (int i = globalHistogramBinsList.Count - 1; i >= 0; --i)
                    {
                        distributions.Add(globalHistogramBinsList[i].ToDistribution());
                        globalHistogramBinsList.RemoveAt(i);
                    }
                }
                finally
                {
                    globalHistogramBinsLock.ExitWriteLock();
                }
            }
            finally
            {
                globalHistogramBinsLock.ExitUpgradeableReadLock();
            }
            return distributions;
        }

        /// <summary>
        /// Gets a <see cref="Snapshot" /> of the contents of the histogram prior to the current
        /// minute.
        /// </summary>
        /// <returns>The snapshot.</returns>
        public Snapshot GetSnapshot()
        {
            var snapshot = new TDigest(Accuracy, Compression);
            globalHistogramBinsLock.EnterUpgradeableReadLock();
            try
            {
                foreach (var bin in GetGlobalHistogramBinsList())
                {
                    foreach (var dist in bin.PerThreadDist.Values)
                    {
                        foreach (var centroid in dist.GetDistribution())
                        {
                            snapshot.Add(centroid.Value, centroid.Count);
                        }
                    }
                }
            }
            finally
            {
                globalHistogramBinsLock.ExitUpgradeableReadLock();
            }
            
            if (snapshot.CentroidCount > Recompression_Threshold_Factor / Accuracy)
            {
                snapshot = Compress(snapshot);
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

        private long CurrentMinuteMillis()
        {
            return (clockMillis() / 60000L) * 60000L;
        }

        /// <summary>
        /// Helper to retrieve the current bin.
        /// Will flush currentMinuteBin into globalHistogramBinsList if it's a new minute.
        /// </summary>
        /// <returns>The current ThreadMinuteBin.</returns>
        private ThreadMinuteBin GetCurrentBin()
        {
            return FlushCurrentBin(CurrentMinuteMillis());
        }

        private ThreadMinuteBin FlushCurrentBin(long currMinuteMillis)
        {
            if (currentMinuteBin.MinuteMillis == currMinuteMillis)
            {
                return currentMinuteBin;
            }

            lock (this)
            {
                if (currentMinuteBin.MinuteMillis != currMinuteMillis)
                {
                    globalHistogramBinsLock.EnterWriteLock();
                    try
                    {
                        if (globalHistogramBinsList.Count > MaxBins)
                        {
                            globalHistogramBinsList.RemoveAt(0);
                        }
                        globalHistogramBinsList.Add(new ThreadMinuteBin(currentMinuteBin));
                    }
                    finally
                    {
                        globalHistogramBinsLock.ExitWriteLock();
                    }
                    currentMinuteBin = new ThreadMinuteBin(currMinuteMillis);
                }
                return currentMinuteBin;
            }
        }

        private IList<ThreadMinuteBin> GetGlobalHistogramBinsList()
        {
            FlushCurrentBin(CurrentMinuteMillis());
            return globalHistogramBinsList;
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
            public double Mean
            {
                // TODO: use distribution.Average once Average is fixed for TDigest.
                get
                {
                    double value = 0, count = 0;
                    foreach (var centroid in distribution.GetDistribution())
                    {
                        value += centroid.Value * centroid.Count;
                        count += centroid.Count;
                    }
                    return count == 0 ? Double.NaN : value / count;
                }
            }

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
            /// The standard deviation of the values in the distribution.
            /// </summary>
            /// <value>The standard deviation of distribution values.</value>
            public double StdDev
            {
                get
                {
                    double mean = Mean;
                    double varianceSum = distribution.GetDistribution().Select(centroid =>
                    {
                        double diff = centroid.Value - mean;
                        return diff * diff * centroid.Count;
                    }).Sum();
                    double variance = Count == 0 ? 0 : varianceSum / distribution.Count;
                    return Math.Sqrt(variance);
                }
            }

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
        private class ThreadMinuteBin
        {
            /// <summary>
            /// Gets the <see cref="TDigest"/> distribution for each thread in the given minute.
            /// </summary>
            /// <value>The dictionary mapping thread id to TDigest distribution.</value>
            public ConcurrentDictionary<int, TDigest> PerThreadDist { get; }

            /// <summary>
            /// Gets the timestamp at the start of the minute.
            /// </summary>
            /// <value>The timestamp in milliseconds.</value>
            public long MinuteMillis { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="ThreadMinuteBin"/> class.
            /// </summary>
            /// <param name="minuteMillis">The start of the minute in milliseconds.</param>
            public ThreadMinuteBin(long minuteMillis)
            {
                PerThreadDist = new ConcurrentDictionary<int, TDigest>();
                MinuteMillis = minuteMillis;
            }

            /// <summary>
            /// Copy constructor for appending ThreadMinuteBin to globalHistogramBinsList.
            /// </summary>
            /// <param name="threadMinuteBin">The thread minute bin to copy.</param>
            public ThreadMinuteBin(ThreadMinuteBin threadMinuteBin)
            {
                PerThreadDist =
                    new ConcurrentDictionary<int, TDigest>(threadMinuteBin.PerThreadDist);
                MinuteMillis = threadMinuteBin.MinuteMillis;
            }

            /// <summary>
            /// Retrieves the thread-local <see cref="TDigest"/> distribution in the given minute.
            /// </summary>
            /// <returns>The TDigest distribution.</returns>
            /// <param name="threadId">The thread id.</param>
            public TDigest GetDistByThreadId(int threadId)
            {
                return PerThreadDist.GetOrAdd(threadId, new TDigest(Accuracy, Compression));
            }

            /// <summary>
            /// Adds a value to the thread-local <see cref="TDigest"/> distribution.
            /// </summary>
            /// <param name="threadId">The thread id.</param>
            /// <param name="value">The value to add.</param>
            public void UpdateByThreadId(int threadId, double value)
            {
                GetDistByThreadId(threadId).Add(value);
            }

            /// <summary>
            /// Adds a set of centroids to the thread-local <see cref="TDigest"/> distribution.
            /// </summary>
            /// <param name="threadId">The thread id.</param>
            /// <param name="means">The centroid values.</param>
            /// <param name="counts">The centroid weights/sample counts.</param>
            public void BulkUpdateByThreadId(int threadId, IList<double> means, IList<int> counts)
            {
                if (means != null && counts != null)
                {
                    TDigest dist = GetDistByThreadId(threadId);
                    for (int i = 0; i < Math.Min(means.Count, counts.Count); ++i)
                    {
                        dist.Add(means[i], counts[i]);
                    }
                }
            }

            /// <summary>
            /// Converts to a <see cref="Distribution"/>.
            /// </summary>
            /// <returns>The distribution.</returns>
            public Distribution ToDistribution()
            {
                TDigest merged = new TDigest(Accuracy, Compression);
                foreach (TDigest dist in PerThreadDist.Values)
                {
                    foreach (DistributionPoint centroid in dist.GetDistribution())
                    {
                        merged.Add(centroid.Value, centroid.Count);
                    }
                }

                if (merged.CentroidCount > Recompression_Threshold_Factor / Accuracy)
                {
                    merged = Compress(merged);
                }

                var centroids = merged.GetDistribution()
                                      .Select(centroid => new KeyValuePair<double, int>(
                                          centroid.Value, (int)centroid.Count
                                        ))
                                      .ToList();
                return new Distribution(MinuteMillis, centroids);
            }
        }

        /// <summary>
        ///     Copy of TDigest's private Compress() method.
        /// </summary>
        /// <param name="digest"></param>
        /// <returns></returns>
        private static TDigest Compress(TDigest digest)
        {
            TDigest newTDigest = new TDigest(Accuracy, Compression);
            List<DistributionPoint> temp = digest.GetDistribution().ToList();
            temp.Shuffle();

            foreach (DistributionPoint centroid in temp)
            {
                newTDigest.Add(centroid.Value, centroid.Count);
            }

            return newTDigest;
        }
    }
}
