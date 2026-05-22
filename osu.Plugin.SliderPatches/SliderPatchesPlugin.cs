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
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Plugins;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Plugin.SliderPatches;

/// <summary>
/// Plugin that provides comprehensive slider body styling options.
/// Replaces the default slider path with a <see cref="ConfigurableDrawableSliderPath"/> once per instance,
/// then updates styling via bindable properties — no path swapping at runtime.
/// Automatically re-injects the custom path after <see cref="SliderBody.RecyclePath"/> is called
/// (e.g. when a pooled slider is killed and reused).
/// </summary>
public partial class SliderPatchesPlugin : OsuPlugin
{
    public override string? DisplayName => "Slider Patches";

    public override string? Description => "Adds customizable slider body styles, border options, and combo color overrides.";

    // ─── Plugin settings ───

    [SettingSource("Body Style", "Select the slider body rendering style.")]
    public Bindable<BodyStyle> BodyStyle { get; } = new Bindable<BodyStyle>
    {
        Default = global::osu.Plugin.SliderPatches.BodyStyle.Default,
        Value = global::osu.Plugin.SliderPatches.BodyStyle.Default,
    };

    [SettingSource("Use Combo Colors", "If enabled, slider bodies use combo colours instead of the skin's slider track colour.")]
    public BindableBool UseComboColors { get; } = new BindableBool
    {
        Default = false,
        Value = false,
    };

    [SettingSource("Border Style", "Select how the slider border colour is determined.")]
    public Bindable<BorderStyle> BorderStyle { get; } = new Bindable<BorderStyle>
    {
        Default = global::osu.Plugin.SliderPatches.BorderStyle.Skin,
        Value = global::osu.Plugin.SliderPatches.BorderStyle.Skin,
    };

    [SettingSource("Border Lightness", "Adjust the lightness of the border when using CustomLightness mode. Negative = darker, Positive = lighter.")]
    public BindableFloat BorderLightness { get; } = new BindableFloat
    {
        Default = 0f,
        Value = 0f,
        MinValue = -1f,
        MaxValue = 1f,
    };

    [SettingSource("Body Opacity", "Adjust the overall opacity of the slider body.")]
    public BindableFloat BodyOpacity { get; } = new BindableFloat
    {
        Default = 0.7f,
        Value = 0.7f,
        MinValue = 0.1f,
        MaxValue = 1f,
    };

    [SettingSource("Glow Width", "Controls how much of the slider body fades toward the centre. 0 = solid fill, 1 = full gradient across the entire body width.")]
    public BindableFloat GlowWidth { get; } = new BindableFloat
    {
        Default = 0.5f,
        Value = 0.5f,
        MinValue = 0f,
        MaxValue = 1f,
    };

    public override IEnumerable<Drawable>? CreateSettingsControls()
    {
        yield return new SettingsItemV2(new FormEnumDropdown<global::osu.Plugin.SliderPatches.BodyStyle>
        {
            Caption = "Body Style",
            HintText = "Select the slider body rendering style.",
            Current = { BindTarget = BodyStyle },
        });

        yield return new SettingsItemV2(new FormCheckBox
        {
            Caption = "Use Combo Colors",
            HintText = "If enabled, slider bodies will use combo colours instead of the slider track colour.",
            Current = { BindTarget = UseComboColors },
        });

        yield return new SettingsItemV2(new FormEnumDropdown<global::osu.Plugin.SliderPatches.BorderStyle>
        {
            Caption = "Border Style",
            HintText = "Select how the slider border colour is determined.",
            Current = { BindTarget = BorderStyle },
        });

        yield return new SettingsItemV2(new FormSliderBar<float>
        {
            Caption = "Border Lightness",
            HintText = "Adjust the lightness of the border outline. Negative = darker, Positive = lighter.",
            Current = { BindTarget = BorderLightness },
        });

        yield return new SettingsItemV2(new FormSliderBar<float>
        {
            Caption = "Body Opacity",
            HintText = "Adjust the opacity of the slider body.",
            Current = { BindTarget = BodyOpacity },
        });

        yield return new SettingsItemV2(new FormSliderBar<float>
        {
            Caption = "Glow Width",
            HintText = "Controls how much of the slider body fades toward the centre. Higher = more transparent centre.",
            Current = { BindTarget = GlowWidth },
        });
    }

    // ─── Activator hook ───

    private OsuGame game = null!;

