using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditorInternal;
using AIBuilder.CreatureStage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace AIBuilder.EditorTools.CreatureStage
{
    public sealed class StageStoryWindow : EditorWindow
    {
        [SerializeField] private PlayableDirector director;
        private int selected;
        private Vector2 scroll;
        private bool previewing, playing, repeatSegment, ripple = true;
        private double time, lastUpdate;
        private string message;
        private ReorderableList clipList;

        [MenuItem("Tools/场景剧情/打开编排窗口")]
        public static void Open()
        {
            var window = GetWindow<StageStoryWindow>("场景剧情编排");
            window.minSize = new Vector2(430, 520);
            if (window.director == null) window.director = FindObjectsOfType<PlayableDirector>().FirstOrDefault(d => d.playableAsset is TimelineAsset t && t.GetOutputTracks().Any(x => x is StagePerformanceTrack));
        }

        public static void ShowDirector(PlayableDirector target)
        {
            var window = GetWindow<StageStoryWindow>("场景剧情编排");
            window.StopPreview(); window.director = target; window.selected = 0; window.time = target != null ? target.time : 0;
            window.minSize = new Vector2(430, 520); window.Repaint();
        }

        public static void StopAllPreviews()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<StageStoryWindow>()) window.StopPreview();
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            Selection.selectionChanged += SyncSelection;
            SceneView.duringSceneGui += SceneGUI;
            Undo.undoRedoPerformed += StopPreview;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorSceneManager.sceneClosing += SceneClosing;
        }
        private void OnDisable()
        {
            StopPreview();
            EditorApplication.update -= Tick;
            Selection.selectionChanged -= SyncSelection;
            SceneView.duringSceneGui -= SceneGUI;
            Undo.undoRedoPerformed -= StopPreview;
            AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorSceneManager.sceneClosing -= SceneClosing;
        }
        private void PlayModeChanged(PlayModeStateChange state) { StopPreview(); }
        private void SceneClosing(UnityEngine.SceneManagement.Scene scene, bool removing) { StopPreview(); }

        private StagePerformanceTrack Track => director != null && director.playableAsset is TimelineAsset timeline
            ? timeline.GetOutputTracks().OfType<StagePerformanceTrack>().FirstOrDefault() : null;
        private TimelineClip[] Clips => Track == null ? Array.Empty<TimelineClip>() : Track.GetClips().OrderBy(c => c.start).ToArray();

        private void SyncSelection()
        {
            var clips = Clips;
            var native = TimelineEditor.selectedClip;
            var index = Array.FindIndex(clips, c => c == native || c.asset == Selection.activeObject);
            if (index >= 0) { selected = index; Repaint(); SceneView.RepaintAll(); }
        }

        private void Tick()
        {
            if (director != null && !previewing && !EditorApplication.isPlayingOrWillChangePlaymode &&
                AnimationMode.InAnimationMode() && TimelineEditor.inspectedDirector == director)
            {
                if (Math.Abs(time - director.time) > .000001) { time = director.time; Repaint(); }
            }
            if (!playing || director == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var now = EditorApplication.timeSinceStartup;
            time += Math.Min(0.1, now - lastUpdate);
            lastUpdate = now;
            var clips = Clips;
            var end = clips.Length == 0 ? 0 : repeatSegment ? clips[Mathf.Clamp(selected, 0, clips.Length - 1)].end : clips[clips.Length - 1].end;
            if (time >= end)
            {
                if (repeatSegment && clips.Length > 0) time = clips[Mathf.Clamp(selected, 0, clips.Length - 1)].start;
                else { time = end; playing = false; }
            }
            Evaluate();
            Repaint();
        }

        public void StopPreview()
        {
            playing = false;
            if (previewing && director != null) director.Stop();
            previewing = false;
            SceneView.RepaintAll();
        }

        private void Evaluate()
        {
            if (director == null) return;
            if (EditorApplication.isPlaying) { director.time = time; director.Evaluate(); return; }
            if (AnimationMode.InAnimationMode() && !previewing)
            { message = "请先关闭原生 Timeline 的 Preview，再用此窗口预览；也可直接在 Timeline 中拖动时间。"; playing = false; return; }
            previewing = true;
            director.time = time;
            director.Evaluate();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            var next = (PlayableDirector)EditorGUILayout.ObjectField("剧情对象", director, typeof(PlayableDirector), true);
            if (EditorGUI.EndChangeCheck()) { StopPreview(); director = next; selected = 0; }
            if (Track == null) { EditorGUILayout.HelpBox("打开剧情示例，或选择带角色表演轨的 PlayableDirector。", MessageType.Info); return; }
            if (GUILayout.Button("打开原生 Timeline（拖动片段调整时间）"))
            { StopPreview(); TimelineEditor.GetOrCreateWindow().SetTimeline(director); }
            if (Track.animationTrack == null && GUILayout.Button("拆分动画到 Timeline 独立轨道"))
            {
                StopPreview();
                if (AnimationMode.InAnimationMode()) message = "请先关闭 Timeline Preview 再拆分。";
                else
                {
                    director.Stop(); StageAnimationTrackSetup.Create(director, Track); director.RebuildGraph();
                    TimelineEditor.GetOrCreateWindow().SetTimeline(director);
                    TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
                }
            }
            if (Track.animationTrack != null) EditorGUILayout.HelpBox("动画请在 Timeline 的动画轨编辑，重叠片段可混合过渡。移动片段下方可启用动画步态同步，随路线距离、时长和速度曲线自动调整步频。", MessageType.Info);
            var clips = Clips;
            var duration = clips.Length == 0 ? 0 : clips[clips.Length - 1].end;
            EditorGUI.BeginChangeCheck();
            time = EditorGUILayout.Slider("剧情时间（秒）", (float)time, 0, (float)duration);
            if (EditorGUI.EndChangeCheck()) { playing = false; Evaluate(); }
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button(playing ? "暂停预览" : "播放预览"))
                {
                    if (time >= duration) time = 0;
                    playing = !playing;
                    lastUpdate = EditorApplication.timeSinceStartup;
                    Evaluate();
                }
                if (GUILayout.Button("结束预览 / 恢复角色")) StopPreview();
            }
            EditorGUILayout.EndHorizontal();
            repeatSegment = EditorGUILayout.Toggle("循环当前片段", repeatSegment);
            ripple = EditorGUILayout.Toggle("改时长后顺延后续片段", ripple);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Warning);
            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            DrawClipList(clips);
            clips = Clips;
            if (clips.Length > 0)
            {
                selected = Mathf.Clamp(selected, 0, clips.Length - 1);
                DrawClipActions(clips[selected]);
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 停留表演")) AddClip(false);
            if (GUILayout.Button("+ 移动表演")) AddClip(true);
            EditorGUILayout.EndHorizontal();
            clips = Clips;
            if (clips.Length > 0)
            {
                selected = Mathf.Clamp(selected, 0, clips.Length - 1);
                DrawClip(clips[selected]);
                DrawGaitControls(clips[selected]);
            }
            EditorGUILayout.HelpBox("停留片段绑定剧情点；移动片段绑定路线。选中片段后在 Scene 拖动手柄，修改会保存到场景；时间和动作保存在 Timeline。当前尚未实现地形脚锁及接触校准。", MessageType.Info);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndScrollView();
        }

        private void DrawClipList(TimelineClip[] clips)
        {
            if (clipList == null || !clipList.list.Cast<TimelineClip>().SequenceEqual(clips))
            {
                clipList = new ReorderableList(new List<TimelineClip>(clips), typeof(TimelineClip), true, true, false, false);
                clipList.elementHeight = 22;
                clipList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "拖动片段排序 · 保留时长，自动首尾相接");
                clipList.drawElementCallback = (rect, index, active, focused) =>
                {
                    var item = (TimelineClip)clipList.list[index];
                    EditorGUI.LabelField(rect, $"{item.start:F1}–{item.end:F1} 秒  {item.displayName}");
                };
                clipList.onSelectCallback = list =>
                {
                    if (list.index < 0 || list.index >= list.count) return;
                    selected = list.index;
                    TimelineEditor.selectedClip = (TimelineClip)list.list[list.index];
                    SceneView.RepaintAll();
                };
                clipList.onReorderCallbackWithDetails = (list, from, to) =>
                {
                    StopPreview();
                    var moved = (TimelineClip)list.list[to];
                    ReorderClip(Track, from, to);
                    selected = to;
                    time = moved.start;
                    TimelineEditor.selectedClip = moved;
                    director.RebuildGraph();
                    TimelineEditor.Refresh(RefreshReason.ContentsModified);
                    SceneView.RepaintAll();
                    Repaint();
                };
            }
            if (GUIUtility.hotControl == 0)
                clipList.index = clips.Length == 0 ? -1 : Mathf.Clamp(selected, 0, clips.Length - 1);
            clipList.DoLayoutList();
            using (new EditorGUI.DisabledScope(clips.Length == 0))
            {
                if (GUILayout.Button("删除选中片段（后续时间前移）"))
                {
                    StopPreview();
                    var index = Mathf.Clamp(selected, 0, clips.Length - 1);
                    DeleteClip(Track, index);
                    var remaining = Clips;
                    selected = Mathf.Clamp(index, 0, Math.Max(0, remaining.Length - 1));
                    TimelineEditor.selectedClip = remaining.Length == 0 ? null : remaining[selected];
                    time = remaining.Length == 0 ? 0 : remaining[selected].start;
                    director.time = time;
                    director.RebuildGraph();
                    clipList = null;
                    StageStoryHierarchy.Refresh(EditorPrefs.GetBool("AIBuilder.CreatureStage.ShowUnusedObjects", false));
                    TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
                    SceneView.RepaintAll();
                    Repaint();
                }
            }
        }

        public static void DeleteClip(StagePerformanceTrack track, int index)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));
            var ordered = track.GetClips().OrderBy(c => c.start).ToList();
            if (index < 0 || index >= ordered.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var start = ordered[0].start;
            var removed = ordered[index];
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(track, "删除剧情片段");
            track.DeleteClip(removed);
            ordered.RemoveAt(index);
            foreach (var item in ordered) { item.start = start; start += item.duration; }
            EditorUtility.SetDirty(track);
            Undo.FlushUndoRecordObjects();
        }

        public static void ReorderClip(StagePerformanceTrack track, int from, int to)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));
            var ordered = track.GetClips().OrderBy(c => c.start).ToList();
            if (from < 0 || to < 0 || from >= ordered.Count || to >= ordered.Count)
                throw new ArgumentOutOfRangeException(nameof(to));
            if (from == to) return;
            if (ordered.Any(c => !StageMotionPlan.IsFinite(c.duration) || c.duration <= 0))
                throw new InvalidOperationException("片段时长无效，无法排序。");
            var start = ordered[0].start;
            var moved = ordered[from];
            ordered.RemoveAt(from);
            ordered.Insert(to, moved);
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(track, "拖动剧情片段排序");
            foreach (var item in ordered) { item.start = start; start += item.duration; }
            EditorUtility.SetDirty(track);
            Undo.FlushUndoRecordObjects();
        }

        private void DrawClip(TimelineClip clip)
        {
            var data = (StagePerformanceClip)clip.asset;
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            var name = EditorGUILayout.TextField("片段名称", clip.displayName);
            var start = EditorGUILayout.DoubleField("开始（秒）", clip.start);
            var duration = EditorGUILayout.DoubleField("持续（秒）", clip.duration);
            var nativeAnimation = Track.animationTrack != null;
            EditorGUI.BeginDisabledGroup(nativeAnimation);
            var animation = (AnimationClip)EditorGUILayout.ObjectField("动作", data.animation, typeof(AnimationClip), false);
            var actor = director.GetGenericBinding(Track) as StageActorDriver;
            if (actor != null && actor.profile != null && actor.profile.actions != null)
            {
                var actions = actor.profile.actions.Where(a => a != null && a.clip != null).ToArray();
                var choices = new[] { "从动作库选择…" }.Concat(actions.Select(a => a.displayName)).ToArray();
                var chosen = EditorGUILayout.Popup("动作库", 0, choices);
                if (chosen > 0) animation = actions[chosen - 1].clip;
            }
            EditorGUI.EndDisabledGroup();
            var mode = (StagePerformanceMode)EditorGUILayout.Popup("表演类型", (int)data.mode, new[] { "停留表演", "沿路线移动" });
            var point = data.station.Resolve(director);
            var route = data.route.Resolve(director);
            var followPrevious = data.followPrevious;
            if (mode == StagePerformanceMode.Hold)
            {
                followPrevious = EditorGUILayout.Toggle("自动跟随上一段终点", followPrevious);
                var following = followPrevious && StagePerformanceTrack.PlacementSource(clip) != clip;
                using (new EditorGUI.DisabledScope(following))
                    point = (Transform)EditorGUILayout.ObjectField(following ? "备用独立停留点" : "停留点", point, typeof(Transform), true);
                if (following) EditorGUILayout.HelpBox("连续停留向前跟随移动终点；前面没有移动时，以第一段停留点为基准。关闭自动跟随后可独立摆放。", MessageType.Info);
            }
            else
            {
                route = (StageRoute)EditorGUILayout.ObjectField("场景路线", route, typeof(StageRoute), true);
                if (route != null)
                {
                    EditorGUI.BeginChangeCheck();
                    var snap = EditorGUILayout.Toggle("本片段贴合地形", data.UsesGround(route));
                    if (EditorGUI.EndChangeCheck())
                    {
                        StopPreview(); Undo.RecordObject(data, "切换片段贴合地形");
                        data.groundMode = snap ? StageGroundMode.Ground : StageGroundMode.FreeSpace;
                        route.previewGroundMode = snap; route.Invalidate(); EditorUtility.SetDirty(data);
                        EditorSceneManager.MarkSceneDirty(route.gameObject.scene); SceneView.RepaintAll();
                    }
                    if (!data.UsesGround(route)) EditorGUILayout.HelpBox("当前为空中路线：直接使用节点 XYZ 坐标，可自由调整高度。", MessageType.Info);
                }
            }
            EditorGUI.BeginDisabledGroup(nativeAnimation);
            var loop = EditorGUILayout.Toggle("循环动作", data.loop);
            var rate = EditorGUILayout.FloatField("动画倍率", data.playbackRate);
            EditorGUI.EndDisabledGroup();
            var moveCurve = new AnimationCurve(data.movementSpeed == null ? AnimationCurve.Linear(0, 1, 1, 1).keys : data.movementSpeed.keys);
            var animCurve = new AnimationCurve(data.animationSpeed == null ? AnimationCurve.Linear(0, 1, 1, 1).keys : data.animationSpeed.keys);
            if (mode == StagePerformanceMode.Move)
            {
                if (route != null)
                {
                    try
                    {
                        var average = route.LengthForGround(data.UsesGround(route)) / duration;
                        var requested = EditorGUILayout.DoubleField("平均移动速度（米/秒）", average);
                        if (requested > 0 && StageMotionPlan.IsFinite(requested) && Math.Abs(requested - average) > 0.000001)
                            duration = route.LengthForGround(data.UsesGround(route)) / requested;
                    }
                    catch (InvalidOperationException error) { EditorGUILayout.HelpBox(error.Message, MessageType.Warning); }
                }
                moveCurve = EditorGUILayout.CurveField("移动速度曲线", moveCurve, Color.green, new Rect(0, 0, 1, 2));
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("匀速")) { moveCurve = AnimationCurve.Linear(0, 1, 1, 1); GUI.changed = true; }
                if (GUILayout.Button("缓起缓停"))
                {
                    moveCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.2f, 1), new Keyframe(0.8f, 1), new Keyframe(1, 0));
                    GUI.changed = true;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("横轴 0–1 为本段时间，纵轴为相对速度。按曲线面积分配路程，保证到达终点；平均速度通过时长调整。负值按零处理。", MessageType.None);
            }
            using (new EditorGUI.DisabledScope(nativeAnimation || data.distanceSync && mode == StagePerformanceMode.Move))
                animCurve = EditorGUILayout.CurveField("动画速度曲线", animCurve, Color.cyan, new Rect(0, 0, 1, 2));
            var transition = EditorGUILayout.FloatField(nativeAnimation ? "入场朝向缓转（秒）" : "入场衔接（秒）", data.transitionDuration);
            EditorGUILayout.HelpBox(nativeAnimation ? "此项只调整路线片段之间的朝向缓转；动画过渡请在动画轨重叠片段并调整混合曲线。" : "前后片段相接时，从上段末姿态平滑混入本段，同时缓转朝向。0 为直接切换；最长为本段时长的一半。", MessageType.None);
            var offset = EditorGUILayout.FloatField("朝向偏移（度）", data.facingOffset);
            EditorGUI.BeginDisabledGroup(nativeAnimation);
            var sync = EditorGUILayout.Toggle("按校准步幅同步", data.distanceSync);
            if (sync && mode == StagePerformanceMode.Move)
                EditorGUILayout.HelpBox("同步模式由移动距离驱动动画，忽略动画倍率和动画速度曲线。独立调节动画速度请关闭同步。", MessageType.Info);
            var cycleDistance = data.cycleDistance;
            if (sync) cycleDistance = EditorGUILayout.FloatField("一个循环走多远（米）", cycleDistance);
            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck())
            {
                if (!StageMotionPlan.IsFinite(start) || start < 0 || !StageMotionPlan.IsFinite(duration) || duration < 0.05 || !StageMotionPlan.IsFinite(rate) || rate <= 0)
                    message = "时间和倍率无效：时长至少 0.05 秒，开始不能为负。";
                else
                {
                    StopPreview();
                    Undo.RecordObjects(new UnityEngine.Object[] { Track, data, director }, "编辑剧情片段");
                    var delta = duration - clip.duration;
                    if (ripple && Math.Abs(delta) > 0.000001) foreach (var other in Clips) if (other != clip && other.start >= clip.end - 0.000001) other.start += delta;
                    clip.start = start; clip.duration = duration; clip.displayName = name;
                    data.followPrevious = followPrevious;
                    data.animation = animation; data.mode = mode; data.loop = loop; data.playbackRate = rate;
                    data.facingOffset = offset; data.distanceSync = sync; data.cycleDistance = cycleDistance;
                    data.movementSpeed = moveCurve; data.animationSpeed = animCurve;
                    data.transitionDuration = StageMotionPlan.IsFinite(transition) ? Mathf.Max(0, transition) : 0;
                    Bind(data, route, point);
                    EditorUtility.SetDirty(data); EditorUtility.SetDirty(Track); EditorUtility.SetDirty(director);
                    EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
                    director.RebuildGraph();
                    message = null;
                    TimelineEditor.Refresh(RefreshReason.ContentsModified);
                }
            }
        }

        private void DrawGaitControls(TimelineClip routeClip)
        {
            if (Track.animationTrack == null || ((StagePerformanceClip)routeClip.asset).mode != StagePerformanceMode.Move) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("动画步态同步", EditorStyles.boldLabel);
            foreach (var clip in Track.animationTrack.GetClips().Where(c => c.start < routeClip.end && c.end > routeClip.start).ToArray())
            {
                if (!(clip.asset is AnimationPlayableAsset animation) || animation.clip == null) continue;
                EditorGUILayout.LabelField($"{clip.start:F1}–{clip.end:F1} 秒  {clip.displayName}");
                var gait = animation as StageGaitAnimationAsset;
                EditorGUI.BeginChangeCheck();
                var sync = EditorGUILayout.Toggle("动画跟随移动距离", gait != null && gait.synchronizeDistance);
                var distance = gait != null ? gait.cycleDistance : 1f;
                if (sync) distance = EditorGUILayout.FloatField("一循环前进距离（米）", distance);
                if (EditorGUI.EndChangeCheck())
                {
                    if (!StageMotionPlan.IsFinite(distance) || distance <= 0) { message = "循环距离必须大于零。"; continue; }
                    StopPreview();
                    if (gait == null) gait = StageGaitAnimationEditor.Enable(clip, director);
                    Undo.RecordObject(gait, "调整动画步态同步");
                    gait.synchronizeDistance = sync; gait.cycleDistance = distance;
                    EditorUtility.SetDirty(gait); director.RebuildGraph();
                    TimelineEditor.Refresh(RefreshReason.ContentsModified);
                }
                if (gait != null && gait.synchronizeDistance)
                {
                    try
                    {
                        var rate = (gait.SampleTime(Track, director, clip, routeClip.end) - gait.SampleTime(Track, director, clip, routeClip.start)) /
                            Math.Max(.001, Math.Min(clip.end, routeClip.end) - Math.Max(clip.start, routeClip.start));
                        EditorGUILayout.LabelField("本段平均步态倍率", rate.ToString("F2") + " 倍");
                        if (rate > 2 || rate < .5)
                            EditorGUILayout.HelpBox("步态倍率偏离原速较多。可调整移动时长或换用更合适的走 / 跑动画；请先校准循环距离。", MessageType.Warning);
                    }
                    catch (InvalidOperationException error) { EditorGUILayout.HelpBox(error.Message, MessageType.Warning); }
                }
            }
            EditorGUILayout.HelpBox("仅对循环走跑动作开启。距离越大或时间越短，步频越快；移动曲线也会同步。该设置作用于整个动画片段，跨停留时步态暂停；吃东西、观察及拍翅请保持关闭。", MessageType.Info);
        }

        private void DrawClipActions(TimelineClip clip)
        {
            var data = (StagePerformanceClip)clip.asset;
            var route = data.route.Resolve(director);
            var point = data.station.Resolve(director);
            var source = StagePerformanceTrack.PlacementSource(clip);
            var sourceData = (StagePerformanceClip)source.asset;
            var sourceRoute = sourceData.route.Resolve(director);
            var locationTarget = sourceData.mode == StagePerformanceMode.Move && sourceRoute != null
                ? (source.start > clip.start
                    ? (sourceRoute.startAnchor != null ? sourceRoute.startAnchor : sourceRoute.transform)
                    : (sourceRoute.endAnchor != null ? sourceRoute.endAnchor : sourceRoute.transform))
                : sourceData.station.Resolve(director);
            if (GUILayout.Button("定位此段并预览")) { time = clip.start; Evaluate(); FrameTarget(locationTarget); }
            if (GUILayout.Button("选中路线 / 停留点")) Selection.activeGameObject = locationTarget != null ? locationTarget.gameObject : null;
            if (GUILayout.Button("复制此片段到末尾"))
            {
                StopPreview(); Undo.RecordObjects(new UnityEngine.Object[] { Track, director }, "复制剧情片段");
                var end = Clips.Max(c => c.end);
                var copy = Track.CreateClip<StagePerformanceClip>();
                Undo.RegisterCreatedObjectUndo(copy.asset, "复制剧情片段");
                EditorUtility.CopySerialized(data, copy.asset);
                var copied = (StagePerformanceClip)copy.asset;
                copied.route.exposedName = new PropertyName(Guid.NewGuid().ToString("N"));
                copied.station.exposedName = new PropertyName(Guid.NewGuid().ToString("N"));
                StageRoute routeCopy = null;
                Transform pointCopy = null;
                if (route != null && data.mode == StagePerformanceMode.Move)
                {
                    var obj = Instantiate(route.gameObject, route.transform.parent);
                    obj.name = route.name + "（副本）";
                    Undo.RegisterCreatedObjectUndo(obj, "复制路线");
                    routeCopy = obj.GetComponent<StageRoute>();
                    if (route.startAnchor != null) routeCopy.startAnchor = CopyPoint(route.startAnchor, obj.transform);
                    if (route.endAnchor != null) routeCopy.endAnchor = CopyPoint(route.endAnchor, obj.transform);
                }
                if (point != null && data.mode == StagePerformanceMode.Hold) pointCopy = CopyPoint(point, director.transform);
                Bind(copied, routeCopy, pointCopy);
                copy.start = end; copy.duration = clip.duration; copy.displayName = clip.displayName + "（副本）";
                EditorUtility.SetDirty(Track); EditorUtility.SetDirty(director);
                TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
            }
        }

        private void Bind(StagePerformanceClip data, StageRoute route, Transform point)
        {
            if (PropertyName.IsNullOrEmpty(data.route.exposedName)) data.route.exposedName = new PropertyName(Guid.NewGuid().ToString("N"));
            if (PropertyName.IsNullOrEmpty(data.station.exposedName)) data.station.exposedName = new PropertyName(Guid.NewGuid().ToString("N"));
            director.SetReferenceValue(data.route.exposedName, route);
            director.SetReferenceValue(data.station.exposedName, point);
        }

        private void AddClip(bool moving)
        {
            StopPreview();
            Undo.RecordObjects(new UnityEngine.Object[] { Track, director }, "新增剧情片段");
            var oldClips = Clips;
            Vector3? previousEnd = null;
            var previousFacing = Quaternion.identity;
            if (oldClips.Length > 0)
            {
                try
                {
                    StagePerformanceTrack.SamplePlacement(oldClips[oldClips.Length - 1], director, 1, out var end, out previousFacing);
                    previousEnd = end;
                }
                catch (InvalidOperationException error) { message = error.Message; }
            }
            var clip = Track.CreateClip<StagePerformanceClip>();
            Undo.RegisterCreatedObjectUndo(clip.asset, "新增剧情片段");
            clip.start = oldClips.Length == 0 ? 0 : oldClips.Max(c => c.end);
            clip.duration = 3; clip.displayName = moving ? "移动到下一点" : "停留表演";
            var data = (StagePerformanceClip)clip.asset;
            data.mode = moving ? StagePerformanceMode.Move : StagePerformanceMode.Hold;
            var previousMove = oldClips.Select(c => (StagePerformanceClip)c.asset).LastOrDefault(c => c.mode == StagePerformanceMode.Move);
            if (previousMove != null) data.groundMode = previousMove.groundMode;
            data.animation = oldClips.Select(c => (StagePerformanceClip)c.asset).FirstOrDefault(c => c.mode == data.mode)?.animation;
            var anchor = new GameObject("剧情点 " + (oldClips.Length + 1));
            Undo.RegisterCreatedObjectUndo(anchor, "新增剧情点");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(anchor, director.gameObject.scene);
            anchor.transform.SetParent(director.transform, false);
            var actor = director.GetGenericBinding(Track) as StageActorDriver;
            if (actor != null) anchor.transform.position = actor.transform.position;
            if (previousEnd.HasValue) anchor.transform.SetPositionAndRotation(previousEnd.Value, previousFacing);
            StageRoute route = null;
            if (moving)
            {
                var routeObject = new GameObject("路线 " + (oldClips.Length + 1));
                Undo.RegisterCreatedObjectUndo(routeObject, "新增路线");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(routeObject, director.gameObject.scene);
                routeObject.transform.SetParent(director.transform, false);
                route = routeObject.AddComponent<StageRoute>();
                route.startAnchor = anchor.transform;
                route.endAnchor = CopyPoint(anchor.transform, director.transform);
                route.endAnchor.name = "到达点 " + (oldClips.Length + 1);
                route.endAnchor.position += anchor.transform.forward * 2;
                var referenceRoute = oldClips.Where(c => ((StagePerformanceClip)c.asset).mode == StagePerformanceMode.Move).Select(c => ((StagePerformanceClip)c.asset).route.Resolve(director)).LastOrDefault(r => r != null);
                if (referenceRoute != null) { route.groundSurfaces = referenceRoute.groundSurfaces; route.snapToGround = referenceRoute.snapToGround; route.autoBindGround = referenceRoute.autoBindGround; }
                route.points = new[] { route.transform.InverseTransformPoint(anchor.transform.position), route.transform.InverseTransformPoint(route.endAnchor.position) };
            }
            Bind(data, route, anchor.transform);
            if (moving && route != null && route.autoBindGround && data.UsesGround(route))
            {
                var repairResult = StageGroundRepair.Repair(director.gameObject.scene);
                message = route.groundSurfaces != null && route.groundSurfaces.Any(c => c != null) ? null : repairResult;
                try
                {
                    route.startAnchor.position = route.ProjectToGround(route.startAnchor.position, data.UsesGround(route));
                    route.endAnchor.position = route.ProjectToGround(route.endAnchor.position, data.UsesGround(route));
                }
                catch (InvalidOperationException error) { message = error.Message; }
            }
            EditorUtility.SetDirty(Track); EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            selected = Clips.Length - 1;
            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
        }

        private static void FrameTarget(Transform target)
        {
            if (target != null && SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(new Bounds(target.position, Vector3.one * 3), false);
        }

        private static Transform CopyPoint(Transform source, Transform parent)
        {
            var obj = new GameObject(source.name + "（副本）");
            Undo.RegisterCreatedObjectUndo(obj, "复制剧情点");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, parent.gameObject.scene);
            obj.transform.SetParent(parent, false);
            obj.transform.SetPositionAndRotation(source.position, source.rotation);
            return obj.transform;
        }

        private void SceneGUI(SceneView view)
        {
            var clips = Clips;
            if (clips.Length == 0 || EditorApplication.isPlaying) return;
            selected = Mathf.Clamp(selected, 0, clips.Length - 1);
            var data = (StagePerformanceClip)clips[selected].asset;
            if (data.mode == StagePerformanceMode.Move)
            {
                var route = data.route.Resolve(director);
                if (route != null && Selection.activeGameObject == null) StageRouteEditor.DrawHandles(route);
            }
            else
            {
                if (StagePerformanceTrack.PlacementSource(clips[selected]) != clips[selected])
                {
                    try
                    {
                        StagePerformanceTrack.SamplePlacement(clips[selected], director, 1, out var end, out _);
                        Handles.Label(end + Vector3.up * 0.25f, clips[selected].displayName + "（自动跟随路线位置）");
                        Handles.DrawWireDisc(end, Vector3.up, HandleUtility.GetHandleSize(end) * 0.12f);
                    }
                    catch (InvalidOperationException) { }
                    return;
                }
                var point = data.station.Resolve(director);
                if (point == null) return;
                if (Selection.activeGameObject != null && Selection.activeGameObject != point.gameObject) return;
                Handles.Label(point.position + Vector3.up * 0.25f, clips[selected].displayName);
                if (Selection.activeGameObject == point.gameObject) return; // 已有原生 Transform 手柄。
                EditorGUI.BeginChangeCheck();
                var position = Handles.PositionHandle(point.position, point.rotation);
                var rotation = Handles.Disc(point.rotation, position, Vector3.up, HandleUtility.GetHandleSize(position) * 0.7f, false, 15);
                if (EditorGUI.EndChangeCheck())
                { StopPreview(); Undo.RecordObject(point, "编辑停留点"); point.SetPositionAndRotation(position, rotation); EditorUtility.SetDirty(point); }
            }
        }
    }

    public sealed class StagePreviewSaveGuard : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths) { StageStoryWindow.StopAllPreviews(); return paths; }
    }
}
