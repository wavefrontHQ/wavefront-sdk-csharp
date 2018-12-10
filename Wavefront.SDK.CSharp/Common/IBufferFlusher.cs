namespace Wavefront.SDK.CSharp.Common
{
    /// <summary>
    /// Buffer flusher that is responsible for flushing the buffer whenever flush method is invoked.  
    /// </summary>
    public interface IBufferFlusher
    {
        /// <summary>
        /// Flushes buffer, if applicable.
        /// </summary>
        void Flush();

        /// <summary>
        /// Returns the number of failed writes to the server.
        /// </summary>
        /// <returns>The number of failed writes to the server.</returns>
        int GetFailureCount();
    }
}
