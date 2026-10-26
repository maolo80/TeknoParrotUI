# TeknoNaomi: expose existing runner options and add Auto internal resolution

TeknoNaomi.exe already accepts these options, but TPUI offered none of them. This adds them as profile settings and passes them at launch.

| Setting | Runner option | Default |
|---|---|---|
| Cabinet Outputs (General) | `--outputs` (MAMEOutput lamps, motors, force feedback) | on |
| Free Play (General) | `--free-play` | off |
| BIOS (General) | `--bios N`, empty uses the runner default | empty |
| Precise SH-4 FPU (General) | `--sh4-precise-fpu` | off |
| Side-by-Side 3D (Video) | `--sbs` | off |
| Enable VR (Video) | `--vr`, `--vr-screen-distance`, `--vr-screen-width`, `--vr-head-locked`, `--vr-filter`, `--no-vr-controls` | off |
| Internal Resolution: Auto (Video) | `--render-scale` from the primary display height | default stays 4 |

- **Fields come from `Normalize`:** `TeknoNaomiProfileSettings.Normalize` adds the fields, so the 349 stock XMLs and saved profiles gain them without a revision bump.
- **Revision templates:** revision templates get the same fields in `ArcadeGameRevisions.Stock`. Otherwise `MergeSettings`, which copies only fields the template has, drops the saved values at launch for every game with a Game Revision selector.
- **VR and side-by-side:** VR takes precedence over side-by-side.
- **Validation:** invalid BIOS, VR size and VR filter values are rejected with a message.
- **Auto resolution:** the runner reduces its raster with nearest sampling (`presentation_filter=nearest`), so a scale above the display only discards rows: 8x on a 2160p screen looks worse than 4x. Auto picks the largest scale whose 480 source lines fit the primary display's current mode (physical pixels via `EnumDisplaySettings`, as `MonitorSelection` does), falling back to 4. The launch log names the scale used.

## Testing
- `Tests/TeknoNaomiRelease`: 4410 checks. New checks cover defaults, each option, VR precedence, invalid values, and Auto at 300–4320 lines plus an unknown display.
- `Tests/TeknoNaomiOnline`: 2817 checks. Runner options saved on a parent profile must reach every resolved revision's launch plan. This check fails before the revision fix.
- Manual tests on Windows 11 with an RTX 5070 Ti, a Ryzen 9 5950X and a 3840×2160 display: Crazy Taxi, Free Play, BIOS, side-by-side, Auto, plus a GD-ROM game and a vertical game.
- **Not tested:** VR (no headset) and force feedback outputs (no wheel).

## Notes
A profile saved with `Internal Resolution = Auto` fails validation in an older TPUI until a number is selected again.

The runner has no V-sync or presentation-filter option, so neither is offered here.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01Xkm42Nb8k8qCUfB5RCiVU3
