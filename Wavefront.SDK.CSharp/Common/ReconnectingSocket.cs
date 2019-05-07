using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wavefront.SDK.CSharp.Common.Metrics;

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

        private readonly WavefrontSdkCounter writeSuccesses;
        private readonly WavefrontSdkCounter writeErrors;
        private readonly WavefrontSdkCounter flushSuccesses;
        private readonly WavefrontSdkCounter flushErrors;
        private readonly WavefrontSdkCounter resetSuccesses;
        private readonly WavefrontSdkCounter resetErrors;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="T:Wavefront.SDK.CSharp.Common.ReconnectingSocket"/> class.
        /// </summary>
        /// <param name="host">The hostname of the Wavefront proxy.</param>
        /// <param name="port">The port number of the Wavefront proxy to connect to.</param>
        public ReconnectingSocket(string host, int port,
            WavefrontSdkMetricsRegistry sdkMetricsRegistry, string entityPrefix)
        {
            this.host = host;
            this.port = port;

            entityPrefix = string.IsNullOrWhiteSpace(entityPrefix) ? "" : entityPrefix + ".";
            writeSuccesses = sdkMetricsRegistry.Counter(entityPrefix + "write.success");
            writeErrors = sdkMetricsRegistry.Counter(entityPrefix + "write.errors");
            flushSuccesses = sdkMetricsRegistry.Counter(entityPrefix + "flush.success");
            flushErrors = sdkMetricsRegistry.Counter(entityPrefix + "flush.errors");
            resetSuccesses = sdkMetricsRegistry.Counter(entityPrefix + "reset.success");
            resetErrors = sdkMetricsRegistry.Counter(entityPrefix + "reset.errors");

            client = new TcpClient
            {
                ReceiveTimeout = serverReadTimeoutMillis
            };
            // Block while attempting to establish a connection
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
                    try
                    {
                        socketOutputStream?.Close();
                    }
                    catch (IOException e)
                    {
                        Logger.LogInformation(0, e, "Could not flush and close socket.");
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
                        if (isReset)
                        {
                            resetSuccesses.Inc();
                        }
                        Logger.LogInformation(
                               string.Format("Successfully connected to {0}:{1}", host, port));
                    }
                    else
                    {
                        Logger.LogWarning(string.Format("Unable to connect to {0}:{1}", host, port));
                        client.Close();
                    }
                }
                catch (Exception e)
                {
                    if (isReset)
                    {
                        resetErrors.Inc();
                    }
                    Logger.LogWarning(0, e,
                        string.Format("Unable to connect to {0}:{1}", host, port));
                    client.Close();
                    throw new IOException(e.Message, e);
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
        private async Task ResetSocketAsync()
        {
            await ConnectAsync(true);
        }

        /// <summary>
        /// Try to send the given message. On failure, reset and try again.
        /// If that fails, just rethrow the exception.
        /// </summary>
        /// <param name="message">The message to be sent to the Wavefront proxy.</param>
        public async Task WriteAsync(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            try
            {
                // Might be NPE due to previously failed call to ResetSocket.
                await socketOutputStream.WriteAsync(bytes, 0, bytes.Length);
                writeSuccesses.Inc();
            }
            catch (Exception e)
            {
                try
                {
                    Logger.LogWarning(0, e, "Attempting to reset socket connection.");
                    await ResetSocketAsync();
                    await socketOutputStream.WriteAsync(bytes, 0, bytes.Length);
                    writeSuccesses.Inc();
                }
                catch (Exception e2)
                {
                    writeErrors.Inc();
                    throw new IOException(e2.Message, e2);
                }
            }
        }

        /// <summary>
        /// Flushes the outputStream best-effort. If that fails, we reset the connection.
        /// </summary>
        public async Task FlushAsync()
        {
            try
            {
                await socketOutputStream.FlushAsync();
                flushSuccesses.Inc();
            }
            catch (Exception e)
            {
                flushErrors.Inc();
                Logger.LogWarning(0, e, "Attempting to reset socket connection.");
                await ResetSocketAsync();
            }
        }

        /// <summary>
        /// Closes the outputStream and the TCP client.
        /// </summary>
        public void Close()
        {
            try
            {
                socketOutputStream?.Close();
            }
            catch (IOException e)
            {
                Logger.LogInformation(0, e, "Could not flush and close socket.");
            }
            client.Close();
        }
    }
}
