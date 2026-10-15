using System.Collections.Generic;
using System.Linq;
using System.IO;
using AIBuilder.CreatureStage;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace AIBuilder.EditorTools.CreatureStage
{
    // 只整理工具生成对象的 Hierarchy 可见性，保留引用和撤销所需对象。
    [InitializeOnLoad]
    public static class StageStoryHierarchy
    {
        private const string Menu = "Tools/场景剧情/维护/显示未使用剧情对象";
        private const string Preference = "AIBuilder.CreatureStage.ShowUnusedObjects";
        private static double nextRefresh;
        private static bool dirty = true;
        static StageStoryHierarchy()
        {
            EditorApplication.update += Tick;
            Undo.undoRedoPerformed += () => dirty = true;
            EditorApplication.hierarchyChanged += () => dirty = true;
            EditorApplication.projectChanged += () => dirty = true;
            ObjectChangeEvents.changesPublished += ObjectsChanged;
        }
        private static void ObjectsChanged(ref ObjectChangeEventStream stream) { dirty = true; }
        [MenuItem(Menu)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(Preference, !EditorPrefs.GetBool(Preference, false));
            nextRefresh = 0;
            dirty = true;
        }
        [MenuItem(Menu, true)]
        private static bool ValidateMenu()
        {
            UnityEditor.Menu.SetChecked(Menu, EditorPrefs.GetBool(Preference, false));
            return true;
        }
        private static void Tick()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + .5;
            const string request = "Library/CreatureStage/group-points.request.txt";
            if (File.Exists(request) && !AnimationMode.InAnimationMode()) { GroupAll(); File.Delete(request); }
            if (dirty) { dirty = false; Refresh(EditorPrefs.GetBool(Preference, false)); }
        }

        [MenuItem("Tools/场景剧情/维护/按角色整理路径点")]
        public static void GroupAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || AnimationMode.InAnimationMode()) return;
            foreach (var director in Object.FindObjectsOfType<PlayableDirector>(true)) GroupDirector(director);
            Refresh();
        }

        public static void GroupDirector(PlayableDirector director)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || AnimationMode.InAnimationMode()) return;
            if (director == null || !director.gameObject.scene.IsValid() || !(director.playableAsset is TimelineAsset timeline)) return;
            var owners = new Dictionary<Transform, HashSet<StagePerformanceTrack>>();
            foreach (var track in timeline.GetOutputTracks().OfType<StagePerformanceTrack>())
            foreach (var clip in track.GetClips())
            {
                if (!(clip.asset is StagePerformanceClip data)) continue;
                var route = data.mode == StagePerformanceMode.Move ? data.route.Resolve(director) : null;
                var points = route != null ? new[] { route.transform, route.startAnchor, route.endAnchor }
                    : new[] { data.station.Resolve(director) };
                foreach (var point in points)
                {
                    if (point == null || (point.parent != director.transform &&
                        (point.parent == null || point.parent.parent != director.transform || point.parent.GetComponent<StageStoryPointGroup>() == null))) continue;
                    // 只移动路线及纯锚点，保留用户模型和其他功能组件。
                    if (point.GetComponent<StageRoute>() == null && (point.GetComponents<Component>().Length != 1 || point.childCount != 0)) continue;
                    if (!owners.TryGetValue(point, out var tracks)) owners.Add(point, tracks = new HashSet<StagePerformanceTrack>());
                    tracks.Add(track);
                }
            }
            Undo.IncrementCurrentGroup(); var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("按角色整理路径点");
            foreach (var entry in owners)
            {
                var track = entry.Value.Count == 1 ? entry.Value.First() : null;
                var group = director.GetComponentsInChildren<StageStoryPointGroup>(true)
                    .FirstOrDefault(g => g.transform.parent == director.transform && g.track == track);
                if (group == null)
                {
                    var actor = track != null ? director.GetGenericBinding(track) as StageActorDriver : null;
                    var obj = new GameObject(track == null ? "共用路径点" : (actor != null ? actor.name : track.name) + " · 路径点");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, director.gameObject.scene);
                    obj.transform.SetParent(director.transform, false);
                    Undo.RegisterCreatedObjectUndo(obj, "创建角色路径分组");
                    group = Undo.AddComponent<StageStoryPointGroup>(obj); group.track = track;
                }
                if (entry.Key.parent != group.transform) Undo.SetTransformParent(entry.Key, group.transform, "整理角色路径点");
            }
            Undo.CollapseUndoOperations(undoGroup);
        }
        public static void Refresh(bool showUnused = false)
        {
            var candidates = new HashSet<GameObject>();
            var used = new HashSet<GameObject>();
            foreach (var director in Object.FindObjectsOfType<PlayableDirector>(true))
            {
                if (!director.gameObject.scene.IsValid() || !(director.playableAsset is TimelineAsset timeline)) continue;
                var tracks = timeline.GetOutputTracks().OfType<StagePerformanceTrack>().ToArray();
                if (tracks.Length == 0) continue;
                foreach (var child in director.GetComponentsInChildren<Transform>(true).Where(t => t.parent == director.transform ||
                    t.parent != null && t.parent.parent == director.transform && t.parent.GetComponent<StageStoryPointGroup>() != null))
                {
                    var route = child.GetComponent<StageRoute>();
                    if (route != null)
                    {
                        candidates.Add(child.gameObject);
                        AddOwnedPoint(candidates, route.startAnchor, director.transform);
                        AddOwnedPoint(candidates, route.endAnchor, director.transform);
                    }
                    else if (IsGeneratedPoint(child)) candidates.Add(child.gameObject);
                }
                foreach (var track in tracks)
                foreach (var clip in track.GetClips())
                {
                    var data = (StagePerformanceClip)clip.asset;
                    if (data.mode == StagePerformanceMode.Move)
                    {
                        var route = data.route.Resolve(director);
                        if (route == null) continue;
                        Keep(used, route.transform);
                        // 层级可见性只读片段连接，不触发地面采样。
                        var ordered = track.GetClips().OrderBy(c => c.start).ToArray();
                        var index = System.Array.IndexOf(ordered, clip);
                        var shared = tracks.SelectMany(t => t.GetClips()).Count(c => c.asset is StagePerformanceClip d &&
                            d.mode == StagePerformanceMode.Move && d.route.Resolve(director) == route) > 1;
                        var linked = route.autoConnectPrevious && !shared && index > 0 && StagePerformanceTrack.PlacementSource(ordered[index - 1]) != clip;
                        if (!linked) Keep(used, route.startAnchor);
                        Keep(used, route.endAnchor);
                    }
                    else
                    {
                        var source = StagePerformanceTrack.PlacementSource(clip);
                        var placement = (StagePerformanceClip)source.asset;
                        if (placement.mode == StagePerformanceMode.Hold) Keep(used, placement.station.Resolve(director));
                    }
                }
            }
            var changed = false;
            foreach (var obj in candidates)
            {
                var flags = showUnused || used.Contains(obj) ? obj.hideFlags & ~HideFlags.HideInHierarchy : obj.hideFlags | HideFlags.HideInHierarchy;
                if (obj.hideFlags == flags) continue;
                obj.hideFlags = flags;
                changed = true;
            }
            if (changed) EditorApplication.RepaintHierarchyWindow();
        }
        private static void Keep(HashSet<GameObject> used, Transform point)
        {
            for (; point != null; point = point.parent) used.Add(point.gameObject);
        }
        private static void AddOwnedPoint(HashSet<GameObject> candidates, Transform point, Transform root)
        {
            if (point != null && (point.parent == root || point.parent != null && point.parent.parent == root && point.parent.GetComponent<StageStoryPointGroup>() != null) && point.GetComponents<Component>().Length == 1)
                candidates.Add(point.gameObject);
        }
        private static bool IsGeneratedPoint(Transform point)
        {
            if (point.GetComponents<Component>().Length != 1 || point.childCount != 0) return false;
            return point.name.StartsWith("Story point ") || point.name.StartsWith("Arrival point ") || point.name.StartsWith("剧情点 ") || point.name.StartsWith("到达点 ") ||
                new[] { "A 起始位置", "B 倒木前觅食", "C 草丛藏身", "D 再次觅食", "E 离场位置" }.Contains(point.name);
        }
    }
}
