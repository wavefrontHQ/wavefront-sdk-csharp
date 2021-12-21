using C5;
using System;
using System.Linq;
using System.Collections.Generic;
// ReSharper disable InlineOutVariableDeclaration

namespace Wavefront.SDK.CSharp.Entities.StatsLib
{
    // ReSharper disable once InconsistentNaming
    public class TDigest
    {
        private TreeDictionary<double, Centroid> centroids;
        private static Random _rand;
        private double count;

        /// <summary>
        /// Returns the sum of the weights of all objects added to the Digest.
        /// Since the default weight for each object is 1, this will be equal to the number
        /// of objects added to the digest unless custom weights are used.
        /// </summary>
        public double Count => count;

        /// <summary>
        /// Returns the number of Internal Centroid objects allocated.
        /// The number of these objects is directly proportional to the amount of memory used.
        /// </summary>
        public int CentroidCount => centroids.Count;

        /// <summary>
        /// Gets the Accuracy setting as specified in the constructor.
        /// Smaller numbers result in greater accuracy at the expense of
        /// poorer performance and greater memory consumption
        /// Default is .02
        /// </summary>
        private double Accuracy { get; set; }

        /// <summary>
        /// The Compression Constant Setting
        /// </summary>
        private double CompressionConstant { get; set; }

        /// <summary>
        /// The Average
        /// </summary>
        public double Average => count > 0 ? newAvg : 0;

        /// <summary>
        /// The Max
        /// </summary>
        public double Max { get; private set; }


        /// <summary>
        /// The Min
        /// </summary>
        public double Min { get; private set; }

        private double newAvg, oldAvg;

        /// <summary>
        /// Construct a T-Digest,
        /// </summary>
        /// <param name="accuracy">Controls the trade-off between accuracy and memory consumption/performance.
        /// Default value is .05, higher values result in worse accuracy, but better performance and decreased memory usage, while
        /// lower values result in better accuracy and increased performance and memory usage</param>
        /// <param name="compression">K value</param>
        public TDigest(double accuracy = 0.02, double compression = 25)
        {
            if (accuracy <= 0) throw new ArgumentOutOfRangeException(nameof(accuracy), "must be greater than 0");
            if (compression < 15) throw new ArgumentOutOfRangeException(nameof(compression), "must be 15 or greater");

            centroids = new TreeDictionary<double, Centroid>();
            _rand = new Random();
            count = 0;
            Accuracy = accuracy;
            CompressionConstant = compression;
        }

        /// <summary>
        /// Add a new value to the T-Digest. Note that this method is NOT thread safe.
        /// </summary>
        /// <param name="value">The value to add</param>
        /// <param name="weight">The relative weight associated with this value. Default is 1 for all values.</param>
        public void Add(double value, double weight = 1)
        {
            if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight), "must be greater than 0");

            var first = count == 0;
            count += weight;

            if (first)
            {
                oldAvg = value;
                newAvg = value;
                Min = value;
                Max = value;
            }
            else
            {
                newAvg = oldAvg + (value - oldAvg) / count;
                oldAvg = newAvg;
                Max = value > Max ? value : Max;
                Min = value < Min ? value : Min;
            }

            if (centroids.Count == 0)
            {
                centroids.Add(value, new Centroid(value, weight));
                return;
            }

            var closest = GetClosestCentroids(value);

            var candidates = closest
                .Select(c => new
                {
                    Threshold = GetThreshold(ComputeCentroidQuantile(c)),
                    Centroid = c
                })
                .Where(c => c.Centroid.Count + weight < c.Threshold)
                .ToList();

            while (candidates.Count > 0 & weight > 0)
            {
                var cData = candidates[_rand.Next() % candidates.Count];
                var deltaW = Math.Min(cData.Threshold - cData.Centroid.Count, weight);

                double oldMean;
                if (cData.Centroid.Update(deltaW, value, out oldMean))
                {
                    ReInsertCentroid(oldMean, cData.Centroid);
                }

                weight -= deltaW;
                candidates.Remove(cData);
            }

            if (weight > 0)
            {
                var toAdd = new Centroid(value, weight);

                if (centroids.FindOrAdd(value, ref toAdd))
                {
                    double oldMean;

                    if (toAdd.Update(weight, toAdd.Mean, out oldMean))
                    {
                        ReInsertCentroid(oldMean, toAdd);
                    }
                }
            }

