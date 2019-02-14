using System;

namespace Wavefront.SDK.CSharp.Common
{
    /// <summary>
    /// Common Util methods relating to DateTime.
    /// </summary>
    public static class DateTimeUtils
    {
        private static readonly long UnixEpochTicks =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        private static readonly long UnixEpochMilliseconds =
            UnixEpochTicks / TimeSpan.TicksPerMillisecond;
        private static readonly long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        private static readonly long UnixEpochMicroseconds = UnixEpochTicks / TicksPerMicrosecond;

        /// <summary>
        /// Converts a <see cref="DateTime"/> object to the number of milliseconds elapsed since
        /// the Unix epoch.
        /// </summary>
        /// <returns>The timestamp as milliseconds elapsed since the epoch.</returns>
        /// <param name="dateTime">A <see cref="DateTime"/> object.</param>
        public static long UnixTimeMilliseconds(DateTime dateTime)
        {
            long milliseconds = dateTime.ToUniversalTime().Ticks / TimeSpan.TicksPerMillisecond;
            return milliseconds - UnixEpochMilliseconds;
        }

        /// <summary>
        /// Converts a <see cref="DateTime"/> object to the number of microseconds elapsed since
        /// the Unix epoch.
        /// </summary>
        /// <returns>The timestamp as microseconds elapsed since the epoch.</returns>
        /// <param name="dateTime">A <see cref="DateTime"/> object.</param>
        public static long UnixTimeMicroseconds(DateTime dateTime)
        {
            long microseconds = dateTime.ToUniversalTime().Ticks / TicksPerMicrosecond;
            return microseconds - UnixEpochMicroseconds;
        }

        /// <summary>
        /// Converts a duration from TimeSpan to microseconds elapsed.
        /// </summary>
        /// <returns>The duration in microseconds.</returns>
        /// <param name="duration">The duration as a TimeSpan.</param>
        public static long TimeSpanToMicroseconds(TimeSpan duration)
        {
            return duration.Ticks / TicksPerMicrosecond;
        }
    }
}
