// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Plugins;

namespace osu.Plugin.Patches;

/// <summary>
/// Plugin that provides skin settings for slider body styling.
/// </summary>
public partial class SkinSettingsPlugin : OsuPlugin
{
    [SettingSource("Slider Style", "Select the slider body rendering style.")]
    public Bindable<SliderStyle> Style { get; } = new Bindable<SliderStyle>
    {
        Default = global::osu.Plugin.Patches.SliderStyle.Default,
        Value = global::osu.Plugin.Patches.SliderStyle.Default,
    };

    [SettingSource("Use Combo Colors", "If enabled, slider bodies will use combo colours instead of the slider track colour.")]
    public BindableBool UseComboColors { get; } = new BindableBool
    {
        Default = false,
        Value = false,
    };

    [SettingSource("Border Style", "Select how the slider border should be rendered.")]
    public Bindable<BorderStyle> BorderStyle { get; } = new Bindable<BorderStyle>
    {
        Default = global::osu.Plugin.Patches.BorderStyle.Skin,
        Value = global::osu.Plugin.Patches.BorderStyle.Skin,
    };

    [SettingSource("Border Lightness", "Adjust the lightness of the border outline. Negative = darker, Positive = lighter.")]
    public BindableFloat BorderLightness { get; } = new BindableFloat
    {
        Default = 0f,
        Value = 0f,
        MinValue = -1f,
        MaxValue = 1f,
    };

    [SettingSource("Body Opacity", "Adjust the opacity of the slider body.")]
    public BindableFloat BodyOpacity { get; } = new BindableFloat
    {
        Default = 0.7f,
        Value = 0.7f,
        MinValue = 0.1f,
        MaxValue = 1f,
    };

    private readonly ISkinSettings[] skinSettings;

    public SkinSettingsPlugin()
    {
        skinSettings = new ISkinSettings[]
        {
            new SliderColorSettings()
            {
                Style = { BindTarget = Style },
                UseComboColors = { BindTarget = UseComboColors },
                BorderStyle = { BindTarget = BorderStyle },
                BorderLightness = { BindTarget = BorderLightness },
                BodyOpacity = { BindTarget = BodyOpacity },
            },
        };
    }

    public override IEnumerable<Drawable>? CreateSettingsControls()
    {
        foreach (var settings in skinSettings)
        {
            foreach (var controls in settings.CreateSettingsControls())
            {
                yield return controls;
            }
        }
    }

    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame)
            return;

        foreach (var settings in skinSettings)
            settings.Load((OsuGame)gameBase, scheduler);
    }
}