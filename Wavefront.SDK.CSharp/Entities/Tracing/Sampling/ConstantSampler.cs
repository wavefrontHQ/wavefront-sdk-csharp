namespace Wavefront.SDK.CSharp.Entities.Tracing.Sampling
{
    /// <summary>
    /// Sampler that allows spans through at a constant rate (all in or all out).
    /// </summary>
    public class ConstantSampler : ISampler
    {
        private volatile bool decision;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="decision">
        /// <c>true</c> to allow all spans through, <c>false</c> to disallow all.
        /// </param>
        public ConstantSampler(bool decision)
        {
            this.decision = decision;
        }

        /// <see cref="ISampler.Sample(string, long, long)"/>
        public bool Sample(string operationName, long traceId, long duration)
        {
            return decision;
        }

        /// <see cref="ISampler.IsEarly"/>
        public bool IsEarly => true;

        /// <summary>
        /// Sets the decision for this sampler.
        /// </summary>
        /// <param name="decision">The sampling decision.</param>
        public void SetDecision(bool decision)
        {
            this.decision = decision;
        }
    }
}
