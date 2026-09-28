using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // Living flames on a Void weapon: soft flame sprites (Kenney's CC0 flame art, Resources/
    // Flames) licking up from points along the blade or barrel. Each tongue is born small at its
    // point, rises and swells while its artwork flickers, then fades, on its own rhythm, so the
    // fire never looks like a loop. Dark smoky licks underneath, bright tongues in the weapon's
    // color on top. `boost` (0..1, eased) makes them roar, for inspects and reveals.
    [ExecuteAlways]
    public class WeaponFlames : MonoBehaviour
    {
        public Color hue = new(1f, 0.3f, 0.1f);
        public float size = 0.03f;   // tongue height in the weapon's own units
        public float rise = 0.05f;   // how far a tongue climbs before it fades
        [Range(0f, 1f)] public float boost;
        // Only the bright tongues, no dark smoke (item pictures, where smoke reads as blots)
        public bool brightOnly;
        public Material template;
        // When set, flames face this camera instead of the main one (item pictures)
        public static Camera Facing;

        readonly List<Vector3> points = new();
        readonly List<(MeshRenderer r, int point, float phase, float speed, bool bright)> tongues = new();
        static Texture2D[] frames;
        Material mat;
        MaterialPropertyBlock props;
        float shown;

        public void Setup(Material template, IList<Vector3> at, Color hue, float size, float rise, int perPoint = 3)
        {
            this.template = template;
            this.hue = hue;
            this.size = size;
            this.rise = rise;
            points.Clear();
            points.AddRange(at);
            Build(perPoint);
        }

        void Build(int perPoint)
        {
            foreach (var t in tongues) if (t.r) WeaponBuilder.Kill(t.r.gameObject);
            tongues.Clear();
            if (frames == null)
            {
                frames = new Texture2D[6];
                for (int f = 0; f < 6; f++) frames[f] = Resources.Load<Texture2D>($"Flames/flame_0{f + 1}");
            }
            if (!mat && template)
            {
                mat = ViewModel.MakeTransparent(new Material(template));
                mat.EnableKeyword("_EMISSION");
                mat.SetFloat("_Smoothness", 0f);
                mat.SetFloat("_Metallic", 0f);
            }
            props ??= new MaterialPropertyBlock();
            var random = new System.Random(points.Count * 7919 + perPoint);
            for (int p = 0; p < points.Count; p++)
            for (int k = 0; k < perPoint; k++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Flame";
                WeaponBuilder.Kill(quad.GetComponent<Collider>());
                quad.layer = gameObject.layer;
                quad.hideFlags = HideFlags.DontSave;
                quad.transform.SetParent(transform, false);
                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                tongues.Add((r, p, (float)random.NextDouble(), 1.1f + (float)random.NextDouble() * 0.9f, k != 0));
            }
        }

        void OnDestroy()
        {
            if (mat) WeaponBuilder.Kill(mat);
        }

        void LateUpdate()
        {
            if (tongues.Count == 0 || frames == null) return;
            float time = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartupAsDouble;
            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
            shown = Mathf.MoveTowards(shown, boost, dt * 3f);
            float roar = 1f + shown * 0.5f;
            var cam = Facing ? Facing : Camera.main;
            // Flames rise in the world, whichever way the weapon is turned
            Vector3 up = transform.InverseTransformDirection(Vector3.up);
            foreach (var (r, point, phase, speed, bright) in tongues)
            {
                if (!r) continue;
                r.enabled = bright || !brightOnly;
                float u = Mathf.Repeat(time * speed * (1f + shown * 0.5f) + phase, 1f);
                Vector3 at = points[point] + up * (u * rise * roar) + new Vector3(Mathf.Sin(time * 5f + phase * 20f), 0f, Mathf.Cos(time * 4f + phase * 13f)) * (size * 0.12f);
                r.transform.localPosition = at;
                if (cam) r.transform.rotation = Quaternion.LookRotation(r.transform.position - cam.transform.position, cam.transform.up)
                    * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 3f + phase * 9f) * 12f);
                float grow = (0.55f + u * 0.6f) * roar * (bright ? 1f : 1.25f);
                r.transform.localScale = new Vector3(size * grow * 0.75f, size * grow, 1f);
                int frame = bright ? 4 + (int)(time * 12f + phase * 7f) % 2 : (int)(time * 10f + phase * 13f) % 4;
                float alpha = Mathf.Sin(u * Mathf.PI) * (bright ? 0.9f : 0.75f);
                props.SetTexture("_BaseMap", frames[frame]);
                props.SetTexture("_EmissionMap", frames[frame]);
                if (bright)
                {
                    props.SetColor("_BaseColor", new Color(hue.r, hue.g, hue.b, alpha));
                    props.SetColor("_EmissionColor", hue * (1.4f + shown * 1.2f));
                }
                else
                {
                    props.SetColor("_BaseColor", new Color(0.02f, 0.01f, 0.015f, alpha));
                    props.SetColor("_EmissionColor", hue * 0.08f);
                }
                r.SetPropertyBlock(props);
            }
        }
    }
}
