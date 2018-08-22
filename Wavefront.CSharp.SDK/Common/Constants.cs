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
    }
}
