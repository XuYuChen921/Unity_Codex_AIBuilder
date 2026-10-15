# 服装顶点色收缩工具

这个工具用于小幅修正 AI 生成角色过于臃肿的衣服和裤子。它把顶点色 RGB 灰度当成收缩权重：黑色不动，白色按完整强度收缩，中间灰度线性过渡。

## 使用

1. 在场景中选中衣服对象上的 `SkinnedMeshRenderer` 或 `MeshRenderer`。
2. 打开 `Tools/AIBuilder/服装收缩`。
3. 点击“创建可编辑 Mesh 副本”。副本写入 `Assets/AIBuilderGenerated/ClothingShrink`，原 FBX 不会被覆盖。
4. 复制衣服原材质，把复制材质的 Shader 改为 `AIBuilder/URP/Vertex Color Clothing Shrink`，再检查 Base Map、Normal Map、Metallic 和 Smoothness。
5. 点击“添加收缩参数组件”。
6. 点击“在 Scene 视图绘制”，左键刷入当前灰度，`Shift + 左键`擦除。
7. 每次按下到松开左键算一次笔画，工具会保存精确顶点色快照，可用 `Ctrl+Z` 整次撤销，`Ctrl+Y` 重做。
8. 把“显示蒙版”调到 `1` 检查权重；调回 `0` 查看最终材质。

如果 FBX 没有开启 Read/Write，工具会停止。请在该 FBX 的 `Import Settings > Model > Read/Write` 中手动开启，然后再创建副本。

## 两种收缩方向

- `Surface Normal`：沿变形后的表面法线向内移动。适合大多数蒙皮衣服，也是默认方式。
- `Local Axis`：每个顶点朝一条局部轴线移动。适合躯干、袖管和裤腿，可减少褶皱法线造成的凹凸放大。默认轴线是局部 Y 轴，中心使用模型局部坐标。

建议从 `0.005–0.02` 模型单位开始，逐步增加。局部缩放不为 1 时，实际世界空间位移会随缩放改变。

## 边界

- Shader 只改变渲染顶点，不会改变碰撞体、布料碰撞、NavMesh 或身体网格。
- 阴影、深度和 DepthNormals Pass 使用同样位移；材质只实现 URP 不透明/Alpha Clip 的常用 Lit 参数，不等同于任意原始自定义 Shader。
- 双层衣料、反向法线和大幅收缩容易互相穿插。超过约 2–3 厘米的体型修正，优先在 Blender 用 Shrinkwrap/Cage，或制作 BlendShape，再回 Unity 微调。
- Scene 画笔使用当前未收缩的 CPU 网格做命中。收缩量很大时先把强度临时调低再绘制。
