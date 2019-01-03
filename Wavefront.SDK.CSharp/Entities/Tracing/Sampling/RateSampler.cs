using System;
using System.Threading;

namespace Wavefront.SDK.CSharp.Entities.Tracing.Sampling
{
    /// <summary>
    /// Sampler that allows a certain probabilistic rate (between 0.0 and 1.0) of spans to be
    /// reported.
    /// 
    /// Note: Sampling is performed per trace id. All spans for a sampled trace will be reported.
    /// </summary>
    public class RateSampler : ISampler
    {
        private static readonly double MinSamplingRate = 0.0;
        private static readonly double MaxSamplingRate = 1.0;
        private static readonly long ModFactor = 10000L;

        private long boundary;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="samplingRate">A sampling rate between 0.0 and 1.0.</param>
        public RateSampler(double samplingRate)
        {
            SetSamplingRate(samplingRate);
        }

        /// <see cref="ISampler.Sample(string, long, long)"/>
        public bool Sample(string operationName, long traceId, long duration)
        {
            return Math.Abs(traceId % ModFactor) <= Interlocked.Read(ref boundary);
        }

        /// <see cref="ISampler.IsEarly"/>
        public bool IsEarly => true;

        /// <summary>
        /// Sets the sampling rate for this sampler.
        /// </summary>
        /// <param name="samplingRate">A sampling rate between 0.0 and 1.0.</param>
        public void SetSamplingRate(double samplingRate)
        {
            if (samplingRate < MinSamplingRate || samplingRate > MaxSamplingRate)
            {
                throw new ArgumentOutOfRangeException(
                    $"sampling rate must be between {MinSamplingRate} and {MaxSamplingRate}");
            }
            Interlocked.Exchange(ref boundary, (long)(samplingRate * ModFactor));
        }
    }
}