    private static readonly IDependencyActivatorProxy legacySliderBodyActivator =
#pragma warning disable CA2255
        DependencyActivatorProxyFactory.GetProxy(typeof(LegacySliderBody));
#pragma warning restore CA2255

    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255", Justification = "Intentional usage of module initializer to ensure early initialization.")]
    internal static void Initialize()
    {
        _ = legacySliderBodyActivator;
    }

    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame gameInstance)
            return;

        game = gameInstance;

        legacySliderBodyActivator.OnInjected += onSliderBodyInjected;
        game.add_OnDispose(() => legacySliderBodyActivator.OnInjected -= onSliderBodyInjected);
    }

    private void onSliderBodyInjected(object target, IReadOnlyDependencyContainer dependencies)
    {
        if (!ReferenceEquals(dependencies.Get<OsuGame>(), game))
            return;

        var sliderBody = (LegacySliderBody)target;
        var skin = dependencies.Get<ISkinSource>();
        var drawableSlider = (DrawableSlider)dependencies.Get<DrawableHitObject>();

        // ── Styling state (persists across path re-creation) ──

        Color4 lastBaseAccent = Color4.White;
        Color4 lastBorderColour = Color4.White;
        float lastBodyAlpha = 0.7f;
        BodyStyle lastBodyStyle = global::osu.Plugin.SliderPatches.BodyStyle.Default;
        BorderStyle lastBorderStyleType = global::osu.Plugin.SliderPatches.BorderStyle.Skin;
        float lastBorderLightness = 0f;
        float lastGlowWidth = 0.5f;
        bool lastPluginEnabled = true;

        // ── Setting bindings (declared up front so all closures can capture them) ──

        var headAccentColour = PlaySliderBodyAccessor.GetAccentColourBindable(sliderBody);
        headAccentColour.UnbindEvents();

        var bodyStyleBindable = BodyStyle.GetBoundCopy();
        var useComboBindable = UseComboColors.GetBoundCopy();
        var borderStyleBindable = BorderStyle.GetBoundCopy();
        var borderLightnessBindable = BorderLightness.GetBoundCopy();
        var bodyOpacityBindable = BodyOpacity.GetBoundCopy();
        var glowWidthBindable = GlowWidth.GetBoundCopy();
        var enabledBindable = Enabled.GetBoundCopy();

        // ── Create our custom path ──

        ConfigurableDrawableSliderPath customPath = null!;

        void updateStyle()
        {
            lastPluginEnabled = enabledBindable.Value;

            if (!lastPluginEnabled)
            {
                lastBaseAccent = LegacySliderBodyAccessor.GetBodyAccentColour(sliderBody, skin, headAccentColour.Value);
                lastBorderColour = Color4.White;
                lastBodyAlpha = 1f;
                lastBodyStyle = global::osu.Plugin.SliderPatches.BodyStyle.Default;
                lastBorderStyleType = global::osu.Plugin.SliderPatches.BorderStyle.Skin;
                lastBorderLightness = 0f;
                lastGlowWidth = 0.5f;
                applyStyleToPath();
                return;
            }

            Color4 rawCombo = headAccentColour.Value;

            if (useComboBindable.Value)
            {
                lastBaseAccent = rawCombo;
                lastBorderColour = new Color4(rawCombo.R, rawCombo.G, rawCombo.B, 1f);
            }
            else
            {
                lastBaseAccent = LegacySliderBodyAccessor.GetBodyAccentColour(sliderBody, skin, rawCombo);
                lastBorderColour = LegacySliderBodyAccessor.GetBorderColour(sliderBody, skin);
            }

            lastBodyAlpha = bodyOpacityBindable.Value;
            lastBodyStyle = bodyStyleBindable.Value;
            lastBorderStyleType = borderStyleBindable.Value;
            lastBorderLightness = borderLightnessBindable.Value;
            lastGlowWidth = glowWidthBindable.Value;
            applyStyleToPath();
        }

        void applyStyleToPath()
        {
            customPath.PluginEnabled = lastPluginEnabled;

            if (!lastPluginEnabled)
            {
                customPath.AccentColour = lastBaseAccent;
                return;
            }

            customPath.ComputedAccentColour = new Color4(lastBaseAccent.R, lastBaseAccent.G, lastBaseAccent.B, 1f);
            customPath.ComputedBorderColour = lastBorderColour;
            customPath.ComputedBodyAlpha = lastBodyAlpha;
            customPath.ActiveBodyStyle = lastBodyStyle;
            customPath.ActiveBorderStyle = lastBorderStyleType;
            customPath.ActiveBorderLightness = lastBorderLightness;
            customPath.ActiveGlowWidth = lastGlowWidth;
        }

        void recreateCustomPath()
        {
            var currentPath = SliderBodyAccessor.GetPath(sliderBody);

            customPath = new ConfigurableDrawableSliderPath
            {
                PathRadius = currentPath?.PathRadius ?? 10f,
                BorderSize = currentPath?.BorderSize ?? 1f,
                Vertices = currentPath?.Vertices?.ToArray() ?? Array.Empty<Vector2>(),
                Position = currentPath?.Position ?? Vector2.Zero,
                AccentColour = currentPath?.AccentColour ?? Color4.White,
                AutoSizeAxes = Axes.None,
                Size = sliderBody.Size,
            };

            if (currentPath != null && currentPath != customPath)
                sliderBody.RemoveInternal(currentPath, true);

            injectCustomPath(sliderBody, customPath);
            applyStyleToPath();
        }

        // ── Initial path injection ──

        recreateCustomPath();

        // ── Wire up all bindings ──

        bodyStyleBindable.BindValueChanged(_ => updateStyle());
        useComboBindable.BindValueChanged(_ => updateStyle());
        borderStyleBindable.BindValueChanged(_ => updateStyle());
        borderLightnessBindable.BindValueChanged(_ => updateStyle());
        bodyOpacityBindable.BindValueChanged(_ => updateStyle());
        glowWidthBindable.BindValueChanged(_ => updateStyle());
        enabledBindable.BindValueChanged(_ => updateStyle());
        headAccentColour.BindValueChanged(_ => updateStyle(), true);

        // ── Path version tracking (re-inject after RecyclePath) ──

        var pathVersion = drawableSlider.PathVersion.GetBoundCopy();
        pathVersion.BindValueChanged(_ =>
        {
            sliderBody.Scheduler.AddOnce(recreateCustomPath);
        });

        // ── Cleanup ──

        sliderBody.add_OnDispose(() =>
        {
            bodyStyleBindable.UnbindAll();
            useComboBindable.UnbindAll();
            borderStyleBindable.UnbindAll();
            borderLightnessBindable.UnbindAll();
            bodyOpacityBindable.UnbindAll();
            glowWidthBindable.UnbindAll();
            enabledBindable.UnbindAll();
            pathVersion.UnbindAll();
        });
    }

    /// <summary>
    /// Replaces the path field and visual tree child of a <see cref="SliderBody"/>
    /// with the given custom path.
    /// </summary>
    private static void injectCustomPath(SliderBody body, ConfigurableDrawableSliderPath customPath)
    {
        SliderBodyAccessor.SetPath(body, customPath);
        body.AddInternal(customPath);
    }
}

