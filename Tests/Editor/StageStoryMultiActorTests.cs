using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AIBuilder.CreatureStage;
using AIBuilder.EditorTools.CreatureStage;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace AIBuilder.EditorTools.Tests
{
    public sealed class StageStoryMultiActorTests
    {
        private GameObject root;
        private PlayableDirector target;
        private StageStoryWindow window;
        private readonly List<TimelineAsset> timelines = new List<TimelineAsset>();
        private readonly List<AnimationClip> animations = new List<AnimationClip>();

        [SetUp] public void Setup()
        {
            root = new GameObject("MultiActorTest"); target = Director("Master");
            window = ScriptableObject.CreateInstance<StageStoryWindow>();
            typeof(StageStoryWindow).GetField("director", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, target);
        }

        [TearDown] public void Cleanup()
        {
            UnityEngine.Object.DestroyImmediate(window);
            foreach (var director in root.GetComponentsInChildren<PlayableDirector>()) director.Stop();
            UnityEngine.Object.DestroyImmediate(root);
            foreach (var timeline in timelines)
            {
                foreach (var track in timeline.GetOutputTracks().ToArray())
                {
                    foreach (var clip in track.GetClips()) if (clip.asset != null) UnityEngine.Object.DestroyImmediate(clip.asset);
                    UnityEngine.Object.DestroyImmediate(track);
                }
                UnityEngine.Object.DestroyImmediate(timeline);
            }
            timelines.Clear(); Selection.activeObject = null; Undo.ClearAll();
            foreach (var animation in animations) UnityEngine.Object.DestroyImmediate(animation);
            animations.Clear();
        }

        private GameObject Child(string name, Transform parent)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
        private StageActorDriver Actor(string name)
        {
            var actor = Child(name, root.transform).AddComponent<StageActorDriver>();
            actor.animator = Child(name + " Rig", actor.transform).AddComponent<Animator>(); return actor;
        }
        private PlayableDirector Director(string name)
        {
            var director = Child(name, root.transform).AddComponent<PlayableDirector>();
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>(); timelines.Add(timeline);
            director.playableAsset = timeline; director.playOnAwake = false; return director;
        }
        private TimelineClip Hold(PlayableDirector director, StagePerformanceTrack track, double start, double duration)
        {
            var clip = track.CreateClip<StagePerformanceClip>(); clip.start = start; clip.duration = duration;
            var data = (StagePerformanceClip)clip.asset; data.mode = StagePerformanceMode.Hold;
            data.station.exposedName = new PropertyName("shared-name");
            director.SetReferenceValue(data.station.exposedName, Child("Station", director.transform).transform); return clip;
        }
        [Test] public void ExistingTimelineCanBeResolvedWithoutPerformanceTracks()
        {
            Assert.That(window.ActiveTrack, Is.Null);
            Assert.That(StageStoryWindow.ResolveStoryObject(target), Is.SameAs(target));
            Assert.That(StageStoryWindow.ResolveStoryObject(target.gameObject), Is.SameAs(target));
            Assert.That(StageStoryWindow.ResolveStoryObject(target.playableAsset), Is.SameAs(target));
            Assert.That(StageStoryWindow.ResolveStoryObject(null), Is.Null);
            Assert.Throws<InvalidOperationException>(() => StageStoryWindow.ResolveStoryObject(root));
            var other = Director("Shared"); other.playableAsset = target.playableAsset;
            Assert.Throws<InvalidOperationException>(() => StageStoryWindow.ResolveStoryObject(target.playableAsset));
        }

        [Test] public void AddFirstActorToExistingTimelinePreservesOriginalContent()
        {
            var timeline = (TimelineAsset)target.playableAsset;
            var model = Child("OriginalModel", root.transform);
            var animator = model.AddComponent<Animator>();
            var animationTrack = timeline.CreateTrack<AnimationTrack>(null, "Original animation");
            var clip = animationTrack.CreateClip<AnimationPlayableAsset>(); clip.start = 3; clip.duration = 5;
            target.SetGenericBinding(animationTrack, animator);
            var audio = timeline.CreateTrack<AudioTrack>(null, "Original audio");
            var audioSource = model.AddComponent<AudioSource>(); target.SetGenericBinding(audio, audioSource);
            target.extrapolationMode = DirectorWrapMode.Loop;
            var track = StageStoryMultiActor.AddSceneObject(target, model);
            Assert.That(target.playableAsset, Is.SameAs(timeline));
            Assert.That(track.animationTrack, Is.SameAs(animationTrack));
            Assert.That(animationTrack.GetClips().Single(), Is.SameAs(clip));
            Assert.That(clip.start, Is.EqualTo(3)); Assert.That(clip.duration, Is.EqualTo(5));
            Assert.That(target.GetGenericBinding(animationTrack), Is.SameAs(animator));
            Assert.That(target.GetGenericBinding(audio), Is.SameAs(audioSource));
            Assert.That(target.extrapolationMode, Is.EqualTo(DirectorWrapMode.Loop));
            Assert.That(timeline.GetOutputTracks().Count(), Is.EqualTo(3));
        }

        [Test] public void GroupPointsSeparatesActorsPreservesWorldPositionsAndUndo()
        {
            AnimationMode.StopAnimationMode();
            var a = StageStoryMultiActor.AddActor(target, Actor("A"));
            var b = StageStoryMultiActor.AddActor(target, Actor("B"));
            var ca = Hold(target, a, 0, 2);
            var pa = ((StagePerformanceClip)ca.asset).station.Resolve(target);
            ((StagePerformanceClip)ca.asset).station.exposedName = new PropertyName("actor-a-station");
            target.SetReferenceValue(((StagePerformanceClip)ca.asset).station.exposedName, pa);
            var cb = Hold(target, b, 0, 2);
            var pb = ((StagePerformanceClip)cb.asset).station.Resolve(target);
            Assert.That(pa == pb, Is.False, "两个角色必须有独立站点");
            pa.SetParent(target.transform, true); pb.SetParent(target.transform, true);
            pa.position = new Vector3(2, 3, 4); pb.position = new Vector3(6, 7, 8);
            StageStoryHierarchy.GroupDirector(target);
            Assert.That(pa.parent == pb.parent, Is.False, $"分组结果：{pa.parent.name}/{pb.parent.name}，分组数 {target.GetComponentsInChildren<StageStoryPointGroup>().Length}");
            Assert.That(pa.parent.GetComponent<StageStoryPointGroup>().track, Is.SameAs(a));
            Assert.That(pb.parent.GetComponent<StageStoryPointGroup>().track, Is.SameAs(b));
            Assert.That(pa.position, Is.EqualTo(new Vector3(2, 3, 4)));
            Assert.That(pb.position, Is.EqualTo(new Vector3(6, 7, 8)));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(pa.parent, Is.SameAs(target.transform));
            Assert.That(pb.parent, Is.SameAs(target.transform));
        }

        [Test] public void GroundChangesMoveAnchorsBothWaysAndLeaveFlightPointsAlone()
        {
            var floor = Child("Ground", root.transform).AddComponent<BoxCollider>();
            floor.size = new Vector3(20, 1, 20); floor.transform.position = new Vector3(0, -.5f, 0);
            var route = Child("Ground route", root.transform).AddComponent<StageRoute>();
            route.autoConnectPrevious = false; route.groundSurfaces = new Collider[] { floor };
            route.startAnchor = Child("Start", root.transform).transform;
            route.endAnchor = Child("End", root.transform).transform; route.endAnchor.position = Vector3.forward;
            var flight = Child("Flight", root.transform).AddComponent<StageRoute>();
            flight.snapToGround = false; flight.groundSurfaces = new Collider[] { floor };
            flight.startAnchor = Child("Air", root.transform).transform; flight.startAnchor.position = Vector3.up * 5;
            StageGroundPointSync.SynchronizeChangedGround();
            floor.transform.position += Vector3.up * 2;
            Assert.That(StageGroundPointSync.SynchronizeChangedGround(), Is.GreaterThanOrEqualTo(2));
            Assert.That(route.startAnchor.position.y, Is.EqualTo(2).Within(.001));
            Assert.That(route.endAnchor.position.y, Is.EqualTo(2).Within(.001));
            floor.transform.position -= Vector3.up * 3;
            Physics.SyncTransforms();
            Assert.That(route.ProjectToGround(route.startAnchor.position, true).y, Is.EqualTo(-1).Within(.001), "地面投射高度");
            StageGroundPointSync.SynchronizeChangedGround();
            Assert.That(route.startAnchor.position.y, Is.EqualTo(-1).Within(.001));
            Assert.That(route.endAnchor.position.y, Is.EqualTo(-1).Within(.001));
            Assert.That(flight.startAnchor.position.y, Is.EqualTo(5));
            Assert.That(StageGroundPointSync.SynchronizeChangedGround(), Is.Zero);
        }

        [Test] public void TerrainSculptUpdatesAnchorsWithoutMovingTerrainTransform()
        {
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(10, 10, 10) };
            var terrain = Terrain.CreateTerrainGameObject(data); terrain.transform.SetParent(root.transform, false);
            try
            {
                var route = Child("Terrain route", root.transform).AddComponent<StageRoute>();
                route.autoConnectPrevious = false;
                route.groundSurfaces = new Collider[] { terrain.GetComponent<TerrainCollider>() };
                route.startAnchor = Child("Start", root.transform).transform; route.startAnchor.position = new Vector3(2, 0, 2);
                route.endAnchor = Child("End", root.transform).transform; route.endAnchor.position = new Vector3(3, 0, 3);
                StageGroundPointSync.SynchronizeChangedGround();
                var heights = new float[33, 33];
                for (var y = 0; y < 33; y++) for (var x = 0; x < 33; x++) heights[y, x] = .3f;
                data.SetHeights(0, 0, heights);
                StageGroundPointSync.SynchronizeChangedGround();
                Assert.That(route.startAnchor.position.y, Is.EqualTo(3).Within(.01));
                data.SetHeights(0, 0, new float[33, 33]);
                StageGroundPointSync.SynchronizeChangedGround();
                Assert.That(route.startAnchor.position.y, Is.EqualTo(0).Within(.01));
            }
            finally { UnityEngine.Object.DestroyImmediate(terrain); UnityEngine.Object.DestroyImmediate(data); }
        }

        private void AddFromWindow()
        { typeof(StageStoryWindow).GetMethod("AddClip", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { false }); }

        [UnityTest] public IEnumerator ScenePointSelectionSwitchesRoleAndClipWithoutStealingSceneSelection()
        {
            var a = StageStoryMultiActor.AddActor(target, Actor("A"));
            var b = StageStoryMultiActor.AddActor(target, Actor("B"));
            var first = Hold(target, b, 0, 2);
            var move = b.CreateClip<StagePerformanceClip>(); move.start = 2; move.duration = 2;
            var data = (StagePerformanceClip)move.asset; data.mode = StagePerformanceMode.Move;
            var route = Child("Route", root.transform).AddComponent<StageRoute>();
            route.startAnchor = Child("Start", root.transform).transform;
            route.endAnchor = Child("End", root.transform).transform;
            data.route.exposedName = new PropertyName("route-b"); target.SetReferenceValue(data.route.exposedName, route);
            var selectedField = typeof(StageStoryWindow).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var point in new[] { route.gameObject, route.startAnchor.gameObject, route.endAnchor.gameObject })
            {
                window.SelectTrack(a);
                Selection.activeGameObject = point;
                yield return null;
                Assert.That(window.ActiveTrack, Is.EqualTo(b));
                Assert.That((int)selectedField.GetValue(window), Is.EqualTo(1));
                Assert.That(Selection.activeGameObject, Is.EqualTo(point));
            }
            var station = ((StagePerformanceClip)first.asset).station.Resolve(target);
            Selection.activeGameObject = station.gameObject;
            yield return null;
            Assert.That((int)selectedField.GetValue(window), Is.EqualTo(0));
            var unrelated = Child("Unrelated", root.transform);
            Selection.activeGameObject = unrelated;
            yield return null;
            Assert.That(window.ActiveTrack, Is.EqualTo(b));
            Assert.That(window.SelectScenePoint(unrelated), Is.False);
            window.SelectTrack(a);
            StageRouteEditor.SelectPoint(route, 1);
            Assert.That(window.ActiveTrack, Is.EqualTo(b));
            Assert.That(Selection.activeGameObject, Is.EqualTo(route.gameObject));
        }

        [Test] public void SceneSelectionIgnoresDestroyedRouteAnchors()
        {
            var track = StageStoryMultiActor.AddActor(target, Actor("Actor"));
            var route = Child("Route", root.transform).AddComponent<StageRoute>();
            route.startAnchor = Child("Removed start", root.transform).transform;
            route.endAnchor = Child("Removed end", root.transform).transform;
            UnityEngine.Object.DestroyImmediate(route.startAnchor.gameObject);
            UnityEngine.Object.DestroyImmediate(route.endAnchor.gameObject);
            var clip = track.CreateClip<StagePerformanceClip>();
            var data = (StagePerformanceClip)clip.asset;
            data.mode = StagePerformanceMode.Move;
            data.route.exposedName = new PropertyName(Guid.NewGuid().ToString());
            target.SetReferenceValue(data.route.exposedName, route);
            Assert.That(window.SelectScenePoint(target.gameObject), Is.False);
            Assert.That(window.SelectScenePoint(route.gameObject), Is.True);
        }

        [Test] public void SceneObjectJoinPreservesPoseAndUndoRestoresHierarchy()
        {
            var model = Child("Prop", root.transform);
            model.transform.localPosition = new Vector3(2, 3, 4);
            model.transform.localRotation = Quaternion.Euler(15, 25, 35);
            model.transform.localScale = new Vector3(2, 3, 4);
            var matrix = model.transform.localToWorldMatrix;
            var track = StageStoryMultiActor.AddSceneObject(target, model);
            var actor = target.GetGenericBinding(track) as StageActorDriver;
            Assert.That(actor, Is.Not.Null);
            Assert.That(actor.transform.parent, Is.EqualTo(root.transform));
            Assert.That(actor.animator, Is.EqualTo(model.GetComponent<Animator>()));
            Assert.That(actor.transform.lossyScale, Is.EqualTo(Vector3.one));
            for (var i = 0; i < 16; i++) Assert.That(model.transform.localToWorldMatrix[i], Is.EqualTo(matrix[i]).Within(.00001f));
            var token = new object(); Assert.DoesNotThrow(() => actor.Acquire(token)); actor.Release(token);
            Undo.PerformUndo();
            Assert.That(model.transform.parent, Is.EqualTo(root.transform));
            Assert.That(model.GetComponent<Animator>(), Is.Null);
            Assert.That(((TimelineAsset)target.playableAsset).GetOutputTracks().Count(), Is.Zero);
        }

        [Test] public void SceneObjectJoinReusesAnimatorAndDoesNotDuplicateRole()
        {
            var model = Child("Model", root.transform);
            var animator = Child("Rig", model.transform).AddComponent<Animator>();
            var track = StageStoryMultiActor.AddSceneObject(target, model);
            Assert.That(StageStoryMultiActor.AddSceneObject(target, animator.gameObject), Is.SameAs(track));
            Assert.That(((StageActorDriver)target.GetGenericBinding(track)).animator, Is.SameAs(animator));
            Assert.That(((TimelineAsset)target.playableAsset).GetOutputTracks().OfType<StagePerformanceTrack>().Count(), Is.EqualTo(1));
        }

        [Test] public void SceneObjectJoinRejectsAmbiguousRigWithoutMutations()
        {
            var model = Child("Group", root.transform);
            Child("RigA", model.transform).AddComponent<Animator>();
            Child("RigB", model.transform).AddComponent<Animator>();
            Assert.Throws<InvalidOperationException>(() => StageStoryMultiActor.AddSceneObject(target, model));
            Assert.That(model.transform.parent, Is.EqualTo(root.transform));
            Assert.That(root.GetComponentsInChildren<StageActorDriver>().Length, Is.Zero);
            Assert.That(((TimelineAsset)target.playableAsset).GetOutputTracks().Count(), Is.Zero);
        }

        [Test] public void AddingToSecondAndThirdActorDoesNotTouchFirstActor()
        {
            var tracks = new[] { Actor("A"), Actor("B"), Actor("C") }.Select(a => StageStoryMultiActor.AddActor(target, a)).ToArray();
            Hold(target, tracks[0], 0, 2);
            window.SelectTrack(tracks[1]); AddFromWindow(); AddFromWindow();
            window.SelectTrack(tracks[2]); AddFromWindow();
            CollectionAssert.AreEqual(new[] { 1, 2, 1 }, tracks.Select(t => t.GetClips().Count()).ToArray());
            Assert.AreEqual(0, tracks[1].GetClips().First().start);
            Assert.AreEqual(0, tracks[2].GetClips().First().start);
            Assert.AreSame(tracks[2], TimelineEditor.selectedClip.GetParentTrack());
            foreach (var track in tracks)
                foreach (var clip in track.GetClips()) Assert.IsNotNull(((StagePerformanceClip)clip.asset).station.Resolve(target));
        }

        [UnityTest] public IEnumerator NativeSelectionFollowsEmptyActorAndAnimationTracks()
        {
            var first = StageStoryMultiActor.AddActor(target, Actor("A"));
            var second = StageStoryMultiActor.AddActor(target, Actor("B"));
            window.SelectTrack(first); TimelineEditor.GetOrCreateWindow().SetTimeline(target);
            yield return null;
            Selection.activeObject = second; yield return null;
            Assert.AreSame(second, window.ActiveTrack);
            Selection.activeObject = first.animationTrack; yield return null;
            Assert.AreSame(first, window.ActiveTrack);
        }

        [UnityTest] public IEnumerator NativeTrackSelectionSwitchesToInspectedStory()
        {
            StageStoryMultiActor.AddActor(target, Actor("A"));
            var other = Director("Other");
            var track = StageStoryMultiActor.AddActor(other, Actor("B"));
            TimelineEditor.GetOrCreateWindow().SetTimeline(other);
            yield return null;
            Selection.activeObject = track; yield return null;
            Assert.AreSame(track, window.ActiveTrack);
            TimelineEditor.GetOrCreateWindow().Close();
            yield return null;
            AddFromWindow();
            Assert.AreEqual(1, track.GetClips().Count());
            Assert.AreEqual(0, ((TimelineAsset)target.playableAsset).GetOutputTracks().Sum(t => t.GetClips().Count()));
        }

        [Test] public void PreviewDurationIncludesOtherActorsLongerTrack()
        {
            var a = StageStoryMultiActor.AddActor(target, Actor("A"));
            var b = StageStoryMultiActor.AddActor(target, Actor("B"));
            Hold(target, a, 0, 2); Hold(target, b, 0, 40); window.SelectTrack(a);
            var duration = (double)typeof(StageStoryWindow).GetProperty("StoryDuration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
            Assert.That(duration, Is.EqualTo(40).Within(.00001));
        }

        [Test] public void RouteColorsFollowActorBindingsAndUndoWithoutChangingOtherActors()
        {
            var a = Actor("A"); var b = Actor("B");
            a.routeColor = Color.red; b.routeColor = Color.blue;
            var routes = new[] { a, b }.Select(actor =>
            {
                var track = StageStoryMultiActor.AddActor(target, actor);
                var route = Child("Route", root.transform).AddComponent<StageRoute>();
                var data = (StagePerformanceClip)track.CreateClip<StagePerformanceClip>().asset;
                data.mode = StagePerformanceMode.Move;
                data.route.exposedName = new PropertyName(Guid.NewGuid().ToString("N"));
                target.SetReferenceValue(data.route.exposedName, route);
                return route;
            }).ToArray();
            Assert.AreEqual(Color.red, routes[0].DisplayColor);
            Assert.AreEqual(Color.blue, routes[1].DisplayColor);
            var settings = new SerializedObject(a);
            settings.FindProperty("routeColor").colorValue = Color.yellow; settings.ApplyModifiedProperties();
            Undo.FlushUndoRecordObjects();
            Assert.AreEqual(Color.yellow, routes[0].DisplayColor);
            Assert.AreEqual(Color.blue, routes[1].DisplayColor);
            Undo.PerformUndo();
            Assert.AreEqual(Color.red, routes[0].DisplayColor);
        }

        [Test] public void ImportedRouteColorFollowsActorOutsideMasterHierarchy()
        {
            var source = Director("Source"); var actor = Actor("B");
            var track = StageStoryMultiActor.AddActor(source, actor);
            var route = Child("Route", source.transform).AddComponent<StageRoute>();
            var data = (StagePerformanceClip)track.CreateClip<StagePerformanceClip>().asset;
            data.mode = StagePerformanceMode.Move; data.route.exposedName = new PropertyName("route");
            source.SetReferenceValue(data.route.exposedName, route);
            StageStoryMultiActor.Import(target, source);
            actor.routeColor = Color.magenta;
            Assert.AreEqual(Color.magenta, route.DisplayColor);
            Assert.AreSame(source.transform, route.transform.parent);
        }

        [Test] public void OneDirectorEvaluatesAndRestoresThreeActorsTogether()
        {
            var actors = new[] { Actor("A"), Actor("B"), Actor("C") };
            for (var i = 0; i < actors.Length; i++)
            {
                actors[i].transform.position = new Vector3(i * 10, 0, 0);
                var track = StageStoryMultiActor.AddActor(target, actors[i]);
                var clip = Hold(target, track, 0, 3);
                var data = (StagePerformanceClip)clip.asset;
                // 同一 Director 下每个片段使用独立 ExposedReference 名称。
                data.station.exposedName = new PropertyName(Guid.NewGuid().ToString("N"));
                var point = Child("Point" + i, root.transform).transform; point.position = new Vector3(i * 10, 0, 5);
                target.SetReferenceValue(data.station.exposedName, point);
            }
            target.RebuildGraph(); target.time = 1; target.Evaluate();
            for (var i = 0; i < actors.Length; i++) Assert.That(actors[i].transform.position, Is.EqualTo(new Vector3(i * 10, 0, 5)));
            target.Stop();
            for (var i = 0; i < actors.Length; i++) Assert.That(actors[i].transform.position, Is.EqualTo(new Vector3(i * 10, 0, 0)));
        }

        [Test] public void ImportLegacyTrackKeepsEmbeddedAnimationMode()
        {
            var source = Director("Legacy"); var actor = Actor("B");
            var track = ((TimelineAsset)source.playableAsset).CreateTrack<StagePerformanceTrack>(); source.SetGenericBinding(track, actor);
            Hold(source, track, 2, 4);
            var imported = StageStoryMultiActor.Import(target, source).Single();
            Assert.IsNull(imported.animationTrack);
            Assert.AreEqual(1, ((TimelineAsset)target.playableAsset).GetOutputTracks().Count());
            Assert.AreEqual(2, imported.GetClips().Single().start);
        }

        [Test] public void ImportPreservesTimingBindingsAndUndoRestoresSourcePlayback()
        {
            var main = StageStoryMultiActor.AddActor(target, Actor("A")); var old = Hold(target, main, 0, 3);
            var source = Director("Source"); var actor = Actor("B");
            var route = StageStoryMultiActor.AddActor(source, actor); var hold = Hold(source, route, 7, 9);
            var animation = route.animationTrack.CreateClip<StageGaitAnimationAsset>();
            var action = new AnimationClip(); animations.Add(action);
            AnimationUtility.SetEditorCurve(action, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Linear(0, 0, 2, 1));
            ((StageGaitAnimationAsset)animation.asset).clip = action;
            animation.start = 6; animation.duration = 12; animation.clipIn = .75; animation.timeScale = 1.5;
            Assert.AreEqual(.75, animation.clipIn);
            ((StageGaitAnimationAsset)animation.asset).cycleDistance = 2.25f;
            var playback = source.gameObject.AddComponent<StageStoryPlayback>(); playback.director = source; source.playOnAwake = true;
            var originalPoint = ((StagePerformanceClip)hold.asset).station.Resolve(source);
            var oldPoint = ((StagePerformanceClip)old.asset).station.Resolve(target);
            var imported = StageStoryMultiActor.Import(target, source).Single();
            Assert.AreSame(actor, target.GetGenericBinding(imported));
            Assert.AreSame(actor.animator, target.GetGenericBinding(imported.animationTrack));
            var copy = imported.GetClips().Single(); Assert.AreEqual(7, copy.start); Assert.AreEqual(9, copy.duration);
            Assert.AreNotSame(hold.asset, copy.asset);
            Assert.AreSame(originalPoint, ((StagePerformanceClip)copy.asset).station.Resolve(target));
            Assert.AreSame(oldPoint, ((StagePerformanceClip)old.asset).station.Resolve(target));
            var copiedAnimation = imported.animationTrack.GetClips().Single();
            Assert.AreEqual(.75, copiedAnimation.clipIn); Assert.AreEqual(1.5, copiedAnimation.timeScale);
            Assert.AreEqual(2.25f, ((StageGaitAnimationAsset)copiedAnimation.asset).cycleDistance);
            Assert.IsFalse(source.enabled); Assert.IsFalse(source.playOnAwake); Assert.IsFalse(playback.enabled);
            Assert.AreEqual(1, route.GetClips().Count());
            Undo.PerformUndo();
            Assert.IsTrue(source.enabled); Assert.IsTrue(source.playOnAwake); Assert.IsTrue(playback.enabled);
            Assert.AreEqual(1, ((TimelineAsset)target.playableAsset).GetOutputTracks().OfType<StagePerformanceTrack>().Count());
        }

        [Test] public void ImportFillsExistingEmptyActorTracksWithoutDuplicates()
        {
            var actor = Actor("B"); var empty = StageStoryMultiActor.AddActor(target, actor);
            var source = Director("Source");
            var track = ((TimelineAsset)source.playableAsset).CreateTrack<StagePerformanceTrack>(); source.SetGenericBinding(track, actor);
            track.animationTrack = ((TimelineAsset)source.playableAsset).CreateTrack<AnimationTrack>(); source.SetGenericBinding(track.animationTrack, actor.animator);
            Hold(source, track, 3, 4);
            Assert.AreSame(empty, StageStoryMultiActor.Import(target, source).Single());
            Assert.AreEqual(2, ((TimelineAsset)target.playableAsset).GetOutputTracks().Count());
            Assert.AreEqual(3, empty.GetClips().Single().start);
        }

        [Test] public void RejectOccupiedTargetBeforeDisablingOrChangingSource()
        {
            var actor = Actor("B"); var occupied = StageStoryMultiActor.AddActor(target, actor); Hold(target, occupied, 0, 2);
            var source = Director("Source"); var track = ((TimelineAsset)source.playableAsset).CreateTrack<StagePerformanceTrack>(); source.SetGenericBinding(track, actor); Hold(source, track, 0, 2);
            Assert.Throws<InvalidOperationException>(() => StageStoryMultiActor.Import(target, source));
            Assert.IsTrue(source.enabled); Assert.AreEqual(1, occupied.GetClips().Count());
        }

        [Test] public void RejectUnboundAndLockedTrackAddsWithoutCreatingClips()
        {
            var track = ((TimelineAsset)target.playableAsset).CreateTrack<StagePerformanceTrack>(); window.SelectTrack(track);
            AddFromWindow(); Assert.IsEmpty(track.GetClips());
            target.SetGenericBinding(track, Actor("A")); track.locked = true;
            AddFromWindow(); Assert.IsEmpty(track.GetClips());
        }

        [TestCase(false)] [TestCase(true)]
        public void InsertAfterSelectedClipPreservesOtherActorAndUndo(bool moving)
        {
            var a = StageStoryMultiActor.AddActor(target, Actor("A"));
            var b = StageStoryMultiActor.AddActor(target, Actor("B"));
            Hold(target, b, 11, 7);
            var first = Hold(target, a, 0, 2); var last = Hold(target, a, 2, 4);
            ((StagePerformanceClip)first.asset).station.Resolve(target).position = new Vector3(3, 1, 2);
            window.SelectTrack(a);
            typeof(StageStoryWindow).GetField("ripple", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, false);
            typeof(StageStoryWindow).GetMethod("AddClip", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { moving });
            var ordered = a.GetClips().OrderBy(c => c.start).ToArray();
            Assert.AreSame(first, ordered[0]); Assert.AreSame(last, ordered[2]);
            Assert.AreEqual(2, ordered[1].start); Assert.AreEqual(5, last.start);
            Assert.AreEqual(11, b.GetClips().Single().start);
            Assert.AreSame(ordered[1], TimelineEditor.selectedClip);
            Assert.AreEqual(new Vector3(3, 1, 2), ((StagePerformanceClip)ordered[1].asset).station.Resolve(target).position);
            Undo.PerformUndo();
            Assert.AreEqual(2, a.GetClips().Count()); Assert.AreEqual(2, a.GetClips().OrderBy(c => c.start).Last().start);
        }

        [Test] public void ReorderedImportedRoutesFollowMasterAndPreserveAuthoredPoints()
        {
            var source = Director("Source"); var actor = Actor("B");
            var track = StageStoryMultiActor.AddActor(source, actor);
            var hold = Hold(source, track, 0, 2);
            var station = ((StagePerformanceClip)hold.asset).station.Resolve(source); station.position = new Vector3(4, 0, 0);
            var routes = new StageRoute[2];
            for (var i = 0; i < 2; i++)
            {
                var route = Child("Route" + i, source.transform).AddComponent<StageRoute>();
                route.snapToGround = false; route.autoBindGround = false;
                route.points = new[] { new Vector3(99, 0, 0), new Vector3(10 + i * 10, 0, 2), new Vector3(10 + i * 10, 0, 4) };
                routes[i] = route;
                var clip = track.CreateClip<StagePerformanceClip>(); clip.start = 2 + i * 2; clip.duration = 2;
                var data = (StagePerformanceClip)clip.asset; data.mode = StagePerformanceMode.Move;
                data.route.exposedName = new PropertyName("route" + i); source.SetReferenceValue(data.route.exposedName, route);
            }
            var imported = StageStoryMultiActor.Import(target, source).Single();
            Assert.AreEqual(station.position, routes[0].WorldPoint(0));
            Assert.AreEqual(routes[0].WorldPoint(2), routes[1].WorldPoint(0));
            var authored = routes.Select(r => r.points.ToArray()).ToArray();
            window.SelectTrack(imported);
            typeof(StageStoryWindow).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, 1);
            AddFromWindow();
            Assert.AreEqual(routes[0].WorldPoint(2), routes[1].WorldPoint(0));
            typeof(StageStoryWindow).GetMethod("AddClip", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { true });
            var inserted = ((StagePerformanceClip)TimelineEditor.selectedClip.asset).route.Resolve(target);
            Assert.AreEqual(routes[0].WorldPoint(2), inserted.WorldPoint(0));
            Assert.AreEqual(inserted.WorldPoint(inserted.PointCount - 1), routes[1].WorldPoint(0));
            Undo.PerformUndo(); Undo.PerformUndo();
            Assert.AreEqual(3, imported.GetClips().Count());
            StageStoryWindow.ReorderClip(imported, 2, 1);
            Assert.AreEqual(station.position, routes[1].WorldPoint(0));
            Assert.AreEqual(routes[1].WorldPoint(2), routes[0].WorldPoint(0));
            foreach (var route in routes) { route.Sample(0, out var start, out _); Assert.AreEqual(route.WorldPoint(0), start); }
            for (var i = 0; i < 2; i++) CollectionAssert.AreEqual(authored[i], routes[i].points);
            Undo.PerformUndo();
            Assert.AreEqual(station.position, routes[0].WorldPoint(0));
            Assert.AreEqual(routes[0].WorldPoint(2), routes[1].WorldPoint(0));
        }

        [UnityTest] public IEnumerator NativePreviewAllowsRoutePointEditingAndRefreshesHandleCache()
        {
            var track = StageStoryMultiActor.AddActor(target, Actor("EditablePreview"));
            var clip = track.CreateClip<StagePerformanceClip>(); clip.duration = 10;
            var data = (StagePerformanceClip)clip.asset; data.mode = StagePerformanceMode.Move;
            var route = Child("PreviewRoute", root.transform).AddComponent<StageRoute>();
            route.snapToGround = false;
            data.route.exposedName = new PropertyName("preview-edit-route");
            target.SetReferenceValue(data.route.exposedName, route);
            var native = TimelineEditor.GetOrCreateWindow(); native.SetTimeline(target);
            yield return null;
            var state = native.GetType().GetProperty("state").GetValue(native);
            var preview = state.GetType().GetProperty("previewMode");
            try
            {
                StageRouteOverlay.RefreshEditedRoute(route);
                preview.SetValue(state, true);
                target.time = 2;
                var before = StageRouteOverlay.DisplayControlPosition(route, 1);
                var moved = before + Vector3.right * 3;
                Undo.RecordObject(route, "Move preview point");
                route.points[1] = route.transform.InverseTransformPoint(moved);
                StageRouteOverlay.RefreshEditedRoute(route);
                Assert.That(StageRouteOverlay.DisplayControlPosition(route, 1), Is.EqualTo(moved));
                Assert.That(AnimationMode.InAnimationMode(), Is.True);
                Assert.That(target.time, Is.EqualTo(2));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                StageRouteOverlay.RefreshEditedRoute(route);
                Assert.That(StageRouteOverlay.DisplayControlPosition(route, 1), Is.EqualTo(before));
            }
            finally { preview.SetValue(state, false); native.Close(); }
            // 原生 Timeline 的延迟求值和关闭事件必须在下一个场景测试前排空。
            for (var i = 0; i < 3; i++) yield return null;
        }

        [UnityTest] public IEnumerator NativePreviewTakesOverToolClockAndDefersEditorMaintenance()
        {
            var track = StageStoryMultiActor.AddActor(target, Actor("PreviewActor"));
            Hold(target, track, 0, 30);
            window.SelectTrack(track);
            var native = TimelineEditor.GetOrCreateWindow(); native.SetTimeline(target);
            yield return null;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var state = native.GetType().GetProperty("state").GetValue(native);
            var previewMode = state.GetType().GetProperty("previewMode");
            try
            {
                previewMode.SetValue(state, true);
                Assert.That(AnimationMode.InAnimationMode(), Is.True);
                typeof(StageStoryWindow).GetField("playing", flags).SetValue(window, true);
                typeof(StageStoryWindow).GetField("previewing", flags).SetValue(window, true);
                typeof(StageStoryWindow).GetField("time", flags).SetValue(window, .25d);
                typeof(StageStoryWindow).GetField("lastUpdate", flags).SetValue(window, EditorApplication.timeSinceStartup - .05);
                target.time = 3;
                typeof(StageStoryWindow).GetMethod("Tick", flags).Invoke(window, null);
                Assert.That(target.time, Is.EqualTo(3));
                Assert.That((bool)typeof(StageStoryWindow).GetField("playing", flags).GetValue(window), Is.False);
                Assert.That((bool)typeof(StageStoryWindow).GetField("previewing", flags).GetValue(window), Is.False);
                var stops = 0;
                System.Action<PlayableDirector> stopped = _ => stops++;
                target.stopped += stopped;
                typeof(StageStoryWindow).GetField("previewing", flags).SetValue(window, true);
                window.StopPreview();
                target.stopped -= stopped;
                Assert.That(stops, Is.Zero);
                var staticFlags = BindingFlags.Static | BindingFlags.NonPublic;
                var overlay = typeof(StageRouteOverlay);
                overlay.GetField("dirty", staticFlags).SetValue(null, true);
                overlay.GetField("nextRefresh", staticFlags).SetValue(null, 0d);
                var sampled = StageRouteOverlay.SampledRouteCount;
                overlay.GetMethod("RefreshLines", staticFlags).Invoke(null, null);
                Assert.That(StageRouteOverlay.SampledRouteCount, Is.EqualTo(sampled));
                Assert.That((bool)typeof(StageGroundRepair).GetProperty("CanEdit", staticFlags).GetValue(null), Is.False);
                state.GetType().GetMethod("SetPlaying").Invoke(state, new object[] { true });
                target.stopped += stoppedDirector => Debug.Log("PREVIEW_STOP_TRACE " + System.Environment.StackTrace);
                var previewTrace = "start=" + target.state + ", time=" + target.time + ", duration=" + target.duration;
                for (var i = 0; i < 20; i++)
                {
                    yield return null;
                    previewTrace += $"; {i}:state={target.state},time={target.time},native={TimelineEditor.inspectedDirector == target},preview={AnimationMode.InAnimationMode()}";
                }
                Assert.That((bool)state.GetType().GetProperty("playing").GetValue(state), Is.True, previewTrace);
            }
            finally
            {
                state.GetType().GetMethod("SetPlaying").Invoke(state, new object[] { false });
                previewMode.SetValue(state, false);
                native.Close();
            }
        }

        [UnityTest] public IEnumerator AddAfterDeletingLastClipExitsNativePreviewAndKeepsOtherActor()
        {
            var first = StageStoryMultiActor.AddActor(target, Actor("A")); Hold(target, first, 0, 3);
            var actor = Actor("B"); actor.transform.position = new Vector3(8, 0, 2);
            var track = StageStoryMultiActor.AddActor(target, actor); Hold(target, track, 0, 3);
            window.SelectTrack(track);
            var native = TimelineEditor.GetOrCreateWindow(); native.SetTimeline(target);
            yield return null;
            foreach (var moving in new[] { false, true })
            {
                var state = native.GetType().GetProperty("state").GetValue(native);
                state.GetType().GetProperty("previewMode").SetValue(state, true);
                Assert.IsTrue(AnimationMode.InAnimationMode(), "测试必须在原生 Preview 中开始");
                StageStoryWindow.DeleteClip(track, 0);
                Assert.IsEmpty(track.GetClips());
                typeof(StageStoryWindow).GetMethod("AddClip", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { moving });
                Assert.AreEqual(1, track.GetClips().Count());
                Assert.AreEqual(1, first.GetClips().Count());
                var clip = track.GetClips().Single();
                Assert.AreEqual(0, clip.start);
                var data = (StagePerformanceClip)clip.asset;
                Assert.AreEqual(moving ? StagePerformanceMode.Move : StagePerformanceMode.Hold, data.mode);
                Assert.AreEqual(new Vector3(8, 0, 2), data.station.Resolve(target).position);
                if (moving) Assert.IsNotNull(data.route.Resolve(target));
                yield return null;
            }
            native.Close();
        }
    }
}
