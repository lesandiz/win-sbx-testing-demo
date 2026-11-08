using RomanCalc.Core;

namespace RomanCalc.Core.Tests;

public class RomanNumeralTests
{
    [Theory]
    [InlineData(1, "I")]
    [InlineData(4, "IV")]
    [InlineData(9, "IX")]
    [InlineData(14, "XIV")]
    [InlineData(40, "XL")]
    [InlineData(90, "XC")]
    [InlineData(400, "CD")]
    [InlineData(1994, "MCMXCIV")]
    [InlineData(2026, "MMXXVI")]
    [InlineData(3888, "MMMDCCCLXXXVIII")]
    [InlineData(3999, "MMMCMXCIX")]
    public void ToRoman_and_Parse_round_trip(int value, string roman)
    {
        Assert.Equal(roman, RomanNumeral.ToRoman(value));
        Assert.Equal(value, RomanNumeral.Parse(roman));
        Assert.Equal(value, RomanNumeral.Parse(roman.ToLowerInvariant()));
    }

    [Fact]
    public void Every_value_in_range_round_trips()
    {
        for (var i = RomanNumeral.Min; i <= RomanNumeral.Max; i++)
            Assert.Equal(i, RomanNumeral.Parse(RomanNumeral.ToRoman(i)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4000)]
    public void ToRoman_rejects_out_of_range(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RomanNumeral.ToRoman(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("IIII")]
    [InlineData("VV")]
    [InlineData("IC")]
    [InlineData("XM")]
    [InlineData("IIV")]
    [InlineData("MMMM")]
    [InlineData("ABC")]
    [InlineData("X1")]
    public void TryParse_rejects_invalid(string? text) =>
        Assert.False(RomanNumeral.TryParse(text, out _));
}

public class RomanCalculatorTests
{
    [Theory]
    [InlineData("XII", Operation.Add, "VIII", "XX")]
    [InlineData("MCM", Operation.Add, "XCIX", "MCMXCIX")]
    [InlineData("L", Operation.Subtract, "IX", "XLI")]
    [InlineData("XII", Operation.Multiply, "XII", "CXLIV")]
    [InlineData("C", Operation.Divide, "IV", "XXV")]
    public void Calculates_basic_operations(string a, Operation op, string b, string expected)
    {
        var r = RomanCalculator.Calculate(a, op, b);
        Assert.True(r.Ok, r.Error);
        Assert.Equal(expected, r.Roman);
        Assert.Equal(0, r.Remainder);
    }

    [Fact]
    public void Division_reports_remainder()
    {
        var r = RomanCalculator.Calculate("X", Operation.Divide, "III");
        Assert.Equal("III", r.Roman);
        Assert.Equal(1, r.Remainder);
        Assert.Equal("III r I", r.Display);
    }

    [Fact]
    public void Division_below_one_is_an_error()
    {
        var r = RomanCalculator.Calculate("II", Operation.Divide, "V");
        Assert.False(r.Ok);
    }

    [Theory]
    [InlineData("V", Operation.Subtract, "V")]   // zero
    [InlineData("V", Operation.Subtract, "X")]   // negative
    [InlineData("MM", Operation.Add, "MM")]      // 4000
    [InlineData("C", Operation.Multiply, "C")]   // 10000
    public void Out_of_range_results_are_errors(string a, Operation op, string b)
    {
        var r = RomanCalculator.Calculate(a, op, b);
        Assert.False(r.Ok);
        Assert.Null(r.Value);
    }

    [Fact]
    public void Invalid_operand_is_an_error()
    {
        var r = RomanCalculator.Calculate("IIII", Operation.Add, "I");
        Assert.False(r.Ok);
        Assert.Contains("IIII", r.Error);
    }

    [Fact]
    public void Divide_by_zero_int_overload_is_an_error() =>
        Assert.False(RomanCalculator.Calculate(5, Operation.Divide, 0).Ok);
}
