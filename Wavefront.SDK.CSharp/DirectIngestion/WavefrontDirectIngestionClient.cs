using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Timers;
using Microsoft.Extensions.Logging;
using Wavefront.SDK.CSharp.Common;
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
        private static readonly string DefaultSource = "wavefrontDirectSender";
        private static readonly ILogger Logger =
            Logging.LoggerFactory.CreateLogger<WavefrontDirectIngestionClient>();

        private volatile int failures = 0;
        private int batchSize;
        private BlockingCollection<string> metricsBuffer;
        private BlockingCollection<string> histogramsBuffer;
        private BlockingCollection<string> tracingSpansBuffer;
        private IDataIngesterAPI directService;
        private System.Timers.Timer timer;

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
                    directService = new DataIngesterService(server, token)
                };

                client.timer = new System.Timers.Timer(flushIntervalSeconds * 1000);
                client.timer.Elapsed += client.Run;
                client.timer.Enabled = true;

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
            var lineData =
                Utils.MetricToLineData(name, value, timestamp, source, tags, DefaultSource);

            if (!metricsBuffer.TryAdd(lineData))
            {
                Logger.Log(LogLevel.Trace, "Buffer full, dropping metric point: " + lineData);
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
            var lineData = Utils.HistogramToLineData(name, centroids, histogramGranularities,
                                                        timestamp, source, tags, DefaultSource);
            if (!histogramsBuffer.TryAdd(lineData))
            {
                Logger.Log(LogLevel.Trace, "Buffer full, dropping histograms: " + lineData);
            }
        }

        /// <see cref="IWavefrontTracingSpanSender.SendSpan"/>
        public void SendSpan(string name, long startMillis, long durationMillis, string source,
                             Guid traceId, Guid spanId, IList<Guid> parents,
                             IList<Guid> followsFrom, IList<KeyValuePair<string, string>> tags,
                             IList<SpanLog> spanLogs)
        {
            var lineData = Utils.TracingSpanToLineData(name, startMillis, durationMillis, source,
                                                       traceId, spanId, parents, followsFrom,
                                                       tags, spanLogs, DefaultSource);
            if (!tracingSpansBuffer.TryAdd(lineData))
            {
                Logger.Log(LogLevel.Trace, "Buffer full, dropping span: " + lineData);
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
            InternalFlush(metricsBuffer, Constants.WavefrontMetricFormat);
            InternalFlush(histogramsBuffer, Constants.WavefrontHistogramFormat);
            InternalFlush(tracingSpansBuffer, Constants.WavefrontTracingSpanFormat);
        }

        private void InternalFlush(BlockingCollection<string> buffer, string format)
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
                    var statusCode = directService.Report(format, stream);
                    if (statusCode >= 400 && statusCode < 600)
                    {
                        Logger.Log(LogLevel.Trace,
                                   "Error reporting points, respStatus=" + statusCode);
                        foreach (var item in batch)
                        {
                            if (!buffer.TryAdd(item))
                            {
                                Logger.Log(LogLevel.Trace,
                                           "Buffer full, dropping attempted points");
                            }
                        }
                    }
                }
            }
            catch (IOException e) {
                Interlocked.Increment(ref failures);
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
            return failures;
        }

        /// <summary>
        /// Flushes one last time before stopping the flushing of points on a regular interval.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
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
        }
    }
}
