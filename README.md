# Wavefront by VMware SDK for C# [![travis build status](https://travis-ci.com/wavefrontHQ/wavefront-sdk-csharp.svg?branch=master)](https://travis-ci.com/wavefrontHQ/wavefront-sdk-csharp) [![NuGet](https://img.shields.io/nuget/v/Wavefront.SDK.CSharp.svg)](https://www.nuget.org/packages/Wavefront.SDK.CSharp)

Wavefront by VMware SDK for C# is the core library for sending metrics, histograms and trace data from your .NET application to Wavefront using an `IWavefrontSender` interface.

## Dependencies
  * .NET Standard (>= 2.0)
  * Microsoft.Extensions.Logging (>= 2.1.1)
  * Microsoft.Extensions.Logging.Debug (>= 2.1.1)
  
## Set Up an IWavefrontSender
You can choose to send data to Wavefront using either the [Wavefront proxy](https://docs.wavefront.com/proxies.html) or [direct ingestion](https://docs.wavefront.com/direct_ingestion.html).

The `IWavefrontSender` interface has two implementations. Instantiate the implementation that corresponds to your choice:
* [Create a `WavefrontProxyClient`](#create-a-wavefrontproxyclient) to send data to a Wavefront proxy
* [Create a `WavefrontDirectIngestionClient`](#create-a-wavefrontdirectingestionclient) to send data directly to a Wavefront service
  
### Create a WavefrontProxyClient
To create a WavefrontProxyClient, you specify the proxy host and one or more ports for the proxy to listen on.

Before data can be sent from your application, you must ensure the Wavefront proxy is configured and running:
* [Install](http://docs.wavefront.com/proxies_installing.html) a Wavefront proxy on the specified proxy host if necessary.
* [Configure](http://docs.wavefront.com/proxies_configuring.html) the proxy to listen on the specified port(s) by setting the corresponding properties: `pushListenerPort`, `histogramDistListenerPort`, `traceListenerPort`
* Start (or restart) the proxy.

```csharp
// Create the builder with the proxy hostname or address
WavefrontProxyClient.Builder wfProxyClientBuilder = new WavefrontProxyClient.Builder(proxyHostName);

// Note: At least one of metrics/histogram/tracing port is required.
// Only set a port if you wish to send that type of data to Wavefront and you
// have the port enabled on the proxy.

// Set the pushListenerPort (example: 2878) to send metrics to Wavefront
wfProxyClientBuilder.MetricsPort(2878);

// Set the histogramDistListenerPort (example: 40,000) to send histograms to Wavefront
wfProxyClientBuilder.DistributionPort(40_000);

// Set the traceListenerPort (example: 30,000) to send opentracing spans to Wavefront
wfProxyClientBuilder.TracingPort(30_000);

// Optional: Set this to override the default flush interval of 5 seconds
wfProxyClientBuilder.FlushIntervalSeconds(2);

// Finally create a WavefrontProxyClient
IWavefrontSender wavefrontSender = wfProxyClientBuilder.Build();
```

### Create a WavefrontDirectIngestionClient
To create a `WavefrontDirectIngestionClient`, you must have access to a Wavefront instance with direct data ingestion permission:
```csharp
// Create a builder with the URL of the form "https://DOMAIN.wavefront.com"
// and a Wavefront API token with direct ingestion permission
WavefrontDirectIngestionClient.Builder wfDirectIngestionClientBuilder =
  new WavefrontDirectIngestionClient.Builder(wavefrontURL, token);

// Optional configuration properties.
// Only override the defaults to set higher values.

// This is the size of internal buffer beyond which data is dropped
// Optional: Set this to override the default max queue size of 50,000
wfDirectIngestionClientBuilder.MaxQueueSize(100_000);

// This is the max batch of data sent per flush interval
// Optional: Set this to override the default batch size of 10,000
wfDirectIngestionClientBuilder.BatchSize(20_000);

// Together with batch size controls the max theoretical throughput of the sender
// Optional: Set this to override the default flush interval value of 1 second
wfDirectIngestionClientBuilder.FlushIntervalSeconds(2);

// Finally create a WavefrontDirectIngestionClient
IWavefrontSender wavefrontSender = wfDirectIngestionClientBuilder.Build();
```

## Send Data to Wavefront

 To send data to Wavefront using the `IWavefrontSender` you instantiated:

### Metrics and Delta Counters

```csharp
// Wavefront Metrics Data format
// <metricName> <metricValue> [<timestamp>] source=<source> [pointTags]
// Example: "new-york.power.usage 42422 1533529977 source=localhost datacenter=dc1"
wavefrontSender.SendMetric(
    "new-york.power.usage",
    42422.0,
    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    "localhost",
    new Dictionary<string, string> { { "datacenter", "dc1" } }.ToImmutableDictionary()
);

// Wavefront Delta Counter format
// <metricName> <metricValue> source=<source> [pointTags]
// Example: "lambda.thumbnail.generate 10 source=lambda_thumbnail_service image-format=jpeg"
wavefrontSender.SendDeltaCounter(
    "lambda.thumbnail.generate",
    10,
    "lambda_thumbnail_service",
    new Dictionary<string, string> { { "image-format", "jpeg" } }.ToImmutableDictionary()
);
```

### Distributions (Histograms)

```csharp
// Wavefront Histogram Data format
// {!M | !H | !D} [<timestamp>] #<count> <mean> [centroids] <histogramName> source=<source>
// [pointTags]
// Example: You can choose to send to at most 3 bins: Minute, Hour, Day
// "!M 1533529977 #20 30.0 #10 5.1 request.latency source=appServer1 region=us-west"
// "!H 1533529977 #20 30.0 #10 5.1 request.latency source=appServer1 region=us-west"
// "!D 1533529977 #20 30.0 #10 5.1 request.latency source=appServer1 region=us-west"
wavefrontSender.SendDistribution(
    "request.latency",
    ImmutableList.Create(
        new KeyValuePair<double, int>(30.0, 20),
        new KeyValuePair<double, int>(5.1, 10)
    ),
    ImmutableHashSet.Create(
        HistogramGranularity.Minute,
        HistogramGranularity.Hour,
        HistogramGranularity.Day
    ),
    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    "appServer1",
    new Dictionary<string, string> { { "region", "us-west" } }.ToImmutableDictionary()
);
```

### Tracing Spans

```csharp
 // Wavefront Tracing Span Data format
 // <tracingSpanName> source=<source> [pointTags] <start_millis> <duration_milliseconds>
 // Example: "getAllUsers source=localhost
 //           traceId=7b3bf470-9456-11e8-9eb6-529269fb1459
 //           spanId=0313bafe-9457-11e8-9eb6-529269fb1459
 //           parent=2f64e538-9457-11e8-9eb6-529269fb1459
 //           application=Wavefront http.method=GET
 //           1533529977 343500"
wavefrontSender.SendSpan(
    "getAllUsers",
    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    343500L,
    "localhost",
    new Guid("7b3bf470-9456-11e8-9eb6-529269fb1459"),
    new Guid("0313bafe-9457-11e8-9eb6-529269fb1459"),
    ImmutableList.Create(new Guid("2f64e538-9457-11e8-9eb6-529269fb1459")),
    null,
    ImmutableList.Create(
        new KeyValuePair<string, string>("application", "Wavefront"),
        new KeyValuePair<string, string>("http.method", "GET")
    ),
    null
);
```

## Close the IWavefrontSender
Remember to flush the buffer and close the sender before shutting down your application.
```csharp
// If there are any failures observed while sending metrics/histograms/tracing-spans above,
// you get the total failure count using the below API
int totalFailures = wavefrontSender.GetFailureCount();

// on-demand buffer flush (may want to do this if you are shutting down your application)
wavefrontSender.Flush();

// close the sender connection before shutting down application
// this will flush in-flight buffer and close connection
wavefrontSender.Close();
```