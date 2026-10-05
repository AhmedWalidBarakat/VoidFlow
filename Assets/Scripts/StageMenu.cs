using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // The stage menu (M): every stage you've reached, as a grid of tiles in each zone's colours;
    // click one to start a run from its checkpoint. Only stages reached for real count (not by
    // noclip), so it never takes you anywhere you haven't been. Below them, the bhop challenge's
    // ten stages: any of them, straight to its start pad (past the furthest you've reached it's
    // practice, with nothing won). Also the game's volume.
    public class StageMenu : MonoBehaviour
    {
        const string ReachedKey = "VoidFlow.reachedStage", VolumeKey = "VoidFlow.volume";
        const float DefaultVolume = 0.5f; // (the game was loud: half of what it was)

        RunTimer timer;
        EndlessCourse course;
        PlayerMovement player;
        int reached;
        float openTime = -99f, scroll, scrollTarget;
        int hovered = -1;
        bool dragging;

        public static bool IsOpen { get; private set; }

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
            set
            {
                value = Mathf.Clamp01(value);
                AudioListener.volume = value;
                PlayerPrefs.SetFloat(VolumeKey, value);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            AudioListener.volume = Volume;
            if (!FindAnyObjectByType<RunTimer>()) return;
            new GameObject("StageMenu").AddComponent<StageMenu>();
        }

        void Start()
        {
            timer = FindAnyObjectByType<RunTimer>();
            course = FindAnyObjectByType<EndlessCourse>();
            player = FindAnyObjectByType<PlayerMovement>();
            reached = Mathf.Max(PlayerPrefs.GetInt(ReachedKey, 0), PlayerPrefs.GetInt("VoidFlow.checkpointRamp", 0) / Mathf.Max(1, course.rampsPerBiome));
        }

        static float Scale() => Mathf.Clamp(Screen.height / 800f, 0.6f, 2.2f);

        void Update()
        {
            if (!course || !course.Ready) return;
            // A stage counts as reached once you're on it in a real run (not noclip practice)
            if (timer.Running && !timer.Practice && !player.Flying && course.CurrentStage > reached)
            {
                reached = course.CurrentStage;
                PlayerPrefs.SetInt(ReachedKey, reached);
                PlayerPrefs.Save();
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!IsOpen)
            {
                if (kb.mKey.wasPressedThisFrame && !ViewModel.InputBlocked && !Inventory.IsOpen) Open();
            }
            else if (kb.mKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) Close();
            if (IsOpen && Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            scroll = Mathf.Lerp(scroll, scrollTarget, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
        }

        void Open()
        {
            IsOpen = true;
            openTime = Time.unscaledTime;
            ViewModel.InputBlocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // Opened at the stage you're on
            int rowOf = Mathf.Max(0, course.CurrentStage) / Columns;
            scroll = scrollTarget = Mathf.Max(0f, rowOf * (TileH + Gap) - TileH);
        }

        void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ViewModel.InputBlocked = false;
            dragging = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        const int MaxColumns = 6;
        const float TileW = 150f, TileH = 74f, Gap = 10f;
        const float BhopTileH = 56f, BhopGap = 8f;
        // Each bhop stage's colour, from its room
        static readonly Color[] BhopHues =
        {
            new(1f, 0.78f, 0.4f), new(1f, 0.45f, 0.3f), new(1f, 0.55f, 0.35f), new(0.3f, 0.9f, 1f), new(0.85f, 0.87f, 0.92f),
            new(1f, 0.3f, 0.9f), new(0.6f, 0.95f, 1f), new(0.75f, 0.45f, 1f), new(1f, 0.45f, 0.15f), new(1f, 0.8f, 0.4f),
        };
        int Columns = MaxColumns; // fewer in a narrow window

        void OnGUI()
        {
            if (!IsOpen || !course) return;
            float s = Scale(), w = Screen.width / s, h = Screen.height / s;
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float t = Mathf.Clamp01((Time.unscaledTime - openTime) * 6f);
            var e = Event.current;

            GUI.color = new Color(0f, 0f, 0f, 0.6f * t);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            Columns = Mathf.Clamp(Mathf.FloorToInt((w - 40f - 48f + Gap) / (TileW + Gap)), 2, MaxColumns);
            float pw = Columns * (TileW + Gap) - Gap + 48f;
            // (the bhop challenge's stages along the bottom: one row of ten, or two of five)
            int bhopCols = pw - 48f >= 10 * 78f + 9 * BhopGap ? 10 : 5;
            float bhopH = 30f + (10 / bhopCols) * (BhopTileH + BhopGap);
            float ph = Mathf.Min(h - 80f, 620f + bhopH);
            var panel = new Rect((w - pw) / 2f, (h - ph) / 2f + (1f - t) * 30f, pw, ph);
            UiArt.Rounded(panel, new Color(0.07f, 0.06f, 0.1f, 0.96f * t), 18f);
            UiArt.Rounded(new Rect(panel.x, panel.y, panel.width, 4f), new Color(0.85f, 0.55f, 1f, t), 2f);
            UiArt.Text(new Rect(panel.x + 24, panel.y + 14, 400, 40), "STAGES", 30, new Color(1f, 1f, 1f, t));
            int total = course.FinalRamp / course.rampsPerBiome;
            UiArt.Text(new Rect(panel.x + 24, panel.y + 50, pw - 48, 22),
                Columns >= 5 ? $"you've reached stage {reached + 1} of {total}  ·  click a stage to start a run there  ·  M or Esc to close"
                    : $"reached stage {reached + 1} of {total}  ·  M or Esc to close", 14, new Color(0.8f, 0.78f, 0.9f, t), TextAnchor.MiddleLeft, false);

            // The tiles, scrolled
            var view = new Rect(panel.x + 24, panel.y + 86, pw - 48, ph - 86 - 70 - bhopH);
            int count = Mathf.Min(reached + 1, total);
            float content = Mathf.Ceil(count / (float)Columns) * (TileH + Gap);
            float maxScroll = Mathf.Max(0f, content - view.height);
            if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition))
            {
                scrollTarget = Mathf.Clamp(scrollTarget + e.delta.y * 22f, 0f, maxScroll);
                e.Use();
            }
            scrollTarget = Mathf.Clamp(scrollTarget, 0f, maxScroll);
            GUI.BeginGroup(view);
            int nowHover = -1;
            Vector2 mouse = e.mousePosition; // (group space)
            for (int i = 0; i < count; i++)
            {
                var r = new Rect((i % Columns) * (TileW + Gap), (i / Columns) * (TileH + Gap) - scroll, TileW, TileH);
                if (r.yMax < 0f || r.y > view.height) continue;
                var b = Biome.All[i];
                bool usable = i == 0 || course.IsCheckpoint(i * course.rampsPerBiome);
                bool over = usable && r.Contains(mouse) && mouse.y >= 0f && mouse.y <= view.height;
                if (over) nowHover = i;
                bool here = i == course.CurrentStage && timer.Running;
                Color glow = b.glow.maxColorComponent > 0.05f ? b.glow / b.glow.maxColorComponent : Color.white;
                UiArt.Rounded(r, new Color(0.13f, 0.12f, 0.17f, t) + (over ? new Color(0.08f, 0.08f, 0.1f, 0f) : Color.clear), 10f);
                if (here) UiArt.Rounded(r, new Color(glow.r, glow.g, glow.b, 0.9f * t), 10f, 2f);
                UiArt.Rounded(new Rect(r.x, r.y, 6f, r.height), new Color(glow.r, glow.g, glow.b, (usable ? 1f : 0.35f) * t), 3f);
                var text = new Color(1f, 1f, 1f, (usable ? 1f : 0.45f) * t);
                UiArt.Text(new Rect(r.x + 14, r.y + 6, r.width - 18, 26), i == 0 ? "1  START" : $"{i + 1}", 20, text);
                // your best time on it, if you've ridden it through
                float best = StageClock.Instance ? StageClock.Instance.BestFor(i) : 0f;
                if (best > 0f)
                    UiArt.Text(new Rect(r.x, r.y + 8, r.width - 10, 22), $"{(int)(best / 60f)}:{best % 60f:00.00}", 13, new Color(1f, 0.82f, 0.3f, t), TextAnchor.MiddleRight);
                UiArt.Text(new Rect(r.x + 14, r.y + 32, r.width - 18, 18), b.name, 12, text, TextAnchor.MiddleLeft, false);
                string tier = !usable ? "no checkpoint" : b.tier ?? (i >= Biome.LegendFrom ? "LEGEND" : i >= Biome.FinaleFrom ? "EXPERT" : EndlessCourse.TierNames[(int)EndlessCourse.TierOf(i)]);
                if (i == total - 1) tier = "FINALE";
                UiArt.Text(new Rect(r.x + 14, r.y + 50, r.width - 18, 16), tier, 11, new Color(glow.r, glow.g, glow.b, (usable ? 0.95f : 0.4f) * t), TextAnchor.MiddleLeft, false);
                if (over && e.type == EventType.MouseDown && e.button == 0)
                {
                    e.Use();
                    int stage = i;
                    GUI.EndGroup();
                    GUI.matrix = oldMatrix;
                    Close();
                    timer.StartAtStage(stage);
                    return;
                }
            }
            GUI.EndGroup();
            hovered = nowHover;

            // The bhop challenge's ten stages: click one to go to its start pad
            var bhop = BhopChallenge.Instance;
            if (bhop)
            {
                float inner = pw - 48f, y0 = view.yMax + 8f, bw = (inner - (bhopCols - 1) * BhopGap) / bhopCols;
                UiArt.Text(new Rect(panel.x + 24, y0, inner, 22), "BHOP CHALLENGE", 16, new Color(1f, 1f, 1f, t));
                UiArt.Text(new Rect(panel.x + 24 + 168, y0, inner - 168, 22), Columns >= 5 ? "click a stage to go there  ·  past the furthest you've reached it's practice (nothing won)" : "past your furthest: practice",
                    12, new Color(0.8f, 0.78f, 0.9f, t), TextAnchor.MiddleLeft, false);
                int furthest = Mathf.Max(1, BhopChallenge.Reached), here = BhopChallenge.Active ? bhop.Stage : 0;
                for (int i = 0; i < 10; i++)
                {
                    int n = i + 1;
                    var r = new Rect(panel.x + 24 + (i % bhopCols) * (bw + BhopGap), y0 + 28f + (i / bhopCols) * (BhopTileH + BhopGap), bw, BhopTileH);
                    bool ok = n <= furthest, over = r.Contains(e.mousePosition);
                    var c = BhopHues[i];
                    UiArt.Rounded(r, new Color(0.13f, 0.12f, 0.17f, t) + (over ? new Color(0.08f, 0.08f, 0.1f, 0f) : Color.clear), 10f);
                    if (n == here) UiArt.Rounded(r, new Color(c.r, c.g, c.b, 0.9f * t), 10f, 2f);
                    UiArt.Rounded(new Rect(r.x, r.y, 5f, r.height), new Color(c.r, c.g, c.b, (ok ? 1f : 0.4f) * t), 3f);
                    var text = new Color(1f, 1f, 1f, (ok ? 1f : 0.55f) * t);
                    UiArt.Text(new Rect(r.x + 12, r.y + 3, r.width - 14, 24), $"{n}", 18, text);
                    UiArt.Text(new Rect(r.x + 12, r.y + 26, r.width - 14, 14), BhopLayout.Stages[i].name, 11, text, TextAnchor.MiddleLeft, false);
                    UiArt.Text(new Rect(r.x + 12, r.y + 40, r.width - 14, 13), !ok ? "practice" : n == 1 ? "full run" : "carry on", 10, new Color(c.r, c.g, c.b, (ok ? 0.95f : 0.5f) * t), TextAnchor.MiddleLeft, false);
                    if (over && e.type == EventType.MouseDown && e.button == 0)
                    {
                        e.Use();
                        GUI.matrix = oldMatrix;
                        Close();
                        timer.EndRun(); // (a surf run in progress stops; the course goes back to its start)
                        bhop.GoToStage(n);
                        return;
                    }
                }
            }

            // Volume
            var bar = new Rect(panel.x + 140, panel.yMax - 42, pw - 140 - 90, 8);
            UiArt.Text(new Rect(panel.x + 24, panel.yMax - 54, 110, 32), "VOLUME", 16, new Color(1f, 1f, 1f, t));
            var grab = new Rect(bar.x - 8, bar.y - 12, bar.width + 16, bar.height + 24);
            if (e.type == EventType.MouseDown && e.button == 0 && grab.Contains(e.mousePosition)) dragging = true;
            if (e.type == EventType.MouseUp) dragging = false;
            if (dragging && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                Volume = (e.mousePosition.x - bar.x) / bar.width;
                e.Use();
            }
            float v = Volume;
            UiArt.Rounded(bar, new Color(0.25f, 0.22f, 0.32f, t), 4f);
            UiArt.Rounded(new Rect(bar.x, bar.y, bar.width * v, bar.height), new Color(0.85f, 0.55f, 1f, t), 4f);
            UiArt.Rounded(new Rect(bar.x + bar.width * v - 9f, bar.y - 6f, 18f, 20f), new Color(1f, 1f, 1f, t), 9f);
            UiArt.Text(new Rect(bar.xMax + 14, panel.yMax - 54, 70, 32), $"{Mathf.RoundToInt(v * 100f)}%", 16, new Color(1f, 1f, 1f, t));

            GUI.color = Color.white;
            GUI.matrix = oldMatrix;
        }
    }
}
