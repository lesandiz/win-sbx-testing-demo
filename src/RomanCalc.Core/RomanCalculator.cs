namespace RomanCalc.Core;

public enum Operation { Add, Subtract, Multiply, Divide }

/// <summary>Result of a calculation: either a Roman value (with optional remainder) or an error message.</summary>
public sealed record CalcResult(int? Value, int Remainder, string? Error)
{
    public bool Ok => Error is null;
    public string Roman => Value is int v ? RomanNumeral.ToRoman(v) : string.Empty;

    /// <summary>Human-readable display text, e.g. "XII", "III r II", or the error.</summary>
    public string Display => !Ok ? Error! : Remainder > 0 ? $"{Roman} r {RomanNumeral.ToRoman(Remainder)}" : Roman;

    public static CalcResult Success(int value, int remainder = 0) => new(value, remainder, null);
    public static CalcResult Fail(string error) => new(null, 0, error);
}

public static class RomanCalculator
{
    public static CalcResult Calculate(string left, Operation op, string right)
    {
        if (!RomanNumeral.TryParse(left, out var a)) return CalcResult.Fail($"Invalid numeral: {left}");
        if (!RomanNumeral.TryParse(right, out var b)) return CalcResult.Fail($"Invalid numeral: {right}");
        return Calculate(a, op, b);
    }

    public static CalcResult Calculate(int a, Operation op, int b)
    {
        long result;
        int remainder = 0;
        switch (op)
        {
            case Operation.Add: result = (long)a + b; break;
            case Operation.Subtract: result = (long)a - b; break;
            case Operation.Multiply: result = (long)a * b; break;
            case Operation.Divide:
                if (b == 0) return CalcResult.Fail("Division by zero"); // unreachable for valid numerals, kept for int callers
                result = a / b;
                remainder = a % b;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(op));
        }

        if (result < RomanNumeral.Min) return CalcResult.Fail("Result below I (no zero or negatives)");
        if (result > RomanNumeral.Max) return CalcResult.Fail("Result above MMMCMXCIX (3999)");
        return CalcResult.Success((int)result, remainder);
    }

    public static string Symbol(Operation op) => op switch
    {
        Operation.Add => "+",
        Operation.Subtract => "−",
        Operation.Multiply => "×",
        Operation.Divide => "÷",
        _ => "?",
    };
}
