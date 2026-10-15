# Changelog

## 0.6.0

- Added structured scene decomposition with normalized screen polygons, depth bands, occlusion relations, landmarks, and human-scale metadata.
- Added editable `screen_polygon` ProBuilder meshes that are back-projected from the locked reference camera instead of guessed in world space.
- Added exact-aspect reference, overlay, difference, and projection-error outputs, plus an in-window overlay preview.

## 0.5.0

- Reduced the public workflow to scene blockout and object blockout only.
- Added one unified Chinese editor window that automatically detects the latest Codex task.
- Unified both task types on editable ProBuilder geometry and automatic main, side, and top review captures.
- Added an optional reference camera so scene previews can retain the composition inferred from the source image.
- Added a temporary review light and capture warm-up so ProBuilder previews remain readable and free of selection overlays.
- Invalidated stale previews when Codex updates a task in place.
- Kept preview asset-free and added explicit, non-overwriting scene or Prefab commit actions.
- Hid legacy scene, prefab-library, and asset-management menus while retaining their code for compatibility.

## 0.4.0

- Added formal JSON Schemas for `SceneSpec` v2 and `ProBuilderModelSpec` v2.
- Added unique Codex review bundles under `Library/AIBuilder/Reviews`.
- Added automatic main, side, and top scene captures without saving the preview scene.
- Added aggregated reporting for exact prefab matches, keyword fallback, procedural fallback, skipped placements, and unresolved prefab references.
- Added editor coverage for execution-report aggregation and review-bundle output.

## 0.3.0

- Added model-agnostic `SceneSpec` v2 with exact prefab GUID/path placement and explicit hierarchy.
- Added strict scene/model validation before any scene or asset mutation.
- Changed scene and ProBuilder generation to preview-only by default; saving and overwriting now require explicit actions.
- Namespaced generated materials and profiles to avoid accidental cross-scene overwrites.
- Removed fixed forest behavior from clean templates while preserving legacy scatter/procedural fallback.
- Added editor tests and Unity package metadata files.

## 0.2.2

- Renamed Unity menus to clear Chinese task-based labels.
- Removed visible default forest scene presets and bundled forest default specs.
- Made scene creation use a clean template with optional scene feature toggles disabled by default.

## 0.2.1

- Added the missing `Unity.RenderPipelines.Core.Runtime` assembly reference required by URP `Volume` and `VolumeProfile` types.

## 0.2.0

- Added `ProBuilderModelSpec` for Codex-authored model generation from one or more reference images.
- Added `AIBuilder/ProBuilder Model From Reference`.
- Added template creation from selected model reference images.
- Added a default stylized crate ProBuilder model spec.
- Updated documentation and the Codex Skill workflow for model-reference-to-ProBuilder generation.

## 0.1.0

- Added Unity Editor scene generation from `SceneSpec`.
- Added asset catalog layout and multi-camera capture.
- Added prefab screenshot library builder.
- Added fast prefab metadata-only index builder.
- Added Codex Skill workflow.
- Added workflow and MCP-vs-Skill documentation.
