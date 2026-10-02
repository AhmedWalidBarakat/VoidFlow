using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidFlow
{
    // Keeps the web build smooth on any machine. The page renders at the screen's full
    // sharpness (display scaling included, as in the editor). It starts from a web baseline (2x
    // anti-aliasing, hard shadows: nearly the same look for much less work), then, only if the
    // frame rate stays low for a while, lowers the 3D render resolution a little, a notch at a
    // time (down to about half, which on a scaled display is where it used to start), and
    // never switches back and forth: every change rebuilds
    // the render buffers, which is a hitch of its own. Web player only; the editor and desktop
    // builds keep full quality (and changing the pipeline asset in the editor would save it).
    // Frames while the course is still being built at load (slow on purpose, behind the
    // progress bar) don't count, or every player would start at the lowest resolution.
    public class AutoQuality : MonoBehaviour
    {
        const float Low = 48f;      // frames per second that count as struggling
        const float Window = 3f;    // seconds of frames judged at a time
        const float Cooldown = 5f;  // seconds between notches
        static readonly float[] Scales = { 1f, 0.9f, 0.8f, 0.7f, 0.6f, 0.5f };

        UniversalRenderPipelineAsset urp;
        EndlessCourse course;
        int notch;
        float frames, time, sinceChange;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) return;
            var go = new GameObject("AutoQuality");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoQuality>();
        }

        void Awake()
        {
            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp)
            {
                urp.msaaSampleCount = 2;
                urp.renderScale = Scales[0];
            }
            foreach (var l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional && l.shadows == LightShadows.Soft) l.shadows = LightShadows.Hard;
        }

        void Update()
        {
            if (!course) course = FindAnyObjectByType<EndlessCourse>();
            if (course && !course.Ready)
            {
                frames = time = sinceChange = 0f;
                return;
            }
            frames++;
            time += Time.unscaledDeltaTime;
            sinceChange += Time.unscaledDeltaTime;
            if (time < Window) return;
            float fps = frames / time;
            frames = time = 0f;
            if (fps < Low && sinceChange > Cooldown && notch < Scales.Length - 1 && urp)
            {
                notch++;
                sinceChange = 0f;
                urp.renderScale = Scales[notch];
            }
        }
    }
}
