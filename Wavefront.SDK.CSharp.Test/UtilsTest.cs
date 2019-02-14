using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Entities.Histograms;
using Xunit;

namespace Wavefront.SDK.CSharp.Test
{
    /// <summary>
    /// Unit tests for <see cref="Utils"/>.
    /// </summary>
    public class UtilsTest
    {
        [Fact]
        public void TestSanitize()
        {
            Assert.Equal(@"""hello""", Utils.Sanitize("hello"));
            Assert.Equal(@"""hello-world""", Utils.Sanitize("hello world"));
            Assert.Equal(@"""hello.world""", Utils.Sanitize("hello.world"));
            Assert.Equal(@"""hello\""world\""""", Utils.Sanitize("hello\"world\""));
            Assert.Equal(@"""hello'world""", Utils.Sanitize("hello'world"));
        }

        [Fact]
        public void TestMetricToLineData()
        {
            Assert.Equal(
                "\"new-york.power.usage\" 42422 1493773500 source=\"localhost\" " +
                "\"datacenter\"=\"dc1\"\n",
                Utils.MetricToLineData(
                    "new-york.power.usage", 42422, 1493773500L, "localhost",
                    new Dictionary<string, string>{ { "datacenter", "dc1"} }.ToImmutableDictionary(),
                    "defaultSource"));
            // null timestamp
            Assert.Equal(
                "\"new-york.power.usage\" 42422 source=\"localhost\" \"datacenter\"=\"dc1\"\n",
                Utils.MetricToLineData(
                    "new-york.power.usage", 42422, null, "localhost",
                    new Dictionary<string, string>{ { "datacenter", "dc1"} }.ToImmutableDictionary(),
                    "defaultSource"));
            // null tags
            Assert.Equal(
                "\"new-york.power.usage\" 42422 1493773500 source=\"localhost\"\n",
                Utils.MetricToLineData("new-york.power.usage", 42422, 1493773500L, "localhost",
                                       null, "defaultSource"));
            // null tags and null timestamp
            Assert.Equal(
                "\"new-york.power.usage\" 42422 source=\"localhost\"\n",
                Utils.MetricToLineData("new-york.power.usage", 42422, null, "localhost", null,
                                       "defaultSource"));
        }

        [Fact]
        public void TestHistogramToLineData()
        {
            Assert.Equal(
                "!M 1493773500 #20 30 #10 5.1 \"request.latency\" source=\"appServer1\" " +
                "\"region\"=\"us-west\"\n",
                Utils.HistogramToLineData(
                    "request.latency",
                    ImmutableList.Create(
                        new KeyValuePair<double, int>(30.0, 20),
                        new KeyValuePair<double, int>(5.1, 10)
                    ),
                    ImmutableHashSet.Create(HistogramGranularity.Minute), 1493773500L, "appServer1",
                    new Dictionary<string, string>{ { "region", "us-west"} }.ToImmutableDictionary(),
                    "defaultSource"));

            // null timestamp
            Assert.Equal(
                "!M #20 30 #10 5.1 \"request.latency\" source=\"appServer1\" " +
                "\"region\"=\"us-west\"\n",
                Utils.HistogramToLineData(
                    "request.latency",
                    ImmutableList.Create(
                        new KeyValuePair<double, int>(30.0, 20),
                        new KeyValuePair<double, int>(5.1, 10)
                    ),
                    ImmutableHashSet.Create(HistogramGranularity.Minute), null, "appServer1",
                    new Dictionary<string, string> { { "region", "us-west" } }.ToImmutableDictionary(),
                    "defaultSource"));

            // null tags
            Assert.Equal(
                "!M 1493773500 #20 30 #10 5.1 \"request.latency\" source=\"appServer1\"\n",
                Utils.HistogramToLineData(
                    "request.latency",
                    ImmutableList.Create(
                        new KeyValuePair<double, int>(30.0, 20),
                        new KeyValuePair<double, int>(5.1, 10)
                    ),
                    ImmutableHashSet.Create(HistogramGranularity.Minute),
                    1493773500L, "appServer1", null, "defaultSource"));

            // empty centroids
            Assert.Throws<ArgumentException>(() => Utils.HistogramToLineData(
                "request.latency",
                ImmutableList.Create<KeyValuePair<double, int>>(),
                ImmutableHashSet.Create(HistogramGranularity.Minute),
                1493773500L, "appServer1", null, "defaultSource"));

            // no histogram granularity specified
            Assert.Throws<ArgumentException>(() => Utils.HistogramToLineData(
                "request.latency",
                ImmutableList.Create(
                    new KeyValuePair<double, int>(30.0, 20),
                    new KeyValuePair<double, int>(5.1, 10)
                ),
                ImmutableHashSet.Create<HistogramGranularity>(),
                1493773500L, "appServer1", null, "defaultSource"));

            // multiple granularities
            Assert.Equal(
                "!M 1493773500 #20 30 #10 5.1 \"request.latency\" source=\"appServer1\" \"region\"=\"us-west\"\n" +
                "!H 1493773500 #20 30 #10 5.1 \"request.latency\" source=\"appServer1\" \"region\"=\"us-west\"\n" +
                "!D 1493773500 #20 30 #10 5.1 \"request.latency\" source=\"appServer1\" \"region\"=\"us-west\"\n",
                Utils.HistogramToLineData(
                    "request.latency",
                    ImmutableList.Create(
                        new KeyValuePair<double, int>(30.0, 20),
                        new KeyValuePair<double, int>(5.1, 10)
                    ),
                    ImmutableSortedSet.Create(
                        HistogramGranularity.Minute,
                        HistogramGranularity.Hour,
                        HistogramGranularity.Day
                    ),
                    1493773500L, "appServer1",
                    new Dictionary<string, string> { { "region", "us-west" } }.ToImmutableDictionary(),
                    "defaultSource"
                ));
        }

