using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidFlow
{
    // The web build's quality. The page renders at the screen's full sharpness (display scaling
    // included, as in the editor), from a web baseline (2x anti-aliasing, hard shadows: nearly
    // the same look for much less work), with the shadows on or off as the player has them (no
    // ambient occlusion: the sun's shadows give the depth). The 3D view's resolution is the
    // player's own setting (3D RESOLUTION in the settings, full by default) and nothing ever
    // lowers it behind their back: it used to drop a notch whenever the frame rate dipped, which
    // a browser does on its own when the tab sits idle or in the background, so it only ever
    // went down. Web player only; the editor and desktop builds keep full quality (and changing
    // the pipeline asset in the editor would save it).
    public class AutoQuality : MonoBehaviour
    {
        UniversalRenderPipelineAsset urp;
        float applied = -1f;
        int shadowsApplied = -1;
        Light[] suns;

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
            if (urp) urp.msaaSampleCount = 2;
            suns = System.Array.FindAll(FindObjectsByType<Light>(), l => l.type == LightType.Directional);
        }

        void Update()
        {
            // The sun's shadows (hard-edged on the web, like the maps') and the shade where
            // surfaces meet, each as the player has them
            int shadows = GameSettings.Shadows ? 1 : 0;
            if (shadows != shadowsApplied)
            {
                shadowsApplied = shadows;
                foreach (var l in suns) if (l) l.shadows = shadows == 1 ? LightShadows.Hard : LightShadows.None;
            }
            // (set only when it changes: every change rebuilds the render buffers)
            float want = GameSettings.RenderScale;
            if (!urp || Mathf.Approximately(want, applied)) return;
            applied = want;
            urp.renderScale = want;
        }
    }
}
