// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics.CodeAnalysis;
using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
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

    // (none currently needed)

    protected override Color4 ColourAt(float position)
    {
        // ── Border zone ──
        // NOTE: the original LegacyDrawableSliderPath uses a hardcoded border_portion of 0.1875f,
        // independent of BorderSize. We honour that for disabled mode.
        if (PluginEnabled)
        {
            // Plugin mode: use the base DrawableSliderPath's CalculatedBorderPortion.
            if (CalculatedBorderPortion > 0f && position <= CalculatedBorderPortion)
            {
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
        }
        else
        {
            // Disabled mode: shadow + border zones matching LegacyDrawableSliderPath exactly.
            Color4 shadow = new Color4(0, 0, 0, 0.25f);
            const float shadow_portion = 1f - (OsuLegacySkinTransformer.LEGACY_CIRCLE_RADIUS / OsuHitObject.OBJECT_RADIUS);
            const float border_portion = 0.1875f;

            if (position <= shadow_portion)
                return InterpolateColourLinear(position, Color4.Black.Opacity(0f), shadow, 0, shadow_portion);
            if (position <= border_portion)
                return InterpolateColourLinear(position, shadow, BorderColour, shadow_portion, border_portion);

            // Body zone starts at border_portion.
            position = (position - border_portion) / Math.Max(1f - border_portion, 0.001f);
            Color4 outerColour = AccentColour.Darken(0.1f);
            Color4 innerColour = Lighten(AccentColour, 0.5f);

            return new Color4(
                outerColour.R + (innerColour.R - outerColour.R) * position,
                outerColour.G + (innerColour.G - outerColour.G) * position,
                outerColour.B + (innerColour.B - outerColour.B) * position,
                outerColour.A + (innerColour.A - outerColour.A) * position
            );
        }

        // ── Body zone (plugin mode) ──
        // Normalise position within the body region (0 = inner edge of border, 1 = centre)
        float bodyPosition = (position - CalculatedBorderPortion) / Math.Max(1f - CalculatedBorderPortion, 0.001f);
        Color4 baseAccent = ComputedAccentColour;
        float bodyAlphaMultiplier = ComputedBodyAlpha;

        switch (ActiveBodyStyle)
        {
            case BodyStyle.Default:
            {
                // Reproduce the original Darken(0.1f)/lighten(0.5f) RGB gradient,
                // with ComputedBodyAlpha applied on top of the original alpha.
                Color4 edgeColour = baseAccent.Darken(0.1f);
                Color4 centreColour = Lighten(baseAccent, 0.5f);

                return new Color4(
                    edgeColour.R + (centreColour.R - edgeColour.R) * bodyPosition,
                    edgeColour.G + (centreColour.G - edgeColour.G) * bodyPosition,
                    edgeColour.B + (centreColour.B - edgeColour.B) * bodyPosition,
                    (edgeColour.A + (centreColour.A - edgeColour.A) * bodyPosition) * bodyAlphaMultiplier
                );
            }

            case BodyStyle.GlowingEdge:
            {
                // Edge is at full colour (Darken(0.1f) matches the original outer edge).
                // Alpha falls off from 1 at the edge to 0 toward the centre.
                // GlowWidth controls how quickly the falloff happens:
                //   0 → instant (edge-only, immediate transparency)
                //   0.5 → alpha reaches 0 about halfway to the centre
                //   1 → alpha reaches 0 at the centre (full body width gradient)
                Color4 edgeColour = baseAccent.Darken(0.1f);
                float gw = Math.Clamp(ActiveGlowWidth, 0.01f, 1f);
                float t = Math.Clamp(bodyPosition / gw, 0f, 1f);
                float alpha = (1f - t) * bodyAlphaMultiplier;
                return new Color4(edgeColour.R, edgeColour.G, edgeColour.B, alpha);
            }

            case BodyStyle.SolidFill:
            {
                // Solid fill at the centre (brightest) colour.
                Color4 centreColour = Lighten(baseAccent, 0.5f);
                return new Color4(centreColour.R, centreColour.G, centreColour.B, 0.8f * bodyAlphaMultiplier);
            }

            case BodyStyle.InvertedGradient:
            {
                // Darker at edge, brighter toward centre, with alpha going 0→0.8.
                Color4 edgeColour = baseAccent.Darken(0.1f);
                Color4 centreColour = Lighten(baseAccent, 0.5f);
                float alpha = (0.0f + (0.8f - 0.0f) * bodyPosition) * bodyAlphaMultiplier;
                return new Color4(
                    edgeColour.R + (centreColour.R - edgeColour.R) * bodyPosition,
                    edgeColour.G + (centreColour.G - edgeColour.G) * bodyPosition,
                    edgeColour.B + (centreColour.B - edgeColour.B) * bodyPosition,
                    alpha
                );
            }

            default:
                return baseAccent;
        }
    }

    /// <summary>
    /// Linear RGB interpolation between two colours (alpha included).
    /// </summary>
    private static Color4 InterpolateColourLinear(float t, Color4 start, Color4 end, float tStart, float tEnd)
    {
        float progress = (t - tStart) / Math.Max(tEnd - tStart, 0.001f);
        progress = Math.Clamp(progress, 0f, 1f);

        return new Color4(
            start.R + (end.R - start.R) * progress,
            start.G + (end.G - start.G) * progress,
            start.B + (end.B - start.B) * progress,
            start.A + (end.A - start.A) * progress
        );
    }

    /// <summary>
    /// Lightens a colour in a way more friendly to dark or strong colours (mirrors LegacySliderBody).
    /// </summary>
    private static Color4 Lighten(Color4 color, float amount)
    {
        amount *= 0.5f;
        return new Color4(
            Math.Min(1, color.R * (1 + 0.5f * amount) + 1 * amount),
            Math.Min(1, color.G * (1 + 0.5f * amount) + 1 * amount),
            Math.Min(1, color.B * (1 + 0.5f * amount) + 1 * amount),
            color.A);
    }
}