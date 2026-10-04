using WardogsTool.Core.Parsing;

namespace WardogsTool.Core.Tests;

public class ParserTests
{
    // ---------- WARDOGS map format: x134.98, y65.56 ----------

    [Theory]
    [InlineData("x134.98, y65.56")]          // as the map shows it
    [InlineData("X134.98, Y65.56")]
    [InlineData("x=134.98, y=65.56")]
    [InlineData("x134.98 y65.56")]
    [InlineData("X = 134.98 Y = 65.56")]
    [InlineData("  x 134.98 ,  y 65.56  ")]
    [InlineData("x134.98,y65.56")]
    [InlineData("y65.56, x134.98")]          // labels decide, not order
    [InlineData("Y=65.56 X=134.98")]
    [InlineData("134.98 65.56")]             // unlabeled still works
    [InlineData("134.98, 65.56")]
    public void Map_format_and_existing_formats(string text)
    {
        Assert.True(CoordinateParser.TryParse(text, out var p, out var error), error);
        Assert.Equal(134.98, p.X);
        Assert.Equal(65.56, p.Y);
    }

    [Theory]
    [InlineData("x-12.5, y-0.25", -12.5, -0.25)]
    [InlineData("y = -3, x = +7", 7, -3)]
    [InlineData("x0, y200", 0, 200)]
    [InlineData("x1e2, y.5", 100, 0.5)]
    public void Labeled_negative_and_decimal_values(string text, double x, double y)
    {
        Assert.True(CoordinateParser.TryParse(text, out var p, out var error), error);
        Assert.Equal(x, p.X);
        Assert.Equal(y, p.Y);
    }

    [Theory]
    [InlineData("x134.98", CoordinateParser.LabeledFormatMessage)]               // no y
    [InlineData("y65.56", CoordinateParser.LabeledFormatMessage)]
    [InlineData("x134.98, x65.56", CoordinateParser.DuplicateLabelMessage)]      // two x
    [InlineData("Y1, y2", CoordinateParser.DuplicateLabelMessage)]
    [InlineData("xabc, y65.56", "could not convert string to float: 'abc'")]
    [InlineData("x134.98, yabc", "could not convert string to float: 'abc'")]
    [InlineData("x134.98 65.56", CoordinateParser.LabeledFormatMessage)]         // one label, one bare number
    [InlineData("134.98 y65.56", CoordinateParser.LabeledFormatMessage)]
    [InlineData("x134.98y65.56", CoordinateParser.LabeledFormatMessage)]         // no separator
    [InlineData("x, y", CoordinateParser.LabeledFormatMessage)]                  // labels without numbers
    [InlineData("x=, y=1", CoordinateParser.LabeledFormatMessage)]
    [InlineData("x1, y2, z3", CoordinateParser.LabeledFormatMessage)]
    [InlineData("x1，y2", CoordinateParser.LabeledFormatMessage)]                // full-width comma is not a separator
    [InlineData("x1 == 2, y3", CoordinateParser.LabeledFormatMessage)]
    public void Malformed_or_ambiguous_labeled_input_is_rejected(string text, string message)
    {
        Assert.False(CoordinateParser.TryParse(text, out _, out var error));
        Assert.Equal(message, error);
    }

    [Theory]
    [InlineData("0x10 1", false)]      // hex-looking token does not start with a label
    [InlineData("Infinity 1", false)]
    [InlineData("1e5 2", false)]
    [InlineData("x1 y2", true)]
    [InlineData("Y = 1 X = 2", true)]
    public void Labeled_mode_only_when_a_token_starts_with_x_or_y(string text, bool labeled)
    {
        Assert.Equal(labeled, CoordinateParser.IsLabeled(text));
    }

    [Fact]
    public void Labeled_values_keep_python_number_rules()
    {
        // Same overflow / negative-zero handling as unlabeled input.
        Assert.True(CoordinateParser.TryParse("x-0, y1e400", out var p, out _));
        Assert.True(DoublePolyfills.IsNegative(p.X));
        Assert.True(double.IsPositiveInfinity(p.Y));
    }

    // ---------- unlabeled (wardogs_tool.py parse_xy) ----------

