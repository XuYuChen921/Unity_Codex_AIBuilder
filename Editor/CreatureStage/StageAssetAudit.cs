using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AIBuilder.CreatureStage;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools.CreatureStage
{
    public static class StageAssetAudit
    {
        public static string Inspect(GameObject model, AnimationClip clip)
        {
            var report = new StringBuilder();
            report.AppendLine("角色剧情素材检查（只读，未执行校准）");
            report.AppendLine("Unity：" + Application.unityVersion);
            if (model == null) report.AppendLine("MODEL_MISSING：请选择 Project 中的模型或 Prefab。");
            else
            {
                var path = AssetDatabase.GetAssetPath(model);
                report.AppendLine("模型：" + path);
                if (string.IsNullOrEmpty(path)) report.AppendLine("MODEL_INSTANCE：当前为场景实例，请改选源资产以检查导入参数。");
                else report.AppendLine("依赖签名：" + AssetDatabase.GetAssetDependencyHash(path));
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null)
                {
                    report.AppendLine($"动画类型：{importer.animationType}；优化骨架：{importer.optimizeGameObjects}");
                    report.AppendLine($"导入 Scale：{importer.globalScale}；运动节点：{importer.motionNodeName}");
                    if (importer.animationType != ModelImporterAnimationType.Generic)
                        report.AppendLine("RIG_TYPE：本阶段需要 Generic 素材；检查器不会修改导入设置。");
                    if (importer.optimizeGameObjects) report.AppendLine("RIG_OPTIMIZED：需要检查被隐藏的运行时骨骼。");
                }
                var transforms = model.GetComponentsInChildren<Transform>(true);
                report.AppendLine("变换节点数：" + transforms.Length);
                foreach (var duplicate in transforms.GroupBy(t => t.name).Where(g => g.Count() > 1))
                    report.AppendLine("DUPLICATE_NAME：" + duplicate.Key + "；绑定必须使用完整相对路径。");
                foreach (var bone in transforms)
                {
                    if (bone.name.IndexOf("HorseLink", StringComparison.OrdinalIgnoreCase) >= 0)
                        report.AppendLine("HorseLink 候选：" + AnimationUtility.CalculateTransformPath(bone, model.transform));
                    var scale = bone.localScale;
                    if (scale.x <= 0 || Mathf.Abs(scale.x - scale.y) > 0.0001f || Mathf.Abs(scale.x - scale.z) > 0.0001f)
                        report.AppendLine("SCALE_REVIEW：" + AnimationUtility.CalculateTransformPath(bone, model.transform));
                }
            }
            if (clip == null) report.AppendLine("CLIP_MISSING：选择动画后可检查曲线绑定。");
            else
            {
                report.AppendLine($"动画：{AssetDatabase.GetAssetPath(clip)} / {clip.name}");
                report.AppendLine($"时长：{clip.length:F4} 秒；帧率：{clip.frameRate}；循环：{clip.isLooping}");
                report.AppendLine($"Legacy：{clip.legacy}；Humanoid：{clip.isHumanMotion}");
                var bindings = AnimationUtility.GetCurveBindings(clip);
                report.AppendLine("曲线数：" + bindings.Length);
                foreach (var group in bindings.Where(b => b.type == typeof(Transform) &&
                    (b.propertyName.StartsWith("m_LocalPosition", StringComparison.Ordinal) ||
                     b.propertyName.StartsWith("m_LocalRotation", StringComparison.Ordinal) ||
                     b.propertyName.IndexOf("Euler", StringComparison.OrdinalIgnoreCase) >= 0)).GroupBy(b => b.path))
                {
                    report.AppendLine("运动曲线候选节点：" + (group.Key.Length == 0 ? "<根>" : group.Key));
                    foreach (var binding in group)
                    {
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        if (curve == null || curve.length == 0) continue;
                        var delta = curve.Evaluate(clip.length) - curve.Evaluate(0);
                        report.AppendLine($"  {binding.propertyName}；关键帧 {curve.length}；首尾分量差 {delta:G5}");
                    }
                }
                report.AppendLine("首尾曲线分量差只用于定位，不代表已提取累计轨迹；旋转分量差不是转角。");
            }
            report.AppendLine("尚未验证：根运动清理、支撑区间、步幅、HorseLink 求解、地形及设备性能。");
            return report.ToString();
        }

        public static string[] ValidateProfile(StageActorProfile profile)
        {
            var errors = new List<string>();
            if (profile == null) return new[] { "PROFILE_MISSING：缺少角色配置。" };
            if (string.IsNullOrWhiteSpace(profile.actorTypeId)) errors.Add("ACTOR_ID：角色 ID 不能为空。");
            if (!profile.bindingsReviewed) errors.Add("BINDINGS_UNREVIEWED：骨架与运动来源尚未人工复核。");
            if (!StageMotionPlan.IsFinite(profile.uniformScale) || profile.uniformScale <= 0)
                errors.Add("ACTOR_SCALE：校准比例必须为正数。");
            if (profile.prefab == null || !EditorUtility.IsPersistent(profile.prefab))
                errors.Add("ACTOR_PREFAB：需要 Project 中的模型或 Prefab 资产。");
            else
            {
                var skeleton = Resolve(profile.prefab.transform, profile.skeletonRootPath);
                if (skeleton == null) errors.Add("SKELETON_PATH：骨架根路径不存在。");
                else
                {
                    if (Resolve(skeleton, profile.motionSourcePath) == null) errors.Add("MOTION_PATH：运动来源路径不存在。");
                    CheckLeg(skeleton, profile.leftLeg, "左腿", errors);
                    CheckLeg(skeleton, profile.rightLeg, "右腿", errors);
                }
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (profile.actions == null || profile.actions.Length == 0) errors.Add("ACTOR_ACTIONS：尚未配置动作。");
            else foreach (var action in profile.actions)
            {
                if (action == null) { errors.Add("ACTION_NULL：动作引用丢失。"); continue; }
                if (!ids.Add(action.actionId ?? "")) errors.Add("ACTION_DUPLICATE：动作 ID 重复：" + action.actionId);
                errors.AddRange(action.Validate().Select(e => action.name + " / " + e));
            }
            return errors.ToArray();
        }

        private static Transform Resolve(Transform root, string path)
        {
            return string.IsNullOrEmpty(path) ? root : root.Find(path);
        }

        private static void CheckLeg(Transform root, StageLegBinding leg, string label, List<string> errors)
        {
            if (leg == null) { errors.Add(label + "：缺少完整腿链。"); return; }
            var paths = new[] { leg.thigh, leg.calf, leg.horseLink, leg.foot };
            Transform previous = null;
            foreach (var path in paths)
            {
                var current = string.IsNullOrEmpty(path) ? null : root.Find(path);
                if (current == null) { errors.Add(label + "：骨骼路径缺失：" + path); previous = null; continue; }
                if (previous != null)
                {
                    if (current.parent != previous) errors.Add(label + "：必须绑定完整的相邻腿链，不能跳过 HorseLink。");
                    if (Vector3.Distance(current.position, previous.position) <= 0.000001f)
                        errors.Add(label + "：骨长近零，需要人工确认求解拓扑。");
                }
                previous = current;
            }
        }
    }
}
