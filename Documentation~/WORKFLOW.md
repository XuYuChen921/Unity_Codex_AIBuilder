# AIBuilder 白盒工作流

## 目标

AIBuilder 只解决一个问题：把参考图快速变成可编辑、可复查、可安全落盘的 ProBuilder 白盒。

它有两种任务：

- `scene_blockout`：一张场景图，或同一场景的多视角图。
- `object_blockout`：一张物体图。

Codex 是视觉分析和迭代入口；Unity 不运行本地视觉模型。

## 1. 在 Codex 中提交参考图

说明任务类型和已知尺度，例如：

```text
按这三张同一场景的参考图搭一个 ProBuilder 白盒。门高约 2.2 米，只需要空间、比例和主要体块。
```

Codex 应先提取：

- 主体轮廓和大体块。
- 前景、中景、背景及遮挡关系。
- 地面、墙体、通道、开口和关键物体的相对尺度。
- 多视角之间一致的空间关系。
- 单图无法确定的深度和背面假设。

## 2. Codex 写入白盒配置

配置写入：

```text
Assets/AIBuilder/Data/<任务名>.json
```

结构合同：

```text
Packages/com.xuyuchen.ai-builder/Schemas~/ProBuilderModelSpec.v2.schema.json
```

关键字段：

- `blockoutType`：`scene_blockout` 或 `object_blockout`。
- `displayName`：Unity 窗口显示名称。
- `scaleHint`：已知尺度或保守尺度假设。
- `referenceImages`：已导入 Unity 时记录路径；图片仅存在于 Codex 任务中时可以为空。
- `parts`：可编辑 ProBuilder 体块。
- `camera`：场景主参考相机；用于复现参考图构图和生成主视角截图，物体白盒可关闭。
- `referenceFrame`：参考图宽高、固定画幅、地平线和归一化坐标系。
- `sceneAnalysis`：元素轮廓、前中后景深度、人物尺度、遮挡关系、负空间与关键锚点。
- `analysisElementId`：把 ProBuilder 部件绑定到分析元素；`screen_polygon` 会从该元素的屏幕轮廓反投影成可编辑厚体。
- `codexSuggestions`：本轮仍需关注的不确定项。
- `savePrefab`：场景为 `false`，物体为 `true`。

优先用少量大体块建立正确轮廓和比例。不要在白盒阶段模拟纹理、装饰或最终材质。

## 3. Unity 预览

打开：

```text
AI构建器/打开白盒搭建器
```

窗口会按修改时间自动识别最新任务。选择“场景白盒”或“物体白盒”，点击“生成 / 更新预览”。

预览行为：

- 生成可编辑 ProBuilder 体块。
- 不创建场景、Prefab 或材质资产。
- 自动截取主视角、侧视角和俯视角。
- 当参考图路径有效时，额外生成 `Reference.png`、`Overlay.png`、`Difference.png` 和 `ProjectionReport.md`。
- 在 `Library/AIBuilder/Reviews/<任务名_时间>` 创建唯一复查包。

## 4. Codex 复查与迭代

复查包包含：

```text
Main.png
Side.png
Top.png
ProBuilderModelSpec.snapshot.json
BlockoutSummary.md
ReviewManifest.json
CodexReviewRequest.md
```

Codex 应依次检查轮廓、比例、深度、通道宽度、遮挡和不可见面假设。只更新配置，不直接替用户确认最终写入。

配置在同一路径被修改后，Unity 会自动让旧预览失效，必须重新预览。

## 5. 人工确认写入

- 场景白盒：“确认写入场景”，在 `Assets/AIBuilder/Scenes` 保存新的场景副本，并持久化材质。
- 物体白盒：“确认保存 Prefab”，保存新的 Prefab 和材质。

两种操作都先弹出确认，不覆盖同名资产，并保留 Undo。Unity 中未保存的其他编辑不应被自动覆盖。

## 能力边界

- 输出是白盒，不是最终美术资产。
- 单张图无法唯一恢复真实三维结构；Codex 必须保守推断并记录假设。
- 多视角必须来自同一场景，且视角之间不能互相矛盾。
- 当前不需要 MCP；Codex 和 Unity 通过项目内配置与本地复查包协作。
