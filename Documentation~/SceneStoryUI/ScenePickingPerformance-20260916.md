# Scene 点选性能修复

定位到的重复工作：全路线点选使每个 Scene GUI 事件都扫描路线、Timeline 绑定和颜色；Repaint 重新采样曲线，Gizmo 再采样；LineRenderer 又独立刷新。贴地衔接新增的显示采样对 65 个点逐个积分速度曲线。

修改：
- Scene GUI 使用统一显示缓存，不再在每次鼠标事件或 Repaint 扫描场景与计算路线。
- 缓存平时每 0.2 秒更新，拖动手柄时每 0.1 秒更新；选择、撤销、层级与资源变动触发提前更新。
- 编辑模式 LineRenderer 使用相同采样结果；去掉重复 Gizmo 计算。运行时辅助线限频，实际角色的 Timeline 运动求值频率不变。
- 一批显示点只做一次 256 格累计积分，保留原网格、负速度处理与末格积分算法。

验证：Unity 2022.3.62f3 的 StageStory EditMode 测试 67/67 通过。65、70 点的批量积分与原逐点算法误差在 1e-10 内；过渡、重排、地面、点选回归通过。

当前 P2 场景共 54 条路线，同一显示数据计算工作负载各测 5 批：原版平均 55.147 ms，优化后 5.236 ms，降低约 90.5%。新缓存整批刷新平均 5.936 ms。该数据仅为工具显示计算耗时，不是场景总帧率或 GPU 帧耗时。

独立速度积分基准，65 点 × 100 批：123.32 ms → 2.44 ms。

目标工程已同步并重新编译；实际点击蓝色路线点，面板切换至科萌并选中第 5 段。临时测量脚本已移出 Assets，归档在目标 Library/CreatureStage/SceneStoryPerformanceAudit.executed.cs.txt；前后测量在同目录 overlay-benchmark-before-scene.txt、overlay-benchmark-after-scene.txt。测试报告在 E:/X/Validation/SceneStoryRelease_20260911/performance-tests.xml。

日志另有 Android Extension ADB 设备扫描及 CancellationTokenSource Dispose 消息；未将其归因到本工具，未改动 Android 插件。未测量完整运行场景总帧率或头显性能。
