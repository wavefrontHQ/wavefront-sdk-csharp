using System;
using System.Collections.Generic;

namespace Wavefront.CSharp.SDK.Entities.Tracing
{
    /// <summary>
    /// Interface for sending an open-tracing span to Wavefront.
    /// </summary>
    public interface IWavefrontTracingSpanSender
    {
        /// <summary>
        /// Send a trace span to Wavefront.
        /// </summary>
        /// <param name="name">The operation name of the span.</param>
        /// <param name="startMillis">The start time in milliseconds for this span.</param>
        /// <param name="durationMillis">The duration of the span in milliseconds.</param>
        /// <param name="source">
        /// The source (or host) that's sending the span. If null, then assigned by Wavefront.
        /// </param>
        /// <param name="traceId">The unique trace ID for the span.</param>
        /// <param name="spanId">The unique span ID for the span.</param>
        /// <param name="parents">The list of parent span IDs, can be null if this is a root span.</param>
        /// <param name="followsFrom">The list of preceding span IDs, can be null if this is a root span.</param>
        /// <param name="tags">The span tags associated with this span. Supports repeated tags.</param>
        /// <param name="spanLogs">The span logs associated with this span.</param>
        void SendSpan(string name, long startMillis, long durationMillis, string source,
                      Guid traceId, Guid spanId, IList<Guid> parents,
                      IList<Guid> followsFrom, IList<KeyValuePair<string, string>> tags,
                      IList<SpanLog> spanLogs);
    }
}
