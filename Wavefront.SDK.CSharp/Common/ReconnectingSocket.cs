using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wavefront.SDK.CSharp.Common
{
    /// <summary>
    /// Creates a TCP client suitable for the WF proxy. That is: a client which is long-lived and
    /// semantically one-way. This client tries persistently to reconnect to the given host and port
    /// if a connection is ever broken.
    /// </summary>
    public class ReconnectingSocket
    {
        private static readonly ILogger Logger =
            Logging.LoggerFactory.CreateLogger<ReconnectingSocket>();

        private readonly int serverReadTimeoutMillis = 2000;
        private readonly int serverConnectTimeoutMillis = 2000;
        private readonly int bufferSize = 8192;

        private readonly string host;
        private readonly int port;
        private volatile TcpClient client;
        private volatile Stream socketOutputStream;
        private volatile SemaphoreSlim connectSemaphore = new SemaphoreSlim(1);
        private volatile int failures;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="T:Wavefront.SDK.CSharp.Common.ReconnectingSocket"/> class.
        /// </summary>
        /// <param name="host">The hostname of the Wavefront proxy.</param>
        /// <param name="port">The port number of the Wavefront proxy to connect to.</param>
        public ReconnectingSocket(string host, int port)
        {
            this.host = host;
            this.port = port;
            client = new TcpClient
            {
                ReceiveTimeout = serverReadTimeoutMillis
            };
            Connect();
        }

        /// <summary>
        /// Blocks while attempting to establish a connection.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Connect()
        {
            ConnectAsync(false).GetAwaiter().GetResult();
        }

        private async Task ConnectAsync(bool isReset)
        {
            if (!await connectSemaphore.WaitAsync(TimeSpan.Zero))
            {
                // skip if another thread is already attempting to connect
                return;
            }

            try
            {
                if (isReset)
                {
                    // Close the outputStream and connection
                    if (socketOutputStream != null)
                    {
                        try
                        {
                            socketOutputStream.Close();
                        }
                        catch (IOException e)
                        {
                            Logger.Log(
                                LogLevel.Information, "Could not flush and close socket.", e);
                        }
                    }
                    client.Close();
                    client = new TcpClient
                    {
                        ReceiveTimeout = serverReadTimeoutMillis
                    };
                }

                // Open a connection and instantiate the outputStream
                try
                {
                    var connectTask = client.ConnectAsync(host, port);
                    var timeoutTask = Task.Delay(serverConnectTimeoutMillis);
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    if (completedTask == connectTask)
                    {
                        socketOutputStream =
                            Stream.Synchronized(new BufferedStream(client.GetStream(), bufferSize));
                        Logger.Log(LogLevel.Information,
                               string.Format("Successfully connected to {0}:{1}", host, port));
                    }
                    else
                    {
                        Logger.Log(LogLevel.Warning,
                               string.Format("Unable to connect to {0}:{1}", host, port));
                        client.Close();
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(LogLevel.Warning,
                               string.Format("Unable to connect to {0}:{1}", host, port), e);
                    client.Close();
                }
            }
            finally
            {
                connectSemaphore.Release();
            }
        }

        /// <summary>
        /// Closes the outputStream best-effort. Tries to re-instantiate the outputStream.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void ResetSocket()
        {
            _ = ConnectAsync(true);
        }

        /// <summary>
        /// Try to send the given message. On failure, reset and try again.
        /// If that fails, just rethrow the exception.
        /// </summary>
        /// <param name="message">The message to be sent to the Wavefront proxy.</param>
        public void Write(string message)
        {
            _ = WriteAsync(message);
        }

        private async Task WriteAsync(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            try
            {
                // Might be NPE due to previously failed call to ResetSocket.
                await socketOutputStream.WriteAsync(bytes, 0, bytes.Length);
            }
            catch (Exception e)
            {
                try
                {
                    Logger.Log(LogLevel.Warning, "Attempting to reset socket connection.", e);
                    ResetSocket();
                    await socketOutputStream.WriteAsync(bytes, 0, bytes.Length);
                }
                catch (Exception e2)
                {
                    Interlocked.Increment(ref failures);
                    throw new IOException(e2.Message, e2);
                }

            }
        }

        /// <summary>
        /// Flushes the outputStream best-effort. If that fails, we reset the connection.
        /// </summary>
        public void Flush()
        {
            _ = FlushAsync();
        }

        private async Task FlushAsync()
        {
            try
            {
                await socketOutputStream.FlushAsync();
            }
            catch (Exception e)
            {
                try
                {
                    Logger.Log(LogLevel.Warning, "Attempting to reset socket connection.", e);
                    ResetSocket();
                }
                catch (Exception e2)
                {
                    Logger.Log(LogLevel.Information, "Could not flush data", e2);
                }
            }
        }

        /// <summary>
        /// Returns the number of failed writes.
        /// </summary>
        /// <returns>The number of failed writes.</returns>
        public int GetFailureCount()
        {
            return failures;
        }

        /// <summary>
        /// Closes the outputStream and the TCP client.
        /// </summary>
        public void Close()
        {
            if (socketOutputStream != null)
            {
                try
                {
                    socketOutputStream.Close();
                }
                catch (IOException e)
                {
                    Logger.Log(
                        LogLevel.Information, "Could not flush and close socket.", e);
                }
            }
            client.Close();
        }
    }
}
