using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Timers;
using Microsoft.Extensions.Logging;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Common.Metrics;
using Wavefront.SDK.CSharp.Entities.Histograms;
using Wavefront.SDK.CSharp.Entities.Tracing;

namespace Wavefront.SDK.CSharp.DirectIngestion
{
    /// <summary>
    /// Wavefront direct ingestion client that sends data directly to Wavefront cluster via the
    /// direct ingestion API.
    /// </summary>
    public class WavefrontDirectIngestionClient : IWavefrontSender
    {
        private static readonly string DefaultSource = Utils.GetDefaultSource();
        private static readonly ILogger Logger =
            Logging.LoggerFactory.CreateLogger<WavefrontDirectIngestionClient>();

        private int batchSize;
        private BlockingCollection<string> metricsBuffer;
        private BlockingCollection<string> histogramsBuffer;
        private BlockingCollection<string> tracingSpansBuffer;
        private BlockingCollection<string> spanLogsBuffer;
        private IDataIngesterAPI directService;
        private Timer timer;

        private WavefrontSdkMetricsRegistry sdkMetricsRegistry;
        
        // Internal point metrics
        private WavefrontSdkCounter pointsValid;
        private WavefrontSdkCounter pointsInvalid;
        private WavefrontSdkCounter pointsDropped;
        private WavefrontSdkCounter pointReportErrors;

        // Internal histogram metrics
        private WavefrontSdkCounter histogramsValid;
        private WavefrontSdkCounter histogramsInvalid;
        private WavefrontSdkCounter histogramsDropped;
        private WavefrontSdkCounter histogramReportErrors;

        // Internal tracing span metrics
        private WavefrontSdkCounter spansValid;
        private WavefrontSdkCounter spansInvalid;
        private WavefrontSdkCounter spansDropped;
        private WavefrontSdkCounter spanReportErrors;

        // Internal span log metrics
        private WavefrontSdkCounter spanLogsValid;
        private WavefrontSdkCounter spanLogsInvalid;
        private WavefrontSdkCounter spanLogsDropped;
        private WavefrontSdkCounter spanLogReportErrors;

        public class Builder
        {
            // Required parameters
            private readonly string server;
            private readonly string token;

            // Optional parameters
            private int maxQueueSize = 50000;
            private int batchSize = 10000;
            private int flushIntervalSeconds = 1;

            /// <summary>
            /// Creates a new
            /// <see cref="T:Wavefront.SDK.CSharp.DirectIngestion.WavefrontDirectIngestionClient.Builder"/>.
            /// </summary>
            /// <param name="server">
            /// A Wavefront server URL of the form "https://clusterName.wavefront.com".
            /// </param>
            /// <param name="token">
            /// A valid API token with direct ingestion permissions.
            /// </param>
            public Builder(string server, string token)
            {
                this.server = server;
                this.token = token;
            }

            /// <summary>
            /// Sets max queue size of in-memory buffer. Needs to be flushed if full.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="maxQueueSize">Max queue size of in-memory buffer.</param>
            public Builder MaxQueueSize(int maxQueueSize)
            {
                this.maxQueueSize = maxQueueSize;
                return this;
            }

            /// <summary>
            /// Sets batch size to be reported during every flush.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="batchSize">Batch size to be reported during every flush.</param>
            public Builder BatchSize(int batchSize)
            {
                this.batchSize = batchSize;
                return this;
            }

            /// <summary>
            /// Sets interval at which you want to flush points to Wavefront cluster.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="flushIntervalSeconds">
            /// Interval at which you want to flush points to Wavefront cluster, in seconds.
            /// </param>
            public Builder FlushIntervalSeconds(int flushIntervalSeconds)
            {
                this.flushIntervalSeconds = flushIntervalSeconds;
                return this;
            }

