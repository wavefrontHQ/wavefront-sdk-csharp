using System.Collections.Generic;

namespace Wavefront.CSharp.SDK.Entities.Histograms
{
    /// <summary>
    /// Interface for sending a distribution to Wavefront.
    /// </summary>
    public interface IWavefrontHistogramSender
    {
        /// <summary>
        /// Sends a distribution to Wavefront.
        /// </summary>
        /// <param name="name">
        /// The name of the histogram distribution. Spaces are replaced with '-' (dashes) and quotes will be
        /// automatically escaped.
        /// </param>
        /// <param name="centroids">
        /// The distribution of histogram points to be sent. Each centroid is a 2-dimensional
        /// <see cref="KeyValuePair{double, int}"/> where the first dimension is the mean value(Double) of the
        /// centroid and second dimension is the count of points in that centroid.
        /// </param>
        /// <param name="histogramGranularities">
        /// The set of intervals (minute, hour, and/or day) by which histogram data should be aggregated.
        /// </param>
        /// <param name="timestamp">
        /// The timestamp in milliseconds since the epoch to be sent. If null then the timestamp is assigned by
        /// Wavefront when data is received.
        /// </param>
        /// <param name="source">
        /// The source (or host) that's sending the histogram. If null, then assigned by Wavefront.
        /// </param>
        /// <param name="tags">The tags associated with this histogram.</param>
        void SendDistribution(string name, IList<KeyValuePair<double, int>> centroids,
                              ISet<HistogramGranularity> histogramGranularities, long? timestamp,
                              string source, IDictionary<string, string> tags);
    }
}
