# AIBuilder Workflow

## 1. Build The Asset Index

Use Unity menu:

```text
AI构建器/Prefab资产库/打开资产库面板
```

Start with:

```text
Build Fast Metadata Missing / Changed
```

This creates a fast local index at:

```text
Assets/AIBuilder/Generated/PrefabAssetMetadataLibrary
```

The fast index is used for coarse filtering by:

- Type.
- Style.
- Shape.
- Bounds size.
- Material color.
- Texture color when readable.
- Semantic name/path hints.

## 2. Analyze The Reference Image

The reference image must be interpreted as a 3D world, not a flat picture.

Analyze:

- World theme and mood.
- Main subject and secondary elements.
- Foreground, midground, background, and boundaries.
- Camera height, lens feeling, and viewing direction.
- Light direction, color temperature, fog, glow, and shadow softness.
- Object density and placement rhythm.
- What the scene should look like from front, side, and top.

## 3. Coarse Filter Candidate Assets

Read the fast metadata index and shortlist candidates by:

- Required object role.
- Visual style.
- Silhouette category.
- Approximate color family.
- Size ratio.

This step is only for reducing the search space.

## 4. Visual Candidate Comparison

Final prefab choices must use visual comparison.

Use an existing screenshot library or run:

```text
Build Missing / Changed Library
```

This writes:

```text
Assets/AIBuilder/Generated/PrefabAssetLibrary
```

Compare candidate `front.png`, `side.png`, and `top.png` against the reference image.

Priority:

1. Style match.
2. External appearance and silhouette.
3. Object role.
4. Color.
5. Size.

Do not pick a prefab purely because its name or metadata matches.

## 5. Generate Or Update SceneSpec

Edit or create a `SceneSpec` JSON under:

```text
Assets/AIBuilder/Data
```

The spec should encode:

- Theme.
- Camera and light.
- Terrain size and color.
- Central feature.
- Scatter groups.
- Prefab usage where visually validated.
- Procedural fallback where no close prefab exists.

## 6. Generate The Scene

Use:

```text
AI构建器/场景生成/参考图生成场景
```

or call the editor generation method in batchmode.

Review:

- Main camera similarity.
- 3D layout validity.
- Side/top spatial arrangement.
- Asset scale.
- Density and silhouette readability.
- Lighting and atmosphere.

## 7. Iterate

Common iteration points:

- Replace visually weak prefabs.
- Adjust scale and rotation.
- Rebalance foreground/midground/background.
- Add procedural filler meshes.
- Improve lighting and fog.
- Regenerate candidate screenshots if new assets were imported.

## 8. Build A ProBuilder Model From Model References

Use this when the missing asset is a single model, not a full scene.

1. Import one or more model reference images into Unity.
2. If the images are inside the project, select them and run:

```text
AI构建器/模型生成/选中图片生成模型配置
```

3. Send the image(s) to Codex.
4. Codex analyzes front, side, top, and hidden-depth assumptions from the available views.
5. Codex creates or updates a `ProBuilderModelSpec` JSON under:

```text
Assets/AIBuilder/Data
```

6. Run:

```text
AI构建器/模型生成/参考图生成模型 ProBuilder
```

7. Generate the model and refine the created ProBuilder parts.

The generator creates editable ProBuilder primitives, not a final sculpt. Use it for blockout, stylized low-poly construction, layout replacement assets, and rapid iteration. Single-view references are valid but require conservative inference for depth and hidden sides; multi-view references are preferred for accurate silhouettes.
