using System.Collections.Generic;
using System.Timers;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Common.Metrics;
using Wavefront.SDK.CSharp.Entities.Metrics;
using Xunit;

namespace Wavefront.SDK.CSharp.Test
{
    /// <summary>
    /// Unit tests for <see cref="WavefrontSdkMetricsRegistry" />.
    /// </summary>
    public class WavefrontSdkMetricsRegistryTest
    {
        [Fact]
        public void TestGauge()
        {
            var registry = new WavefrontSdkMetricsRegistry.Builder(null)
                .ReportingIntervalSeconds(10000)
                .Build();
            var list = new List<int>();
            var gauge = registry.Gauge("gauge", () => list.Count);
            Assert.Equal(0, gauge.Value);
            list.Add(0);
            Assert.Equal(1, gauge.Value);
        }

        [Fact]
        public void TestCounter()
        {
            var registry = new WavefrontSdkMetricsRegistry.Builder(null)
                .ReportingIntervalSeconds(10000)
                .Build();
            var counter = registry.Counter("counter");
            Assert.Equal(0, counter.Count);
            counter.Inc();
            Assert.Equal(1, counter.Count);
            counter.Inc(2);
            Assert.Equal(3, counter.Count);
            counter.Clear();
            Assert.Equal(0, counter.Count);
            counter.Inc(5);
            Assert.Equal(5, registry.Counter("counter").Count);
            Assert.Equal(0, registry.Counter("counter2").Count);
        }
        public class TestWavefrontSender : IWavefrontMetricSender
        {
            public void SendMetric(string name, double value, long? timestamp, string source,
                               IDictionary<string, string> tags)
            {
            }
        }

        [Fact]
        public void TestDeltaCounter()
        {
            IWavefrontMetricSender testWavefrontSender = new TestWavefrontSender();
            var registry = new WavefrontSdkMetricsRegistry.Builder(testWavefrontSender)
                .ReportingIntervalSeconds(10000)
                .Build();
            var deltaCounter = registry.DeltaCounter("counter");
            Assert.Equal(0, deltaCounter.Count);
            deltaCounter.Inc();
            Assert.Equal(1, deltaCounter.Count);
            deltaCounter.Clear();
            Assert.Equal(0, deltaCounter.Count);
            deltaCounter.Inc(5);
            Assert.Equal(5, deltaCounter.Count);

            /// Delta counter decrements counter each time data is sent.
            deltaCounter.Dec();
            Assert.Equal(4, deltaCounter.Count);
            deltaCounter.Dec(2);
            Assert.Equal(2, deltaCounter.Count);

            /// Verify Delta Counter is reset to 0 after sending
            registry.Run(testWavefrontSender, null);
            Assert.Equal(0, deltaCounter.Count);

        }
    }
}
