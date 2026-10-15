// 在全新导入候选包的验证工程中执行。
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;

public static class SceneStoryStorePlayerBuild
{
    public static void Run()
    {
        var output=Environment.GetEnvironmentVariable("SCENE_STORY_EVIDENCE");Directory.CreateDirectory(output);
        foreach(var name in new[]{"MultiActor","GroundLayers","AudioAndFacing"})
        {
            EditorSceneManager.OpenScene("Assets/SceneStory/Samples/StoreExamples/"+name+".unity");
            var d=UnityEngine.Object.FindObjectOfType<PlayableDirector>();d.time=4;d.Evaluate();
            var camera=Camera.main;
            var target=new RenderTexture(1280,720,24);var previous=RenderTexture.active;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            }
            finally
            {camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);d.Stop();}
        }
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=new[]{"Assets/SceneStory/Samples/StoreExamples/AudioAndFacing.unity"},
            locationPathName="Build/SceneStoryCheck.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        File.WriteAllText(Path.Combine(output,"Build.txt"),result.summary.result+"; errors="+result.summary.totalErrors+"; warnings="+result.summary.totalWarnings);
        if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Player build failed");
    }
}
