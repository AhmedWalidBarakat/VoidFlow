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