            if (centroids.Count > (CompressionConstant / Accuracy))
            {
                Compress();
            }
        }

        /// <summary>
        /// Estimates the specified quantile
        /// </summary>
        /// <param name="quantile">The quantile to estimate. Must be between 0 and 1.</param>
        /// <returns>The value for the estimated quantile</returns>
        public double Quantile(double quantile)
        {
            if (quantile < 0 || quantile > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(quantile), "must be between 0 and 1");
            }

            if (centroids.Count == 0)
            {
                throw new InvalidOperationException(
                    "Cannot call Quantile() method until first Adding values to the digest");
            }

            if (centroids.Count == 1)
            {
                return centroids.First().Value.Mean;
            }

            double index = quantile * count;
            if (index < 1)
            {
                return Min;
            }

            if (index > Count - 1)
            {
                return Max;
            }

            Centroid currentNode = centroids.First().Value;
            Centroid lastNode = centroids.Last().Value;
            double currentWeight = currentNode.Count;
            if (Math.Abs(currentWeight - 2) < Tolerance && index <= 2)
            {
                // first node is a double weight with one sample at min, sou we can infer location of other sample
                return 2 * currentNode.Mean - Min;
            }

            if (Math.Abs(centroids.Last().Value.Count - 2) < Tolerance && index > Count - 2)
            {
                // likewise for last centroid
                return 2 * lastNode.Mean - Max;
            }

            double weightSoFar = currentWeight / 2.0;

            if (index < weightSoFar)
            {
                return WeightedAvg(Min, weightSoFar - index, currentNode.Mean, index - 1);
            }

            foreach (Centroid nextNode in centroids.Values.Skip(1))
            {
                double nextWeight = nextNode.Count;
                double dw = (currentWeight + nextWeight) / 2.0;

                if (index < weightSoFar + dw)
                {
                    double leftExclusion = 0;
                    double rightExclusion = 0;
                    if (Math.Abs(currentWeight - 1) < Tolerance)
                    {
                        if (index < weightSoFar + 0.5)
                        {
                            return currentNode.Mean;
                        }
                        leftExclusion = 0.5;
                    }

                    if (Math.Abs(nextWeight - 1) < Tolerance)
                    {
                        if (index >= weightSoFar + dw - 0.5)
                        {
                            return nextNode.Mean;
                        }
                        rightExclusion = 0.5;
                    }

                    // centroids i and i+1 bracket our current point
                    // we interpolate, but the weights are diminished if singletons are present
                    double weight1 = index - weightSoFar - leftExclusion;
                    double weight2 = weightSoFar + dw - index - rightExclusion;
                    return WeightedAvg(currentNode.Mean, weight2, nextNode.Mean, weight1);
                }

                weightSoFar += dw;
                currentNode = nextNode;
                currentWeight = nextWeight;
            }

            double w1 = index - weightSoFar;
            double w2 = Count - 1 - index;
            return WeightedAvg(currentNode.Mean, w2, Max, w1);
        }

        private const double Tolerance = 0.001;

        private static double WeightedAvg(double m1, double w1, double m2, double w2)
        {
            return m1 * w1 / (w1 + w2) + m2 * w2 / (w1 + w2);
        }

        /// <summary>
        /// Gets the Distribution of the data added thus far
        /// </summary>
        /// <returns>An array of objects that contain a value (x-axis) and a count (y-axis)
        /// which can be used to plot a distribution of the data set</returns>
        public DistributionPoint[] GetDistribution()
        {
            return centroids.Values
                .Select(c => new DistributionPoint(c.Mean, c.Count))
                .ToArray();
        }

        private void Compress()
        {
            TDigest newTDigest = new TDigest(Accuracy, CompressionConstant);
            List<Centroid> temp = centroids.Values.ToList();
            temp.Shuffle();

            foreach (Centroid centroid in temp)
            {
                newTDigest.Add(centroid.Mean, centroid.Count);
            }

            centroids = newTDigest.centroids;
        }


        private double ComputeCentroidQuantile(Centroid centroid)
        {
            double sum = 0;

            foreach (Centroid c in centroids.Values)
            {
                if (c.Mean > centroid.Mean) break;
                sum += c.Count;
            }

            double denominator = count;
            return (centroid.Count / 2 + sum) / denominator;
        }

        private IEnumerable<Centroid> GetClosestCentroids(double x)
        {
            C5.KeyValuePair<double, Centroid> successor;
            C5.KeyValuePair<double, Centroid> predecessor;

            if (!centroids.TryWeakSuccessor(x, out successor))
            {
                yield return centroids.Predecessor(x).Value;
                yield break;
            }

            if (Math.Abs(successor.Value.Mean - x) < Tolerance || !centroids.TryPredecessor(x, out predecessor))
            {
                yield return successor.Value;
                yield break;
            }

            double sDiff = Math.Abs(successor.Value.Mean - x);
            double pDiff = Math.Abs(successor.Value.Mean - x);

            if (sDiff < pDiff) yield return successor.Value;
            else if (pDiff < sDiff) yield return predecessor.Value;
            else
            {
                yield return successor.Value;
                yield return predecessor.Value;
            }
        }

        private double GetThreshold(double q)
        {
            return 4 * count * Accuracy * q * (1 - q);
        }

        private void ReInsertCentroid(double oldMean, Centroid c)
        {
            centroids.Remove(oldMean);
            centroids.Add(c.Mean, c);
        }
    }

    public class DistributionPoint
    {
        public double Value { get; }
        public double Count { get; }

        public DistributionPoint(double value, double count)
        {
            Value = value;
            Count = count;
        }
    }

    internal class Centroid
    {
        public double Mean { get; private set; }
        public double Count { get; private set; }
        private const double Tolerance = 0.0001;

        public Centroid(double mean, double count)
        {
            Mean = mean;
            Count = count;
        }

        public bool Update(double deltaW, double value, out double oldMean)
        {
            oldMean = Mean;
            Count += deltaW;
            Mean += deltaW * (value - Mean) / Count;

            return Math.Abs(oldMean - Mean) > Tolerance;
        }

    }

    public static class ListExtensions
    {
        public static void Shuffle<T>(this System.Collections.Generic.IList<T> list)
        {
            int n = list.Count;
            var rand = new Random();
            while (n > 1)
            {
                n--;
                int k = rand.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }
}