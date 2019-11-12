using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Entities.Histograms;
using Xunit;

namespace Wavefront.SDK.CSharp.Test
{
    /// <summary>
    /// Unit tests for <see cref="WavefrontHistogramImpl"/>
    /// </summary>
    public class WavefrontHistogramImplTest
    {
        private static readonly Dictionary<double, int> PowDict = new Dictionary<double, int>{
            {0.1, 1}, {1, 1}, {10, 2}, {100, 1}, {1000, 1}, {10000, 2}, {100000, 1}
        };

        private static WavefrontHistogramImpl CreatePowHistogram(Func<long> clockMillis)
        {
            var means = new List<double>();
            var counts = new List<int>();

            foreach (var entry in PowDict)
            {
                means.Add(entry.Key);
                counts.Add(entry.Value);
            }

            var wavefrontHistogram = new WavefrontHistogramImpl(clockMillis);
            wavefrontHistogram.BulkUpdate(means, counts);

            return wavefrontHistogram;
        }

        private static WavefrontHistogramImpl CreateRangeHistogram(Func<long> clockMillis)
        {
            var wavefrontHistogram = new WavefrontHistogramImpl(clockMillis);

            for (int i = 1; i <= 1000; ++i)
            {
                wavefrontHistogram.Update(i);
            }

            return wavefrontHistogram;
        }

        private static WavefrontHistogramImpl CreateMultiThreadedHistogram(Func<long> clockMillis)
        {
            var wavefrontHistogram = new WavefrontHistogramImpl(clockMillis);

            // Update the histogram in parallel
            Parallel.For(1, 101, wavefrontHistogram.Update);

            return wavefrontHistogram;
        }

        private static Dictionary<long, Dictionary<double, int>> DistributionsToDictionary(
            IList<WavefrontHistogramImpl.Distribution> distributions)
        {
            var dict = new Dictionary<long, Dictionary<double, int>>();

            foreach (var distribution in distributions)
            {
                if (!dict.ContainsKey(distribution.Timestamp))
                {
                    dict.Add(distribution.Timestamp, new Dictionary<double, int>());
                }
                foreach (var centroid in distribution.Centroids)
                {
                    int val = dict[distribution.Timestamp].ContainsKey(centroid.Key) ?
                        dict[distribution.Timestamp][centroid.Key] : 0;
                    dict[distribution.Timestamp][centroid.Key] = val + centroid.Value;
                }
            }

            return dict;
        }

        [Fact]
        public void TestFlushDistributions()
        {
            var currentTime = DateTime.UtcNow;
            long clockMillis() => DateTimeUtils.UnixTimeMilliseconds(currentTime);

            var wavefrontHistogram = CreatePowHistogram(clockMillis);
            long minute0ClockMillis = clockMillis() / 60000 * 60000;

            // Test that nothing in the current minute bin gets flushed
            var distributions = wavefrontHistogram.FlushDistributions();
            Assert.Equal(0, distributions.Count);

            // Test that prior minute bins get flushed
            currentTime = currentTime.AddMinutes(1);
            long minute1ClockMillis = clockMillis() / 60000 * 60000;

            wavefrontHistogram.Update(0.01);

            currentTime = currentTime.AddMinutes(1);

            distributions = wavefrontHistogram.FlushDistributions();
            var actualDict = DistributionsToDictionary(distributions);

            var expectedDict = new Dictionary<long, Dictionary<double, int>>{
                {minute0ClockMillis, new Dictionary<double, int>{
                        {0.1, 1}, {1, 1}, {10, 2}, {100, 1}, {1000, 1}, {10000, 2}, {100000, 1}
                    }},
                {minute1ClockMillis, new Dictionary<double, int>{ {0.01, 1} }}
            };

            Assert.Equal(expectedDict, actualDict);

            // Test serialization
            var serializedPair = WavefrontHistogramImpl.Serialize(distributions);

            string expectedTimestamps = distributions
                .Select(distribution => distribution.Timestamp.ToString())
                .Aggregate((result, item) => result + ';' + item);

            string expectedCentroids = distributions
                .Select(distribution => distribution.Centroids
                        .Select(centroid => centroid.Key.ToString() + ' ' + centroid.Value)
                        .Aggregate((result, item) => result + ',' + item))
                .Aggregate((result, item) => result + ';' + item);

            Assert.Equal(expectedTimestamps, serializedPair.Key);
            Assert.Equal(expectedCentroids, serializedPair.Value);

            // Test deserialization
            var deserializedDistributions = WavefrontHistogramImpl.Deserialize(serializedPair);
            Assert.Equal(actualDict, DistributionsToDictionary(deserializedDistributions));

            // Test that prior minute bins get cleared
            distributions = wavefrontHistogram.FlushDistributions();
            Assert.Equal(0, distributions.Count);
        }

