using UnityEditor;
using UnityEngine;
namespace VoidFlow.EditorTools
{
    // The web release: builds the game for browsers into docs/, which GitHub Pages serves
    // (gzip with the decompression fallback, so any static host works without extra setup)
    public static class WebBuild
    {
        public static void Run()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.productName = "VoidFlow";
            PlayerSettings.companyName = "Ahmed Barakat";
            PlayerSettings.WebGL.template = "PROJECT:VoidFlow";
            // Compiled for speed at runtime (not a quick build): IL2CPP's Master configuration and
            // the web code optimized for runtime speed with link-time optimization
            PlayerSettings.SetIl2CppCompilerConfiguration(UnityEditor.Build.NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Master);
            PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSpeed);
            var settings = System.Type.GetType("UnityEditor.WebGL.UserBuildSettings, UnityEditor.WebGL.Extensions");
            var mode = System.Type.GetType("UnityEditor.WebGL.WasmCodeOptimization, UnityEditor.WebGL.Extensions");
            var codeOpt = settings?.GetProperty("codeOptimization");
            if (codeOpt != null && mode != null)
            {
                codeOpt.SetValue(null, System.Enum.Parse(mode, System.Enum.IsDefined(mode, "RuntimeSpeedLTO") ? "RuntimeSpeedLTO" : "RuntimeSpeed"));
                Debug.Log("WEBBUILD code optimization: " + codeOpt.GetValue(null));
            }
            else Debug.Log("WEBBUILD code optimization setting not found");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GrayboxBuilder.ScenePath },
                locationPathName = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "docs"),
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            Debug.Log($"WEBBUILD {report.summary.result} size={report.summary.totalSize / 1048576}MB errors={report.summary.totalErrors} time={report.summary.totalTime}");
        }
    }
}
