# wavefront-csharp-sdk [![travis build status](https://travis-ci.com/wavefrontHQ/wavefront-csharp-sdk.svg?branch=master)](https://travis-ci.com/wavefrontHQ/wavefront-csharp-sdk)

This package provides support for sending metrics, histograms and opentracing spans to Wavefront via proxy or direct ingestion.

## Dependencies
  * .NET Standard (>= 2.0)
  * Microsoft.Extensions.Logging (>= 2.1.1)
  * Microsoft.Extensions.Logging.Debug (>= 2.1.1)

## Usage

### Send data to Wavefront via Proxy
```
/*
 * Assume you have a running Wavefront proxy listening on at least one of 
 * metrics/direct-distribution/tracing ports and you know the proxy hostname
 */
var builder = new WavefrontProxyClient.Builder(proxyHost);

/* set this (Example - 2878) if you want to send metrics to Wavefront */
builder.MetricsPort(metricsPort);

/* set this (Example - 40,000) if you want to send histograms to Wavefront */
builder.DistributionPort(distributionPort);

/* set this (Example - 30,000) if you want to send opentracing spans to Wavefront */
builder.TracingPort(tracingPort);

/* set this if you want to change the default flush interval of 5 seconds */
builder.FlushIntervalSeconds(30);

var wavefrontProxyClient = builder.Build();

// 1) Send Metric to Wavefront
/*
 * Wavefront Metrics Data format
 * <metricName> <metricValue> [<timestamp>] source=<source> [pointTags]
 *
 * Example: "new-york.power.usage 42422 1533529977 source=localhost datacenter=dc1"
 */
wavefrontProxyClient.SendMetric(
    "new-york.power.usage",
    42422.0,
    1533529977L,
    "localhost",
    new Dictionary<string, string> { { "datacenter", "dc1" } }.ToImmutableDictionary()
);

// 2) Send Delta Counter to Wavefront     
/*
 * Wavefront Delta Counter format
 * <metricName> <metricValue> source=<source> [pointTags]
 *
 * Example: "lambda.thumbnail.generate 10 source=lambda_thumbnail_service image-format=jpeg"
 */
wavefrontProxyClient.SendDeltaCounter(
    "lambda.thumbnail.generate",
    10,
    "lambda_thumbnail_service",
    new Dictionary<string, string> { { "image-format", "jpeg" } }.ToImmutableDictionary()
);

// 3) Send Direct Distribution (Histogram) to Wavefront
/*
 * Wavefront Histogram Data format
 * {!M | !H | !D} [<timestamp>] #<count> <mean> [centroids] <histogramName> source=<source> 
 * [pointTags]
 *
 * Example: You can choose to send to atmost 3 bins - Minute/Hour/Day
 * 1) Send to minute bin    =>    
 *    "!M 1533529977 #20 30 #10 5.1 request.latency source=appServer1 region=us-west"
 * 2) Send to hour bin      =>    
 *    "!H 1533529977 #20 30 #10 5.1 request.latency source=appServer1 region=us-west"
 * 3) Send to day bin       =>    
 *    "!D 1533529977 #20 30 #10 5.1 request.latency source=appServer1 region=us-west"
 */
wavefrontProxyClient.SendDistribution(
    "request.latency",
    ImmutableList.Create<KeyValuePair<double, int>>(
        new KeyValuePair<double, int>(30.0, 20),
        new KeyValuePair<double, int>(5.1, 10)
    ),
    ImmutableHashSet.Create<HistogramGranularity>(
        HistogramGranularity.Minute,
        HistogramGranularity.Hour,
        HistogramGranularity.Day
    ),
    1533529977L,
    "appServer1",
    new Dictionary<string, string> { { "region", "us-west" } }.ToImmutableDictionary()
);

// 4) Send OpenTracing Span to Wavefront
/*
 * Wavefront Tracing Span Data format
 * <tracingSpanName> source=<source> [pointTags] <start_millis> <duration_milliseconds>
 *
 * Example: "getAllUsers source=localhost
 *           traceId=7b3bf470-9456-11e8-9eb6-529269fb1459
 *           spanId=0313bafe-9457-11e8-9eb6-529269fb1459
 *           parent=2f64e538-9457-11e8-9eb6-529269fb1459
 *           application=Wavefront http.method=GET
 *           1533529977 343500"
 */
wavefrontProxyClient.SendSpan(
    "getAllUsers",
    1533529977L,
    343500L,
    "localhost",
    new Guid("7b3bf470-9456-11e8-9eb6-529269fb1459"),
    new Guid("0313bafe-9457-11e8-9eb6-529269fb1459"),
    ImmutableList.Create<Guid>(new Guid("2f64e538-9457-11e8-9eb6-529269fb1459")),
    null,
    ImmutableList.Create<KeyValuePair<string, string>>(
        new KeyValuePair<string, string>("application", "Wavefront"),
        new KeyValuePair<string, string>("http.method", "GET")
    ),
    null
);

/*
 * If there are any failures observed while sending metrics/histograms/tracing-spans above, 
 * you get the total failure count using the below API
 */
int totalFailures = wavefrontProxyClient.GetFailureCount();

/* on-demand buffer flush */
wavefrontProxyClient.Flush();

/* close connection (this will flush in-flight buffer and close connection) */
wavefrontProxyClient.Close();
```

