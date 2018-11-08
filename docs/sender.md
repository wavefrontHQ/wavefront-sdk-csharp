# Using Wavefront Sender in Wavefront SDKs

Several Wavefront C# SDKs such as [wavefront-appmetrics-sdk-csharp](https://github.com/wavefrontHQ/wavefront-appmetrics-sdk-csharp), [wavefront-aspnetcore-sdk-csharp](https://github.com/wavefrontHQ/wavefront-aspnetcore-sdk-csharp), [wavefront-opentracing-sdk-csharp](https://github.com/wavefrontHQ/wavefront-opentracing-sdk-csharp) etc. use this library and require an `IWavefrontSender` instance.

If you are using multiple Wavefront C# SDKs within the same process, you can instantiate the IWavefrontSender just once and share it amongst the SDKs. For example:

```csharp
// assuming you have a configuration file
IWavefrontSender wavefrontSender = BuildProxyOrDirectSender(config);

// Create a Wavefront open tracing reporter
IReporter spanReporter = new WavefrontSpanReporter.Builder()
  .WithSource("wavefront-tracing-example").
  .Build(wavefrontSender);

// Create an App Metrics registry that reports to Wavefront
IMetricsRoot metrics = new MetricsBuilder()
  .Report.ToWavefront(wavefrontSender)
  .Build();
...
```

However, if you use the SDKs on different processes, you would need to instantiate one IWavefrontSender instance per process.