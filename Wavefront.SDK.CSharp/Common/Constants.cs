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
        /// Use this format to send tracing data to Wavefront.
        /// </summary>
        public const string WavefrontTracingSpanFormat = "trace";

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

    }
}