### Send data to Wavefront via Direct Ingestion
```
/*
 * Assume you have a running Wavefront cluster and you know the 
 * server URL (example - https://mydomain.wavefront.com) and the API token
 */
var builder = new WavefrontDirectIngestionClient.Builder(wavefrontServer, token);

// set this if you want to change the default max queue size of 50,000
builder.MaxQueueSize(100_000);

// set this if you want to change the default batch size of 10,000
builder.BatchSize(20_000);

// set this if you want to change the default flush interval value of 1 seconds
builder.FlushIntervalSeconds(2);

var wavefrontDirectIngestionClient = builder.Build();

// 1) Send Metric to Wavefront
/*
 * Wavefront Metrics Data format
 * <metricName> <metricValue> [<timestamp>] source=<source> [pointTags]
 *
 * Example: "new-york.power.usage 42422 1533529977 source=localhost datacenter=dc1"
 */
wavefrontDirectIngestionClient.SendMetric(
    "new-york.power.usage",
    42422.0,
    1533529977L,
    "localhost",
    new Dictionary<string, string> { { "datacenter", "dc1" } }.ToImmutableDictionary()
);

// 2) Send Delta Counter to Wavefront     
/*
 * Wavefront Delta Counter format
 * <metricName> <metricValue> source=<source> [pointTags]
 *
 * Example: "lambda.thumbnail.generate 10 source=lambda_thumbnail_service image-format=jpeg"
 */
wavefrontDirectIngestionClient.SendDeltaCounter(
   "lambda.thumbnail.generate",
   10,
   "lambda_thumbnail_service",
   new Dictionary<string, string> { { "image-format", "jpeg" } }.ToImmutableDictionary()
);

// 3) Send Direct Distribution (Histogram) to Wavefront
/*
 * Wavefront Histogram Data format
 * {!M | !H | !D} [<timestamp>] #<count> <mean> [centroids] <histogramName> source=<source> 
 * [pointTags]
 *
 * Example: You can choose to send to atmost 3 bins - Minute/Hour/Day
 * 1) Send to minute bin    =>    
 *    "!M 1533529977 #20 30 #10 5.1 request.latency source=appServer1 region=us-west"
 * 2) Send to hour bin      =>    
 *    "!H 1533529977 #20 30 #10 5.1 request.latency source=appServer1 region=us-west"
 * 3) Send to day bin       =>    
 *    "!D 1533529977 #20 30 #10 5.1 request.latency source=appServer1 region=us-west"
 */
wavefrontDirectIngestionClient.SendDistribution(
    "request.latency",
    ImmutableList.Create<KeyValuePair<double, int>>(
        new KeyValuePair<double, int>(30.0, 20),
        new KeyValuePair<double, int>(5.1, 10)
    ),
    ImmutableHashSet.Create<HistogramGranularity>(
        HistogramGranularity.Minute,
        HistogramGranularity.Hour,
        HistogramGranularity.Day
    ),
    1533529977L,
    "appServer1",
    new Dictionary<string, string> { { "region", "us-west" } }.ToImmutableDictionary()
);

// 4) Send OpenTracing Span to Wavefront
/*
 * Wavefront Tracing Span Data format
 * <tracingSpanName> source=<source> [pointTags] <start_millis> <duration_milliseconds>
 *
 * Example: "getAllUsers source=localhost
 *           traceId=7b3bf470-9456-11e8-9eb6-529269fb1459
 *           spanId=0313bafe-9457-11e8-9eb6-529269fb1459
 *           parent=2f64e538-9457-11e8-9eb6-529269fb1459
 *           application=Wavefront http.method=GET
 *           1533529977 343500"
 */
wavefrontDirectIngestionClient.SendSpan(
    "getAllUsers",
    1533529977L,
    343500L,
    "localhost",
    new Guid("7b3bf470-9456-11e8-9eb6-529269fb1459"),
    new Guid("0313bafe-9457-11e8-9eb6-529269fb1459"),
    ImmutableList.Create<Guid>(new Guid("2f64e538-9457-11e8-9eb6-529269fb1459")),
    null,
    ImmutableList.Create<KeyValuePair<string, string>>(
        new KeyValuePair<string, string>("application", "Wavefront"),
        new KeyValuePair<string, string>("http.method", "GET")
    ),
    null
);

/*
 * If there are any failures observed while sending metrics/histograms/tracing-spans above, 
 * you get the total failure count using the below API
 */
int totalFailures = wavefrontDirectIngestionClient.GetFailureCount();

/* on-demand buffer flush */
wavefrontDirectIngestionClient.Flush();

/* close connection (this will flush in-flight buffer and close connection) */
wavefrontDirectIngestionClient.Close();
```