            /// <summary>
            /// Creates a new client that connects directly to a given Wavefront service.
            /// </summary>
            /// <returns>A new <see cref="WavefrontDirectIngestionClient"/>.</returns>
            public WavefrontDirectIngestionClient Build()
            {
                var client = new WavefrontDirectIngestionClient
                {
                    batchSize = batchSize,
                    metricsBuffer = new BlockingCollection<string>(maxQueueSize),
                    histogramsBuffer = new BlockingCollection<string>(maxQueueSize),
                    tracingSpansBuffer = new BlockingCollection<string>(maxQueueSize),
                    spanLogsBuffer = new BlockingCollection<string>(maxQueueSize),
                    directService = new DataIngesterService(server, token)
                };

                client.timer = new Timer(flushIntervalSeconds * 1000);
                client.timer.Elapsed += client.Run;
                client.timer.Enabled = true;

                client.sdkMetricsRegistry = new WavefrontSdkMetricsRegistry.Builder(client)
                    .Prefix(Constants.SdkMetricPrefix + ".core.sender.direct")
                    .Tag(Constants.ProcessTagKey, Process.GetCurrentProcess().Id.ToString())
                    .Build();

                client.sdkMetricsRegistry.Gauge("points.queue.size",
                    () => client.metricsBuffer.Count);
                client.sdkMetricsRegistry.Gauge("points.queue.remaining_capacity",
                    () => client.metricsBuffer.BoundedCapacity - client.metricsBuffer.Count);
                client.pointsValid = client.sdkMetricsRegistry.Counter("points.valid");
                client.pointsInvalid = client.sdkMetricsRegistry.Counter("points.invalid");
                client.pointsDropped = client.sdkMetricsRegistry.Counter("points.dropped");
                client.pointReportErrors =
                    client.sdkMetricsRegistry.Counter("points.report.errors");

                client.sdkMetricsRegistry.Gauge("histograms.queue.size",
                    () => client.histogramsBuffer.Count);
                client.sdkMetricsRegistry.Gauge("histograms.queue.remaining_capacity",
                    () => client.histogramsBuffer.BoundedCapacity - client.histogramsBuffer.Count);
                client.histogramsValid = client.sdkMetricsRegistry.Counter("histograms.valid");
                client.histogramsInvalid = client.sdkMetricsRegistry.Counter("histograms.invalid");
                client.histogramsDropped = client.sdkMetricsRegistry.Counter("histograms.dropped");
                client.histogramReportErrors =
                    client.sdkMetricsRegistry.Counter("histograms.report.errors");

                client.sdkMetricsRegistry.Gauge("spans.queue.size",
                    () => client.tracingSpansBuffer.Count);
                client.sdkMetricsRegistry.Gauge("spans.queue.remaining_capacity",
                    () => client.tracingSpansBuffer.BoundedCapacity - client.tracingSpansBuffer.Count);
                client.spansValid = client.sdkMetricsRegistry.Counter("spans.valid");
                client.spansInvalid = client.sdkMetricsRegistry.Counter("spans.invalid");
                client.spansDropped = client.sdkMetricsRegistry.Counter("spans.dropped");
                client.spanReportErrors =
                    client.sdkMetricsRegistry.Counter("spans.report.errors");

                client.sdkMetricsRegistry.Gauge("span_logs.queue.size",
                    () => client.spanLogsBuffer.Count);
                client.sdkMetricsRegistry.Gauge("span_logs.queue.remaining_capacity",
                    () => client.spanLogsBuffer.BoundedCapacity - client.spanLogsBuffer.Count);
                client.spanLogsValid = client.sdkMetricsRegistry.Counter("span_logs.valid");
                client.spanLogsInvalid = client.sdkMetricsRegistry.Counter("span_logs.invalid");
                client.spanLogsDropped = client.sdkMetricsRegistry.Counter("span_logs.dropped");
                client.spanLogReportErrors =
                    client.sdkMetricsRegistry.Counter("span_logs.report.errors");

                return client;
            }
        }

        private WavefrontDirectIngestionClient()
        {
        }

        /// <see cref="Entities.Metrics.IWavefrontMetricSender.SendMetric"/>
        public void SendMetric(string name, double value, long? timestamp, string source,
                               IDictionary<string, string> tags)
        {
            string lineData;
            try
            {
                lineData = Utils.MetricToLineData(name, value, timestamp, source, tags,
                    DefaultSource);
                pointsValid.Inc();
            }
            catch (ArgumentException e)
            {
                pointsInvalid.Inc();
                throw e;
            }
            

            if (!metricsBuffer.TryAdd(lineData))
            {
                pointsDropped.Inc();
                Logger.LogTrace("Buffer full, dropping metric point: " + lineData);
            }
        }

        /// <see cref="IWavefrontHistogramSender.SendDistribution"/>
        public void SendDistribution(string name,
                                     IList<KeyValuePair<double, int>> centroids,
                                     ISet<HistogramGranularity> histogramGranularities,
                                     long? timestamp,
                                     string source,
                                     IDictionary<string, string> tags)
        {
            string lineData;
            try
            {
                lineData = Utils.HistogramToLineData(name, centroids, histogramGranularities,
                    timestamp, source, tags, DefaultSource);
                histogramsValid.Inc();
            }
            catch (ArgumentException e)
            {
                histogramsInvalid.Inc();
                throw e;
            }

            if (!histogramsBuffer.TryAdd(lineData))
            {
                histogramsDropped.Inc();
                Logger.LogTrace("Buffer full, dropping histograms: " + lineData);
            }
        }

