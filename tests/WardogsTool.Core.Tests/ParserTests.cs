using WardogsTool.Core.Parsing;

namespace WardogsTool.Core.Tests;

public class ParserTests
{
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
