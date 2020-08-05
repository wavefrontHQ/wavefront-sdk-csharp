using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Entities.Histograms;
using Wavefront.SDK.CSharp.Entities.Tracing;
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
            Assert.Equal("\"hello\"", Utils.Sanitize("hello"));
            Assert.Equal("\"hello-world\"", Utils.Sanitize("hello world"));
            Assert.Equal("\"hello.world\"", Utils.Sanitize("hello.world"));
            Assert.Equal("\"hello-world-\"", Utils.Sanitize("hello\"world\""));
            Assert.Equal("\"hello-world\"", Utils.Sanitize("hello'world"));
            Assert.Equal("\"hello-world\"", Utils.Sanitize("hello/world"));
            Assert.Equal("\"~component.heartbeat\"", Utils.Sanitize("~component.heartbeat"));
            Assert.Equal("\"-component.heartbeat\"", Utils.Sanitize("!component.heartbeat"));
            Assert.Equal("\"Δcomponent.heartbeat\"", Utils.Sanitize("Δcomponent.heartbeat"));
            Assert.Equal("\"∆component.heartbeat\"", Utils.Sanitize("∆component.heartbeat"));
        }

        [Fact]
        public void TestSanitizeTagValue()
        {
            Assert.Equal("\"hello\"", Utils.SanitizeTagValue("hello"));
            Assert.Equal("\"hello world\"", Utils.SanitizeTagValue("hello world"));
            Assert.Equal("\"hello.world\"", Utils.SanitizeTagValue("hello.world"));
            Assert.Equal("\"hello\\\"world\\\"\"", Utils.SanitizeTagValue("hello\"world\""));
            Assert.Equal("\"hello'world\"", Utils.SanitizeTagValue("hello'world"));
        }

        [Fact]
        public void TestMetricToLineData()
        {
            Assert.Equal(
                "\"new-york.power.usage\" 42422 1493773500 source=\"localhost\" " +
                "\"datacenter\"=\"dc1\"\n",
                Utils.MetricToLineData(
                    "new-york.power.usage", 42422, 1493773500L, "localhost",
                    new Dictionary<string, string> { { "datacenter", "dc1" } }.ToImmutableDictionary(),
                    "defaultSource"));
            // null timestamp
            Assert.Equal(
                "\"new-york.power.usage\" 42422 source=\"localhost\" \"datacenter\"=\"dc1\"\n",
                Utils.MetricToLineData(
                    "new-york.power.usage", 42422, null, "localhost",
                    new Dictionary<string, string> { { "datacenter", "dc1" } }.ToImmutableDictionary(),
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
            // invalid char in metric name and tag key. whitespace in tag value
            Assert.Equal(
                "\"new-york.power.usage\" 42422 1493773500 source=\"localhost:50050\" \"-key-name-1\"=\"val name 1\"\n",
                Utils.MetricToLineData(
                    "new~york.power.usage", 42422, 1493773500L, "localhost:50050",
                    new Dictionary<string, string> { { " key name~1", " val name 1 " } }.ToImmutableDictionary(),
                    "defaultSource"));
        }

        [Fact]
        public void TestHistogramToLineData()
        {
            Assert.Equal(
                "!M 1493773500 #12 5.1 #1 12 #45 30 \"request.latency\" source=\"appServer1\" " +
                "\"region\"=\"us-west\"\n",
                Utils.HistogramToLineData(
                    "request.latency",
                    ImmutableList.Create(
                        new KeyValuePair<double, int>(5.1, 10),
                        new KeyValuePair<double, int>(5.1, 2),
                        new KeyValuePair<double, int>(12.0, 1),
                        new KeyValuePair<double, int>(30.0, 20),
                        new KeyValuePair<double, int>(30.0, 25)
                    ),
                    ImmutableHashSet.Create(HistogramGranularity.Minute), 1493773500L, "appServer1",
                    new Dictionary<string, string>{ { "region", "us-west"} }.ToImmutableDictionary(),
                    "defaultSource"));

            // null timestamp
            Assert.Equal(
                "!M #12 5.1 #1 12 #45 30 \"request.latency\" source=\"appServer1\" " +
                "\"region\"=\"us-west\"\n",
                Utils.HistogramToLineData(
                    "request.latency",
                    ImmutableList.Create(
                        new KeyValuePair<double, int>(5.1, 10),
                        new KeyValuePair<double, int>(5.1, 2),
                        new KeyValuePair<double, int>(12.0, 1),
                        new KeyValuePair<double, int>(30.0, 20),
                        new KeyValuePair<double, int>(30.0, 25)
                    ),
                    ImmutableHashSet.Create(HistogramGranularity.Minute), null, "appServer1",
                    new Dictionary<string, string> { { "region", "us-west" } }.ToImmutableDictionary(),
                    "defaultSource"));

            // null tags
            Assert.Equal(
                "!M 1493773500 #12 5.1 #1 12 #45 30 \"request.latency\" source=\"appServer1\"\n",
                Utils.HistogramToLineData(
                    "request.latency",
                    ImmutableList.Create(
                        new KeyValuePair<double, int>(5.1, 10),
                        new KeyValuePair<double, int>(5.1, 2),
                        new KeyValuePair<double, int>(12.0, 1),
                        new KeyValuePair<double, int>(30.0, 20),
                        new KeyValuePair<double, int>(30.0, 25)
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
                    new KeyValuePair<double, int>(5.1, 10),
                    new KeyValuePair<double, int>(5.1, 2),
                    new KeyValuePair<double, int>(12.0, 1),
                    new KeyValuePair<double, int>(30.0, 20),
                    new KeyValuePair<double, int>(30.0, 25)
                ),
                ImmutableHashSet.Create<HistogramGranularity>(),
                1493773500L, "appServer1", null, "defaultSource"));

            // multiple granularities
            Assert.Equal(
                "!M 1493773500 #12 5.1 #1 12 #45 30 \"request.latency\" source=\"appServer1\" \"region\"=\"us-west\"\n" +
                "!H 1493773500 #12 5.1 #1 12 #45 30 \"request.latency\" source=\"appServer1\" \"region\"=\"us-west\"\n" +
                "!D 1493773500 #12 5.1 #1 12 #45 30 \"request.latency\" source=\"appServer1\" \"region\"=\"us-west\"\n",
                Utils.HistogramToLineData(
                    "request.latency",
                    ImmutableList.Create(
                        new KeyValuePair<double, int>(5.1, 10),
                        new KeyValuePair<double, int>(5.1, 2),
                        new KeyValuePair<double, int>(12.0, 1),
                        new KeyValuePair<double, int>(30.0, 20),
                        new KeyValuePair<double, int>(30.0, 25)
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

            // span logs tag
            Assert.Equal("\"getAllUsers\" source=\"localhost\" " +
                         "traceId=7b3bf470-9456-11e8-9eb6-529269fb1459 " +
                         "spanId=0313bafe-9457-11e8-9eb6-529269fb1459 " +
                         "\"_spanLogs\"=\"true\" " +
                         "1493773500 343500\n",
                         Utils.TracingSpanToLineData(
                             "getAllUsers", 1493773500L, 343500L, "localhost",
                             new Guid("7b3bf470-9456-11e8-9eb6-529269fb1459"),
                             new Guid("0313bafe-9457-11e8-9eb6-529269fb1459"),
                             null, null, null,
                             ImmutableList.Create(
                                 new SpanLog(1493773500000L, new Dictionary<string, string>())),
                             "defaultSource"));
        }

        [Fact]
        public void TestSpanLogsToLineData()
        {
            var spanLogs = new List<SpanLog>();
            spanLogs.Add(new SpanLog(1493773500123,
                new Dictionary<string, string> { { "event", "error" }, { "event.kind", "\"exception\"" } }.ToImmutableDictionary()));
            spanLogs.Add(new SpanLog(1493773500139,
                new Dictionary<string, string> { { "info", "newline:\n" } }.ToImmutableDictionary()));

            string actual = Utils.SpanLogsToLineData(
                new Guid("7b3bf470-9456-11e8-9eb6-529269fb1459"),
                new Guid("0313bafe-9457-11e8-9eb6-529269fb1459"),
                spanLogs);

#if NET452 || NET46
            Assert.True(
                actual.Equals("{" +
                                  "\"traceId\":\"7b3bf470-9456-11e8-9eb6-529269fb1459\"," +
                                  "\"spanId\":\"0313bafe-9457-11e8-9eb6-529269fb1459\"," +
                                  "\"logs\":[" +
                                    "{" +
                                        "\"timestamp\":1493773500123," +
                                        "\"fields\":{" +
                                            "\"event\":\"error\"," +
                                            "\"event.kind\":\"\\\"exception\\\"\"" +
                                        "}" +
                                    "}," +
                                    "{" +
                                        "\"timestamp\":1493773500139," +
                                        "\"fields\":{" +
                                            "\"info\":\"newline:\\u000a\"" +
                                        "}" +
                                    "}" +
                                  "]," +
                                  "\"span\":null" +
                              "}\n") ||
                actual.Equals("{" +
                                  "\"traceId\":\"7b3bf470-9456-11e8-9eb6-529269fb1459\"," +
                                  "\"spanId\":\"0313bafe-9457-11e8-9eb6-529269fb1459\"," +
                                  "\"logs\":[" +
                                    "{" +
                                        "\"timestamp\":1493773500123," +
                                        "\"fields\":{" +
                                            "\"event.kind\":\"\\\"exception\\\"\"," +
                                            "\"event\":\"error\"" +
                                        "}" +
                                    "}," +
                                    "{" +
                                        "\"timestamp\":1493773500139," +
                                        "\"fields\":{" +
                                            "\"info\":\"newline:\\u000a\"" +
                                        "}" +
                                    "}" +
                                  "]," +
                                  "\"span\":null" +
                              "}\n"));
#else
            Assert.True(
                actual.Equals("{" +
                                  "\"traceId\":\"7b3bf470-9456-11e8-9eb6-529269fb1459\"," +
                                  "\"spanId\":\"0313bafe-9457-11e8-9eb6-529269fb1459\"," +
                                  "\"logs\":[" +
                                    "{" +
                                        "\"timestamp\":1493773500123," +
                                        "\"fields\":{" +
                                            "\"event\":\"error\"," +
                                            "\"event.kind\":\"\\\"exception\\\"\"" +
                                        "}" +
                                    "}," +
                                    "{" +
                                        "\"timestamp\":1493773500139," +
                                        "\"fields\":{" +
                                            "\"info\":\"newline:\\n\"" +
                                        "}" +
                                    "}" +
                                  "]," +
                                  "\"span\":null" +
                              "}\n") ||
                actual.Equals("{" +
                                  "\"traceId\":\"7b3bf470-9456-11e8-9eb6-529269fb1459\"," +
                                  "\"spanId\":\"0313bafe-9457-11e8-9eb6-529269fb1459\"," +
                                  "\"logs\":[" +
                                    "{" +
                                        "\"timestamp\":1493773500123," +
                                        "\"fields\":{" +
                                            "\"event.kind\":\"\\\"exception\\\"\"," +
                                            "\"event\":\"error\"" +
                                        "}" +
                                    "}," +
                                    "{" +
                                        "\"timestamp\":1493773500139," +
                                        "\"fields\":{" +
                                            "\"info\":\"newline:\\n\"" +
                                        "}" +
                                    "}" +
                                  "]," +
                                  "\"span\":null" +
                              "}\n"));
#endif
        }

        [Fact]
        public void TestSpanLogsToLineDataWithSpan()
        {
            var spanLogs = new List<SpanLog>();
            spanLogs.Add(new SpanLog(1493773500123,
                new Dictionary<string, string> { { "event", "error" } }.ToImmutableDictionary()));

            string actual = Utils.SpanLogsToLineData(
                new Guid("7b3bf470-9456-11e8-9eb6-529269fb1459"),
                new Guid("0313bafe-9457-11e8-9eb6-529269fb1459"),
                spanLogs,
                "\"getAllUsers\" source=\"localhost\" " +
                "traceId=7b3bf470-9456-11e8-9eb6-529269fb1459 " +
                "spanId=0313bafe-9457-11e8-9eb6-529269fb1459 " +
                "parent=2f64e538-9457-11e8-9eb6-529269fb1459 " +
                "followsFrom=5f64e538-9457-11e8-9eb6-529269fb1459 " +
                "\"application\"=\"Wavefront\" " +
                "\"http.method\"=\"GET\" 1493773500 343500\n");

#if NET452 || NET46
            Assert.Equal("{" +
                            "\"traceId\":\"7b3bf470-9456-11e8-9eb6-529269fb1459\"," +
                            "\"spanId\":\"0313bafe-9457-11e8-9eb6-529269fb1459\"," +
                            "\"logs\":[" +
                                "{" +
                                    "\"timestamp\":1493773500123," +
                                    "\"fields\":{" +
                                        "\"event\":\"error\"" +
                                    "}" +
                                "}" +
                            "]," +
                            "\"span\":\"\\\"getAllUsers\\\" source=\\\"localhost\\\" " +
                                "traceId=7b3bf470-9456-11e8-9eb6-529269fb1459 " +
                                "spanId=0313bafe-9457-11e8-9eb6-529269fb1459 " +
                                "parent=2f64e538-9457-11e8-9eb6-529269fb1459 " +
                                "followsFrom=5f64e538-9457-11e8-9eb6-529269fb1459 " +
                                "\\\"application\\\"=\\\"Wavefront\\\" " +
                                "\\\"http.method\\\"=\\\"GET\\\" " +
                                "1493773500 343500\\u000a\"" +
                        "}\n", actual);
#else
            Assert.Equal("{" +
                            "\"traceId\":\"7b3bf470-9456-11e8-9eb6-529269fb1459\"," +
                            "\"spanId\":\"0313bafe-9457-11e8-9eb6-529269fb1459\"," +
                            "\"logs\":[" +
                                "{" +
                                    "\"timestamp\":1493773500123," +
                                    "\"fields\":{" +
                                        "\"event\":\"error\"" +
                                    "}" +
                                "}" +
                            "]," +
                            "\"span\":\"\\\"getAllUsers\\\" source=\\\"localhost\\\" " +
                                "traceId=7b3bf470-9456-11e8-9eb6-529269fb1459 " +
                                "spanId=0313bafe-9457-11e8-9eb6-529269fb1459 " +
                                "parent=2f64e538-9457-11e8-9eb6-529269fb1459 " +
                                "followsFrom=5f64e538-9457-11e8-9eb6-529269fb1459 " +
                                "\\\"application\\\"=\\\"Wavefront\\\" " +
                                "\\\"http.method\\\"=\\\"GET\\\" " +
                                "1493773500 343500\\n\"" +
                        "}\n", actual);
#endif
        }

        [Fact]
        public void TestBuildVersion()
        {
            Assert.Equal(0.0D, Utils.GetSemVer(""));

            Assert.Equal(1.0100D, Utils.GetSemVer("1.1.0"));

            Assert.Equal(1.0100D, Utils.GetSemVer("1.1.0-SNAPSHOT"));

            Assert.Equal(1.0101D, Utils.GetSemVer("1.1.1"));

            Assert.Equal(1.1001D, Utils.GetSemVer("1.10.1"));

            Assert.Equal(1.0110D, Utils.GetSemVer("1.1.10"));

            Assert.Equal(1.0001D, Utils.GetSemVer("1.0.1"));

            Assert.Equal(1.0010D, Utils.GetSemVer("1.0.10"));

            Assert.Equal(1.1010D, Utils.GetSemVer("1.10.10"));
        }
    }
}
