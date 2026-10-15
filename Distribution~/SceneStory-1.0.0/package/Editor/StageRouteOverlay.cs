using System;
using System.Linq;
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
        private static double nextRefresh;
        static StageRouteOverlay()
        {
            SceneView.duringSceneGui += Draw;
            EditorApplication.update += RefreshLines;
            Undo.undoRedoPerformed += () => { nextRefresh = 0; SceneView.RepaintAll(); };
        }
        private static void RefreshLines()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + 0.2;
            foreach (var route in UnityEngine.Object.FindObjectsOfType<StageRoute>())
            {
                StagePerformanceClip chosen = null;
                foreach (var director in UnityEngine.Object.FindObjectsOfType<PlayableDirector>())
                {
                    if (!(director.playableAsset is TimelineAsset timeline) || director.gameObject.scene != route.gameObject.scene) continue;
                    foreach (var track in timeline.GetOutputTracks().OfType<StagePerformanceTrack>())
                    foreach (var clip in track.GetClips().OrderBy(c => c.start))
                    {
                        var data = (StagePerformanceClip)clip.asset;
                        if (data.mode != StagePerformanceMode.Move || data.route.Resolve(director) != route) continue;
                        if (clip == TimelineEditor.selectedClip) { chosen = data; goto Found; }
                        if (chosen == null) chosen = data;
                    }
                }
                Found:
                route.previewGroundMode = chosen == null ? (bool?)null : chosen.UsesGround(route);
            }
            foreach (var line in UnityEngine.Object.FindObjectsOfType<StageRouteLine>()) line.RefreshDisplay();
        }
        private static void Draw(SceneView view)
        {
            if (Event.current.type != EventType.Repaint) return;
            var oldTest = Handles.zTest;
            var oldColor = Handles.color;
            try
            {
                // 编辑辅助线始终可见，地形或草丛不会遮住它。
                Handles.zTest = CompareFunction.Always;
                foreach (var route in UnityEngine.Object.FindObjectsOfType<StageRoute>())
                {
                    if (route.PointCount < 2 || !route.IsUsedByStory() || !route.gameObject.scene.IsValid() || SceneVisibilityManager.instance.IsHidden(route.gameObject)) continue;
                    var points = new Vector3[65];
                    try
                    {
                        for (var i = 0; i < points.Length; i++) route.Sample(i / 64.0, out points[i], out _);
                        Handles.color = new Color(0.2f, 1, 0.7f);
                        Handles.DrawAAPolyLine(3, points);
                    }
                    catch (InvalidOperationException)
                    {
                        Handles.color = new Color(1, 0.65f, 0.1f);
                        for (var i = 1; i < route.PointCount; i++) Handles.DrawDottedLine(route.WorldPoint(i - 1), route.WorldPoint(i), 4);
                        Handles.Label(route.WorldPoint(0), route.name + "：地面绑定失效，控制点仍保留");
                    }
                }
            }
            finally { Handles.zTest = oldTest; Handles.color = oldColor; }
        }
    }
}
