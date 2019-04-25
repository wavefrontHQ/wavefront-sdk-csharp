using System;
using System.IO;
using System.Runtime.CompilerServices;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Common.Metrics;

namespace Wavefront.SDK.CSharp.Proxy
{
    /// <summary>
    /// Connection handler class for sending data to a Wavefront proxy listening on a given port.
    /// </summary>
    public class ProxyConnectionHandler : IBufferFlusher
    {
        private readonly string host;
        private readonly int port;
        private volatile ReconnectingSocket reconnectingSocket;

        private readonly WavefrontSdkMetricsRegistry sdkMetricsRegistry;
        private readonly string entityPrefix;
        private readonly WavefrontSdkCounter connectErrors;

        protected internal ProxyConnectionHandler(string host, int port,
            WavefrontSdkMetricsRegistry sdkMetricsRegistry, string entityPrefix)
        {
            this.host = host;
            this.port = port;
            reconnectingSocket = null;

            this.sdkMetricsRegistry = sdkMetricsRegistry;
            this.entityPrefix = string.IsNullOrWhiteSpace(entityPrefix) ? "" : entityPrefix + ".";
            this.sdkMetricsRegistry.Gauge(this.entityPrefix + "errors.count",
                () => GetFailureCount());
            connectErrors = this.sdkMetricsRegistry.Counter(this.entityPrefix + "connect.errors");
        }

        /// <summary>
        /// Connect to the Wavefront proxy.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Connect()
        {
            if (reconnectingSocket != null)
            {
                throw new InvalidOperationException("Already connected");
            }
            try
            {
                reconnectingSocket = new ReconnectingSocket(host, port, sdkMetricsRegistry,
                    entityPrefix + "socket");
            }
            catch (Exception e)
            {
                connectErrors.Inc();
                throw new IOException(e.Message, e);
            }
        }

        /// <see cref="IBufferFlusher.Flush" />
        public void Flush()
        {
            if (IsConnected())
            {
                reconnectingSocket.Flush();
            }
        }

        /// <summary>
        /// Checks whether or not a connection with the Wavefront proxy exists.
        /// </summary>
        /// <returns><c>true</c>, if connected, <c>false</c> otherwise.</returns>
        public bool IsConnected()
        {
            return reconnectingSocket != null;
        }

        /// <see cref="IBufferFlusher.GetFailureCount" />
        public int GetFailureCount()
        {
            return IsConnected() ? reconnectingSocket.GetFailureCount() : 0;
        }

        /// <summary>
        /// Closes the connection to the Wavefront proxy.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Close()
        {
            if (IsConnected())
            {
                reconnectingSocket.Close();
                reconnectingSocket = null;
            }
        }

        /// <summary>
        /// Sends data to the Wavefront proxy.
        /// </summary>
        /// <param name="lineData">The data to be sent, in Wavefront data format.</param>
        public void SendData(string lineData)
        {
            if (!IsConnected())
            {
                try
                {
                    Connect();
                }
                catch (InvalidOperationException)
                {
                    // already connected.
                }
            }

            reconnectingSocket.Write(lineData);
        }
    }
}