        /// <see cref="IWavefrontTracingSpanSender.SendSpan"/>
        public void SendSpan(string name, long startMillis, long durationMillis, string source,
                             Guid traceId, Guid spanId, IList<Guid> parents,
                             IList<Guid> followsFrom, IList<KeyValuePair<string, string>> tags,
                             IList<SpanLog> spanLogs)
        {
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
                    DefaultSource);
                spansValid.Inc();
            }
            catch (ArgumentException e)
            {
                spansInvalid.Inc();
                throw e;
            }

            if (tracingSpansBuffer.TryAdd(tracingSpanLineData))
            {
                // Enqueue valid span logs for sending only if the span was successfully enqueued
                if (spanLogsLineData != null && !spanLogsBuffer.TryAdd(spanLogsLineData))
                {
                    spanLogsDropped.Inc();
                    Logger.LogTrace("Buffer full, dropping span logs: " + spanLogsLineData);
                }
            }
            else
            {
                spansDropped.Inc();
                Logger.LogTrace("Buffer full, dropping span: " + tracingSpanLineData);
                if (spanLogsLineData != null)
                {
                    spanLogsDropped.Inc();
                }
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
                Logger.LogTrace(0, e, "Unable to report to Wavefront cluster");
            }
        }

        /// <see cref="IBufferFlusher.Flush" />
        public void Flush()
        {
            InternalFlush(metricsBuffer, Constants.WavefrontMetricFormat, "points",
                pointsDropped, pointReportErrors);
            InternalFlush(histogramsBuffer, Constants.WavefrontHistogramFormat, "histograms",
                histogramsDropped, histogramReportErrors);
            InternalFlush(tracingSpansBuffer, Constants.WavefrontTracingSpanFormat, "spans",
                spansDropped, spanReportErrors);
            InternalFlush(spanLogsBuffer, Constants.WavefrontSpanLogsFormat, "span_logs",
                spanLogsDropped, spanLogReportErrors);
        }

        private void InternalFlush(BlockingCollection<string> buffer, string format,
            string entityPrefix, WavefrontSdkCounter dropped, WavefrontSdkCounter reportErrors)
        {
            var batch = GetBatch(buffer);
            if (batch.Count == 0)
            {
                return;
            }

            try
            {
                using (var stream = BatchToStream(batch))
                {
                    int statusCode = directService.Report(format, stream);
                    sdkMetricsRegistry.Counter(entityPrefix + ".report." + statusCode).Inc();
                    if (statusCode >= 400 && statusCode < 600)
                    {
                        Logger.LogTrace("Error reporting points, respStatus=" + statusCode);
                        int numAddedBackToBuffer = 0;
                        foreach (var item in batch)
                        {
                            if (buffer.TryAdd(item))
                            {
                                numAddedBackToBuffer++;
                            }
                            else
                            {
                                dropped.Inc(batch.Count - numAddedBackToBuffer);
                                Logger.LogTrace("Buffer full, dropping attempted points");
                                return;
                            }
                        }
                    }
                }
            }
            catch (IOException e) {
                dropped.Inc(batch.Count);
                reportErrors.Inc();
                throw e;
            }
        }

        private IList<string> GetBatch(BlockingCollection<string> buffer)
        {
            var blockSize = Math.Min(buffer.Count, batchSize);
            var batch = new List<string>(blockSize);
            for (int i = 0; i < blockSize && buffer.TryTake(out string item); i++)
            {
                batch.Add(item);
            }
            return batch;
        }

        private Stream BatchToStream(IList<string> batch)
        {
            var sb = new StringBuilder();
            foreach (var item in batch)
            {
                // every line item already ends with \n
                sb.Append(item);
            }
            return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        /// <see cref="IBufferFlusher.GetFailureCount" />
        public int GetFailureCount()
        {
            return (int) (pointReportErrors.Count + histogramReportErrors.Count +
                spanReportErrors.Count + spanLogReportErrors.Count);
        }

        /// <summary>
        /// Flushes one last time before stopping the flushing of points on a regular interval.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
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
        }
    }
}
