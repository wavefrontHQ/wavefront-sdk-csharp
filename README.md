# Wavefront by VMware SDK for C# [![travis build status](https://travis-ci.com/wavefrontHQ/wavefront-sdk-csharp.svg?branch=master)](https://travis-ci.com/wavefrontHQ/wavefront-sdk-csharp) [![NuGet](https://img.shields.io/nuget/v/Wavefront.SDK.CSharp.svg)](https://www.nuget.org/packages/Wavefront.SDK.CSharp)

## Table of Content
* [Prerequisites](#Prerequisites)
* [Set Up an IWavefrontSender](#set-up-an-iwavefrontsender)
* [Send Data to Wavefront](#send-data-to-wavefront)
* [Close the IWavefrontSender](#close-the-iwavefrontsender)
* [License](#License)
* [How to Get Support and Contribute](#how-to-get-support-and-contribute)

# Welcome to the Wavefront C# SDK

Wavefront by VMware C# SDK lets you send raw data from your .NET application to Wavefront an `IWavefrontSender` interface. The data is then stored as metrics, histograms, and trace data. This SDK is also called the Wavefront Sender SDK for C#. 

Although this library is mostly used by the other Wavefront C# SDKs to send data to Wavefront, you can also use this SDK directly. For example, you can send data directly from a data store or CSV file to Wavefront.

**Before you start implementing, let us make sure you are using the correct SDK!**

![C# Sender SDK Decision Tree](docs/csharp_sender_sdk.png)

> ***Note***:
> </br>
>   * **This is the Wavefront by VMware SDK for C# (Wavefront Sender SDK for C#)!**
>   If this SDK is not what you were looking for, see the [table](#wavefront-sdks) below.


#### Wavefront SDKs
<table id="SDKlevels" style="width: 100%">
<tr>
  <th width="10%">SDK Type</th>
  <th width="45%">SDK Description</th>
  <th width="45%">Supported Languages</th>
</tr>

<tr>
  <td><a href="https://docs.wavefront.com/wavefront_sdks.html#sdks-for-collecting-trace-data">OpenTracing SDK</a></td>
  <td align="justify">Implements the OpenTracing specification. Lets you define, collect, and report custom trace data from any part of your application code. <br>Automatically derives Rate Errors Duration (RED) metrics from the reported spans. </td>
  <td>
    <ul>
    <li>
      <b>Java</b>: <a href ="https://github.com/wavefrontHQ/wavefront-opentracing-sdk-java">OpenTracing SDK</a> <b>|</b> <a href ="https://github.com/wavefrontHQ/wavefront-opentracing-bundle-java">Tracing Agent</a>
    </li>
    <li>
      <b>Python</b>: <a href ="https://github.com/wavefrontHQ/wavefront-opentracing-sdk-python">OpenTracing SDK</a>
    </li>
    <li>
      <b>Go</b>: <a href ="https://github.com/wavefrontHQ/wavefront-opentracing-sdk-go">OpenTracing SDK</a>
    </li>
    <li>
      <b>.Net/C#</b>: <a href ="https://github.com/wavefrontHQ/wavefront-opentracing-sdk-csharp">OpenTracing SDK</a>
    </li>
    </ul>
  </td>
</tr>

<tr>
  <td><a href="https://docs.wavefront.com/wavefront_sdks.html#sdks-for-collecting-metrics-and-histograms">Metrics SDK</a></td>
  <td align="justify">Implements a standard metrics library. Lets you define, collect, and report custom business metrics and histograms from any part of your application code.   </td>
  <td>
    <ul>
    <li>
    <b>Java</b>: <a href ="https://github.com/wavefrontHQ/wavefront-dropwizard-metrics-sdk-java">Dropwizard</a> <b>|</b> <a href ="https://github.com/wavefrontHQ/wavefront-runtime-sdk-jvm">JVM</a>
    </li>
    <li>
    <b>Python</b>: <a href ="https://github.com/wavefrontHQ/wavefront-pyformance">Pyformance SDK</a>
    </li>
    <li>
      <b>Go</b>: <a href ="https://github.com/wavefrontHQ/go-metrics-wavefront">Go Metrics SDK</a>
      </li>
    <li>
    <b>.Net/C#</b>: <a href ="https://github.com/wavefrontHQ/wavefront-appmetrics-sdk-csharp">App Metrics SDK</a>
    </li>
    </ul>
  </td>
</tr>

<tr>
  <td><a href="https://docs.wavefront.com/wavefront_sdks.html#sdks-that-instrument-frameworks">Framework SDK</a></td>
  <td align="justify">Reports predefined traces, metrics, and histograms from the APIs of a supported app framework. Lets you get started quickly with minimal code changes.</td>
  <td>
    <ul>
    <li><b>Java</b>:
    <a href="https://github.com/wavefrontHQ/wavefront-dropwizard-sdk-java">Dropwizard</a> <b>|</b> <a href="https://github.com/wavefrontHQ/wavefront-gRPC-sdk-java">gRPC</a> <b>|</b> <a href="https://github.com/wavefrontHQ/wavefront-jaxrs-sdk-java">JAX-RS</a> <b>|</b> <a href="https://github.com/wavefrontHQ/wavefront-jersey-sdk-java">Jersey</a></li>
    <li><b>.Net/C#</b>:
    <a href="https://github.com/wavefrontHQ/wavefront-aspnetcore-sdk-csharp">ASP.Net core</a> </li>
    <!--- [Python](wavefront_sdks_python.html#python-sdks-that-instrument-frameworks) --->
    </ul>
  </td>
</tr>

<tr>
  <td><a href="https://docs.wavefront.com/wavefront_sdks.html#sdks-for-sending-raw-data-to-wavefront">Sender SDK</a></td>
  <td align="justify">Lets you send raw data to Wavefront for storage as metrics, histograms, or traces, e.g., to import CSV data into Wavefront.
  </td>
  <td>
    <ul>
    <li>
    <b>Java</b>: <a href ="https://github.com/wavefrontHQ/wavefront-sdk-java">Sender SDK</a>
    </li>
    <li>
    <b>Python</b>: <a href ="https://github.com/wavefrontHQ/wavefront-sdk-python">Sender SDK</a>
    </li>
    <li>
    <b>Go</b>: <a href ="https://github.com/wavefrontHQ/wavefront-sdk-go">Sender SDK</a>
    </li>
    <li>
    <b>.Net/C#</b>: <a href ="https://github.com/wavefrontHQ/wavefront-sdk-csharp">Sender SDK</a>
    </li>
    <li>
    <b>C++</b>: <a href ="https://github.com/wavefrontHQ/wavefront-sdk-cpp">Sender SDK</a>
    </li>
    </ul>
  </td>
</tr>

</tbody>
</table>

## Prerequisites
* Supported Frameworks
  * .NET Framework (>= 4.5.2)
  * .NET Standard (>= 2.0)

* Installation
  
  Install the [NuGet package](https://www.nuget.org/packages/Wavefront.SDK.CSharp/) using the Package Manager Console or the .NET CLI Console
  
  * Package Manager Console
      ```
      PM> Install-Package Wavefront.SDK.CSharp
      ```
  * .NET CLI Console
      ```
      > dotnet add package Wavefront.SDK.CSharp
      ```
  
## Set Up an IWavefrontSender

You can send metrics, histograms, or trace data from your application to the Wavefront service using a Wavefront proxy or direct ingestions.

* Option 1: Use a [**Wavefront proxy**](https://docs.wavefront.com/proxies.html), which then forwards the data to the Wavefront service. This is the recommended choice for a large-scale deployment that needs resilience to internet outages, control over data queuing and filtering, and more.
[Create a ProxyConfiguration](#option-1-sending-data-via-the-wavefront-proxy) to send data to a Wavefront proxy.
  
* Use [**direct ingestion**](https://docs.wavefront.com/direct_ingestion.html) to send the data directly to the Wavefront service. This is the simplest way to get up and running quickly.
[Create a DirectConfiguration](#option-2-sending-data-via-direct-ingestion) to send data directly to a Wavefront service.
  
### Option 1: Sending Data via the Wavefront Proxy

Before data can be sent from your application, you must ensure the Wavefront proxy is configured and running:
* [Install](http://docs.wavefront.com/proxies_installing.html) a Wavefront proxy on the specified proxy host .
* [Configure](http://docs.wavefront.com/proxies_configuring.html) the proxy to listen to the specified port(s) by setting the corresponding properties: `pushListenerPorts`, `histogramDistListenerPorts`, `traceListenerPorts`
* Start (or restart) the proxy.

```csharp
// Create the builder with the proxy hostname or address
WavefrontProxyClient.Builder wfProxyClientBuilder = new WavefrontProxyClient.Builder(proxyHostName);

// Note: At least one of metrics/histogram/tracing port is required.
// Only set a port if you wish to send that type of data to Wavefront and you
// have the port enabled on the proxy.

// Set the pushListenerPort (example: 2878) to send metrics to Wavefront
wfProxyClientBuilder.MetricsPort(2878);

// Set the histogramDistListenerPort (example: 2878) to send histograms to Wavefront
wfProxyClientBuilder.DistributionPort(2878);

// Set the traceListenerPort (example: 30,000) to send opentracing spans to Wavefront
wfProxyClientBuilder.TracingPort(30_000);

// Optional: Set this to override the default flush interval of 5 seconds
wfProxyClientBuilder.FlushIntervalSeconds(2);

// Finally create a WavefrontProxyClient
IWavefrontSender wavefrontSender = wfProxyClientBuilder.Build();
```

### Option 2: Sending Data via Direct Ingestion
To create a `WavefrontDirectIngestionClient`, you must have access to a Wavefront instance with [direct data ingestion permission](https://docs.wavefront.com/permissions_overview.html):

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
 
 Wavefront supports different metric types, such as gauges, counters, delta counters, histograms, traces, and spans. See [Metrics](https://docs.wavefront.com/metric_types.html) for details. To send data to Wavefront using `IWavefrontSender` you need to instantiate the following:
 * [Metrics and Delta Counters](#Metrics-and-Delta-Counters)
 * [Distributions (Histograms)](#distributions-histograms)
 * [Tracing Spans](#Tracing-Spans)

### Metrics and Delta Counters

```csharp
// Wavefront Metrics Data format
// <metricName> <metricValue> [<timestamp>] source=<source> [pointTags]
// Example: "new-york.power.usage 42422 1533529977 source=localhost datacenter=dc1"
wavefrontSender.SendMetric(
    "new-york.power.usage",
    42422.0,
    DateTimeUtils.UnixTimeMilliseconds(DateTime.UtcNow),
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
    DateTimeUtils.UnixTimeMilliseconds(DateTime.UtcNow),
    "appServer1",
    new Dictionary<string, string> { { "region", "us-west" } }.ToImmutableDictionary()
);
```

### Tracing Spans

When you use a Sender SDK, you won’t see span-level RED metrics by default unless you use the Wavefront proxy and define a custom tracing port (`TracingPort`). See [Instrument Your Application with Wavefront Sender SDKs](https://docs.wavefront.com/tracing_instrumenting_frameworks.html#instrument-your-application-with-wavefront-sender-sdks) for details.


```csharp
 // Wavefront Tracing Span Data format
 // <tracingSpanName> source=<source> [pointTags] <start_millis> <duration_milliseconds>
 // Example: "getAllUsers source=localhost
 //           traceId=7b3bf470-9456-11e8-9eb6-529269fb1459
 //           spanId=0313bafe-9457-11e8-9eb6-529269fb1459
 //           parent=2f64e538-9457-11e8-9eb6-529269fb1459
 //           application=Wavefront http.method=GET
 //           1552949776000 343"
wavefrontSender.SendSpan(
    "getAllUsers",
    DateTimeUtils.UnixTimeMilliseconds(DateTime.UtcNow),
    343,
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
Before shutting down your application, flush the buffer and close the sender.

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

## License
[Apache 2.0 License](LICENSE).

## How to Get Support and Contribute

* Reach out to us on our public [Slack channel](https://www.wavefront.com/join-public-slack).
* If you run into any issues, let us know by creating a GitHub issue.
