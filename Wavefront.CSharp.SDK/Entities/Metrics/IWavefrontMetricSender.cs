using System.Collections.Generic;

namespace Wavefront.CSharp.SDK.Entities.Metrics
{
    /// <summary>
    /// Interface for sending a metric to Wavefront.
    /// </summary>
    public interface IWavefrontMetricSender
    {
        /// <summary>
        /// Sends a metric to Wavefront.
        /// </summary>
        /// <param name="name">
        /// The name of the metric. Spaces are replaced with '-' (dashes) and quotes will be
        /// automatically escaped.
        /// </param>
        /// <param name="value">The value to be sent.</param>
        /// <param name="timestamp">
        /// The timestamp in milliseconds since the epoch to be sent. If null then the timestamp is
        /// assigned by Wavefront when data is received.
        /// </param>
        /// <param name="source">
        /// The source (or host) that's sending the metric. If null then assigned by Wavefront.
        /// </param>
        /// <param name="tags">The tags associated with this metric.</param>
        void SendMetric(string name, double value, long? timestamp, string source,
                        IDictionary<string, string> tags);
    }
}
