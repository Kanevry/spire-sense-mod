using System.Collections.Generic;
using Godot;

namespace SpireSenseMod;

/// <summary>
/// Manages the in-game overlay using Godot's CanvasLayer.
/// Renders minimal tier badges (S/A/B/C/D/F) on card reward screens.
/// </summary>
public class OverlayManager
{
    private CanvasLayer? _overlayLayer;
    private readonly List<Control> _activeBadges = new();

    public void Initialize(Node parent)
    {
        _overlayLayer = new CanvasLayer();
        _overlayLayer.Layer = 100; // Render on top of game UI
        parent.AddChild(_overlayLayer);
    }

    /// <summary>
    /// Show tier badges for card rewards.
    /// Creates a badge for each card positioned relative to the card UI.
    /// </summary>
    public void ShowCardTiers(List<CardInfo> cards)
    {
        HideCardTiers();

        if (_overlayLayer == null) return;

        // TODO: Position badges relative to actual card reward UI elements.
        // This requires knowing the game's UI layout for card rewards.
        // For now, create badges at estimated positions.
        for (int i = 0; i < cards.Count; i++)
        {
            var badge = CreateTierBadge(cards[i], i);
            _overlayLayer.AddChild(badge);
            _activeBadges.Add(badge);
        }
    }

    /// <summary>
    /// Remove all tier badges from the overlay.
    /// </summary>
    public void HideCardTiers()
    {
        foreach (var badge in _activeBadges)
        {
            badge.QueueFree();
        }
        _activeBadges.Clear();
    }

    private static Control CreateTierBadge(CardInfo card, int index)
    {
        // Tier is determined by the web app's scoring engine.
        // For the in-game overlay, we use a simplified static tier
        // or fetch from the web app API.
        var tier = "?"; // Will be populated by scoring data

        var container = new PanelContainer();
        container.Position = new Vector2(200 + (index * 300), 50);
        container.Size = new Vector2(40, 40);

        // Styling
        var styleBox = new StyleBoxFlat();
        styleBox.BgColor = GetTierColor(tier);
        styleBox.CornerRadiusTopLeft = 6;
        styleBox.CornerRadiusTopRight = 6;
        styleBox.CornerRadiusBottomLeft = 6;
        styleBox.CornerRadiusBottomRight = 6;
        container.AddThemeStyleboxOverride("panel", styleBox);

        var label = new Label();
        label.Text = tier;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 20);
        container.AddChild(label);

        return container;
    }

    private static Color GetTierColor(string tier) => tier switch
    {
        "S" => new Color(0.95f, 0.65f, 0.15f),  // Gold
        "A" => new Color(0.30f, 0.75f, 0.35f),   // Green
        "B" => new Color(0.25f, 0.55f, 0.85f),   // Blue
        "C" => new Color(0.55f, 0.45f, 0.75f),   // Purple
        "D" => new Color(0.75f, 0.45f, 0.25f),   // Orange
        "F" => new Color(0.75f, 0.25f, 0.20f),   // Red
        _ => new Color(0.4f, 0.4f, 0.4f),        // Gray
    };
}
