using System.Text.RegularExpressions;

namespace Wavefront.SDK.CSharp.Common
{
    /// <summary>
    /// Class to define all sdk constants
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Use this format to send metric data to Wavefront.
        /// </summary>
        public const string WavefrontMetricFormat = "wavefront";

        /// <summary>
        /// Use this format to send histogram data to Wavefront.
        /// </summary>
        public const string WavefrontHistogramFormat = "histogram";

        /// <summary>
        /// Use this format to send tracing spans to Wavefront.
        /// </summary>
        public const string WavefrontTracingSpanFormat = "trace";

        /// <summary>
        /// Use this format to send span logs to Wavefront.
        /// </summary>
        public const string WavefrontSpanLogsFormat = "spanLogs";

        /// <summary>
        /// ∆: INCREMENT
        /// </summary>
        public const string DeltaPrefix = "\u2206";

        /// <summary>
        /// Δ: GREEK CAPITAL LETTER DELTA
        /// </summary>
        public const string DeltaPrefix2 = "\u0394";

        /// <summary>
        /// Heartbeat metric
        /// </summary>
        public const string HeartbeatMetric = "~component.heartbeat";

        /// <summary>
        /// Internal source used for internal and aggregated metrics.
        /// </summary>
        public const string WavefrontProvidedSource = "wavefront-provided";

        /// <summary>
        /// Null value emitted for optional undefined tags.
        /// </summary>
        public const string NullTagValue = "none";

        /// <summary>
        /// Key for defining a source.
        /// </summary>
        public const string SourceTagKey = "source";

        /// <summary>
        /// Tag key for defining an application.
        /// </summary>
        public const string ApplicationTagKey = "application";

        /// <summary>
        /// Tag key for defining a cluster.
        /// </summary>
        public const string ClusterTagKey = "cluster";

        /// <summary>
        /// Tag key for defining a shard.
        /// </summary>
        public const string ShardTagKey = "shard";

        /// <summary>
        /// Tag key for defining a service.
        /// </summary>
        public const string ServiceTagKey = "service";

        /// <summary>
        /// Tag key for defining a component.
        /// </summary>
        public const string ComponentTagKey = "component";

        /// <summary>
        /// Tag key for indicating span logs are present for a span.
        /// </summary>
        public const string SpanLogTagKey = "_spanLogs";

        /// <summary>
        /// Tag key for defining a process identifier.
        /// </summary>
        public const string ProcessTagKey = "pid";

        /// <summary>
        /// Name prefix for internal diagnostic metrics for Wavefront SDKs.
        /// </summary>
        public const string SdkMetricPrefix = "~sdk.csharp";

        /// <summary>
        /// Name of the HTTP header indicating that the request should be ignored
        /// by Wavefront instrumentation.
        /// </summary>
        public const string WavefrontIgnoreHeader = "X-WF-IGNORE";

        /// <summary>
        /// Semantic version matcher regex.
        /// </summary>
        public static readonly Regex SemverRegex =
            new Regex("([0-9]\\d*)\\.(\\d+)\\.(\\d+)(?:-([a-zA-Z0-9]+))?");

        /// <summary>
        /// Default http status code for sending points.
        /// </summary>
        public static readonly int HttpNoResponse = -1;
    }
}
