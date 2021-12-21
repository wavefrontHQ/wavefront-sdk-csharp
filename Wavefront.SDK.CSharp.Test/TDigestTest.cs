using System;
using System.Collections.Generic;
using Wavefront.SDK.CSharp.Entities.StatsLib;
using Xunit;

namespace Wavefront.SDK.CSharp.Test
{
    /// <summary>
    /// Unit tests for <see cref="TDigest"/>.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class TDigestTest
    {
        [Fact]
        public void AccuracyMustBePositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TDigest(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TDigest(-1));
        }

        [Fact]
        public void WeightMustBePositive()
        {
            var digest = new TDigest();
            Assert.Throws<ArgumentOutOfRangeException>(() => digest.Add(1, -1));
        }

        [Fact]
        public void Count()
        {
            var digest = new TDigest();
            for (var i = 0; i < 100; i++)
            {
                digest.Add(0);
                Assert.Equal(i + 1, digest.Count);
            }
        }

        [Fact]
        public void MaxValue()
        {
            var digest = new TDigest();
            for (var i = 0; i < 100; i++)
            {
                digest.Add(i);
                Assert.Equal(i, digest.Max);
            }
        }

        [Fact]
        public void NegativeMax()
        {
            var digest = new TDigest();
            digest.Add(-1);
            Assert.Equal(-1, digest.Max);
        }

        [Fact]
        public void MinValue()
        {
            var digest = new TDigest();
            for (var i = 100; i > 0; i--)
            {
                digest.Add(i);
                Assert.Equal(i, digest.Min);
            }
        }

        [Fact]
        public void GetsAllDistributionPoints()
        {
            var digest = new TDigest();
            var total = new Random().Next(10, 100);
            for (int i = 0; i < total; i++)
            {
                digest.Add(i);
            }

            var points = digest.GetDistribution();
            Assert.Equal(total, points.Length);
            for (int i = 0; i < total; i++)
            {
                Assert.Equal(i, points[i].Value);
            }
        }

        [Fact]
        public void Average()
        {
            var digest = new TDigest();
            var total = 0.0;
            for (var i = 100; i > 0; i--)
            {
                var value = new Random().NextDouble();
                total += value;
                digest.Add(value);
            }

            Assert.Equal(total / 100, digest.Average, 10);
        }

        [Fact]
        public void QuantileEdges()
        {
            var digest = new TDigest();
            var min = Double.MaxValue;
            var max = Double.MinValue;
            for (var i = 100; i > 0; i--)
            {
                var value = new Random().NextDouble();
                if (value > max) max = value;
                if (value < min) min = value;
                digest.Add(value);
            }

            Assert.Equal(min, digest.Quantile(0.001));
            Assert.Equal(max, digest.Quantile(1));
        }

        [Fact]
        public void Quantile()
        {
            var digest = new TDigest();
            for (var i = 0; i <= 9; i++)
            {
                digest.Add(i);
            }
            Assert.Equal(2, digest.Quantile(.29999999));
            Assert.Equal(3, digest.Quantile(.3));
            Assert.Equal(3, digest.Quantile(.39999999));

            Assert.Equal(8, digest.Quantile(.8));
            Assert.Equal(9, digest.Quantile(.99999999));
        }
    }

    public class ListExtensionsTest
    {
        [Fact]
        public void ShuffleEmptyList()
        {
            var points = new List<DistributionPoint>();
            points.Shuffle();

            Assert.Empty(points);
        }

        [Fact]
        public void ShuffleListOfOneItem()
        {
            var p = new DistributionPoint(1, 1);
            var points = new List<DistributionPoint> {p};
            points.Shuffle();

            Assert.Single(points);
            Assert.Contains(p, points);
        }

        [Fact]
        public void Shuffle()
        {
            var points = new List<DistributionPoint>();
            for (var i = 0; i < 5; i++)
            {
                points.Add(new DistributionPoint(i, 1));
            }

            points.Shuffle();

            Assert.Equal(5, points.Count);
            bool orderChanged = false;
            for (var i = 0; i < 5; i++)
            {
                if (Math.Abs(points[i].Value - i) > 0.1)
                {
                    orderChanged = true;
                }
            }

            Assert.True(orderChanged);
        }
    }
}