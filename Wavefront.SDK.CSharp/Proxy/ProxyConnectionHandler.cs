using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
        private readonly ILoggerFactory loggerFactory;
        private volatile ReconnectingSocket reconnectingSocket;

        private readonly WavefrontSdkMetricsRegistry sdkMetricsRegistry;
        private readonly string entityPrefix;
        private readonly WavefrontSdkCounter errors;
        private readonly WavefrontSdkCounter connectErrors;

        protected internal ProxyConnectionHandler(string host, int port,
            WavefrontSdkMetricsRegistry sdkMetricsRegistry, string entityPrefix)
            : this(host, port, sdkMetricsRegistry, entityPrefix, Logging.LoggerFactory)
        { }

        protected internal ProxyConnectionHandler(string host, int port,
            WavefrontSdkMetricsRegistry sdkMetricsRegistry, string entityPrefix,
            ILoggerFactory loggerFactory)
        {
            this.host = host;
            this.port = port;
            this.loggerFactory = loggerFactory;
            reconnectingSocket = null;

            this.sdkMetricsRegistry = sdkMetricsRegistry;
            this.entityPrefix = string.IsNullOrWhiteSpace(entityPrefix) ? "" : entityPrefix + ".";
            errors = this.sdkMetricsRegistry.DeltaCounter(this.entityPrefix + "errors");
            connectErrors = this.sdkMetricsRegistry.DeltaCounter(this.entityPrefix + "connect.errors");
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
                    entityPrefix + "socket", loggerFactory);
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
                _ = reconnectingSocket.FlushAsync();
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
            return (int)errors.Count;
        }

        /// <summary>
        /// Increments the failure count by one.
        /// </summary>
        public void IncrementFailureCount()
        {
            errors.Inc();
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
        public async Task SendDataAsync(string lineData)
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

            await reconnectingSocket.WriteAsync(lineData);
        }
    }
}
