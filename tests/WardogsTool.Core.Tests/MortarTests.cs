using WardogsTool.Core.Mortar;
using WardogsTool.Core.Parsing;

namespace WardogsTool.Core.Tests;

public class MortarTests
{
    private static MortarSolution Solve(double mx, double my, double tx, double ty)
    {
        Assert.True(MortarCalculator.TrySolve(new(mx, my), new(tx, ty), out var s, out var error), error);
        return s;
    }

    // The three self-tests from wardogs_tool.py (run_self_tests).
    [Theory]
    [InlineData(100.32, 59.45, 104.39, 63.59, 45, 581)]
    [InlineData(78.49, 71.84, 81.44, 70.78, 110, 313)]
    [InlineData(78.49, 71.84, 83.60, 72.96, 78, 523)]
    public void Known_in_game_cases(double mx, double my, double tx, double ty, int direction, long range)
    {
        var s = Solve(mx, my, tx, ty);
        Assert.Equal(direction, s.Direction);
        Assert.Equal(range, s.Range);
    }

    [Fact]
    public void Known_case_display_strings()
    {
        var s = Solve(100.32, 59.45, 104.39, 63.59);
        Assert.Equal("DIRECTION: 045°", MortarText.Direction(s));
        Assert.Equal("RANGE:     581 m", MortarText.Range(s));
        Assert.Equal("Bearing exact: 44.51°   Range exact: 580.56 m", MortarText.Exact(s));
        Assert.Equal("(104.39, 63.59)  045°  581 m", MortarText.HistoryEntry(new(104.39, 63.59), s));
    }

    // +X east, +Y north; compass bearing with north = 0.
    [Theory]
    [InlineData(0, 1, 0)]      // N
    [InlineData(1, 1, 45)]     // NE
    [InlineData(1, 0, 90)]     // E
    [InlineData(1, -1, 135)]   // SE
    [InlineData(0, -1, 180)]   // S
    [InlineData(-1, -1, 225)]  // SW
    [InlineData(-1, 0, 270)]   // W
    [InlineData(-1, 1, 315)]   // NW
    public void Cardinal_and_intercardinal_bearings(double dx, double dy, int expected)
    {
        foreach (var (ox, oy) in new[] { (0.0, 0.0), (100.0, 100.0), (-12.5, 37.25) })
        {
            var s = Solve(ox, oy, ox + dx * 2.5, oy + dy * 2.5);
            Assert.Equal(expected, s.Direction);
            Assert.Equal(expected, s.BearingDegrees, 9);
        }
    }

    // One target in each quadrant around the mortar (NE, SE, SW, NW), off the diagonals.
    [Theory]
    [InlineData(1, 3, 18)]     // NE: atan2(1, 3)  = 18.43°
    [InlineData(3, -1, 108)]   // SE: 90 + 18.43
    [InlineData(-1, -3, 198)]  // SW: 180 + 18.43
    [InlineData(-3, 1, 288)]   // NW: 270 + 18.43
    public void Four_quadrants(double dx, double dy, int expected)
    {
        Assert.Equal(expected, Solve(50, 50, 50 + dx, 50 + dy).Direction);
    }

    [Fact]
    public void Boundary_359_49_stays_359_and_359_50_wraps_to_000()
    {
        static MortarSolution At(double degrees)
        {
            var a = degrees * Math.PI / 180;
            return Solve(0, 0, 10 * Math.Sin(a), 10 * Math.Cos(a));
        }
        Assert.Equal(359, At(359.49).Direction);
        Assert.Equal(0, At(359.51).Direction);
        Assert.Equal(0, MortarCalculator.RoundDirection(359.50));
        Assert.Equal(359, MortarCalculator.RoundDirection(359.49));
    }

    [Fact]
    public void Scientific_notation_coordinates()
    {
        Assert.True(CoordinateParser.TryParse("1e2 5E1", out var mortar, out _));
        Assert.True(CoordinateParser.TryParse("1.0e2, 5.3e1", out var target, out _));
        Assert.True(MortarCalculator.TrySolve(mortar, target, out var s, out _));
        Assert.Equal(0, s.Direction);
        Assert.Equal(300, s.Range);
    }

    [Fact]
    public void Negative_zero_target_is_north_not_negative()
    {
        var s = Solve(0, 0, -0.0, 1);
        Assert.Equal(0, s.Direction);
        Assert.False(double.IsNegative(s.BearingDegrees));
    }

    [Fact]
    public void One_coordinate_unit_is_100_metres()
    {
        Assert.Equal(100, Solve(0, 0, 1, 0).DistanceMeters, 12);
        Assert.Equal(500, Solve(0, 0, 3, 4).DistanceMeters, 12);
        Assert.Equal(500, Solve(10, 10, 7, 6).Range);
        Assert.Equal(1, Solve(0, 0, 0, 0.01).Range);
        Assert.Equal(0, Solve(0, 0, 0, 0.004).Range);  // 0.4 m rounds down
        Assert.Equal(1, Solve(0, 0, 0, 0.005).Range);  // 0.5 m rounds up (half-up, not banker's)
    }

