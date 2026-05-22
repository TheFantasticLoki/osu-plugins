// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Plugin.Patches;

/// <summary>
/// Settings control for managing slider style presets.
/// </summary>
public partial class PresetControls : CompositeDrawable
{
    public Bindable<SliderStylePreset?> CurrentPreset { get; set; } = new Bindable<SliderStylePreset?>();
    public Bindable<SliderStyle> Style { get; set; } = new Bindable<SliderStyle>();
    public BindableBool UseComboColors { get; set; } = new BindableBool();
    public Bindable<BorderStyle> BorderStyle { get; set; } = new Bindable<BorderStyle>();
    public BindableFloat BorderLightness { get; set; } = new BindableFloat();
    public BindableFloat BodyOpacity { get; set; } = new BindableFloat();

    private FillFlowContainer buttonsFlow = null!;

    public PresetControls()
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
        
        InternalChild = buttonsFlow = new FillFlowContainer
        {
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(4),
            AutoSizeAxes = Axes.Both,
        };
        
        CreateButtons();
    }

    private void CreateButtons()
    {
        buttonsFlow.Children = new Drawable[]
        {
            new FormButton
            {
                ButtonText = "Save Current as Preset",
                Action = SaveCurrentPreset
            },
            new FormButton
            {
                ButtonText = "Export to Clipboard",
                Action = ExportToClipboard
            },
            new FormButton
            {
                ButtonText = "Import from Text",
                Action = ImportFromText
            }
        };
    }

    private void SaveCurrentPreset()
    {
        var preset = new SliderStylePreset("Custom", Style.Value, UseComboColors.Value, BorderStyle.Value, BorderLightness.Value, BodyOpacity.Value);
        CurrentPreset.Value = preset;
    }

    private void ExportToClipboard()
    {
        var preset = new SliderStylePreset("Exported", Style.Value, UseComboColors.Value, BorderStyle.Value, BorderLightness.Value, BodyOpacity.Value);
        // Note: Actual clipboard access would need platform-specific code
        // For now, just log the string
        System.Console.WriteLine($"Exported preset: {preset.ToShareableString()}");
    }

    private void ImportFromText()
    {
        // For now, this requires manual input - would need a text input field
        // This is a placeholder for future implementation
    }
}