using System.Collections.Generic;
using Wavefront.SDK.CSharp.Common;
using Xunit;

namespace Wavefront.SDK.CSharp.Test
{
    /// <summary>
    /// Unit tests for <see cref="TagsDictionaryComparer"/>.
    /// </summary>
    public class TagsDictionaryComparerTest
    {
        private readonly TagsDictionaryComparer comparer = new TagsDictionaryComparer();
        private readonly IDictionary<string, string> dict1 = new SortedDictionary<string, string>()
        {
            { "application", "myApp" },
            { "service", "myService" },
            { "customId", "123" }
        };
        private readonly IDictionary<string, string> dict2 = new SortedDictionary<string, string>()
        {
            { "service", "myService" },
            { "application", "myApp" },
            { "customId", "123" }
        };
        private readonly IDictionary<string, string> dict3 = new SortedDictionary<string, string>()
        {
            { "application", "myApp" },
            { "service", "myService" },
            { "customId", "456" }
        };
        private readonly IDictionary<string, string> dict4 = new SortedDictionary<string, string>()
        {
            { "application", "myApp" },
            { "service", "myService" }
        };

        [Fact]
        public void TestEquals()
        {
            Assert.True(comparer.Equals(dict1, dict2));
            Assert.False(comparer.Equals(dict1, dict3));
            Assert.False(comparer.Equals(dict1, dict4));
        }

        [Fact]
        public void TestGetHashCode()
        {
            int hash1 = comparer.GetHashCode(dict1);
            int hash2 = comparer.GetHashCode(dict2);
            int hash3 = comparer.GetHashCode(dict3);
            int hash4 = comparer.GetHashCode(dict4);
            Assert.Equal(hash1, hash2);
            Assert.NotEqual(hash1, hash3);
            Assert.NotEqual(hash1, hash4);
        }
    }
}