    [Fact]
    public void Zero_distance_is_bearing_000_range_0()
    {
        var s = Solve(50, 50, 50, 50);
        Assert.Equal(0, s.Direction);
        Assert.Equal(0, s.Range);
    }

    [Theory]
    [InlineData(359.5, 0)]         // rounds up to 360 and wraps to 000
    [InlineData(359.49999999, 359)]
    [InlineData(359.99, 0)]
    [InlineData(360.0, 0)]         // Python's % can return exactly 360.0
    [InlineData(0.0, 0)]
    [InlineData(0.5, 1)]           // half-up, banker's would give 0
    [InlineData(2.5, 3)]           // half-up, banker's would give 2
    [InlineData(44.5, 45)]
    [InlineData(44.49999, 44)]
    public void Direction_rounding_is_half_up_and_wraps_at_360(double bearing, int expected)
    {
        Assert.Equal(expected, MortarCalculator.RoundDirection(bearing));
    }

    [Fact]
    public void Bearing_just_west_of_north_wraps_to_000()
    {
        // 359.6° from the mortar: rounds to 360 → 000.
        var a = 359.6 * Math.PI / 180;
        var s = Solve(10, 20, 10 + 5 * Math.Sin(a), 20 + 5 * Math.Cos(a));
        Assert.Equal(359.6, s.BearingDegrees, 6);
        Assert.Equal(0, s.Direction);
        Assert.Equal("DIRECTION: 000°", MortarText.Direction(s));

        // 359.4° stays 359.
        a = 359.4 * Math.PI / 180;
        Assert.Equal(359, Solve(10, 20, 10 + 5 * Math.Sin(a), 20 + 5 * Math.Cos(a)).Direction);
    }

    [Theory]
    [InlineData(-45.0, 315.0)]
    [InlineData(-90.0, 270.0)]
    [InlineData(720.0, 0.0)]
    [InlineData(359.9, 359.9)]
    [InlineData(-1e-17, 360.0)]    // CPython quirk: 360 - 1e-17 rounds to exactly 360.0
    public void Python_float_modulo(double x, double expected)
    {
        Assert.Equal(expected, MortarCalculator.PythonFloatMod(x, 360));
    }

    [Fact]
    public void Python_float_modulo_turns_negative_zero_into_positive_zero()
    {
        var r = MortarCalculator.PythonFloatMod(-0.0, 360);
        Assert.Equal(0.0, r);
        Assert.False(double.IsNegative(r));
    }

    [Theory]
    [InlineData(double.NaN, 0, 1, 1)]
    [InlineData(0, double.PositiveInfinity, 1, 1)]
    [InlineData(0, 0, double.NegativeInfinity, 1)]
    [InlineData(0, 0, 1, double.NaN)]
    [InlineData(-1e308, 0, 1e308, 0)]  // finite inputs, infinite distance
    public void Non_finite_inputs_or_results_are_rejected(double mx, double my, double tx, double ty)
    {
        Assert.False(MortarCalculator.TrySolve(new(mx, my), new(tx, ty), out _, out var error));
        Assert.Equal(MortarCalculator.NonFiniteMessage, error);
    }

    [Fact]
    public void Parsed_nan_is_rejected_by_the_calculator_not_the_parser()
    {
        // Same split of responsibility as Python: parse_xy accepts "nan".
        Assert.True(CoordinateParser.TryParse("nan 1", out var target, out _));
        Assert.False(MortarCalculator.TrySolve(new(0, 0), target, out _, out _));
    }

    [Theory]
    [InlineData(0.125, "0.12")]    // exact binary tie: Python rounds half-to-even
    [InlineData(0.375, "0.38")]
    [InlineData(2.675, "2.67")]    // 2.675 is really 2.67499999...
    [InlineData(580.5639, "580.56")]
    [InlineData(360.0, "360.00")]
    [InlineData(0.0, "0.00")]
    [InlineData(123456700.0, "123456700.00")]
    [InlineData(-0.0, "-0.00")]
    public void Fixed_point_formatting_matches_python(double value, string expected)
    {
        Assert.Equal(expected, MortarText.FixedPoint(value, 2));
    }

    [Theory]
    [InlineData(104.39, "104.39")]
    [InlineData(83.6, "83.6")]
    [InlineData(0.0, "0")]
    [InlineData(1234567.0, "1.23457e+06")]
    [InlineData(1e-05, "1e-05")]
    [InlineData(0.0001, "0.0001")]
    [InlineData(-0.0, "-0")]
    public void General_formatting_matches_python(double value, string expected)
    {
        Assert.Equal(expected, MortarText.General(value));
    }
}
