using System;
using System.Collections.Generic;
using Wavefront.CSharp.SDK.Entities.Histograms;
using Wavefront.CSharp.SDK.Entities.Metrics;
using Wavefront.CSharp.SDK.Entities.Tracing;

namespace Wavefront.CSharp.SDK.Common
{
    /// <summary>
    /// Abstract base class for a client that handles sending data to Wavefront.
    /// </summary>
    public abstract class WavefrontClient
        : IWavefrontMetricSender, IWavefrontHistogramSender, IWavefrontTracingSpanSender, IBufferFlusher
    {
        /// <see cref="IWavefrontMetricSender.SendMetric"/>
        public abstract void SendMetric(string name, double value, long? timestamp, string source,
                               IDictionary<string, string> tags);

        /// <see cref="IWavefrontHistogramSender.SendDistribution"/>
        public abstract void SendDistribution(string name, IList<KeyValuePair<double, int>> centroids,
                                     ISet<HistogramGranularity> histogramGranularities, long? timestamp,
                                     string source, IDictionary<string, string> tags);

        /// <see cref="IWavefrontTracingSpanSender.SendSpan"/>
        public abstract void SendSpan(string name, long startMillis, long durationMillis, string source,
                             Guid traceId, Guid spanId, IList<Guid> parents,
                             IList<Guid> followsFrom, IList<KeyValuePair<string, string>> tags,
                             IList<SpanLog> spanLogs);

        /// <see cref="IBufferFlusher.Flush" />
        public abstract void Flush();

        /// <see cref="IBufferFlusher.GetFailureCount" />
        public abstract int GetFailureCount();

        /// <summary>
        /// Closes this client instance.
        /// </summary>
        public abstract void Close();
    }
}