    [Theory]
    [InlineData("104.39 63.59")]
    [InlineData("104.39, 63.59")]
    [InlineData("104.39,63.59")]
    [InlineData("  104.39\t 63.59  ")]
    public void Space_and_comma_separated_coordinates(string text)
    {
        Assert.True(CoordinateParser.TryParse(text, out var p, out _));
        Assert.Equal(104.39, p.X);
        Assert.Equal(63.59, p.Y);
    }

    [Theory]
    [InlineData("")]
    [InlineData("104.39")]
    [InlineData("1 2 3")]
    [InlineData("104,39 63,59")]     // decimal commas become four numbers
    [InlineData("104.39，63.59")]    // full-width comma is not a separator (same as Python)
    public void Wrong_number_of_values(string text)
    {
        Assert.False(CoordinateParser.TryParse(text, out _, out var error));
        Assert.Equal(CoordinateParser.WrongCountMessage, error);
    }

    [Theory]
    [InlineData("abc 1", "could not convert string to float: 'abc'")]
    [InlineData("1 2abc", "could not convert string to float: '2abc'")]
    [InlineData("1.2.3 4", "could not convert string to float: '1.2.3'")]
    [InlineData("0x10 1", "could not convert string to float: '0x10'")]
    [InlineData("1__0 2", "could not convert string to float: '1__0'")]
    [InlineData("- 1", "could not convert string to float: '-'")]
    public void Malformed_numbers(string text, string message)
    {
        Assert.False(CoordinateParser.TryParse(text, out _, out var error));
        Assert.Equal(message, error);
    }

    [Fact]
    public void Full_width_digits_parse_like_python()
    {
        Assert.True(CoordinateParser.TryParse("１０４.３９ ６３.５９", out var p, out _));
        Assert.Equal(104.39, p.X);
        Assert.Equal(63.59, p.Y);
    }

    [Fact]
    public void Parsing_ignores_the_current_culture()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.True(CoordinateParser.TryParse("104.39 63.59", out var p, out _));
            Assert.Equal(104.39, p.X);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData(" +2 ", 2)]
    [InlineData("5_0_0", 500)]
    [InlineData("007", 7)]
    [InlineData("-1", -1)]
    public void Python_int_accepts(string text, long expected)
    {
        Assert.True(PythonNumber.TryParseInt(text, out var v, out _));
        Assert.Equal(new System.Numerics.BigInteger(expected), v);
    }

    [Fact]
    public void Python_int_is_unbounded()
    {
        Assert.True(PythonNumber.TryParseInt("99999999999999999999999", out var v, out _));
        Assert.Equal(System.Numerics.BigInteger.Parse("99999999999999999999999"), v);
    }

    [Theory]
    [InlineData("2.0")]
    [InlineData("")]
    [InlineData("1e3")]
    [InlineData("_1")]
    public void Python_int_rejects(string text)
    {
        Assert.False(PythonNumber.TryParseInt(text, out _, out _));
    }

    [Theory]
    [InlineData("abc", "'abc'")]
    [InlineData("1\\2", "'1\\\\2'")]
    [InlineData("it's", "\"it's\"")]
    [InlineData("'\"x", "'\\'\"x'")]
    [InlineData("a\tb", "'a\\tb'")]
    [InlineData("104.39\u200b", "'104.39\\u200b'")]
    [InlineData("1\u007f", "'1\\x7f'")]
    [InlineData("１０４．３９", "'１０４．３９'")]
    [InlineData("a\u00a0b", "'a\\xa0b'")]
    public void Python_repr(string text, string expected)
    {
        Assert.Equal(expected, PythonNumber.PythonRepr(text));
    }

    [Fact]
    public void Astral_unicode_digits_parse_like_python()
    {
        Assert.True(CoordinateParser.TryParse("\U0001D7CF\U0001D7CE 5", out var p, out _));
        Assert.Equal(10, p.X);
        Assert.Equal(5, p.Y);
    }

    [Fact]
    public void Int_error_repr_is_cut_at_200_characters_like_python()
    {
        Assert.False(PythonNumber.TryParseInt(new string('x', 250), out _, out var error));
        Assert.Equal("invalid literal for int() with base 10: '" + new string('x', 199), error);
    }
}
