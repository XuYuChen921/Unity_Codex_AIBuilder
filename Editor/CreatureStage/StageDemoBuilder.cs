using System;
using System.IO;
using System.Linq;
using AIBuilder.CreatureStage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIBuilder.EditorTools.CreatureStage
{
    public static class StageDemoBuilder
    {
        public const string ScenePath = "Assets/CreatureStage/Examples/SinosauropteryxDemo/中华龙鸟_入门示例.unity";
        private const string Folder = "Assets/CreatureStage/Examples/SinosauropteryxDemo";
        private const string Source = "Assets/CreatureStage/Source/Sinosauropteryx";

        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Create();
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            if (Directory.Exists(Folder) && Directory.EnumerateFileSystemEntries(Folder).Any())
                throw new IOException("示例目录已经存在内容，不覆盖已有示例。");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "/Model/skin.FBX");
            if (model == null) throw new InvalidOperationException("找不到中华龙鸟模型。");
            var names = new[] { "idle.FBX", "walk.FBX", "run.FBX", "difu.FBX" };
            var clips = names.Select(n => AssetDatabase.LoadAllAssetsAtPath(Source + "/Animations/" + n)
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"))).ToArray();
            if (clips.Any(c => c == null)) throw new InvalidOperationException("缺少 idle、walk、run 或 difu 动画。");
            var shader = Shader.Find("AIBuilder/CreatureStage/DemoSurface");
            if (shader == null) throw new InvalidOperationException("示例材质尚未编译。");
            var previous = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(previous.path) && previous.isDirty)
                throw new InvalidOperationException("请先保存当前未命名场景，再创建示例。");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                string.IsNullOrEmpty(previous.path) ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
                var actorRoot = new GameObject("中华龙鸟（示例副本）");
                var actorObject = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                actorObject.name = "模型";
                actorObject.transform.SetParent(actorRoot.transform, false);
                var actor = actorObject.GetComponent<Animator>();
                if (actor == null) actor = actorObject.AddComponent<Animator>();
                var material = new Material(shader) { name = "中华龙鸟_示例材质", color = Color.white };
                material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Textures/zhln_color.jpg");
                AssetDatabase.CreateAsset(material, Folder + "/角色材质.mat");
                foreach (var renderer in actorObject.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                var renderers = actorObject.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

                var groundMaterial = new Material(shader) { name = "展示台", color = new Color(0.12f, 0.22f, 0.25f) };
                AssetDatabase.CreateAsset(groundMaterial, Folder + "/展示台材质.mat");
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ground.name = "圆形展示台";
                ground.transform.position = new Vector3(bounds.center.x, -0.045f, bounds.center.z);
                ground.transform.localScale = new Vector3(bounds.size.magnitude * 1.15f, 0.04f, bounds.size.magnitude * 1.15f);
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
                UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());

                var camera = new GameObject("示例相机").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.065f, 0.11f, 0.145f);
                camera.fieldOfView = 34;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 50;
                camera.rect = new Rect(0.269f, 0, 0.731f, 1);
                var center = bounds.center;
                var distance = bounds.extents.magnitude * 3.5f;
                var rotation = Quaternion.Euler(12, 100, 0);
                camera.transform.SetPositionAndRotation(center - rotation * Vector3.forward * distance, rotation);
                var controller = new GameObject("示例控制面板");
                controller.SetActive(false);
                var player = controller.AddComponent<StageDemoPlayer>();
                player.actor = actor;
                player.clips = clips;
                player.labels = new[] { "待机 · 看看角色", "行走 · 观察迈步", "奔跑 · 观察步态", "低伏 · 观察姿态" };
                player.viewCamera = camera;
                player.viewCenter = center;
                player.viewDistance = distance;
                controller.SetActive(true);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            File.WriteAllText(Folder + "/先看这里.md", "# 中华龙鸟入门示例\n\n1. 双击同目录的 中华龙鸟_入门示例.unity。\n2. 点击 Unity 顶部 ▶ Play，切换到 Game 窗口。\n3. 默认每 4 秒轮换待机、行走、奔跑、低伏；点击左侧按钮选择动作。\n4. 拖动动作进度会暂停；点继续播放恢复，底部滑条可以转动观察角度。\n\n这是原地动作示例，没有完成路径编排、脚锁或地形适配。所有对象和材质都是示例副本，源模型和动画保持原样。\n");
            AssetDatabase.Refresh();
        }
    }
}
