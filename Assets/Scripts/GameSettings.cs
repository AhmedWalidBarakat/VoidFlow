using UnityEngine;

namespace VoidFlow
{
    // The player's settings, kept in the browser between visits. Sensitivity is in CS2's own
    // units (degrees turned per mouse count = 0.022 x sens), so a CS player can type in the
    // sensitivity they already use.
    public static class GameSettings
    {
        const string Prefix = "VoidFlow.set.";
        public const float CsYaw = 0.022f;

        static float Get(string key, float fallback) => PlayerPrefs.GetFloat(Prefix + key, fallback);
        static void Set(string key, float value) { PlayerPrefs.SetFloat(Prefix + key, value); PlayerPrefs.Save(); }
        static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) == 1;
        static void SetBool(string key, bool value) { PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0); PlayerPrefs.Save(); }

        // CS2 sensitivity (0.08 degrees a count was the game's own: about 3.6 in CS2)
        public static float CsSensitivity { get => Get("sens", 3.64f); set => Set("sens", Mathf.Clamp(value, 0.2f, 12f)); }
        public static float DegreesPerCount => CsSensitivity * CsYaw;

        // Horizontal field of view at 16:9 (CS2's is 106)
        public static float Fov { get => Get("fov", 106.26f); set => Set("fov", Mathf.Clamp(value, 80f, 130f)); }

        public static bool ShowKeys { get => GetBool("keys", true); set => SetBool("keys", value); }
        public static bool Ghost { get => GetBool("ghost", true); set => SetBool("ghost", value); }
        public static bool Splits { get => GetBool("splits", true); set => SetBool("splits", value); }
        public static bool Grading { get => GetBool("grade", true); set => SetBool("grade", value); }
        public static bool SpeedLines { get => GetBool("speedlines", true); set => SetBool("speedlines", value); }
        public static bool ShowFps { get => GetBool("fps", false); set => SetBool("fps", value); }
        // Lighting with depth: the sun's shadows, and the soft shade where surfaces meet
        // (ambient occlusion). Both on; off for a slower machine
        public static bool Shadows { get => GetBool("shadows", true); set => SetBool("shadows", value); }
        public static bool AmbientOcclusion { get => GetBool("ao", true); set => SetBool("ao", value); }

        // The 3D view's resolution as a share of the screen's (the HUD always stays sharp): 1 is
        // full sharpness, lower runs on slower machines. Only ever changed by the player.
        public static float RenderScale { get => Get("res", 1f); set => Set("res", Mathf.Clamp(value, 0.25f, 1f)); }

        // The crosshair's colour, one of a few
        public static readonly Color[] CrosshairColors =
        {
            Color.white, new(0.3f, 1f, 0.4f), new(0.3f, 0.95f, 1f), new(1f, 0.35f, 0.8f), new(1f, 0.85f, 0.2f),
        };
        public static readonly string[] CrosshairNames = { "WHITE", "GREEN", "CYAN", "PINK", "YELLOW" };
        public static int Crosshair { get => PlayerPrefs.GetInt(Prefix + "xhair", 0); set { PlayerPrefs.SetInt(Prefix + "xhair", (value % CrosshairColors.Length + CrosshairColors.Length) % CrosshairColors.Length); PlayerPrefs.Save(); } }
        public static Color CrosshairColor => CrosshairColors[Crosshair];
    }
}
