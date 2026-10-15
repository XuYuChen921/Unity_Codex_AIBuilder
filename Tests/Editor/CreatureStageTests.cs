using System;
using System.Linq;
using AIBuilder.CreatureStage;
using AIBuilder.EditorTools.CreatureStage;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AIBuilder.EditorTools.Tests
{
    public sealed class CreatureStageTests
    {
        private static StageMotionPlan CreatePlan()
        {
            Assert.IsTrue(StageMotionPlan.TryCreate(new[] { Vector3.zero, new Vector3(0, 0, 1), new Vector3(3, 0, 1) },
                10, 4, 1, 1, 0.8, 1.2, 0.25, 0.5, out var plan, out var error), error);
            return plan;
        }

        [Test]
        public void Motion_UsesArcLengthInsteadOfPointIndex()
        {
            var frame = CreatePlan().Evaluate(12);
            Assert.That(frame.Position, Is.EqualTo(new Vector3(1, 0, 1)));
            Assert.That(frame.Distance, Is.EqualTo(2));
            Assert.That(frame.UnwrappedPhase, Is.EqualTo(2.25));
            Assert.That(frame.ClipTime, Is.EqualTo(0.75));
        }

        [Test]
        public void Motion_ClampsBeforeStartAndAfterEnd()
        {
            var plan = CreatePlan();
            Assert.That(plan.Evaluate(0).Position, Is.EqualTo(Vector3.zero));
            Assert.That(plan.Evaluate(100).Position, Is.EqualTo(new Vector3(3, 0, 1)));
            Assert.That(plan.Evaluate(100).ClipTime, Is.EqualTo(plan.Evaluate(14).ClipTime));
        }

        [Test]
        public void Motion_RandomSeekMatchesSequentialEvaluation()
        {
            var plan = CreatePlan();
            var expected = plan.Evaluate(12.75);
            for (var t = 10d; t < 14; t += 0.017) plan.Evaluate(t);
            plan.Evaluate(11); plan.Evaluate(14); plan.Evaluate(10.3);
            var actual = plan.Evaluate(12.75);
            Assert.That(actual.Position, Is.EqualTo(expected.Position));
            Assert.That(actual.ClipTime, Is.EqualTo(expected.ClipTime));
            Assert.That(actual.UnwrappedPhase, Is.EqualTo(expected.UnwrappedPhase));
        }

        [Test]
        public void Motion_SnapshotDoesNotShareMutableSourcePath()
        {
            var points = new[] { Vector3.zero, Vector3.forward };
            Assert.IsTrue(StageMotionPlan.TryCreate(points, 0, 1, 1, 1, 1, 1, 0, 0, out var plan, out _));
            points[1] = Vector3.right * 100;
            Assert.That(plan.Evaluate(1).Position, Is.EqualTo(Vector3.forward));
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void Motion_RejectsInvalidDuration(double duration)
        {
            Assert.IsFalse(StageMotionPlan.TryCreate(new[] { Vector3.zero, Vector3.forward }, 0, duration,
                1, 1, 0.8, 1.2, 0, 0, out var plan, out var error));
            Assert.IsNull(plan);
            StringAssert.StartsWith("TIME_INVALID", error);
        }

        [Test]
        public void Motion_RejectsImpossibleRateWithoutChangingDuration()
        {
            Assert.IsFalse(StageMotionPlan.TryCreate(new[] { Vector3.zero, Vector3.forward * 100 }, 0, 1,
                1, 1, 0.8, 1.2, 0, 0, out var plan, out var error));
            Assert.IsNull(plan);
            StringAssert.StartsWith("RATE_OUT_OF_RANGE", error);
        }

        [Test]
        public void Motion_RejectsStationaryAndDuplicatePoints()
        {
            Assert.IsFalse(StageMotionPlan.TryCreate(new[] { Vector3.zero, Vector3.zero }, 0, 1,
                1, 1, 0.8, 1.2, 0, 0, out _, out var error));
            StringAssert.StartsWith("PATH_SEGMENT", error);
        }

        [Test]
        public void Motion_RejectsNonfiniteCoordinatesAndSeek()
        {
            Assert.IsFalse(StageMotionPlan.TryCreate(new[] { Vector3.zero, new Vector3(float.NaN, 0, 1) }, 0, 1,
                1, 1, 0.8, 1.2, 0, 0, out _, out var error));
            StringAssert.StartsWith("PATH_INVALID", error);
            Assert.Throws<ArgumentOutOfRangeException>(() => CreatePlan().Evaluate(double.NaN));
        }

        [Test]
        public void Motion_CycleBoundaryKeepsUnwrappedPhase()
        {
            var plan = CreatePlan();
            Assert.That(plan.Evaluate(10.75).UnwrappedPhase, Is.EqualTo(1));
            Assert.That(plan.Evaluate(10.75).ClipTime, Is.EqualTo(0.5));
            Assert.That(plan.Evaluate(10.751).UnwrappedPhase, Is.GreaterThan(1));
        }

        [TestCase(0, 1, 0.8, 1.2, 0, "GAIT_INVALID")]
        [TestCase(1, 0, 0.8, 1.2, 0, "GAIT_INVALID")]
        [TestCase(1, 1, 2, 1, 0, "RATE_INVALID")]
        [TestCase(1, 1, 0.8, 1.2, 1, "PHASE_INVALID")]
        public void Motion_RejectsInvalidCalibration(double distance, double period, double min, double max, double phase, string code)
        {
            Assert.IsFalse(StageMotionPlan.TryCreate(new[] { Vector3.zero, Vector3.forward }, 0, 1,
                distance, period, min, max, phase, 0, out _, out var error));
            StringAssert.StartsWith(code, error);
        }

        [Test]
        public void Motion_RejectsUnrepresentableTimeRange()
        {
            Assert.IsFalse(StageMotionPlan.TryCreate(new[] { Vector3.zero, Vector3.forward }, double.MaxValue, 1,
                1, 1, 0.8, 1.2, 0, 0, out _, out var error));
            StringAssert.StartsWith("TIME_INVALID", error);
        }

        [Test]
        public void Action_RequiresCalibrationAndValidSamplingRange()
        {
            var action = ScriptableObject.CreateInstance<StageActionDefinition>();
            var clip = CreateClip();
            try
            {
                action.actionId = "walk"; action.clip = clip;
                Assert.That(action.Validate(), Has.Some.StartsWith("ACTION_UNCALIBRATED"));
                action.calibrated = true;
                Assert.IsEmpty(action.Validate());
                action.clipOut = 5;
                Assert.That(action.Validate(), Has.Some.StartsWith("ACTION_RANGE"));
            }
            finally { UnityEngine.Object.DestroyImmediate(action); UnityEngine.Object.DestroyImmediate(clip); }
        }

        [Test]
        public void Audit_MissingInputsReportExplicitly()
        {
            var report = StageAssetAudit.Inspect(null, null);
            StringAssert.Contains("MODEL_MISSING", report);
            StringAssert.Contains("CLIP_MISSING", report);
            Assert.That(StageAssetAudit.ValidateProfile(null), Has.Some.StartsWith("PROFILE_MISSING"));
        }

        [Test]
        public void Pose_AbsoluteSamplingRestoresTransformsAndAnimatorSettings()
        {
            var root = new GameObject("PoseTest");
            var bone = new GameObject("Bone");
            bone.transform.SetParent(root.transform, false);
            bone.transform.localPosition = Vector3.one * 7;
            var animator = root.AddComponent<Animator>();
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            var clip = CreateClip();
            try
            {
                using (var session = new StagePoseSession(animator, clip))
                {
                    session.Evaluate(0.25);
                    Assert.That(bone.transform.localPosition.x, Is.EqualTo(0.5f).Within(0.001f));
                    session.Evaluate(0.9); session.Evaluate(0); session.Evaluate(0.25);
                    Assert.That(bone.transform.localPosition.x, Is.EqualTo(0.5f).Within(0.001f));
                    Assert.IsFalse(animator.applyRootMotion);
                    Assert.IsFalse(animator.fireEvents);
                    Assert.Throws<InvalidOperationException>(() => new StagePoseSession(animator, clip));
                }
                Assert.That(bone.transform.localPosition, Is.EqualTo(Vector3.one * 7));
                Assert.IsTrue(animator.applyRootMotion);
                Assert.IsTrue(animator.fireEvents);
                Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.CullCompletely));
                using (var session = new StagePoseSession(animator, clip)) session.Evaluate(0.75);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(clip); }
        }

        [Test]
        public void Pose_RejectsCompetingControllerAndInvalidSampleTime()
        {
            var root = new GameObject("ControllerTest");
            var animator = root.AddComponent<Animator>();
            var controller = new AnimatorController();
            var clip = CreateClip();
            var independentRoot = new GameObject("IndependentPoseTest");
            try
            {
                animator.runtimeAnimatorController = controller;
                Assert.Throws<InvalidOperationException>(() => new StagePoseSession(animator, clip));
                var independentAnimator = independentRoot.AddComponent<Animator>();
                var session = new StagePoseSession(independentAnimator, clip);
                try
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => session.Evaluate(-1));
                    Assert.Throws<ArgumentOutOfRangeException>(() => session.Evaluate(double.NaN));
                    Assert.Throws<ArgumentOutOfRangeException>(() => session.Evaluate(2));
                }
                finally { session.Dispose(); }
                session.Dispose();
                Assert.Throws<ObjectDisposedException>(() => session.Evaluate(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(independentRoot);
                UnityEngine.Object.DestroyImmediate(controller);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        private static AnimationClip CreateClip()
        {
            var clip = new AnimationClip();
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Bone", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Linear(0, 0, 1, 2));
            return clip;
        }

        [Test]
        public void Intake_RejectsEscapingAndUnapprovedOutputPaths()
        {
            Assert.Throws<ArgumentException>(() => StageIntake.Run(new StageIntake.Request
            {
                modelPath = "Assets/../Source.fbx", animationFolder = "Assets/Animations",
                outputFolder = "Assets/CreatureStage/Generated/Test"
            }));
            Assert.Throws<ArgumentException>(() => StageIntake.Run(new StageIntake.Request
            {
                modelPath = "Assets/Source.fbx", animationFolder = "Assets/Animations", outputFolder = "Assets"
            }));
        }

        [Test]
        public void RealAssets_AllSuppliedClipsSeekDeterministicallyOnModel()
        {
            const string folder = "Assets/CreatureStage/Source/Sinosauropteryx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Model/skin.FBX");
            if (model == null) Assert.Ignore("当前验证工程没有中华龙鸟素材；此测试不代表通用测试通过后的素材验收。");
            var paths = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder + "/Animations" })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().ToArray();
            Assert.That(paths.Length, Is.GreaterThan(0));
            var movingClipCount = 0;
            foreach (var path in paths)
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
            {
                var clone = UnityEngine.Object.Instantiate(model);
                try
                {
                    var animator = clone.GetComponent<Animator>();
                    if (animator == null)
                    {
                        animator = clone.AddComponent<Animator>();
                        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model)).OfType<Avatar>().FirstOrDefault();
                    }
                    var bones = clone.GetComponentsInChildren<Transform>(true);
                    using (var session = new StagePoseSession(animator, clip))
                    {
                        session.Evaluate(clip.length * 0.37);
                        var expectedPositions = bones.Select(b => b.localPosition).ToArray();
                        var expectedRotations = bones.Select(b => b.localRotation).ToArray();
                        session.Evaluate(clip.length * 0.9);
                        if (bones.Where((b, i) => Vector3.Distance(expectedPositions[i], b.localPosition) > 0.00001f ||
                            Quaternion.Angle(expectedRotations[i], b.localRotation) > 0.1f).Any()) movingClipCount++;
                        session.Evaluate(0);
                        session.Evaluate(clip.length * 0.37);
                        for (var i = 0; i < bones.Length; i++)
                        {
                            Assert.That(Vector3.Distance(expectedPositions[i], bones[i].localPosition), Is.LessThan(0.00001f), path + "/" + bones[i].name);
                            Assert.That(Quaternion.Angle(expectedRotations[i], bones[i].localRotation), Is.LessThan(0.1f), path + "/" + bones[i].name);
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
            }
            Assert.That(movingClipCount, Is.GreaterThan(0), "所有素材均未产生姿态变化，需检查动画绑定。");
            TestContext.Out.WriteLine($"实际产生姿态变化的动画数：{movingClipCount}；检查素材文件数：{paths.Length}");
        }
    }
}
