using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AIBuilder.CreatureStage;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools.CreatureStage
{
    // 编辑器自动化只处理本工程 Library/CreatureStage 中显式提交的检查请求。
    [InitializeOnLoad]
    public static class StageIntake
    {
        [Serializable]
        public sealed class Request
        {
            public string modelPath;
            public string animationFolder;
            public string outputFolder;
        }

        private static double nextPoll;
        static StageIntake() { EditorApplication.update += Poll; }

        public static void RunBatch()
        {
            var requestPath = Path.GetFullPath("Library/CreatureStage/intake.request.json");
            var result = Run(JsonUtility.FromJson<Request>(File.ReadAllText(requestPath)));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(requestPath), "intake.result.txt"), result);
        }

        private static void Poll()
        {
            if (Application.isBatchMode) return;
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextPoll = EditorApplication.timeSinceStartup + 2;
            var requestPath = Path.GetFullPath("Library/CreatureStage/intake.request.json");
            if (!File.Exists(requestPath)) return;
            var runningPath = requestPath + ".running";
            if (File.Exists(runningPath)) return;
            File.Move(requestPath, runningPath);
            try
            {
                var request = JsonUtility.FromJson<Request>(File.ReadAllText(runningPath));
                var result = Run(request);
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(requestPath), "intake.result.txt"), result);
                Debug.Log("Creature Stage 素材检查完成：" + result);
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(requestPath), "intake.result.txt"), "FAILED\n" + exception);
                Debug.LogException(exception);
            }
            finally { File.Delete(runningPath); }
        }

        public static string Run(Request request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ValidateAssetPath(request.modelPath);
            ValidateAssetPath(request.animationFolder);
            ValidateAssetPath(request.outputFolder);
            if (!request.outputFolder.StartsWith("Assets/CreatureStage/Generated/", StringComparison.Ordinal))
                throw new ArgumentException("输出必须位于 Assets/CreatureStage/Generated 的新目录。");
            if (Directory.Exists(request.outputFolder) || File.Exists(request.outputFolder))
                throw new IOException("输出已存在，不覆盖已有配置。");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(request.modelPath);
            if (model == null) throw new ArgumentException("模型尚未导入或不存在。");
            if (!AssetDatabase.IsValidFolder(request.animationFolder)) throw new ArgumentException("动画目录不存在。");
            var animations = AssetDatabase.FindAssets("t:AnimationClip", new[] { request.animationFolder })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (animations.Length == 0) throw new ArgumentException("目录中没有已导入的动画。");

            EnsureFolder(request.outputFolder);
            var profile = ScriptableObject.CreateInstance<StageActorProfile>();
            profile.actorTypeId = "sinosauropteryx";
            profile.prefab = model;
            var roots = model.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Bone_root").ToArray();
            var skeleton = roots.Length == 1 ? roots[0] : model.transform;
            profile.skeletonRootPath = AnimationUtility.CalculateTransformPath(skeleton, model.transform);
            profile.leftLeg = FindLeg(skeleton, "L");
            profile.rightLeg = FindLeg(skeleton, "R");
            var definitions = new List<StageActionDefinition>();
            var report = new StringBuilder("# 中华龙鸟素材接入报告\n\n");
            report.AppendLine("生成时间：" + DateTime.Now.ToString("O"));
            report.AppendLine("状态：自动候选配置；未审批、未校准，不代表接地或动画验收。\n");
            report.AppendLine("```text\n" + StageAssetAudit.Inspect(model, null) + "```\n");
            report.AppendLine("## 骨架路径与参考位置\n");
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
                report.AppendLine($"- `{AnimationUtility.CalculateTransformPath(bone, model.transform)}`；局部位置 {bone.localPosition.ToString("F5")}；局部比例 {bone.localScale.ToString("F5")}");
            report.AppendLine("\n## 动画与绑定兼容检查\n");
            foreach (var path in animations)
            {
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)))
                {
                    var action = ScriptableObject.CreateInstance<StageActionDefinition>();
                    var id = Path.GetFileNameWithoutExtension(path) + "_" + definitions.Count;
                    action.actionId = id;
                    action.displayName = Path.GetFileNameWithoutExtension(path) + "（待分类）";
                    action.kind = StageActionKind.OneShot;
                    action.clip = clip;
                    action.clipOut = clip.length;
                    action.calibrationNotes = "自动接入：未确认动作语义、根运动、循环、距离及接触区间。";
                    AssetDatabase.CreateAsset(action, request.outputFolder + "/Action_" + id + ".asset");
                    definitions.Add(action);
                    var animationModel = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var mismatches = animationModel == null ? -1 : skeleton.GetComponentsInChildren<Transform>(true).Count(b =>
                    {
                        var relative = AnimationUtility.CalculateTransformPath(b, model.transform);
                        return relative.Length != 0 && animationModel.transform.Find(relative) == null;
                    });
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    var missing = bindings.Where(b => b.type == typeof(Transform) && b.path.Length != 0 && model.transform.Find(b.path) == null).ToArray();
                    report.AppendLine($"### {Path.GetFileName(path)} / {clip.name}\n");
                    report.AppendLine($"骨架路径缺失数：{mismatches}；动画目标缺失数：{missing.Length}。路径相同不代表轴向、绑定姿态或蒙皮已经兼容。\n");
                    foreach (var binding in missing) report.AppendLine("- 缺失绑定：`" + binding.path + "` / " + binding.propertyName);
                    report.AppendLine("```text\n" + StageAssetAudit.Inspect(animationModel, clip) + "```\n");
                }
            }
            profile.actions = definitions.ToArray();
            AssetDatabase.CreateAsset(profile, request.outputFolder + "/ActorProfile.asset");
            report.AppendLine("## 配置待办\n\n" + string.Join("\n", StageAssetAudit.ValidateProfile(profile).Select(e => "- " + e)));
            var reportPath = request.outputFolder + "/IntakeReport.md";
            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(reportPath);
            AssetDatabase.SaveAssets();
            return reportPath;
        }

        private static StageLegBinding FindLeg(Transform root, string side)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            Func<string, string> find = suffix =>
            {
                var matches = all.Where(t => t.name == "Bip001 " + side + " " + suffix).ToArray();
                return matches.Length == 1 ? AnimationUtility.CalculateTransformPath(matches[0], root) : "";
            };
            return new StageLegBinding { thigh = find("Thigh"), calf = find("Calf"), horseLink = find("HorseLink"), foot = find("Foot") };
        }

        private static void ValidateAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains("\\") || path.Split('/').Any(p => p == ".." || p == "." || p.Length == 0))
                throw new ArgumentException("需要不含相对跳转的 Assets 路径。");
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var parent = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var current = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(current)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = current;
            }
        }
    }
}
