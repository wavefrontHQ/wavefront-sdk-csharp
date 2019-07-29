using System;
using System.Collections.Generic;
using Wavefront.SDK.CSharp.Common;

namespace Wavefront.SDK.CSharp.Entities.Metrics
{
    /// <summary>
    /// Extension methods for <see cref="IWavefrontMetricSender"/>.
    /// </summary>
    public static class WavefrontMetricSenderExtensions
    {
        /// <summary>
        /// Sends the given delta counter to Wavefront. The timestamp for the point on the client
        /// side is null because the final timestamp of the delta counter is assigned when the
        /// point is aggregated on the server side. Do not use this method to send older points
        /// (say around 5 min old) as they will be aggregated on server with the current timestamp
        /// which yields in a wrong final aggregated value.
        /// </summary>
        /// <param name="sender">
        /// The instance that implements <see cref="IWavefrontMetricSender"/>.
        /// </param>
        /// <param name="name">
        /// The name of the delta counter. Name will be prefixed by ∆ if it does not start with
        /// that symbol already. Also, spaces are replaced with '-' (dashes) and quotes will be
        /// automatically escaped.
        /// </param>
        /// <param name="value">
        /// The delta value to be sent. This will be aggregated on the Wavefront server side.
        /// Non-positive delta values are ignored.
        /// </param>
        /// <param name="source">
        /// The source (or host) that's sending the metric. If null then assigned by Wavefront.
        /// </param>
        /// <param name="tags">The tags associated with this metric.</param>
        public static void SendDeltaCounter(this IWavefrontMetricSender sender,
                                            string name, double value, string source,
                                            IDictionary<string, string> tags)
        {
            if (value <= 0)
            {
                // Delta counters cannot be non-positive
                return;
            }

            if (!name.StartsWith(Constants.DeltaPrefix, StringComparison.Ordinal) &&
                !name.StartsWith(Constants.DeltaPrefix2, StringComparison.Ordinal))
            {
                name = Constants.DeltaPrefix + name;
            }
            sender.SendMetric(name, value, null, source, tags);
        }
    }
}
