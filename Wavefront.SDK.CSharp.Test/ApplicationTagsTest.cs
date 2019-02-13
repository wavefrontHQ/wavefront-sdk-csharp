using System.Collections.Generic;
using Wavefront.SDK.CSharp.Common;
using Wavefront.SDK.CSharp.Common.Application;
using Xunit;

namespace Wavefront.SDK.CSharp.Test
{
    /// <summary>
    /// Unit tests for <see cref="ApplicationTags"/>
    /// </summary>
    public class ApplicationTagsTest
    {
        private readonly ApplicationTags applicationTags = new ApplicationTags.Builder("app1", "service1")
            .Cluster("us-west")
            .Shard("db1")
            .CustomTags(new Dictionary<string, string>
            {
                { "env", "production" },
                { "location", "SF" }
            })
            .Build();

        [Fact]
        public void TestApplicationTags()
        {
            Assert.Equal("app1", applicationTags.Application);
            Assert.Equal("service1", applicationTags.Service);
            Assert.Equal("us-west", applicationTags.Cluster);
            Assert.Equal("db1", applicationTags.Shard);
            Assert.Equal(new Dictionary<string, string>
            {
                { "env", "production" },
                { "location", "SF" }
            }, applicationTags.CustomTags);
        }

        [Fact]
        public void TestToPointTags()
        {
            var pointTags = applicationTags.ToPointTags();
            Assert.Equal(6, pointTags.Count);
            Assert.Contains(new KeyValuePair<string, string>(Constants.ApplicationTagKey, "app1"), pointTags);
            Assert.Contains(new KeyValuePair<string, string>(Constants.ServiceTagKey, "service1"), pointTags);
            Assert.Contains(new KeyValuePair<string, string>(Constants.ClusterTagKey, "us-west"), pointTags);
            Assert.Contains(new KeyValuePair<string, string>(Constants.ShardTagKey, "db1"), pointTags);
            Assert.Contains(new KeyValuePair<string, string>("env", "production"), pointTags);
            Assert.Contains(new KeyValuePair<string, string>("location", "SF"), pointTags);
        }
    }
}
