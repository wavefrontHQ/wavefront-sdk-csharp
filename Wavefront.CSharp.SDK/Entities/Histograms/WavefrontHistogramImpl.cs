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
        /*
         * If a thread's bin queue has exceeded MaxBins number of bins (e.g., the thread has data
         * that has yet to be reported for more than MaxBins number of minutes), delete the oldest
         * bin. Defaulted to 10 because we can expect the histogram to be reported at least once
         * every 10 minutes.
         */
        private static readonly int MaxBins = 10;

        private readonly Func<long> clockMillis;

        // Global list of thread local histogramBinsList wrapped in WeakReference
        private readonly List<WeakReference<List<MinuteBin>>> globalHistogramBinsList;

        /*
         * ThreadLocal histogramBinsList where the initial value set is also added to a
         * global list of thread local histogramBinsList wrapped in WeakReference.
         */
        private readonly ThreadLocal<List<MinuteBin>> histogramBinsList;

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
            globalHistogramBinsList = new List<WeakReference<List<MinuteBin>>>();
            histogramBinsList = new ThreadLocal<List<MinuteBin>>(() =>
            {
                var sharedBinsInstance = new List<MinuteBin>();
                globalHistogramBinsList.Add(
                    new WeakReference<List<MinuteBin>>(sharedBinsInstance));
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
            var cutOffMillis = CurrentMinuteMillis();
            var minuteBins = new List<MinuteBin>();

            lock(globalHistogramBinsList)
            {
                foreach (var weakRef in globalHistogramBinsList)
                {
                    if (weakRef.TryGetTarget(out var bins))
                    {
                        minuteBins.AddRange(bins.Where(bin => bin.MinuteMillis < cutOffMillis));
                    }
                }
            }

            var distributions = new List<Distribution>();
            foreach (var minuteBin in minuteBins)
            {
                var centroids = minuteBin
                    .Distribution.GetDistribution()
                    .Select(centroid => new KeyValuePair<double, int>(
                        centroid.Value, (int)centroid.Count
                    ))
                    .ToList();
                distributions.Add(new Distribution(minuteBin.MinuteMillis, centroids));
            }

            ClearPriorCurrentMinuteBin(cutOffMillis);

            return distributions;
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
        /// Gets a <see cref="Snapshot" /> of the current contents of the histogram.
        /// </summary>
        /// <returns>The snapshot.</returns>
        public Snapshot GetSnapshot()
        {
            var snapshot = new TDigest();

            lock(globalHistogramBinsList)
            {
                foreach (var weakRef in globalHistogramBinsList)
                {
                    if (weakRef.TryGetTarget(out var bins))
                    {
                        bins.ForEach(
                            bin => bin.Distribution.GetDistribution().ToList().ForEach(
                                centroid => snapshot.Add(centroid.Value, centroid.Count)
                            )
                        );
                    }
                }
            }

            return new Snapshot(snapshot);
        }

        private void ClearPriorCurrentMinuteBin(long cutOffMillis)
        {
            lock(globalHistogramBinsList)
            {
                for (int i = globalHistogramBinsList.Count - 1; i >= 0; i--)
                {
                    if (!globalHistogramBinsList[i].TryGetTarget(out var sharedBinsInstance))
                    {
                        globalHistogramBinsList.RemoveAt(i);
                        continue;
                    }

                    /*
                     * GetCurrentBin() method will add (PRODUCER) item to the sharedBinsInstance
                     * list, so lock the access to sharedBinsInstance
                     */
                    lock (sharedBinsInstance)
                    {
                        sharedBinsInstance.RemoveAll(
                            minuteBin => minuteBin.MinuteMillis < cutOffMillis);
                    }
                }
            }
        }

        /// <summary>
        /// Helper to retrieve the current bin. Will be invoked on the thread local
        /// histogramBinsList.
        /// </summary>
        /// <returns>The current bin.</returns>
        private MinuteBin GetCurrentBin()
        {
            var sharedBinsInstance = histogramBinsList.Value;
            var currMinuteMillis = CurrentMinuteMillis();

            lock(sharedBinsInstance)
            {
                int n = sharedBinsInstance.Count;
                if (n == 0 || sharedBinsInstance[n - 1].MinuteMillis != currMinuteMillis)
                {
                    sharedBinsInstance.Add(new MinuteBin(currMinuteMillis));
                    if (sharedBinsInstance.Count > MaxBins)
                    {
                        sharedBinsInstance.RemoveAt(0);
                    }
                }
                return sharedBinsInstance[sharedBinsInstance.Count - 1];
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
            public double GetValue(double quantile) => distribution.Quantile(quantile);
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
            public MinuteBin(long minuteMillis)
            {
                Distribution = new TDigest();
                MinuteMillis = minuteMillis;
            }
        }
    }
}
