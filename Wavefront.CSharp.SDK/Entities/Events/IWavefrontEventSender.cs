using System.Collections.Generic;

namespace Wavefront.CSharp.SDK.Entities.Events
{
    /// <summary>
    /// Interface for sending an event to Wavefront.
    /// </summary>
    public interface IWavefrontEventSender
    {
        /// <summary>
        /// Send an event to Wavefront.
        /// </summary>
        /// <param name="name">
        /// The name of the event. Spaces are replaced with '-' (dashes) and quotes will be automatically escaped.
        /// </param>
        /// <param name="startMillis">The timestamp in milliseconds when the event was started.</param>
        /// <param name="endMillis">The timestamp in milliseconds when the event was ended.</param>
        /// <param name="source">
        /// The source (or host) that's sending the event. If null, then assigned by Wavefront.
        /// </param>
        /// <param name="tags">The tags associated with this event.</param>
        void SendEvent(string name, long startMillis, long endMillis, string source, IDictionary<string, string> tags);
    }
}
