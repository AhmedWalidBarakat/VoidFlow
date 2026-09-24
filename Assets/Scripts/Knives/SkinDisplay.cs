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

        public Skins.Skin Skin => sniper ? Skins.Snipers[Mathf.Clamp(skinIndex, 0, Skins.Snipers.Length - 1)] : Skins.Knives[Mathf.Clamp(skinIndex, 0, Skins.Knives.Length - 1)];

        void OnEnable() => Build();
        void OnDisable() => Clear();

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
            offset.localPosition = sniper ? new Vector3(0f, -0.02f, -0.25f) : new Vector3(0f, -0.06f, 0f);
            var builder = new WeaponBuilder(template, gameObject.layer, true, materials);
            parts = sniper ? builder.Rifle(Skin, offset) : builder.Knife(Skin, offset);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
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
            Vector3 d = player.Position - transform.position;
            d.y = 0f;
            near = d.magnitude < useRange && !ViewModel.InputBlocked;
            var kb = Keyboard.current;
            if (near && kb != null && kb.eKey.wasPressedThisFrame)
            {
                if (!viewModel) viewModel = FindAnyObjectByType<ViewModel>();
                if (!viewModel) return;
                if (sniper) viewModel.EquipSniperSkin(skinIndex);
                else viewModel.EquipKnifeSkin(skinIndex);
            }
        }

        void OnGUI()
        {
            if (!near) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var skin = Skin;
            style.normal.textColor = Skins.RarityColor(skin.rarity);
            GUI.Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 30f), $"[E]  try on  ★ {skin.name}  ({Skins.RarityName(skin.rarity)})", style);
        }
    }
}
