using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.Timeline;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using AIBuilder.CreatureStage;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AIBuilder.EditorTools.CreatureStage
{
    [InitializeOnLoad]
    public static class StageRouteOverlay
    {
        private sealed class Display
        {
            public StageRoute route;
            public Color color;
            public Vector3[] points;
            public Vector3[] controls;
            public bool[] editableControls;
            public bool linkedStart;
            public int signature;
            public int geometrySignature;
        }
        private static readonly HashSet<Transform> stations = new HashSet<Transform>();
        private static readonly List<Display> displays = new List<Display>();
        private static Dictionary<StageRoute, Display> displayByRoute = new Dictionary<StageRoute, Display>();
        private static double nextRefresh;
        private static bool dirty = true;
        private static int groundRevision = -1;
        private static TimelineClip selectedClip;
        public static int RebuildCount { get; private set; }
        public static int SampledRouteCount { get; private set; }
        static StageRouteOverlay()
        {
            SceneView.duringSceneGui += Draw;
            EditorApplication.update += RefreshLines;
            Undo.undoRedoPerformed += Invalidate;
            EditorApplication.hierarchyChanged += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            Selection.selectionChanged += Invalidate;
            ObjectChangeEvents.changesPublished += ObjectsChanged;
            TerrainCallbacks.heightmapChanged += (terrain, region, synced) => Invalidate();
        }
        private static void ObjectsChanged(ref ObjectChangeEventStream stream) { Invalidate(); }
        private static void Invalidate() { dirty = true; }
        public static void RefreshEditedRoute(StageRoute route)
        {
            if (route == null) return;
            route.Invalidate();
            dirty = true;
            nextRefresh = 0;
            RefreshLines();
            // 原生 Preview 可以编辑路径；只请求当前帧重新求值，不停止播放、不重建图。
            TimelineEditor.Refresh(RefreshReason.SceneNeedsUpdate | RefreshReason.WindowNeedsRedraw);
            SceneView.RepaintAll();
        }

        private static void RefreshLines()
        {
            if (StageGroundRepair.IsBinding || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + (GUIUtility.hotControl != 0 ? .1 : .2);
            if (groundRevision != StageGroundPointSync.Revision || selectedClip != TimelineEditor.selectedClip)
            { dirty = true; groundRevision = StageGroundPointSync.Revision; selectedClip = TimelineEditor.selectedClip; }
            if (!dirty)
                foreach (var display in displays)
                    if (display.route == null || display.signature != ControlSignature(display.route)) { dirty = true; break; }
            if (!dirty) return;
            dirty = false;
            // 鼠标事件只用缓存，路线关系与采样在这里统一更新。
            RebuildCount++;
            var chosen = new Dictionary<StageRoute, StagePerformanceClip>();
            var dependencies = new Dictionary<StageRoute, int>();
            var colors = new Dictionary<StageRoute, Color>();
            var used = new HashSet<StageRoute>();
            var scenesWithTimeline = new HashSet<int>();
            var groundHashes = new Dictionary<Collider, int>();
            var routeHashes = new Dictionary<StageRoute, int>();
            var previousDisplays = displayByRoute;
            var allRoutes = UnityEngine.Object.FindObjectsOfType<StageRoute>();
            foreach (var route in allRoutes)
                routeHashes[route] = Mix(ControlSignature(route), StageGroundPointSync.SurfaceSignature(route.groundSurfaces, groundHashes));
            stations.Clear();
            // 一次剧情遍历同时建立归属、颜色和连接依赖，避免每条路线重新搜索整个场景。
            foreach (var director in UnityEngine.Object.FindObjectsOfType<PlayableDirector>(true))
            {
                if (!(director.playableAsset is TimelineAsset timeline)) continue;
                scenesWithTimeline.Add(director.gameObject.scene.handle);
                foreach (var track in timeline.GetOutputTracks().OfType<StagePerformanceTrack>())
                {
                    var clips = track.GetClips().OrderBy(c => c.start).ToArray();
                    var related = new HashSet<StageRoute>();
                    var hash = Mix(track.GetInstanceID(), EditorUtility.GetDirtyCount(track));
                    hash = Mix(hash, director.isActiveAndEnabled.GetHashCode());
                    hash = Mix(hash, track.mutedInHierarchy.GetHashCode());
                    TimelineClip placement = null;
                    foreach (var clip in clips)
                    {
                        if (!(clip.asset is StagePerformanceClip data)) continue;
                        hash = Mix(hash, data.GetInstanceID());
                        hash = Mix(hash, EditorUtility.GetDirtyCount(data));
                        hash = Mix(hash, clip.start.GetHashCode());
                        hash = Mix(hash, clip.duration.GetHashCode());
                        hash = Mix(hash, data.followPrevious.GetHashCode());
                        hash = Mix(hash, (int)data.groundMode);
                        if (placement == null || data.mode != StagePerformanceMode.Hold || !data.followPrevious) placement = clip;
                        if (data.mode == StagePerformanceMode.Hold)
                        {
                            var point = data.station.Resolve(director);
                            if (point != null)
                            {
                                hash = Mix(hash, point.GetInstanceID());
                                hash = Mix(hash, point.position.GetHashCode());
                                if (placement == clip && director.gameObject.activeInHierarchy) stations.Add(point);
                            }
                            continue;
                        }
                        var route = data.route.Resolve(director);
                        if (route == null) continue;
                        related.Add(route);
                        hash = Mix(hash, route.GetInstanceID());
                        hash = Mix(hash, routeHashes.TryGetValue(route, out var state) ? state : ControlSignature(route));
                        if (!track.mutedInHierarchy)
                        {
                            used.Add(route);
                            if (director.GetGenericBinding(track) is StageActorDriver actor && (!colors.ContainsKey(route) || director.isActiveAndEnabled)) colors[route] = actor.routeColor;
                        }
                        if (director.gameObject.activeInHierarchy && (!chosen.ContainsKey(route) || clip == TimelineEditor.selectedClip)) chosen[route] = data;
                    }
                    foreach (var route in related)
                        dependencies[route] = Mix(dependencies.TryGetValue(route, out var old) ? old : 0, hash);
                }
            }
            displays.Clear();
            using (StageRoute.BeginSampling())
            foreach (var route in allRoutes)
            {
                route.previewGroundMode = chosen.TryGetValue(route, out var data) ? (bool?)data.UsesGround(route) : null;
                var line = route.GetComponent<StageRouteLine>();
                if (route.PointCount < 2 || !route.gameObject.scene.IsValid() || scenesWithTimeline.Contains(route.gameObject.scene.handle) && !used.Contains(route))
                { if (line != null) line.ApplyDisplay(null, Color.white); continue; }
                var color = colors.TryGetValue(route, out var routeColor) ? routeColor : new Color(.2f, .9f, .7f);
                var geometryHash = Mix(routeHashes[route], dependencies.TryGetValue(route, out var dependency) ? dependency : 0);
                geometryHash = Mix(geometryHash, route.previewGroundMode.GetHashCode());
                geometryHash = Mix(geometryHash, route.startAnchor != null && stations.Contains(route.startAnchor) ? 1 : 0);
                geometryHash = Mix(geometryHash, route.endAnchor != null && stations.Contains(route.endAnchor) ? 1 : 0);
                if (previousDisplays.TryGetValue(route, out var cached) && cached.geometrySignature == geometryHash)
                {
                    if (cached.color != color && line != null) line.ApplyDisplay(cached.points, color);
                    cached.color = color;
                    displays.Add(cached);
                    continue;
                }
                var display = new Display { route = route, color = color, controls = new Vector3[route.PointCount], geometrySignature = geometryHash };
                display.editableControls = new bool[route.PointCount];
                var linkedStart = route.TryGetLinkedStart(out _);
                display.linkedStart = linkedStart;
                for (var i = 0; i < display.controls.Length; i++)
                {
                    display.controls[i] = route.WorldPoint(i);
                    var anchor = i == 0 ? route.startAnchor : i == route.PointCount - 1 ? route.endAnchor : null;
                    display.editableControls[i] = !(i == 0 && linkedStart) && (anchor == null || !stations.Contains(anchor));
                }
                route.Invalidate();
                SampledRouteCount++;
                try { route.SampleDisplayGeometry(out display.points, out display.controls); }
                catch (InvalidOperationException) { display.points = null; }
                if (line != null) line.ApplyDisplay(display.points, display.color);
                display.signature = ControlSignature(route);
                displays.Add(display);
            }
            displayByRoute = displays.ToDictionary(d => d.route);
        }

        // 仅检查控制数据，不采样路线或遍历地面。兼容未经过 Undo 的脚本移动锚点。
        private static int ControlSignature(StageRoute route)
        {
            var hash = EditorUtility.GetDirtyCount(route);
            hash = Mix(hash, route.autoConnectPrevious.GetHashCode());
            hash = Mix(hash, route.curved.GetHashCode());
            hash = Mix(hash, route.smoothConnections.GetHashCode());
            hash = Mix(hash, route.smoothness.GetHashCode());
            hash = Mix(hash, route.snapToGround.GetHashCode());
            hash = Mix(hash, route.limitGroundProjection.GetHashCode());
            hash = Mix(hash, route.groundStepUp.GetHashCode());
            hash = Mix(hash, route.groundDrop.GetHashCode());
            hash = Mix(hash, route.groundSlope.GetHashCode());
            var matrix = route.transform.localToWorldMatrix;
            for (var i = 0; i < 16; i++) hash = Mix(hash, matrix[i].GetHashCode());
            foreach (var point in route.points ?? Array.Empty<Vector3>())
                for (var i = 0; i < 3; i++) hash = Mix(hash, point[i].GetHashCode());
            if (route.startAnchor != null)
                for (var i = 0; i < 3; i++) hash = Mix(hash, route.startAnchor.position[i].GetHashCode());
            if (route.endAnchor != null)
                for (var i = 0; i < 3; i++) hash = Mix(hash, route.endAnchor.position[i].GetHashCode());
            return hash;
        }
        private static Display CachedDisplay(StageRoute route)
            => route != null && displayByRoute.TryGetValue(route, out var display) ? display : null;

        public static bool DisplayLinkedStart(StageRoute route)
            => CachedDisplay(route)?.linkedStart ?? false;

        public static Color DisplayColor(StageRoute route)
            => CachedDisplay(route)?.color ?? Color.cyan;

        public static bool TryDisplayEnd(StageRoute route, out Vector3 end)
        {
            var display = CachedDisplay(route);
            if (display != null && display.points != null && display.controls.Length > 0)
            { end = display.controls[display.controls.Length - 1]; return true; }
            end = default; return false;
        }

        public static Vector3 DisplayControlPosition(StageRoute route, int index)
        {
            var display = CachedDisplay(route);
            return display != null && display.points != null && index >= 0 && index < display.controls.Length
                ? display.controls[index] : route.WorldPoint(index);
        }

        private static int Mix(int hash, int value) => (hash << 5 | (int)((uint)hash >> 27)) ^ value;
        private static void Draw(SceneView view)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var oldTest = Handles.zTest;
            var oldColor = Handles.color;
            try
            {
                // 编辑辅助线始终可见，地形或草丛不会遮住它。
                Handles.zTest = CompareFunction.Always;
                foreach (var point in stations)
                {
                    if (point == null || SceneVisibilityManager.instance.IsHidden(point.gameObject)) continue;
                    var size = HandleUtility.GetHandleSize(point.position) * .085f;
                    if (Handles.Button(point.position, Quaternion.identity, size, size * 1.3f, Handles.SphereHandleCap))
                    {
                        Selection.activeGameObject = point.gameObject;
                        foreach (var window in Resources.FindObjectsOfTypeAll<StageStoryWindow>()) window.SelectScenePoint(point.gameObject);
                        SceneView.RepaintAll();
                    }
                }
                foreach (var display in displays)
                {
                    var route = display.route;
                    if (route == null || SceneVisibilityManager.instance.IsHidden(route.gameObject)) continue;
                    // 未选中的路线也可直接点选，无需先在 Hierarchy 找到路线物体。
                    if (Selection.activeGameObject != route.gameObject)
                    using (new Handles.DrawingScope(display.color))
                        for (var i = 0; i < display.controls.Length; i++)
                        {
                            if (!display.editableControls[i]) continue;
                            var point = display.controls[i];
                            var size = HandleUtility.GetHandleSize(point) * .065f;
                            if (Handles.Button(point, Quaternion.identity, size, size * 1.3f, Handles.SphereHandleCap))
                                StageRouteEditor.SelectPoint(route, i);
                        }
                    if (Event.current.type != EventType.Repaint) continue;
                    if (display.points != null)
                    {
                        Handles.color = display.color;
                        Handles.DrawAAPolyLine(3, display.points);
                    }
                    else
                    {
                        Handles.color = new Color(1, .65f, .1f);
                        for (var i = 1; i < display.controls.Length; i++) Handles.DrawDottedLine(display.controls[i - 1], display.controls[i], 4);
                        if (route.groundFailurePosition.HasValue)
                        {
                            var failed = route.groundFailurePosition.Value;
                            Handles.color = Color.red;
                            Handles.DrawWireDisc(failed, Vector3.up, HandleUtility.GetHandleSize(failed) * .1f);
                            Handles.Label(failed, route.name + StageLanguage.Text("：地面/坡度/高度范围不符"));
                        }
                    }
                }
            }
            finally { Handles.zTest = oldTest; Handles.color = oldColor; }
        }
    }
}
