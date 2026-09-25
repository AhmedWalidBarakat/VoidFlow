using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidFlow
{
    // A big spinning model of one skin over a pedestal in the start hall. Walk up to it and
    // press E to try it on. The model is built when the scene loads (and in the editor), not
    // saved into the scene.
    [ExecuteAlways]
    public class SkinDisplay : MonoBehaviour
    {
        public int skinIndex;
        public bool sniper;
        public bool glove;
        [Tooltip("Gallery piece: just to look at (shows its name up close), can't be equipped")]
        public bool gallery;
        [Tooltip("If above 0: the shelf is this far below, and the item must fit in maxHeight above it (it's lifted and shrunk to fit, so nothing pokes through)")]
        public float shelfBelow = -1f, maxHeight = 1f;
        [Tooltip("Any URP Lit material; the model's materials are made from it")]
        public Material template;
        public float scale = 8f;
        public float useRange = 2.6f;

        Transform model;
        WeaponParts parts;
        readonly List<Material> materials = new();
        PlayerMovement player;
        ViewModel viewModel;
        bool near;
        GUIStyle style;

        public Skins.Skin Skin => glove ? Skins.Gloves[Mathf.Clamp(skinIndex, 0, Skins.Gloves.Length - 1)] : sniper ? Skins.Snipers[Mathf.Clamp(skinIndex, 0, Skins.Snipers.Length - 1)] : Skins.Knives[Mathf.Clamp(skinIndex, 0, Skins.Knives.Length - 1)];

        static readonly List<SkinDisplay> all = new();

        void OnEnable()
        {
            all.Add(this);
            Build();
        }

        void Start()
        {
            if (Application.isPlaying) FxLibrary.Sparkle(transform, Skins.RarityColor(Skin.rarity), 0.4f, 2.5f);
        }
        void OnDisable()
        {
            all.Remove(this);
            Clear();
        }

        // Only the display nearest you (within range) answers to E
        bool IsNearest(Vector3 p)
        {
            float mine = Flat(p);
            if (mine > useRange) return false;
            foreach (var d in all)
                if (d != this && d.Flat(p) < mine) return false;
            return true;
        }

        float Flat(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        void Build()
        {
            Clear();
            if (!template) return;
            model = new GameObject("Display Model").transform;
            model.SetParent(transform, false);
            model.localScale = Vector3.one * scale;
            // Center the weapon on the spin axis
            var offset = new GameObject("Offset").transform;
            offset.SetParent(model, false);
            offset.localPosition = sniper ? new Vector3(0f, -0.02f, -0.25f) : glove ? new Vector3(0f, 0.12f, 0f) : new Vector3(0f, -0.06f, 0f);
            var builder = new WeaponBuilder(template, gameObject.layer, true, materials);
            parts = glove ? builder.GloveModel(Skin, offset) : sniper ? builder.Rifle(Skin, offset) : builder.Knife(Skin, offset);
            if (shelfBelow > 0f) FitAboveShelf();
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        // Spinning about the vertical keeps its height the same, so measure it once: shrink it
        // if it's too tall for its shelf, then lift it so its lowest point clears the shelf
        void FitAboveShelf()
        {
            float low = float.MaxValue, high = float.MinValue;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || mf.name == "Flame") continue;
                var b = mf.sharedMesh.bounds;
                var m = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    float y = m.MultiplyPoint3x4(corner).y;
                    low = Mathf.Min(low, y);
                    high = Mathf.Max(high, y);
                }
            }
            if (low > high) return;
            float fit = Mathf.Min(1f, maxHeight / Mathf.Max(high - low, 0.01f));
            model.localScale *= fit;
            low *= fit;
            const float gap = 0.05f;
            if (low < -shelfBelow + gap) model.localPosition += Vector3.up * (-shelfBelow + gap - low);
        }

        void Clear()
        {
            if (model) WeaponBuilder.Kill(model.gameObject);
            model = null;
            parts = null;
            foreach (var m in materials) WeaponBuilder.Kill(m);
            materials.Clear();
        }

        void Update()
        {
            float time = Application.isPlaying ? Time.time : (float)(Time.realtimeSinceStartupAsDouble);
            if (model) model.localRotation = Quaternion.Euler(0f, time * 50f, 0f);
            parts?.Animate(time, -1f);
            if (!Application.isPlaying) return;

            if (!player) player = FindAnyObjectByType<PlayerMovement>();
            if (!player) return;
            // Far across the hall, the model switches off (a gallery holds a lot of them)
            if (model) model.gameObject.SetActive((player.Position - transform.position).sqrMagnitude < 34f * 34f);
            near = IsNearest(player.Position) && !ViewModel.InputBlocked;
            var kb = Keyboard.current;
            if (near && !gallery && kb != null && kb.eKey.wasPressedThisFrame)
            {
                if (!viewModel) viewModel = FindAnyObjectByType<ViewModel>();
                if (!viewModel) return;
                if (glove) viewModel.EquipGloveSkin(skinIndex);
                else if (sniper) viewModel.EquipSniperSkin(skinIndex);
                else viewModel.EquipKnifeSkin(skinIndex);
            }
        }

        void OnGUI()
        {
            if (!near) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var skin = Skin;
            style.normal.textColor = Skins.RarityColor(skin.rarity);
            string text = gallery
                ? $"★ {skin.name}   ·   {Skins.RarityName(skin.rarity)}   ·   from the Void Case"
                : $"[E]  try on  ★ {skin.name}  ({Skins.RarityName(skin.rarity)})";
            GUI.Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 30f), text, style);
        }
    }
}
