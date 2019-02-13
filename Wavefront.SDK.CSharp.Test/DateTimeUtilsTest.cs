using System;
using Xunit;
using static Wavefront.SDK.CSharp.Common.DateTimeUtils;

namespace Wavefront.SDK.CSharp.Test
{
    /// <summary>
    /// Unit tests for <see cref="DateTimeUtils"/>.
    /// </summary>
    public class DateTimeUtilsTest
    {
        [Fact]
        public void TestUnixTimeMilliseconds()
        {
            DateTime utcTimestamp = new DateTime(2019, 1, 1, 23, 59, 59, 999, DateTimeKind.Utc);
            Assert.Equal(1546387199999L, UnixTimeMilliseconds(utcTimestamp));

            utcTimestamp = new DateTime(636818976012345670L);
            Assert.Equal(1546300801234L, UnixTimeMilliseconds(utcTimestamp));
        }

        [Fact]
        public void TestUnixTimeMicroseconds()
        {
            DateTime utcTimestamp = new DateTime(2019, 1, 1, 23, 59, 59, 999, DateTimeKind.Utc);
            Assert.Equal(1546387199999000L, UnixTimeMicroseconds(utcTimestamp));

            utcTimestamp = new DateTime(636818976012345670L);
            Assert.Equal(1546300801234567L, UnixTimeMicroseconds(utcTimestamp));
        }

        [Fact]
        public void TestTimeSpanToMicroseconds()
        {
            DateTime timestamp1 = new DateTime(636818976012345670L);
            DateTime timestamp2 = new DateTime(636819839999990000L);
            TimeSpan duration = timestamp2 - timestamp1;
            Assert.Equal(86398764433L, TimeSpanToMicroseconds(duration));
        }
    }
}
