using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Timers;
using Microsoft.Extensions.Logging;
using Wavefront.CSharp.SDK.Common;
using Wavefront.CSharp.SDK.Entities.Histograms;
using Wavefront.CSharp.SDK.Entities.Tracing;

namespace Wavefront.CSharp.SDK.Integrations
{
    /// <summary>
    /// Client that sends data directly via TCP to the Wavefront Proxy Agent. User should probably
    /// attempt to reconnect when exceptions are thrown from any methods.
    /// </summary>
    public class WavefrontProxyClient : IWavefrontSender
    {
        private static readonly ILogger Logger =
            Logging.LoggerFactory.CreateLogger<WavefrontProxyClient>();

        private ProxyConnectionHandler metricsProxyConnectionHandler;
        private ProxyConnectionHandler histogramProxyConnectionHandler;
        private ProxyConnectionHandler tracingProxyConnectionHandler;
        private Timer timer;

        // Source to use if entity source is null
        private readonly string defaultSource = Dns.GetHostEntry("LocalHost").HostName;

        public class Builder
        {
            private readonly string proxyHostName;

            private int? metricsPort;
            private int? distributionPort;
            private int? tracingPort;
            private int flushIntervalSeconds = 5;

            /// <summary>
            /// Creates a new
            /// <see cref="T:Wavefront.CSharp.SDK.Integrations.WavefrontProxyClient.Builder"/>.
            /// </summary>
            /// <param name="proxyHostName">The hostname of the Wavefront proxy.</param>
            public Builder(string proxyHostName)
            {
                this.proxyHostName = proxyHostName;
            }

            /// <summary>
            /// Enables sending of metrics to Wavefront cluster via proxy.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="metricsPort">
            /// The metrics port on which the Wavefront proxy is listening.
            /// </param>
            public Builder MetricsPort(int metricsPort)
            {
                this.metricsPort = metricsPort;
                return this;
            }

            /// <summary>
            /// Enables sending of distributions to Wavefront cluster via proxy.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="distributionPort">
            /// The distribution port on which the Wavefront proxy is listening.
            /// </param>
            public Builder DistributionPort(int distributionPort)
            {
                this.distributionPort = distributionPort;
                return this;
            }

            /// <summary>
            /// Enables sending of tracing spans to Wavefront cluster via proxy.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="tracingPort">
            /// The tracing port on which the Wavefront proxy is listening.
            /// </param>
            public Builder TracingPort(int tracingPort)
            {
                this.tracingPort = tracingPort;
                return this;
            }

            /// <summary>
            /// Sets interval at which you want to flush points to Wavefront proxy.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="flushIntervalSeconds">
            /// The interval at which you want to flush points to Wavefront proxy, in seconds.
            /// </param>
            public Builder FlushIntervalSeconds(int flushIntervalSeconds)
            {
                this.flushIntervalSeconds = flushIntervalSeconds;
                return this;
            }

            /// <summary>
            /// Builds a new client that connects to the Wavefront Proxy Agent.
            /// </summary>
            /// <returns>A new <see cref="WavefrontProxyClient"/>.</returns>
            public WavefrontProxyClient Build()
            {
                var client = new WavefrontProxyClient();

                if (metricsPort == null)
                {
                    client.metricsProxyConnectionHandler = null;
                }
                else
                {
                    client.metricsProxyConnectionHandler = new ProxyConnectionHandler(
                        proxyHostName, metricsPort.Value);
                }

                if (distributionPort == null)
                {
                    client.histogramProxyConnectionHandler = null;
                }
                else
                {
                    client.histogramProxyConnectionHandler = new ProxyConnectionHandler(
                        proxyHostName, distributionPort.Value);
                }

                if (tracingPort == null)
                {
                    client.tracingProxyConnectionHandler = null;
                }
                else
                {
                    client.tracingProxyConnectionHandler = new ProxyConnectionHandler(
                        proxyHostName, tracingPort.Value);
                }

                client.timer = new Timer(flushIntervalSeconds * 1000);
                client.timer.Elapsed += client.Run;
                client.timer.Enabled = true;

                return client;
            }
        }

        private WavefrontProxyClient()
        {
        }

        /// <see cref="Entities.Metrics.IWavefrontMetricSender.SendMetric"/>
        public void SendMetric(string name, double value, long? timestamp, string source,
                               IDictionary<string, string> tags)
        {
            if (metricsProxyConnectionHandler == null)
            {
                return;
            }

            if (!metricsProxyConnectionHandler.IsConnected())
            {
                try
                {
                    metricsProxyConnectionHandler.Connect();
                }
                catch (InvalidOperationException)
                {
                    // already connected.
                }
            }

            try
            {
                try
                {
                    String lineData =
                        Utils.MetricToLineData(name, value, timestamp, source, tags, defaultSource);
                    metricsProxyConnectionHandler.SendData(lineData);
                }
                catch (Exception e)
                {
                    throw new IOException(e.Message, e);
                }
            }
            catch (IOException e)
            {
                metricsProxyConnectionHandler.IncrementFailureCount();
                throw e;
            }
        }

