using UnityEngine;

namespace VoidFlow
{
    // Keeps a sign's depth-tested text material (VoidFlow/Text3D) showing its font's current
    // glyph atlas: fonts draw their letters into a texture at runtime and rebuild it when new
    // letters are needed, so the material follows along
    [RequireComponent(typeof(TextMesh))]
    public class TextDepth : MonoBehaviour
    {
        Font font;
        Renderer target;

        void OnEnable()
        {
            font = GetComponent<TextMesh>().font;
            target = GetComponent<Renderer>();
            Font.textureRebuilt += Rebuilt;
            Sync();
        }

        void OnDisable() => Font.textureRebuilt -= Rebuilt;

        void Rebuilt(Font f) { if (f == font) Sync(); }

        void Sync()
        {
            if (font && target && target.sharedMaterial && font.material)
                target.sharedMaterial.mainTexture = font.material.mainTexture;
        }
    }
}