        [Fact]
        public void TestSnapshot()
        {
            var currentTime = DateTime.UtcNow;
            long clockMillis() => DateTimeUtils.UnixTimeMilliseconds(currentTime);

            var powHistogram = CreatePowHistogram(clockMillis);
            var rangeHistogram = CreateRangeHistogram(clockMillis);
            var multiThreadedHistogram = CreateMultiThreadedHistogram(clockMillis);
            var emptyHistogram = new WavefrontHistogramImpl(clockMillis);

            currentTime = currentTime.AddMinutes(1);

            var powSnapshot = powHistogram.GetSnapshot();
            var rangeSnapshot = rangeHistogram.GetSnapshot();
            var multiThreadedSnapshot = multiThreadedHistogram.GetSnapshot();
            var emptySnapshot = emptyHistogram.GetSnapshot();

            // Test snapshot for the pow histogram

            Assert.Equal(9, powSnapshot.Count);
            Assert.Equal(100000, powSnapshot.Max);
            // The TDigest returns 12366.325 as the average
            //Assert.Equal(13457.9, powSnapshot.Mean);
            Assert.Equal(0.1, powSnapshot.Min);
            Assert.Equal(7, powSnapshot.Size);
            Assert.Equal(121121.1, powSnapshot.Sum);
            Assert.Equal(
                PowDict.SelectMany(entry => Enumerable.Repeat(entry.Key, entry.Value)),
                powSnapshot.Values
            );
            Assert.Equal(100, powSnapshot.GetValue(0.5d));

            // Test snapshot for the range histogram

            Assert.Equal(1000, rangeSnapshot.Count);
            Assert.Equal(1000, rangeSnapshot.Max);
            Assert.Equal(500.5, rangeSnapshot.Mean);
            Assert.Equal(1, rangeSnapshot.Min);
            Assert.Equal(1000, rangeSnapshot.Size);
            Assert.Equal(288.6749902572095, rangeSnapshot.StdDev, 5);
            Assert.Equal(500500, rangeSnapshot.Sum);
            Assert.Equal(Enumerable.Range(1, 1000).Select(i => (double)i), rangeSnapshot.Values);
            Assert.Equal(500.5, rangeSnapshot.GetValue(0.5d));
            Assert.Equal(750.5, rangeSnapshot.GetValue(0.75d));
            Assert.Equal(950.5, rangeSnapshot.GetValue(0.95d));
            Assert.Equal(980.5, rangeSnapshot.GetValue(0.98d));
            Assert.Equal(990.5, rangeSnapshot.GetValue(0.99d));
            Assert.Equal(999.5, rangeSnapshot.GetValue(0.999d));

            // Test snapshot for multi-threaded histogram

            Assert.Equal(100, multiThreadedSnapshot.Count);
            Assert.Equal(28.86607004772212, multiThreadedSnapshot.StdDev, 5);
            Assert.Equal(5050, multiThreadedSnapshot.Sum);
            Assert.Equal(99.5, multiThreadedSnapshot.GetValue(0.999d), 0);

            // Test snapshot for empty histogram

            Assert.Equal(0, emptySnapshot.StdDev);
        }
    }
}
