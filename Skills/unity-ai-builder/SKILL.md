---
name: unity-ai-builder
description: Use when working with Unity Codex AIBuilder to generate Unity scenes from reference images, scan prefab libraries, visually select matching assets, update SceneSpec JSON, and use procedural or ProBuilder fallback when prefabs do not match.
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

## Asset Index Workflow

1. Run Unity menu `AIBuilder/Prefab Asset Library`.
2. Use `Build Fast Metadata Missing / Changed` after importing assets.
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
6. Generate through `AIBuilder/Scene From Reference`.
7. Review main camera plus side/top spatial validity.

## Important Files

- `Assets/AIBuilder/Editor/AIReferenceSceneGenerator.cs`
- `Assets/AIBuilder/Editor/PrefabAssetLibraryWindow.cs`
- `Assets/AIBuilder/Editor/AssetCatalogWindow.cs`
- `Assets/AIBuilder/Runtime/SceneSpec.cs`
- `docs/WORKFLOW.md`
- `docs/MCP_VS_SKILL.md`

