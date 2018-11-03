# Application Tags

Several Wavefront C# SDKs such as `wavefront-aspnetcore-sdk-csharp`, `wavefront-opentracing-sdk-csharp` etc. require `ApplicationTags`.

Application tags determine the metadata (aka point/span tags) that are included with any metrics/histograms/spans reported to Wavefront.

The following tags are mandatory:
* `application`: The name of your C# application, for example: `OrderingApp`.
* `service`: The name of the microservice within your application, for example: `inventory`.

The following tags are optional:
* `cluster`: For example: `us-west-2`.
* `shard`: The shard (aka mirror), for example: `secondary`.

You can also optionally add custom tags specific to your application in the form of a `IDictionary` (see snippet below).

To create the application tags:
```csharp
string application = "OrderingApp";
string service = "inventory";
string cluster = "us-west-2";
string shard = "secondary";

var customTags = new Dictionary<string, string>
{
  { "location", "Oregon" },
  { "env", "Staging" }
};

var applicationTags = new ApplicationTags.Builder(application, service)
    .Cluster(cluster)       // optional
    .Shard(shard)           // optional
    .CustomTags(customTags) // optional
    .Build();
```

You would typically define the above metadata in your application's configuration and create the `ApplicationTags`.
