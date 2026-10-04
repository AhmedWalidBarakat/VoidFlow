using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // The inventory (I): your loadout (primary sniper, secondary knife, hands) and everything
    // you've earned: skins you've unboxed and the Void Cases you've got (collect 25 Void
    // Shards on the course, or hit 4 of 10 at the skeet range). It's saved on every change
    // (PlayerPrefs: the browser's storage on the web build), so it survives refreshes and
    // restarts. Click an item to equip it, a case to open it.
    public class Inventory : MonoBehaviour
    {
        public ViewModel viewModel;

        public static bool IsOpen { get; private set; }
        public static int VoidCases { get; private set; }

        class Item { public ItemSlot slot; public int index, count, order; public bool fresh; }
        static readonly List<Item> items = new();
        static int order, freshCases;
        static float caseToastTime = -99f;
        static string caseToastWhy = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            items.Clear();
            VoidCases = order = freshCases = 0;
            loaded = false;
            IsOpen = false;
            caseToastTime = -99f;
            if (!GiveEverything) return;
            // Testing: every item and a few Void Cases, so all of it can be seen
            for (int slot = 0; slot < 3; slot++)
            {
                var pool = Skins.Pool((ItemSlot)slot);
                for (int i = 1; i < pool.Length; i++) Add((ItemSlot)slot, i);
            }
            foreach (var it in items) it.fresh = false;
            VoidCases = 10;
        }

        // Saved as "slot:index:count;..." plus the number of unopened cases
        const string ItemsKey = "VoidFlow.inventory2", LegacyItemsKey = "VoidFlow.inventory", CasesKey = "VoidFlow.voidCases";
        static bool loaded;

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            // Saved as "slot:count:name;" (by name, so the item lists can change). An item that
            // isn't in the game any more (the old Void items) comes back as a Void Case.
            int retired = 0;
            void Load(ItemSlot slot, string name, int count)
            {
                if (count <= 0 || string.IsNullOrEmpty(name)) return;
                int index = Skins.IndexOf(slot, name);
                if (index <= 0) { if (index < 0) retired += count; return; }
                var item = items.Find(i => i.slot == slot && i.index == index);
                if (item == null) items.Add(item = new Item { slot = slot, index = index, order = ++order });
                item.count = Mathf.Max(item.count, count);
            }
            bool legacy = !PlayerPrefs.HasKey(ItemsKey);
            foreach (var entry in PlayerPrefs.GetString(legacy ? LegacyItemsKey : ItemsKey, "").Split(';'))
            {
                var bits = entry.Split(new[] { ':' }, 3);
                if (bits.Length != 3 || !int.TryParse(bits[0], out int slot) || slot < 0 || slot > 2) continue;
                if (legacy)
                {
                    // the old "slot:index:count", by place in the list
                    if (int.TryParse(bits[1], out int oldIndex) && int.TryParse(bits[2], out int oldCount))
                        Load((ItemSlot)slot, Skins.LegacyName((ItemSlot)slot, oldIndex), oldCount);
                }
                else if (int.TryParse(bits[1], out int saved)) Load((ItemSlot)slot, bits[2], saved);
            }
            VoidCases = Mathf.Max(VoidCases, PlayerPrefs.GetInt(CasesKey, 0));
            if (retired > 0)
            {
                VoidCases += retired;
                Save();
            }
            else if (legacy && PlayerPrefs.HasKey(LegacyItemsKey)) Save();
            // A one-time gift: the Void knives, so they can be tried straight away
            const string GiftKey = "VoidFlow.gift.voidKnives";
            if (!PlayerPrefs.HasKey(GiftKey))
            {
                PlayerPrefs.SetInt(GiftKey, 1);
                for (int i = 0; i < Skins.Knives.Length; i++)
                    if (Skins.IsVoidKnife(Skins.Knives[i].asset) && !items.Exists(x => x.slot == ItemSlot.Secondary && x.index == i)) Add(ItemSlot.Secondary, i);
                Save();
            }
        }

        static void Save()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var it in items) sb.Append((int)it.slot).Append(':').Append(it.count).Append(':').Append(Skins.Pool(it.slot)[it.index].name).Append(';');
            PlayerPrefs.SetString(ItemsKey, sb.ToString());
            PlayerPrefs.SetInt(CasesKey, VoidCases);
            PlayerPrefs.Save();
        }

        // For testing: start every session owning everything (true), or empty as players do
        static readonly bool GiveEverything = false;

        // Finishing the whole course: every item in the game, straight into the inventory
        public static void GrantEverything()
        {
            EnsureLoaded();
            for (int slot = 0; slot < 3; slot++)
            {
                var pool = Skins.Pool((ItemSlot)slot);
                for (int i = 1; i < pool.Length; i++)
                    if (!items.Exists(it => it.slot == (ItemSlot)slot && it.index == i)) Add((ItemSlot)slot, i);
            }
            Save();
        }

        // An unboxed skin
        public static void Add(ItemSlot slot, int index)
        {
            EnsureLoaded();
            var item = items.Find(i => i.slot == slot && i.index == index);
            if (item == null) items.Add(item = new Item { slot = slot, index = index });
            item.count++;
            item.fresh = true;
            item.order = ++order;
            Save();
        }

        // A Void Case, straight into the inventory, with a pop-up saying where it came from
        public static void AddCase(string why)
        {
            EnsureLoaded();
            VoidCases++;
            freshCases++;
            caseToastTime = Time.unscaledTime;
            caseToastWhy = why;
            Save();
        }

        const int TabAll = 0, TabCases = 4, CaseId = -2;
        static readonly string[] TabNames = { "ALL", "PRIMARY", "SECONDARY", "HANDS", "CASES" };

        struct Entry { public bool isCase, fresh; public ItemSlot slot; public int index, count, order; }
        readonly List<Entry> view = new(), all = new();
        readonly int[] counts = new int[5];
        readonly int[] equipped = new int[3];
        readonly Dictionary<int, float> hover = new();
        readonly List<int> hoverKeys = new();
        int tab, hovered = -1, hoveredSlot = -1, lastHovered = -1, equipId = -1, equipSlot = -1;
        float openTime, closeTime = -99f, scroll, scrollTarget, equipTime = -99f, lastToast = -99f, toastTime = -99f;
        string toast = "";
        Color toastColor = Color.white;
        CaseStation voidCaseOpener;
        AudioSource audioSource;

        class Bit { public Vector2 p, v; public float size, life, age; public Color color; public bool star; }
        readonly List<Bit> confetti = new();

        static int Id(Entry e) => e.isCase ? CaseId : (int)e.slot * 1000 + e.index;
        static float Scale() => Mathf.Clamp(Screen.height / 800f, 0.6f, 2.2f);

        void Start()
        {
            if (!viewModel) viewModel = FindAnyObjectByType<ViewModel>();
            EnsureLoaded();
            ItemIcons.Warm();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            for (int s = 0; s < 3; s++) equipped[s] = Skins.Equipped((ItemSlot)s);
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (!IsOpen) { if (kb.iKey.wasPressedThisFrame && !ViewModel.InputBlocked) Open(); }
                else if (kb.iKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) Close();
            }
            if (IsOpen && Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Smooth scrolling and hover, the same at any frame rate
            scroll = Mathf.Lerp(scroll, scrollTarget, 1f - Mathf.Exp(-16f * dt));
            if (hovered != -1 && !hover.ContainsKey(hovered)) hover[hovered] = 0f;
            if (hoveredSlot != -1 && !hover.ContainsKey(hoveredSlot)) hover[hoveredSlot] = 0f;
            hoverKeys.Clear();
            hoverKeys.AddRange(hover.Keys);
            foreach (int key in hoverKeys)
                hover[key] = Mathf.MoveTowards(hover[key], key == hovered || key == hoveredSlot ? 1f : 0f, dt * 7f);
            int anyHover = hovered != -1 ? hovered : hoveredSlot;
            if (anyHover != lastHovered)
            {
                if (anyHover != -1 && IsOpen) Play(WeaponSounds.Tick, 0.22f, 1.8f);
                lastHovered = anyHover;
            }

            // A case just earned: a chime and a burst of confetti from the pop-up
            if (caseToastTime > lastToast)
            {
                lastToast = caseToastTime;
                Play(WeaponSounds.Reveal, 0.6f, 1.15f);
                SpawnConfetti(new Vector2(Screen.width / Scale() / 2f, 210f), 46, UiArt.CasePink);
            }
            UpdateConfetti(dt);
        }

        void Open()
        {
            IsOpen = true;
            openTime = Time.unscaledTime;
            scroll = scrollTarget = 0f;
            ViewModel.InputBlocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Play(WeaponSounds.Launch, 0.35f, 1.7f);
            foreach (var it in items) ItemIcons.Request(it.slot, it.index, true);
            for (int s = 0; s < 3; s++) ItemIcons.Request((ItemSlot)s, Skins.Equipped((ItemSlot)s), true);
            ItemIcons.Warm();
        }

        void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            closeTime = Time.unscaledTime;
            ViewModel.InputBlocked = false;
            hovered = hoveredSlot = -1;
            foreach (var it in items) it.fresh = false;
            freshCases = 0;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Play(WeaponSounds.Launch, 0.25f, 2.1f);
        }

        void Equip(Entry entry, Vector2 at)
        {
            if (!viewModel) viewModel = FindAnyObjectByType<ViewModel>();
            if (viewModel) viewModel.EquipSkin(entry.slot, entry.index);
            var skin = Skins.Pool(entry.slot)[entry.index];
            equipId = Id(entry);
            equipSlot = (int)entry.slot;
            equipTime = toastTime = Time.unscaledTime;
            toast = $"EQUIPPED   ·   {skin.name}";
            toastColor = Skins.RarityColor(skin.rarity);
            bool isVoid = skin.rarity == SkinRarity.Void;
            SpawnConfetti(at, isVoid ? 44 : 24, toastColor);
            Play(WeaponSounds.Reveal, 0.35f, isVoid ? 1.25f : 1.6f);
        }

        void OpenCase()
        {
            if (VoidCases <= 0) return;
            if (!viewModel) viewModel = FindAnyObjectByType<ViewModel>();
            if (!voidCaseOpener)
            {
                var go = new GameObject("Void Case");
                go.SetActive(false);
                voidCaseOpener = go.AddComponent<CaseStation>();
                voidCaseOpener.voidCase = true;
                voidCaseOpener.title = "VOID CASE";
                voidCaseOpener.rayColor = new Color(0.9f, 0.4f, 1f);
                voidCaseOpener.template = viewModel ? viewModel.template : null;
                go.SetActive(true);
            }
            VoidCases--;
            freshCases = 0;
            Save();
            Close();
            voidCaseOpener.OpenNow();
        }

        void BuildView()
        {
            view.Clear();
            all.Clear();
            for (int i = 0; i < counts.Length; i++) counts[i] = 0;
            // The default of each slot is always yours
            for (int s = 0; s < 3; s++) all.Add(new Entry { slot = (ItemSlot)s, index = 0 });
            foreach (var it in items)
                all.Add(new Entry { slot = it.slot, index = it.index, count = it.count, fresh = it.fresh, order = it.order });
            // Best first: Void, then Mythic, newest first within each
            all.Sort((a, b) =>
            {
                int ra = (int)Skins.Pool(a.slot)[a.index].rarity, rb = (int)Skins.Pool(b.slot)[b.index].rarity;
                return ra != rb ? rb.CompareTo(ra) : b.order != a.order ? b.order.CompareTo(a.order) : a.slot.CompareTo(b.slot);
            });
            foreach (var a in all) { counts[TabAll]++; counts[1 + (int)a.slot]++; }
            counts[TabCases] = VoidCases;
            if (VoidCases > 0) counts[TabAll]++;
            if (VoidCases > 0 && (tab == TabAll || tab == TabCases)) view.Add(new Entry { isCase = true, count = VoidCases, fresh = freshCases > 0 });
            if (tab != TabCases)
                foreach (var a in all)
                    if (tab == TabAll || tab == 1 + (int)a.slot) view.Add(a);
        }

        bool TabHasNew(int t)
        {
            if (t == TabCases) return freshCases > 0;
            foreach (var it in items) if (it.fresh && (t == TabAll || t == 1 + (int)it.slot)) return true;
            return t == TabAll && freshCases > 0;
        }

        // ------------------------------------------------------------------ GUI

        void OnGUI()
        {
            float now = Time.unscaledTime;
            bool showing = IsOpen || now - closeTime < 0.2f;
            bool caseToast = now - caseToastTime < 3.8f;
            if (!showing && !caseToast && confetti.Count == 0) return;
            GUI.depth = -20;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            float k = Scale();
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            float W = Screen.width / k, H = Screen.height / k;
            if (Event.current.type == EventType.Layout) BuildView();
            if (showing) DrawInventory(now, W, H);
            else if (caseToast) DrawCaseToast(now, W);
            DrawConfetti();
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        void DrawInventory(float now, float W, float H)
        {
            var e = Event.current;
            float t = now - openTime;
            float fade = IsOpen ? Mathf.SmoothStep(0f, 1f, t / 0.2f) : 1f - Mathf.Clamp01((now - closeTime) / 0.2f);
            Color violet = UiArt.CaseViolet, pink = UiArt.CasePink;

            // Backdrop: the world darkened, slow colored light drifting over it
            GUI.color = new Color(0.01f, 0.005f, 0.03f, 0.84f * fade);
            GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
            UiArt.Blob(new Vector2(W * (0.18f + 0.06f * Mathf.Sin(now * 0.3f)), H * 0.12f), W * 0.9f, violet, 0.32f * fade);
            UiArt.Blob(new Vector2(W * (0.86f + 0.05f * Mathf.Cos(now * 0.25f)), H * 0.92f), W * 0.8f, pink, 0.22f * fade);

            float pw = Mathf.Min(W - 50f, 1500f), ph = H - 50f;
            float rise = IsOpen ? (1f - UiArt.BackOut(Mathf.Clamp01(t / 0.35f))) * 40f : (1f - fade) * 20f;
            var panel = new Rect((W - pw) / 2f, (H - ph) / 2f + rise, pw, ph);
            GUI.color = new Color(violet.r, violet.g, violet.b, 0.28f * fade);
            GUI.DrawTexture(new Rect(panel.x, panel.y - 140f, pw, 300f), UiArt.Glow);
            UiArt.Rounded(panel, new Color(0.045f, 0.035f, 0.085f, 0.95f * fade), 20f);
            UiArt.Rounded(panel, new Color(violet.r, violet.g, violet.b, 0.55f * fade), 20f, 1.5f);

            // Header
            UiArt.Text(new Rect(panel.x + 34f, panel.y + 20f, 500f, 50f), "INVENTORY", 40, new Color(1f, 1f, 1f, fade));
            float grow = UiArt.BackOut(Mathf.Clamp01((t - 0.1f) / 0.4f));
            UiArt.Rounded(new Rect(panel.x + 36f, panel.y + 72f, 120f * grow, 4f), new Color(violet.r, violet.g, violet.b, fade), 2f);
            UiArt.Rounded(new Rect(panel.x + 36f + 120f * grow, panel.y + 72f, 90f * grow, 4f), new Color(pink.r, pink.g, pink.b, fade), 2f);
            UiArt.Text(new Rect(panel.x + 36f, panel.y + 82f, 700f, 22f), "saved on this device   ·   kept when you reload the page", 14,
                new Color(0.72f, 0.68f, 0.85f, fade), TextAnchor.MiddleLeft, false);
            var closeRect = new Rect(panel.xMax - 190f, panel.y + 28f, 156f, 34f);
            bool overClose = IsOpen && closeRect.Contains(e.mousePosition);
            UiArt.Pill(closeRect, "[ I ]   CLOSE", 14, new Color(1f, 1f, 1f, (overClose ? 0.16f : 0.07f) * fade), new Color(1f, 1f, 1f, 0.85f * fade));
            if (overClose && e.type == EventType.MouseDown && e.button == 0) { Close(); e.Use(); return; }

            // Loadout on the left
            float top = panel.y + 122f;
            float lw = Mathf.Clamp(pw * 0.22f, 220f, 300f);
            var col = new Rect(panel.x + 34f, top, lw, panel.yMax - 28f - top);
            UiArt.Text(new Rect(col.x, col.y - 4f, lw, 24f), "LOADOUT", 15, new Color(0.8f, 0.65f, 1f, fade));
            float slotH = Mathf.Min((col.height - 30f - 2f * 14f) / 3f, 215f);
            int overSlot = -1;
            for (int s = 0; s < 3; s++)
            {
                var r = new Rect(col.x, col.y + 30f + s * (slotH + 14f), lw, slotH);
                if (DrawSlot(r, (ItemSlot)s, now, fade, t)) overSlot = 10000 + s;
            }

            // Tabs over the grid
            float gx = col.xMax + 30f;
            var grid = new Rect(gx, top + 44f, panel.xMax - 34f - gx, panel.yMax - 28f - (top + 44f));
            float tx = gx;
            for (int i = 0; i < TabNames.Length; i++)
            {
                string label = counts[i] > 0 ? $"{TabNames[i]}   {counts[i]}" : TabNames[i];
                float tw = 34f + label.Length * 9.6f;
                var r = new Rect(tx, top - 6f, tw, 36f);
                bool over = IsOpen && r.Contains(e.mousePosition);
                bool active = tab == i;
                Color fill = active ? (i == TabCases ? pink : violet) : new Color(1f, 1f, 1f, over ? 0.13f : 0.05f);
                fill.a *= fade;
                UiArt.Pill(r, label, 14, fill, new Color(1f, 1f, 1f, (active ? 1f : 0.75f) * fade));
                if (TabHasNew(i))
                {
                    float pulse = 0.6f + 0.4f * Mathf.Sin(now * 6f);
                    GUI.color = new Color(1f, 0.4f, 0.75f, pulse * fade);
                    GUI.DrawTexture(new Rect(r.xMax - 14f, r.y - 5f, 16f, 16f), UiArt.Dot);
                }
                if (over && e.type == EventType.MouseDown && e.button == 0)
                {
                    tab = i;
                    scrollTarget = 0f;
                    Play(WeaponSounds.Tick, 0.45f, 1.3f);
                    e.Use();
                }
                tx += tw + 10f;
            }
            int overCard = DrawGrid(grid, now, fade, t);

            if (e.type == EventType.Repaint)
            {
                hovered = IsOpen ? overCard : -1;
                hoveredSlot = IsOpen ? overSlot : -1;
            }

            // A pill that rises after equipping something
            float ta = now - toastTime;
            if (ta < 1.8f)
            {
                float a = Mathf.Clamp01(ta / 0.15f) * (1f - Mathf.Clamp01((ta - 1.4f) / 0.4f)) * fade;
                float y = panel.yMax - 70f - UiArt.BackOut(Mathf.Clamp01(ta / 0.35f)) * 20f;
                float tw = 60f + toast.Length * 10f;
                var r = new Rect(panel.center.x - tw / 2f, y, tw, 42f);
                UiArt.Rounded(r, new Color(0.03f, 0.02f, 0.06f, 0.92f * a), 21f);
                UiArt.Rounded(r, new Color(toastColor.r, toastColor.g, toastColor.b, a), 21f, 2f);
                UiArt.Text(r, toast, 16, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
            }
        }

        // One loadout slot; returns whether the mouse is over it. Click to see that slot's items.
        bool DrawSlot(Rect r, ItemSlot slot, float now, float fade, float t)
        {
            var e = Event.current;
            int id = 10000 + (int)slot, index = equipped[(int)slot];
            var skin = Skins.Pool(slot)[index];
            bool over = IsOpen && r.Contains(e.mousePosition);
            float hv = hover.TryGetValue(id, out var h) ? h : 0f;
            float appear = IsOpen ? Mathf.Clamp01((t - 0.05f - (int)slot * 0.07f) / 0.3f) : 1f;
            float pop = equipSlot == (int)slot ? Pop(now - equipTime) : 0f;
            var rr = UiArt.Grow(r, (0.85f + 0.15f * UiArt.BackOut(appear)) * (1f + 0.03f * hv + pop));
            string key = slot == ItemSlot.Primary ? "   ·   1" : slot == ItemSlot.Secondary ? "   ·   2" : "";
            UiArt.Card(rr, skin, ItemIcons.Get(slot, index), fade * appear, hv, now, id, Skins.SlotName(slot) + key);
            float flash = equipSlot == (int)slot ? 1f - Mathf.Clamp01((now - equipTime) / 0.4f) : 0f;
            if (flash > 0f) UiArt.Rounded(rr, new Color(1f, 1f, 1f, 0.35f * flash * fade), 14f);
            if (over && e.type == EventType.MouseDown && e.button == 0)
            {
                tab = 1 + (int)slot;
                scrollTarget = 0f;
                Play(WeaponSounds.Tick, 0.45f, 1.3f);
                e.Use();
            }
            return over;
        }

        static float Pop(float u) => u < 0f || u > 0.45f ? 0f : Mathf.Sin(u / 0.45f * Mathf.PI) * 0.09f * (1f - u / 0.45f * 0.5f);

        // The scrolling grid of items; returns the id of the card under the mouse
        int DrawGrid(Rect area, float now, float fade, float t)
        {
            var e = Event.current;
            const float gap = 16f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((area.width + gap) / (190f + gap)));
            float cw = (area.width - gap * (cols - 1)) / cols, ch = cw * 0.84f;
            int rows = (view.Count + cols - 1) / cols;
            float content = rows * (ch + gap) + 16f;
            float maxScroll = Mathf.Max(0f, content - area.height);
            if (e.type == EventType.ScrollWheel && area.Contains(e.mousePosition))
            {
                scrollTarget = Mathf.Clamp(scrollTarget + e.delta.y * 30f, 0f, maxScroll);
                e.Use();
            }
            scrollTarget = Mathf.Clamp(scrollTarget, 0f, maxScroll);

            int over = -1;
            GUI.BeginGroup(area);
            Vector2 mouse = e.mousePosition;
            bool inside = IsOpen && new Rect(0f, 0f, area.width, area.height).Contains(mouse);
            for (int i = 0; i < view.Count; i++)
            {
                var entry = view[i];
                int id = Id(entry);
                var r = new Rect((i % cols) * (cw + gap), 8f + (i / cols) * (ch + gap) - scroll, cw, ch);
                if (r.yMax < -30f || r.y > area.height + 30f) continue;
                float appear = IsOpen ? Mathf.Clamp01((t - 0.1f - i * 0.03f) / 0.3f) : 1f;
                if (appear <= 0f) continue;
                bool isOver = inside && r.Contains(mouse);
                if (isOver) over = id;
                float hv = hover.TryGetValue(id, out var h) ? h : 0f;
                float pop = id == equipId ? Pop(now - equipTime) : 0f;
                var rr = UiArt.Grow(r, (0.8f + 0.2f * UiArt.BackOut(appear)) * (1f + 0.035f * hv + pop));
                rr.y += (1f - appear) * 30f - hv * 3f;
                float a = fade * appear;
                if (entry.isCase) UiArt.CaseCard(rr, ItemIcons.VoidCase, entry.count, a, hv, now, true);
                else
                {
                    var skin = Skins.Pool(entry.slot)[entry.index];
                    UiArt.Card(rr, skin, ItemIcons.Get(entry.slot, entry.index), a, hv, now, id);
                    float bx = rr.xMax - 8f;
                    if (equipped[(int)entry.slot] == entry.index)
                    {
                        var b = new Rect(bx - 84f, rr.y + 8f, 84f, 20f);
                        UiArt.Pill(b, "EQUIPPED", 11, new Color(0.15f, 0.75f, 0.45f, 0.95f * a), new Color(1f, 1f, 1f, a));
                        bx = b.x - 6f;
                    }
                    if (entry.fresh)
                    {
                        Color c = Color.Lerp(UiArt.CasePink, UiArt.CaseAmber, 0.5f + 0.5f * Mathf.Sin(now * 5f));
                        UiArt.Pill(new Rect(bx - 44f, rr.y + 8f, 44f, 20f), "NEW", 11, new Color(c.r, c.g, c.b, a), new Color(1f, 1f, 1f, a));
                    }
                    if (entry.count > 1)
                        UiArt.Text(new Rect(rr.x, rr.yMax - 26f, rr.width - 12f, 20f), $"×{entry.count}", 15, new Color(1f, 1f, 1f, a), TextAnchor.MiddleRight);
                }
                if (isOver && e.type == EventType.MouseDown && e.button == 0 && appear > 0.6f)
                {
                    var center = area.position + rr.center;
                    if (entry.isCase) OpenCase();
                    else Equip(entry, center);
                    e.Use();
                }
            }

            // Hints when there's not much here yet
            float below = 8f + rows * (ch + gap) - scroll + 10f;
            if (tab == TabCases && VoidCases == 0)
            {
                UiArt.Text(new Rect(0f, area.height * 0.35f, area.width, 34f), "No Void Cases yet", 24, new Color(1f, 1f, 1f, 0.9f * fade), TextAnchor.MiddleCenter);
                UiArt.Text(new Rect(0f, area.height * 0.35f + 36f, area.width, 24f), "hit 4 of 10 at the skeet range, or collect 25 Void Shards on the course",
                    15, new Color(1f, 0.65f, 0.9f, 0.85f * fade), TextAnchor.MiddleCenter, false);
            }
            else if (items.Count == 0 && VoidCases == 0 && below < area.height - 30f)
                UiArt.Text(new Rect(0f, below, area.width, 24f), "collect Void Shards on the course or hit 4 of 10 at skeet to earn Void Cases",
                    15, new Color(0.8f, 0.7f, 1f, 0.8f * fade), TextAnchor.MiddleLeft, false);
            GUI.EndGroup();

            // Soft fades where the grid scrolls under the edges, and a slim scrollbar
            var panelColor = new Color(0.045f, 0.035f, 0.085f, fade);
            GUI.color = panelColor;
            GUI.DrawTexture(new Rect(area.x, area.yMax - 26f, area.width, 26f), UiArt.Fade);
            if (scroll > 1f) GUI.DrawTextureWithTexCoords(new Rect(area.x, area.y, area.width, 20f), UiArt.Fade, new Rect(0f, 1f, 1f, -1f));
            if (maxScroll > 1f)
            {
                float barH = Mathf.Max(30f, area.height * area.height / content);
                float y = area.y + (area.height - barH) * (scroll / maxScroll);
                UiArt.Rounded(new Rect(area.xMax + 10f, area.y, 4f, area.height), new Color(1f, 1f, 1f, 0.06f * fade), 2f);
                UiArt.Rounded(new Rect(area.xMax + 10f, y, 4f, barH), new Color(0.7f, 0.4f, 1f, 0.9f * fade), 2f);
            }
            return over;
        }

        // "+1 VOID CASE" pop-up at the top of the screen when you earn one
        void DrawCaseToast(float now, float W)
        {
            float t = now - caseToastTime;
            float pop = UiArt.ElasticOut(Mathf.Clamp01(t / 0.6f));
            float gone = Mathf.Clamp01((t - 3.2f) / 0.5f);
            float a = 1f - gone;
            if (pop < 0.02f) return;
            var r = UiArt.Grow(new Rect(W / 2f - 240f, 150f - gone * 30f, 480f, 120f), pop);
            UiArt.Rounded(r, new Color(0.07f, 0.02f, 0.13f, 0.96f * a), 18f);
            GUI.BeginGroup(r);
            float w = r.width, h = r.height;
            UiArt.Blob(new Vector2(w * 0.2f, h * 0.5f), h * 2.2f, UiArt.CaseViolet, 0.8f * a);
            UiArt.Blob(new Vector2(w * (0.7f + 0.1f * Mathf.Sin(now * 1.3f)), h * 0.3f), h * 2f, UiArt.CasePink, 0.5f * a);
            UiArt.Blob(new Vector2(w * 0.9f, h), h * 1.6f, UiArt.CaseAmber, 0.35f * a);
            UiArt.DrawRays(new Rect(w * 0.2f - h, h * 0.5f - h, h * 2f, h * 2f), now * 0.3f, new Color(1f, 0.8f, 1f, 0.45f * a));
            var icon = ItemIcons.VoidCase;
            if (icon)
            {
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(new Rect(6f, 4f + Mathf.Sin(now * 3f) * 3f, w * 0.34f, h - 8f), icon, ScaleMode.ScaleToFit);
            }
            float fs = w / 480f;
            UiArt.Text(new Rect(w * 0.36f, h * 0.1f, w * 0.62f, 38f * fs), "+1 VOID CASE", Mathf.RoundToInt(30f * fs), new Color(1f, 1f, 1f, a));
            UiArt.Text(new Rect(w * 0.36f, h * 0.44f, w * 0.62f, 22f * fs), caseToastWhy, Mathf.RoundToInt(16f * fs), new Color(1f, 0.7f, 0.95f, a));
            UiArt.Text(new Rect(w * 0.36f, h * 0.66f, w * 0.62f, 20f * fs), "press  I  to open it", Mathf.RoundToInt(13f * fs), new Color(0.85f, 0.8f, 1f, 0.85f * a), TextAnchor.MiddleLeft, false);
            GUI.EndGroup();
            Color edge = Color.Lerp(UiArt.CaseViolet, UiArt.CasePink, 0.5f + 0.5f * Mathf.Sin(now * 3f));
            UiArt.Rounded(r, new Color(edge.r, edge.g, edge.b, a), 18f, 2.5f);
        }

        // ------------------------------------------------------------------ confetti and sound

        void SpawnConfetti(Vector2 at, int count, Color color)
        {
            Color[] palette = { color, Color.Lerp(color, Color.white, 0.5f), Color.white, UiArt.CasePink, UiArt.CaseAmber };
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f), speed = Random.Range(180f, 650f);
                confetti.Add(new Bit
                {
                    p = at + Random.insideUnitCircle * 14f,
                    v = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) - 0.8f) * speed,
                    size = Random.Range(5f, 13f), life = Random.Range(0.9f, 1.7f),
                    color = palette[Random.Range(0, palette.Length)], star = Random.value < 0.5f,
                });
            }
        }

        void UpdateConfetti(float dt)
        {
            for (int i = confetti.Count - 1; i >= 0; i--)
            {
                var b = confetti[i];
                b.age += dt;
                if (b.age > b.life) { confetti.RemoveAt(i); continue; }
                b.v.y += 900f * dt;
                b.v *= Mathf.Exp(-1.2f * dt);
                b.p += b.v * dt;
            }
        }

        void DrawConfetti()
        {
            foreach (var b in confetti)
            {
                float fade = Mathf.Clamp01((b.life - b.age) / 0.4f);
                GUI.color = new Color(b.color.r, b.color.g, b.color.b, fade);
                GUI.DrawTexture(new Rect(b.p.x - b.size, b.p.y - b.size, b.size * 2f, b.size * 2f), b.star ? UiArt.Star : UiArt.Dot);
            }
        }

        void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (!clip) return;
            if (!audioSource)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            if (Mathf.Approximately(pitch, 1f)) { audioSource.PlayOneShot(clip, volume); return; }
            var one = gameObject.AddComponent<AudioSource>();
            one.spatialBlend = 0f;
            one.pitch = pitch;
            one.PlayOneShot(clip, volume);
            Destroy(one, clip.length / pitch + 0.1f);
        }
    }
}
