using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Wavefront.SDK.CSharp.Entities.Tracing
{
    /// <summary>
    /// Data contract object used to serialize the span logs for a particular span to JSON for
    /// sending to Wavefront.
    /// </summary>
    [DataContract]
    public class SpanLogs
    {
        /// <summary>
        /// Gets the unique trace ID.
        /// </summary>
        /// <value>The trace ID.</value>
        [DataMember(Name = "traceId", IsRequired = true, Order = 1)]
        public Guid TraceId { get; private set; }

        /// <summary>
        /// Gets the unique span ID.
        /// </summary>
        /// <value>The span ID.</value>
        [DataMember(Name = "spanId", IsRequired = true, Order = 2)]
        public Guid SpanId { get; private set; }

        /// <summary>
        /// Gets the list of span logs for a particular span. 
        /// </summary>
        /// <value>The list of span logs.</value>
        [DataMember(Name = "logs", IsRequired = true, Order = 3)]
        public IList<SpanLog> Logs { get; private set; }

        /// <summary>
        /// Gets the corresponding span in Wavefront data format.
        /// </summary>
        /// <value>The span in Wavefront data format.</value>
        [DataMember(Name = "span", IsRequired = false, Order = 4)]
        public string Span { get; private set; }

        public SpanLogs(Guid traceId, Guid spanId, IList<SpanLog> logs, string span = null)
        {
            TraceId = traceId;
            SpanId = spanId;
            Logs = logs;
            Span = span;
        }
    }
}
