using System.Reflection;
using AIBuilder.CreatureStage;
using AIBuilder.EditorTools.CreatureStage;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIBuilder.EditorTools.Tests
{
    public sealed class StageGroundBindingWindowTests
    {
        private Scene scene;
        private GameObject root;
        private StageGroundBindingWindow window;
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private StageGroundSettings WindowSettings => (StageGroundSettings)typeof(StageGroundBindingWindow).GetField("settings", Flags).GetValue(window);

        [SetUp] public void Setup()
        {
            scene = SceneManager.GetActiveScene();
            root = new GameObject("GroundWindowTest");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<StageRoute>();
            Selection.activeObject = null;
        }
        [TearDown] public void Cleanup()
        {
            AnimationMode.StopAnimationMode();
            if (window != null) window.Close();
            Object.DestroyImmediate(root);
            Undo.ClearAll();
        }
        [Test] public void PreviewReadsExistingSettingsWithoutSelection()
        {
            var expected = root.AddComponent<StageGroundSettings>();
            AnimationMode.StartAnimationMode();
            Assert.That(StageGroundRepair.GetSettings(scene), Is.SameAs(expected));
            StageGroundBindingWindow.Open(scene);
            window = EditorWindow.GetWindow<StageGroundBindingWindow>();
            Assert.That(WindowSettings, Is.SameAs(expected));
            Assert.That(Selection.activeObject, Is.Null);
        }
        [Test] public void MissingSettingsRecoverAfterPreviewWithoutSelection()
        {
            AnimationMode.StartAnimationMode();
            StageGroundBindingWindow.Open(scene);
            window = EditorWindow.GetWindow<StageGroundBindingWindow>();
            Assert.That(WindowSettings, Is.Null);
            Assert.That(root.GetComponent<StageGroundSettings>(), Is.Null, "Preview 期间不可偷偷创建组件");
            AnimationMode.StopAnimationMode();
            typeof(StageGroundBindingWindow).GetField("nextResolve", Flags).SetValue(window, 0d);
            typeof(StageGroundBindingWindow).GetMethod("ResolveMissingSettings", Flags).Invoke(window, null);
            Assert.That(WindowSettings, Is.Not.Null);
            Assert.That(WindowSettings.gameObject.scene, Is.EqualTo(scene), "保持打开窗口时指定的场景");
            Assert.That(root.GetComponents<StageGroundSettings>().Length, Is.EqualTo(1));
            Assert.That(Selection.activeObject, Is.Null);
        }
        [Test] public void DuplicateSettingsStayAmbiguousDuringPreview()
        {
            root.AddComponent<StageGroundSettings>();
            var second = new GameObject("SecondGroundSettings");
            second.transform.SetParent(root.transform);
            second.AddComponent<StageGroundSettings>();
            AnimationMode.StartAnimationMode();
            Assert.That(StageGroundRepair.GetSettings(scene), Is.Null);
        }
    }
}
