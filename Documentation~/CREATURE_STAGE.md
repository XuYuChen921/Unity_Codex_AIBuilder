# 角色剧情编排工具：开发入口

当前主入口是场景剧情编辑原型：支持在白模场景中编辑路线、停留、动作和 Timeline 时间。完整用法见 [场景剧情编辑示例](CREATURE_STAGE_SCENE_EDITING.md)。需求与排期见同目录的《Unity2022.3_角色剧情编排工具_开发计划_V1.0.md》。

## 第一次用：编辑场景剧情

打开菜单 `Tools > AIBuilder > 角色剧情 > 打开场景剧情编辑示例`，选择片段后在 Scene 拖动路线控制点，在“场景剧情编排”窗口修改动画和时长并预览。已接入 `Export.fbx`，场景为 `Assets/CreatureStage/Examples/StoryEditing/中华龙鸟_场景剧情编辑.unity`。

## 旧素材展示入口

在 `AIBuilderTest` 中打开 `Tools > AIBuilder > 角色剧情 > 打开中华龙鸟示例`，然后点击 Unity 顶部 Play，切到 Game 窗口。

示例场景位于 `Assets/CreatureStage/Examples/SinosauropteryxDemo/中华龙鸟_入门示例.unity`，也可以直接双击打开。已配好模型、贴图、展示台和中文控制面板，不需要自己拖引用。

默认每 4 秒轮播待机、行走、奔跑、低伏。左侧按钮可以切换动作，进度条可以定位姿态，底部滑条可以转动视角。这个示例展示原地素材，不代表路径、过渡和防滑步已经完成。

## 工程与素材位置

- 插件源码：当前 AIBuilder 包的 `Runtime/CreatureStage` 和 `Editor/CreatureStage`。
- 已核实的测试宿主：`D:/Unity Project/AIBuilderTest`，Unity `2022.3.62f3`，URP `14.0.12`，Timeline `1.7.7`。
- 已接入的源素材：`D:/Unity Project/AIBuilderTest/Assets/CreatureStage/Source/Sinosauropteryx`，含 1 个模型和 37 个动画 FBX。
- 素材子目录：`Model`、`Animations`、`Textures`；完整素材目录可整体保留，不强制打散。
- 自动化验证使用独立工程 `E:/X/Validation/CreatureStageValidation_20260911`，不修改业务场景。

宿主与包版本于 2026-09-11 从实际文件核实，未升级已有依赖。尚未确认正式发布平台、刷新率和并发数量。

## 当前可用功能

Unity 菜单：`Tools > AIBuilder > 角色剧情 > 开发工作台`。

1. 选择 Project 内的模型或 Prefab，以及动画子资产。
2. 点击“只读检查模型与动画”，查看 Generic 类型、优化设置、比例、重复骨名、HorseLink 候选、动画绑定及曲线首尾分量差。
3. 选择已生成的 `Assets/CreatureStage/Generated/Intake_20260911_01/ActorProfile.asset`，在动作库下拉框切换动作；也可在 Project 的 Create 菜单中使用 `AIBuilder > 角色剧情 > 角色配置/动作定义` 新建配置。
4. 为角色填入模型、相对骨架路径、完整左右腿链和动作列表，再检查角色配置。
5. 点击“创建独立模型预览”，拖动“动画采样秒”检查原动画姿态，调整观察角度。预览使用副本，关闭窗口、脚本重载、进入或退出 Play Mode 时释放。
6. 用“时间 / 距离实验”输入世界起终点、时长、周期距离和倍率范围。成功后可拖动采样时间检查位置、展开相位和源动画时间；超出倍率范围时拒绝生成计划。

检查器不修改导入器、源 FBX、动画曲线、场景或 Prefab。首尾分量差不是根运动提取结果，四元数分量差不是转身角度；报告用于人工定位。

## 运行时基础 API

- `StageActionDefinition`：动作 ID、类型、采样区间、周期距离、倍率范围和校准标记。默认未校准，结构检查不能代替实测批准。
- `StageActorProfile`：角色资产、骨架路径、完整腿链及动作引用。当前腿链检查针对有 HorseLink 的地面角色，Hover 配置另行开发。
- `StageMotionPlan.TryCreate`：将世界折线路径复制为只读弧长表，检查时间、倍率和数值范围；`Evaluate(absoluteTime)` 由绝对时间返回位置和循环采样时间。
- `StagePoseSession`：独占无 Controller/其他动画图的启用 Animator，使用手动 Playables 绝对采样。禁止 AnimationEvent 副作用，退出时恢复局部姿态与 Animator 设置。必须用 `using` 或 `finally` 清理。

`StagePoseSession` 是底层实验 API，不负责扫描任意外部脚本是否写骨骼。调用方必须提供独立、明确交出控制权的对象，不能直接用于仍有导航或业务脚本控制的角色。已有 Controller 的 Animator 即使移除 Controller，也可能暂时保留内部图绑定；当前不自动接管这类对象。工作台预览使用无业务脚本、未优化骨架的 Generic FBX 副本，避免竞争。该预览不等于完整 Timeline 预览工作流。

`StageIntake` 支持批处理检查请求：在当前验证工程 `Library/CreatureStage/intake.request.json` 指定 `modelPath`、`animationFolder`、`outputFolder`，用 `-executeMethod AIBuilder.EditorTools.CreatureStage.StageIntake.RunBatch` 执行。输出只允许写入 `Assets/CreatureStage/Generated/` 下的新目录，不覆盖既有配置。交互编辑器也会消费显式提交的同名请求；默认无请求时不执行扫描或写入。

## 明确未完成的能力

- 自动步态匹配与转身规划。Timeline 移动/停留编排、速度曲线、入场姿态混合和朝向缓转已实现，见场景编辑说明。
- 非线性周期距离曲线、接触区间、支撑锚点和防滑步。
- 身体修正、HorseLink IK、注视与 Hover。指定地面碰撞器的路线投射已实现。
- 剧情编译缓存、失效检测、事件状态、多角色时钟及交互等待。
- 真实中华龙鸟视觉验收、目标平台构建和设备性能。

当前运动实验是匀速折线，起停瞬间无速度过渡、折点方向可突变；只用于基础数学验证，不能作为正式表演。零距离移动被拒绝，后续应使用独立静止表演逻辑。动画循环终点按循环首相位采样，不等于已验证的停稳姿态。

## 下一步

当前 37 个动画的路径兼容和随机 Seek 已验证；所有动作定义仍为待分类、未校准。下一步复核负缩放末端辅助骨骼、确认运动来源和动作语义，标注足底接触、校准步幅，再建立最小平地与缓坡实验。核心运动、接触和 HorseLink 验证通过后，再扩展完整 Timeline 美术编辑器。

阶段证据见 [2026-09-11 开发记录](CREATURE_STAGE_PROGRESS_20260911.md)。
