using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Wavefront.CSharp.SDK.Common
{
    /// <summary>
    /// Creates a TCP client suitable for the WF proxy. That is: a client which is long-lived and
    /// semantically one-way. This client tries persistently to reconnect to the given host and port
    /// if a connection is ever broken.
    /// </summary>
    public class ReconnectingSocket
    {
        private static readonly ILogger Logger = Logging.LoggerFactory.CreateLogger<ReconnectingSocket>();

        private readonly int serverReadTimeoutMillis = 2000;
        private readonly int bufferSize = 8192;

        private readonly string host;
        private readonly int port;
        private volatile TcpClient client;
        private volatile Stream socketOutputStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Wavefront.CSharp.SDK.Common.ReconnectingSocket"/> class.
        /// </summary>
        /// <param name="host">The hostname of the Wavefront proxy.</param>
        /// <param name="port">The port number of the Wavefront proxy to connect to.</param>
        public ReconnectingSocket(string host, int port)
        {
            this.host = host;
            this.port = port;
            client = new TcpClient(host, port)
            {
                ReceiveTimeout = serverReadTimeoutMillis
            };
            socketOutputStream = new BufferedStream(client.GetStream(), bufferSize);
        }

        /// <summary>
        /// Closes the outputStream best-effort. Tries to re-instantiate the outputStream.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void ResetSocket()
        {
            try
            {
                if (socketOutputStream != null)
                {
                    socketOutputStream.Close();
                }
            }
            catch (SocketException e)
            {
                Logger.Log(LogLevel.Information, "Could not flush to socket.", e);
            }
            finally
            {
                try
                {
                    Interlocked.Exchange(ref client, new TcpClient(host, port)
                    {
                        ReceiveTimeout = serverReadTimeoutMillis
                    }).Close();
                }
                catch (SocketException e)
                {
                    Logger.Log(LogLevel.Warning, "Could not close old socket.", e);
                }
                socketOutputStream = new BufferedStream(client.GetStream(), bufferSize);
                Logger.Log(LogLevel.Information, String.Format("Successfully reset connection to {0}:{1}", host, port));
            }
        }

        /// <summary>
        /// Try to send the given message. On failure, reset and try again. If that fails, just rethrow the exception.
        /// </summary>
        /// <param name="message">The message to be sent to the Wavefront proxy.</param>
        public void Write(string message)
        {
            try
            {
                // Might be NPE due to previously failed call to ResetSocket.
                socketOutputStream.Write(Encoding.UTF8.GetBytes(message), 0, message.Length);
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Warning, "Attempting to reset socket connection.", e);
                ResetSocket();
                socketOutputStream.Write(Encoding.UTF8.GetBytes(message), 0, message.Length);
            }
        }

        /// <summary>
        /// Flushes the outputStream best-effort. If that fails, we reset the connection.
        /// </summary>
        public void Flush()
        {
            try
            {
                socketOutputStream.Flush();
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Warning, "Attempting to reset socket connection.", e);
                ResetSocket();
            }
        }

        /// <summary>
        /// Closes the outputStream and the TCP client.
        /// </summary>
        public void Close()
        {
            try
            {
                Flush();
            }
            finally
            {
                socketOutputStream.Close();
                client.Close();
            }
        }
    }
}
