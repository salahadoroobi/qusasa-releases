using System.Windows;
using System.Windows.Media;

namespace Qusasa;

internal static class GlassTheme
{
    internal static void Apply(ResourceDictionary theme)
    {
        Color frame = ColorOf(theme, "Frame");
        Color accent = ColorOf(theme, "Accent");
        theme["Frame"] = Gradient(Mix(frame, accent, .17), frame, Mix(frame, accent, .08));

        Color panel = ColorOf(theme, "Panel");
        Color elevated = ColorOf(theme, "Elevated");
        theme["Panel"] = Gradient(Mix(panel, accent, .14, 232), WithAlpha(panel, 218), Mix(panel, accent, .05, 238));
        theme["Elevated"] = Gradient(Mix(elevated, accent, .19, 248), WithAlpha(elevated, 234), WithAlpha(elevated, 245));
        theme["Input"] = new SolidColorBrush(WithAlpha(ColorOf(theme, "Input"), 241));
        theme["Line"] = new SolidColorBrush(Mix(ColorOf(theme, "Line"), accent, .22));
    }

    private static Color ColorOf(ResourceDictionary theme, string key) => ((SolidColorBrush)theme[key]).Color;
    private static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);
    private static Color Mix(Color a, Color b, double amount, byte alpha = 255) =>
        Color.FromArgb(alpha, Blend(a.R, b.R, amount), Blend(a.G, b.G, amount), Blend(a.B, b.B, amount));
    private static byte Blend(byte a, byte b, double amount) => (byte)(a + (b - a) * amount);

    private static Brush Gradient(Color a, Color b, Color c) => new LinearGradientBrush(
        new GradientStopCollection { new GradientStop(a, 0), new GradientStop(b, .46), new GradientStop(c, 1) },
        new Point(0, 0), new Point(1, 1));
}
