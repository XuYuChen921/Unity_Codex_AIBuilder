using AIBuilder.CreatureStage;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools.CreatureStage
{
    public sealed class CreatureStageWorkbench : EditorWindow
    {
        [SerializeField] private GameObject model;
        [SerializeField] private AnimationClip clip;
        [SerializeField] private StageActorProfile profile;
        [SerializeField] private Vector3 start;
        [SerializeField] private Vector3 end = new Vector3(0, 0, 4);
        [SerializeField] private float duration = 4;
        [SerializeField] private float cycleDistance = 1;
        [SerializeField] private float cycleDuration = 1;
        [SerializeField] private float minRate = 0.8f;
        [SerializeField] private float maxRate = 1.2f;
        private double sampleTime;
        private StageMotionPlan plan;
        private string report = "选择素材后点击检查。检查不会修改源素材。";
        private Vector2 scroll;
        private StageModelPreview modelPreview;
        private float poseTime;
        private float viewYaw = 145;
        private int selectedAction;

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClosePreview;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ClosePreview;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            ClosePreview();
        }

        private void OnPlayModeChanged(PlayModeStateChange state) { ClosePreview(); }
        private void ClosePreview() { modelPreview?.Dispose(); modelPreview = null; }

        public static void Open() => GetWindow<CreatureStageWorkbench>("角色剧情开发");

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("G0/G1 开发原型：素材检查与匀速运动实验。尚未实现 Timeline 编排、起停、地形、防滑步和 HorseLink 求解。", MessageType.Info);
            EditorGUILayout.LabelField("素材与配置", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            model = (GameObject)EditorGUILayout.ObjectField("模型 / Prefab", model, typeof(GameObject), false);
            clip = (AnimationClip)EditorGUILayout.ObjectField("动画", clip, typeof(AnimationClip), false);
            if (EditorGUI.EndChangeCheck()) ClosePreview();
            if (GUILayout.Button("只读检查模型与动画")) report = StageAssetAudit.Inspect(model, clip);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("创建独立模型预览"))
                {
                    ClosePreview();
                    try { modelPreview = new StageModelPreview(model, clip); poseTime = 0; }
                    catch (System.Exception exception) { report = exception.Message; }
                }
            }
            if (modelPreview != null)
            {
                poseTime = EditorGUILayout.Slider("动画采样秒", poseTime, 0, (float)modelPreview.Duration);
                viewYaw = EditorGUILayout.Slider("观察角度", viewYaw, -180, 180);
                var rect = GUILayoutUtility.GetRect(100, 280, GUILayout.ExpandWidth(true));
                try { modelPreview.Draw(rect, poseTime, viewYaw); }
                catch (System.Exception exception) { report = exception.Message; ClosePreview(); }
                if (GUILayout.Button("关闭模型预览")) ClosePreview();
            }
            EditorGUI.BeginChangeCheck();
            profile = (StageActorProfile)EditorGUILayout.ObjectField("角色配置", profile, typeof(StageActorProfile), false);
            var profileChanged = EditorGUI.EndChangeCheck();
            if (profileChanged)
            {
                selectedAction = 0;
                ClosePreview();
                if (profile != null) model = profile.prefab;
            }
            if (profile != null && profile.actions != null && profile.actions.Length > 0)
            {
                selectedAction = Mathf.Clamp(selectedAction, 0, profile.actions.Length - 1);
                var names = System.Array.ConvertAll(profile.actions, a => a == null ? "<缺少动作>" : a.displayName);
                EditorGUI.BeginChangeCheck();
                selectedAction = EditorGUILayout.Popup("动作库", selectedAction, names);
                if (EditorGUI.EndChangeCheck() || profileChanged)
                {
                    ClosePreview();
                    clip = profile.actions[selectedAction] == null ? null : profile.actions[selectedAction].clip;
                }
            }
            if (GUILayout.Button("检查角色配置"))
            {
                var errors = StageAssetAudit.ValidateProfile(profile);
                report = errors.Length == 0 ? "结构检查通过；不代表运动、接地或视觉验收通过。" : string.Join("\n", errors);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("时间 / 距离实验（单位：米、秒）", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            start = EditorGUILayout.Vector3Field("世界起点", start);
            end = EditorGUILayout.Vector3Field("世界终点", end);
            duration = EditorGUILayout.FloatField("指定时长", duration);
            cycleDistance = EditorGUILayout.FloatField("周期距离", cycleDistance);
            cycleDuration = EditorGUILayout.FloatField("周期时长", cycleDuration);
            minRate = EditorGUILayout.FloatField("最小倍率", minRate);
            maxRate = EditorGUILayout.FloatField("最大倍率", maxRate);
            if (EditorGUI.EndChangeCheck()) plan = null;
            if (GUILayout.Button("计算运动计划"))
            {
                if (StageMotionPlan.TryCreate(new[] { start, end }, 0, duration, cycleDistance, cycleDuration,
                    minRate, maxRate, 0, 0, out plan, out var error))
                    report = $"运动数学实验可行：路径 {plan.Length:F3} 米，倍率 {plan.PlaybackRate:F3}。\n未检查地形、起停、接触和到达姿态。";
                else report = error;
                sampleTime = 0;
            }
            if (plan != null)
            {
                sampleTime = EditorGUILayout.Slider("采样时间", (float)sampleTime, 0, (float)plan.Duration);
                var frame = plan.Evaluate(sampleTime);
                EditorGUILayout.LabelField("位置", frame.Position.ToString("F3"));
                EditorGUILayout.LabelField("累计距离 / 相位", $"{frame.Distance:F3} / {frame.UnwrappedPhase:F3}");
                EditorGUILayout.LabelField("动画源时间", frame.ClipTime.ToString("F4"));
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("检查结果", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(report, GUILayout.MinHeight(180));
            if (GUILayout.Button("复制报告")) EditorGUIUtility.systemCopyBuffer = report;
            EditorGUILayout.EndScrollView();
        }
    }
}
