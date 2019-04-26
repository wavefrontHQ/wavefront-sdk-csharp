using System;

namespace Wavefront.SDK.CSharp.Common.Metrics
{
    /// <summary>
    /// A gauge used for metrics that are internal to Wavefront SDKs.
    /// </summary>
    public class WavefrontSdkGauge : IWavefrontSdkMetric
    {
        private readonly Func<double> valueDelegate;
        
        internal WavefrontSdkGauge(Func<double> valueDelegate)
        {
            this.valueDelegate = valueDelegate;
        }

        /// <summary>
        /// Gets the gauge's current value.
        /// </summary>
        public double Value
        {
            get
            {
                return valueDelegate();
            }
        }
    }
}
