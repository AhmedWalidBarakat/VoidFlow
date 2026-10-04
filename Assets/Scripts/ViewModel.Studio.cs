using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidFlow
{
    // The weapons in your hands are lit like a studio, not like the zone: a soft key light from
    // the upper left, a cool fill, and reflections of a bright studio, so a gold sword is gold
    // in a red hall and a pink karambit is pink in a green one. A quarter of the zone's own
    // ambient colour is kept so the weapon still sits in the scene. Three parts:
    //  - ambient: each viewmodel renderer gets its own light probe (spherical harmonics)
    //  - reflections: a small probe round the camera showing a studio, more important than the
    //    sky probe (so only the viewmodel, right by the camera, uses it)
    //  - the sun: while the viewmodel camera draws, the sun shines neutral white
    public partial class ViewModel
    {
        ReflectionProbe studioProbe;
        Cubemap studioCube;
        MaterialPropertyBlock studioBlock;
        readonly List<Renderer> studioLit = new();
        float studioAt = -99f;
        Color studioAmbient = new(-1f, 0f, 0f);
        Light sunLight;
        Color sunColor; float sunIntensity; bool sunSwapped;

        static readonly Color StudioSky = new(0.62f, 0.64f, 0.7f), StudioEquator = new(0.42f, 0.42f, 0.45f), StudioGround = new(0.16f, 0.15f, 0.16f);
        const float ZoneShare = 0.25f;

        void SetupStudio()
        {
            studioCube = new Cubemap(32, TextureFormat.RGBA32, false) { name = "Studio" };
            for (int f = 0; f < 6; f++)
            {
                var face = (CubemapFace)f;
                var px = new Color[32 * 32];
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        Vector3 d = FaceDirection(face, (x + 0.5f) / 32f * 2f - 1f, (y + 0.5f) / 32f * 2f - 1f);
                        // bright overhead, a soft horizon, dark floor, a big softbox up front-left
                        float up = d.y;
                        Color c = up > 0f ? Color.Lerp(new Color(0.55f, 0.56f, 0.6f), new Color(0.95f, 0.95f, 1f), up) : Color.Lerp(new Color(0.5f, 0.5f, 0.52f), new Color(0.08f, 0.08f, 0.09f), -up * 1.6f);
                        float box = Vector3.Dot(d, new Vector3(-0.5f, 0.6f, 0.62f).normalized);
                        if (box > 0.86f) c = Color.Lerp(c, Color.white * 1.2f, Mathf.InverseLerp(0.86f, 0.93f, box));
                        float rim = Vector3.Dot(d, new Vector3(0.8f, 0.2f, -0.55f).normalized);
                        if (rim > 0.9f) c = Color.Lerp(c, new Color(0.75f, 0.85f, 1f), Mathf.InverseLerp(0.9f, 0.96f, rim));
                        px[y * 32 + x] = c;
                    }
                studioCube.SetPixels(px, face);
            }
            studioCube.Apply();
            studioProbe = new GameObject("Studio Reflections").AddComponent<ReflectionProbe>();
            studioProbe.transform.SetParent(transform, false);
            studioProbe.mode = ReflectionProbeMode.Custom;
            studioProbe.customBakedTexture = studioCube;
            studioProbe.size = Vector3.one * 1.6f; // (just round the camera: only the viewmodel's in it)
            studioProbe.importance = 50;
            studioProbe.boxProjection = false;
            studioBlock = new MaterialPropertyBlock();
            RenderPipelineManager.beginCameraRendering += StudioBegin;
            RenderPipelineManager.endCameraRendering += StudioEnd;
        }

        void StudioDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= StudioBegin;
            RenderPipelineManager.endCameraRendering -= StudioEnd;
            if (studioCube) Kill(studioCube);
        }

        static Vector3 FaceDirection(CubemapFace face, float u, float v) => (face switch
        {
            CubemapFace.PositiveX => new Vector3(1f, -v, -u),
            CubemapFace.NegativeX => new Vector3(-1f, -v, u),
            CubemapFace.PositiveY => new Vector3(u, 1f, v),
            CubemapFace.NegativeY => new Vector3(u, -1f, -v),
            CubemapFace.PositiveZ => new Vector3(u, -v, 1f),
            _ => new Vector3(-u, -v, -1f),
        }).normalized;

        // Every few frames: the viewmodel's renderers (weapons are rebuilt) get the studio's
        // ambient, mixed with a little of the zone's
        void UpdateStudio()
        {
            if (studioBlock == null || !anchor) return;
            Color zone = RenderSettings.ambientSkyColor;
            bool changed = (zone - studioAmbient).maxColorComponent > 0.01f || (studioAmbient - zone).maxColorComponent > 0.01f;
            if (!changed && Time.unscaledTime < studioAt) return;
            studioAt = Time.unscaledTime + 0.25f;
            studioAmbient = zone;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(Color.Lerp(StudioEquator, RenderSettings.ambientEquatorColor, ZoneShare));
            // (sky above and ground below as two broad lights)
            sh.AddDirectionalLight(Vector3.up, Color.Lerp(StudioSky, RenderSettings.ambientSkyColor, ZoneShare) - StudioEquator * 0.5f, 0.6f);
            sh.AddDirectionalLight(Vector3.down, Color.Lerp(StudioGround, RenderSettings.ambientGroundColor, ZoneShare), 0.3f);
            // a soft key light from the upper left, toward the camera's view
            sh.AddDirectionalLight(transform.TransformDirection(new Vector3(-0.5f, 0.6f, 0.62f)).normalized, new Color(1f, 0.97f, 0.92f), 0.35f);
            studioBlock.Clear();
            studioBlock.CopySHCoefficientArraysFrom(new List<SphericalHarmonicsL2> { sh });
            studioLit.Clear();
            anchor.GetComponentsInChildren(true, studioLit);
            foreach (var r in studioLit)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                r.lightProbeUsage = LightProbeUsage.CustomProvided;
                r.SetPropertyBlock(studioBlock);
            }
        }

        // While the viewmodel camera draws, the sun is a neutral white
        void StudioBegin(ScriptableRenderContext context, Camera cam)
        {
            if (cam != overlay) return;
            if (!sunLight) sunLight = RenderSettings.sun ? RenderSettings.sun : FindAnyObjectByType<EndlessCourse>()?.sun;
            if (!sunLight) return;
            sunColor = sunLight.color; sunIntensity = sunLight.intensity;
            sunLight.color = Color.Lerp(new Color(1f, 0.97f, 0.93f), sunColor, ZoneShare);
            sunLight.intensity = Mathf.Lerp(1.05f, sunIntensity, ZoneShare);
            sunSwapped = true;
        }

        void StudioEnd(ScriptableRenderContext context, Camera cam)
        {
            if (!sunSwapped || !sunLight) return;
            sunLight.color = sunColor; sunLight.intensity = sunIntensity;
            sunSwapped = false;
        }
    }
}
