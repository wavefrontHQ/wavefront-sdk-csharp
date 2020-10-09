using System.Threading;

namespace Wavefront.SDK.CSharp.Common.Metrics
{
    /// <summary>
    /// A counter used for metrics that are internal to Wavefront SDKs.
    /// </summary>
    public class WavefrontSdkCounter : IWavefrontSdkMetric
    {
        private long count;

        internal WavefrontSdkCounter()
        {
        }

        /// <summary>
        /// Gets the counter's current value.
        /// </summary>
        public long Count
        {
            get
            {
                return Interlocked.Read(ref count);
            }
        }

        /// <summary>
        /// Increments the counter by one.
        /// </summary>
        public void Inc()
        {
            Inc(1);
        }

        /// <summary>
        /// Increments the counter by the specified amount.
        /// </summary>
        /// <param name="n">The amount to increment by.</param>
        public void Inc(long n)
        {
            Interlocked.Add(ref count, n);
        }

        /// <summary>
        /// Decrements the counter by one.
        /// </summary>
        public void Dec()
        {
            Dec(1);
        }

        /// <summary>
        /// Decrements the counter by the specified amount.
        /// </summary>
        /// <param name="n">The amount to decrement by.</param>
        public void Dec(long n)
        {
            Interlocked.Add(ref count, -n);
        }

        /// <summary>
        /// Resets the counter's value to 0.
        /// </summary>
        public void Clear()
        {
            Interlocked.Exchange(ref count, 0);
        }
    }
}
