using System.Collections.Generic;
using UnityEngine;

namespace VoidFlow
{
    // Holding F on a Void sword eases it into the show pose: raised up sideways before you, the
    // whole blade on show, swaying a little and turning in the light, for as long as F is held
    // (after the Shattered Crystal Sword's hold). A Void scythe is let out on a chain instead,
    // like a kusarigama, and whirled round the hand, the chain paying out from the fist to the
    // butt of the shaft, then reeled back in when F is let go.
    public partial class ViewModel
    {
        float showcase;   // 0..1: how far into the show pose (or the chain whirl)
        float chainAngle; // degrees the chained scythe has gone round
        readonly List<Transform> chainLinks = new();
        Material chainMetal;

        const int ChainLinks = 14;

        Pose ShowcaseHand(float time)
        {
            if (routine != null && routine.chain)
                return new Pose(new Vector3(0.025f, -0.075f, 0.44f), RaiseQ); // (out in front and to the middle, so the whole circle shows)
            var q = TurnQ(Lean(RaiseQ, -12f + Mathf.Sin(time * 1.1f) * 4f), Mathf.Sin(time * 0.7f) * 16f);
            var p = RaiseAt + new Vector3(Mathf.Sin(time * 0.9f) * 0.004f, Mathf.Sin(time * 1.3f) * 0.003f, 0f);
            return new Pose(p, q);
        }

        // The scythe whirling round the fist on its chain, `shown` of the way out
        Pose ChainPose(Pose right, Pose held, float shown, float size)
        {
            Vector3 center = right.p + right.q * GripFront;
            float a = chainAngle * Mathf.Deg2Rad;
            // round in a circle facing you, tilted a touch so it reads as a circle, not a line
            // (flattened below, so it sweeps low across the view instead of dropping off the bottom)
            float up = Mathf.Sin(a);
            Vector3 dir = new Vector3(Mathf.Cos(a), up > 0f ? up * 0.9f : up * 0.28f, up > 0f ? up * 0.3f : -up * 0.5f).normalized; // (the low side swung away from you, behind the arm, which turns see-through)
            float reach = 0.075f * shown, butt = 0.26f * size;
            var q = Quaternion.LookRotation(Vector3.forward, dir); // the shaft pointing out along the chain, the flat to you
            var whirl = new Pose(center + dir * (reach + butt), q);
            ShowChain(center, center + dir * reach, shown);
            return Pose.Blend(held, whirl, shown);
        }

        void ShowChain(Vector3 from, Vector3 to, float shown)
        {
            if (chainLinks.Count == 0)
            {
                chainMetal = Make(new Color(0.16f, 0.16f, 0.18f), 0.8f, 0.9f);
                Material glow = Make(Color.black, 0.2f, 0f);
                glow.EnableKeyword("_EMISSION");
                glow.SetColor("_EmissionColor", Skins.HueOf(Skins.Knives[knifeSkin]) * 2f);
                materials.Add(glow);
                for (int i = 0; i < ChainLinks; i++)
                {
                    var link = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Kill(link.GetComponent<Collider>());
                    link.name = "Chain Link";
                    link.layer = Layer;
                    link.transform.SetParent(hand.parent, false);
                    var r = link.GetComponent<MeshRenderer>();
                    r.sharedMaterial = i % 4 == 3 ? glow : chainMetal; // (every fourth link glowing in the scythe's colour)
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    chainLinks.Add(link.transform);
                }
            }
            Vector3 along = to - from;
            float length = along.magnitude;
            bool on = shown > 0.02f && length > 0.005f;
            Vector3 dir = on ? along / length : Vector3.up;
            // (a slight sag toward the middle, as a whirled chain has)
            Vector3 sag = Vector3.Cross(dir, Vector3.forward).normalized * (0.012f * shown);
            for (int i = 0; i < chainLinks.Count; i++)
            {
                var link = chainLinks[i];
                link.gameObject.SetActive(on);
                if (!on) continue;
                float f = (i + 0.5f) / chainLinks.Count;
                Vector3 at = Vector3.Lerp(from, to, f) + sag * Mathf.Sin(f * Mathf.PI);
                float linkLength = Mathf.Min(length / chainLinks.Count * 1.25f, 0.016f);
                link.SetLocalPositionAndRotation(at, Quaternion.LookRotation(dir, Vector3.forward) * Quaternion.Euler(0f, 0f, i % 2 == 0 ? 0f : 90f));
                link.localScale = new Vector3(0.006f, 0.0022f, linkLength);
            }
        }

        void HideChain()
        {
            foreach (var link in chainLinks) if (link) link.gameObject.SetActive(false);
        }

        // Rebuilt with each weapon (the glowing links take its colour)
        void ResetChain()
        {
            foreach (var link in chainLinks) if (link) Kill(link.gameObject);
            chainLinks.Clear();
            showcase = 0f;
        }
    }
}
