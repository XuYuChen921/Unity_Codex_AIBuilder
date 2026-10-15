using System;
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
    [InitializeOnLoad]
    public static class StageGroundPointSync
    {
        private static readonly Dictionary<StageRoute, int> signatures = new Dictionary<StageRoute, int>();
        private static int terrainRevision;
        private static double next;
        public static int Revision { get; private set; }
        private static string PreferenceKey => "SceneStory.GroundSync." + Application.dataPath;
        private static bool automatic = EditorPrefs.GetBool(PreferenceKey + ".Automatic", true);
        private static float interval = NormalizeInterval(EditorPrefs.GetFloat(PreferenceKey + ".Interval", .5f));
        public static bool Automatic
        {
            get => automatic;
            set { automatic = value; EditorPrefs.SetBool(PreferenceKey + ".Automatic", value); next = EditorApplication.timeSinceStartup + interval; }
        }
        public static float Interval
        {
            get => interval;
            set { interval = NormalizeInterval(value); EditorPrefs.SetFloat(PreferenceKey + ".Interval", interval); next = EditorApplication.timeSinceStartup + interval; }
        }
        private static float NormalizeInterval(float value)
            => float.IsNaN(value) || float.IsInfinity(value) ? .5f : Mathf.Clamp(value, .1f, 3600f);

        public static int SynchronizeNow()
        {
            // 手动刷新必须绕过旧签名，节点自身移动或同一网格内容变更也能重新计算。
            terrainRevision++;
            next = EditorApplication.timeSinceStartup + interval;
            return SynchronizeChangedGround();
        }


        static StageGroundPointSync()
        {
            EditorApplication.update += Tick;
            TerrainCallbacks.heightmapChanged += (terrain, region, synced) => terrainRevision++;
        }

        private static void Tick()
        {
            if (!automatic || Application.isBatchMode || StageGroundRepair.IsBinding || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || AnimationMode.InAnimationMode() ||
                GUIUtility.hotControl != 0 || EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + interval;
            SynchronizeChangedGround();
        }

        public static int SynchronizeChangedGround()
        {
            Physics.SyncTransforms();
            var routes = UnityEngine.Object.FindObjectsOfType<StageRoute>(true)
                .Where(r => r.gameObject.scene.IsValid() && r.gameObject.scene.isLoaded).ToArray();
            foreach (var stale in signatures.Keys.Where(r => r == null || !routes.Contains(r)).ToArray()) signatures.Remove(stale);
            // 同一组地面被多条路线引用时，每个碰撞体每轮只读取一次状态。
            var groundHashes = new Dictionary<Collider, int>();
            var groundOnly = GroundOnlyRoutes(routes, out var linkedStarts);
            var protectedPoints = new HashSet<Transform>();
            foreach (var route in routes.Where(r => !groundOnly[r]))
            { protectedPoints.Add(route.startAnchor); protectedPoints.Add(route.endAnchor); }
            var count = 0;
            var changed = false;
            foreach (var route in routes)
            {
                var hash = GroundSignature(route, groundHashes);
                if (signatures.TryGetValue(route, out var previous) && hash == previous) continue;
                signatures[route] = hash;
                changed = true;
                if (!groundOnly[route]) continue;
                route.Invalidate();
                Vector3[] projectedControls;
                var oldPreviewGround = route.previewGroundMode;
                try
                {
                    route.previewGroundMode = true;
                    route.SampleDisplayGeometry(out _, out projectedControls);
                }
                catch (InvalidOperationException) { continue; }
                finally { route.previewGroundMode = oldPreviewGround; }
                for (var i = 0; i < route.PointCount; i++)
                {
                    // 只判断链接关系，不递归采样整条前序剧情。
                    if (i == 0 && linkedStarts.Contains(route)) continue;
                    var anchor = i == 0 ? route.startAnchor : i == route.PointCount - 1 ? route.endAnchor : null;
                    if (anchor != null && protectedPoints.Contains(anchor)) continue;
                    var point = anchor != null ? anchor.position : route.transform.TransformPoint(route.points[i]);
                    var projected = projectedControls[i];
                    if ((projected - point).sqrMagnitude < .00000001f) continue;
                    if (anchor != null)
                    {
                        Undo.RecordObject(anchor, "路径点跟随地面"); anchor.position = projected;
                        EditorUtility.SetDirty(anchor);
                    }
                    else
                    {
                        Undo.RecordObject(route, "路径点跟随地面"); route.points[i] = route.transform.InverseTransformPoint(projected);
                        EditorUtility.SetDirty(route);
                    }
                    EditorSceneManager.MarkSceneDirty(route.gameObject.scene); count++;
                }
                route.Invalidate();
            }
            count += StageStationEditing.Synchronize(groundHashes);
            if (changed) Revision++;
            if (count > 0) SceneView.RepaintAll();
            return count;
        }

        private static int GroundSignature(StageRoute route, Dictionary<Collider, int> groundHashes)
            => SurfaceSignature(route.groundSurfaces, groundHashes);

        internal static int SurfaceSignature(Collider[] surfaces, Dictionary<Collider, int> groundHashes)
        {
            unchecked
            {
                var hash = terrainRevision;
                foreach (var ground in surfaces ?? Array.Empty<Collider>())
                {
                    hash = hash * 397 ^ (ground != null ? ground.GetInstanceID() : 0);
                    if (ground == null) continue;
                    if (!groundHashes.TryGetValue(ground, out var state))
                    {
                    state = 0;
                    var matrix = ground.transform.localToWorldMatrix;
                    for (var i = 0; i < 16; i++) state = Mix(state, matrix[i].GetHashCode());
                    var bounds = ground.bounds;
                    for (var i = 0; i < 3; i++)
                    { state = Mix(state, bounds.center[i].GetHashCode()); state = Mix(state, bounds.extents[i].GetHashCode()); }
                    state = Mix(state, ground.enabled.GetHashCode());
                    if (ground is MeshCollider mesh && mesh.sharedMesh != null)
                        state = Mix(state, mesh.sharedMesh.GetInstanceID() ^ mesh.sharedMesh.bounds.GetHashCode());
                    if (ground is TerrainCollider terrain && terrain.terrainData != null)
                        state = Mix(state, terrain.terrainData.GetInstanceID() ^ terrain.terrainData.size.GetHashCode());
                    groundHashes.Add(ground, state);
                    }
                    hash = Mix(hash, state);
                }
                return hash;
            }
        }

        // 旋转后混合，避免矩阵和包围盒同时反转高度符号时抵消。
        private static int Mix(int hash, int value) => (hash << 5 | (int)((uint)hash >> 27)) ^ value;

        private static Dictionary<StageRoute, bool> GroundOnlyRoutes(StageRoute[] routes, out HashSet<StageRoute> linkedStarts)
        {
            var result = routes.ToDictionary(r => r, r => r.snapToGround);
            var found = new HashSet<StageRoute>();
            var usages = new Dictionary<StageRoute, int>();
            linkedStarts = new HashSet<StageRoute>();
            foreach (var director in UnityEngine.Object.FindObjectsOfType<PlayableDirector>(true))
            {
                if (!(director.playableAsset is TimelineAsset timeline)) continue;
                foreach (var track in timeline.GetOutputTracks().OfType<StagePerformanceTrack>())
                {
                var clips = track.GetClips().OrderBy(c => c.start).ToArray();
                for (var index = 0; index < clips.Length; index++)
                {
                    var clip = clips[index];
                    if (!(clip.asset is StagePerformanceClip data) || data.mode != StagePerformanceMode.Move) continue;
                    var route = data.route.Resolve(director);
                    if (route == null || !result.ContainsKey(route) || route.gameObject.scene != director.gameObject.scene) continue;
                    var ground = data.UsesGround(route);
                    result[route] = found.Add(route) ? ground : result[route] && ground;
                    usages.TryGetValue(route, out var count); usages[route] = count + 1;
                    if (route.autoConnectPrevious && index > 0 && StagePerformanceTrack.PlacementSource(clips[index - 1]) != clip)
                        linkedStarts.Add(route);
                }
                }
            }
            linkedStarts.RemoveWhere(r => usages[r] > 1);
            return result;
        }
    }
}
