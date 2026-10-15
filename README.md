# Unity AIBuilder

Unity AIBuilder 是一个面向白盒搭建的 Unity 2022.3 编辑器包。Codex 负责理解参考图和规划体块，Unity + ProBuilder 负责生成、预览和安全写入。

只保留两个使用入口：

- 场景白盒：一张参考图，或同一场景的多视角图。
- 物体白盒：一张物体参考图。

不需要 Unity 内置视觉模型，不要求 MCP，也不以 Prefab 搜索或最终美术生成为目标。

## 安装

在 Unity Package Manager 中选择 `Add package from git URL`：

```text
https://github.com/XuYuChen921/Unity_Codex_AIBuilder.git
```

支持 Unity `2022.3.x`，依赖 URP 14、TextMeshPro 3 和 ProBuilder 5。

## 最短工作流

1. 在 Codex 中发送参考图，并说明要“场景白盒”或“物体白盒”。
2. Codex 分析比例、轮廓、空间关系和不可见部分，在 Unity 项目的 `Assets/AIBuilder/Data` 下写入白盒配置。
3. Unity 打开 `AI构建器/打开白盒搭建器`，自动识别最新任务。
4. 点击“生成 / 更新预览”。Unity 生成可编辑 ProBuilder 体块，并自动输出主、侧、俯视角复查图。
5. 需要修正时，让 Codex 根据复查包更新配置，再次预览。
6. 人工确认后，点击“确认写入场景”或“确认保存 Prefab”。

预览不会创建 Prefab、场景或材质资产。最终写入默认使用唯一文件名，不覆盖已有结果，并保留 Unity Undo。

## 数据与输出

Codex 使用统一的 `ProBuilderModelSpec` v2 描述场景和物体白盒：

```text
Schemas~/ProBuilderModelSpec.v2.schema.json
```

主要输出：

```text
Assets/AIBuilder/Data                         Codex 白盒任务
Library/AIBuilder/Reviews                    自动复查截图与请求
Assets/AIBuilder/Scenes                      已确认的场景副本
Assets/AIBuilder/Generated/Blockouts         场景白盒材质
Assets/AIBuilder/GeneratedModels             物体 Prefab 与材质
```

旧场景生成、Prefab 资产库和资产整理代码暂时保留用于兼容，但不再出现在正常菜单中。

详细流程见 `Documentation~/WORKFLOW.md`。
