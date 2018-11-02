using System;

namespace Wavefront.SDK.CSharp.Entities.Histograms
{
    /// <summary>
    /// Granularity (minute, hour, or day) by which histograms distributions are aggregated.
    /// </summary>
    public sealed class HistogramGranularity : IComparable
    {
        public readonly string Identifier;
        private readonly int Ordinal;

        public static readonly HistogramGranularity Minute = new HistogramGranularity("!M", 0);
        public static readonly HistogramGranularity Hour = new HistogramGranularity("!H", 1);
        public static readonly HistogramGranularity Day = new HistogramGranularity("!D", 2);

        private HistogramGranularity(string identifier, int ordinal)
        {
            Identifier = identifier;
            Ordinal = ordinal;
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            if (obj is HistogramGranularity histogramGranularity)
            {
                return Ordinal.CompareTo(histogramGranularity.Ordinal);
            }
            else
            {
                throw new ArgumentException("Object is not a HistogramGranularity");
            }
        }
    }
}
