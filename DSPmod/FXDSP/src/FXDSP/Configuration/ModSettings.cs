using BepInEx.Configuration;
using UnityEngine;

namespace FXDSP.Configuration
{
    internal sealed class ModSettings
    {
        public ConfigEntry<bool> VerboseLogging { get; private set; }
        public ConfigEntry<KeyboardShortcut> WindowKey { get; private set; }
        public ConfigEntry<int> PositionHudMode { get; private set; }
        public ConfigEntry<bool> BypassAbnormalityChecks { get; private set; }

        public ModSettings(ConfigFile config)
        {
            BypassAbnormalityChecks = config.Bind("Compatibility", "BypassAbnormalityChecks", true,
                "Bypass local abnormality checks while FXDSP is loaded. Existing abnormality records are retained. Sandbox and account eligibility checks remain unchanged. Disable to restore native checks.");
            VerboseLogging = config.Bind(
                "Development",
                "VerboseLogging",
                false,
                "Enable additional development logs. Changes apply immediately.");
            WindowKey = config.Bind("Controls", "WindowKey", new KeyboardShortcut(KeyCode.F8),
                "Open or close the FXDSP market window. Default: F8.");
            PositionHudMode = config.Bind("Interface", "PositionHudMode", 0,
                "Holdings panel: 0 visible, 2 hidden. Legacy value 1 also means visible; restore tab stays available.");
        }
    }
}
