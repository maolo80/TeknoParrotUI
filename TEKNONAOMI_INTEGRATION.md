# TeknoNaomi integration

The Windows release integration adds 349 exact-revision profiles and ROM/BIOS/CHD
metadata, 126 profiles with continuous controls, and 235 TPOnline room revisions.
Profiles use normal release visibility and subscriber flags. The updater and
ParrotPatcher install the numeric-versioned runtime at TeknoNaomi/TeknoNaomi.exe.

Select a ROM ZIP, then select CHD Root in the folder picker directly below it
for disk games. ROM Root is inferred from the ZIP directory. Old CHD settings
migrate, and renamed controls retain assignments by mapping ID. Audio and normal
pacing are always enabled; Mute Audio and Host Timing are removed from settings
and ignored when present in old saved profiles.

Runner options appear in every profile, including saved ones, without a
revision bump: Cabinet Outputs (`--outputs`, on by default, for MAMEOutput
lamps, motors and force feedback), Free Play (`--free-play`), BIOS
(`--bios N`, empty uses the runner default) and Precise SH-4 FPU
(`--sh4-precise-fpu`). Video adds Side-by-Side 3D (`--sbs`) and Enable VR
(`--vr`) with screen distance and width (0.25-8 m), head-locked screen, VR
filter and Touch controls. VR takes precedence over side-by-side. The runner
has no V-sync or presentation-filter option, so neither is offered.

Brave Firefighters has independent nozzle X/Y axes for both players. NASCAR
has wheel, gas and brake axes. Maraca, sword, rod/reel, trackball-speed, clutch
and pedal controls use continuous values; labelled digital alternatives remain
optional. This does not add physical relative-trackball acquisition.

Builds and software contract checks do not qualify physical controllers,
lightguns, live multiplayer, widescreen gameplay or audio playback. Those
acceptance checks remain paused. Protected releases must be built on the build
machine; the local native validation package uses ReleaseNoVMP.

The native runner acquires the selected DirectInput, XInput and RawInput devices.
TPUI exports exact saved bindings and bypasses its generic JVS/input listeners.
Use the updated native runner for zero-neutral stick-to-pedal bindings.

Validation: Release build; 2,749 release/profile checks, 2,582 exact online-room
and settings-preservation checks, and 85 compiled updater/real ZIP checks.
Tests are under Tests/TeknoNaomiRelease, Tests/TeknoNaomiOnline and
Tests/NativeArcadeUpdates. Tests use fixture profiles and never edit UserProfiles.
