# 多角色 Timeline 修复与工程同步

2026-09-14，目标工程：`D:/Unity Project/dinosaurtosky_3.0_beta1`。

- 修复窗口固定编辑第一条角色轨的问题；新增当前角色选择，并跟随原生 Timeline 的角色轨、动画轨和片段选择，必要时切换当前 Director。
- 新增角色轨对及并入独立剧情入口；复制时间、动画设置、场景引用，停用来源播放，支持 Undo。已有内容冲突会明确阻止覆盖。
- 整段预览使用完整 Timeline 时长；新增片段检查锁定、角色绑定及原生 Preview 状态。
- 目标场景 `Assets/GamePlay/Art/Scene/P2白模.unity` 已保存。主剧情包含中华龙鸟 30 段、龙出现 1 段、科萌 2 段；按用户选择保留主剧情的龙出现片段。
- 原有 11 条输出轨内容比较一致，音频保留；两个独立 SceneStory Director 停用，原资源及路线保留。其他旧 Timeline 未改动。
- 备份：目标工程 `Library/CreatureStage/MergeBackup-20260914-162740`；执行报告 `Library/CreatureStage/merge-result.txt`。
- 隔离 Unity 2022.3.62f3 工程 EditMode 测试 48/48 通过，包括三角色共同求值/恢复、目标轨新增、跨 Director 选择、引用复制、拒绝覆盖及 Undo。
- 测试报告：`E:/X/Validation/SceneStoryRelease_20260911/MultiActor-tests.xml`。目标工程实际点击龙出现片段，窗口正确切换到主剧情第二条角色轨。
- 尚未进行目标场景整段播放和设备验收。本次未重发历史 1.0.0 安装包。
