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
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("2.0")]
    [InlineData("")]
    [InlineData("1e3")]
    [InlineData("_1")]
    [InlineData("99999999999999999999999")]
    public void Python_int_rejects(string text)
    {
        Assert.False(PythonNumber.TryParseInt(text, out _, out _));
    }
}
