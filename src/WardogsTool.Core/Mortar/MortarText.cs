using System.Globalization;
using System.Numerics;

namespace WardogsTool.Core.Mortar;

/// <summary>
/// The exact strings the Python tool shows, so the two can be compared character for character.
/// </summary>
public static class MortarText
{
    public const string EmptyDirection = "DIRECTION: ---°";
    public const string EmptyRange = "RANGE:     --- m";

    public static string Direction(MortarSolution s) => $"DIRECTION: {s.Direction:000}°";

    public static string Range(MortarSolution s) => $"RANGE:     {s.Range.ToString(CultureInfo.InvariantCulture)} m";

    public static string Exact(MortarSolution s) =>
        $"Bearing exact: {FixedPoint(s.BearingDegrees, 2)}°   Range exact: {FixedPoint(s.DistanceMeters, 2)} m";

    public static string HistoryEntry(MapPoint target, MortarSolution s) =>
        $"({General(target.X)}, {General(target.Y)})  {s.Direction:000}°  {s.Range.ToString(CultureInfo.InvariantCulture)} m";

    /// <summary>
    /// Python's <c>f"{x:.{digits}f}"</c>: the exact binary value rounded half-to-even. .NET's "F2"
    /// can round an exact tie (e.g. 0.125) the other way, so this works on the exact value.
    /// </summary>
    public static string FixedPoint(double value, int digits)
    {
        if (double.IsNaN(value)) return "nan";
        if (double.IsInfinity(value)) return value > 0 ? "inf" : "-inf";

        var negative = double.IsNegative(value);
        var bits = BitConverter.DoubleToInt64Bits(Math.Abs(value));
        var exponentBits = (int)((bits >> 52) & 0x7FF);
        var fraction = bits & 0xF_FFFF_FFFF_FFFFL;
        BigInteger mantissa = exponentBits == 0 ? fraction : fraction | (1L << 52);
        var exponent = (exponentBits == 0 ? 1 : exponentBits) - 1075; // value = mantissa * 2^exponent

        var scaled = mantissa * BigInteger.Pow(10, digits);
        BigInteger rounded;
        if (exponent >= 0)
        {
            rounded = scaled << exponent;
        }
        else
        {
            var denominator = BigInteger.One << -exponent;
            rounded = BigInteger.DivRem(scaled, denominator, out var remainder);
            var twice = remainder * 2;
            if (twice > denominator || (twice == denominator && !rounded.IsEven))
                rounded += 1;
        }

        var text = rounded.ToString(CultureInfo.InvariantCulture).PadLeft(digits + 1, '0');
        if (digits > 0)
            text = text[..^digits] + "." + text[^digits..];
        return negative ? "-" + text : text;
    }

    /// <summary>
    /// Python's <c>f"{x:g}"</c>: 6 significant digits, trailing zeros stripped, scientific notation
    /// when the exponent is below -4 or at least 6.
    /// </summary>
    public static string General(double value)
    {
        if (double.IsNaN(value)) return "nan";
        if (double.IsInfinity(value)) return value > 0 ? "inf" : "-inf";
        return value.ToString("G6", CultureInfo.InvariantCulture).Replace("E", "e");
    }
}
