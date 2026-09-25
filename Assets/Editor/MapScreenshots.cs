using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VoidFlow.EditorTools
{
    // Renders a few views of the start hall from the player's camera to Logs/*.png, so it
    // can be checked without playing. (The surf bot photographs each biome of the course.)
    public static class MapScreenshots
    {
        static readonly (string name, Vector3 offset, Vector3 euler)[] Views =
        {
            ("hall_from_spawn", new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f)),
            ("hall_knife_wall", new Vector3(4f, 0f, 14f), new Vector3(5f, -70f, 0f)),
            ("hall_case_and_ramp", new Vector3(0f, 0f, 20f), new Vector3(5f, 60f, 0f)),
            ("hall_looking_back", new Vector3(0f, 0f, 40f), new Vector3(10f, 180f, 0f)),
            ("drop_edge_view", new Vector3(0f, 0f, 43f), new Vector3(20f, 0f, 0f)),
            ("viewmodel", new Vector3(0f, 0f, 8f), new Vector3(0f, 0f, 0f)),
        };

        [MenuItem("VoidFlow/Capture Map Screenshots")]
        public static void Capture()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(GrayboxBuilder.ScenePath);

            // Set up the world exactly as it is when you press Play (biome lighting, fog, ramps)
            Object.FindAnyObjectByType<EndlessCourse>()?.ResetCourse();
            var cam = Object.FindAnyObjectByType<PlayerMovement>().cameraPivot.GetComponent<Camera>();
            // Show the knife: build it and let the main camera draw its layer for the photo
            // (overlay cameras don't render on their own)
            var viewModel = cam.GetComponent<ViewModel>();
            if (viewModel) viewModel.BuildNow();
            var spawn = GameObject.Find("Spawn").transform;
            Directory.CreateDirectory("Logs");

            var rt = new RenderTexture(1280, 720, 24);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            void Shoot(string name, Vector3 offset, Vector3 euler)
            {
                cam.transform.SetPositionAndRotation(spawn.position + offset + Vector3.up * 1.63f, Quaternion.Euler(euler));
                cam.targetTexture = rt;
                // The first headless render after changes can come out with mixed-up materials
                // (the game itself is fine), so render a throwaway frame first
                for (int warm = 0; warm < 4; warm++) cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"Logs/{name}.png", tex.EncodeToPNG());
            }
            foreach (var (name, offset, euler) in Views) Shoot(name, offset, euler);
            if (viewModel)
            {
                viewModel.Equip(ViewModel.SniperSlot);
                Shoot("viewmodel_sniper", new Vector3(0f, 0f, 8f), Vector3.zero);
                viewModel.PreviewSniperCycle(0.65f);
                Shoot("viewmodel_sniper_bolt", new Vector3(0f, 0f, 8f), Vector3.zero);
                viewModel.PreviewSniperCycle(-1f);
                for (int i = 1; i < Skins.Snipers.Length; i++)
                {
                    viewModel.EquipSniperSkin(i);
                    Shoot($"skin_sniper_{i}", new Vector3(0f, 0f, 8f), Vector3.zero);
                }
                viewModel.EquipSniperSkin(0);
                for (int i = 0; i < Skins.Knives.Length; i++)
                {
                    viewModel.EquipKnifeSkin(i);
                    Shoot($"skin_knife_{i}", new Vector3(0f, 0f, 8f), Vector3.zero);
                    viewModel.PreviewKnifeInspect(0.6f);
                    Shoot($"skin_knife_{i}_inspect", new Vector3(0f, 0f, 8f), Vector3.zero);
                    viewModel.PreviewKnifeInspect(-1f);
                }
                // Per-knife inspects, mid-move
                foreach (var (skin, t) in new[] { (7, 0.2f), (7, 0.45f), (7, 0.7f), (7, 1.0f), (7, 1.3f), (7, 1.55f) })
                {
                    viewModel.EquipKnifeSkin(skin);
                    viewModel.PreviewKnifeInspect(t);
                    Shoot($"inspect_{skin}_{t:0.0}", new Vector3(0f, 0f, 8f), Vector3.zero);
                }
                viewModel.PreviewKnifeInspect(-1f);
                // Sword draw from the sheath, and the sheath while holding the sniper
                foreach (float t in new[] { 0.2f, 0.4f, 0.7f })
                {
                    viewModel.PreviewSwordDraw(t);
                    Shoot($"sworddraw_{t:0.0}", new Vector3(0f, 0f, 8f), Vector3.zero);
                }
                viewModel.Equip(ViewModel.SniperSlot);
                Shoot("sheath_with_sniper", new Vector3(0f, 0f, 8f), Vector3.zero);
                viewModel.Equip(ViewModel.KnifeSlot);
                viewModel.EquipKnifeSkin(0);
            }
            // The skeet range from the start button
            var range = Object.FindAnyObjectByType<SkeetRange>();
            if (range)
            {
                Vector3 from = range.startButton.position + new Vector3(-1.5f, 0.55f, 1.2f);
                cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(range.launcher.position + Vector3.up * 1.5f - from));
                cam.targetTexture = rt;
                for (int warm = 0; warm < 4; warm++) cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes("Logs/skeet_range.png", tex.EncodeToPNG());
            }
            // The cases, from the lane
            foreach (var station in System.Array.Empty<CaseStation>())
            {
                Vector3 p = station.transform.position;
                cam.transform.SetPositionAndRotation(p + new Vector3(-4.2f, 1.2f, -1.8f), Quaternion.LookRotation(p + Vector3.up * 1f - (p + new Vector3(-4.2f, 1.2f, -1.8f))));
                cam.targetTexture = rt;
                cam.Render();
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"Logs/case_{(station.sniperCase ? "sniper" : "knife")}.png", tex.EncodeToPNG());
            }
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("VoidFlow: screenshots saved to Logs/");
        }
    }
}
