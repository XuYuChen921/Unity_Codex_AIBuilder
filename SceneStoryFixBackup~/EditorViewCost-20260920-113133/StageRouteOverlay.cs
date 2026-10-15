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
            public int signature;
        }
        private static readonly HashSet<Transform> stations = new HashSet<Transform>();
        private static readonly List<Display> displays = new List<Display>();
        private static double nextRefresh;
        private static bool dirty = true;
        private static int groundRevision = -1;
        private static TimelineClip selectedClip;
        public static int RebuildCount { get; private set; }
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
            stations.Clear();
            foreach (var director in UnityEngine.Object.FindObjectsOfType<PlayableDirector>())
            {
                if (!(director.playableAsset is TimelineAsset timeline)) continue;
                foreach (var track in timeline.GetOutputTracks().OfType<StagePerformanceTrack>())
                foreach (var clip in track.GetClips())
                {
                    var data = (StagePerformanceClip)clip.asset;
                    if (data.mode == StagePerformanceMode.Hold && StagePerformanceTrack.PlacementSource(clip) == clip)
                    {
                        var point = data.station.Resolve(director);
                        if (point != null) stations.Add(point);
                    }
                    if (data.mode != StagePerformanceMode.Move) continue;
                    var route = data.route.Resolve(director);
                    if (route == null) continue;
                    if (!chosen.ContainsKey(route) || clip == TimelineEditor.selectedClip) chosen[route] = data;
                }
            }
            displays.Clear();
            foreach (var route in UnityEngine.Object.FindObjectsOfType<StageRoute>())
            {
                route.previewGroundMode = chosen.TryGetValue(route, out var data) ? (bool?)data.UsesGround(route) : null;
                var line = route.GetComponent<StageRouteLine>();
                if (route.PointCount < 2 || !route.IsUsedByStory() || !route.gameObject.scene.IsValid())
                { if (line != null) line.ApplyDisplay(null, Color.white); continue; }
                var display = new Display { route = route, color = route.DisplayColor, points = new Vector3[65], controls = new Vector3[route.PointCount] };
                display.editableControls = new bool[route.PointCount];
                var linkedStart = route.TryGetLinkedStart(out _);
                for (var i = 0; i < display.controls.Length; i++)
                {
                    display.controls[i] = route.WorldPoint(i);
                    // 自动连接起点由前段控制，不能再作为第二个可点选节点绘制。
                    var anchor = i == 0 ? route.startAnchor : i == route.PointCount - 1 ? route.endAnchor : null;
                    display.editableControls[i] = !(i == 0 && linkedStart) && (anchor == null || !stations.Contains(anchor));
                }
                try { route.SampleDisplayGeometry(out display.points, out display.controls); }
                catch (InvalidOperationException) { display.points = null; }
                if (line != null) line.ApplyDisplay(display.points, display.color);
                display.signature = ControlSignature(route);
                displays.Add(display);
            }
        }

        // 仅检查控制数据，不采样路线或遍历地面。兼容未经过 Undo 的脚本移动锚点。
        private static int ControlSignature(StageRoute route)
        {
            var hash = EditorUtility.GetDirtyCount(route);
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
        public static Vector3 DisplayControlPosition(StageRoute route, int index)
        {
            var display = displays.FirstOrDefault(d => d.route == route);
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
