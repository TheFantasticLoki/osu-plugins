// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics.CodeAnalysis;
using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osuTK.Graphics;

namespace osu.Plugin.Patches;

/// <summary>
/// A slider path that renders with a glowing edge style.
/// Edge is colored, center fades to transparent.
/// Border is rendered as a separate outline around the glow.
/// </summary>
[SuppressMessage("Style", "OFSG001", Justification = "There's no dependency injection candidate.")]
public partial class GlowingEdgeDrawableSliderPath : DrawableSliderPath
{
    private const float opacity_at_edge = 0.8f;
    private const float opacity_at_centre = 0.0f; // Transparent center for glowing effect

    /// <summary>
    /// Whether to render a border outline around the glowing edge.
    /// </summary>
    public bool ShowBorderOutline { get; set; } = true;

    protected override Color4 ColourAt(float position)
    {
        // For glowing edge style with border outline:
        // - Border outline (position 0 to CalculatedBorderPortion): Border colour
        // - Glow (position CalculatedBorderPortion to 1): Edge colored, center transparent
        
        if (ShowBorderOutline && CalculatedBorderPortion > 0f && position <= CalculatedBorderPortion)
        {
            // Render border outline at the edge
            return BorderColour;
        }

        // Calculate glow position (0 = edge of glow, 1 = center)
        float glowPosition = ShowBorderOutline 
            ? Math.Clamp((position - CalculatedBorderPortion) / GRADIENT_PORTION, 0f, 1f)
            : Math.Clamp(position, 0f, 1f);

        // Glow: Edge is full color, center is transparent
        float alpha = opacity_at_edge - (opacity_at_edge - opacity_at_centre) * glowPosition;
        return new Color4(AccentColour.R, AccentColour.G, AccentColour.B, alpha * AccentColour.A);
    }
}