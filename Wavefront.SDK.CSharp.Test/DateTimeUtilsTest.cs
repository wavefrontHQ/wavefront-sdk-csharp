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
            DateTime dateTime = new DateTime(2019, 1, 1, 23, 59, 59, 999, DateTimeKind.Utc);
            Assert.Equal(1546387199999L, UnixTimeMilliseconds(dateTime));

            dateTime = new DateTime(636818976012345670L, DateTimeKind.Utc);
            Assert.Equal(1546300801234L, UnixTimeMilliseconds(dateTime));
        }

        [Fact]
        public void TestUnixTimeMicroseconds()
        {
            DateTime dateTime = new DateTime(2019, 1, 1, 23, 59, 59, 999, DateTimeKind.Utc);
            Assert.Equal(1546387199999000L, UnixTimeMicroseconds(dateTime));

            dateTime = new DateTime(636818976012345670L, DateTimeKind.Utc);
            Assert.Equal(1546300801234567L, UnixTimeMicroseconds(dateTime));
        }

        [Fact]
        public void TestTimeSpanToMicroseconds()
        {
            DateTime dateTime1 = new DateTime(636818976012345670L);
            DateTime dateTime2 = new DateTime(636819839999990000L);
            TimeSpan duration = dateTime2 - dateTime1;
            Assert.Equal(86398764433L, TimeSpanToMicroseconds(duration));
        }
    }
}
