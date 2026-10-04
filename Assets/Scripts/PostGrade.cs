using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidFlow
{
    // Turns the scene's colour grade (filmic tone curve, colour, vignette) on or off with the
    // player's setting. The grade itself is built into the scene's volume, so the web build
    // keeps those effects.
    public class PostGrade : MonoBehaviour
    {
        VolumeProfile profile;
        bool applied;
        bool state;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            var volume = FindAnyObjectByType<Volume>();
            if (!volume || !volume.sharedProfile) return;
            var g = new GameObject("PostGrade").AddComponent<PostGrade>();
            g.profile = Instantiate(volume.sharedProfile); // (a copy: the asset itself stays as built)
            volume.profile = g.profile;
        }

        void Update()
        {
            bool want = GameSettings.Grading;
            if (applied && want == state) return;
            applied = true;
            state = want;
            if (profile.TryGet(out Tonemapping tone)) tone.active = want;
            if (profile.TryGet(out ColorAdjustments colour)) colour.active = want;
            if (profile.TryGet(out Vignette vignette)) vignette.active = want;
        }
    }
}
