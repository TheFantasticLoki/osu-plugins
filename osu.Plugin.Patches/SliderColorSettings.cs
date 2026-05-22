// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using AccessItEasy;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Plugins;
using osu.Game.Rulesets.Osu.Skinning;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Plugin.Patches;

/// <summary>
/// Skin settings for slider body styling.
/// Provides style selection, combo color override, and border customization options.
/// </summary>
public class SliderColorSettings : ISkinSettings
{
    private OsuGame game = null!;

    public readonly Bindable<SliderStyle> Style = new Bindable<SliderStyle>
    {
        Default = global::osu.Plugin.Patches.SliderStyle.Default,
        Value = global::osu.Plugin.Patches.SliderStyle.Default,
    };

    public readonly BindableBool UseComboColors = new BindableBool
    {
        Default = false,
        Value = false,
    };

    public readonly Bindable<BorderStyle> BorderStyle = new Bindable<BorderStyle>
    {
        Default = global::osu.Plugin.Patches.BorderStyle.Skin,
        Value = global::osu.Plugin.Patches.BorderStyle.Skin,
    };

    public readonly BindableFloat BorderLightness = new BindableFloat
    {
        Default = 0f,
        Value = 0f,
        MinValue = -1f,
        MaxValue = 1f,
    };

    public readonly BindableFloat BodyOpacity = new BindableFloat
    {
        Default = 0.7f,
        Value = 0.7f,
        MinValue = 0.1f,
        MaxValue = 1f,
    };

    public readonly Bindable<SliderStylePreset?> CurrentPreset = new Bindable<SliderStylePreset?>();

    IEnumerable<Drawable> ISkinSettings.CreateSettingsControls()
    {
        return new[]
        {
            new SettingsItemV2(new FormEnumDropdown<SliderStyle>
            {
                Caption = "Slider Style",
                HintText = "Select the slider body rendering style.",
                Current = Style,
            }),
            new SettingsItemV2(new FormCheckBox
            {
                Caption = "Use Combo Colors",
                HintText = "If enabled, slider bodies will use combo colours instead of the slider track colour.",
                Current = UseComboColors,
            }),
            new SettingsItemV2(new FormEnumDropdown<BorderStyle>
            {
                Caption = "Border Style",
                HintText = "Select how the slider border should be rendered.",
                Current = BorderStyle,
            }),
            new SettingsItemV2(new FormSliderBar<float>
            {
                Caption = "Border Lightness",
                HintText = "Adjust the lightness of the border outline. Negative = darker, Positive = lighter.",
                Current = BorderLightness,
            }),
            new SettingsItemV2(new FormSliderBar<float>
            {
                Caption = "Body Opacity",
                HintText = "Adjust the opacity of the slider body.",
                Current = BodyOpacity,
            }),
        };
    }

    void ISkinSettings.Load(OsuGame game, Scheduler scheduler)
    {
        this.game = game;

        legacySliderBody.OnInjected += onInjected;
        game.add_OnDispose(() => legacySliderBody.OnInjected -= onInjected);
    }