        [Fact]
        public void TestTracingSpanToLineData()
        {
            Assert.Equal("\"getAllUsers\" source=\"localhost\" " +
                         "traceId=7b3bf470-9456-11e8-9eb6-529269fb1459 " +
                         "spanId=0313bafe-9457-11e8-9eb6-529269fb1459 " +
                         "parent=2f64e538-9457-11e8-9eb6-529269fb1459 " +
                         "followsFrom=5f64e538-9457-11e8-9eb6-529269fb1459 " +
                         "\"application\"=\"Wavefront\" " +
                         "\"http.method\"=\"GET\" 1493773500 343500\n",
                         Utils.TracingSpanToLineData(
                             "getAllUsers", 1493773500L, 343500L, "localhost",
                             new Guid("7b3bf470-9456-11e8-9eb6-529269fb1459"),
                             new Guid("0313bafe-9457-11e8-9eb6-529269fb1459"),
                             ImmutableList.Create(new Guid("2f64e538-9457-11e8-9eb6-529269fb1459")),
                             ImmutableList.Create(new Guid("5f64e538-9457-11e8-9eb6-529269fb1459")),
                             ImmutableList.Create(
                                 new KeyValuePair<string, string>("application", "Wavefront"),
                                 new KeyValuePair<string, string>("http.method", "GET")
                             ),
                             null, "defaultSource"));

            // null followsFrom
            Assert.Equal("\"getAllUsers\" source=\"localhost\" " +
                         "traceId=7b3bf470-9456-11e8-9eb6-529269fb1459 " +
                         "spanId=0313bafe-9457-11e8-9eb6-529269fb1459 " +
                         "parent=2f64e538-9457-11e8-9eb6-529269fb1459 " +
                         "\"application\"=\"Wavefront\" " +
                         "\"http.method\"=\"GET\" 1493773500 343500\n",
                         Utils.TracingSpanToLineData(
                             "getAllUsers", 1493773500L, 343500L, "localhost",
                             new Guid("7b3bf470-9456-11e8-9eb6-529269fb1459"),
                             new Guid("0313bafe-9457-11e8-9eb6-529269fb1459"),
                             ImmutableList.Create(new Guid("2f64e538-9457-11e8-9eb6-529269fb1459")),
                             null,
                             ImmutableList.Create(
                                 new KeyValuePair<string, string>("application", "Wavefront"),
                                 new KeyValuePair<string, string>("http.method", "GET")
                             ),
                             null, "defaultSource"));

            // root span
            Assert.Equal("\"getAllUsers\" source=\"localhost\" " +
                         "traceId=7b3bf470-9456-11e8-9eb6-529269fb1459 " +
                         "spanId=0313bafe-9457-11e8-9eb6-529269fb1459 " +
                         "\"application\"=\"Wavefront\" " +
                         "\"http.method\"=\"GET\" 1493773500 343500\n",
                         Utils.TracingSpanToLineData(
                             "getAllUsers", 1493773500L, 343500L, "localhost",
                             new Guid("7b3bf470-9456-11e8-9eb6-529269fb1459"),
                             new Guid("0313bafe-9457-11e8-9eb6-529269fb1459"),
                             null, null,
                             ImmutableList.Create(
                                 new KeyValuePair<string, string>("application", "Wavefront"),
                                 new KeyValuePair<string, string>("http.method", "GET")
                             ),
                             null, "defaultSource"));

            // null tags
            Assert.Equal("\"getAllUsers\" source=\"localhost\" " +
                         "traceId=7b3bf470-9456-11e8-9eb6-529269fb1459 " +
                         "spanId=0313bafe-9457-11e8-9eb6-529269fb1459 " +
                         "1493773500 343500\n",
                         Utils.TracingSpanToLineData(
                             "getAllUsers", 1493773500L, 343500L, "localhost",
                             new Guid("7b3bf470-9456-11e8-9eb6-529269fb1459"),
                             new Guid("0313bafe-9457-11e8-9eb6-529269fb1459"),
                             null, null, null, null, "defaultSource"));
        }
    }
}
