using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RomanCalc.Core;

namespace RomanCalc.App;

public partial class MainWindow : Window
{
    private static readonly Brush Normal = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2));
    private static readonly Brush Invalid = new SolidColorBrush(Color.FromRgb(0xE0, 0x6C, 0x6C));

    private string _entry = "";          // numeral currently being typed
    private int? _left;                  // left operand once an operator is chosen
    private Operation? _pending;         // chosen operator
    private bool _showingResult;         // display holds a result; next numeral starts fresh

    public MainWindow()
    {
        InitializeComponent();
        Render();
    }

    private void Numeral_Click(object sender, RoutedEventArgs e) => AppendNumeral((string)((Button)sender).Tag);

    private void Op_Click(object sender, RoutedEventArgs e) =>
        ChooseOperation(Enum.Parse<Operation>((string)((Button)sender).Tag));

    private void Equals_Click(object sender, RoutedEventArgs e) => Evaluate();
    private void Clear_Click(object sender, RoutedEventArgs e) => Clear();
    private void Back_Click(object sender, RoutedEventArgs e) => Backspace();

    private void AppendNumeral(string letter)
    {
        if (_showingResult) { _entry = ""; _showingResult = false; ExpressionText.Text = ""; }
        if (_entry.Length >= 15) return; // longest valid numeral (MMMDCCCLXXXVIII) is 15 chars
        _entry += letter;
        Render();
    }

    private void ChooseOperation(Operation op)
    {
        if (_entry.Length > 0)
        {
            if (!RomanNumeral.TryParse(_entry, out var value)) { ShowError($"Invalid numeral: {_entry}"); return; }

            // Chain: "X + V −" evaluates X + V first.
            if (_left is int l && _pending is Operation p && !_showingResult)
            {
                var r = RomanCalculator.Calculate(l, p, value);
                if (!r.Ok) { ShowError(r.Error!); return; }
                value = r.Value!.Value;
            }
            _left = value;
        }
        else if (_left is null)
        {
            return; // nothing to operate on yet
        }

        _pending = op;
        _entry = "";
        _showingResult = false;
        ExpressionText.Text = $"{RomanNumeral.ToRoman(_left!.Value)} {RomanCalculator.Symbol(op)}";
        Render();
    }

    private void Evaluate()
    {
        if (_left is not int l || _pending is not Operation op || _entry.Length == 0) return;
        if (!RomanNumeral.TryParse(_entry, out var right)) { ShowError($"Invalid numeral: {_entry}"); return; }

        var result = RomanCalculator.Calculate(l, op, right);
        ExpressionText.Text = $"{RomanNumeral.ToRoman(l)} {RomanCalculator.Symbol(op)} {_entry} =";
        if (!result.Ok) { ShowError(result.Error!); return; }

        _entry = result.Roman;
        _left = null;
        _pending = null;
        _showingResult = true;
        Render(result.Remainder);
    }

    private void Clear()
    {
        _entry = "";
        _left = null;
        _pending = null;
        _showingResult = false;
        ExpressionText.Text = "";
        Render();
    }

    private void Backspace()
    {
        if (_showingResult || _entry.Length == 0) return;
        _entry = _entry[..^1];
        Render();
    }

    private void ShowError(string message)
    {
        _entry = "";
        _left = null;
        _pending = null;
        _showingResult = true;
        DisplayText.Text = "Error";
        DisplayText.Foreground = Invalid;
        DecimalText.Text = message;
    }

    private void Render(int remainder = 0)
    {
        DisplayText.Foreground = Normal;
        if (_entry.Length == 0)
        {
            DisplayText.Text = _pending is null ? "—" : "";
            DecimalText.Text = "";
            return;
        }

        if (RomanNumeral.TryParse(_entry, out var value))
        {
            DisplayText.Text = remainder > 0 ? $"{_entry} r {RomanNumeral.ToRoman(remainder)}" : _entry;
            DecimalText.Text = remainder > 0 ? $"= {value} remainder {remainder}" : $"= {value}";
        }
        else
        {
            DisplayText.Text = _entry;
            DisplayText.Foreground = Invalid;
            DecimalText.Text = "not a valid numeral";
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.M: case Key.D: case Key.C: case Key.L: case Key.X: case Key.V: case Key.I:
                AppendNumeral(e.Key.ToString()); break;
            case Key.Add: case Key.OemPlus when Keyboard.Modifiers == ModifierKeys.Shift:
                ChooseOperation(Operation.Add); break;
            case Key.Subtract: case Key.OemMinus:
                ChooseOperation(Operation.Subtract); break;
            case Key.Multiply:
                ChooseOperation(Operation.Multiply); break;
            case Key.Divide: case Key.OemQuestion:
                ChooseOperation(Operation.Divide); break;
            case Key.Enter: case Key.OemPlus:
                Evaluate(); break;
            case Key.Back:
                Backspace(); break;
            case Key.Escape: case Key.Delete:
                Clear(); break;
            default:
                return;
        }
        e.Handled = true;
    }
}
