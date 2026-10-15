# Scene Story 1.0.0-rc2 / 场景剧情编排

Requires Timeline. Open Tools > Scene Story > Open Story Editor.
需要 Timeline。菜单 Tools > 场景剧情 > 打开编排窗口。窗口顶部可切换中英文。

Import into a clean project. Do not install alongside the UPM variant or AIBuilder containing the same classes.
不要与包含相同类的 UPM 或 AIBuilder 同时安装。

See QuickStart.md and 使用教程.md. Samples/StoreExamples contains MultiActor, GroundLayers and AudioAndFacing.
GroundLayers keeps an overhead collider enabled with its Renderer disabled so the camera can show the route below.
地面示例的顶部碰撞面保持启用，仅隐藏 Renderer，便于看到下方路线。

Release candidate: some diagnostics and Inspector labels remain Chinese. Exact tested versions and limitations are recorded in the external Validation.json and listing drafts. Do not assume Tuanjie, mobile, XR or every render pipeline is verified.
此版本为候选版，不代表已通过商店审核；部分诊断和 Inspector 标签尚未英文化。团结引擎与未测平台不能宣称已兼容。

Publisher: GINDXRSTU. Support: chrowder7@163.com.
发行者：GINDXRSTU。售后邮箱：chrowder7@163.com。

## RC2 changes / 本次更新

Compact story editor layout, separate clip/property scrolling, persistent selected-clip header, collapsible groups, compact actor color and ground status, rounded speed display without changing stored precision.
主窗口布局优化：压缩顶部、列表与属性独立滚动、固定选中片段标题、折叠分组、紧凑角色颜色与地面状态、速度显示三位小数。

## Verification / 验证范围

Unity 2022.3.62f3 + Timeline 1.7.7: compilation and 76/76 EditMode tests passed. Live narrow dock and wide layout checked. Runtime scripts unchanged. RC1 player/Unity 6 checks are historical evidence, not repeated RC2 acceptance. See release-side Validation.json for archive checks.
本次未重新验证 Unity 6、Play、设备或团结引擎。

## Update / 更新

Import over Assets/SceneStory with all files selected, preserving paths and GUIDs. Back up first. Do not install alongside the old UPM variant. Store user-created scenes and timelines outside Samples.
先备份工程，在 Assets/SceneStory 上完整覆盖导入，不要漏选 StageLanguage.cs 等脚本及资源。不要与旧 UPM 包并装。自建场景和 Timeline 放在 Samples 之外，避免更新示例时覆盖。
