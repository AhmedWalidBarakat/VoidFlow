using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // The HUD, laid out like a surf server's: the course's progress along the top edge, the
    // stage, the zone and the clocks at the top centre (the run's time, this stage's time and
    // your best on it), your split against your best as you reach each checkpoint, the keys
    // you're pressing under the crosshair, and your speed at the bottom, coloured from cool to
    // hot as you go faster. Everything scales with the screen.
    public partial class RunTimer
    {
        static readonly Color Panel = new(0.04f, 0.035f, 0.07f, 0.62f), Accent = new(0.72f, 0.42f, 1f), Soft = new(0.78f, 0.78f, 0.86f);
        static readonly Color Faster = new(0.35f, 1f, 0.55f), Slower = new(1f, 0.38f, 0.4f), Gold = new(1f, 0.82f, 0.3f);
        string hudRunTime = "", hudStage = "", hudStageTime = "", hudBestTime = "", hudZone = "", hudFalls = "";
        float hudSpeedShown;
        string hudSpeedText = "0";

        static string Clock(float t) => t >= 3600f ? $"{(int)(t / 3600f)}:{(int)(t / 60f) % 60:00}:{t % 60f:00.00}" : $"{(int)(t / 60f)}:{t % 60f:00.00}";
        static string Delta(float d) => (d < 0f ? "-" : "+") + (Mathf.Abs(d) >= 60f ? Clock(Mathf.Abs(d)) : $"{Mathf.Abs(d):0.00}");

        // Cool to hot: grey-white standing, cyan, violet, pink, gold at the top speed
        static Color SpeedColor(float ups)
        {
            (float at, Color c)[] stops = { (0f, new Color(0.86f, 0.88f, 0.95f)), (700f, new Color(0.4f, 0.92f, 1f)), (1600f, new Color(0.72f, 0.48f, 1f)), (2600f, new Color(1f, 0.38f, 0.72f)), (3400f, new Color(1f, 0.82f, 0.3f)) };
            for (int i = 1; i < stops.Length; i++)
                if (ups <= stops[i].at) return Color.Lerp(stops[i - 1].c, stops[i].c, Mathf.InverseLerp(stops[i - 1].at, stops[i].at, ups));
            return stops[^1].c;
        }

        void RefreshHud()
        {
            var clock = StageClock.Instance;
            int stages = course.FinalRamp / course.rampsPerBiome;
            hudRunTime = Clock(RunTime);
            hudStage = $"STAGE {course.CurrentStage + 1} / {stages}";
            hudZone = $"{course.CurrentBiome.name}   ·   {course.CurrentTierName}";
            if (clock && clock.Timing)
            {
                hudStageTime = Clock(clock.StageTime);
                float best = clock.BestFor(clock.Stage);
                hudBestTime = best > 0f ? "BEST  " + Clock(best) : "NO BEST YET";
            }
            else { hudStageTime = ""; hudBestTime = Practice ? "PRACTICE  ·  NOT TIMED" : ""; }
            hudFalls = falls > 0 ? $"{falls} FALL{(falls == 1 ? "" : "S")}" : "";
        }

        void DrawRunHud(float w, float h, float px)
        {
            // the course's progress, along the very top
            float done = Mathf.Clamp01((course.CurrentRamp + 1f) / course.FinalRamp);
            UiArt.Rounded(new Rect(0f, 0f, w, 4f * px), new Color(0f, 0f, 0f, 0.45f), 0f);
            UiArt.Rounded(new Rect(0f, 0f, w * done, 4f * px), Color.Lerp(Accent, UiArt.CasePink, done), 0f);

            // the stage and its clocks
            float pw = 420f * px, ph = 92f * px, x = (w - pw) * 0.5f, y = 14f * px;
            UiArt.Rounded(new Rect(x, y, pw, ph), Panel, 14f * px);
            UiArt.Pill(new Rect(x + 14f * px, y + 10f * px, 130f * px, 22f * px), hudStage, Mathf.RoundToInt(12 * px), new Color(Accent.r, Accent.g, Accent.b, 0.9f), Color.white);
            UiArt.Text(new Rect(x + 154f * px, y + 10f * px, pw - 168f * px, 22f * px), hudZone, Mathf.RoundToInt(12 * px), Soft);
            UiArt.Text(new Rect(x + 14f * px, y + 36f * px, pw * 0.55f, 46f * px), hudRunTime, Mathf.RoundToInt(36 * px), Color.white);
            if (hudStageTime.Length > 0)
            {
                UiArt.Text(new Rect(x, y + 38f * px, pw - 16f * px, 22f * px), "STAGE  " + hudStageTime, Mathf.RoundToInt(15 * px), Color.white, TextAnchor.MiddleRight);
                UiArt.Text(new Rect(x, y + 60f * px, pw - 16f * px, 20f * px), hudBestTime, Mathf.RoundToInt(12 * px), Soft, TextAnchor.MiddleRight);
            }
            else if (hudBestTime.Length > 0)
                UiArt.Text(new Rect(x, y + 48f * px, pw - 16f * px, 22f * px), hudBestTime, Mathf.RoundToInt(12 * px), Gold, TextAnchor.MiddleRight);
            if (hudFalls.Length > 0)
                UiArt.Text(new Rect(w - 220f * px, 16f * px, 204f * px, 22f * px), hudFalls, Mathf.RoundToInt(13 * px), Soft, TextAnchor.MiddleRight);

            // the split, as you reach each checkpoint
            var clock = StageClock.Instance;
            if (clock && GameSettings.Splits)
            {
                var split = clock.LastSplit;
                float age = Time.time - split.at;
                if (split.stage >= 0 && age < 3.2f)
                {
                    float a = Mathf.Clamp01(Mathf.Min(age * 5f, (3.2f - age) * 1.6f));
                    float pop = 1f + 0.12f * Mathf.Max(0f, 1f - age * 4f);
                    Color c = split.best ? (split.delta < 0f ? Faster : Gold) : Slower;
                    string head = split.best ? (split.delta < 0f ? "NEW BEST" : "FIRST TIME") : "STAGE " + (split.stage + 1);
                    string line = Clock(split.time) + (split.delta != 0f ? "   " + Delta(split.delta) : "");
                    var r = UiArt.Grow(new Rect((w - 360f * px) * 0.5f, y + ph + 12f * px, 360f * px, 62f * px), pop);
                    UiArt.Rounded(r, new Color(Panel.r, Panel.g, Panel.b, Panel.a * a), 12f * px);
                    UiArt.Rounded(new Rect(r.x, r.y, 5f * px, r.height), new Color(c.r, c.g, c.b, a), 3f * px);
                    UiArt.Text(new Rect(r.x, r.y + 6f * px, r.width, 20f * px), head + (split.best ? "" : $"   ·   STAGE {split.stage + 1}"), Mathf.RoundToInt(12 * px), new Color(c.r, c.g, c.b, a), TextAnchor.MiddleCenter);
                    UiArt.Text(new Rect(r.x, r.y + 26f * px, r.width, 30f * px), line, Mathf.RoundToInt(24 * px), new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
                }
            }
        }

        // The keys you're holding, under the crosshair, as surf servers show them
        static void DrawKeys(float w, float h, float px)
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            float s = 26f * px, g = 5f * px, cx = w * 0.5f, top = h * 0.5f + 70f * px;
            void Key(Rect r, string label, bool on)
            {
                UiArt.Rounded(r, on ? new Color(Accent.r, Accent.g, Accent.b, 0.88f) : new Color(0f, 0f, 0f, 0.32f), 6f * px);
                UiArt.Text(r, label, Mathf.RoundToInt(12 * px), on ? Color.white : new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter);
            }
            Key(new Rect(cx - s * 0.5f, top, s, s), "W", kb.wKey.isPressed);
            Key(new Rect(cx - s * 1.5f - g, top + s + g, s, s), "A", kb.aKey.isPressed);
            Key(new Rect(cx - s * 0.5f, top + s + g, s, s), "S", kb.sKey.isPressed);
            Key(new Rect(cx + s * 0.5f + g, top + s + g, s, s), "D", kb.dKey.isPressed);
            Key(new Rect(cx - s * 1.5f - g, top + 2f * (s + g), 3f * s + 2f * g, s * 0.8f), "JUMP", kb.spaceKey.isPressed);
        }

        // Your speed, at the bottom: the number and a bar that fills to the top speed
        void DrawSpeed(float w, float h, float px)
        {
            float ups = player.HorizontalSpeed / PlayerMovement.SourceUnit;
            hudSpeedShown = Mathf.Lerp(hudSpeedShown, ups, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            Color c = SpeedColor(hudSpeedShown);
            float bw = 280f * px, y = h - 96f * px;
            UiArt.Text(new Rect(0f, y, w, 50f * px), hudSpeedText, Mathf.RoundToInt(44 * px), c, TextAnchor.MiddleCenter);
            UiArt.Text(new Rect(w * 0.5f + 62f * px, y + 14f * px, 60f * px, 30f * px), "u/s", Mathf.RoundToInt(13 * px), new Color(1f, 1f, 1f, 0.55f));
            var bar = new Rect((w - bw) * 0.5f, y + 52f * px, bw, 6f * px);
            UiArt.Rounded(bar, new Color(0f, 0f, 0f, 0.45f), 3f * px);
            UiArt.Rounded(new Rect(bar.x, bar.y, bw * Mathf.Clamp01(hudSpeedShown / 3500f), bar.height), c, 3f * px);
        }

        // In the start hall: what to do, on cards
        void DrawHallHud(float w, float h, float px)
        {
            float cw = 520f * px, y = 18f * px;
            UiArt.Rounded(new Rect((w - cw) * 0.5f, y, cw, HasSave ? 84f * px : 52f * px), Panel, 14f * px);
            UiArt.Text(new Rect(0f, y + 6f * px, w, 40f * px), "DROP IN TO START THE RUN", Mathf.RoundToInt(24 * px), Color.white, TextAnchor.MiddleCenter);
            if (HasSave)
            {
                int ramp = PlayerPrefs.GetInt(SaveRamp, 0);
                UiArt.Text(new Rect(0f, y + 46f * px, w, 28f * px), $"or press  C  to carry on from stage {ramp / course.rampsPerBiome + 1}  ·  {course.BiomeAt(ramp).name}  ·  {Format(PlayerPrefs.GetFloat(SaveTime, 0f))}", Mathf.RoundToInt(14 * px), Gold, TextAnchor.MiddleCenter);
            }
        }

        void DrawHelp(float w, float h, float px)
        {
            string[] lines =
            {
                "WASD move   ·   SPACE jump (hold to bhop)   ·   T restart the stage   ·   R restart the run   ·   double tap SPACE noclip   ·   TAB settings",
                "1 sniper   ·   2 knife   ·   Q last weapon   ·   CLICK fire / slash   ·   RIGHT CLICK scope   ·   F inspect (hold to show off)   ·   E use   ·   I inventory   ·   M stages",
                "On ramps: let go of W, hold A or D toward the ramp, and steer with the mouse",
            };
            float lh = 18f * px, pw = Mathf.Min(ViewModel.WeaponBox.x - 24f * px, 980f * px), ph = lh * lines.Length + 16f * px;
            var r = new Rect(12f * px, h - ph - 12f * px, pw, ph);
            UiArt.Rounded(r, new Color(0f, 0f, 0f, 0.42f), 10f * px);
            for (int i = 0; i < lines.Length; i++)
                UiArt.Text(new Rect(r.x + 12f * px, r.y + 8f * px + i * lh, r.width - 24f * px, lh), lines[i], Mathf.RoundToInt(12 * px), i == 2 ? Accent : Soft, TextAnchor.MiddleLeft, i < 2);
        }

        static readonly string[] Tips =
        {
            "On a ramp, let go of W and hold A or D toward the ramp: steer with the mouse",
            "In the air, hold A or D and turn the mouse the same way to gain speed",
            "Hold SPACE to bhop: every jump on landing keeps your speed",
            "Press TAB for settings: set your CS2 sensitivity and field of view",
            "Beat your best time on a stage and its ghost will race you next time",
            "Hold F to show off a Void weapon, tap F for its inspect",
            "Collect Void Shards on the course to earn Void Cases (I to open your inventory)",
            "Fell? You're back at the stage's checkpoint, the clock still running (T takes you back any time)",
            "M shows every stage you've reached: start a run from any of them",
        };

        void DrawLoading(float w, float h, float px, float progress)
        {
            float t = Time.unscaledTime;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 0f);
            // a slow violet glow behind the logo
            UiArt.Blob(new Vector2(w * 0.5f, h * 0.42f), 900f * px, Accent, 0.12f + 0.04f * Mathf.Sin(t * 1.3f));
            UiArt.Text(new Rect(0f, h * 0.36f, w, 90f * px), "VOIDFLOW", Mathf.RoundToInt(78 * px), Color.white, TextAnchor.MiddleCenter);
            UiArt.Text(new Rect(0f, h * 0.36f + 84f * px, w, 24f * px), "SURF THE VOID", Mathf.RoundToInt(14 * px), new Color(0.78f, 0.62f, 1f), TextAnchor.MiddleCenter);
            var bar = new Rect(w * 0.32f, h * 0.58f, w * 0.36f, 6f * px);
            UiArt.Rounded(bar, new Color(1f, 1f, 1f, 0.1f), 3f * px);
            UiArt.Rounded(new Rect(bar.x, bar.y, bar.width * progress, bar.height), Color.Lerp(Accent, UiArt.CasePink, progress), 3f * px);
            UiArt.Blob(new Vector2(bar.x + bar.width * progress, bar.center.y), 40f * px, UiArt.CasePink, 0.6f);
            UiArt.Text(new Rect(0f, bar.yMax + 10f * px, w, 22f * px), $"BUILDING THE COURSE   ·   {Mathf.RoundToInt(progress * 100f)}%", Mathf.RoundToInt(13 * px), Soft, TextAnchor.MiddleCenter);
            int tip = Mathf.FloorToInt(t / 5f) % Tips.Length;
            float fade = Mathf.Clamp01(Mathf.Min(Mathf.Repeat(t, 5f) * 2f, (5f - Mathf.Repeat(t, 5f)) * 2f));
            UiArt.Text(new Rect(0f, h * 0.78f, w, 24f * px), "TIP   ·   " + Tips[tip], Mathf.RoundToInt(14 * px), new Color(1f, 1f, 1f, 0.7f * fade), TextAnchor.MiddleCenter);
        }

        // A new zone: its name, big, between two lines of light
        void DrawBanner(float w, float h, float px, string text, float alpha)
        {
            var parts = text.Split('\n');
            float y = h * 0.24f;
            float lw = 260f * px * alpha;
            UiArt.Rounded(new Rect(w * 0.5f - lw, y, lw * 2f, 2f * px), new Color(Accent.r, Accent.g, Accent.b, alpha * 0.9f), 1f);
            UiArt.Text(new Rect(0f, y + 8f * px, w, 70f * px), parts[0], Mathf.RoundToInt(54 * px), new Color(1f, 1f, 1f, alpha), TextAnchor.MiddleCenter);
            if (parts.Length > 1)
                UiArt.Text(new Rect(0f, y + 78f * px, w, 28f * px), parts[1], Mathf.RoundToInt(16 * px), new Color(Soft.r, Soft.g, Soft.b, alpha), TextAnchor.MiddleCenter);
            UiArt.Rounded(new Rect(w * 0.5f - lw, y + 112f * px, lw * 2f, 2f * px), new Color(Accent.r, Accent.g, Accent.b, alpha * 0.9f), 1f);
        }

        void DrawNote(float w, float h, float px, string text, float alpha)
        {
            var style = UiArt.Style(Mathf.RoundToInt(15 * px), true, TextAnchor.MiddleCenter);
            float tw = style.CalcSize(new GUIContent(text)).x + 40f * px;
            var r = new Rect((w - tw) * 0.5f, 196f * px, tw, 34f * px); // (under the split, above the zone's banner)
            UiArt.Rounded(r, new Color(Panel.r, Panel.g, Panel.b, Panel.a * alpha), 17f * px);
            UiArt.Text(r, text, Mathf.RoundToInt(15 * px), new Color(0.75f, 1f, 0.82f, alpha), TextAnchor.MiddleCenter);
        }

        // ------------------------------------------------------------------ the finish

        // Worked out once per finish (not on every GUI pass)
        float resultsFor = -1f, sumOfBest;
        int newBests, firstTimes, bestsKnown;
        string resultsTime, resultsLine, resultsTop;
        Color resultsLineColor;

        // The end of the course: confetti, the run's time against your best, its falls, top
        // speed and stage bests, and a strip of every stage (gold a new best, violet a first time,
        // red slower than your best) under it
        void DrawResults(float w, float h, float px, float since)
        {
            var clock = StageClock.Instance;
            int stages = course.FinalRamp / course.rampsPerBiome;
            if (resultsFor != finishTime)
            {
                resultsFor = finishTime;
                newBests = firstTimes = bestsKnown = 0;
                sumOfBest = 0f;
                if (clock)
                    foreach (var split in clock.RunSplits.Values)
                    {
                        if (split.prior <= 0f) firstTimes++;
                        else if (split.time < split.prior) newBests++;
                    }
                for (int i = 0; i < stages; i++)
                {
                    float b = clock ? clock.BestFor(i) : 0f;
                    if (b > 0f) { bestsKnown++; sumOfBest += b; }
                }
                resultsTime = Clock(finalTime);
                resultsTop = runFrom == 0 ? "CONGRATS   ·   COURSE COMPLETE" : $"CONGRATS   ·   FINISHED FROM STAGE {runFrom + 1}";
                if (Practice) { resultsLine = "PRACTICE RUN   ·   noclip used, nothing saved"; resultsLineColor = Soft; }
                else if (newRunBest && prevRunBest > 0f) { resultsLine = $"NEW PERSONAL BEST   ·   {Delta(finalTime - prevRunBest)}"; resultsLineColor = Gold; }
                else if (newRunBest) { resultsLine = "FIRST CLEAR   ·   now beat it"; resultsLineColor = Gold; }
                else if (runFrom == 0) { resultsLine = $"{Delta(finalTime - prevRunBest)}   on your best of {Clock(prevRunBest)}"; resultsLineColor = Slower; }
                else { resultsLine = prevRunBest > 0f ? $"your whole-course best: {Clock(prevRunBest)}" : "start from stage 1 to set a whole-course time"; resultsLineColor = Soft; }
            }

            // confetti, for the first few seconds
            if (since < 7f)
            {
                for (int i = 0; i < 90; i++)
                {
                    float fall = (160f + UiArt.Hash(i + 7) * 260f) * px;
                    float y = -30f * px - UiArt.Hash(i + 3) * h * 0.7f + since * fall;
                    if (y < -20f * px || y > h) continue;
                    float x = UiArt.Hash(i) * w + Mathf.Sin(since * (1.5f + UiArt.Hash(i + 5) * 2f) + i) * 30f * px;
                    float size = (5f + UiArt.Hash(i + 9) * 7f) * px;
                    var c = Color.HSVToRGB(Mathf.Lerp(0.72f, 1.08f, UiArt.Hash(i + 11)) % 1f, 0.55f, 1f);
                    GUI.color = new Color(c.r, c.g, c.b, Mathf.Clamp01((7f - since) * 0.6f));
                    GUI.DrawTexture(new Rect(x, y, size, size), UiArt.Dot);
                }
                GUI.color = Color.white;
            }

            float a = Mathf.Clamp01(since * 2.5f);
            float rise = (1f - UiArt.BackOut(Mathf.Clamp01(since * 1.8f))) * 50f * px;
            float cw = Mathf.Min(w - 32f * px, 700f * px), ch = 410f * px;
            var card = new Rect((w - cw) * 0.5f, Mathf.Max(118f * px, (h - ch) * 0.5f) + rise, cw, ch);
            Color hue = Color.HSVToRGB(Mathf.Repeat(0.75f + Time.time * 0.06f, 1f), 0.6f, 1f);
            UiArt.Blob(card.center, cw * 1.25f, hue, 0.28f * a);
            GUI.color = Color.white;
            UiArt.Rounded(card, new Color(0.04f, 0.035f, 0.07f, 0.92f * a), 18f * px);
            UiArt.Rounded(new Rect(card.x, card.y, card.width, 4f * px), new Color(hue.r, hue.g, hue.b, a), 2f * px);

            float x0 = card.x + 28f * px, iw = card.width - 56f * px, y0 = card.y;
            UiArt.Text(new Rect(x0, y0 + 20f * px, iw, 22f * px), resultsTop, Mathf.RoundToInt(14 * px), new Color(hue.r, hue.g, hue.b, a), TextAnchor.MiddleCenter);
            float pulse = 1f + 0.03f * Mathf.Sin(Time.time * 4f);
            UiArt.Text(new Rect(x0, y0 + 44f * px, iw, 74f * px), resultsTime, Mathf.RoundToInt(64 * px * pulse), new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
            UiArt.Text(new Rect(x0, y0 + 118f * px, iw, 24f * px), resultsLine, Mathf.RoundToInt(16 * px), new Color(resultsLineColor.r, resultsLineColor.g, resultsLineColor.b, a), TextAnchor.MiddleCenter);

            // the numbers
            float ty = y0 + 156f * px, tg = 10f * px, tw = (iw - tg * 3f) / 4f, th = 66f * px;
            Tile(new Rect(x0, ty, tw, th), "FALLS", falls == 0 ? "NONE" : falls.ToString(), falls == 0 ? Gold : Color.white, a, px);
            Tile(new Rect(x0 + (tw + tg), ty, tw, th), "TOP SPEED", $"{topSpeed:0}", SpeedColor(topSpeed), a, px);
            Tile(new Rect(x0 + (tw + tg) * 2f, ty, tw, th), "STAGE BESTS", newBests + firstTimes == 0 ? "—" : $"{newBests + firstTimes}", newBests > 0 ? Gold : Accent, a, px);
            Tile(new Rect(x0 + (tw + tg) * 3f, ty, tw, th), "SUM OF BEST", bestsKnown == stages ? Clock(sumOfBest) : $"{bestsKnown}/{stages}", Soft, a, px);

            // every stage, one bar each, revealed left to right
            float sy = y0 + 240f * px, sh = 58f * px;
            UiArt.Text(new Rect(x0, sy, iw, 16f * px), "EVERY STAGE", Mathf.RoundToInt(11 * px), new Color(Soft.r, Soft.g, Soft.b, 0.8f * a));
            Legend(new Rect(x0, sy, iw, 16f * px), a, px);
            var strip = new Rect(x0, sy + 22f * px, iw, sh);
            UiArt.Rounded(strip, new Color(1f, 1f, 1f, 0.04f * a), 6f * px);
            float longest = 1f;
            if (clock) foreach (var split in clock.RunSplits.Values) longest = Mathf.Max(longest, split.time);
            float bw = strip.width / stages, gap = bw > 4f ? bw * 0.22f : 0f;
            float shown = Mathf.Clamp01((since - 0.35f) / 1.4f) * stages;
            for (int i = 0; i < stages && i < shown; i++)
            {
                Color c; float k;
                if (clock && clock.RunSplits.TryGetValue(i, out var split))
                {
                    k = Mathf.Lerp(0.22f, 1f, Mathf.Sqrt(split.time / longest));
                    c = split.prior <= 0f ? Accent : split.time < split.prior ? Gold : Slower;
                }
                else { k = 0.1f; c = new Color(1f, 1f, 1f, 0.25f); }
                float bh = (sh - 8f * px) * k;
                UiArt.Rounded(new Rect(strip.x + i * bw + gap * 0.5f, strip.yMax - 4f * px - bh, Mathf.Max(1f, bw - gap), bh), new Color(c.r, c.g, c.b, c.a * a), Mathf.Min(2f * px, bw * 0.4f));
            }

            UiArt.Text(new Rect(x0, y0 + 334f * px, iw, 22f * px), Practice ? "beat it without noclip to win every item in the game" : "EVERY ITEM IN THE GAME IS NOW IN YOUR INVENTORY   ·   press  I", Mathf.RoundToInt(14 * px), new Color(Gold.r, Gold.g, Gold.b, a), TextAnchor.MiddleCenter);
            UiArt.Text(new Rect(x0, y0 + 366f * px, iw, 20f * px), "R  run it again      ·      M  pick a stage", Mathf.RoundToInt(12 * px), new Color(Soft.r, Soft.g, Soft.b, 0.75f * a), TextAnchor.MiddleCenter);
        }

        static void Tile(Rect r, string label, string value, Color color, float a, float px)
        {
            UiArt.Rounded(r, new Color(1f, 1f, 1f, 0.06f * a), 10f * px);
            UiArt.Text(new Rect(r.x, r.y + 8f * px, r.width, 16f * px), label, Mathf.RoundToInt(11 * px), new Color(Soft.r, Soft.g, Soft.b, 0.8f * a), TextAnchor.MiddleCenter);
            UiArt.Text(new Rect(r.x, r.y + 26f * px, r.width, 32f * px), value, Mathf.RoundToInt(24 * px), new Color(color.r, color.g, color.b, a), TextAnchor.MiddleCenter);
        }

        // the strip's key, right-aligned on its label's line
        static void Legend(Rect r, float a, float px)
        {
            (string name, Color c)[] keys = { ("NEW BEST", Gold), ("FIRST TIME", Accent), ("SLOWER", Slower) };
            var style = UiArt.Style(Mathf.RoundToInt(11 * px));
            float x = r.xMax;
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                float tw = style.CalcSize(new GUIContent(keys[i].name)).x;
                x -= tw;
                UiArt.Text(new Rect(x, r.y, tw + 4f * px, r.height), keys[i].name, Mathf.RoundToInt(11 * px), new Color(Soft.r, Soft.g, Soft.b, 0.8f * a));
                x -= 14f * px;
                UiArt.Rounded(new Rect(x, r.center.y - 4f * px, 8f * px, 8f * px), new Color(keys[i].c.r, keys[i].c.g, keys[i].c.b, a), 4f * px);
                x -= 16f * px;
            }
        }
    }
}
