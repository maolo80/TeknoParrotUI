using System;
using System.Linq;

namespace TeknoParrotUi.Common.Jvs
{
    /// <summary>
    /// KanColle Arcade's helm and telegraph, as fc.exe 7.40 reads them from the JVS board (sub_1401BE5D0).
    ///
    /// The helm (舵輪) is an endless 8-bit rotary encoder in Gray code on the second player's switches: Up, Down,
    /// Left, Right, Button 3, 4, 5, 6 are code bits 7 to 0. The game turns the ship by how far the code moved since
    /// its last read (at most 20 steps a read) and moves menu cursors up/left for every 8 steps up, down/right for
    /// every 8 steps down, so a rising position is a turn to the left. A stick or keys therefore have to turn the
    /// encoder at a speed; a wheel can also give its position directly.
    ///
    /// The telegraph (速力) is analog channel 0, 16 bit: 7 notches from 原速 to 一杯, by default at 0xFFFF * n / 6
    /// (the test menu's TELEGRAPH SETTING can move them). Keys step one notch per press and the lever stays there.
    /// </summary>
    public static class KanColleControls
    {
        public static bool Enabled;

        private const int HelmAnalog = 2;
        private const int TelegraphAnalog = 0;
        private const int Notches = 7;
        private const double HelmDeadZone = 0.06;
        private const int TelegraphMoved = 6;

        private static bool _helmAxisBound;
        private static bool _telegraphAxisBound;
        private static bool _helmIsPosition;
        private static bool _invertHelm;
        private static bool _invertTelegraph;
        private static double _helmSpeed;

        private static double _helmSteps;
        private static int _notch;
        private static bool _telegraphFromAxis;
        private static int _lastTelegraphAxis;
        private static bool _speedUpHeld;
        private static bool _speedDownHeld;

        public static void Initialize(GameProfile gameProfile)
        {
            Enabled = gameProfile?.EmulationProfile == EmulationProfile.NuKanColle;
            _helmSteps = 0;
            _notch = 0;
            _telegraphFromAxis = false;
            _lastTelegraphAxis = -1;
            _speedUpHeld = false;
            _speedDownHeld = false;
            if (!Enabled)
                return;

            _helmAxisBound = IsBound(gameProfile, InputMapping.Analog0 + HelmAnalog);
            _telegraphAxisBound = IsBound(gameProfile, InputMapping.Analog0 + TelegraphAnalog);
            _helmIsPosition = Setting(gameProfile, "Helm Axis") == "Wheel position";
            _invertHelm = Setting(gameProfile, "Invert Helm") == "1";
            _invertTelegraph = Setting(gameProfile, "Invert Telegraph") == "1";
            _helmSpeed = int.TryParse(Setting(gameProfile, "Helm Speed"), out var speed) ? Math.Max(1, Math.Min(20, speed)) : 6;
            InputCode.AnalogBytes[HelmAnalog] = 0x80;
        }

        private static string Setting(GameProfile gameProfile, string name) =>
            gameProfile.ConfigValues?.FirstOrDefault(x => x.FieldName == name)?.FieldValue;

        private static bool IsBound(GameProfile gameProfile, InputMapping mapping) =>
            gameProfile.JoystickButtons?.Any(x => x.InputMapping == mapping &&
                x.AnalogType != AnalogType.Minimum && x.AnalogType != AnalogType.Maximum &&
                (x.DirectInputButton != null || x.XInputButton != null)) == true;

        private static bool Held(bool? button) => button.HasValue && button.Value;

        /// <summary>The helm's next Gray code put into the second player's switch bytes (one call per switch read).</summary>
        public static void AddHelm(ref byte player2, ref byte player2Ext)
        {
            var buttons = InputCode.PlayerDigitalButtons[0];
            double axis = _helmAxisBound ? (InputCode.AnalogBytes[HelmAnalog] - 128) / 127.0 : 0;
            axis = Math.Max(-1, Math.Min(1, axis));

            if (!_helmIsPosition && Math.Abs(axis) >= HelmDeadZone)
                _helmSteps -= axis * _helmSpeed;
            if (Held(buttons.ExtensionButton1_1))
                _helmSteps += _helmSpeed;
            if (Held(buttons.ExtensionButton1_2))
                _helmSteps -= _helmSpeed;
            _helmSteps = ((_helmSteps % 256) + 256) % 256;

            double position = _helmSteps;
            if (_helmIsPosition)
                position -= axis * 127;
            if (_invertHelm)
                position = -position;

            int code = (((int)Math.Round(position) % 256) + 256) % 256;
            int gray = code ^ (code >> 1);
            player2 = (byte)((player2 & ~0x3C) | ((gray >> 2) & 0x3C));
            player2Ext = (byte)((player2Ext & 0x0F) | ((gray << 4) & 0xF0));
        }

        /// <summary>Analog channel 0, high byte first.</summary>
        public static ushort Telegraph()
        {
            var buttons = InputCode.PlayerDigitalButtons[0];
            int axis = InputCode.AnalogBytes[TelegraphAnalog];
            if (_invertTelegraph)
                axis = 255 - axis;

            if (_telegraphAxisBound)
            {
                if (_lastTelegraphAxis < 0)
                    _lastTelegraphAxis = axis;
                if (Math.Abs(axis - _lastTelegraphAxis) > TelegraphMoved)
                    _telegraphFromAxis = true;
                if (_telegraphFromAxis)
                    _lastTelegraphAxis = axis;
            }

            bool up = Held(buttons.ExtensionButton1_5);
            bool down = Held(buttons.ExtensionButton1_6);
            if ((up && !_speedUpHeld) || (down && !_speedDownHeld))
            {
                if (_telegraphFromAxis)
                    _notch = (int)Math.Round(axis * (Notches - 1) / 255.0);
                _notch = Math.Max(0, Math.Min(Notches - 1, _notch + (up && !_speedUpHeld ? 1 : 0) - (down && !_speedDownHeld ? 1 : 0)));
                _telegraphFromAxis = false;
            }
            _speedUpHeld = up;
            _speedDownHeld = down;

            return _telegraphFromAxis ? (ushort)(axis * 257) : (ushort)(0xFFFF * _notch / (Notches - 1));
        }
    }
}
