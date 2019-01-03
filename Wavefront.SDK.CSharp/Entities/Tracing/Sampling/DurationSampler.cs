using System.Threading;

namespace Wavefront.SDK.CSharp.Entities.Tracing.Sampling
{
    /// <summary>
    /// Sampler that allows spans above a given duration in milliseconds to be reported.
    /// </summary>
    public class DurationSampler : ISampler
    {
        private long duration;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="duration">
        /// The duration in milliseconds. Spans with durations higher than this are reported.
        /// </param>
        public DurationSampler(long duration)
        {
            SetDuration(duration);
        }

        /// <see cref="ISampler.Sample(string, long, long)"/>
        public bool Sample(string operationName, long traceId, long duration)
        {
            return duration > Interlocked.Read(ref this.duration);
        }

        /// <see cref="ISampler.IsEarly"/>
        public bool IsEarly => false;

        /// <summary>
        /// Sets the duration for this sampler.
        /// </summary>
        /// <param name="duration">The duration in milliseconds.</param>
        public void SetDuration(long duration)
        {
            Interlocked.Exchange(ref this.duration, duration);
        }
    }
}
