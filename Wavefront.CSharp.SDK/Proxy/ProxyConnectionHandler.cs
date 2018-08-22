using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Wavefront.CSharp.SDK.Common;

namespace Wavefront.CSharp.SDK.Integrations
{
    /// <summary>
    /// Connection handler class for sending data to a Wavefront proxy listening on a given port.
    /// </summary>
    public class ProxyConnectionHandler : IBufferFlusher
    {
        private readonly string host;
        private readonly int port;
        private volatile ReconnectingSocket reconnectingSocket;
        private volatile int failures;

        protected internal ProxyConnectionHandler(string host, int port)
        {
            this.host = host;
            this.port = port;
            reconnectingSocket = null;
            failures = 0;
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
                reconnectingSocket = new ReconnectingSocket(host, port);
            }
            catch (Exception e)

            {
                throw new IOException(e.Message, e);
            }
        }

        /// <see cref="IBufferFlusher.Flush" />
        public void Flush()
        {
            if (reconnectingSocket != null)
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
            return failures;
        }

        /// <summary>
        /// Increments the failure count by 1.
        /// </summary>
        public void IncrementFailureCount()
        {
            Interlocked.Increment(ref failures);
        }

        /// <summary>
        /// Closes the connection to the Wavefront proxy.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Close()
        {
            if (reconnectingSocket != null)
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
            reconnectingSocket.Write(lineData);
        }
    }
}
