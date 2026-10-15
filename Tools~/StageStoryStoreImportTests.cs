// 仅装入独立验证工程，不随商店包分发。
using System.Collections;
using System.Linq;
using AIBuilder.CreatureStage;
using AIBuilder.EditorTools.CreatureStage;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

public class StageStoryStoreImportTests
{
    private const string Folder = "Assets/SceneStory/Samples/StoreExamples/";
    [TestCase("MultiActor", 3)]
    [TestCase("GroundLayers", 2)]
    [TestCase("AudioAndFacing", 1)]
    public void ImportedScene_RetainsBindingsAndFiniteSampling(string name, int count)
    {
        var scene = EditorSceneManager.OpenScene(Folder + name + ".unity");
        foreach (var root in scene.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), t.name);
        var d = Object.FindObjectOfType<PlayableDirector>();
        var tracks = ((TimelineAsset)d.playableAsset).GetOutputTracks().OfType<StagePerformanceTrack>().ToArray();
        Assert.AreEqual(count, tracks.Length);
        foreach (var track in tracks)
        {
            Assert.IsNotNull(d.GetGenericBinding(track));
            Assert.IsNotNull(d.GetGenericBinding(track.animationTrack));
            foreach (var clip in track.GetClips())
            {
                var data = (StagePerformanceClip)clip.asset;
                if (data.mode == StagePerformanceMode.Move) Assert.IsNotNull(data.route.Resolve(d));
                else Assert.IsNotNull(data.station.Resolve(d));
            }
        }
        for (int i = 0; i <= 48; i++)
        {
            d.time = d.duration * i / 48; d.Evaluate();
            foreach (var actor in Object.FindObjectsOfType<StageActorDriver>())
            {
                var p = actor.transform.position;
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z));
                Assert.Less(p.sqrMagnitude, 10000);
            }
        }
        d.Stop();
        if (name == "GroundLayers")
        {
            var step = ((StagePerformanceClip)tracks[0].GetClips().Single().asset).route.Resolve(d);
            var hill = ((StagePerformanceClip)tracks[1].GetClips().Single().asset).route.Resolve(d);
            step.SampleForGround(.5, true, out var a, out _);
            hill.SampleForGround(.5, true, out var b, out _);
            Assert.AreEqual(1, a.y, .03f); Assert.AreEqual(3, b.y, .03f);
        }
        // 保存并重开，验证引用不是仅存在于创建时内存中。
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene(Folder + name + ".unity");
        Assert.AreEqual(count, Object.FindObjectsOfType<StageActorDriver>().Length);
    }
    [Test]
    public void LanguageSwitch_UsesEnglishAndChineseWithoutRenamingActor()
    {
        const string key = "SceneStory.Editor.Chinese";
        var existed = EditorPrefs.HasKey(key); var old = EditorPrefs.GetBool(key);
        var obj = new GameObject("用户角色");
        try
        {
            EditorPrefs.SetBool(key, false); Assert.AreEqual("Scene Story", StageLanguage.Text("场景剧情"));
            Assert.IsNotNull(Resources.Load<TextAsset>("SceneStoryTutorialEnglish"));
            EditorPrefs.SetBool(key, true); Assert.AreEqual("场景剧情", StageLanguage.Text("场景剧情"));
            Assert.AreEqual("用户角色", obj.name);
        }
        finally
        {
            Object.DestroyImmediate(obj);
            if (existed) EditorPrefs.SetBool(key, old); else EditorPrefs.DeleteKey(key);
        }
    }
    [UnityTest]
    public IEnumerator AudioExample_PlayModeAdvancesActorAndSchedulesAudio()
    {
        EditorSceneManager.OpenScene(Folder + "AudioAndFacing.unity");
        yield return new EnterPlayMode();
        var d = Object.FindObjectOfType<PlayableDirector>();
        // EditMode 协程进入 Play 后仍由编辑器测试器驱动，使用 null 等待实际 Director 时钟。
        var deadline = System.DateTime.UtcNow.AddSeconds(30);
        while (d.time < 1.25 && System.DateTime.UtcNow < deadline) yield return null;
        Assert.Greater(d.time, .5);
        Assert.Greater(Object.FindObjectOfType<StageActorDriver>().transform.position.z, .3f);
        Assert.IsTrue(d.GetComponent<AudioSource>().isPlaying, "Native Timeline audio must be scheduled");
        yield return new ExitPlayMode();
    }
}
