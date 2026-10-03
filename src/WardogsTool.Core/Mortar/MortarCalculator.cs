namespace WardogsTool.Core.Mortar;

/// <summary>A map coordinate. +X is east, +Y is north, 1.00 unit = 100 m.</summary>
public readonly record struct MapPoint(double X, double Y);

/// <summary>
/// Exact bearing/distance plus the recommended firing values (whole degrees, whole metres).
/// </summary>
public readonly record struct MortarSolution(double BearingDegrees, double DistanceMeters, int Direction, long Range);

/// <summary>
/// Port of <c>mortar_solution</c> / <c>firing_solution</c> from wardogs_tool.py.
/// </summary>
public static class MortarCalculator
{
    public const double MetersPerUnit = 100;
    public const string NonFiniteMessage = "坐标必须是有限数字（不支持 inf / nan）";

    /// <summary>
    /// Bearing is a compass bearing (north 0°, east 90°), so atan2 takes (east, north) — the
    /// reverse of the usual (y, x).
    /// </summary>
    public static (double BearingDegrees, double DistanceMeters) Exact(MapPoint mortar, MapPoint target)
    {
        var dx = target.X - mortar.X;
        var dy = target.Y - mortar.Y;
        var distance = double.Hypot(dx, dy) * MetersPerUnit;
        // math.degrees(x) in CPython is x * (180 / pi).
        var bearing = PythonFloatMod(Math.Atan2(dx, dy) * (180.0 / Math.PI), 360);
        return (bearing, distance);
    }

    /// <summary>
    /// Firing values, rounded half-up with floor(v + 0.5) (not banker's rounding). 359.5° rounds to
    /// 360 and wraps to 000.
    /// </summary>
    /// <returns>false with a message when an input or result is not finite.</returns>
    public static bool TrySolve(MapPoint mortar, MapPoint target, out MortarSolution solution, out string error)
    {
        solution = default;
        error = NonFiniteMessage;
        if (!double.IsFinite(mortar.X) || !double.IsFinite(mortar.Y) || !double.IsFinite(target.X) || !double.IsFinite(target.Y))
            return false;

        var (bearing, distance) = Exact(mortar, target);
        if (!double.IsFinite(bearing) || !double.IsFinite(distance))
            return false;
        var range = Math.Floor(distance + 0.5);
        if (range >= long.MaxValue)
            return false;

        solution = new MortarSolution(bearing, distance, RoundDirection(bearing), (long)range);
        error = "";
        return true;
    }

    /// <summary>
    /// Python: <c>int(math.floor(bearing + 0.5)) % 360</c>. Half-up, so 0.5 → 1 and 2.5 → 3
    /// (banker's rounding would give 0 and 2), and 359.5 → 360 → 0.
    /// </summary>
    public static int RoundDirection(double bearingDegrees) => (int)(Math.Floor(bearingDegrees + 0.5) % 360);

    /// <summary>
    /// CPython's float modulo: the result takes the sign of the divisor, so -45 % 360 == 315 (C#'s %
    /// would give -45), -0.0 % 360 == +0.0, and a tiny negative value can round up to exactly 360.0.
    /// </summary>
    public static double PythonFloatMod(double x, double m)
    {
        var mod = x % m; // C#'s % on doubles is C fmod, the same starting point CPython uses
        if (mod != 0)
        {
            if ((m < 0) != (mod < 0))
                mod += m;
        }
        else
        {
            mod = Math.CopySign(0.0, m);
        }
        return mod;
    }
}
