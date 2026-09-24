using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VoidFlow.EditorTools
{
    // Renders a few views of the map from the player's camera to Logs/*.png, so the map
    // can be checked without playing.
    public static class MapScreenshots
    {
        const string ScenePath = "Assets/Scenes/Surf_Graybox.unity";

        static readonly (string name, Vector3 offset, Vector3 euler)[] Views =
        {
            ("hall_from_spawn", new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            ("hall_knife_wall", new Vector3(4f, 0f, 14f), new Vector3(5f, -70f, 0f)),
            ("hall_case_and_ramp", new Vector3(0f, 0f, 20f), new Vector3(5f, 60f, 0f)),
            ("hall_looking_back", new Vector3(0f, 0f, 40f), new Vector3(10f, 180f, 0f)),
            ("drop_edge_view", new Vector3(0f, 0f, 43f), new Vector3(20f, 0f, 0f)),
        };

        [MenuItem("VoidFlow/Capture Map Screenshots")]
        public static void Capture()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);

            var cam = Object.FindAnyObjectByType<Camera>();
            var spawn = GameObject.Find("Spawn").transform;
            Directory.CreateDirectory("Logs");

            var rt = new RenderTexture(1280, 720, 24);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            foreach (var (name, offset, euler) in Views)
            {
                cam.transform.SetPositionAndRotation(spawn.position + offset + Vector3.up * 1.63f, Quaternion.Euler(euler));
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"Logs/{name}.png", tex.EncodeToPNG());
            }
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("VoidFlow: screenshots saved to Logs/");
        }
    }
}
