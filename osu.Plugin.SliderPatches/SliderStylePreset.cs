// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Plugin.SliderPatches;

/// <summary>
/// Represents a snapshot of slider styling settings that can be saved, loaded, and shared.
/// </summary>
public class SliderStylePreset
{
    public string Name { get; set; } = "Default";
    public BodyStyle BodyStyle { get; set; } = global::osu.Plugin.SliderPatches.BodyStyle.Default;
    public BorderStyle BorderStyle { get; set; } = global::osu.Plugin.SliderPatches.BorderStyle.Skin;
    public bool UseComboColors { get; set; } = false;
    public float BorderLightness { get; set; } = 0f;
    public float BodyOpacity { get; set; } = 0.7f;
    public float GlowWidth { get; set; } = 0.5f;

    public SliderStylePreset()
    {
    }

    public SliderStylePreset(string name, BodyStyle bodyStyle, bool useComboColors, BorderStyle borderStyle, float borderLightness, float bodyOpacity, float glowWidth)
    {
        Name = name;
        BodyStyle = bodyStyle;
        UseComboColors = useComboColors;
        BorderStyle = borderStyle;
        BorderLightness = borderLightness;
        BodyOpacity = bodyOpacity;
        GlowWidth = glowWidth;
    }

    /// <summary>
    /// Converts the preset to a shareable string format.
    /// </summary>
    public string ToShareableString()
    {
        return $"{Name}|{(int)BodyStyle}|{UseComboColors}|{(int)BorderStyle}|{BorderLightness}|{BodyOpacity}|{GlowWidth}";
    }

    /// <summary>
    /// Creates a preset from a shareable string.
    /// </summary>
    public static SliderStylePreset FromShareableString(string data)
    {
        var parts = data.Split('|');
        if (parts.Length != 7)
            throw new ArgumentException("Invalid preset format. Expected 7 parts.");

        return new SliderStylePreset
        {
            Name = parts[0],
            BodyStyle = (BodyStyle)int.Parse(parts[1]),
            UseComboColors = bool.Parse(parts[2]),
            BorderStyle = (BorderStyle)int.Parse(parts[3]),
            BorderLightness = float.Parse(parts[4]),
            BodyOpacity = float.Parse(parts[5]),
            GlowWidth = float.Parse(parts[6]),
        };
    }
}