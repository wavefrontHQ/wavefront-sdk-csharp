namespace Wavefront.CSharp.SDK.Common
{
    /// <summary>
    /// Class to define all sdk constants
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Use this format to send metric data to Wavefront.
        /// </summary>
        public static readonly string WavefrontMetricFormat = "wavefront";

        /// <summary>
        /// Use this format to send histogram data to Wavefront.
        /// </summary>
        public static readonly string WavefrontHistogramFormat = "histogram";

        /// <summary>
        /// Use this format to send tracing data to Wavefront.
        /// </summary>
        public static readonly string WavefrontTracingSpanFormat = "trace";
    }
}
