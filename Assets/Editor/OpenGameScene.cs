using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoidFlow.EditorTools
{
    // Keeps the editor pointed at the game: pressing Play always runs the VoidFlow scene,
    // whatever scene is open, and if the editor starts on an empty untitled scene it opens
    // VoidFlow instead.
    [InitializeOnLoad]
    static class OpenGameScene
    {
        static OpenGameScene()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GrayboxBuilder.ScenePath);
                if (scene == null) return;
                EditorSceneManager.playModeStartScene = scene;

                Scene active = SceneManager.GetActiveScene();
                if (!EditorApplication.isPlayingOrWillChangePlaymode && string.IsNullOrEmpty(active.path) && !active.isDirty)
                    EditorSceneManager.OpenScene(GrayboxBuilder.ScenePath);
            };
        }
    }
}
