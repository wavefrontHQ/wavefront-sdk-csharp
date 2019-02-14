using System.Collections.Generic;
using System.Collections.Immutable;

namespace Wavefront.SDK.CSharp.Common.Application
{
    /// <summary>
    /// Metadata about your application represented as tags in Wavefront.
    /// </summary>
    public class ApplicationTags
    {
        /// <summary>
        /// Gets the application name.
        /// </summary>
        /// <value>The name of the application.</value>
        public string Application { get; private set; }

        /// <summary>
        /// Gets the cluster name.
        /// </summary>
        /// <value>The name of the cluster.</value>
        public string Cluster { get; private set; }

        /// <summary>
        /// Gets the service name.
        /// </summary>
        /// <value>The name of the service.</value>
        public string Service { get; private set; }

        /// <summary>
        /// Gets the shard name.
        /// </summary>
        /// <value>The name of the shard.</value>
        public string Shard { get; private set; }

        /// <summary>
        /// Gets the custom tags.
        /// </summary>
        /// <value>The custom tags.</value>
        public IDictionary<string, string> CustomTags { get; private set; }

        private ApplicationTags()
        {
        }

        public class Builder
        {
            // Required parameters
            private readonly string application;
            private readonly string service;

            // Optional parameters
            private string cluster;
            private string shard;
            private IDictionary<string, string> customTags = new Dictionary<string, string>();

            /// <summary>
            /// Builder to build ApplicationTags
            /// </summary>
            /// <param name="application">Name of the applicaiton</param>
            /// <param name="service">Name of the service.</param>
            public Builder(string application, string service)
            {
                this.application = application;
                this.service = service;
            }

            /// <summary>
            /// Set the cluster (example: us-west-1/us-west-2 etc.) in which your application is
            /// running.
            /// This setting is optional.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="cluster">Cluster in which your application is running.</param>
            public Builder Cluster(string cluster)
            {
                this.cluster = cluster;
                return this;
            }

            /// <summary>
            /// Set the shard (example: primary/secondary etc.) in which your application is
            /// running.
            /// This setting is optional.
            /// </summary>
            /// <returns><see cref="this"/></returns>
            /// <param name="shard">Shard where your application is running.</param>
            public Builder Shard(string shard)
            {
                this.shard = shard;
                return this;
            }

            /// <summary>
            /// Set additional custom tags for your application.
            /// For instance: {location: SF}, {env: Staging} etc.
            /// This setting is optional.
            /// </summary>
            /// <returns><see cref="this"/>.</returns>
            /// <param name="customTags">
            /// Additional custom tags/metadata for your application.
            /// </param>
            public Builder CustomTags(IDictionary<string, string> customTags)
            {
                this.customTags = customTags;
                return this;
            }

            /// <summary>
            /// Build application tags.
            /// </summary>
            /// <returns><see cref="ApplicationTags"/></returns>
            public ApplicationTags Build()
            {
                return new ApplicationTags()
                {
                    Application = application,
                    Cluster = cluster,
                    Service = service,
                    Shard = shard,
                    CustomTags = customTags
                };
            }
        }

        /// <summary>
        /// Converts ApplicationTags to an <see cref="ImmutableDictionary"/> of point tags.
        /// </summary>
        /// <returns>An immutable dictionary of point tags.</returns>
        public IDictionary<string, string> ToPointTags()
        {
            var pointTags = new Dictionary<string, string>
            {
                { Constants.ApplicationTagKey, Application },
                { Constants.ClusterTagKey, Cluster ?? Constants.NullTagValue },
                { Constants.ServiceTagKey, Service },
                { Constants.ShardTagKey, Shard ?? Constants.NullTagValue }
            };

            if (CustomTags != null)
            {
                foreach (var customTag in CustomTags)
                {
                    if (!pointTags.ContainsKey(customTag.Key))
                    {
                        pointTags.Add(customTag.Key, customTag.Value);
                    }
                }
            }

            return pointTags.ToImmutableDictionary();
        }
    }
}