        /// <see cref="IWavefrontHistogramSender.SendDistribution"/>
        public void SendDistribution(string name, IList<KeyValuePair<double, int>> centroids,
                                     ISet<HistogramGranularity> histogramGranularities,
                                     long? timestamp, string source,
                                     IDictionary<string, string> tags)
        {
            if (histogramProxyConnectionHandler == null)
            {
                return;
            }

            if (!histogramProxyConnectionHandler.IsConnected())
            {
                try
                {
                    histogramProxyConnectionHandler.Connect();
                }
                catch (InvalidOperationException)
                {
                    // already connected.
                }
            }

            try
            {
                try
                {
                    String lineData = Utils.HistogramToLineData(name, centroids,
                                                                histogramGranularities,
                                                                timestamp, source, tags,
                                                                defaultSource);
                    histogramProxyConnectionHandler.SendData(lineData);
                }
                catch (Exception e)
                {
                    throw new IOException(e.Message, e);
                }
            }
            catch (IOException e)
            {
                histogramProxyConnectionHandler.IncrementFailureCount();
                throw e;
            }
        }

        /// <see cref="IWavefrontTracingSpanSender.SendSpan"/>
        public void SendSpan(string name, long startMillis, long durationMillis, string source,
                             Guid traceId, Guid spanId, IList<Guid> parents,
                             IList<Guid> followsFrom, IList<KeyValuePair<string, string>> tags,
                             IList<SpanLog> spanLogs)
        {
            if (tracingProxyConnectionHandler == null)
            {
                return;
            }

            if (!tracingProxyConnectionHandler.IsConnected())
            {
                try
                {
                    tracingProxyConnectionHandler.Connect();
                }
                catch (InvalidOperationException)
                {
                    // already connected.
                }
            }

            try
            {
                try
                {
                    String lineData = Utils.TracingSpanToLineData(name, startMillis, durationMillis,
                                                                  source, traceId, spanId, parents,
                                                                  followsFrom, tags, spanLogs,
                                                                  defaultSource);
                    tracingProxyConnectionHandler.SendData(lineData);
                }
                catch (Exception e)
                {
                    throw new IOException(e.Message, e);
                }
            }
            catch (IOException e)
            {
                tracingProxyConnectionHandler.IncrementFailureCount();
                throw e;
            }
        }

        private void Run(object source, ElapsedEventArgs args)
        {
            try
            {
                Flush();
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Trace, "Unable to report to Wavefront cluster", e);
            }
        }

        /// <see cref="IBufferFlusher.Flush" />
        public void Flush()
        {
            if (metricsProxyConnectionHandler != null)
            {
                metricsProxyConnectionHandler.Flush();
            }

            if (histogramProxyConnectionHandler != null)
            {
                histogramProxyConnectionHandler.Flush();
            }

            if (tracingProxyConnectionHandler != null)
            {
                tracingProxyConnectionHandler.Flush();
            }
        }

        /// <see cref="IBufferFlusher.GetFailureCount" />
        public int GetFailureCount()
        {
            int failureCount = 0;
            if (metricsProxyConnectionHandler != null)
            {
                failureCount += metricsProxyConnectionHandler.GetFailureCount();
            }

            if (histogramProxyConnectionHandler != null)
            {
                failureCount += histogramProxyConnectionHandler.GetFailureCount();
            }

            if (tracingProxyConnectionHandler != null)
            {
                failureCount += tracingProxyConnectionHandler.GetFailureCount();
            }
            return failureCount;
        }

        /// <summary>
        /// Flushes one last time before stopping the flushing of points on a regular interval.
        /// Closes the connection to the Wavefront proxy.
        /// </summary>
        public void Close()
        {
            try
            {
                Flush();
            }
            catch (IOException e)
            {
                Logger.Log(LogLevel.Warning, "error flushing buffer", e);
            }

            timer.Dispose();

            if (metricsProxyConnectionHandler != null)
            {
                try
                {
                    metricsProxyConnectionHandler.Close();
                }
                catch (IOException e)
                {
                    Logger.Log(LogLevel.Warning, "error closing metricsProxyConnectionHandler", e);
                }
            }

            if (histogramProxyConnectionHandler != null)
            {
                try
                {
                    histogramProxyConnectionHandler.Close();
                }
                catch (IOException e)
                {
                    Logger.Log(LogLevel.Warning,
                               "error closing histogramProxyConnectionHandler", e);
                }
            }

            if (tracingProxyConnectionHandler != null)
            {
                try
                {
                    tracingProxyConnectionHandler.Close();
                }
                catch (IOException e)
                {
                    Logger.Log(LogLevel.Warning, "error closing tracingProxyConnectionHandler", e);
                }
            }
        }
    }
}
