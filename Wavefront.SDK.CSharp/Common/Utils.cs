using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using Wavefront.SDK.CSharp.Entities.Histograms;
using Wavefront.SDK.CSharp.Entities.Tracing;

namespace Wavefront.SDK.CSharp.Common
{
    /// <summary>
    /// Common Util methods.
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Sanitizes a string to be a valid Wavefront metric name or tag key.
        /// </summary>
        /// <returns>The sanitized string.</returns>
        /// <param name="s">The string to be sanitized.</param>
        public static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool isLegal = true;
                // Legal characters are 44-46 (,-.), 48-57 (numbers), 65-90 (upper),
                // 97-122 (lower), 95 (_)
                if (!(44 <= c && c <= 46) && !(48 <= c && c <= 57) && !(65 <= c && c <= 90) &&
                    !(97 <= c && c <= 122) && c != 95)
                {
                    if (!(i == 0 && (c == 0x2206 || c == 0x0394 || c == 126)))
                    {
                        // First character can also be \u2206 (∆ - INCREMENT)
                        // or \u0394 (Δ - GREEK CAPITAL LETTER DELTA)
                        // or ~ tilda character for internal metrics
                        isLegal = false;
                    }
                }
                sb.Append(isLegal ? c : '-');
            }
            return sb.Append('"').ToString();
        }

        /// <summary>
        /// Sanitizes a string to be a valid Wavefront source or tag value.
        /// </summary>
        /// <returns>The sanitized string.</returns>
        /// <param name="s">The string to be sanitized.</param>
        public static string SanitizeTagValue(string s)
        {
            if (s.Contains("\"") || s.Contains("'"))
            {
                // for single quotes, once we are double-quoted, single quotes can exist happily
                // inside it.
                return "\"" + s.Trim().Replace("\"", "\\\"") + "\"";
            }
            else
            {
                return "\"" + s.Trim() + "\"";
            }
        }

        /// <summary>
        /// Converts a metric point to Wavefront metric data format.
        /// </summary>
        /// <returns>The point in Wavefront data format.</returns>
        /// <param name="name">The name of the metric.</param>
        /// <param name="value">The value of the metric point.</param>
        /// <param name="timestamp">
        /// The timestamp in milliseconds since the epoch. Nullable.
        /// </param>
        /// <param name="source">The source (or host) that is sending the metric.</param>
        /// <param name="tags">The tags associated with the metric.</param>
        /// <param name="defaultSource">
        /// The source to default to if the source parameter is null.
        /// </param>
        public static string MetricToLineData(string name, double value, long? timestamp,
                                              string source, IDictionary<string, string> tags,
                                              string defaultSource)
        {
            /*
             * Wavefront Metrics Data format
             * <metricName> <metricValue> [<timestamp>] source=<source> [pointTags]
             *
             * Example: "new-york.power.usage 42422 1533531013 source=localhost datacenter=dc1"
             */

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("metric name cannot be blank");
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                source = defaultSource;
            }

            var sb = new StringBuilder();
            sb.Append(Sanitize(name));
            sb.Append(' ');
            sb.Append(value);
            if (timestamp.HasValue)
            {
                sb.Append(' ');
                sb.Append(timestamp.Value);
            }
            sb.Append(" source=");
            sb.Append(SanitizeTagValue(source));
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    if (string.IsNullOrWhiteSpace(tag.Key))
                    {
                        throw new ArgumentException("metric point tag key cannot be blank");
                    }
                    if (string.IsNullOrWhiteSpace(tag.Value))
                    {
                        throw new ArgumentException("metric point tag value cannot be blank");
                    }
                    sb.Append(' ');
                    sb.Append(Sanitize(tag.Key));
                    sb.Append('=');
                    sb.Append(SanitizeTagValue(tag.Value));
                }
            }
            sb.Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Converts a histogram distribution to Wavefront distribution data format.
        /// </summary>
        /// <returns>The histogram distribution in Wavefront data format.</returns>
        /// <param name="name">The name of the histogram distribution.</param>
        /// <param name="centroids">
        /// The distribution of histogram points. Each centroid is a
        /// <see cref="KeyValuePair{double, int}"/> where the first dimension is the count of
        /// points in the centroid and second dimension is the count of points in that centroid.
        /// </param>
        /// <param name="histogramGranularities">
        /// The set of intervals (minute, hour, and/or day) by which histogram data should be
        /// aggregated.
        /// </param>
        /// <param name="timestamp">The timestamp in millseconds since the epoch. Nullable.</param>
        /// <param name="source">The source (or host) that is sending the metric.</param>
        /// <param name="tags">The tags associated with the histogram.</param>
        /// <param name="defaultSource">
        /// The source to default to if the source parameter is null.
        /// </param>
        public static string HistogramToLineData(string name,
                                                 IList<KeyValuePair<double, int>> centroids,
                                                 ISet<HistogramGranularity> histogramGranularities,
                                                 long? timestamp,
                                                 string source,
                                                 IDictionary<string, string> tags,
                                                 string defaultSource)
        {
            /*
             * Wavefront Histogram Data format
             * {!M | !H | !D} [<timestamp>] #<count> <mean> [centroids] <histogramName>
             *   source=<source> [pointTags]
             *
             * Example: "!M 1533531013 #20 30 #10 5.1 request.latency source=appServer1
             *   region=us-west"
             */

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("histogram name cannot be blank");
            }
            if (histogramGranularities == null || histogramGranularities.Count == 0)
            {
                throw new ArgumentException("histogram granularities cannot be null or empty");
            }
            if (centroids == null || centroids.Count == 0)
            {
                throw new ArgumentException(
                    "histogram distribution should have at least one centroid"
                );
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                source = defaultSource;
            }

            var sb = new StringBuilder();

            foreach (var histogramGranularity in histogramGranularities)
            {
                if (histogramGranularity == null)
                {
                    throw new ArgumentException("histogram granularity cannot be null");
                }

                sb.Append(histogramGranularity.Identifier);

                if (timestamp.HasValue)
                {
                    sb.Append(' ');
                    sb.Append(timestamp.Value);
                }

                foreach (var centroid in centroids)
                {
                    sb.Append(" #");
                    sb.Append(centroid.Value);
                    sb.Append(' ');
                    sb.Append(centroid.Key);
                }
                sb.Append(' ');
                sb.Append(Sanitize(name));
                sb.Append(" source=");
                sb.Append(SanitizeTagValue(source));
                if (tags != null)
                {
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag.Key))
                        {
                            throw new ArgumentException("histogram tag key cannot be blank");
                        }
                        if (string.IsNullOrWhiteSpace(tag.Value))
                        {
                            throw new ArgumentException("histogram tag value cannot be blank");
                        }
                        sb.Append(' ');
                        sb.Append(Sanitize(tag.Key));
                        sb.Append('=');
                        sb.Append(SanitizeTagValue(tag.Value));
                    }
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Converts an open-tracing span to Wavefront data format.
        /// </summary>
        /// <returns>The trace span in Wavefront data format.</returns>
        /// <param name="name">The operation name of the span.</param>
        /// <param name="startMillis">The start time in milliseconds for this span.</param>
        /// <param name="durationMillis">The duration of the span in milliseconds.</param>
        /// <param name="source">The source (or host) that's sending the span.</param>
        /// <param name="traceId">The unique trace ID for the span.</param>
        /// <param name="spanId">The unique span ID for the span.</param>
        /// <param name="parents">
        /// The list of parent span IDs, can be null if this is a root span.
        /// </param>
        /// <param name="followsFrom">
        /// The list of preceding span IDs, can be null if this is a root span.
        /// </param>
        /// <param name="tags">
        /// The span tags associated with this span. Supports repeated tags.
        /// </param>
        /// <param name="spanLogs">The span logs associated with this span.</param>
        /// <param name="defaultSource">
        /// The source to default to if the source parameter is null.
        /// </param>
        public static string TracingSpanToLineData(string name,
                                                   long startMillis,
                                                   long durationMillis,
                                                   string source,
                                                   Guid traceId,
                                                   Guid spanId,
                                                   IList<Guid> parents,
                                                   IList<Guid> followsFrom,
                                                   IList<KeyValuePair<string, string>> tags,
                                                   IList<SpanLog> spanLogs,
                                                   string defaultSource)
        {
            /*
             * Wavefront Tracing Span Data format
             * <tracingSpanName> source=<source> [pointTags] <start_millis> <duration_milli_seconds>
             *
             * Example: "getAllUsers source=localhost
             *           traceId=7b3bf470-9456-11e8-9eb6-529269fb1459
             *           spanId=0313bafe-9457-11e8-9eb6-529269fb1459
             *           parent=2f64e538-9457-11e8-9eb6-529269fb1459
             *           application=Wavefront http.method=GET
             *           1533531013 343500"
             */
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("span name cannot be blank");
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                source = defaultSource;
            }

            var sb = new StringBuilder();
            sb.Append(Sanitize(name));
            sb.Append(" source=");
            sb.Append(SanitizeTagValue(source));
            sb.Append(" traceId=");
            sb.Append(traceId);
            sb.Append(" spanId=");
            sb.Append(spanId);
            if (parents != null)
            {
                foreach (var parent in parents)
                {
                    sb.Append(" parent=");
                    sb.Append(parent);
                }
            }
            if (followsFrom != null)
            {
                foreach (var item in followsFrom)
                {
                    sb.Append(" followsFrom=");
                    sb.Append(item);
                }
            }
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    if (string.IsNullOrWhiteSpace(tag.Key))
                    {
                        throw new ArgumentException("span tag key cannot be blank");
                    }
                    if (string.IsNullOrWhiteSpace(tag.Value))
                    {
                        throw new ArgumentException("span tag value cannot be blank");
                    }
                    sb.Append(' ');
                    sb.Append(Sanitize(tag.Key));
                    sb.Append('=');
                    sb.Append(SanitizeTagValue(tag.Value));
                }
            }
            if (spanLogs != null && spanLogs.Count > 0)
            {
                sb.Append(' ');
                sb.Append(Sanitize(Constants.SpanLogTagKey));
                sb.Append('=');
                sb.Append(Sanitize("true"));
            }
            sb.Append(' ');
            sb.Append(startMillis);
            sb.Append(' ');
            sb.Append(durationMillis);
            sb.Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Converts the span logs for a particular span to Wavefront data format.
        /// </summary>
        /// <returns>The span logs for a particular span in Wavefront data format.</returns>
        /// <param name="traceId">The unique trace ID for the span.</param>
        /// <param name="spanId">The unique span ID for the span.</param>
        /// <param name="spanLogs">The list of span logs for the span.</param>
        /// <param name="span">The corresponding span in Wavefront data format.</param>
        public static string SpanLogsToLineData(Guid traceId,
                                                Guid spanId,
                                                IList<SpanLog> spanLogs,
                                                string span = null)
        {
            /*
             * Wavefront Span Log Data format
             * Example:
             *  {            
             *      "traceId": "7b3bf470-9456-11e8-9eb6-529269fb1459",
             *      "spanId": "0313bafe-9457-11e8-9eb6-529269fb1459",
             *      "logs": [
             *          {
             *              "timestamp": "1554363517965",
             *              "fields": {
             *                  "event": "error",
             *                  "error.kind": "exception",
             *                  "message": "timed out",
             *                  "stack": "File \"example.py\", line 7, in \<module\>\ncaller()\nFile \"example.py\""
             *              }
             *          }
             *      ]
             *  }
             */
            var spanLogsObject = new SpanLogs(traceId, spanId, spanLogs, span);
            var stream = new MemoryStream();
            var serializerSettings = new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            };
            var serializer = new DataContractJsonSerializer(typeof(SpanLogs), serializerSettings);
            serializer.WriteObject(stream, spanLogsObject);
            byte[] json = stream.ToArray();
            stream.Close();
            return Encoding.UTF8.GetString(json, 0, json.Length) + "\n";
        }

        /// <summary>
        /// Returns the local host name, or "unknown" if the local host name cannot be resolved.
        /// </summary>
        /// <returns>The default source value.</returns>
        public static string GetDefaultSource()
        {
            try
            {
                return Dns.GetHostName();
            }
            catch (Exception)
            {
                return "unknown";
            }
        }
    }
}
