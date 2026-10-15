using System;
using System.IO;
using System.Linq;
using AIBuilder.CreatureStage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace AIBuilder.EditorTools.CreatureStage
{
    public static class StageStoryExample
    {
        public const string Folder = "Assets/CreatureStage/Examples/StoryEditing";
        public const string ScenePath = Folder + "/中华龙鸟_场景剧情编辑.unity";
        private const string Source = "Assets/CreatureStage/Source/Sinosauropteryx";

        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            StageStoryWindow.StopAllPreviews();
            if (!File.Exists(ScenePath)) Create();
            EditorSceneManager.OpenScene(ScenePath);
            var director = UnityEngine.Object.FindObjectsOfType<PlayableDirector>().First(d => d.playableAsset is TimelineAsset);
            Selection.activeGameObject = director.gameObject;
            StageStoryWindow.Open();
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(-2, 0, 5), Quaternion.Euler(55, 0, 0), 13);
        }

        public static void Create()
        {
            if (Directory.Exists(Folder) && Directory.EnumerateFileSystemEntries(Folder).Any()) throw new IOException("示例目录已有内容，未覆盖。");
            var layoutAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CreatureStage/Source/Export.fbx");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "/Model/skin.FBX");
            if (layoutAsset == null || model == null) throw new InvalidOperationException("需要 Export.fbx 和中华龙鸟模型。");
            var previous = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(previous.path) && previous.isDirty) throw new InvalidOperationException("请先保存当前场景。");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, string.IsNullOrEmpty(previous.path) ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
                var floorMaterial = Material("地面", new Color(0.22f, 0.28f, 0.24f));
                var treeMaterial = Material("倒木", new Color(0.38f, 0.32f, 0.23f));
                var plantMaterial = Material("草丛", new Color(0.30f, 0.44f, 0.28f));
                var routeMaterial = Material("路线", new Color(0.12f, 1, 0.65f));
                var skinMaterial = Material("中华龙鸟", Color.white);
                skinMaterial.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Textures/zhln_color.jpg");
                var layout = (GameObject)PrefabUtility.InstantiatePrefab(layoutAsset, scene);
                layout.name = "Export 白模（场景副本）";
                foreach (var renderer in layout.GetComponentsInChildren<Renderer>())
                {
                    // LOD0/LOD1 同时导出的白模只显示其中一个层级，保留源模型不变。
                    if (renderer.name.Contains("LOD1")) renderer.enabled = false;
                    var material = renderer.name.StartsWith("P2Terrain") ? floorMaterial : renderer.name.Contains("BigFallTree") ? treeMaterial : plantMaterial;
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                var ground = layout.GetComponentsInChildren<MeshFilter>().Where(m => m.name.StartsWith("P2Terrain")).Select(m =>
                {
                    var collider = m.GetComponent<MeshCollider>();
                    if (collider == null) collider = m.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = m.sharedMesh;
                    return (Collider)collider;
                }).ToArray();
                if (ground.Length == 0) throw new InvalidOperationException("白模中未找到 P2Terrain 地面，不能猜测路线高度。");
                Physics.SyncTransforms();
                var story = new GameObject("中华龙鸟剧情（选我编辑）");
                var director = story.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.GameTime;
                director.extrapolationMode = DirectorWrapMode.Hold;
                var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                timeline.name = "中华龙鸟剧情";
                AssetDatabase.CreateAsset(timeline, Folder + "/中华龙鸟剧情.playable");
                director.playableAsset = timeline;
                var track = timeline.CreateTrack<StagePerformanceTrack>(null, "中华龙鸟 · 动作与路线");

                var points = new[] { new Vector3(-7.79f, 0, 11.09f), new Vector3(-6.5f, 0, 5.2f), new Vector3(-3.8f, 0, 1.2f), new Vector3(-1.8f, 0, 0.1f), new Vector3(5, 0, -1.5f) };
                var names = new[] { "A 起始位置", "B 倒木前觅食", "C 草丛藏身", "D 再次觅食", "E 离场位置" };
                var anchors = new Transform[points.Length];
                var projectObject = new GameObject("GroundProjection");
                var projector = projectObject.AddComponent<StageRoute>(); projector.groundSurfaces = ground;
                for (var i = 0; i < points.Length; i++)
                {
                    anchors[i] = new GameObject(names[i]).transform;
                    anchors[i].SetParent(story.transform, false);
                    anchors[i].position = projector.ProjectToGround(points[i]);
                    anchors[i].rotation = Quaternion.Euler(0, 135, 0);
                }
                UnityEngine.Object.DestroyImmediate(projectObject);
                var routes = new StageRoute[4];
                for (var i = 0; i < routes.Length; i++)
                {
                    var obj = new GameObject($"路线 {names[i][0]} → {names[i + 1][0]}");
                    obj.transform.SetParent(story.transform, false);
                    var route = obj.AddComponent<StageRoute>();
                    route.startAnchor = anchors[i]; route.endAnchor = anchors[i + 1]; route.groundSurfaces = ground;
                    var middle = (anchors[i].position + anchors[i + 1].position) * 0.5f;
                    if (i == 0) middle = new Vector3(-8.1f, middle.y, 7.8f);
                    route.points = new[] { anchors[i].position, middle, anchors[i + 1].position };
                    var line = obj.AddComponent<LineRenderer>();
                    line.sharedMaterial = routeMaterial; line.useWorldSpace = true; line.widthMultiplier = 0.04f;
                    line.numCapVertices = 3; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    obj.AddComponent<StageRouteLine>();
                    routes[i] = route;
                }
                for (var i = 1; i < anchors.Length; i++)
                { routes[i - 1].Sample(1, out _, out var forward); anchors[i].rotation = Quaternion.LookRotation(forward); }
                routes[0].Sample(0, out _, out var firstForward); anchors[0].rotation = Quaternion.LookRotation(firstForward);

                var actorRoot = new GameObject("中华龙鸟 · 路线驱动");
                actorRoot.transform.SetPositionAndRotation(anchors[0].position, anchors[0].rotation);
                var skin = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                skin.transform.SetParent(actorRoot.transform, false);
                var animator = skin.GetComponent<Animator>();
                if (animator == null) animator = skin.AddComponent<Animator>();
                foreach (var renderer in skin.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterials = Enumerable.Repeat(skinMaterial, renderer.sharedMaterials.Length).ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                var driver = actorRoot.AddComponent<StageActorDriver>(); driver.animator = animator;
                driver.profile = AssetDatabase.FindAssets("t:StageActorProfile").Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<StageActorProfile>).FirstOrDefault(p => p.prefab == model);
                director.SetGenericBinding(track, driver);
                double cursor = 0;
                Action<string, string, double, int, bool, bool> add = (label, file, duration, point, move, loop) =>
                {
                    var clip = track.CreateClip<StagePerformanceClip>(); clip.start = cursor; clip.duration = duration; clip.displayName = label; cursor += duration;
                    var data = (StagePerformanceClip)clip.asset;
                    data.animation = AssetDatabase.LoadAllAssetsAtPath(Source + "/Animations/" + file).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                    data.loop = loop; data.mode = move ? StagePerformanceMode.Move : StagePerformanceMode.Hold;
                    data.route.exposedName = new PropertyName(Guid.NewGuid().ToString("N"));
                    data.station.exposedName = new PropertyName(Guid.NewGuid().ToString("N"));
                    director.SetReferenceValue(data.station.exposedName, anchors[point]);
                    if (move) director.SetReferenceValue(data.route.exposedName, routes[point]);
                    data.note = "可编辑示例时间与动作组合，尚未校准步幅、接触和转场。";
                };
                add("起点找食物", "mishi.FBX", 3, 0, false, true);
                add("走到倒木前", "walk.FBX", 6, 0, true, true);
                add("倒木前觅食", "mishi.FBX", 4, 1, false, true);
                add("听见声音 · 警觉", "shoujing.FBX", 1.2, 1, false, false);
                add("观察声音方向", "guancha_L.FBX", 1.5, 1, false, false);
                add("继续寻找食物", "mishi.FBX", 3, 1, false, true);
                add("压低身体跑向草丛", "yadishentikuaipao.FBX", 2.8, 1, true, true);
                add("刹停并低伏", "stop_difu.fbx", 1, 2, false, false);
                add("草丛里等待", "difu.FBX", 3, 2, false, true);
                add("探头查看环境", "tantou.FBX", 2, 2, false, false);
                add("起身", "qishen.fbx", 1, 2, false, false);
                add("慢慢走出草丛", "walk.FBX", 4, 2, true, true);
                add("再次觅食", "mishi.FBX", 4, 3, false, true);
                add("不放心 · 再观察", "guancha_R.FBX", 1.5, 3, false, false);
                add("离开这里", "walk.FBX", 6, 3, true, true);
                var camera = new GameObject("剧情观察相机").AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.12f, 0.15f, 0.18f);
                camera.fieldOfView = 48; camera.nearClipPlane = 0.05f; camera.farClipPlane = 200;
                var center = new Vector3(-2, 0, 5);
                camera.transform.position = center + new Vector3(0, 14, -17);
                camera.transform.LookAt(center);
                var playback = story.AddComponent<StageStoryPlayback>(); playback.director = director; playback.sceneCamera = camera; playback.actor = actorRoot.transform;
                EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(track); EditorUtility.SetDirty(director);
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            File.WriteAllText(Folder + "/先看这里.md", "# 场景剧情编辑示例\n\n打开菜单 Tools/AIBuilder/角色剧情/打开场景剧情编辑示例。\n\n1. 左侧选择剧情片段。\n2. 在 Scene 拖动路线控制点或停留位置，旋转圆盘改变朝向。\n3. 调整动作、持续秒数；默认顺延后续片段。\n4. 拖剧情时间或点播放预览，角色沿路线表演；结束预览恢复原位。\n5. 保存场景和 Timeline。原生 Timeline 可拖动片段调整时间。\n\n白模来自 Export.fbx 的场景副本；地面高度取自 P2Terrain，未修改源 FBX。示例时长和选点可改，可调整速度曲线和入场衔接；尚未实现接触脚锁、复杂坡面适配及步态匹配。\n");
            AssetDatabase.Refresh();
        }

        private static Material Material(string name, Color color)
        {
            var shader = Shader.Find("AIBuilder/CreatureStage/DemoSurface");
            if (shader == null) throw new InvalidOperationException("示例着色器缺失。");
            var material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, Folder + "/" + name + ".mat");
            return material;
        }
    }
}
