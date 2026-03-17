using Godot;

namespace SpireSenseMod;

/// <summary>
/// Individual tier badge UI element for in-game overlay.
/// Displays a single letter grade (S/A/B/C/D/F) with color coding.
/// </summary>
public partial class TierBadge : Control
{
    private string _tier = "?";
    private int _score;
    private string _cardName = "";

    public void SetData(string tier, int score, string cardName)
    {
        _tier = tier;
        _score = score;
        _cardName = cardName;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = new Vector2(36, 36);
        var rect = new Rect2(Vector2.Zero, size);
        var color = GetTierColor(_tier);

        // Background rounded rect
        DrawRect(rect, color);

        // Tier letter
        var font = ThemeDB.FallbackFont;
        var fontSize = 22;
        var textSize = font.GetStringSize(_tier, HorizontalAlignment.Center, -1, fontSize);
        var textPos = new Vector2(
            (size.X - textSize.X) / 2,
            (size.Y + textSize.Y) / 2 - 2
        );
        DrawString(font, textPos, _tier, HorizontalAlignment.Left, -1, fontSize, Colors.White);
    }

    private static Color GetTierColor(string tier) => tier switch
    {
        "S" => new Color(0.95f, 0.65f, 0.15f),
        "A" => new Color(0.30f, 0.75f, 0.35f),
        "B" => new Color(0.25f, 0.55f, 0.85f),
        "C" => new Color(0.55f, 0.45f, 0.75f),
        "D" => new Color(0.75f, 0.45f, 0.25f),
        "F" => new Color(0.75f, 0.25f, 0.20f),
        _ => new Color(0.4f, 0.4f, 0.4f),
    };
}
