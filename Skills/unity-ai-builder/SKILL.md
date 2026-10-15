---
name: unity-ai-builder
description: Use when Codex needs to turn one or more scene reference images into a Unity ProBuilder scene blockout, or one object reference image into a ProBuilder object blockout, using AIBuilder preview and review bundles.
---

# Unity AIBuilder 白盒搭建

## 只处理两类任务

- 同一场景的一张或多张参考图 -> `scene_blockout`。
- 一张物体参考图 -> `object_blockout`。

不要启动 Prefab 资产库、资产整理、旧 `SceneSpec` 场景生成或本地视觉模型流程，除非用户明确要求恢复旧能力。

## 执行顺序

1. 确认 Unity 项目路径和任务类型。
2. 检查所有参考图。多张图必须确认属于同一场景；不一致处标为假设。
3. 分析元素、屏幕轮廓、负空间、人物尺度、前中后景、角度、遮挡关系和不可见深度。
4. 读取包内 `Schemas~/ProBuilderModelSpec.v2.schema.json`。
5. 在 Unity 项目的 `Assets/AIBuilder/Data/<任务名>.json` 创建或更新 `ProBuilderModelSpec`。
6. 告知用户在 Unity 打开 `AI构建器/打开白盒搭建器` 并生成预览；若 Codex可控制当前 Unity 编辑器，则仍先预览，不直接确认写入。
7. 读取最新 `Library/AIBuilder/Reviews/*/CodexReviewRequest.md` 及其中列出的主、侧、俯视图。
8. 只修改同一份配置，修正体块、比例和布局；等待再次预览。
9. 最终写入必须由用户明确确认。

## 配置规则

- `blockoutType` 只能是 `scene_blockout` 或 `object_blockout`。
- `displayName` 使用清楚的任务名。
- `scaleHint` 写已知尺寸；未知时明确“按参考图保守推断”。
- `referenceImages` 可记录 Unity 内图片路径；图片只在当前 Codex 任务中时允许为空。
- `savePrefab`：场景为 `false`，物体为 `true`。
- `parts` 必须是命名清楚、可编辑的 ProBuilder 体块。
- `scene_blockout` 应按参考图构图填写 `camera`；`object_blockout` 可关闭它。
- `scene_blockout` 应填写 `referenceFrame` 和 `sceneAnalysis`；坐标使用左上角原点的 0 到 1 归一化值。
- 单图主构图优先使用 `screen_polygon`，并通过 `analysisElementId` 绑定分析元素；负空间元素不得生成实体。
- 先保证大轮廓、比例和空间关系，再增加次级体块。
- 单图不可见区域保守推断，并写入 `codexSuggestions` 或 `modelingNotes`。

支持的形状：`box`、`cylinder`、`cone`、`sphere`、`plane`、`prism`、`pipe`、`torus`、`arch`、`stair`、`screen_polygon`，以及 `empty/group` 层级节点。

## 复查重点

1. `Overlay.png`：固定相机构图、负空间和主要轮廓。
2. `ProjectionReport.md`：元素屏幕位置、尺寸和矩形 IoU。
3. `Side.png`：深度、遮挡、前中后景。
4. `Top.png`：布局、通道宽度、边界和密度。

不要把白盒扩展成最终建模、自动贴图或 Prefab 匹配任务。

## 安全边界

- 预览不得创建场景、Prefab 或材质资产。
- 配置变化后必须重新预览。
- 不覆盖已有输出。
- 场景写入保存新副本，不自动覆盖用户当前场景。
- 写入保留 Unity Undo，并要求明确确认。