    private void onInjected(object target, IReadOnlyDependencyContainer dependencies)
    {
        // not managed by us.
        if (!ReferenceEquals(dependencies.Get<OsuGame>(), game))
            return;

        var sliderBody = (LegacySliderBody)target;
        var skin = dependencies.Get<ISkinSource>();

        var headAccentColour = (Bindable<Color4>)PlaySliderBodyAccessor.GetAccentColourBindable(sliderBody);
        var style = Style.GetBoundCopy();
        var useComboColors = UseComboColors.GetBoundCopy();
        var borderStyle = BorderStyle.GetBoundCopy();
        var borderLightness = BorderLightness.GetBoundCopy();
        var bodyOpacity = BodyOpacity.GetBoundCopy();

        headAccentColour.UnbindEvents();

        style.BindValueChanged(_ => updateSliderStyle());
        useComboColors.BindValueChanged(_ => updateSliderStyle());
        borderStyle.BindValueChanged(_ => updateSliderStyle());
        borderLightness.BindValueChanged(_ => updateSliderStyle());
        bodyOpacity.BindValueChanged(_ => updateSliderStyle());
        headAccentColour.BindValueChanged(_ => updateSliderStyle(), true);

        sliderBody.add_OnDispose(() =>
        {
            style.UnbindAll();
            useComboColors.UnbindAll();
            borderStyle.UnbindAll();
            borderLightness.UnbindAll();
            bodyOpacity.UnbindAll();
        });

        void updateSliderStyle()
        {
            // Get the base color - either combo color or slider track color
            Color4 newColour = useComboColors.Value
                ? headAccentColour.Value
                : PlaySliderBodyAccessor.GetBodyAccentColour(sliderBody, skin, headAccentColour.Value);

            // Apply opacity
            newColour = newColour.Opacity(bodyOpacity.Value);

            // Apply style based on selection
            if (style.Value == global::osu.Plugin.Patches.SliderStyle.GlowingEdge)
            {
                applyGlowingEdgeStyle(sliderBody, newColour, skin, borderStyle.Value, borderLightness.Value);
            }
            else
            {
                // Default style - just apply color and opacity
                SliderBodyAccessor.SetAccentColour(sliderBody, newColour);
            }
        }

        void applyGlowingEdgeStyle(LegacySliderBody body, Color4 colour, ISkinSource skin, BorderStyle borderStyle, float lightness)
        {
            var currentPath = SliderBodyAccessor.GetPath(body);
            if (currentPath == null) return;

            // Get border color based on border style
            var borderColour = GetBorderColourForStyle(body, skin, borderStyle, colour, lightness);

            // Create a new glowing edge path
            var glowingPath = new GlowingEdgeDrawableSliderPath
            {
                PathRadius = currentPath.PathRadius,
                AccentColour = colour,
                BorderColour = borderColour,
                BorderSize = currentPath.BorderSize,
                Vertices = currentPath.Vertices.ToArray(),
                ShowBorderOutline = true,
            };

            // Remove the old path from the visual tree
            var oldPath = SliderBodyAccessor.GetPath(body);
            if (oldPath != null)
            {
                body.RemoveInternal(oldPath, true);
            }

            // Set the new path directly
            SliderBodyAccessor.SetPath(body, glowingPath);
            body.AddInternal(glowingPath);
        }

        Color4 GetBorderColourForStyle(LegacySliderBody body, ISkinSource skin, BorderStyle style, Color4 comboColour, float lightness)
        {
            Color4 borderColour = style switch
            {
                global::osu.Plugin.Patches.BorderStyle.ComboColor => comboColour,
                global::osu.Plugin.Patches.BorderStyle.LighterCombo => comboColour.Lighten(lightness),
                _ => LegacySliderBodyAccessor.GetBorderColour(body, skin)
            };

            if (lightness != 0f && style == global::osu.Plugin.Patches.BorderStyle.Skin)
            {
                borderColour = lightness > 0 
                    ? borderColour.Lighten(lightness) 
                    : borderColour.Darken(-lightness);
            }

            return borderColour;
        }
    }

    private static readonly IDependencyActivatorProxy legacySliderBody = DependencyActivatorProxyFactory.GetProxy(typeof(LegacySliderBody));

    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255", Justification = "Intentional usage of module initializer to ensure early initialization.")]
    internal static void Initialize()
    {
        _ = legacySliderBody; // ensure the proxy is initialized
    }
}

[SuppressMessage("Style", "OFSG001", Justification = "There's no dependency injection candidate.")]
file abstract class PlaySliderBodyAccessor : PlaySliderBody
{
    [PrivateAccessor(PrivateAccessorKind.Method, Name = $"get_{nameof(AccentColourBindable)}")]
    internal static extern IBindable<Color4> GetAccentColourBindable(PlaySliderBody instance);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = nameof(GetBodyAccentColour))]
    internal static extern Color4 GetBodyAccentColour(PlaySliderBody instance, ISkinSource skin, Color4 hitObjectAccentColour);
}

[SuppressMessage("Style", "OFSG001", Justification = "There's no dependency injection candidate.")]
file abstract class SliderBodyAccessor : SliderBody
{
    [PrivateAccessor(PrivateAccessorKind.Method, Name = $"set_{nameof(AccentColour)}")]
    internal static extern void SetAccentColour(SliderBody instance, Color4 value);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = nameof(RecyclePath))]
    internal static extern void RecyclePath(SliderBody instance);

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "path")]
    internal static extern void SetPath(SliderBody instance, DrawableSliderPath value);

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "path")]
    internal static extern DrawableSliderPath? GetPath(SliderBody instance);
}

[SuppressMessage("Style", "OFSG001", Justification = "There's no dependency injection candidate.")]
file abstract class LegacySliderBodyAccessor : LegacySliderBody
{
    [PrivateAccessor(PrivateAccessorKind.Method, Name = nameof(GetBorderColour))]
    internal static extern Color4 GetBorderColour(LegacySliderBody instance, ISkinSource skin);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = nameof(CreateSliderPath))]
    internal static extern DrawableSliderPath CreateSliderPath(LegacySliderBody instance);
}