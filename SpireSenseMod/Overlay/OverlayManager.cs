using System.Collections.Generic;
using Godot;

namespace SpireSenseMod;

/// <summary>
/// Manages the in-game overlay using Godot's CanvasLayer.
/// Renders minimal tier badges (S/A/B/C/D/F) on card reward screens.
/// Lazy-initializes by attaching to the scene tree when first needed.
/// </summary>
public class OverlayManager
{
    private CanvasLayer? _overlayLayer;
    private readonly List<Control> _activeBadges = new();

    // Card reward layout constants — estimated from NCardRewardSelectionScreen.
    //
    // POSITIONING APPROACH (GitLab #126 analysis, 2026-03-30):
    // Accurate badge placement requires knowing where NCardRewardSelectionScreen
    // positions its NCardHolder children. To get exact positions:
    //
    //   1. Decompile NCardRewardSelectionScreen from sts2.dll:
    //      ilspycmd "path/to/sts2.dll" -t "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen"
    //
    //   2. Find how ShowScreen() creates/positions NCardHolder instances — look for
    //      layout containers (HBoxContainer, GridContainer) or manual Position assignments
    //
    //   3. At runtime, walk the scene tree from the NCardRewardSelectionScreen node
    //      to find the NCardHolder children and read their GlobalPosition.
    //      This requires a Harmony postfix on ShowScreen that captures __result
    //      (the screen instance) and traverses its Godot child nodes.
    //
    //   4. Pass those positions into ShowCardTiers() so badges align to actual cards.
    //
    // Until then, we use viewport-relative estimates based on STS2's default
    // 1920x1080 layout where 3 cards are centered horizontally with ~300px spacing.
    // The values below are normalized against the viewport and scale at runtime.

    /// <summary>
    /// Fraction of viewport width between adjacent cards.
    /// </summary>
    private const float CardSpacingXFraction = 0.28f;

    /// <summary>
    /// Fraction of viewport height for badge Y position (top of card area).
    /// </summary>
    private const float BadgeYFraction = 0.04f;

    /// <summary>
    /// Remove the overlay from the scene tree and release all resources.
    /// </summary>
    public void Cleanup()
    {
        HideCardTiers();
        _overlayLayer?.GetParent()?.RemoveChild(_overlayLayer);
        _overlayLayer = null;
    }

    /// <summary>
    /// Show tier badges for card rewards.
    /// Creates a badge for each card positioned relative to the viewport.
    /// </summary>
    public void ShowCardTiers(List<CardInfo> cards)
    {
        EnsureInitialized();
        HideCardTiers();

        if (_overlayLayer == null) return;

        var viewport = _overlayLayer.GetViewport();
        var viewportSize = viewport?.GetVisibleRect().Size ?? new Vector2(1920, 1080);

        for (int i = 0; i < cards.Count; i++)
        {
            var badge = CreateTierBadge(cards[i], i, cards.Count, viewportSize);
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

    private void EnsureInitialized()
    {
        if (_overlayLayer != null) return;

        // Find the scene tree root and attach overlay
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        if (sceneTree?.Root == null) return;

        _overlayLayer = new CanvasLayer();
        _overlayLayer.Layer = 100; // Render on top of game UI
        sceneTree.Root.CallDeferred("add_child", _overlayLayer);
    }

    private static Control CreateTierBadge(CardInfo card, int index, int totalCards, Vector2 viewportSize)
    {
        // Tier is determined by the web app's scoring engine.
        // For the in-game overlay, we use a simplified static tier
        // or fetch from the web app API.
        var tier = "?"; // Will be populated by scoring data
        var score = 0;

        // Calculate position relative to viewport size.
        // Center the card group: compute the total width, then offset from center.
        float totalWidth = (totalCards - 1) * CardSpacingXFraction * viewportSize.X;
        float groupStartX = (viewportSize.X - totalWidth) / 2;
        float x = groupStartX + (index * CardSpacingXFraction * viewportSize.X);
        float y = BadgeYFraction * viewportSize.Y;

        var badge = new TierBadge();
        badge.Position = new Vector2(x, y);
        badge.SetData(tier, score, card.Name);

        return badge;
    }
}
