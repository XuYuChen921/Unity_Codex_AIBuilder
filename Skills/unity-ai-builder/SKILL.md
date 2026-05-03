---
name: unity-ai-builder
description: Use when working with Unity Codex AIBuilder to generate Unity scenes from reference images, generate ProBuilder models from model reference images, scan prefab libraries, visually select matching assets, update SceneSpec/ProBuilderModelSpec JSON, and use procedural or ProBuilder fallback when prefabs do not match.
---

# Unity AIBuilder

Use this workflow for Unity 2022.3 URP AIBuilder projects.

## Core Rules

- Treat the reference image as a 3D world, not a flat collage.
- Final prefab selection must use visual comparison against prefab screenshots.
- Use fast metadata only for coarse filtering.
- Prioritize style and external appearance over color and size.
- Color, scale, rotation, and density can be adjusted after selection.
- If no prefab visually matches, use procedural or ProBuilder fallback.
- For model references, Codex performs image understanding and writes `ProBuilderModelSpec`; Unity generates editable ProBuilder parts from the spec.

## Asset Index Workflow

1. Run Unity menu `AI构建器/Prefab资产库/打开资产库面板`.
2. Use `快速扫描缺失或变化资产` after importing assets.
3. Read `Assets/AIBuilder/Generated/PrefabAssetMetadataLibrary/PrefabAssetLibrary_Index.md` and the relevant `metadata.json` files.
4. Shortlist candidates by object role, style, shape, and approximate color.
5. For final selection, use existing `PrefabAssetLibrary` screenshots or request/run `Build Missing / Changed Library` for shortlisted candidates.

## Reference Image Analysis

Before editing any spec, identify:

- World theme and mood.
- Main subject and secondary object roles.
- Foreground, midground, background, and boundary objects.
- Camera height, perspective, and focal direction.
- Light direction, color temperature, fog, glow, and shadow softness.
- Placement logic from front, side, and top views.

## Prefab Selection Priority

Use this order:

1. Style and visual language.
2. Silhouette and external appearance.
3. Semantic role.
4. Color family.
5. Size.

Reject visually wrong prefabs even if their metadata or name matches.

## Scene Generation Workflow

1. Read the existing `SceneSpec` model in `Assets/AIBuilder/Runtime/SceneSpec.cs`.
2. Create or update JSON specs under `Assets/AIBuilder/Data`.
3. Encode camera, lighting, terrain, central features, and scatter groups.
4. Use validated prefab paths when available.
5. Use procedural fallback where prefab candidates do not visually match.
6. Generate through `AI构建器/场景生成/参考图生成场景`.
7. Review main camera plus side/top spatial validity.

## Model Reference To ProBuilder Workflow

Use this when the user provides one or more model reference images and wants a model built in Unity with ProBuilder.

1. Inspect every supplied view: single-view, single-image multi-view, or multiple images.
2. Identify silhouette, proportions, symmetry, large forms, secondary forms, surface regions, material colors, and style.
3. Mark assumptions for hidden sides and depth. Do not invent high-confidence details that the reference does not show.
4. Read `Assets/AIBuilder/Runtime/ProBuilderModelSpec.cs`.
5. Create or update `Assets/AIBuilder/Data/<ModelName>.json` using `ProBuilderModelSpec`.
6. Split the model into named editable parts using `box`, `cylinder`, `cone`, `sphere`, `plane`, `prism/wedge/roof`, `pipe/ring`, `torus`, `arch`, or `stair`.
7. Generate through `AI构建器/模型生成/参考图生成模型 ProBuilder`.
8. Review the generated model from front, side, and top; adjust the spec if the silhouette does not match.

## Important Files

- `Assets/AIBuilder/Editor/AIReferenceSceneGenerator.cs`
- `Assets/AIBuilder/Editor/AIProBuilderModelGenerator.cs`
- `Assets/AIBuilder/Editor/PrefabAssetLibraryWindow.cs`
- `Assets/AIBuilder/Editor/AssetCatalogWindow.cs`
- `Assets/AIBuilder/Runtime/SceneSpec.cs`
- `Assets/AIBuilder/Runtime/ProBuilderModelSpec.cs`
- `Documentation~/WORKFLOW.md`
- `Documentation~/MCP_VS_SKILL.md`
