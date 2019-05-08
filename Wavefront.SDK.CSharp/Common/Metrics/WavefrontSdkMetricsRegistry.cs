using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Timers;
using Wavefront.SDK.CSharp.Entities.Metrics;

namespace Wavefront.SDK.CSharp.Common.Metrics
{
    /// <summary>
    /// Metrics registry used to send internal SDK metrics to Wavefront.
    /// </summary>
    public class WavefrontSdkMetricsRegistry : IDisposable
    {
        private static readonly ILogger Logger =
            Logging.LoggerFactory.CreateLogger<WavefrontSdkMetricsRegistry>();

        private IWavefrontMetricSender wavefrontMetricSender;
        private string source;
        private IDictionary<string, string> tags;
        private string prefix;
        private ConcurrentDictionary<string, IWavefrontSdkMetric> metrics;
        private Timer timer;

        public class Builder
        {
            // Required parameters
            private readonly IWavefrontMetricSender wavefrontMetricSender;

            // Optional parameters
            private readonly IDictionary<string, string> tags;
            private int reportingIntervalSeconds = 60;
            private string source;
            private string prefix;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="wavefrontMetricSender">
            /// The sender instance used to send metrics to Wavefront.
            /// </param>
            public Builder(IWavefrontMetricSender wavefrontMetricSender)
            {
                this.wavefrontMetricSender = wavefrontMetricSender;
                tags = new Dictionary<string, string>();
            }

            /// <summary>
            /// Sets the interval in seconds at which to report metrics to Wavefront.
            /// </summary>
            /// <param name="reportingIntervalSeconds">
            /// Interval at which to report metrics to Wavefront.
            /// </param>
            /// <returns><see cref="this"/></returns>
            public Builder ReportingIntervalSeconds(int reportingIntervalSeconds)
            {
                this.reportingIntervalSeconds = reportingIntervalSeconds;
                return this;
            }

            /// <summary>
            /// Sets the source (or host) that is sending the metrics.
            /// </summary>
            /// <param name="source">The source (or host) that is sending the metrics.</param>
            /// <returns><see cref="this"/></returns>
            public Builder Source(string source)
            {
                this.source = source;
                return this;
            }

            /// <summary>
            /// Adds point tags associated with the registry's metrics.
            /// </summary>
            /// <param name="tags">The point tags associated with the registry's metrics.</param>
            /// <returns><see cref="this"/></returns>
            public Builder Tags(IDictionary<string, string> tags)
            {
                foreach (var tag in tags)
                {
                    if (tag.Key != null && !this.tags.ContainsKey(tag.Key))
                    {
                       this.tags.Add(tag.Key, tag.Value);
                    }
                }
                return this;
            }

            /// <summary>
            /// Adds a point tag associated with the registry's metrics.
            /// </summary>
            /// <param name="key">The tag key.</param>
            /// <param name="value">The tag value.</param>
            /// <returns><see cref="this"/></returns>
            public Builder Tag(string key, string value)
            {
                if (key != null && !tags.ContainsKey(key))
                {
                    tags.Add(key, value);
                }
                return this;
            }

            /// <summary>
            /// Sets the name prefix for the registry's metrics.
            /// </summary>
            /// <param name="prefix">The name prefix for the registry's metrics.</param>
            /// <returns><see cref="this"/></returns>
            public Builder Prefix(string prefix)
            {
                this.prefix = prefix;
                return this;
            }

            /// <summary>
            /// Builds a registry.
            /// </summary>
            /// <returns>A new instance of the registry.</returns>
            public WavefrontSdkMetricsRegistry Build()
            {
                var registry = new WavefrontSdkMetricsRegistry
                {
                    wavefrontMetricSender = wavefrontMetricSender,
                    source = source,
                    tags = tags,
                    prefix = string.IsNullOrWhiteSpace(prefix) ? "" : prefix + ".",
                    metrics = new ConcurrentDictionary<string, IWavefrontSdkMetric>(),
                };

                registry.timer = new System.Timers.Timer(reportingIntervalSeconds * 1000);
                registry.timer.Elapsed += registry.Run;
                registry.timer.Enabled = true;

                return registry;
            }
        }

        private WavefrontSdkMetricsRegistry()
        {
        }

        private void Run(object sender, ElapsedEventArgs e)
        {
            long timestamp = DateTimeUtils.UnixTimeMilliseconds(DateTime.UtcNow);
            foreach (var entry in metrics)
            {
                string name = prefix + entry.Key;
                IWavefrontSdkMetric metric = entry.Value;
                try
                {
                    if (metric is WavefrontSdkGauge)
                    {
                        wavefrontMetricSender.SendMetric(name,
                            ((WavefrontSdkGauge)metric).Value, timestamp, source, tags);
                    }
                    else if (metric is WavefrontSdkCounter)
                    {
                        wavefrontMetricSender.SendMetric(name + ".count",
                            ((WavefrontSdkCounter)metric).Count, timestamp, source, tags);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(0, ex, "Unable to send internal SDK metric");
                }
            }
        }

        /// <summary>
        /// Returns the gauge registered under the given name. If no metric is registered under
        /// this name, create and register and new gauge using the given delegate.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <param name="valueDelegate">The delegate used to supply the value of the gauge.</param>
        /// <returns>A new or pre-existing gauge.</returns>
        public WavefrontSdkGauge Gauge(string name, Func<double> valueDelegate)
        {
            return GetOrAdd(name, new WavefrontSdkGauge(valueDelegate));
        }

        /// <summary>
        /// Returns the counter registered under the given name. If no metric is registered under
        /// this name, create and register a new counter.
        /// </summary>
        /// <param name="name">The metric name.</param>
        /// <returns>A new or pre-existing counter.</returns>
        public WavefrontSdkCounter Counter(string name)
        {
            return GetOrAdd(name, new WavefrontSdkCounter());
        }

        private T GetOrAdd<T>(string name, T metric) where T : IWavefrontSdkMetric
        {
            return (T) metrics.GetOrAdd(name, metric);
        }

        public void Dispose()
        {
            timer?.Dispose();
        }
    }
}
