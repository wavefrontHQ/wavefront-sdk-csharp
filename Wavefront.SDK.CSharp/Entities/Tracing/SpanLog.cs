using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Wavefront.SDK.CSharp.Entities.Tracing
{
    /// <summary>
    /// SpanLog defined as per the opentracing.io specification
    /// </summary>
    [DataContract]
    public class SpanLog
    {
        /// <summary>
        /// Gets the epoch timestamp in microseconds.
        /// </summary>
        /// <value>The epoch timestamp in microseconds.</value>
        [DataMember(Name = "timestamp", IsRequired = true, Order = 1)]
        public long TimestampMicros { get; private set; }

        /// <summary>
        /// Gets the dictionary of fields associated with the span log.
        /// </summary>
        /// <value>The dictionary of fields.</value>
        [DataMember(Name = "fields", IsRequired = true, Order = 2)]
        public IDictionary<string, string> Fields { get; private set; }

        public SpanLog(long timestampMicros, IDictionary<string, string> fields)
        {
            TimestampMicros = timestampMicros;
            Fields = fields;
        }
    }
}
