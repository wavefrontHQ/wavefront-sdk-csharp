using System.Collections.Generic;

namespace Wavefront.SDK.CSharp.Entities.Tracing
{
    /// <summary>
    /// SpanLog defined as per the opentracing.io specification
    /// </summary>
    public class SpanLog
    {
        /// <summary>
        ///     Epoch timestamp in microseconds.
        /// </summary>
        /// <value>The epoch timestamp in microseconds.</value>
        public long TimestampMicros { get; private set; }

        /// <summary>
        ///     Dictionary of fields associated with the span log.
        /// </summary>
        /// <value>The dictionary of fields.</value>
        public IDictionary<string, string> Fields { get; private set; }

        public SpanLog(long timestampMicros, IDictionary<string, string> fields)
        {
            TimestampMicros = timestampMicros;
            Fields = fields;
        }
    }
}
