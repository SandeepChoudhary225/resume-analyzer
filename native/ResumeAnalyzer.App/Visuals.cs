using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace ResumeAnalyzer.App;
internal static class Visuals
{
    public static Brush Brush(string key) => (Brush)System.Windows.Application.Current.FindResource(key);
    public static SolidColorBrush Hex(string value) => new((Color)ColorConverter.ConvertFromString(value));
    public static TextBlock Text(string text, double size = 14, string color = "Ink", FontWeight? weight = null)
        => new() { Text = text, FontSize = size, Foreground = Brush(color), FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 6), LineHeight = size * 1.45 };
    public static StackPanel Stack(params UIElement[] children)
    {
        var panel = new StackPanel(); foreach (var child in children) panel.Children.Add(child); return panel;
    }
    public static WrapPanel Row(params UIElement[] children)
    {
        var panel = new WrapPanel { Margin = new(0, 4, 0, 8) }; foreach (var child in children) panel.Children.Add(child); return panel;
    }
    public static Button Button(string label, string id, Action action, bool primary = false)
    {
        var button = new Button { Content = label, Style = (Style)System.Windows.Application.Current.FindResource(primary ? "PrimaryButton" : typeof(Button)), HorizontalContentAlignment = HorizontalAlignment.Center, ToolTip = label };
        AutomationProperties.SetAutomationId(button, id); AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action(); return button;
    }
    public static Border Card(UIElement content, string background = "Surface", Thickness? padding = null)
        => new() { Child = content, Background = Brush(background), BorderBrush = Brush("Line"), BorderThickness = new(1), CornerRadius = new(16), Padding = padding ?? new Thickness(22), Margin = new(0, 0, 0, 16) };
    public static Border Chip(string text, bool warn = false)
        => new() { Child = Text(text, 12, warn ? "Warn" : "AccentText", FontWeights.SemiBold), Background = Brush(warn ? "WarnSoft" : "AccentSoft"), CornerRadius = new(6), Padding = new(9, 5, 9, 0), Margin = new(0, 0, 7, 7) };
    public static Grid Columns(UIElement left, UIElement right, double leftWidth = 1, double rightWidth = 1)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new(leftWidth, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = new(18) }); grid.ColumnDefinitions.Add(new() { Width = new(rightWidth, GridUnitType.Star) }); grid.Children.Add(left); grid.Children.Add(right); Grid.SetColumn(right, 2); return grid;
    }
    public static TextBox Input(string value, string name, double height = 200, bool readOnly = false)
    {
        var input = new TextBox { Text = value, AcceptsReturn = true, AcceptsTab = false, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = height, IsReadOnly = readOnly, MaxLength = 200_000, Margin = new(0, 4, 0, 10) };
        AutomationProperties.SetName(input, name); AutomationProperties.SetAutomationId(input, name); return input;
    }
    public static Grid Score(int? score)
    {
        var grid = new Grid { Width = 160, Height = 160, Margin = new(0, 8, 24, 8) };
         grid.Children.Add(new Ellipse { Width = 146, Height = 146, StrokeThickness = 9, Stroke = Hex("#4A4F59") });
        if (score is > 0)
        {
            var degrees = Math.Min(score.Value, 99.99) * 3.6;
            var end = new Point(80 + 73 * Math.Sin(degrees * Math.PI / 180), 80 - 73 * Math.Cos(degrees * Math.PI / 180));
            var figure = new PathFigure { StartPoint = new(80, 7), IsClosed = false };
            figure.Segments.Add(new ArcSegment(end, new Size(73, 73), 0, degrees > 180, SweepDirection.Clockwise, true));
             grid.Children.Add(new System.Windows.Shapes.Path { Data = new PathGeometry([figure]), Stroke = Hex("#C2C6CC"), StrokeThickness = 9, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        }
        var number = new TextBlock { Text = score?.ToString() ?? "—", FontSize = 46, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, -14, 0, 0) };
         var caption = new TextBlock { Text = score.HasValue ? "out of 100" : "not analyzed", FontSize = 12, Foreground = Hex("#B4B8C0"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 54, 0, 0) };
        grid.Children.Add(number); grid.Children.Add(caption); AutomationProperties.SetName(grid, score.HasValue ? $"Estimated compatibility {score} out of 100" : "Not analyzed"); return grid;
    }
    public static void Theme(string selection)
    {
        bool dark = selection == "Dark";
        if (selection == "System")
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            dark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        string[] names = ["Canvas", "Surface", "Soft", "Ink", "Muted", "Line", "Accent", "AccentText", "AccentSoft", "Warn", "WarnSoft", "Danger"];
         string[] light = ["#F6F6F7", "#FFFFFF", "#F0F1F3", "#202329", "#68707A", "#DDE0E4", "#1B1E24", "#0B0D10", "#EDF0F3", "#8E5610", "#FFF0D8", "#AC3939"];
         string[] night = ["#15171B", "#1F2228", "#272A31", "#F0F1F3", "#A5A9B1", "#4A4F58", "#1B1E24", "#C2C6CC", "#30333A", "#F3C47C", "#42341F", "#FFB0A8"];
        for (int i = 0; i < names.Length; i++) System.Windows.Application.Current.Resources[names[i]] = Hex((dark ? night : light)[i]);
        if (SystemParameters.HighContrast)
        {
            foreach (var name in new[] { "Canvas", "Surface", "Soft", "AccentSoft", "WarnSoft" }) System.Windows.Application.Current.Resources[name] = SystemColors.WindowBrush;
            foreach (var name in new[] { "Ink", "Muted", "AccentText", "Warn", "Danger" }) System.Windows.Application.Current.Resources[name] = SystemColors.WindowTextBrush;
        }
    }
}
