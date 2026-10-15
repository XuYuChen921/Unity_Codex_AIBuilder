# 搜索文案与发布说明

状态：本地文案草稿，未修改商店后台、官网或已交付的商店 Kit ZIP。Listing.en.md 与 Listing.zh-CN.md 是新版文案来源；上传时替换旧 Kit 文案。兼容性、AI 使用披露继续使用发布包对应的审核资料，不用营销文案覆盖。

## 产品身份

英文固定名称：Scene Story - Actor Routes for Timeline
中文展示名称：Scene Story — Unity Timeline 多角色剧情编排
发布者：GINDXRSTU
网站：https://www.gindxrstu.com/

官网建议英文 title：Scene Story | Unity Timeline Character Staging
官网建议中文 title：Scene Story | Unity Timeline 多角色剧情编排
Meta description 可用各语言 Listing 的摘要。商店标题保持稳定，把 cutscene、character staging 等搜索意图放进摘要和正文。关键词建议不是搜索量或排名数据。

## 中英文 FAQ（可用于官网及商店描述补充）

### Scene Story 是什么？ / What is Scene Story?

Scene Story 是 GINDXRSTU 开发的 Unity Timeline 角色走位与多角色演出编排工具。它将场景路径编辑、停留与移动片段、朝向控制和可选距离同步步态放进可视化制作流程。

Scene Story is a Unity Timeline tool by GINDXRSTU for character routes and multi-actor staging. It combines scene path editing, Hold and Move clips, heading control and optional distance-synchronized gait in an authoring workflow.

### 和 NavMeshAgent 有什么区别？ / How does it differ from NavMeshAgent?

NavMeshAgent 主要解决导航移动。Scene Story 主要解决预先编排的表演：谁在什么时候走到哪里、停留多久、如何转身，以及剧情修改后如何编辑路线与时间。它不提供动态避障，也不宣称能替代所有导航需求；导航系统配合自定义脚本也能实现演出。

NavMeshAgent handles navigation movement. Scene Story focuses on authored performances: where actors move, when they wait, how they turn, and how to revise those choices on a Timeline. It does not provide dynamic avoidance. Navigation systems can also support scripted performances with additional tooling.

### 改变时长后，走路动画如何匹配？ / How does gait respond to timing changes?

启用距离同步并校准每个动画循环的移动距离后，步态采样由路程驱动。改变片段时长会改变单位时间内的移动及动画进度。它不是脚部 IK，仍需检查动画选择、速度范围与实际滑步。

With distance synchronization enabled and distance per animation cycle calibrated, gait sampling follows traveled distance. Changing clip duration changes movement and animation progress over time. This is not foot IK; animation suitability, speed range and visible sliding still require review.

### 是否需要 AI 服务或包含人物模型？ / Does it need AI services or include characters?

使用插件无需 AI 服务或 API 密钥。演示人形模型不包含在插件包内。AI 辅助开发说明应在商店要求的披露字段如实填写，与运行时功能分开说明。

The tool does not require an AI service or API key. Promotional humanoid models are not included. AI-assisted development is disclosed separately from runtime features in the store's disclosure field.

## 搜索实施顺序

1. 商店：使用具体摘要、真实功能说明、12 个相关关键词和可读截图；不堆砌 AI、NavMesh、最佳、零滑步等误导性词语。
2. 官网：建立可公开访问的产品详情页，正文包含用途、操作、上述 FAQ、文档和演示。重要信息提供 HTML 文字，不能仅烧录进视频。中英文分别提供页面并互相链接；实际部署时再配置 canonical/hreflang。
3. 视频：标题可用“Scene Story: Revise Multi-Actor Blocking in Unity Timeline”；中文“Scene Story：在 Unity Timeline 中快速修改多角色走位”。描述写出片中实际操作，附字幕、文字记录、产品页和教程链接。录完后再填写准确章节时间。
4. 结构化数据：仅在自有网站根据真实可见信息配置合适的 Product/SoftwareApplication、VideoObject 信息。当前没有公开视频 URL、最终售价或评价数据，不提前生成虚构数据；不承诺富结果或 AI 引用。
5. 发布后：用站点搜索表现检查实际查询词、曝光、点击和转化，再调整标题摘要。Unity 商店内部检索与 Google 是不同系统，不把 Google 建议当作商店排名规则。

Google 官方说明：AI 搜索仍使用基础 SEO 原则；重要内容应有文本、可抓取并可被索引，结构化数据应与可见内容一致，不需要专用 AI 文件或特殊标记，也不保证收录或引用。此说明只针对 Google，不推断所有 AI 搜索平台采用相同规则。
来源（2026-09-16 查阅）：https://developers.google.com/search/docs/appearance/ai-features

## 字幕交付说明

StoryRevision-DRAFT.*.srt 是新 90 秒分镜的排时草稿，不能直接套用旧 36 秒视频。包含中文、英文、双语各 11 条。实际录制若功能没有展示，必须删改对应字幕。

1080p 建议：底部留安全边距，白字配半透明深底，中英各一行；中文约 32 px，英文约 28 px，避免遮挡 Timeline 数值和点击区域。国际商店主片可用英文一行，双语版用上下两行。每条字幕只解释一个动作，不同时放长段营销说明。最终需在缩小播放器上检查可读性，并逐镜调整字幕位置和时间。
