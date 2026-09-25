using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // The Void Case on show in the hall: the case itself turning slowly over a pedestal,
    // flames and all. It's only to look at; Void Cases are earned (see the sign beside it) and
    // opened from the inventory. Built when the scene loads, not saved into it.
    [ExecuteAlways]
    public class VoidCaseDisplay : MonoBehaviour
    {
        [Tooltip("Any URP Lit material; the case's materials are made from it")]
        public Material template;
        public float scale = 1.6f;

        Transform model;
        WeaponParts parts;
        readonly List<Material> materials = new();

        void OnEnable() => Build();
        void OnDisable() => Clear();

        void Start()
        {
            if (Application.isPlaying) FxLibrary.Sparkle(transform, UiArt.CasePink, 1.2f, 6f);
        }

        void Build()
        {
            Clear();
            if (!template) return;
            model = new GameObject("Void Case Model").transform;
            model.SetParent(transform, false);
            model.localScale = Vector3.one * scale;
            parts = new WeaponBuilder(template, gameObject.layer, true, materials).VoidCaseModel(model);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        void Clear()
        {
            if (model)
            {
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh && mf.sharedMesh.name is "Blade" or "Crystal" or "Ring") WeaponBuilder.Kill(mf.sharedMesh);
                WeaponBuilder.Kill(model.gameObject);
            }
            model = null;
            parts = null;
            foreach (var m in materials) WeaponBuilder.Kill(m);
            materials.Clear();
        }

        void Update()
        {
            if (!model) return;
            float time = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartupAsDouble;
            model.localRotation = Quaternion.Euler(0f, time * 25f, 0f);
            model.localPosition = Vector3.up * (Mathf.Sin(time * 1.2f) * 0.08f);
            parts?.Animate(time, -1f);
        }
    }
}
