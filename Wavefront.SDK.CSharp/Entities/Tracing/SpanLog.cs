using System.Collections.Generic;

namespace Wavefront.SDK.CSharp.Entities.Tracing
{
    /// <summary>
    /// SpanLog defined as per the opentracing.io specification
    /// </summary>
    public class SpanLog
    {
        private readonly long timestamp;
        private readonly IDictionary<string, string> fields;

        public SpanLog(long timestamp, IDictionary<string, string> fields)
        {
            this.timestamp = timestamp;
            this.fields = fields;
        }
    }
}
