// 渲染真实示例 Timeline 的媒体，不导出到商品；不修改用户工程。
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;

public static class SceneStoryMarketingCapture
{
    public static void Run()
    {
        var output = Environment.GetEnvironmentVariable("SCENE_STORY_MEDIA");
        Directory.CreateDirectory(output);
        foreach (var name in new[] { "MultiActor", "GroundLayers", "AudioAndFacing" })
        {
            EditorSceneManager.OpenScene("Assets/SceneStory/Samples/StoreExamples/" + name + ".unity");
            var director = UnityEngine.Object.FindObjectsOfType<PlayableDirector>().Single();
            var camera = Camera.main;
            var frames = Path.Combine(output, name); Directory.CreateDirectory(frames);
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                for (int i = 0; i < 240; i++)
                {
                    director.time = i / 30.0; director.Evaluate();
                    camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                    var bytes = image.EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(frames, i.ToString("D4") + ".png"), bytes);
                    if (i == 120) File.WriteAllBytes(Path.Combine(output, name + ".png"), bytes);
                }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; director.Stop();
                UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
            }
        }
        File.WriteAllText(Path.Combine(output, "Capture.txt"), "Unity 2022.3.62f3 camera renders from included sample scenes; Timeline evaluated at 30fps. 240 frames per scene. Not a screen recording or human interaction test.");
    }
}
