using System;
using System.Linq;
using AIBuilder.CreatureStage;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace AIBuilder.EditorTools.Tests
{
    public sealed class StageStoryTests
    {
        private GameObject sceneRoot;
        private AnimationClip animation;
        private TimelineAsset timeline;
        private PlayableDirector director;
        private StageActorDriver actor;
        private StageRoute route;
        private StagePerformanceTrack track;

        [SetUp]
        public void Setup()
        {
            sceneRoot = new GameObject("StoryTest");
            var actorObject = Child("Actor", sceneRoot.transform);
            actorObject.transform.position = new Vector3(10, 0, 0);
            var skin = Child("Skin", actorObject.transform);
            Child("Bone", skin.transform);
            actor = actorObject.AddComponent<StageActorDriver>();
            actor.animator = skin.AddComponent<Animator>();
            route = Child("Route", sceneRoot.transform).AddComponent<StageRoute>();
            route.curved = false;
            route.points = new[] { Vector3.zero, new Vector3(0, 0, 4) };
            animation = new AnimationClip();
            AnimationUtility.SetEditorCurve(animation, EditorCurveBinding.FloatCurve("Bone", typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Linear(0, 0, 1, 2));
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            track = timeline.CreateTrack<StagePerformanceTrack>();
            director = sceneRoot.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.playableAsset = timeline;
            director.SetGenericBinding(track, actor);
        }

        [TearDown]
        public void Cleanup()
        {
            if (director != null) director.Stop();
            UnityEngine.Object.DestroyImmediate(sceneRoot);
            foreach (var clip in track.GetClips()) UnityEngine.Object.DestroyImmediate(clip.asset);
            UnityEngine.Object.DestroyImmediate(track);
            UnityEngine.Object.DestroyImmediate(timeline);
            UnityEngine.Object.DestroyImmediate(animation);
        }

        private TimelineClip Add(double start, double duration, StagePerformanceMode mode)
        {
            var clip = track.CreateClip<StagePerformanceClip>();
            clip.start = start; clip.duration = duration;
            var data = (StagePerformanceClip)clip.asset;
            data.animation = animation; data.mode = mode;
            data.route.exposedName = new PropertyName(Guid.NewGuid().ToString());
            director.SetReferenceValue(data.route.exposedName, route);
            var station = Child("Station", sceneRoot.transform).transform;
            station.position = new Vector3(0, 0, 4);
            data.station.exposedName = new PropertyName(Guid.NewGuid().ToString());
            director.SetReferenceValue(data.station.exposedName, station);
            return clip;
        }

        private void Seek(double time) { director.time = time; director.Evaluate(); }

        [Test]
        public void BasicExample_UsesPrimitivesAndSamplesScaleRotationAndFlight()
        {
            var folder = "Assets/SceneStoryTest_" + Guid.NewGuid().ToString("N");
            var previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var bootstrap = string.IsNullOrEmpty(previousScene.path) ? folder + "_Bootstrap.unity" : null;
            if (bootstrap != null) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(previousScene, bootstrap);
            PlayableDirector sample = null;
            try
            {
                sample = AIBuilder.EditorTools.CreatureStage.StageBasicExample.Build(folder);
                var asset = (TimelineAsset)sample.playableAsset;
                var movement = asset.GetOutputTracks().OfType<StagePerformanceTrack>().Single();
                Assert.That(movement.GetClips().Count(), Is.EqualTo(5));
                Assert.That(movement.animationTrack.GetClips().Count(), Is.EqualTo(3));
                var driver = (StageActorDriver)sample.GetGenericBinding(movement);
                var visual = driver.animator.transform.Find("Visual");
                sample.time = 1; sample.Evaluate();
                Assert.That(visual.localScale.x, Is.GreaterThan(1.2));
                sample.time = 7; sample.Evaluate();
                Assert.That(Math.Abs(Mathf.DeltaAngle(visual.localEulerAngles.y, 180)), Is.LessThan(2));
                sample.time = 10; sample.Evaluate();
                Assert.That(driver.transform.position.y, Is.GreaterThan(.5));
                Assert.That(AssetDatabase.GetDependencies(folder + "/BasicStory.unity", true).Any(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)), Is.False);
                var ground = sample.GetComponent<StageGroundSettings>();
                Assert.That(ground.groundRoot.name, Is.EqualTo("Ground_Plane"));
                Assert.That(AIBuilder.EditorTools.CreatureStage.StageGroundRepair.Repair(sample.gameObject.scene), Does.Contain("地面 1 块"));
            }
            finally
            {
                if (sample != null) { sample.Stop(); UnityEditor.SceneManagement.EditorSceneManager.CloseScene(sample.gameObject.scene, true); }
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
                AssetDatabase.DeleteAsset(folder);
                if (bootstrap != null) AssetDatabase.DeleteAsset(bootstrap);
            }
        }

        [Test]
        public void PreviewClock_RepairsOwnedManualDirectorWithoutMovingTimeOrClips()
        {
            var clip = Add(0, 4, StagePerformanceMode.Move);
            var playback = sceneRoot.AddComponent<StageStoryPlayback>(); playback.director = director;
            director.time = 1.25;
            Assert.That(AIBuilder.EditorTools.CreatureStage.StageStoryPreviewClock.EnsurePreviewClock(playback), Is.True);
            Assert.That(director.timeUpdateMode, Is.EqualTo(DirectorUpdateMode.GameTime));
            Assert.That(director.time, Is.EqualTo(1.25)); Assert.That(clip.start, Is.EqualTo(0)); Assert.That(clip.duration, Is.EqualTo(4));
            Assert.That(AIBuilder.EditorTools.CreatureStage.StageStoryPreviewClock.EnsurePreviewClock(playback), Is.False);
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            Assert.That(AIBuilder.EditorTools.CreatureStage.StageStoryPreviewClock.EnsurePreviewClock(playback), Is.False);
        }

        [Test]
        public void PlaybackClock_ManualOnlyDuringRuntimeOwnershipAndRestoredOnDisable()
        {
            Add(0, 4, StagePerformanceMode.Move);
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            var playback = sceneRoot.AddComponent<StageStoryPlayback>(); playback.director = director;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(StageStoryPlayback).GetMethod("Start", flags).Invoke(playback, null);
            Assert.That(director.timeUpdateMode, Is.EqualTo(DirectorUpdateMode.Manual));
            playback.AdvancePlayback(.05);
            Assert.That(director.time, Is.EqualTo(.05).Within(.00001));
            typeof(StageStoryPlayback).GetMethod("OnDisable", flags).Invoke(playback, null);
            Assert.That(director.timeUpdateMode, Is.EqualTo(DirectorUpdateMode.GameTime));
            Assert.That(actor.transform.position.x, Is.EqualTo(10).Within(.001));
            director.time = 1.5;
            typeof(StageStoryPlayback).GetMethod("OnDisable", flags).Invoke(playback, null);
            Assert.That(director.time, Is.EqualTo(1.5));
        }

        [Test]
        public void GaitSync_NativePoseFollowsDistanceDurationAndSeeking()
        {
            var move = Add(0, 4, StagePerformanceMode.Move);
            var native = AIBuilder.EditorTools.CreatureStage.StageAnimationTrackSetup.Create(director, track);
            var clip = native.GetClips().Single();
            var gait = AIBuilder.EditorTools.CreatureStage.StageGaitAnimationEditor.Enable(clip, director);
            gait.cycleDistance = 2;
            Seek(.5); // 0.5 米 = 四分之一循环。
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(.5).Within(.015));
            route.points[1] = new Vector3(0, 0, 8); route.Invalidate();
            Seek(.5);
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(1).Within(.015));
            move.duration = 2;
            Seek(.25);
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(1).Within(.015));
            Seek(1.7); Seek(.25);
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(1).Within(.015));
            gait.synchronizeDistance = false; Seek(.25);
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(.5).Within(.015));
            director.Stop(); UnityEngine.Object.DestroyImmediate(gait); UnityEngine.Object.DestroyImmediate(native);
        }

        [Test]
        public void GaitSync_IntegratesCurvesAcrossHoldsAndPreservesNativeBlend()
        {
            var move = Add(0, 2, StagePerformanceMode.Move);
            ((StagePerformanceClip)move.asset).movementSpeed = AnimationCurve.Linear(0, 0, 1, 2);
            var hold = Add(2, 2, StagePerformanceMode.Hold);
            var idle = new AnimationClip();
            AnimationUtility.SetEditorCurve(idle, EditorCurveBinding.FloatCurve("Bone", typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Linear(0, 4, 1, 4));
            ((StagePerformanceClip)hold.asset).animation = idle;
            var native = AIBuilder.EditorTools.CreatureStage.StageAnimationTrackSetup.Create(director, track);
            var clips = native.GetClips().OrderBy(c => c.start).ToArray();
            var gait = AIBuilder.EditorTools.CreatureStage.StageGaitAnimationEditor.Enable(clips[0], director);
            gait.cycleDistance = 8;
            Assert.That(gait.SampleTime(track, director, clips[0], 1), Is.EqualTo(.125).Within(.001));
            Seek(1);
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(.25).Within(.015));
            Seek(2.125);
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(2.5).Within(.06));
            Seek(2.01); Seek(2.125);
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(2.5).Within(.06));
            Assert.That(gait.SampleTime(track, director, clips[0], 2.2), Is.EqualTo(.5).Within(.001));
            director.Stop(); foreach (var c in clips) UnityEngine.Object.DestroyImmediate(c.asset);
            UnityEngine.Object.DestroyImmediate(native); UnityEngine.Object.DestroyImmediate(idle);
        }

        [Test]
        public void GaitSync_AdjacentAnimationClipsKeepPhaseAndConversionSupportsUndo()
        {
            Add(0, 2, StagePerformanceMode.Move); Add(2, 2, StagePerformanceMode.Move);
            var native = AIBuilder.EditorTools.CreatureStage.StageAnimationTrackSetup.Create(director, track);
            var clips = native.GetClips().OrderBy(c => c.start).ToArray();
            var first = AIBuilder.EditorTools.CreatureStage.StageGaitAnimationEditor.Enable(clips[0], director);
            first.cycleDistance = 8;
            Undo.IncrementCurrentGroup();
            var original = clips[1].asset;
            var second = AIBuilder.EditorTools.CreatureStage.StageGaitAnimationEditor.Enable(clips[1], director);
            second.cycleDistance = 8;
            Assert.That(second.clip, Is.SameAs(animation));
            Assert.That(clips[1].start, Is.EqualTo(2));
            Assert.That(clips[0].blendOutDuration, Is.EqualTo(.25));
            Assert.That(first.SampleTime(track, director, clips[0], 2.125), Is.EqualTo(second.SampleTime(track, director, clips[1], 2.125)).Within(.001));
            Seek(2.125);
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(1.0625).Within(.025));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(clips[1].asset, Is.TypeOf<AnimationPlayableAsset>());
            Assert.That(((AnimationPlayableAsset)clips[1].asset).clip, Is.SameAs(animation));
            director.RebuildGraph(); Seek(3);
            director.Stop(); foreach (var c in native.GetClips()) UnityEngine.Object.DestroyImmediate(c.asset);
            UnityEngine.Object.DestroyImmediate(native);
        }

        [Test]
        public void GroundToFlight_ConnectsToProjectedEndpointWithoutChangingAirDestination()
        {
            var ground = Child("Ground", sceneRoot.transform).AddComponent<BoxCollider>();
            ground.size = new Vector3(100, 1, 100);
            ground.transform.position = new Vector3(0, -.5f, 0); Physics.SyncTransforms();
            route.groundSurfaces = new Collider[] { ground };
            route.points = new[] { new Vector3(0, 5, 0), new Vector3(0, 5, 4) };
            var walking = (StagePerformanceClip)Add(0, 2, StagePerformanceMode.Move).asset;
            walking.groundMode = StageGroundMode.Ground;
            var clip = Add(2, 2, StagePerformanceMode.Move);
            var flying = (StagePerformanceClip)clip.asset;
            var air = Child("AirRoute", sceneRoot.transform).AddComponent<StageRoute>();
            air.curved = false;
            air.points = new[] { new Vector3(50, 50, 50), new Vector3(0, 8, 8) };
            flying.groundMode = StageGroundMode.FreeSpace;
            director.SetReferenceValue(flying.route.exposedName, air);
            Seek(2); Assert.That(actor.transform.position, Is.EqualTo(new Vector3(0, 0, 4)));
            Seek(3); Assert.That(actor.transform.position, Is.EqualTo(new Vector3(0, 4, 6)));
            Assert.That(air.points[1], Is.EqualTo(new Vector3(0, 8, 8)));
        }

        [Test]
        public void SharedRoute_PerClipGroundModesAndFollowingHoldsRemainIndependent()
        {
            var ground = Child("Ground", sceneRoot.transform).AddComponent<BoxCollider>();
            ground.size = new Vector3(100, 1, 100);
            ground.transform.position = new Vector3(0, -.5f, 0); Physics.SyncTransforms();
            route.groundSurfaces = new Collider[] { ground };
            route.points = new[] { new Vector3(0, 5, 0), new Vector3(0, 9, 4) };
            var walk = (StagePerformanceClip)Add(0, 2, StagePerformanceMode.Move).asset;
            Add(2, 1, StagePerformanceMode.Hold);
            var fly = (StagePerformanceClip)Add(3, 2, StagePerformanceMode.Move).asset;
            Add(5, 1, StagePerformanceMode.Hold);
            walk.groundMode = StageGroundMode.Ground;
            fly.groundMode = StageGroundMode.FreeSpace;
            route.previewGroundMode = false; // 编辑器选择不得影响其他片段的运行采样。
            Seek(1); Assert.That(actor.transform.position.y, Is.EqualTo(0).Within(.001));
            Seek(2.5); Assert.That(actor.transform.position.y, Is.EqualTo(0).Within(.001));
            Seek(4); Assert.That(actor.transform.position.y, Is.EqualTo(7).Within(.001));
            Seek(5.5); Assert.That(actor.transform.position.y, Is.EqualTo(9).Within(.001));
            Seek(1); Assert.That(actor.transform.position.y, Is.EqualTo(0).Within(.001));
            Assert.That(route.LengthForGround(false), Is.GreaterThan(route.LengthForGround(true)));
            Assert.That(route.HasFreeSpaceUsage(), Is.True);
            Assert.That(route.points[0].y, Is.EqualTo(5));
            Assert.That(route.snapToGround, Is.True);
        }

        [Test]
        public void FlightRoute_ToggleUsesXYZAndInvalidatesGroundCache()
        {
            var ground = Child("Ground", sceneRoot.transform).AddComponent<BoxCollider>();
            ground.size = new Vector3(100, 1, 100);
            ground.transform.position = new Vector3(0, -.5f, 0);
            Physics.SyncTransforms();
            route.groundSurfaces = new Collider[] { ground };
            route.points = new[] { new Vector3(0, 5, 0), new Vector3(0, 9, 4) };
            route.Sample(.5, out var grounded, out _);
            Assert.That(grounded.y, Is.EqualTo(0).Within(.001));
            route.snapToGround = false;
            route.Sample(.5, out var airborne, out _);
            Assert.That(airborne, Is.EqualTo(new Vector3(0, 7, 2)));
            Assert.That(route.ProjectToGround(new Vector3(1, 20, 1)).y, Is.EqualTo(20));
            ground.transform.position = new Vector3(0, 2, 0); Physics.SyncTransforms();
            route.Sample(.5, out var unchanged, out _);
            Assert.That(unchanged, Is.EqualTo(airborne));
            route.snapToGround = true;
            route.Sample(.5, out grounded, out _);
            Assert.That(grounded.y, Is.EqualTo(2.5).Within(.001));
            Assert.That(route.points[0].y, Is.EqualTo(5));
        }

        [Test]
        public void StartAlignment_ShiftsBothTracksAndPreservesOverlap()
        {
            Add(2.1666667, 2, StagePerformanceMode.Move);
            Add(4.1666667, 2, StagePerformanceMode.Hold);
            var native = AIBuilder.EditorTools.CreatureStage.StageAnimationTrackSetup.Create(director, track);
            var before = native.GetClips().OrderBy(c => c.start).ToArray();
            var overlap = before[0].end - before[1].start;
            AIBuilder.EditorTools.CreatureStage.StageStoryStart.Align(director);
            Assert.That(track.GetClips().Min(c => c.start), Is.EqualTo(0));
            Assert.That(native.GetClips().Min(c => c.start), Is.EqualTo(0));
            Assert.That(before[0].end - before[1].start, Is.EqualTo(overlap).Within(.00001));
            var playback = sceneRoot.AddComponent<StageStoryPlayback>();
            playback.director = director;
            director.time = 3;
            playback.Restart();
            typeof(StageStoryPlayback).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(playback, null);
            Assert.That(director.time, Is.EqualTo(0));
            playback.AdvancePlayback(2.5);
            Assert.That(director.time, Is.EqualTo(.1).Within(.00001));
            playback.AdvancePlayback(.02);
            Assert.That(director.time, Is.EqualTo(.12).Within(.00001));
            director.Stop();
            foreach (var clip in native.GetClips()) UnityEngine.Object.DestroyImmediate(clip.asset);
            UnityEngine.Object.DestroyImmediate(native);
        }

        [Test]
        public void NativeAnimationTrack_PreservesAnimationSpeedCurveByBaking()
        {
            var clip = Add(0, 2, StagePerformanceMode.Move);
            var data = (StagePerformanceClip)clip.asset;
            data.animationSpeed = AnimationCurve.Linear(0, .25f, 1, .75f);
            data.loop = false;
            var native = AIBuilder.EditorTools.CreatureStage.StageAnimationTrackSetup.Create(director, track);
            var migrated = (AnimationPlayableAsset)native.GetClips().Single().asset;
            Assert.That(migrated.clip, Is.Not.SameAs(animation));
            Seek(.5);
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(2 * data.SourceTime(.5, 2, 4)).Within(.01));
            Assert.That(actor.transform.position.z, Is.EqualTo(1).Within(.001));
            director.Stop();
            UnityEngine.Object.DestroyImmediate(migrated.clip);
            UnityEngine.Object.DestroyImmediate(migrated);
            UnityEngine.Object.DestroyImmediate(native);
        }

        [Test]
        public void NativeAnimationTrack_BlendsIndependentlyWhileRouteStillMoves()
        {
            var first = Add(0, 2, StagePerformanceMode.Move);
            var last = Add(2, 2, StagePerformanceMode.Hold);
            var secondAnimation = new AnimationClip();
            AnimationUtility.SetEditorCurve(secondAnimation, EditorCurveBinding.FloatCurve("Bone", typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Linear(0, 4, 1, 4));
            ((StagePerformanceClip)last.asset).animation = secondAnimation;
            var native = AIBuilder.EditorTools.CreatureStage.StageAnimationTrackSetup.Create(director, track);
            Assert.That(director.GetGenericBinding(native), Is.SameAs(actor.animator));
            var clips = native.GetClips().OrderBy(c => c.start).ToArray();
            Assert.That(clips[0].end, Is.GreaterThan(clips[1].start));
            Seek(1);
            Assert.That(actor.transform.position.z, Is.EqualTo(2).Within(.001));
            Seek(2.125);
            var bone = actor.animator.transform.Find("Bone").localPosition.x;
            Assert.That(bone, Is.GreaterThan(.3).And.LessThan(3.9));
            Seek(3);
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(4).Within(.001));
            ((StagePerformanceClip)first.asset).animation = null;
            Seek(1); // 路线片段已无需动画，不应与原生动画图抢控制权。
            Assert.That(actor.transform.position.z, Is.EqualTo(2).Within(.001));
            director.Stop();
            Assert.That(actor.transform.position.x, Is.EqualTo(10).Within(.001));
            foreach (var c in native.GetClips()) UnityEngine.Object.DestroyImmediate(c.asset);
            UnityEngine.Object.DestroyImmediate(native);
            UnityEngine.Object.DestroyImmediate(secondAnimation);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void PanelDelete_ReflowsTimePreservesReferencesAndSupportsUndo(int index)
        {
            Add(2, 2, StagePerformanceMode.Move);
            Add(4, .6, StagePerformanceMode.Hold);
            Add(4.6, 3, StagePerformanceMode.Move);
            var original = track.GetClips().OrderBy(c => c.start).ToArray();
            var durations = original.Where((c, i) => i != index).Select(c => c.duration).ToArray();
            var assets = original.Where((c, i) => i != index).Select(c => c.asset).ToArray();
            AIBuilder.EditorTools.CreatureStage.StageStoryWindow.DeleteClip(track, index);
            var remaining = track.GetClips().OrderBy(c => c.start).ToArray();
            Assert.That(remaining.Length, Is.EqualTo(2));
            Assert.That(remaining.Select(c => c.asset), Is.EqualTo(assets));
            Assert.That(remaining.Select(c => c.duration), Is.EqualTo(durations));
            Assert.That(remaining[0].start, Is.EqualTo(2));
            Assert.That(remaining[1].start, Is.EqualTo(remaining[0].end));
            Assert.That(route, Is.Not.Null);
            Undo.PerformUndo();
            Assert.That(track.GetClips().OrderBy(c => c.start).Select(c => c.start), Is.EqualTo(new[] { 2.0, 4.0, 4.6 }));
            for (var i = 0; i < 3; i++) AIBuilder.EditorTools.CreatureStage.StageStoryWindow.DeleteClip(track, 0);
            Assert.That(track.GetClips(), Is.Empty);
            Undo.PerformUndo();
            Assert.That(track.GetClips().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Hierarchy_TracksCurrentReferencesAndRestoresAfterUndo()
        {
            var moving = Add(0, 2, StagePerformanceMode.Move);
            var old = Child("路线旧", sceneRoot.transform).AddComponent<StageRoute>();
            var orphan = Child("剧情点 99", sceneRoot.transform);
            var decoration = Child("保留场景物体", sceneRoot.transform);
            var point = Child("到达点 1", sceneRoot.transform).transform;
            point.position = new Vector3(0, 0, 4);
            route.endAnchor = point;
            AIBuilder.EditorTools.CreatureStage.StageStoryHierarchy.Refresh();
            Assert.That(old.gameObject.hideFlags.HasFlag(HideFlags.HideInHierarchy), Is.True);
            Assert.That(orphan.hideFlags.HasFlag(HideFlags.HideInHierarchy), Is.True);
            Assert.That(route.gameObject.hideFlags.HasFlag(HideFlags.HideInHierarchy), Is.False);
            Assert.That(point.gameObject.hideFlags.HasFlag(HideFlags.HideInHierarchy), Is.False);
            Assert.That(decoration.hideFlags, Is.EqualTo(HideFlags.None));
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(track, "删除剧情片段");
            track.DeleteClip(moving); Undo.FlushUndoRecordObjects();
            AIBuilder.EditorTools.CreatureStage.StageStoryHierarchy.Refresh();
            Assert.That(route.gameObject.hideFlags.HasFlag(HideFlags.HideInHierarchy), Is.True);
            Undo.PerformUndo();
            AIBuilder.EditorTools.CreatureStage.StageStoryHierarchy.Refresh();
            Assert.That(route.gameObject.hideFlags.HasFlag(HideFlags.HideInHierarchy), Is.False);
            Assert.That(point.position, Is.EqualTo(new Vector3(0, 0, 4)));
            AIBuilder.EditorTools.CreatureStage.StageStoryHierarchy.Refresh(true);
            Assert.That(orphan.hideFlags.HasFlag(HideFlags.HideInHierarchy), Is.False);
        }

        [Test]
        public void DragSort_PreservesClipsDurationsAndUndoRestoresOriginalTiming()
        {
            var a = Add(1, 2, StagePerformanceMode.Move);
            var b = Add(5, .6, StagePerformanceMode.Hold);
            var c = Add(8, 3, StagePerformanceMode.Move);
            var data = (StagePerformanceClip)c.asset;
            var curve = data.movementSpeed;
            var reference = data.route.exposedName;
            AIBuilder.EditorTools.CreatureStage.StageStoryWindow.ReorderClip(track, 2, 0);
            var ordered = track.GetClips().OrderBy(x => x.start).ToArray();
            Assert.That(ordered, Is.EqualTo(new[] { c, a, b }));
            Assert.That(c.start, Is.EqualTo(1));
            Assert.That(a.start, Is.EqualTo(4));
            Assert.That(b.start, Is.EqualTo(6));
            Assert.That(c.duration, Is.EqualTo(3));
            Assert.That(b.duration, Is.EqualTo(.6).Within(.00001));
            Assert.That(data.movementSpeed, Is.SameAs(curve));
            Assert.That(data.route.exposedName, Is.EqualTo(reference));
            Seek(4.5);
            Undo.PerformUndo();
            Seek(5.2); // 不重建播放图，验证撤销后不会复用旧片段顺序。
            ordered = track.GetClips().OrderBy(x => x.start).ToArray();
            Assert.That(ordered.Select(x => x.start), Is.EqualTo(new double[] { 1, 5, 8 }));
            AIBuilder.EditorTools.CreatureStage.StageStoryWindow.ReorderClip(track, 0, 2);
            ordered = track.GetClips().OrderBy(x => x.start).ToArray();
            Assert.That(ordered.Select(x => x.duration), Is.EqualTo(new double[] { .6, 3, 2 }).Within(.00001));
            Assert.That(ordered[1].start, Is.EqualTo(ordered[0].end));
            Assert.That(ordered[2].start, Is.EqualTo(ordered[1].end));
        }

        [Test]
        public void Routes_ReorderDeleteAndUndoConnectWithoutChangingAuthoredNodes()
        {
            var a = Add(0, 2, StagePerformanceMode.Move);
            var hold = Add(2, 1, StagePerformanceMode.Hold);
            var b = Add(3, 2, StagePerformanceMode.Move);
            var second = Child("SecondRoute", sceneRoot.transform).AddComponent<StageRoute>();
            second.curved = false;
            second.points = new[] { new Vector3(20, 0, 20), new Vector3(2, 0, 6), new Vector3(5, 0, 8) };
            var authored = (Vector3[])second.points.Clone();
            director.SetReferenceValue(((StagePerformanceClip)b.asset).route.exposedName, second);
            second.Sample(0, out var start, out _);
            Assert.That(start, Is.EqualTo(route.WorldPoint(1)));
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(track, "重排路线");
            b.start = 0; a.start = 2; hold.start = 4;
            Undo.FlushUndoRecordObjects();
            route.Sample(0, out start, out _);
            Assert.That(start, Is.EqualTo(second.WorldPoint(2)));
            second.Sample(0, out start, out _);
            Assert.That(start, Is.EqualTo(authored[0]));
            director.RebuildGraph(); Seek(2);
            Assert.That(Vector3.Distance(actor.transform.position, second.WorldPoint(2)), Is.LessThan(0.001));
            Assert.That(second.points, Is.EqualTo(authored));
            Undo.PerformUndo();
            second.Sample(0, out start, out _);
            Assert.That(start, Is.EqualTo(route.WorldPoint(1)));
            a = track.GetClips().First(c => c.start == 0);
            track.DeleteClip(a);
            second.Sample(0, out start, out _);
            var leading = (StagePerformanceClip)track.GetClips().OrderBy(c => c.start).First().asset;
            Assert.That(start, Is.EqualTo(leading.station.Resolve(director).position));
            Assert.That(second.points, Is.EqualTo(authored));
        }

        [Test]
        public void Routes_ManualConnectionOptOutPreservesIndependentStart()
        {
            Add(0, 2, StagePerformanceMode.Hold);
            var move = Add(2, 2, StagePerformanceMode.Move);
            var first = (StagePerformanceClip)track.GetClips().First().asset;
            first.followPrevious = false;
            first.station.Resolve(director).position = new Vector3(2, 0, 1);
            route.Sample(0, out var start, out _);
            // 单独放置的停留点可以明确指定下一条路线的出发位置。
            Assert.That(start, Is.EqualTo(first.station.Resolve(director).position));
            route.autoConnectPrevious = false;
            route.Sample(0, out start, out _);
            Assert.That(start, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void LeadingHolds_KeepFirstStationAndDriveLaterMoveStart()
        {
            var first = Add(0, 2, StagePerformanceMode.Hold);
            var second = Add(2, 2, StagePerformanceMode.Hold);
            var move = Add(4, 2, StagePerformanceMode.Move);
            var after = Add(6, 2, StagePerformanceMode.Hold);
            var afterAgain = Add(8, 2, StagePerformanceMode.Hold);
            var station = ((StagePerformanceClip)first.asset).station.Resolve(director);
            station.position = new Vector3(-4, 0, 3);
            Seek(0);
            Assert.That(actor.transform.position, Is.EqualTo(station.position));
            station.position = new Vector3(-2, 0, 1);
            Seek(3);
            Assert.That(actor.transform.position, Is.EqualTo(station.position));
            Seek(4);
            Assert.That(actor.transform.position, Is.EqualTo(station.position));
            route.points[1] = new Vector3(6, 0, 9);
            Seek(7);
            Assert.That(actor.transform.position, Is.EqualTo(route.points[1]));
            Seek(9);
            Assert.That(actor.transform.position, Is.EqualTo(route.points[1]));
            Assert.That(StagePerformanceTrack.PlacementSource(afterAgain), Is.SameAs(move));
            track.DeleteClip(move);
            director.RebuildGraph(); Seek(9);
            Assert.That(actor.transform.position, Is.EqualTo(station.position));
            Assert.That(StagePerformanceTrack.PlacementSource(second), Is.SameAs(first));
        }

        [Test]
        public void Hold_FollowsLiveEndpointThroughChainAndDirectSeek()
        {
            Add(0, 2, StagePerformanceMode.Move);
            var hold = Add(2, 2, StagePerformanceMode.Hold);
            var last = Add(4, 2, StagePerformanceMode.Hold);
            ((StagePerformanceClip)hold.asset).station.Resolve(director).position = Vector3.one * 50;
            route.endAnchor = Child("End", sceneRoot.transform).transform;
            route.endAnchor.position = new Vector3(3, 0, 8);
            Seek(5);
            Assert.That(Vector3.Distance(actor.transform.position, route.endAnchor.position), Is.LessThan(0.001));
            route.endAnchor.position = new Vector3(6, 0, 9);
            Seek(3);
            Assert.That(Vector3.Distance(actor.transform.position, route.endAnchor.position), Is.LessThan(0.001));
            Seek(5);
            Assert.That(Vector3.Distance(actor.transform.position, route.endAnchor.position), Is.LessThan(0.001));
            Assert.That(StagePerformanceTrack.PlacementSource(last).asset, Is.TypeOf<StagePerformanceClip>());
        }

        [Test]
        public void Hold_ManualOverrideAndFirstHoldKeepTheirStation()
        {
            var first = Add(0, 2, StagePerformanceMode.Hold);
            var manual = Add(2, 2, StagePerformanceMode.Hold);
            var following = Add(4, 2, StagePerformanceMode.Hold);
            var data = (StagePerformanceClip)manual.asset;
            data.followPrevious = false;
            data.station.Resolve(director).position = new Vector3(7, 0, 2);
            Seek(1);
            Assert.That(actor.transform.position, Is.EqualTo(new Vector3(0, 0, 4)));
            Seek(5);
            Assert.That(actor.transform.position, Is.EqualTo(new Vector3(7, 0, 2)));
            Assert.That(StagePerformanceTrack.PlacementSource(following), Is.SameAs(manual));
            manual.start = 6;
            Assert.That(StagePerformanceTrack.PlacementSource(following), Is.SameAs(first));
            track.DeleteClip(first);
            Assert.That(StagePerformanceTrack.PlacementSource(following), Is.SameAs(following));
        }

        [Test]
        public void Timeline_MovesActorAndRestoresOriginalOnStop()
        {
            Add(0, 4, StagePerformanceMode.Move);
            Seek(2);
            Assert.That(Vector3.Distance(actor.transform.position, new Vector3(0, 0, 2)), Is.LessThan(0.0001));
            director.Stop();
            Assert.That(actor.transform.position, Is.EqualTo(new Vector3(10, 0, 0)));
            Assert.That(actor.animator.transform.Find("Bone").localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Timeline_SeekAcrossMoveAndHoldIsDeterministic()
        {
            Add(0, 4, StagePerformanceMode.Move);
            Add(4, 3, StagePerformanceMode.Hold);
            Seek(2.3);
            var position = actor.transform.position;
            var pose = actor.animator.transform.Find("Bone").localPosition;
            Seek(6); Seek(0); Seek(4); Seek(2.3);
            Assert.That(actor.transform.position, Is.EqualTo(position));
            Assert.That(actor.animator.transform.Find("Bone").localPosition, Is.EqualTo(pose));
        }

        [Test]
        public void Timeline_GapsAndEndHoldPriorState()
        {
            Add(0, 2, StagePerformanceMode.Move);
            Add(4, 2, StagePerformanceMode.Hold);
            Seek(3);
            Assert.That(actor.transform.position.z, Is.EqualTo(4).Within(0.001));
            Seek(8);
            Assert.That(actor.transform.position.z, Is.EqualTo(4).Within(0.001));
        }

        [Test]
        public void Timeline_RejectsOverlapAndRestoresActor()
        {
            Add(0, 4, StagePerformanceMode.Move);
            Add(3, 2, StagePerformanceMode.Hold);
            LogAssert.Expect(LogType.Error, "角色剧情：基础表演片段不能重叠，请先错开时间。");
            Seek(3.5);
            Assert.That(actor.transform.position, Is.EqualTo(new Vector3(10, 0, 0)));
        }

        [Test]
        public void Route_MovingAnchorInvalidatesCachedArcLength()
        {
            var end = Child("End", sceneRoot.transform).transform;
            end.position = new Vector3(0, 0, 4);
            route.endAnchor = end;
            Assert.That(route.Length, Is.EqualTo(4).Within(0.001));
            end.position = new Vector3(0, 0, 8);
            route.Sample(0.5, out var position, out _);
            Assert.That(position.z, Is.EqualTo(4).Within(0.001));
        }

        [Test]
        public void Route_CurvePassesEndpointsAndRejectsZeroLength()
        {
            route.curved = true;
            route.points = new[] { Vector3.zero, new Vector3(1, 0, 2), new Vector3(0, 0, 4) };
            route.Sample(0, out var start, out _);
            route.Sample(1, out var end, out _);
            Assert.That(start, Is.EqualTo(Vector3.zero));
            Assert.That(end.z, Is.EqualTo(4).Within(0.001));
            route.points = new[] { Vector3.zero, Vector3.zero };
            Assert.Throws<InvalidOperationException>(() => route.Sample(0.5, out _, out _));
        }

        [Test]
        public void Clip_DistanceSyncAndNonloopClamping()
        {
            var data = (StagePerformanceClip)Add(0, 4, StagePerformanceMode.Move).asset;
            data.distanceSync = true; data.cycleDistance = 2;
            Assert.That(data.SourceTime(1, 4, 4), Is.EqualTo(0.5).Within(0.001));
            data.loop = false;
            Assert.That(data.SourceTime(4, 4, 4), Is.EqualTo(1).Within(0.001));
            data.cycleDistance = 0;
            Assert.Throws<InvalidOperationException>(() => data.SourceTime(1, 4, 4));
        }

        [Test]
        public void SpeedCurves_IntegrateRatesAndAlwaysReachDestination()
        {
            var data = (StagePerformanceClip)Add(0, 4, StagePerformanceMode.Move).asset;
            data.movementSpeed = AnimationCurve.Linear(0, 0, 1, 2);
            data.animationSpeed = AnimationCurve.Linear(0, 0, 1, 2);
            data.playbackRate = 0.25f;
            Assert.That(data.MovementProgress(2, 4), Is.EqualTo(0.25).Within(0.0001));
            Assert.That(data.MovementProgress(4, 4), Is.EqualTo(1).Within(0.0001));
            Assert.That(data.SourceTime(2, 4, 4), Is.EqualTo(0.25).Within(0.0001));
            Seek(2);
            Assert.That(actor.transform.position.z, Is.EqualTo(1).Within(0.001));
            Seek(3); Seek(0); Seek(2);
            Assert.That(actor.transform.position.z, Is.EqualTo(1).Within(0.001));
            data.movementSpeed = AnimationCurve.Linear(0, 0, 1, 0);
            Assert.Throws<InvalidOperationException>(() => data.MovementProgress(2, 4));
        }

        [Test]
        public void Transition_BlendsPoseAndHeadingDeterministicallyThenRestores()
        {
            var first = Add(0, 2, StagePerformanceMode.Hold);
            ((StagePerformanceClip)first.asset).loop = false;
            var second = Add(2, 2, StagePerformanceMode.Hold);
            var data = (StagePerformanceClip)second.asset;
            data.transitionDuration = 0.5f;
            data.facingOffset = 90;
            Seek(2);
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(2).Within(0.001));
            Seek(2.25);
            var pose = actor.animator.transform.Find("Bone").localPosition;
            // 上段末帧 x=2，本段 0.25s 为 x=.5，混合中点为 1.25。
            Assert.That(pose.x, Is.EqualTo(1.25).Within(0.001));
            Assert.That(Quaternion.Angle(Quaternion.identity, actor.transform.rotation), Is.EqualTo(45).Within(0.1));
            Seek(3); Seek(0); Seek(2.25);
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(pose.x).Within(0.001));
            director.Stop();
            Assert.That(actor.animator.transform.Find("Bone").localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Transition_KeepsOutgoingLoopTailInsteadOfWrappingAtBoundary()
        {
            Add(0, 2, StagePerformanceMode.Hold);
            Add(2, 2, StagePerformanceMode.Hold);
            Seek(1.99999);
            var before = actor.animator.transform.Find("Bone").localPosition.x;
            Seek(2);
            Assert.That(actor.animator.transform.Find("Bone").localPosition.x, Is.EqualTo(before).Within(0.001));
        }

        [Test]
        public void Route_ReplacedGroundInvalidatesSamplesWithoutMovingControlPoints()
        {
            var ground = Child("Ground", sceneRoot.transform).AddComponent<BoxCollider>();
            ground.size = new Vector3(20, 1, 20);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            route.groundSurfaces = new Collider[] { ground };
            Physics.SyncTransforms();
            route.Sample(.5, out var before, out _);
            Assert.That(before.y, Is.EqualTo(0).Within(.001));
            UnityEngine.Object.DestroyImmediate(ground.gameObject);
            Assert.Throws<InvalidOperationException>(() => route.Sample(.5, out _, out _));
            var replacement = Child("Replacement", sceneRoot.transform).AddComponent<BoxCollider>();
            replacement.size = new Vector3(20, 1, 20);
            replacement.transform.position = new Vector3(0, 1.5f, 0);
            route.groundSurfaces = new Collider[] { replacement };
            Physics.SyncTransforms();
            route.Sample(.5, out var after, out _);
            Assert.That(after.y, Is.EqualTo(2).Within(.001));
            Assert.That(route.points[0], Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void RouteDisplay_TracksDeletionUndoAndSharedReferences()
        {
            var leading = Add(0, 1, StagePerformanceMode.Hold);
            ((StagePerformanceClip)leading.asset).station.Resolve(director).position = Vector3.zero;
            var moving = Add(1, 2, StagePerformanceMode.Move);
            var display = route.gameObject.AddComponent<StageRouteLine>();
            display.RefreshDisplay();
            var renderer = route.GetComponent<LineRenderer>();
            Assert.That(renderer.positionCount, Is.GreaterThan(0));
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(track, "删除移动片段");
            track.DeleteClip(moving);
            Undo.FlushUndoRecordObjects();
            display.RefreshDisplay();
            Assert.That(route.IsUsedByStory(), Is.False);
            Assert.That(renderer.positionCount, Is.Zero);
            Assert.That(route.points.Length, Is.EqualTo(2));
            Undo.PerformUndo();
            display.RefreshDisplay();
            Assert.That(route.IsUsedByStory(), Is.True);
            Assert.That(renderer.positionCount, Is.GreaterThan(0));
            moving = track.GetClips().First(c => ((StagePerformanceClip)c.asset).mode == StagePerformanceMode.Move);
            var shared = Add(3, 2, StagePerformanceMode.Move);
            track.DeleteClip(moving);
            Assert.That(route.IsUsedByStory(), Is.True);
            track.DeleteClip(shared);
            Assert.That(route.IsUsedByStory(), Is.False);



        }

        [Test]
        public void Curves_AdjacentMovesShareTangentAndReactToNodeChanges()
        {
            route.curved = true;
            Add(0, 4, StagePerformanceMode.Move);
            var clip = Add(4, 4, StagePerformanceMode.Move);
            var next = Child("NextRoute", sceneRoot.transform).AddComponent<StageRoute>();
            next.points = new[] { new Vector3(0, 0, 4), new Vector3(4, 0, 4) };
            director.SetReferenceValue(((StagePerformanceClip)clip.asset).route.exposedName, next);
            route.Sample(1, out var end, out var incoming);
            next.Sample(0, out var start, out var outgoing);
            Assert.That(Vector3.Distance(end, start), Is.LessThan(.0001));
            Assert.That(Vector3.Angle(incoming, outgoing), Is.LessThan(.1));
            Assert.That(Vector3.Angle(incoming, Vector3.forward), Is.EqualTo(45).Within(.1));
            route.Sample(.999, out _, out var before);
            next.Sample(.001, out _, out var after);
            Assert.That(Vector3.Angle(before, after), Is.LessThan(2));
            next.points[1] = new Vector3(-4, 0, 4);
            route.Sample(1, out _, out var changed);
            Assert.That(changed.x, Is.LessThan(0));
            next.smoothConnections = false;
            route.Sample(1, out _, out var unjoined);
            Assert.That(Vector3.Angle(unjoined, Vector3.forward), Is.LessThan(.1));
        }

        [TestCase(1)]
        [TestCase(3)]
        public void Curves_AutoConnectedMovesStaySmoothAcrossHoldsAndReordering(int holdCount)
        {
            route.curved = true;
            var first = Add(0, 2, StagePerformanceMode.Move);
            var holds = Enumerable.Range(0, holdCount).Select(i => Add(2 + i, 1, StagePerformanceMode.Hold)).ToArray();
            var last = Add(2 + holdCount, 2, StagePerformanceMode.Move);
            var next = Child("NextRoute", sceneRoot.transform).AddComponent<StageRoute>();
            next.points = new[] { new Vector3(30, 0, 30), new Vector3(4, 0, 4) };
            director.SetReferenceValue(((StagePerformanceClip)last.asset).route.exposedName, next);
            route.Sample(1, out var end, out var incoming);
            next.Sample(0, out var start, out var outgoing);
            Assert.That(Vector3.Distance(end, start), Is.LessThan(.001));
            Assert.That(Vector3.Angle(incoming, outgoing), Is.LessThan(.1));
            route.Sample(.5, out var curvedPoint, out _);
            Assert.That(Mathf.Abs(curvedPoint.x), Is.GreaterThan(.01));
            Seek(2.5);
            Assert.That(actor.transform.position, Is.EqualTo(end));
            next.points[1] = new Vector3(-4, 0, 4);
            route.Sample(1, out _, out incoming);
            next.Sample(0, out _, out outgoing);
            Assert.That(incoming.x, Is.LessThan(0));
            Assert.That(Vector3.Angle(incoming, outgoing), Is.LessThan(.1));
            // 反向重排后，终点、中间停留、自动起点与圆滑切线必须一起更新。
            last.start = 0; first.start = 2 + holdCount;
            next.Sample(1, out end, out incoming);
            route.Sample(0, out start, out outgoing);
            Assert.That(Vector3.Distance(end, start), Is.LessThan(.001));
            Assert.That(Vector3.Angle(incoming, outgoing), Is.LessThan(.1));
            director.RebuildGraph(); Seek(2.5);
            Assert.That(actor.transform.position, Is.EqualTo(end));
            ((StagePerformanceClip)holds[0].asset).followPrevious = false;
            next.Sample(1, out _, out var manualBoundary);
            var raw = (next.WorldPoint(1) - next.WorldPoint(0)).normalized;
            Assert.That(Vector3.Angle(manualBoundary, raw), Is.LessThan(.1));
        }

        [Test]
        public void Curves_SmoothnessChangesShapeButPreservesNodesAndSeek()
        {
            route.curved = true;
            route.points = new[] { Vector3.zero, new Vector3(0, 0, 2), new Vector3(2, 0, 2) };
            route.smoothness = .5f;
            route.Sample(.3, out var rounded, out var forward);
            route.Sample(.9, out _, out _); route.Sample(.3, out var repeated, out var repeatedForward);
            Assert.That(repeated, Is.EqualTo(rounded)); Assert.That(repeatedForward, Is.EqualTo(forward));
            route.smoothness = 0;
            route.Sample(.3, out var straight, out _);
            Assert.That(Vector3.Distance(rounded, straight), Is.GreaterThan(.01));
            route.Sample(0, out var start, out _); route.Sample(1, out var end, out _);
            Assert.That(start, Is.EqualTo(Vector3.zero));
            Assert.That(end, Is.EqualTo(new Vector3(2, 0, 2)));
        }

        private static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }
    }
}
