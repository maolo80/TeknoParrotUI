# Fix silent startup crash after an incremental build, and dropped controller bindings

## Build: skip missing files when moving output libraries
`AfterBuild` moves `netstandard.dll`, `Newtonsoft.Json.dll` and `RawInput.Sharp.dll` by explicit name. When one of them is not in the output folder (seen with Visual Studio 2026 on every rebuild), the first `Move` fails, so the CefSharp `Move` never runs. `App.config` then points at `libs/CefSharp` for assemblies that stayed in the output root, and the UI exits at startup with an unhandled `FileNotFoundException`, before the crash handler can show anything. Each item is now moved only when it exists.

## Controller setup: read the focused binding box from the active window
The XInput, DirectInput and RawInput binding listeners looked up the focused text box in `Application.Current.Windows[0]`. That is not always the main window: Visual Studio's XAML tooling and first-run windows can come first, and then every press was silently dropped. They now use the active window, or the main window when none is active (`Helpers/BindingFocus.cs`).

## Testing
- Windows 11, Visual Studio 2026, Release and Debug x86 builds: build succeeds with no errors, and the UI starts.
- Controller setup tested with XInput (Xbox Elite 2), DirectInput and RawInput, including a non-Naomi profile, in a Release build started outside the debugger. Before the fix, presses were dropped both under the Visual Studio debugger and on the first run of a fresh output folder.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01Xkm42Nb8k8qCUfB5RCiVU3
