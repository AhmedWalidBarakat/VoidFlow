using UnityEngine;

namespace VoidFlow
{
    // A sign that turns (round its upright only) to face whoever is looking, so it reads the
    // right way round from any side: the bhop challenge's exit zones, seen from all over a room
    public class FaceViewer : MonoBehaviour
    {
        static Camera view;

        void LateUpdate()
        {
            if (!view || !view.isActiveAndEnabled) view = Camera.main;
            if (!view) return;
            Vector3 d = transform.position - view.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d);
        }
    }
}
