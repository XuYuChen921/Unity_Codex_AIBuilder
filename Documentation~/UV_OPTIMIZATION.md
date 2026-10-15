# UV优化

从 Unity 菜单 `Tools > AIBuilder > UV优化` 打开工具。

适用范围：单个主蒙皮网格、已有骨骼权重和 FBX 动画的人形角色。工具会调用 Blender 后台完成以下工作：

1. 保留原 UV，按头、躯干、手臂、腿部和断开细节重新划分接缝。
2. 使用新的 UV 重烘焙 BaseColor，并用推拉金字塔填满透明空白，避免远距离 Mipmap 从 UV 岛外采到杂色。
3. 导出独立的 `_UVOptimized.fbx`、PNG 和 Unity 材质，不修改原 FBX、贴图、材质或场景。输出 FBX 会继承源 FBX 的 Unity Import Settings。
4. 将导出的 FBX 重新导入 Blender，校验顶点/面/面角数量、骨骼名、顶点组、权重、动画帧范围、姿态、变形包围盒和法线。

安全限制：检测到形态键或多个主要蒙皮网格时默认停止。此版本只处理 BaseColor；法线、金属度等贴图需要后续扩展为多通道烘焙。

默认参数为 4096 贴图和 64 像素烘焙扩边。输出贴图在 Unity 中自动开启 Mipmap、Clamp、4 倍各向异性和高质量压缩。
