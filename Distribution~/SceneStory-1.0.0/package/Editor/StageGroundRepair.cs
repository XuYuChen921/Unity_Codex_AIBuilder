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
    [InitializeOnLoad]
    public static class StageGroundRepair
    {
        private const string Request = "Library/CreatureStage/ground-repair.request.txt";
        private static bool pending = true;
        private static double next;
        static StageGroundRepair()
        {
            EditorApplication.update += Poll;
            EditorSceneManager.sceneOpened += (scene, mode) => Schedule();
        }
        internal static void Schedule() { pending = true; }
        public static void RepairActive() { Debug.Log(Repair(SceneManager.GetActiveScene())); }
        public static StageGroundSettings GetSettings(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            var settings = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageGroundSettings>(true)).ToArray();
            if (settings.Length > 0) return settings.Length == 1 ? settings[0] : null;
            var route = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageRoute>(true)).FirstOrDefault();
            if (route == null) return null;
            var created = Undo.AddComponent<StageGroundSettings>(route.transform.root.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            return created;
        }
        private static void Poll()
        {
            if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 2;
            var requested = File.Exists(Request);
            if (!pending && !requested) return;
            pending = false;
            try
            {
                var result = "";
                for (var i = 0; i < SceneManager.sceneCount; i++) result += Repair(SceneManager.GetSceneAt(i)) + "\n";
                if (requested) File.WriteAllText("Library/CreatureStage/ground-repair.result.txt", result);
            }
            catch (Exception e)
            {
                if (requested) File.WriteAllText("Library/CreatureStage/ground-repair.result.txt", "FAILED " + e);
                Debug.LogException(e);
            }
            finally { if (requested) File.Delete(Request); }
        }
        public static string Repair(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return "场景未加载";
            var routes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StageRoute>(true)).ToArray();
            if (routes.Length == 0) return scene.name + "：无剧情路线";
            var settings = GetSettings(scene);
            if (settings == null) return scene.name + "：地面绑定设置不唯一，请每个场景只保留一份";
            if (settings.groundRoot != null && settings.groundRoot.gameObject.scene != scene) return "地面根对象必须属于当前场景";
            var roots = settings.groundRoot != null ? new[] { settings.groundRoot.gameObject } : scene.GetRootGameObjects();
            var meshes = roots.SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).Where(f => settings.Matches(f.name) && f.sharedMesh != null).ToArray();
            if (meshes.Length == 0) return scene.name + "：未匹配到地面（" + settings.groundName + "），保留现有绑定和路线点";
            StageStoryWindow.StopAllPreviews();
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("更新剧情地面绑定");
            var surfaces = meshes.Select(f =>
            {
                var c = f.GetComponent<MeshCollider>();
                if (c == null) c = Undo.AddComponent<MeshCollider>(f.gameObject);
                if (c.sharedMesh != f.sharedMesh)
                {
                    Undo.RecordObject(c, "更新白模碰撞网格"); c.sharedMesh = f.sharedMesh;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                }
                return (Collider)c;
            }).ToArray();
            Physics.SyncTransforms();
            var count = 0;
            var snapped = 0;
            foreach (var route in routes)
            {
                // 自动模式包括新建路线的空绑定；手动/悬空路线通过明确开关保留。
                if (!route.autoBindGround) continue;
                if (route.groundSurfaces == null || !route.groundSurfaces.SequenceEqual(surfaces))
                {
                    Undo.RecordObject(route, "重新绑定路线地面"); route.groundSurfaces = surfaces;
                    EditorUtility.SetDirty(route); count++;
                }
                route.Invalidate();
                // 共享路线只要有飞行片段，就保留原始高度；地面片段在采样时单独投射。
                if (!route.snapToGround || route.HasFreeSpaceUsage()) continue;
                for (var i = 0; i < route.PointCount; i++)
                {
                    if (i == 0 && route.TryGetLinkedStart(out _)) continue;
                    var point = route.WorldPoint(i);
                    Vector3 projected;
                    try { projected = route.ProjectToGround(point, true); }
                    catch (InvalidOperationException) { continue; }
                    if (Mathf.Abs(projected.y - point.y) < 0.0001f) continue;
                    point.y = projected.y;
                    var anchor = i == 0 ? route.startAnchor : i == route.PointCount - 1 ? route.endAnchor : null;
                    if (anchor != null)
                    {
                        Undo.RecordObject(anchor, "路线节点贴地"); anchor.position = point;
                        EditorUtility.SetDirty(anchor);
                    }
                    else
                    {
                        Undo.RecordObject(route, "路线控制点贴地"); route.points[i] = route.transform.InverseTransformPoint(point);
                        EditorUtility.SetDirty(route);
                    }
                    snapped++;
                }
                route.Invalidate();
            }
            Undo.CollapseUndoOperations(group);
            if (count > 0 || snapped > 0) EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate();
            return scene.name + "：地面 " + surfaces.Length + " 块，重绑路线 " + count + " 条，贴地节点 " + snapped + " 个；保留水平位置。请保存场景。";
        }
    }

    public sealed class StageGroundImportWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(moved).Any(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))) StageGroundRepair.Schedule();
        }
    }
}
