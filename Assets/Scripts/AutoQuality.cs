using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidFlow
{
    // Keeps the web build smooth on any machine: watches the frame rate and, when it sags,
    // steps the picture down a notch at a time (less anti-aliasing, a slightly lower render
    // resolution, hard shadows, then no shadows and no bloom), and steps back up when there's
    // room again. Only in the web player: the editor and desktop builds keep full quality
    // (and changing the pipeline asset in the editor would save the change into the project).
    public class AutoQuality : MonoBehaviour
    {
        const float Low = 50f, High = 58f;     // frames per second that step down / allow a step up
        const float Window = 1.5f;             // seconds of frames judged at a time
        const float UpAfter = 8f;              // seconds of headroom before stepping back up

        UniversalRenderPipelineAsset urp;
        Light sun;
        Volume[] volumes;
        int level;
        float frames, time, headroom;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) return;
            var go = new GameObject("AutoQuality");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoQuality>();
        }

        void Awake()
        {
            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            foreach (var l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional) sun = l;
            volumes = FindObjectsByType<Volume>();
            Apply();
        }

        void Update()
        {
            frames++;
            time += Time.unscaledDeltaTime;
            if (time < Window) return;
            float fps = frames / time;
            frames = time = 0f;
            if (fps < Low && level < 5) { level++; headroom = 0f; Apply(); }
            else if (fps > High && level > 0)
            {
                headroom += Window;
                if (headroom >= UpAfter) { level--; headroom = 0f; Apply(); }
            }
            else headroom = 0f;
        }

        void Apply()
        {
            if (urp)
            {
                urp.msaaSampleCount = level == 0 ? 4 : level == 1 ? 2 : 1;
                urp.renderScale = level < 2 ? 1f : level < 4 ? 0.85f : 0.72f;
                urp.shadowDistance = level < 4 ? 50f : 30f;
            }
            if (sun) sun.shadows = level < 3 ? LightShadows.Soft : level < 5 ? LightShadows.Hard : LightShadows.None;
            foreach (var v in volumes) if (v) v.enabled = level < 5;
        }
    }
}
