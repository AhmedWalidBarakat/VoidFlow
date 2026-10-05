using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // The 10 stage bhop challenge behind the start hall (laid out by BhopLayout, built by
    // GrayboxBuilder.Bhop): its checkpoints, falls, clocks, HUD and prize. Every stage is a room;
    // its start pad is a checkpoint: miss (touch the room's floor, or drop well below the blocks
    // round you) and you're back on it (T does the same, R goes back to stage 1). Land on a
    // room's exit pad and its portal takes you to the next room's start pad. The run's clock
    // starts as you leave stage 1's pad. Finish stage 10 and every karambit and the Void gloves
    // painted to match each are yours, the Karambit | Velocity set (only won here) among them and
    // equipped (not with noclip: it's locked out here anyway, so a double tap of jump can't
    // switch it on mid-hop).
    public class BhopChallenge : MonoBehaviour
    {
        public static BhopChallenge Instance { get; private set; }
        // The player is out here: the surf run's own rules (its start, falls, R and T) stand aside.
        // (Worked out from where they are now, not last frame: whichever runs first must agree)
        public static bool Active => Instance && Instance.player && Instance.area.Contains(Instance.Local(Instance.player.Position));

        // The set on show at the plaza and equipped at the finish; the prize is every karambit and its gloves (Prizes)
        public const string PrizeKnife = "Karambit | Velocity", PrizeGloves = "Void Gloves | Velocity";
        const string ReachedKey = "VoidFlow.bhop.reached", BestKey = "VoidFlow.bhop.best", WonKey = "VoidFlow.bhop.won";
        public const int StageCount = 10;

        public BhopPiece[] pieces;
        public float[] headings;   // each stage's way off its pad
        public BhopRoom[] rooms;   // each stage's room
        public Bounds area;        // the challenge's room, in its own (the hall's) space
        public Vector3 portal;     // on the finish pad: back to the plaza
        public PlayerMovement player;

        int[] starts, exits;       // pieces index of stage s's start pad and its exit pad (stage 10's: the finish)
        int stage;                 // 0 the trail and plaza, 1..10 on a stage, 11 finished
        bool inside, timing, practice, fullRun;
        float runStart, stageStart, finishedAt = -99f, bannerAt = -99f, noteAt = -99f;
        float runTime;
        string banner, bannerLine, note;
        bool won;

        void Awake()
        {
            Instance = this;
            BhopLayout.Pads(pieces, out starts, out exits);
        }

        void Start()
        {
            if (!player) player = FindAnyObjectByType<PlayerMovement>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        Vector3 World(Vector3 local) => transform.TransformPoint(local);
        Vector3 Local(Vector3 world) => transform.InverseTransformPoint(world);
        public Vector3 PadOf(int s) => s > StageCount ? pieces[exits[StageCount]].center : pieces[starts[Mathf.Max(s, 1)]].center;
        public static int Reached => PlayerPrefs.GetInt(ReachedKey, 0);
        public static float Best => PlayerPrefs.GetFloat(BestKey, 0f);
        public static bool Won => PlayerPrefs.GetInt(WonKey, 0) == 1;

        static bool Over(in BhopPiece p, Vector3 q, float margin = 0.4f)
        {
            var d = Quaternion.Euler(0f, -p.yaw, 0f) * new Vector3(q.x - p.center.x, 0f, q.z - p.center.z);
            if (p.kind == BhopKind.Plaza) return new Vector2(q.x - p.center.x, q.z - p.center.z).magnitude < p.size.x * 0.5f + margin;
            return Mathf.Abs(d.z) < p.size.x * 0.5f + margin && Mathf.Abs(d.x) < p.size.y * 0.5f + margin;
        }

        bool StandingOn(int index, Vector3 q) => player.Grounded && Over(pieces[index], q) && Mathf.Abs(q.y - pieces[index].center.y) < 0.6f;
        // (a portal: anywhere over its pad, landing or just above it)
        bool Through(int index, Vector3 q) => Over(pieces[index], q, 0.1f) && q.y > pieces[index].center.y - 0.4f && q.y < pieces[index].center.y + 2.4f;

        void Update()
        {
            if (!player || starts == null) return;
            Vector3 q = Local(player.Position);
            bool was = inside;
            inside = area.Contains(q);
            if (!inside)
            {
                if (was) { timing = false; stage = 0; }
                return;
            }
            if (!was) { stage = 0; practice = player.Flying; }
            if (player.Flying) practice = true;

            var kb = Keyboard.current;
            bool keys = kb != null && !ViewModel.InputBlocked && !Inventory.IsOpen;
            if (keys && kb.rKey.wasPressedThisFrame) { Restart(); return; }
            if (keys && kb.tKey.wasPressedThisFrame && stage >= 1 && stage <= StageCount) { Respawn("STAGE RESTARTED"); return; }
            if (keys && kb.cKey.wasPressedThisFrame && stage <= 1 && !timing && Reached > 1)
            {
                stage = Mathf.Min(Reached, StageCount);
                fullRun = false;
                timing = true;
                runStart = stageStart = Time.time;
                Respawn($"CARRYING ON FROM STAGE {stage}");
                Banner();
                return;
            }

            if (player.Flying) return;

            // Onto stage 1's pad from the plaza; through a room's exit portal to the next room
            if (stage == 0 && StandingOn(starts[1], q))
            {
                stage = 1;
                timing = false;
                practice = false;
                fullRun = true;
                Banner();
            }
            else if (stage == StageCount && StandingOn(exits[StageCount], q)) Finish();
            else if (stage >= 1 && stage < StageCount && Through(exits[stage], q))
            {
                stage++;
                stageStart = Time.time;
                if (!practice && stage > Reached) { PlayerPrefs.SetInt(ReachedKey, stage); PlayerPrefs.Save(); }
                player.Teleport(World(PadOf(stage) + Vector3.up * 0.05f), headings[stage - 1] + transform.eulerAngles.y);
                Banner();
                return;
            }
            // Leaving stage 1's pad starts the clock
            if (stage == 1 && !timing && !Over(pieces[starts[1]], q, 0.2f) && !OverPlaza(q))
            {
                timing = true;
                runStart = stageStart = Time.time;
            }
            if (stage == StageCount + 1 && Vector2.Distance(new Vector2(q.x, q.z), new Vector2(portal.x, portal.z)) < 1.6f && Mathf.Abs(q.y - portal.y) < 2f)
            {
                stage = 0;
                timing = false;
                player.Teleport(World(PadOf(1) + Vector3.up * 0.05f), headings[0]);
                Note("BACK TO THE PLAZA");
                return;
            }
            if (Fallen(q)) Respawn(null);
        }

        bool OverPlaza(Vector3 q)
        {
            foreach (var p in pieces) if (p.kind == BhopKind.Plaza && Over(p, q)) return true;
            return false;
        }

        // Down on the room's floor, or below the pieces round you by more than a body's height: a miss
        bool Fallen(Vector3 q)
        {
            if (q.y < -260f) return true;
            int s = Mathf.Clamp(stage, 0, StageCount + 1);
            if (s >= 1 && s <= StageCount && rooms != null && q.y < BhopLayout.FloorOf(rooms, s) + 0.8f) return true;
            float low = float.MaxValue, near = float.MaxValue;
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                bool mine = s == 0 ? p.stage == 0 || i == starts[1]
                    : s > StageCount ? i == exits[StageCount]
                    : p.stage == s;
                if (!mine || p.kind == BhopKind.Again) continue;
                float top = p.kind == BhopKind.Ramp ? p.line[p.line.Length - 1].y - 3f : p.center.y;
                low = Mathf.Min(low, top);
                Vector3 at = p.kind == BhopKind.Ramp ? p.line[p.line.Length / 2] : p.center;
                if (new Vector2(at.x - q.x, at.z - q.z).sqrMagnitude < 30f * 30f) near = Mathf.Min(near, top);
            }
            if (near == float.MaxValue) near = low;
            return q.y < near - 4f;
        }

        void Respawn(string why)
        {
            if (stage == 0)
            {
                player.Teleport(World(BhopLayout.Terrace + Vector3.up * 0.05f), 180f);
                return;
            }
            int s = Mathf.Min(stage, StageCount + 1);
            float heading = s <= StageCount ? headings[s - 1] : headings[StageCount - 1];
            player.Teleport(World(PadOf(s) + Vector3.up * 0.05f), heading + transform.eulerAngles.y);
            if (s <= StageCount) stageStart = Time.time;
            Note(why ?? (s == 1 && !timing ? "STAGE 1" : $"STAGE {s}  ·  FROM THE PAD"));
        }

        void Restart()
        {
            stage = 1;
            timing = false;
            practice = player.Flying;
            fullRun = true;
            player.Teleport(World(PadOf(1) + Vector3.up * 0.05f), headings[0] + transform.eulerAngles.y);
            Note("BACK TO STAGE 1");
        }

        // The stage the player is on out here (0 the trail and plaza, 11 finished)
        public int Stage => inside ? stage : 0;

        // From the stage menu: onto a stage's start pad. Stage 1 is a full run (the clock starts
        // as you hop off); up to the furthest stage you've reached it's like carrying on (C);
        // further on it's practice, with nothing won
        public void GoToStage(int s)
        {
            s = Mathf.Clamp(s, 1, StageCount);
            inside = true; // (so arriving doesn't count as coming in from the hall)
            stage = s;
            practice = s > Mathf.Max(1, Reached);
            fullRun = s == 1;
            timing = s > 1;
            runStart = stageStart = Time.time;
            Respawn(s == 1 ? "STAGE 1" : practice ? $"PRACTICE  ·  STAGE {s}  ·  NOTHING WON" : $"CARRYING ON FROM STAGE {s}");
            Banner();
        }

        void Finish()
        {
            stage = StageCount + 1;
            timing = false;
            runTime = Time.time - runStart;
            finishedAt = Time.time;
            won = false;
            if (practice) return;
            if (fullRun && (Best <= 0f || runTime < Best)) PlayerPrefs.SetFloat(BestKey, runTime);
            PlayerPrefs.SetInt(WonKey, 1);
            PlayerPrefs.Save();
            won = true;
            foreach (var (slot, index) in Prizes())
                if (!Inventory.Owns(slot, index)) Inventory.Add(slot, index);
            int knife = Skins.IndexOf(ItemSlot.Secondary, PrizeKnife), gloves = Skins.IndexOf(ItemSlot.Hands, PrizeGloves);
            var view = FindAnyObjectByType<ViewModel>();
            if (view)
            {
                if (knife >= 0) view.EquipSkin(ItemSlot.Secondary, knife);
                if (gloves >= 0) view.EquipSkin(ItemSlot.Hands, gloves);
            }
            FxLibrary.Celebrate(player.Position + Vector3.up * 1.5f, new Color(0.3f, 0.9f, 1f), true);
        }

        // The prize: every karambit, and the Void gloves painted to match each
        public static System.Collections.Generic.IEnumerable<(ItemSlot slot, int index)> Prizes()
        {
            for (int i = 0; i < Skins.Knives.Length; i++)
                if (Skins.IsKarambit(Skins.Knives[i].asset)) yield return (ItemSlot.Secondary, i);
            for (int i = 0; i < Skins.Gloves.Length; i++)
                if (KarambitPaints.HasGlove(Skins.Gloves[i].paint)) yield return (ItemSlot.Hands, i);
        }

        static int KarambitCount()
        {
            int n = 0;
            foreach (var (slot, _) in Prizes()) if (slot == ItemSlot.Secondary) n++;
            return n;
        }

        void Banner()
        {
            int s = Mathf.Clamp(stage, 1, StageCount);
            banner = $"STAGE {s}  ·  {BhopLayout.Stages[s - 1].name.ToUpper()}";
            bannerLine = BhopLayout.Stages[s - 1].line;
            bannerAt = Time.time;
        }

        void Note(string text) { note = text; noteAt = Time.time; }

        static string Clock(float t) => $"{(int)(t / 60f)}:{t % 60f:00.00}";

        // ------------------------------------------------------------------ HUD

        static readonly Color Panel = new(0.04f, 0.035f, 0.07f, 0.62f), Cyan = new(0.3f, 0.9f, 1f), Soft = new(0.78f, 0.78f, 0.86f), Gold = new(1f, 0.82f, 0.3f);

        void OnGUI()
        {
            if (!inside || PauseMenu.Showing || Inventory.IsOpen || StageMenu.IsOpen) return;
            float w = Screen.width, h = Screen.height, px = Mathf.Clamp(h / 1080f, 0.6f, 2f);
            int fs(float size) => Mathf.RoundToInt(size * px);

            if (stage >= 1 && stage <= StageCount && (timing || stage > 1))
            {
                float pw = 420f * px, ph = 92f * px, x = (w - pw) * 0.5f, y = 14f * px;
                UiArt.Rounded(new Rect(x, y, pw, ph), Panel, 14f * px);
                UiArt.Pill(new Rect(x + 14f * px, y + 10f * px, 150f * px, 22f * px), $"BHOP  ·  STAGE {stage} / {StageCount}", fs(12), new Color(Cyan.r * 0.6f, Cyan.g * 0.6f, Cyan.b * 0.8f, 0.95f), Color.white);
                UiArt.Text(new Rect(x + 174f * px, y + 10f * px, pw - 188f * px, 22f * px), BhopLayout.Stages[stage - 1].name.ToUpper(), fs(12), Soft);
                UiArt.Text(new Rect(x + 14f * px, y + 36f * px, pw * 0.55f, 46f * px), timing ? Clock(Time.time - runStart) : "0:00.00", fs(36), Color.white);
                UiArt.Text(new Rect(x, y + 38f * px, pw - 16f * px, 22f * px), "STAGE  " + Clock(Time.time - stageStart), fs(15), Color.white, TextAnchor.MiddleRight);
                UiArt.Text(new Rect(x, y + 60f * px, pw - 16f * px, 20f * px), practice ? "PRACTICE  ·  NOTHING WON" : fullRun ? (Best > 0f ? "BEST  " + Clock(Best) : "NO BEST YET") : "CARRIED ON  ·  NOT A FULL RUN", fs(12), practice ? new Color(1f, 0.5f, 0.5f) : Soft, TextAnchor.MiddleRight);
                UiArt.Text(new Rect(w - 300f * px, 16f * px, 284f * px, 22f * px), "T  restart stage   ·   R  stage 1", fs(13), Soft, TextAnchor.MiddleRight);
            }
            else if (stage <= 1)
            {
                // The plaza: what it's for
                float cw = 560f * px, y = 18f * px;
                bool carry = Reached > 1;
                UiArt.Rounded(new Rect((w - cw) * 0.5f, y, cw, (carry ? 108f : 84f) * px), Panel, 14f * px);
                UiArt.Text(new Rect(0f, y + 6f * px, w, 36f * px), "10 STAGE BHOP CHALLENGE", fs(24), Color.white, TextAnchor.MiddleCenter);
                UiArt.Text(new Rect(0f, y + 42f * px, w, 22f * px), Won ? $"won  ·  all {KarambitCount()} karambits and their gloves are yours" + (Best > 0f ? "   ·   best " + Clock(Best) : "")
                    : $"finish all 10 for every karambit ({KarambitCount()}) and the gloves that match them", fs(14), Won ? Gold : Cyan, TextAnchor.MiddleCenter);
                UiArt.Text(new Rect(0f, y + 62f * px, w, 20f * px), stage == 1 ? "hop off the pad to start the clock" : "stage 1's pad is in the doorway at the end of the plaza", fs(12), Soft, TextAnchor.MiddleCenter);
                if (carry) UiArt.Text(new Rect(0f, y + 82f * px, w, 22f * px), $"or press  C  to carry on from stage {Mathf.Min(Reached, StageCount)}", fs(14), Gold, TextAnchor.MiddleCenter);
            }

            float age = Time.time - bannerAt;
            if (banner != null && age < 3f)
            {
                float a = Mathf.Clamp01(Mathf.Min(age * 3f, (3f - age) * 1.5f));
                float by = h * 0.24f, lw = 260f * px * a;
                UiArt.Rounded(new Rect(w * 0.5f - lw, by, lw * 2f, 2f * px), new Color(Cyan.r, Cyan.g, Cyan.b, a * 0.9f), 1f);
                UiArt.Text(new Rect(0f, by + 8f * px, w, 70f * px), banner, fs(48), new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
                UiArt.Text(new Rect(0f, by + 74f * px, w, 28f * px), bannerLine, fs(16), new Color(Soft.r, Soft.g, Soft.b, a), TextAnchor.MiddleCenter);
                UiArt.Rounded(new Rect(w * 0.5f - lw, by + 108f * px, lw * 2f, 2f * px), new Color(Cyan.r, Cyan.g, Cyan.b, a * 0.9f), 1f);
            }
            float noteAge = Time.time - noteAt;
            if (note != null && noteAge < 1.6f)
            {
                float a = Mathf.Clamp01(Mathf.Min(noteAge * 5f, (1.6f - noteAge) * 2f));
                var style = UiArt.Style(fs(15), true, TextAnchor.MiddleCenter);
                float tw = style.CalcSize(new GUIContent(note)).x + 40f * px;
                var r = new Rect((w - tw) * 0.5f, 120f * px, tw, 34f * px);
                UiArt.Rounded(r, new Color(Panel.r, Panel.g, Panel.b, Panel.a * a), 17f * px);
                UiArt.Text(r, note, fs(15), new Color(0.75f, 1f, 0.82f, a), TextAnchor.MiddleCenter);
            }

            if (stage == StageCount + 1)
            {
                // The finish: the time, and the prize
                float since = Time.time - finishedAt, a = Mathf.Clamp01(since * 2f);
                float cw = 600f * px, ch = 180f * px, x = (w - cw) * 0.5f, y = h * 0.2f;
                UiArt.Rounded(new Rect(x, y, cw, ch), new Color(Panel.r, Panel.g, Panel.b, 0.8f * a), 18f * px);
                UiArt.Text(new Rect(x, y + 14f * px, cw, 40f * px), practice ? "FINISHED  ·  PRACTICE" : "CHALLENGE COMPLETE", fs(32), new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
                UiArt.Text(new Rect(x, y + 58f * px, cw, 36f * px), Clock(runTime) + (fullRun ? "" : "   (carried on)"), fs(28), new Color(Gold.r, Gold.g, Gold.b, a), TextAnchor.MiddleCenter);
                UiArt.Text(new Rect(x, y + 100f * px, cw, 26f * px), won ? $"all {KarambitCount()} karambits and their matching gloves are yours  ·  Velocity set equipped" : "noclip was used: nothing won this time", fs(15), new Color(Cyan.r, Cyan.g, Cyan.b, a), TextAnchor.MiddleCenter);
                UiArt.Text(new Rect(x, y + 132f * px, cw, 24f * px), "step into the ring to go back up  ·  R for stage 1", fs(13), new Color(Soft.r, Soft.g, Soft.b, a), TextAnchor.MiddleCenter);
            }
        }
    }
}
