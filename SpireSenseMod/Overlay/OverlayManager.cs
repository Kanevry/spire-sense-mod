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
    /// Creates a badge for each card positioned relative to the card UI.
    /// </summary>
    public void ShowCardTiers(List<CardInfo> cards)
    {
        EnsureInitialized();
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

    private static Control CreateTierBadge(CardInfo card, int index)
    {
        // Tier is determined by the web app's scoring engine.
        // For the in-game overlay, we use a simplified static tier
        // or fetch from the web app API.
        var tier = "?"; // Will be populated by scoring data
        var score = 0;

        var badge = new TierBadge();
        badge.Position = new Vector2(200 + (index * 300), 50);
        badge.SetData(tier, score, card.Name);

        return badge;
    }
}
