// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Threading;
using osu.Game;

namespace osu.Plugin.Patches;

internal interface ISkinSettings
{
    void Load(OsuGame game, Scheduler scheduler) { }

    IEnumerable<Drawable> CreateSettingsControls() => Array.Empty<Drawable>();
}