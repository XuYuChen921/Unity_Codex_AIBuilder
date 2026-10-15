using System.Collections.Generic;
using System.Linq;
using AIBuilder.CreatureStage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace AIBuilder.EditorTools.CreatureStage
{
    internal static class StageStationEditing
    {
        private static readonly Dictionary<Transform, int> signatures = new Dictionary<Transform, int>();

        internal static int Synchronize(Dictionary<Collider, int> groundHashes, PlayableDirector[] directors)
        {
            var grounded = new Dictionary<Transform, StageGroundSettings>();
            var protectedPoints = new HashSet<Transform>();
            foreach (var director in directors)
            {
                if (!(director.playableAsset is TimelineAsset timeline)) continue;
                foreach (var track in timeline.GetOutputTracks().OfType<StagePerformanceTrack>())
                foreach (var clip in track.GetClips())
                {
                    if (!(clip.asset is StagePerformanceClip data)) continue;
                    if (data.mode == StagePerformanceMode.Move)
                    {
                        var route = data.route.Resolve(director);
                        if (route != null && !data.UsesGround(route))
                        { protectedPoints.Add(route.startAnchor); protectedPoints.Add(route.endAnchor); }
                        continue;
                    }
                    if (StagePerformanceTrack.PlacementSource(clip) != clip) continue;
                    var point = data.station.Resolve(director);
                    if (point == null) continue;
                    var settings = data.stationGround.Resolve(director);
                    if (data.groundMode != StageGroundMode.Ground || settings == null || track.lockedInHierarchy)
                    { protectedPoints.Add(point); continue; }
                    if (grounded.TryGetValue(point, out var existing) && existing != settings) protectedPoints.Add(point);
                    grounded[point] = settings;
                }
            }
            foreach (var stale in signatures.Keys.Where(p => p == null || !grounded.ContainsKey(p) || protectedPoints.Contains(p)).ToArray()) signatures.Remove(stale);
            var count = 0;
            foreach (var pair in grounded)
            {
                var point = pair.Key;
                if (protectedPoints.Contains(point)) continue;
                var settings = pair.Value;
                var surfaceHash = StageGroundPointSync.SurfaceSignature(settings.boundSurfaces, groundHashes)
                    ^ settings.GetInstanceID() ^ EditorUtility.GetDirtyCount(settings);
                var hash = surfaceHash ^ point.position.GetHashCode();
                if (signatures.TryGetValue(point, out var previous) && previous == hash) continue;
                if (settings.TryPlaceStation(point.position, out var projected) && (point.position - projected).sqrMagnitude > .00000001f)
                {
                    Undo.RecordObject(point, "停留点贴合地面");
                    point.position = projected;
                    EditorUtility.SetDirty(point);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(point);
                    EditorSceneManager.MarkSceneDirty(point.gameObject.scene);
                    count++;
                }
                signatures[point] = surfaceHash ^ point.position.GetHashCode();
            }
            return count;
        }
    }
}
