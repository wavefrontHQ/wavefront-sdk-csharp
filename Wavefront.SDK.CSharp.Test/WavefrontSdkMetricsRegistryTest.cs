using System.Collections.Generic;
using Wavefront.SDK.CSharp.Common.Metrics;
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
    }
}
