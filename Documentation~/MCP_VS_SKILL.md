# 为什么当前只用 Codex Skill

当前方案是：Codex Skill + Unity 编辑器包 + ProBuilder。

不接本地视觉模型，因为参考图理解由 Codex 完成；Unity 只做确定性的几何生成、截图和写入。

暂不接 MCP，因为核心闭环已经可以通过两个本地文件边界完成：

```text
Codex -> Assets/AIBuilder/Data/<任务>.json
Unity -> Library/AIBuilder/Reviews/<本轮复查包>
```

MCP 只有在需要 Codex 无人值守地调用正在运行的 Unity、查询实时 Scene 状态或自动触发多轮生成时才有明显价值。现阶段加入常驻桥接服务会增加安装、连接和故障排查成本，不提升白盒几何本身的质量。

质量主要来自：

- 明确区分场景与物体任务。
- 结构化 ProBuilder 体块配置。
- 主、侧、俯三视角复查。
- 配置修改后强制重新预览。
- 最终写入由人确认，且不覆盖旧结果。
