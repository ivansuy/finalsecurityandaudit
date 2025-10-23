namespace AutoInventoryBackend.Services.AnomalyDetection
{
    public sealed class IsolationForestModel
    {
        private readonly IsolationTree[] _trees;
        private readonly double _averagePathConstant;

        internal IsolationForestModel(IsolationTree[] trees, int sampleSize)
        {
            _trees = trees;
            _averagePathConstant = IsolationForestMath.AveragePathLength(sampleSize);
        }

        public double Score(double[] features)
        {
            if (_trees.Length == 0 || _averagePathConstant <= 0)
            {
                return 0;
            }

            var sum = 0.0;
            foreach (var tree in _trees)
            {
                sum += tree.PathLength(features);
            }

            var averagePathLength = sum / _trees.Length;
            var score = Math.Pow(2, -averagePathLength / _averagePathConstant);
            if (double.IsNaN(score) || double.IsInfinity(score))
            {
                return 0;
            }

            return score;
        }
    }

    public static class IsolationForestTrainer
    {
        public static IsolationForestModel? Train(IReadOnlyList<double[]> dataset, int treeCount, int sampleSize, int? seed = null)
        {
            if (dataset.Count < 2 || treeCount <= 0 || sampleSize <= 0)
            {
                return null;
            }

            var dimension = dataset[0].Length;
            if (dimension == 0)
            {
                return null;
            }

            var cleaned = dataset
                .Where(v => v.Length == dimension && v.All(double.IsFinite))
                .ToList();

            if (cleaned.Count < 2)
            {
                return null;
            }

            sampleSize = Math.Min(sampleSize, cleaned.Count);
            if (sampleSize <= 0)
            {
                return null;
            }

            var random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
            var trees = new IsolationTree[treeCount];

            Parallel.For(0, treeCount, i =>
            {
                var localRandom = seed.HasValue ? new Random(seed.Value + i) : new Random(Guid.NewGuid().GetHashCode());
                var sample = DrawSample(cleaned, sampleSize, localRandom);
                var maxDepth = sample.Count <= 1 ? 0 : (int)Math.Ceiling(Math.Log2(sample.Count));
                var root = BuildTree(sample, 0, maxDepth, dimension, localRandom);
                trees[i] = new IsolationTree(root, maxDepth);
            });

            return new IsolationForestModel(trees, sampleSize);
        }

        private static List<double[]> DrawSample(List<double[]> dataset, int sampleSize, Random random)
        {
            if (sampleSize >= dataset.Count)
            {
                return new List<double[]>(dataset);
            }

            var indices = new HashSet<int>();
            while (indices.Count < sampleSize)
            {
                indices.Add(random.Next(dataset.Count));
            }

            return indices.Select(i => dataset[i]).ToList();
        }

        private static IsolationTreeNode BuildTree(List<double[]> samples, int currentDepth, int maxDepth, int dimension, Random random)
        {
            var size = samples.Count;
            if (size <= 1 || currentDepth >= maxDepth)
            {
                return new IsolationTreeNode(size);
            }

            var feature = random.Next(dimension);
            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            foreach (var sample in samples)
            {
                var value = sample[feature];
                if (value < min) min = value;
                if (value > max) max = value;
            }

            if (!double.IsFinite(min) || !double.IsFinite(max) || Math.Abs(max - min) < 1e-9)
            {
                return new IsolationTreeNode(size);
            }

            var split = random.NextDouble() * (max - min) + min;
            var left = new List<double[]>(size);
            var right = new List<double[]>(size);
            foreach (var sample in samples)
            {
                if (sample[feature] < split)
                {
                    left.Add(sample);
                }
                else
                {
                    right.Add(sample);
                }
            }

            if (left.Count == 0 || right.Count == 0)
            {
                return new IsolationTreeNode(size);
            }

            var leftNode = BuildTree(left, currentDepth + 1, maxDepth, dimension, random);
            var rightNode = BuildTree(right, currentDepth + 1, maxDepth, dimension, random);
            return new IsolationTreeNode(size, feature, split, leftNode, rightNode);
        }
    }

    internal sealed class IsolationTree
    {
        private readonly IsolationTreeNode _root;
        private readonly int _maxDepth;

        public IsolationTree(IsolationTreeNode root, int maxDepth)
        {
            _root = root;
            _maxDepth = maxDepth;
        }

        public double PathLength(double[] sample) => _root.PathLength(sample, 0, _maxDepth);
    }

    internal sealed class IsolationTreeNode
    {
        public int Size { get; }
        public int? Feature { get; }
        public double SplitValue { get; }
        public IsolationTreeNode? Left { get; }
        public IsolationTreeNode? Right { get; }

        public IsolationTreeNode(int size)
        {
            Size = size;
        }

        public IsolationTreeNode(int size, int feature, double splitValue, IsolationTreeNode left, IsolationTreeNode right)
        {
            Size = size;
            Feature = feature;
            SplitValue = splitValue;
            Left = left;
            Right = right;
        }

        public double PathLength(double[] sample, int depth, int maxDepth)
        {
            if (Left == null || Right == null || depth >= maxDepth)
            {
                return depth + IsolationForestMath.AveragePathLength(Size);
            }

            if (!Feature.HasValue)
            {
                return depth;
            }

            var nextDepth = depth + 1;
            if (sample[Feature.Value] < SplitValue)
            {
                return Left.PathLength(sample, nextDepth, maxDepth);
            }

            return Right.PathLength(sample, nextDepth, maxDepth);
        }
    }

    internal static class IsolationForestMath
    {
        private const double EulerGamma = 0.5772156649;

        public static double AveragePathLength(int n)
        {
            if (n <= 1)
            {
                return 0;
            }

            return 2.0 * (Math.Log(n - 1) + EulerGamma) - (2.0 * (n - 1) / n);
        }
    }
}
