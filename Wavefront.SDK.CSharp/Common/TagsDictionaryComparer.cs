using System.Collections.Generic;
using System.Linq;

namespace Wavefront.SDK.CSharp.Common
{
    /// <summary>
    /// Equality comparer for <see cref="IDictionary{string, string}"/> that checks if the
    /// dictionary contents match.
    /// </summary>
    public class TagsDictionaryComparer : IEqualityComparer<IDictionary<string, string>>
    {
        public bool Equals(IDictionary<string, string> dict1, IDictionary<string, string> dict2)
        {
            if (dict1 == null && dict2 == null)
            {
                return true;
            }
            if (dict1 == null || dict2 == null)
            {
                return false;
            }
            return dict1.Count == dict2.Count && !dict1.Except(dict2).Any();
        }

        public int GetHashCode(IDictionary<string, string> dict)
        {
            int hash = 0;
            foreach (var entry in dict)
            {
                int miniHash = 17;
                miniHash = miniHash * 31 +
                       EqualityComparer<string>.Default.GetHashCode(entry.Key);
                miniHash = miniHash * 31 +
                       EqualityComparer<string>.Default.GetHashCode(entry.Value);
                hash ^= miniHash;
            }
            return hash;
        }
    }
}
