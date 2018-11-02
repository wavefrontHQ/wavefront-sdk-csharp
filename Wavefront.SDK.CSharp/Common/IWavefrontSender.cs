using Wavefront.SDK.CSharp.Entities.Histograms;
using Wavefront.SDK.CSharp.Entities.Metrics;
using Wavefront.SDK.CSharp.Entities.Tracing;

namespace Wavefront.SDK.CSharp.Common
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