// ─── Accessor classes using AccessItEasy ───

[SuppressMessage("Style", "OFSG001", Justification = "Not a dependency injection candidate.")]
file abstract class PlaySliderBodyAccessor : PlaySliderBody
{
    [PrivateAccessor(PrivateAccessorKind.Method, Name = $"get_{nameof(AccentColourBindable)}")]
    internal static extern IBindable<Color4> GetAccentColourBindable(PlaySliderBody instance);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = nameof(GetBodyAccentColour))]
    internal static extern Color4 GetBodyAccentColour(PlaySliderBody instance, ISkinSource skin, Color4 hitObjectAccentColour);
}

[SuppressMessage("Style", "OFSG001", Justification = "Not a dependency injection candidate.")]
file abstract class SliderBodyAccessor : SliderBody
{
    [PrivateAccessor(PrivateAccessorKind.Field, Name = "path")]
    internal static extern void SetPath(SliderBody instance, DrawableSliderPath value);

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "path")]
    internal static extern DrawableSliderPath? GetPath(SliderBody instance);
}

[SuppressMessage("Style", "OFSG001", Justification = "Not a dependency injection candidate.")]
file abstract class LegacySliderBodyAccessor : LegacySliderBody
{
    [PrivateAccessor(PrivateAccessorKind.Method, Name = nameof(GetBorderColour))]
    internal static extern Color4 GetBorderColour(LegacySliderBody instance, ISkinSource skin);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = nameof(GetBodyAccentColour))]
    internal static extern Color4 GetBodyAccentColour(LegacySliderBody instance, ISkinSource skin, Color4 hitObjectAccentColour);
}