// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full font licence text.

namespace osu.Plugin.SliderPatches;

/// <summary>
/// Available border rendering styles for slider bodies.
/// </summary>
public enum BorderStyle
{
    /// <summary>
    /// Use the skin-defined slider border colour.
    /// </summary>
    Skin,

    /// <summary>
    /// Use the current combo colour as the border colour.
    /// </summary>
    ComboColor,

    /// <summary>
    /// Use the current combo colour with a configurable lightness modifier applied.
    /// </summary>
    CustomLightness,
}