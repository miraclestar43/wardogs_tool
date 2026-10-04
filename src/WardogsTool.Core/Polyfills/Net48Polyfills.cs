#if NETFRAMEWORK
// Types and members that .NET Framework 4.8 lacks, so the shared sources compile unchanged.
namespace System.Runtime.CompilerServices
{
    // Compiler-only: lets records / init accessors compile.
    internal static class IsExternalInit { }
}

namespace System
{
    // Compiler-only: lets s[..n] / s[^n..] compile (lowered to Length + Substring).
    internal readonly struct Index
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false) => _value = fromEnd ? ~value : value;

        public bool IsFromEnd => _value < 0;
        public int Value => _value < 0 ? ~_value : _value;

        public static implicit operator Index(int value) => new(value);

        public int GetOffset(int length) => IsFromEnd ? length - Value : Value;

        public static Index Start => new(0);
        public static Index End => new(0, fromEnd: true);
    }

    internal readonly struct Range
    {
        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public Index Start { get; }
        public Index End { get; }

        public static Range StartAt(Index start) => new(start, Index.End);
        public static Range EndAt(Index end) => new(Index.Start, end);
        public static Range All => new(Index.Start, Index.End);
    }

    internal static class StringPolyfills
    {
        public static bool Contains(this string s, char c) => s.IndexOf(c) >= 0;
        public static bool StartsWith(this string s, char c) => s.Length > 0 && s[0] == c;
    }

    internal static class DoublePolyfills
    {
        public static bool IsFinite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);

        public static bool IsNegative(double d) => BitConverter.DoubleToInt64Bits(d) < 0;

        public static double CopySign(double x, double y) =>
            IsNegative(x) == IsNegative(y) ? x : -x;

        /// <summary>
        /// sqrt(x² + y²) without intermediate overflow/underflow (scaled by the larger magnitude).
        /// </summary>
        public static double Hypot(double x, double y)
        {
            x = Math.Abs(x);
            y = Math.Abs(y);
            if (double.IsInfinity(x) || double.IsInfinity(y)) return double.PositiveInfinity;
            if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
            var big = Math.Max(x, y);
            var small = Math.Min(x, y);
            if (big == 0) return 0;
            if (big < 1e150 && big > 1e-150)
                return Math.Sqrt(x * x + y * y);
            var r = small / big;
            return big * Math.Sqrt(1 + r * r);
        }
    }
}

namespace System.Linq
{
    internal static class EnumerablePolyfills
    {
        public static TSource MinBy<TSource, TKey>(this Collections.Generic.IEnumerable<TSource> source, Func<TSource, TKey> key)
        {
            var comparer = Collections.Generic.Comparer<TKey>.Default;
            using var e = source.GetEnumerator();
            if (!e.MoveNext()) throw new InvalidOperationException("Sequence contains no elements");
            var best = e.Current;
            var bestKey = key(best);
            while (e.MoveNext())
            {
                var k = key(e.Current);
                if (comparer.Compare(k, bestKey) < 0)
                {
                    best = e.Current;
                    bestKey = k;
                }
            }
            return best;
        }
    }
}
#endif
