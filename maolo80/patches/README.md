# TeknoParrotUI patches (maolo80)

Two independent sets of changes for TeknoParrotUI, made against upstream commit
`b75872a` ("Freedom for TeknoMVS", 22 Oct 2026). They were meant as two pull
requests, but github.com/teknogods/TeknoParrotUI is no longer public.

- `1-tpui-fixes/` (2 patches): general fixes. They touch only the TeknoParrotUi project.
- `2-teknonaomi-options/` (3 patches): TeknoNaomi settings. They touch TeknoParrotUi.Common and the TeknoNaomi tests.

Each set applies on its own; both together apply in either order. The same
commits are also on GitHub:
https://github.com/maolo80/TeknoParrotUI/tree/pr/tpui-fixes and
https://github.com/maolo80/TeknoParrotUI/tree/pr/teknonaomi-options

## Applying

From a TeknoParrotUI checkout at (or near) `b75872a`:

    git am 1-tpui-fixes/*.patch
    git am 2-teknonaomi-options/*.patch

`git am` keeps the authors and commit messages. To review without committing,
use `git apply --stat` or `git apply --check` on the files first.

---

# 1. Fix silent startup crash after an incremental build, and dropped controller bindings


## Build: skip missing files when moving output libraries
`AfterBuild` moves `netstandard.dll`, `Newtonsoft.Json.dll` and `RawInput.Sharp.dll` by explicit name. When one of them is not in the output folder (seen with Visual Studio 2026 on every rebuild), the first `Move` fails, so the CefSharp `Move` never runs. `App.config` then points at `libs/CefSharp` for assemblies that stayed in the output root, and the UI exits at startup with an unhandled `FileNotFoundException`, before the crash handler can show anything. Each item is now moved only when it exists.

## Controller setup: read the focused binding box from the active window
The XInput, DirectInput and RawInput binding listeners looked up the focused text box in `Application.Current.Windows[0]`. That is not always the main window: Visual Studio's XAML tooling and first-run windows can come first, and then every press was silently dropped. They now use the active window, or the main window when none is active (`Helpers/BindingFocus.cs`).

## Testing
- Windows 11, Visual Studio 2026, Release and Debug x86 builds: build succeeds with no errors, and the UI starts.
- Controller setup tested with XInput (Xbox Elite 2), DirectInput and RawInput, including a non-Naomi profile, in a Release build started outside the debugger. Before the fix, presses were dropped both under the Visual Studio debugger and on the first run of a fresh output folder.


---

# 2. TeknoNaomi: expose existing runner options and add Auto internal resolution


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


---

# Feature request for the TeknoNaomi runner

1. `--vsync` / `--no-vsync`, like TeknoS23 and TeknoViper/Vegas. Today the only way to remove tearing is to force V-sync in the NVIDIA control panel (which works).
2. A presentation filter option (`linear` / `bicubic` / area downsample). With `presentation_filter=nearest`, high render scales alias and shimmer: at 8x on 2160p the GPU needs only about 3.5 ms per scene (`--vulkan-profile`), but the image looks worse than at 4x.
3. Precompiled or cached Vulkan PVR materials. Runtime `Vulkan compiling PVR material` events cause hitches: the longest PVR worker job was 36 ms, and `pvr_wait_next_render` showed 81 waits of about 10 ms in 160 s.

Test system: Windows 11, RTX 5070 Ti, Ryzen 9 5950X, 3840x2160, TeknoNaomi 0.1.0.10. The full `--vulkan-profile` log is available on request.
