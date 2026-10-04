using UnityEngine;

namespace VoidFlow
{
    // The settings, whenever the mouse is free (Esc, or before you first click in): a card over
    // the dimmed game with CS2-style sensitivity, field of view, volume and the HUD's options,
    // all kept in the browser. Click anywhere outside the card (or RESUME) to play.
    public class PauseMenu : MonoBehaviour
    {
        public static bool Showing { get; private set; }
        public static bool PointerOverCard { get; private set; }

        EndlessCourse course;
        Rect card;
        int dragging = -1;
        float shownAt;
        bool everLocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if (!FindAnyObjectByType<RunTimer>()) return;
            new GameObject("PauseMenu").AddComponent<PauseMenu>();
        }

        void Start() => course = FindAnyObjectByType<EndlessCourse>();

        void Update()
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (locked) everLocked = true;
            bool show = !locked && course && course.Ready && !Inventory.IsOpen && !StageMenu.IsOpen && !ViewModel.InputBlocked;
            if (show && !Showing) shownAt = Time.unscaledTime;
            Showing = show;
            if (!show) { PointerOverCard = false; dragging = -1; }
        }

        void OnGUI()
        {
            if (!Showing) return;
            GUI.depth = -20;
            float w = Screen.width, h = Screen.height, px = Mathf.Clamp(h / 1080f, 0.6f, 2f);
            float a = Mathf.Clamp01((Time.unscaledTime - shownAt) * 6f);
            var e = Event.current;
            Vector2 mouse = e.mousePosition;

            UiArt.Rounded(new Rect(0f, 0f, w, h), new Color(0.01f, 0f, 0.03f, 0.55f * a), 0f);
            float cw = 560f * px, ch = 610f * px;
            card = new Rect((w - cw) * 0.5f, (h - ch) * 0.5f, cw, ch);
            PointerOverCard = card.Contains(mouse);
            UiArt.Rounded(card, new Color(0.05f, 0.04f, 0.09f, 0.94f * a), 18f * px);
            UiArt.Rounded(new Rect(card.x, card.y, card.width, 4f * px), new Color(0.72f, 0.42f, 1f, a), 2f * px);

            float x = card.x + 32f * px, iw = card.width - 64f * px, y = card.y + 24f * px;
            UiArt.Text(new Rect(x, y, iw, 54f * px), "VOIDFLOW", Mathf.RoundToInt(44 * px), new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
            y += 52f * px;
            UiArt.Text(new Rect(x, y, iw, 22f * px), everLocked ? "PAUSED" : "CLICK ANYWHERE TO PLAY", Mathf.RoundToInt(13 * px), new Color(0.78f, 0.62f, 1f, a), TextAnchor.MiddleCenter);
            y += 40f * px;

            float row = 44f * px;
            // (saved only when they change, not on every pass)
            float sens = GameSettings.CsSensitivity, fov = GameSettings.Fov, vol = StageMenu.Volume;
            float ns = Slider(0, new Rect(x, y, iw, row), "SENSITIVITY  (CS2)", sens, 0.2f, 8f, v => v.ToString("0.00"), e, a, 0.01f); y += row;
            float nf = Slider(1, new Rect(x, y, iw, row), "FIELD OF VIEW", fov, 80f, 130f, v => $"{v:0}°", e, a, 1f); y += row;
            // (the 3D view's sharpness: 100% full, lower for a slower machine; the HUD stays sharp)
            float res = GameSettings.RenderScale * 100f;
            float nr = Slider(3, new Rect(x, y, iw, row), "3D RESOLUTION", res, 25f, 100f, v => $"{v:0}%", e, a, 5f); y += row;
            float nv = Slider(2, new Rect(x, y, iw, row), "VOLUME", vol, 0f, 1f, v => $"{v * 100f:0}%", e, a, 0.01f); y += row + 8f * px;
            if (ns != sens) GameSettings.CsSensitivity = ns;
            if (nf != fov) GameSettings.Fov = nf;
            if (nr != res) GameSettings.RenderScale = nr / 100f;
            if (nv != vol) StageMenu.Volume = nv;

            float tw = (iw - 12f * px) / 2f, th = 38f * px;
            if (Toggle(new Rect(x, y, tw, th), "SHOW KEYS", GameSettings.ShowKeys, e, a)) GameSettings.ShowKeys = !GameSettings.ShowKeys;
            if (Toggle(new Rect(x + tw + 12f * px, y, tw, th), "GHOST OF YOUR BEST", GameSettings.Ghost, e, a)) GameSettings.Ghost = !GameSettings.Ghost;
            y += th + 10f * px;
            if (Toggle(new Rect(x, y, tw, th), "STAGE SPLITS", GameSettings.Splits, e, a)) GameSettings.Splits = !GameSettings.Splits;
            if (Toggle(new Rect(x + tw + 12f * px, y, tw, th), "COLOUR GRADING", GameSettings.Grading, e, a)) GameSettings.Grading = !GameSettings.Grading;
            y += th + 10f * px;
            if (Toggle(new Rect(x, y, tw, th), "SPEED LINES", GameSettings.SpeedLines, e, a)) GameSettings.SpeedLines = !GameSettings.SpeedLines;
            if (Toggle(new Rect(x + tw + 12f * px, y, tw, th), "SHOW FPS", GameSettings.ShowFps, e, a)) GameSettings.ShowFps = !GameSettings.ShowFps;
            y += th + 10f * px;
            var xr = new Rect(x, y, iw, th);
            bool overX = xr.Contains(mouse);
            UiArt.Rounded(xr, new Color(1f, 1f, 1f, (overX ? 0.12f : 0.06f) * a), 10f * px);
            UiArt.Text(new Rect(xr.x + 14f * px, xr.y, xr.width, xr.height), "CROSSHAIR  ·  " + GameSettings.CrosshairNames[GameSettings.Crosshair], Mathf.RoundToInt(13 * px), new Color(1f, 1f, 1f, a));
            UiArt.Rounded(new Rect(xr.xMax - 30f * px, xr.center.y - 7f * px, 14f * px, 14f * px), GameSettings.CrosshairColor * new Color(1f, 1f, 1f, a), 7f * px);
            if (overX && e.type == EventType.MouseDown && e.button == 0) { GameSettings.Crosshair++; e.Use(); }
            y += th + 22f * px;

            var resume = new Rect(card.center.x - 110f * px, y, 220f * px, 46f * px);
            bool overR = resume.Contains(mouse);
            UiArt.Rounded(resume, new Color(0.72f, 0.42f, 1f, (overR ? 1f : 0.85f) * a), 23f * px);
            UiArt.Text(resume, everLocked ? "RESUME" : "PLAY", Mathf.RoundToInt(17 * px), new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
            if (overR && e.type == EventType.MouseDown && e.button == 0) { PointerOverCard = false; Lock(); e.Use(); }
            UiArt.Text(new Rect(card.x, card.yMax - 34f * px, card.width, 22f * px), "click outside this card to play   ·   ESC to come back here", Mathf.RoundToInt(11 * px), new Color(1f, 1f, 1f, 0.5f * a), TextAnchor.MiddleCenter);
        }

        static void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        float Slider(int id, Rect r, string label, float value, float min, float max, System.Func<float, string> show, Event e, float a, float step)
        {
            float px = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
            UiArt.Text(new Rect(r.x, r.y, r.width * 0.45f, r.height), label, Mathf.RoundToInt(13 * px), new Color(0.85f, 0.85f, 0.92f, a));
            var track = new Rect(r.x + r.width * 0.45f, r.center.y - 3f * px, r.width * 0.4f, 6f * px);
            var hit = new Rect(track.x - 8f * px, r.y, track.width + 16f * px, r.height);
            float t = Mathf.InverseLerp(min, max, value);
            if (e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition)) { dragging = id; e.Use(); }
            if (e.type == EventType.MouseUp) dragging = -1;
            if (dragging == id && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown || e.type == EventType.Used))
            {
                t = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width);
                value = Mathf.Round(Mathf.Lerp(min, max, t) / step) * step;
                t = Mathf.InverseLerp(min, max, value);
            }
            // mouse wheel over a slider nudges it a step at a time
            if (e.type == EventType.ScrollWheel && hit.Contains(e.mousePosition))
            {
                value = Mathf.Clamp(value - Mathf.Sign(e.delta.y) * step * (step < 0.05f ? 5f : 1f), min, max);
                t = Mathf.InverseLerp(min, max, value);
                e.Use();
            }
            UiArt.Rounded(track, new Color(1f, 1f, 1f, 0.12f * a), 3f * px);
            UiArt.Rounded(new Rect(track.x, track.y, track.width * t, track.height), new Color(0.72f, 0.42f, 1f, a), 3f * px);
            float k = 16f * px;
            UiArt.Rounded(new Rect(track.x + track.width * t - k * 0.5f, track.center.y - k * 0.5f, k, k), new Color(1f, 1f, 1f, a), k * 0.5f);
            UiArt.Text(new Rect(track.xMax + 12f * px, r.y, r.xMax - track.xMax - 12f * px, r.height), show(value), Mathf.RoundToInt(14 * px), new Color(1f, 1f, 1f, a), TextAnchor.MiddleRight);
            return value;
        }

        static bool Toggle(Rect r, string label, bool on, Event e, float a)
        {
            float px = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
            bool over = r.Contains(e.mousePosition);
            UiArt.Rounded(r, new Color(1f, 1f, 1f, (over ? 0.12f : 0.06f) * a), 10f * px);
            UiArt.Text(new Rect(r.x + 14f * px, r.y, r.width - 70f * px, r.height), label, Mathf.RoundToInt(13 * px), new Color(1f, 1f, 1f, a));
            var sw = new Rect(r.xMax - 52f * px, r.center.y - 10f * px, 38f * px, 20f * px);
            UiArt.Rounded(sw, on ? new Color(0.72f, 0.42f, 1f, a) : new Color(1f, 1f, 1f, 0.18f * a), 10f * px);
            float k = 16f * px;
            UiArt.Rounded(new Rect(on ? sw.xMax - k - 2f * px : sw.x + 2f * px, sw.center.y - k * 0.5f, k, k), new Color(1f, 1f, 1f, a), k * 0.5f);
            if (over && e.type == EventType.MouseDown && e.button == 0) { e.Use(); return true; }
            return false;
        }
    }
}
