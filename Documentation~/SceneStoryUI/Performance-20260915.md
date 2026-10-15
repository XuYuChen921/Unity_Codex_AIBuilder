# P2 场景运行卡顿检查

目标：`D:/Unity Project/dinosaurtosky_3.0_beta1`，Unity 2022.3.62f3，2026-09-15。

## 原因与修改

绘制路线的 65 个显示点各自调用 `Sample`，每次都重新查询场景 Director、片段顺序、相邻路线以及地面签名。当前场景 50 条路线、37 个有效移动片段、4 个 StageRouteLine，在 Scene 视图开启时重复成本明显。

新增 `StageRoute.SamplePolyline`，每条显示线只刷新一次，再对缓存批量采样。Scene Overlay 和 LineRenderer 共用它。Play 期间停止仅供编辑的 Overlay/Gizmo 绘制，保留正常角色运动和原有运行时 LineRenderer。

历史日志存在 `StageGroundRepair` 运行中调用 `MarkSceneDirty` 的异常，以及路线未命中地面的错误。修复入口增加 Play 状态检查；当前场景复测 37 个移动片段均未发现采样错误，没有改写用户的路线或地面配置。

## 验证

- 修改前：37 条路线各采样 65 点，合计 174.7302 ms；修改后批量采样合计 2.8643 ms。
- 相同场景编辑器 Play 抽样：修改前 192.32 ms/frame（前 30 帧排除前 5 帧），修改后完整运行平均约 4.33 ms/frame（同样排除前 5 帧）。采样时长不同，是现场抽样对比，不是设备性能结论。
- 主剧情自然运行至 137.177880936881 / 137.177880936881 秒，播放标记停止；运行期间捕获 Error/Exception/Assert 为 0，Console 错误数为 0。
- EditMode 56/56 通过，包括批量与逐点采样结果一致、节点修改后更新，以及已有插入、重排、撤销和角色恢复测试。
- 临时诊断脚本已从 Assets 清理，执行脚本和原始证据保留于目标工程 `Library/CreatureStage` 下的 `performance-before.json`、`performance-play-before.json`、`performance-edit-after.json`、`performance-play-after.json`。
- 本次没有设备验收，没有修改场景的模型、材质、灯光或作者编排的数据。

## 14:33 用户再次反馈后的补充修复

最新日志证明仅检查 `isPlayingOrWillChangePlaymode` 不够：进入 Play 的场景恢复后，自动地面修复仍到达 `MarkSceneDirty` 并抛异常。修复现在同时检查 Application/Editor 的运行状态，并用 SessionState 保存跨域重载的 Play 过渡状态，直到收到 EnteredEditMode 才解除；退出后延迟两秒执行待处理的地面修复。

路线未命中地面的异常增加路线名称与失败坐标，保留原来的贴地约束，不自动关闭贴地或改写作者路线。

- 新增过渡状态下地面修复不能创建场景设置的回归测试，EditMode 57/57 通过。
- 本次未安装任何提前采样/预热路线的诊断脚本。实际 Game 自然运行至 137.2 / 137.2 秒，Console 与对应运行日志无这两类错误；Game Stats 现场读数约 198–232 FPS，仅代表当前编辑器画面的瞬时值。
- 第二类地面报错在本次完整播放中未复现，不能单独断言其所有触发原因均已消除。
