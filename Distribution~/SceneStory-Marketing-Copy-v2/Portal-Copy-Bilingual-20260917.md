# 商店填写文案 · 2026-09-17

## Summary（直接替换）
Stage multiple actors on one Unity Timeline. Edit routes, Hold/Move clips, facing and distance-synced gait. 在同一时间轴编排多角色走位、停留与转向，支持经校准的步态距离同步。

## Description（直接替换）
Scene Story is a Unity Timeline tool for multi-actor staging, character routes and cutscene blocking. Arrange where actors move, when they wait and how they turn, then revise the performance directly in the editor.

KEY FEATURES
• Coordinate multiple scene actors on one Timeline.
• Edit straight or curved routes in Scene view.
• Insert Hold or Move clips after the selected clip. Reorder clips while automatically connected starts follow the preceding endpoint.
• Use ripple timing to shift later route clips on the same actor track. Native animation, audio and other actor tracks remain independently authored.
• Animate facing offsets to turn, hold and return within a clip; adjust facing transitions between clips.
• Synchronize looping walk/run animations to traveled distance after calibrating distance per animation cycle.
• Bind walkable static meshes, exclude vegetation objects and control grounded/free-space height transitions.
• Give actors distinct route colors. Select a route point to switch the active actor track.
• Scrub the Timeline to inspect key moments without replaying the whole scene.

Use Scene Story for cutscene blocking, narrative previews, scripted character performances and fixed exhibition sequences. Main authoring controls support English and Simplified Chinese.

Scene Story 是基于 Unity Timeline 的多角色剧情编排工具。统一安排角色去哪里、何时停留、如何转身，并在编辑器里反复调整剧情走位与表演节奏。

主要功能
• 在同一条 Timeline 上编排多个场景角色。
• 在 Scene 视图直接编辑直线或曲线路径。
• 在选中片段后插入停留或移动；重排片段时，开启自动连接的路线起点跟随前段终点。
• 修改时长时顺延当前角色后续路线片段。原生动画、音频及其他角色轨道保持独立编排。
• 在同一片段内安排转向、保持与回正，并调节跨片段朝向过渡。
• 校准动画循环对应的移动距离后，让循环走跑动画按实际路程推进。
• 绑定可行走静态 Mesh、排除植被对象，控制贴地与自由空间的高度过渡。
• 自定义各角色路线颜色，选择路径点即可切换当前角色轨道。
• 拖动 Timeline 检查关键时刻，无需每次从头播放。

适合过场走位预演、剧情场景、多角色固定演出和展陈叙事。主要编排界面支持英文与简体中文。

INCLUDED / 包含内容
Scene Story editor and runtime scripts, primitive-based examples and quick-start documentation.
Scene Story 编辑器与运行时代码、基础几何体示例和快速入门文档。

SCOPE / 功能范围
Routes are authored by the user. Automatic pathfinding, dynamic avoidance, dialogue, camera direction, head-look IK, foot locking and automatic retargeting are not included. Gait sync requires suitable looping animations and calibration. Ground filtering operates at object/mesh level; combined multi-level surfaces may require separate walking surfaces.
路线由作者编排；不包含自动寻路、动态避障、对话、镜头系统、头部注视 IK、脚部锁定或自动动画重定向。步态同步需要合适的循环动画与校准。地面筛选在对象/Mesh 层级执行，合并的多层地面可能需要拆分行走面。

Promotional character models and animations are not included. No external AI service or API key is required to use the tool.
宣传视频中的人物模型和动画不包含在插件中。使用工具无需外部 AI 服务或 API 密钥。

Website: https://www.gindxrstu.com/
Support: chrowder7@163.com
Publisher: GINDXRSTU

## Technical details（直接替换）
• Unity: release package exported with Unity 2022.3.62f3.
• Dependency: Unity Timeline (com.unity.timeline) 1.7.7 for Unity 2022.3; install through Package Manager before importing.
• Validated render pipeline: Built-in. Bundled saved examples use Standard materials. URP/HDRP sample material conversion is not included.
• Validated editor OS: Windows. Other operating systems and XR devices have not been validated.
• English and Simplified Chinese authoring UI.
• Do not install alongside another package containing the same Scene Story classes.

• 发布包由 Unity 2022.3.62f3 导出。
• Unity 2022.3 依赖 Timeline 1.7.7，请在导入前通过 Package Manager 安装。
• 已验证管线：Built-in。附带示例使用 Standard 材质，不包含 URP/HDRP 示例材质转换。
• 已验证编辑器操作系统：Windows；其他系统及 XR 设备尚未验证。
• 编排界面支持英文与简体中文。
• 不要与含有相同 Scene Story 类型的其他包重复安装。

## Release notes（首次发布文案）
Initial release: multi-actor Timeline route authoring, Hold/Move clip editing, connected route reordering, facing curves, calibrated distance-synced gait, ground binding and bilingual authoring controls.

This build also improves Timeline recognition: the window can use the Timeline currently inspected in Unity, or resolve a Timeline asset with exactly one scene Director binding. When multiple Directors share the asset, select the intended scene Director explicitly.

首次发布：多角色 Timeline 路线编排、停留/移动片段编辑、自动连接路线重排、朝向曲线、经校准的步态距离同步、地面绑定及中英文编排界面。

本构建同时改进 Timeline 识别：可使用 Unity 当前查看的 Timeline，或识别只有一个场景 Director 绑定的 Timeline 资源；多个 Director 共用该资源时，需要明确选择目标场景 Director。

## 使用方式
先填写 Description 页的 Summary、Description、Technical details，保留既有 AI 使用披露，再 Save。Release notes 是独立页，粘贴对应内容即可。文案按当前发布包能力组织；不将 AI 辅助开发写成插件具有 AI 生成剧情功能，也不承诺搜索排名。
