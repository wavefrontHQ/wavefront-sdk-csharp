using Wavefront.CSharp.SDK.Entities.Histograms;
using Wavefront.CSharp.SDK.Entities.Metrics;
using Wavefront.CSharp.SDK.Entities.Tracing;

namespace Wavefront.CSharp.SDK.Common
{
    /// <summary>
    /// An uber Wavefront sender interface that abstracts various atom senders along with flushing
    /// and closing logic.
    /// </summary>
    public interface IWavefrontSender
       : IWavefrontMetricSender, IWavefrontHistogramSender, IWavefrontTracingSpanSender,
         IBufferFlusher
    {
        /// <summary>
        /// Closes this client instance.
        /// </summary>
        void Close();
    }
}
