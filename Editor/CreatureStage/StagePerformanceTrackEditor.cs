using System;
using System.Collections.Generic;
using System.Linq;
using AIBuilder.CreatureStage;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace AIBuilder.EditorTools.CreatureStage
{
    [CustomTimelineEditor(typeof(StagePerformanceTrack))]
    public sealed class StagePerformanceTrackEditor : TrackEditor
    {
        private sealed class Snapshot
        {
            public TimelineClip[] clips;
            public double start;
        }

        private static readonly Dictionary<TrackAsset, Snapshot> snapshots = new Dictionary<TrackAsset, Snapshot>();

        static StagePerformanceTrackEditor()
        {
            // 撤销/重做直接恢复原始时间，不把恢复过程当作新的删除。
            Undo.undoRedoPerformed += snapshots.Clear;
            EditorApplication.playModeStateChanged += _ => snapshots.Clear();
        }

        public override TrackDrawOptions GetTrackOptions(TrackAsset track, UnityEngine.Object binding)
        {
            Synchronize(track);
            return base.GetTrackOptions(track, binding);
        }

        public override void OnTrackChanged(TrackAsset track) => Synchronize(track);

        private static void Synchronize(TrackAsset track)
        {
            if (!(track is StagePerformanceTrack) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var stale in snapshots.Keys.Where(t => t == null).ToArray()) snapshots.Remove(stale);
            var clips = track.GetClips().OrderBy(c => c.start).ToArray();
            if (snapshots.TryGetValue(track, out var previous) && !track.lockedInHierarchy &&
                clips.Length < previous.clips.Length && clips.All(previous.clips.Contains))
            {
                var removed = previous.clips.Except(clips).ToArray();
                // 删除会销毁片段资源；跨轨移动、拖动、裁剪不触发补齐。
                if (removed.All(c => c.asset == null))
                {
                    var cursor = previous.start;
                    var changed = false;
                    foreach (var clip in clips)
                    {
                        if (Math.Abs(clip.start - cursor) > .000001)
                        {
                            if (!changed) Undo.RegisterCompleteObjectUndo(track, "删除剧情片段后补齐");
                            clip.start = cursor;
                            changed = true;
                        }
                        cursor += clip.duration;
                    }
                    if (changed)
                    {
                        EditorUtility.SetDirty(track);
                        TimelineEditor.Refresh(RefreshReason.ContentsModified);
                        SceneView.RepaintAll();
                        foreach (var window in Resources.FindObjectsOfTypeAll<StageStoryWindow>()) window.Repaint();
                    }
                }
            }
            snapshots[track] = new Snapshot { clips = clips, start = clips.Length == 0 ? 0 : clips[0].start };
        }
    }
}
