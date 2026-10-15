using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace AIBuilder.CreatureStage
{
    [AddComponentMenu("AIBuilder/角色剧情/场景路线")]
    public sealed class StageRoute : MonoBehaviour
    {
        [InspectorName("按 Timeline 顺序自动连接起点")] public bool autoConnectPrevious = true;
        public Transform startAnchor;
        public Transform endAnchor;
        public Vector3[] points = { Vector3.zero, Vector3.forward };
        [InspectorName("圆滑曲线")] public bool curved = true;
        [Range(0, 1), InspectorName("圆滑度")] public float smoothness = 0.5f;
        [InspectorName("圆滑连接相邻移动片段")] public bool smoothConnections = true;
        [Tooltip("自动使用场景的地面绑定设置。关闭后可使用手动指定地面或悬空路线。")]
        [InspectorName("自动绑定场景地面")] public bool autoBindGround = true;
        [Tooltip("开启时沿地面采样；关闭时直接使用节点的世界 XYZ 坐标，可编辑飞行路线。") ]
        public bool snapToGround = true;
        public Collider[] groundSurfaces = Array.Empty<Collider>();
        private Vector3[] samples;
        private Vector3[] tangents;
        private double[] lengths;
        private int signature;
        [NonSerialized] public bool? previewGroundMode;
        private bool? samplingGroundMode;
        private bool EffectiveGround => samplingGroundMode ?? previewGroundMode ?? snapToGround;
        private bool refreshing;
        private Vector3? refreshStart;
        public double Length { get { Refresh(); return lengths[lengths.Length - 1]; } }
        public int PointCount => points == null ? 0 : points.Length;
        public bool IsUsedByStory()
        {
            var hasTimeline = false;
            foreach (var director in FindObjectsOfType<PlayableDirector>(true))
            {
                if (director.gameObject.scene != gameObject.scene || !(director.playableAsset is TimelineAsset timeline)) continue;
                hasTimeline = true;
                foreach (var track in timeline.GetOutputTracks())
                {
                    if (!(track is StagePerformanceTrack) || track.mutedInHierarchy) continue;
                    foreach (var clip in track.GetClips())
                        if (clip.asset is StagePerformanceClip data && data.mode == StagePerformanceMode.Move && data.route.Resolve(director) == this)
                            return true;
                }
            }
            // 未接入剧情的独立路线仍可编辑；剧情场景只绘制还被片段引用的路线。
            return !hasTimeline;
        }
        public void Invalidate() { samples = null; lengths = null; }
        private void OnValidate() { Invalidate(); }

        public Vector3 WorldPoint(int index)
        {
            if (index == 0)
            {
                if (refreshing && refreshStart.HasValue) return refreshStart.Value;
                if (!refreshing && TryGetLinkedStart(out var linked)) return linked;
            }
            if (index == 0 && startAnchor != null) return startAnchor.position;
            if (index == PointCount - 1 && endAnchor != null) return endAnchor.position;
            return transform.TransformPoint(points[index]);
        }

        // 只读取前段终点，不改写场景锚点，重排、撤销和删除都按当前顺序求值。
        public bool TryGetLinkedStart(out Vector3 position)
        {
            position = default;
            if (!autoConnectPrevious) return false;
            var usages = 0;
            var linked = false;
            foreach (var director in FindObjectsOfType<PlayableDirector>(true))
            {
                if (director.gameObject.scene != gameObject.scene || !(director.playableAsset is TimelineAsset timeline)) continue;
                foreach (var track in timeline.GetOutputTracks().OfType<StagePerformanceTrack>())
                {
                    if (track.mutedInHierarchy) continue;
                    var ordered = track.GetClips().OrderBy(c => c.start).ToArray();
                    for (var index = 0; index < ordered.Length; index++)
                    {
                        var current = (StagePerformanceClip)ordered[index].asset;
                        if (current.mode != StagePerformanceMode.Move || current.route.Resolve(director) != this) continue;
                        // 共享路线无法同时拥有不同的起点，保留显式路线数据。
                        if (++usages > 1) return false;
                        if (index == 0) continue;
                        var source = StagePerformanceTrack.PlacementSource(ordered[index - 1]);
                        if (source == ordered[index]) continue; // 开头停留本来就跟随本路线起点。
                        var data = (StagePerformanceClip)source.asset;
                        if (data.mode == StagePerformanceMode.Move)
                        {
                            var previous = data.route.Resolve(director);
                            if (previous == null || previous == this || previous.PointCount < 2) continue;
                            position = previous.WorldPoint(previous.PointCount - 1);
                            try { position = previous.ProjectToGround(position, data.UsesGround(previous)); }
                            catch (InvalidOperationException) { /* 地面失效时保留原始点，允许继续修复路线。 */ }
                        }
                        else
                        {
                            var station = data.station.Resolve(director);
                            if (station == null) continue;
                            position = station.position;
                        }
                        linked = true;
                    }
                }
            }
            return linked;
        }

        private Vector3? NeighborPoint(bool atStart)
        {
            if (!curved || !smoothConnections || smoothness <= 0) return null;
            Vector3? found = null;
            foreach (var director in FindObjectsOfType<PlayableDirector>(true))
            {
                if (director.gameObject.scene != gameObject.scene || !(director.playableAsset is TimelineAsset timeline)) continue;
                foreach (var track in timeline.GetOutputTracks())
                {
                    if (!(track is StagePerformanceTrack) || track.mutedInHierarchy) continue;
                    var ordered = track.GetClips().OrderBy(c => c.start).ToArray();
                    for (var index = 0; index < ordered.Length; index++)
                    {
                        var clip = ordered[index];
                        if (!(clip.asset is StagePerformanceClip current) || current.mode != StagePerformanceMode.Move || current.route.Resolve(director) != this) continue;
                        var step = atStart ? -1 : 1;
                        var boundary = clip;
                        for (var cursor = index + step; cursor >= 0 && cursor < ordered.Length; cursor += step)
                        {
                            var other = ordered[cursor];
                            if (Math.Abs(atStart ? other.end - boundary.start : other.start - boundary.end) > 0.000001) break;
                            var data = (StagePerformanceClip)other.asset;
                            // 自动停留只暂停时间，不切断空间路径的贝塞尔切线。
                            if (data.mode == StagePerformanceMode.Hold)
                            {
                                if (!data.followPrevious) break;
                                boundary = other;
                                continue;
                            }
                            var neighbor = data.route.Resolve(director);
                            if (neighbor == null || neighbor == this || neighbor.PointCount < 2 || !neighbor.curved || !neighbor.smoothConnections || neighbor.smoothness <= 0) break;
                            var joint = WorldPoint(atStart ? 0 : PointCount - 1);
                            var endpoint = neighbor.WorldPoint(atStart ? neighbor.PointCount - 1 : 0);
                            var gap = endpoint - joint; if (EffectiveGround && data.UsesGround(neighbor)) gap.y = 0;
                            if (gap.sqrMagnitude > 0.000001f) break;
                            var candidate = neighbor.WorldPoint(atStart ? neighbor.PointCount - 2 : 1);
                            if (found.HasValue && (found.Value - candidate).sqrMagnitude > 0.000001f) return null;
                            found = candidate;
                            break;
                        }
                    }
                }
            }
            return found;
        }

        private Vector3 HandleOffset(int index, Vector3? previous, Vector3? next)
        {
            var point = WorldPoint(index);
            var a = index > 0 ? WorldPoint(index - 1) : previous ?? point;
            var b = index < PointCount - 1 ? WorldPoint(index + 1) : next ?? point;
            var before = Vector3.Distance(a, point);
            var after = Vector3.Distance(point, b);
            var span = before < 0.00001f ? after : after < 0.00001f ? before : Mathf.Min(before, after);
            return (b - a).normalized * span * Mathf.Clamp01(smoothness) * 0.5f;
        }

        private void Refresh()
        {
            refreshStart = TryGetLinkedStart(out var linked) ? linked : (Vector3?)null;
            refreshing = true;
            try { RefreshCore(); }
            finally { refreshing = false; }
        }

        private void RefreshCore()
        {
            if (PointCount < 2) throw new InvalidOperationException("路线至少需要两个点。");
            var hash = (curved ? 1 : 0) ^ (EffectiveGround ? 2 : 0);
            var previous = NeighborPoint(true);
            var next = NeighborPoint(false);
            unchecked { hash = hash * 397 ^ smoothness.GetHashCode(); hash = hash * 397 ^ previous.GetHashCode(); hash = hash * 397 ^ next.GetHashCode(); }
            unchecked { for (var i = 0; i < PointCount; i++) hash = hash * 397 ^ WorldPoint(i).GetHashCode(); }
            unchecked
            {
                if (EffectiveGround && groundSurfaces != null) foreach (var ground in groundSurfaces)
                {
                    hash = hash * 397 ^ (ground != null ? ground.GetInstanceID() : 0);
                    if (ground == null) continue;
                    hash = hash * 397 ^ ground.transform.localToWorldMatrix.GetHashCode();
                    hash = hash * 397 ^ ground.bounds.GetHashCode();
                    if (ground is MeshCollider mesh && mesh.sharedMesh != null)
                        hash = hash * 397 ^ mesh.sharedMesh.GetInstanceID();
                }
            }
            if (samples != null && signature == hash) return;
            var list = new List<Vector3>();
            var directions = new List<Vector3>();
            for (var i = 0; i < PointCount - 1; i++)
            {
                var a = WorldPoint(Math.Max(0, i - 1));
                var b = WorldPoint(i);
                var c = WorldPoint(i + 1);
                var d = WorldPoint(Math.Min(PointCount - 1, i + 2));
                foreach (var point in new[] { a, b, c, d })
                    if (!StageMotionPlan.IsFinite(point.x) || !StageMotionPlan.IsFinite(point.y) || !StageMotionPlan.IsFinite(point.z))
                        throw new InvalidOperationException("路线含无效坐标。");
                var steps = 32;
                var control1 = b + HandleOffset(i, previous, next);
                var control2 = c - HandleOffset(i + 1, previous, next);
                for (var j = 0; j < steps; j++)
                {
                    var t = (float)j / steps;
                    var u = 1 - t;
                    var useCurve = curved && smoothness > 0;
                    list.Add(useCurve ? u * u * u * b + 3 * u * u * t * control1 + 3 * u * t * t * control2 + t * t * t * c : Vector3.Lerp(b, c, t));
                    var direction = useCurve ? 3 * u * u * (control1 - b) + 6 * u * t * (control2 - control1) + 3 * t * t * (c - control2) : c - b;
                    direction.y = 0;
                    directions.Add(direction.sqrMagnitude > 0.000001f ? direction.normalized : (c - b).normalized);
                }
            }
            list.Add(WorldPoint(PointCount - 1));
            var lastDirection = curved && smoothness > 0 ? HandleOffset(PointCount - 1, previous, next) : WorldPoint(PointCount - 1) - WorldPoint(PointCount - 2);
            lastDirection.y = 0;
            directions.Add(lastDirection.sqrMagnitude > 0.000001f ? lastDirection.normalized : directions[directions.Count - 1]);
            // 失败时不能继续复用上次地面的采样缓存。
            samples = null;
            samples = list.ConvertAll(ProjectToGround).ToArray();
            tangents = directions.ToArray();
            lengths = new double[samples.Length];
            for (var i = 1; i < samples.Length; i++) lengths[i] = lengths[i - 1] + Vector3.Distance(samples[i - 1], samples[i]);
            if (lengths[lengths.Length - 1] <= 0.00001) { samples = null; throw new InvalidOperationException("路线长度为零，请移动终点。"); }
            signature = hash;
        }

        public Vector3 ProjectToGround(Vector3 point) => ProjectToGround(point, EffectiveGround);
        public Vector3 ProjectToGround(Vector3 point, bool enabled)
        {
            if (!enabled || groundSurfaces == null || groundSurfaces.Length == 0) return point;
            var ray = new Ray(point + Vector3.up * 100, Vector3.down);
            var nearest = float.PositiveInfinity;
            var result = point;
            foreach (var ground in groundSurfaces)
                if (ground != null && ground.Raycast(ray, out var hit, 200) && hit.distance < nearest)
                { nearest = hit.distance; result = hit.point; }
            if (float.IsPositiveInfinity(nearest)) throw new InvalidOperationException("路线未命中有效地面：请重新绑定白模地面或调整路线点。");
            return result;
        }

        public bool HasFreeSpaceUsage()
        {
            foreach (var director in FindObjectsOfType<PlayableDirector>(true))
            {
                if (director.gameObject.scene != gameObject.scene || !(director.playableAsset is TimelineAsset timeline)) continue;
                foreach (var track in timeline.GetOutputTracks().OfType<StagePerformanceTrack>())
                foreach (var clip in track.GetClips())
                    if (clip.asset is StagePerformanceClip data && data.mode == StagePerformanceMode.Move && data.route.Resolve(director) == this && !data.UsesGround(this)) return true;
            }
            return false;
        }
        public double LengthForGround(bool ground)
        {
            var old = samplingGroundMode; samplingGroundMode = ground;
            try { return Length; }
            finally { samplingGroundMode = old; }
        }
        public void SampleForGround(double progress, bool ground, out Vector3 position, out Vector3 forward)
        {
            var old = samplingGroundMode; samplingGroundMode = ground;
            try { Sample(progress, out position, out forward); }
            finally { samplingGroundMode = old; }
        }

        public void Sample(double normalizedDistance, out Vector3 position, out Vector3 forward)
        {
            if (!StageMotionPlan.IsFinite(normalizedDistance)) throw new ArgumentOutOfRangeException(nameof(normalizedDistance));
            Refresh();
            var distance = Math.Max(0, Math.Min(1, normalizedDistance)) * lengths[lengths.Length - 1];
            var index = Array.BinarySearch(lengths, distance);
            index = Mathf.Clamp(index < 0 ? ~index : index + 1, 1, samples.Length - 1);
            while (index < samples.Length - 1 && lengths[index] - lengths[index - 1] <= 0.000001) index++;
            var segment = lengths[index] - lengths[index - 1];
            var blend = segment > 0 ? (float)((distance - lengths[index - 1]) / segment) : 0;
            position = Vector3.LerpUnclamped(samples[index - 1], samples[index], blend);
            forward = curved && smoothness > 0 ? Vector3.Lerp(tangents[index - 1], tangents[index], blend) : samples[index] - samples[index - 1];
            forward.y = 0;
            if (forward.sqrMagnitude < 0.000001f) forward = Vector3.forward;
            forward.Normalize();
        }

        private void OnDrawGizmos()
        {
            if (PointCount < 2 || !IsUsedByStory()) return;
            try
            {
                Refresh();
                Gizmos.color = new Color(0.2f, 0.9f, 0.7f);
                for (var i = 1; i < samples.Length; i++) Gizmos.DrawLine(samples[i - 1], samples[i]);
                Gizmos.DrawSphere(WorldPoint(0), 0.055f);
                Gizmos.DrawSphere(WorldPoint(PointCount - 1), 0.055f);
            }
            catch (InvalidOperationException) { }
        }
    }
}
