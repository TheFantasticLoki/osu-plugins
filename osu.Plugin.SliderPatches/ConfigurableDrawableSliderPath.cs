// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics.CodeAnalysis;
using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osuTK.Graphics;

namespace osu.Plugin.SliderPatches;

/// <summary>
/// A unified slider path renderer that supports multiple body styles, border styles,
/// glow width control, and opacity — all computed in a single <see cref="ColourAt"/> override.
/// No path swapping at runtime; properties are updated and texture is invalidated instead.
/// </summary>
[SuppressMessage("Style", "OFSG001", Justification = "Not a dependency injection candidate.")]
public partial class ConfigurableDrawableSliderPath : DrawableSliderPath
{
    // ─── Plugin state ───

    /// <summary>
    /// When false, <see cref="ColourAt"/> mimics the exact default osu! slider body gradient
    /// — edge alpha 0.8, centre alpha 0.3, linear across full body width, using <see cref="AccentColour"/>.
    /// </summary>
    public bool PluginEnabled { get; set; } = true;

    // ─── Style properties ───

    /// <summary>
    /// The active body rendering style.
    /// </summary>
    public BodyStyle ActiveBodyStyle { get; set; } = BodyStyle.Default;

    /// <summary>
    /// The active border rendering style.
    /// </summary>
    public BorderStyle ActiveBorderStyle { get; set; } = BorderStyle.Skin;

    /// <summary>
    /// Lightness modifier applied when <see cref="ActiveBorderStyle"/> is <see cref="BorderStyle.CustomLightness"/>.
    /// Range -1..1. Negative = darker, Positive = lighter.
    /// </summary>
    public float ActiveBorderLightness { get; set; }

    /// <summary>
    /// Controls how much of the body width participates in the edge-to-centre gradient
    /// for <see cref="BodyStyle.GlowingEdge"/>. 0 = no glow (solid), 1 = full gradient across entire body.
    /// For <see cref="BodyStyle.Default"/> this is ignored — the classic linear gradient is always used.
    /// </summary>
    public float ActiveGlowWidth { get; set; } = 0.5f;

    // ─── Computed intermediate values (set externally; setters call InvalidateTexture) ───

    private Color4 computedAccentColour = Color4.White;

    /// <summary>
    /// The base accent colour (RGB). Already resolved (combo colour vs. skin track override).
    /// Setting this triggers texture invalidation.
    /// </summary>
    public Color4 ComputedAccentColour
    {
        get => computedAccentColour;
        set
        {
            if (computedAccentColour == value)
                return;

            computedAccentColour = value;
            InvalidateTexture();
        }
    }

    private Color4 computedBorderColour = Color4.White;

    /// <summary>
    /// The base border colour (RGB). Already resolved (skin border vs. combo colour).
    /// Setting this triggers texture invalidation.
    /// </summary>
    public Color4 ComputedBorderColour
    {
        get => computedBorderColour;
        set
        {
            if (computedBorderColour == value)
                return;

            computedBorderColour = value;
            InvalidateTexture();
        }
    }

    private float computedBodyAlpha = 1f;

    /// <summary>
    /// Overall body opacity multiplier applied on top of the style's gradient alpha.
    /// Setting this triggers texture invalidation.
    /// </summary>
    public float ComputedBodyAlpha
    {
        get => computedBodyAlpha;
        set
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(computedBodyAlpha - clamped) < 0.001f)
                return;

            computedBodyAlpha = clamped;
            InvalidateTexture();
        }
    }

    // ─── Style constants ───

    private const float default_edge_alpha = 0.3f;
    private const float default_centre_alpha = 0.8f;
    private const float glow_centre_alpha = 0.0f;
    private const float solid_alpha = 0.8f;
    private const float inverted_edge_alpha = 0.0f;
    private const float inverted_centre_alpha = 0.8f;

    protected override Color4 ColourAt(float position)
    {
        // ── Border zone ──
        if (CalculatedBorderPortion > 0f && position <= CalculatedBorderPortion)
        {
            if (!PluginEnabled)
                return BorderColour;

            Color4 borderColour = ComputedBorderColour;

            if (ActiveBorderStyle == BorderStyle.CustomLightness)
            {
                float l = ActiveBorderLightness;
                borderColour = l >= 0
                    ? borderColour.Lighten(l)
                    : borderColour.Darken(-l);
            }

            return borderColour;
        }

        // ── Body zone ──
        // Normalise position within the body region (0 = inner edge of border, 1 = centre)
        float bodyPosition = (position - CalculatedBorderPortion) / Math.Max(1f - CalculatedBorderPortion, 0.001f);
        float finalAlpha;

        if (!PluginEnabled)
        {
            // ── Exact default behaviour ──
            // Linear interpolation from edge (0.8) to centre (0.3) across full body width.
            // Uses AccentColour (set by the game's normal path colouring).
            finalAlpha = (default_edge_alpha - (default_edge_alpha - default_centre_alpha) * bodyPosition) * AccentColour.A;
            return new Color4(AccentColour.R, AccentColour.G, AccentColour.B, Math.Clamp(finalAlpha, 0f, 1f));
        }

        // ── Plugin-active body style ──

        float edgeAlpha;
        float centreAlpha;

        switch (ActiveBodyStyle)
        {
            case BodyStyle.Default:
                // Classic linear gradient — no glow width involvement.
                finalAlpha = (default_edge_alpha - (default_edge_alpha - default_centre_alpha) * bodyPosition) * ComputedBodyAlpha * ComputedAccentColour.A;
                return new Color4(ComputedAccentColour.R, ComputedAccentColour.G, ComputedAccentColour.B, Math.Clamp(finalAlpha, 0f, 1f));

            case BodyStyle.GlowingEdge:
                edgeAlpha = default_edge_alpha;
                centreAlpha = glow_centre_alpha;
                break;

            case BodyStyle.SolidFill:
                edgeAlpha = solid_alpha;
                centreAlpha = solid_alpha;
                break;

            case BodyStyle.InvertedGradient:
                edgeAlpha = inverted_edge_alpha;
                centreAlpha = inverted_centre_alpha;
                break;

            default:
                return new Color4(ComputedAccentColour.R, ComputedAccentColour.G, ComputedAccentColour.B, ComputedAccentColour.A);
        }

        // ── Glow width gradient ──
        // For GlowingEdge: glowWidth controls how rapidly the alpha falls off from the edge.
        //   glowWidth=0.1 → tight glow (very close to edge)
        //   glowWidth=1.0 → gradient across full body width
        // For SolidFill / InvertedGradient: uses full body width gradient (glowWidth=1 equivalent).
        float gw = ActiveBodyStyle == BodyStyle.GlowingEdge ? Math.Clamp(ActiveGlowWidth, 0.01f, 1f) : 1f;
        float t = Math.Clamp(bodyPosition / gw, 0f, 1f);
        finalAlpha = (edgeAlpha + (centreAlpha - edgeAlpha) * t) * ComputedBodyAlpha * ComputedAccentColour.A;

        return new Color4(
            ComputedAccentColour.R,
            ComputedAccentColour.G,
            ComputedAccentColour.B,
            Math.Clamp(finalAlpha, 0f, 1f)
        );
    }
}