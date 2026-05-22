// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Plugin.Patches;

/// <summary>
/// Available slider body rendering styles.
/// </summary>
public enum SliderStyle
{
    Default,
    GlowingEdge
}

/// <summary>
/// Available border rendering styles.
/// </summary>
public enum BorderStyle
{
    Skin,
    ComboColor,
    LighterCombo
}

/// <summary>
/// Preset configuration for slider styling.
/// </summary>
public class SliderStylePreset
{
    public string Name { get; set; } = "Default";
    public SliderStyle Style { get; set; } = global::osu.Plugin.Patches.SliderStyle.Default;
    public bool UseComboColors { get; set; } = false;
    public BorderStyle BorderStyle { get; set; } = global::osu.Plugin.Patches.BorderStyle.Skin;
    public float BorderLightness { get; set; } = 0f;
    public float BodyOpacity { get; set; } = 0.7f;

    public SliderStylePreset() { }

    public SliderStylePreset(string name, SliderStyle style, bool useComboColors, BorderStyle borderStyle, float borderLightness, float bodyOpacity)
    {
        Name = name;
        Style = style;
        UseComboColors = useComboColors;
        BorderStyle = borderStyle;
        BorderLightness = borderLightness;
        BodyOpacity = bodyOpacity;
    }

    /// <summary>
    /// Converts the preset to a shareable string format.
    /// </summary>
    public string ToShareableString()
    {
        return $"{Name}|{Style}|{UseComboColors}|{BorderStyle}|{BorderLightness}|{BodyOpacity}";
    }

    /// <summary>
    /// Creates a preset from a shareable string.
    /// </summary>
    public static SliderStylePreset FromShareableString(string data)
    {
        var parts = data.Split('|');
        if (parts.Length != 6)
            throw new ArgumentException("Invalid preset format");

        return new SliderStylePreset
        {
            Name = parts[0],
            Style = Enum.Parse<SliderStyle>(parts[1]),
            UseComboColors = bool.Parse(parts[2]),
            BorderStyle = Enum.Parse<BorderStyle>(parts[3]),
            BorderLightness = float.Parse(parts[4]),
            BodyOpacity = float.Parse(parts[5])
        };
    }
}