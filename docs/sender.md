# Set Up an IWavefrontSender Instance
You can choose to send metrics, histograms, or trace data from your application to the Wavefront service using one of the following techniques:
* Use [direct ingestion](https://docs.wavefront.com/direct_ingestion.html) to send the data directly to the Wavefront service. This is the simplest way to get up and running quickly.
* Use a [Wavefront proxy](https://docs.wavefront.com/proxies.html), which then forwards the data to the Wavefront service. This is the recommended choice for a large-scale deployment that needs resilience to internet outages, control over data queuing and filtering, and more. 

The `IWavefrontSender` interface has two implementations. Instantiate the implementation that corresponds to your choice:
* Option 1: [Create a `WavefrontDirectIngestionClient`](#option-1-create-a-wavefrontdirectingestionclient) to send data directly to a Wavefront service.
* Option 2: [Create a `WavefrontProxyClient`](#option-2-create-a-wavefrontproxyclient) to send data to a Wavefront proxy.

## Option 1: Create a WavefrontDirectIngestionClient
To create a `WavefrontDirectIngestionClient`, you build it with the information it needs to send data directly to Wavefront.

### Step 1. Obtain Wavefront Access Information
Gather the following access information:

* Identify the URL of your Wavefront instance. This is the URL you connect to when you log in to Wavefront, typically something like `https://mydomain.wavefront.com`.
* In Wavefront, verify that you have Direct Data Ingestion permission, and [obtain an API token](http://docs.wavefront.com/wavefront_api.html#generating-an-api-token).

### Step 2. Initialize the WavefrontDirectIngestionClient
You initialize a `WavefrontDirectIngestionClient` by building it with the access information you obtained in Step 1.

You can optionally call builder methods to tune the following ingestion properties:

* Max queue size - Internal buffer capacity of the Wavefront sender. Any data in excess of this size is dropped.
* Flush interval - Interval for flushing data from the Wavefront sender directly to Wavefront.
* Batch size - Amount of data to send to Wavefront in each flush interval.

Together, the batch size and flush interval control the maximum theoretical throughput of the Wavefront sender. You should override the defaults _only_ to set higher values.

```csharp
// Create a builder with the Wavefront URL and a Wavefront API token
// that was created with direct ingestion permission.
WavefrontDirectIngestionClient.Builder wfDirectIngestionClientBuilder =
  new WavefrontDirectIngestionClient.Builder(wavefrontURL, token);

// Optional: Override the max queue size (in data points). Default: 50,000
wfDirectIngestionClientBuilder.MaxQueueSize(100_000);

// Optional: Override the batch size (in data points). Default: 10,000
wfDirectIngestionClientBuilder.BatchSize(20_000);

// Optional: Override the flush interval (in seconds). Default: 1 second
wfDirectIngestionClientBuilder.FlushIntervalSeconds(2);

// Create a WavefrontDirectIngestionClient.
IWavefrontSender wavefrontSender = wfDirectIngestionClientBuilder.Build();
 ```

## Option 2: Create a WavefrontProxyClient

**Note:** Before your application can use a `WavefrontProxyClient`, you must [set up and start a Wavefront proxy](https://github.com/wavefrontHQ/java/tree/master/proxy#set-up-a-wavefront-proxy).

To create a `WavefrontProxyClient`, you build it with the information it needs to send data to a Wavefront proxy, including:

* The name of the host that will run the Wavefront proxy.
* One or more proxy listening ports to send data to. The ports you specify depend on the kinds of data you want to send (metrics, histograms, and/or trace data). You must specify at least one listener port. 
* Optional settings for tuning communication with the proxy.


```csharp
// Create the builder with the proxy hostname or address
WavefrontProxyClient.Builder wfProxyClientBuilder = new WavefrontProxyClient.Builder(proxyHostName);

// Set the proxy port to send metrics to. Default: 2878
wfProxyClientBuilder.MetricsPort(2878);

// Set a proxy port to send histograms to.  Recommended: 2878
wfProxyClientBuilder.DistributionPort(2878);

// Set a proxy port to send trace data to. Recommended: 30000
wfProxyClientBuilder.TracingPort(30_000);

// Optional: Set a nondefault interval (in seconds) for flushing data from the sender to the proxy. Default: 5 seconds
wfProxyClientBuilder.FlushIntervalSeconds(2);

// Create the WavefrontProxyClient
IWavefrontSender wavefrontSender = wfProxyClientBuilder.Build();
 ```
**Note:** When you [set up a Wavefront proxy](https://github.com/wavefrontHQ/java/tree/master/proxy#set-up-a-wavefront-proxy) on the specified proxy host, you specify the port it will listen to for each type of data to be sent. The `WavefrontProxyClient` must send data to the same ports that the Wavefront proxy listens to. Consequently, the port-related builder methods must specify the same port numbers as the corresponding proxy configuration properties: 

| `WavefrontProxyClient` builder method | Corresponding property in `wavefront.conf` |
| ----- | -------- |
| `MetricsPort()` | `pushListenerPorts=` |
| `DistributionPort()` | `histogramDistListenerPorts=` |
| `TracingPort()` | `traceListenerPorts=` |
 
# Share an IWavefrontSender Instance

Several Wavefront SDKs for C# use this library and require an `IWavefrontSender` instance.

If you are using multiple Wavefront C# SDKs within the same process, you can instantiate the `IWavefrontSender` just once and share it among the SDKs. 

For example, the following snippet shows how to use the same Wavefront sender when setting up the [wavefront-appmetrics-sdk-csharp](https://github.com/wavefrontHQ/wavefront-appmetrics-sdk-csharp) and  [wavefront-opentracing-sdk-csharp](https://github.com/wavefrontHQ/wavefront-opentracing-sdk-csharp) SDKs.

```csharp
// Create a Wavefront sender, assuming you have a configuration file
IWavefrontSender wavefrontSender = BuildProxyOrDirectSender(config);

// Create a WavefrontSpanReporter for the OpenTracing SDK
IReporter spanReporter = new WavefrontSpanReporter.Builder()
  .WithSource("wavefront-tracing-example")
  .Build(wavefrontSender);

// Create a Wavefront reporter for the App Metrics SDK
IMetricsRoot metrics = new MetricsBuilder()
  .Report.ToWavefront(wavefrontSender)
  .Build();
...
```

**Note:** If you use the SDKs in different processes, you must instantiate one `IWavefrontSender` instance per process.
