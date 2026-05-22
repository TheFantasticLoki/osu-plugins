// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Plugin.SliderPatches;

/// <summary>
/// Available slider body rendering styles.
/// </summary>
public enum BodyStyle
{
    /// <summary>
    /// Default osu! slider body rendering — fades from opaque at edge to semi-transparent at centre.
    /// </summary>
    Default,

    /// <summary>
    /// Glowing edge style — edge is fully opaque, centre is fully transparent, creating a glow effect.
    /// The glow width parameter controls how much of the body participates in the fade.
    /// </summary>
    GlowingEdge,

    /// <summary>
    /// Solid fill — uniform opacity across the entire slider body width.
    /// </summary>
    SolidFill,

    /// <summary>
    /// Inverted gradient — centre is opaque, edges are transparent.
    /// </summary>
    InvertedGradient,
}