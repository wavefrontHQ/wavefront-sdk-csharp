using System.Collections.Generic;

namespace Wavefront.SDK.CSharp.Entities.Tracing.Sampling
{
    /// <summary>
    /// Sampler that delegates to multiple other samplers for sampling.
    /// The sampling decision is true if any of the delegate samplers allow the span.
    /// </summary>
    public class CompositeSampler : ISampler
    {
        private readonly IList<ISampler> samplers;

        public CompositeSampler(IList<ISampler> samplers)
        {
            this.samplers = samplers;
        }

        /// <see cref="ISampler.Sample(string, long, long)"/>
        public bool Sample(string operationName, long traceId, long duration)
        {
            if (samplers == null || samplers.Count == 0)
            {
                return true;
            }
            foreach (var sampler in samplers)
            {
                if (sampler.Sample(operationName, traceId, duration))
                {
                    return true;
                }
            }
            return false;
        }

        /// <see cref="ISampler.IsEarly"/>
        public bool IsEarly => false;
    }
}
