namespace Wavefront.SDK.CSharp.Entities.Tracing.Sampling
{
    public interface ISampler
    {
        /// <summary>
        /// Gets whether a span should be allowed given its operation and trace id.
        /// </summary>
        /// <returns><c>true</c> if the span should be allowed, <c>false</c> otherwise.</returns>
        /// <param name="operationName">The operation name of the span.</param>
        /// <param name="traceId">The trace id of the span.</param>
        /// <param name="duration">The duration of the span in milliseconds.</param>
        bool Sample(string operationName, long traceId, long duration);

        /// <summary>
        /// Whether this sampler performs early or head based sampling.
        /// Offers a non-binding hint for clients using the sampler.
        /// </summary>
        /// <value><c>true</c> if is early; otherwise, <c>false</c>.</value>
        bool IsEarly { get; }
    }
}
