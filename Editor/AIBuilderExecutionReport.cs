using System;
using System.Collections.Generic;
using System.Linq;

namespace AIBuilder.EditorTools
{
    [Serializable]
    public sealed class AIBuilderAssetDecision
    {
        public string semanticType;
        public string matchMode;
        public string requested;
        public string resolvedPath;
        public int count = 1;
    }

    [Serializable]
    public sealed class AIBuilderExecutionReport
    {
        public const string ExactPrefab = "exact_prefab";
        public const string KeywordFallback = "keyword_fallback";
        public const string ProceduralFallback = "procedural_fallback";
        public const string Skipped = "skipped";

        public string sceneName;
        public string generatedAtUtc;
        public int scannedPrefabCount;
        public List<AIBuilderAssetDecision> decisions = new List<AIBuilderAssetDecision>();
        public List<string> warnings = new List<string>();

        public AIBuilderExecutionReport(string sceneName)
        {
            this.sceneName = sceneName ?? string.Empty;
            generatedAtUtc = DateTime.UtcNow.ToString("O");
        }

        public void RecordDecision(string semanticType, string matchMode, string requested, string resolvedPath)
        {
            semanticType = string.IsNullOrWhiteSpace(semanticType) ? "unknown" : semanticType.Trim();
            requested = requested?.Trim() ?? string.Empty;
            resolvedPath = resolvedPath?.Trim() ?? string.Empty;

            var existing = decisions.FirstOrDefault(item =>
                string.Equals(item.semanticType, semanticType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.matchMode, matchMode, StringComparison.Ordinal) &&
                string.Equals(item.requested, requested, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.resolvedPath, resolvedPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.count++;
                return;
            }

            decisions.Add(new AIBuilderAssetDecision
            {
                semanticType = semanticType,
                matchMode = matchMode,
                requested = requested,
                resolvedPath = resolvedPath,
                count = 1
            });
        }

        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning) && !warnings.Contains(warning))
            {
                warnings.Add(warning);
            }
        }

        public string ToMarkdown()
        {
            var lines = new List<string>
            {
                "# AIBuilder 资产决策报告",
                string.Empty,
                $"- 场景：`{sceneName}`",
                $"- 扫描 Prefab：{scannedPrefabCount}",
                $"- 精确匹配：{Count(ExactPrefab)}",
                $"- 关键词回退：{Count(KeywordFallback)}",
                $"- 程序化回退：{Count(ProceduralFallback)}",
                $"- 跳过：{Count(Skipped)}",
                string.Empty,
                "## 决策明细",
                string.Empty,
                "| 类型 | 方式 | 数量 | 请求 | 最终资产 |",
                "| --- | --- | ---: | --- | --- |"
            };

            if (decisions.Count == 0)
            {
                lines.Add("| - | - | 0 | - | - |");
            }
            else
            {
                foreach (var item in decisions)
                {
                    lines.Add($"| {Escape(item.semanticType)} | {Escape(item.matchMode)} | {item.count} | {Escape(item.requested)} | {Escape(item.resolvedPath)} |");
                }
            }

            lines.Add(string.Empty);
            lines.Add("## 警告");
            lines.Add(string.Empty);
            if (warnings.Count == 0)
            {
                lines.Add("- 无");
            }
            else
            {
                lines.AddRange(warnings.Select(warning => "- " + warning));
            }

            return string.Join("\n", lines) + "\n";
        }

        private int Count(string mode)
        {
            return decisions.Where(item => item.matchMode == mode).Sum(item => item.count);
        }

        private static string Escape(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
