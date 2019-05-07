using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Common.Metrics;
using Wavefront.SDK.CSharp.Entities.Histograms;
using Wavefront.SDK.CSharp.Entities.Tracing;

namespace Wavefront.SDK.CSharp.Proxy
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

        private WavefrontSdkMetricsRegistry sdkMetricsRegistry;

        // Internal point metrics
        private WavefrontSdkCounter pointsDiscarded;
        private WavefrontSdkCounter pointsValid;
        private WavefrontSdkCounter pointsInvalid;
        private WavefrontSdkCounter pointsDropped;

        // Internal histogram metrics
        private WavefrontSdkCounter histogramsDiscarded;
        private WavefrontSdkCounter histogramsValid;
        private WavefrontSdkCounter histogramsInvalid;
        private WavefrontSdkCounter histogramsDropped;

        // Internal tracing span metrics
        private WavefrontSdkCounter spansDiscarded;
        private WavefrontSdkCounter spansValid;
        private WavefrontSdkCounter spansInvalid;
        private WavefrontSdkCounter spansDropped;

        // Internal span log metrics
        private WavefrontSdkCounter spanLogsDiscarded;
        private WavefrontSdkCounter spanLogsValid;
        private WavefrontSdkCounter spanLogsInvalid;
        private WavefrontSdkCounter spanLogsDropped;

        // Source to use if entity source is null
        private readonly string defaultSource = Utils.GetDefaultSource();

        public class Builder
        {
            private readonly string proxyHostName;

            private int? metricsPort;
            private int? distributionPort;
            private int? tracingPort;
            private int flushIntervalSeconds = 5;

            /// <summary>
            /// Creates a new
            /// <see cref="T:Wavefront.SDK.CSharp.Proxy.WavefrontProxyClient.Builder"/>.
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

                client.sdkMetricsRegistry = new WavefrontSdkMetricsRegistry.Builder(client)
                    .Prefix(Constants.SdkMetricPrefix + ".core.sender.proxy")
                    .Tag(Constants.ProcessTagKey, Process.GetCurrentProcess().Id.ToString())
                    .Build();

                if (metricsPort == null)
                {
                    client.metricsProxyConnectionHandler = null;
                }
                else
                {
                    client.metricsProxyConnectionHandler = new ProxyConnectionHandler(
                        proxyHostName, metricsPort.Value, client.sdkMetricsRegistry,
                        "metricHandler");
                }

                if (distributionPort == null)
                {
                    client.histogramProxyConnectionHandler = null;
                }
                else
                {
                    client.histogramProxyConnectionHandler = new ProxyConnectionHandler(
                        proxyHostName, distributionPort.Value, client.sdkMetricsRegistry,
                        "histogramHandler");
                }

                if (tracingPort == null)
                {
                    client.tracingProxyConnectionHandler = null;
                }
                else
                {
                    client.tracingProxyConnectionHandler = new ProxyConnectionHandler(
                        proxyHostName, tracingPort.Value, client.sdkMetricsRegistry,
                        "tracingHandler");
                }

                client.timer = new Timer(flushIntervalSeconds * 1000);
                client.timer.Elapsed += client.Run;
                client.timer.Enabled = true;

                client.pointsDiscarded = client.sdkMetricsRegistry.Counter("points.discarded");
                client.pointsValid = client.sdkMetricsRegistry.Counter("points.valid");
                client.pointsInvalid = client.sdkMetricsRegistry.Counter("points.invalid");
                client.pointsDropped = client.sdkMetricsRegistry.Counter("points.dropped");

                client.histogramsDiscarded = client.sdkMetricsRegistry.Counter("histograms.discarded");
                client.histogramsValid = client.sdkMetricsRegistry.Counter("histograms.valid");
                client.histogramsInvalid = client.sdkMetricsRegistry.Counter("histograms.invalid");
                client.histogramsDropped = client.sdkMetricsRegistry.Counter("histograms.dropped");

                client.spansDiscarded = client.sdkMetricsRegistry.Counter("spans.discarded");
                client.spansValid = client.sdkMetricsRegistry.Counter("spans.valid");
                client.spansInvalid = client.sdkMetricsRegistry.Counter("spans.invalid");
                client.spansDropped = client.sdkMetricsRegistry.Counter("spans.dropped");

                client.spanLogsDiscarded = client.sdkMetricsRegistry.Counter("spans_logs.discarded");
                client.spanLogsValid = client.sdkMetricsRegistry.Counter("span_logs.valid");
                client.spanLogsInvalid = client.sdkMetricsRegistry.Counter("span_logs.invalid");
                client.spanLogsDropped = client.sdkMetricsRegistry.Counter("span_logs.dropped");

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
                pointsDiscarded.Inc();
                Logger.LogWarning("Can't send data to Wavefront. " +
                    "Please configure metrics port for Wavefront proxy.");
                return;
            }

            string lineData;
            try
            {
                lineData = Utils.MetricToLineData(name, value, timestamp, source, tags,
                    defaultSource);
                pointsValid.Inc();
            }
            catch (ArgumentException e)
            {
                pointsInvalid.Inc();
                throw e;
            }

            _ = metricsProxyConnectionHandler.SendDataAsync(lineData).ContinueWith(
                task =>
                {
                    pointsDropped.Inc();
                    metricsProxyConnectionHandler.IncrementFailureCount();
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <see cref="IWavefrontHistogramSender.SendDistribution"/>
        public void SendDistribution(string name, IList<KeyValuePair<double, int>> centroids,
                                     ISet<HistogramGranularity> histogramGranularities,
                                     long? timestamp, string source,
                                     IDictionary<string, string> tags)
        {
            if (histogramProxyConnectionHandler == null)
            {
                histogramsDiscarded.Inc();
                Logger.LogWarning("Can't send data to Wavefront. " +
                    "Please configure histogram distribution port for Wavefront proxy.");
                return;
            }

            string lineData;
            try
            {
                lineData = Utils.HistogramToLineData(name, centroids, histogramGranularities,
                    timestamp, source, tags, defaultSource);
                histogramsValid.Inc();
            }
            catch (ArgumentException e)
            {
                histogramsInvalid.Inc();
                throw e;
            }

            _ = histogramProxyConnectionHandler.SendDataAsync(lineData).ContinueWith(
                task =>
                {
                    histogramsDropped.Inc();
                    histogramProxyConnectionHandler.IncrementFailureCount();
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <see cref="IWavefrontTracingSpanSender.SendSpan"/>
        public void SendSpan(string name, long startMillis, long durationMillis, string source,
                             Guid traceId, Guid spanId, IList<Guid> parents,
                             IList<Guid> followsFrom, IList<KeyValuePair<string, string>> tags,
                             IList<SpanLog> spanLogs)
        {
            if (tracingProxyConnectionHandler == null)
            {
                spansDiscarded.Inc();
                if (spanLogs != null && spanLogs.Count > 0)
                {
                    spanLogsDiscarded.Inc();
                }
                Logger.LogWarning("Can't send data to Wavefront. " +
                    "Please configure histogram distribution port for Wavefront proxy.");
                return;
            }

            string spanLogsLineData = null;
            if (spanLogs != null && spanLogs.Count > 0)
            {
                spanLogsLineData = Utils.SpanLogsToLineData(startMillis, durationMillis, traceId,
                    spanId, spanLogs);

                if (spanLogsLineData == null)
                {
                    spanLogsInvalid.Inc();
                }
                else
                {
                    spanLogsValid.Inc();
                    // If valid span logs exist, add indicator tag to span
                    tags = Utils.AddSpanLogIndicatorTag(tags);
                }
            }

            string tracingSpanLineData;
            try
            {
                tracingSpanLineData = Utils.TracingSpanToLineData(name, startMillis,
                    durationMillis, source, traceId, spanId, parents, followsFrom, tags, spanLogs,
                    defaultSource);
                spansValid.Inc();
            }
            catch (ArgumentException e)
            {
                spansInvalid.Inc();
                throw e;
            }

            _ = tracingProxyConnectionHandler.SendDataAsync(tracingSpanLineData).ContinueWith(
                task =>
                {
                    if (task.Exception == null)
                    {
                        // Send valid span logs only if the span was successfully sent
                        if (spanLogsLineData != null)
                        {
                            _ = tracingProxyConnectionHandler.SendDataAsync(spanLogsLineData)
                                .ContinueWith(task2 =>
                                {
                                    spanLogsDropped.Inc();
                                    tracingProxyConnectionHandler.IncrementFailureCount();
                                }, TaskContinuationOptions.OnlyOnFaulted);
                        }
                    }
                    else
                    {
                        spansDropped.Inc();
                        if (spanLogsLineData != null)
                        {
                            spanLogsDropped.Inc();
                        }
                        tracingProxyConnectionHandler.IncrementFailureCount();
                    }
                });
        }

        private void Run(object source, ElapsedEventArgs args)
        {
            try
            {
                Flush();
            }
            catch (Exception e)
            {
                Logger.LogTrace(0, e, "Unable to report to Wavefront cluster");
            }
        }

        /// <see cref="IBufferFlusher.Flush" />
        public void Flush()
        {
            metricsProxyConnectionHandler?.Flush();
            histogramProxyConnectionHandler?.Flush();
            tracingProxyConnectionHandler?.Flush();
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
            sdkMetricsRegistry.Dispose();

            try
            {
                Flush();
            }
            catch (IOException e)
            {
                Logger.LogWarning(0, e, "error flushing buffer");
            }

            timer.Dispose();

            try
            {
                metricsProxyConnectionHandler?.Close();
            }
            catch (IOException e)
            {
                Logger.LogWarning(0, e, "error closing metricsProxyConnectionHandler");
            }

            try
            {
                histogramProxyConnectionHandler.Close();
            }
            catch (IOException e)
            {
                Logger.LogWarning(0, e, "error closing histogramProxyConnectionHandler");
            }

            try
            {
                tracingProxyConnectionHandler.Close();
            }
            catch (IOException e)
            {
                Logger.LogWarning(0, e, "error closing tracingProxyConnectionHandler");
            }
        }
    }
}
