# Unity Codex AIBuilder

Unity Codex AIBuilder is a standalone Unity Package Manager package for reference-image-driven scene generation workflows with Codex.

It provides Unity Editor tools for:

- Building a fast prefab metadata index without screenshots.
- Rendering selected or project-wide prefabs into front/side/top visual references.
- Creating labeled asset catalog views from scene objects.
- Generating editable `SceneSpec` JSON.
- Generating stylized reference scenes from `SceneSpec`.
- Capturing the workflow as a Codex Skill.

## Install

In Unity `2022.3`, open:

```text
Window/Package Manager/Add package from git URL
```

Use:

```text
https://github.com/XuYuChen921/Unity_Codex_AIBuilder.git
```

## Requirements

- Unity `2022.3.x`
- URP `14.x`
- TextMeshPro `3.x`
- ProBuilder `5.x`

These dependencies are declared in `package.json`.

## Menus

```text
AIBuilder/Scene From Reference
AIBuilder/Generate Forest Heart Clearing
AIBuilder/Asset Catalog Capture
AIBuilder/Prefab Asset Library
```

## Recommended Workflow

1. Import your own art assets into the Unity project.
2. Run `AIBuilder/Prefab Asset Library`.
3. Click `Build Fast Metadata Missing / Changed`.
4. Give Codex a reference image.
5. Codex analyzes the reference world and reads the fast metadata index.
6. Codex shortlists candidate prefabs by type, style, shape, and approximate color.
7. Generate or read front/side/top screenshots for shortlisted prefabs.
8. Codex performs final visual comparison.
9. Codex updates or creates `SceneSpec` JSON.
10. Generate the Unity scene.

## Final Asset Selection Rule

Metadata is only for coarse filtering.

Final prefab selection must use visual comparison against candidate screenshots. Selection priority:

1. Style and visual language.
2. Silhouette and external appearance.
3. Semantic role.
4. Color family.
5. Size.

Color and size can be adjusted. A visually wrong prefab should not be selected just because the name or metadata matches.

## Reference Image Rule

Reference images must be understood as 3D worlds. Analyze:

- Theme and atmosphere.
- Foreground, midground, background, and boundaries.
- Camera height, lens feel, and view direction.
- Light direction, color temperature, glow, fog, and shadows.
- Object roles and placement rhythm.
- Front/side/top layout, not only the main 2D frame.

## Output Locations

The tools write generated local project data under:

```text
Assets/AIBuilder/Generated
Assets/AIBuilder/Data
Assets/AIBuilder/Scenes
Assets/AIBuilder/Materials
```

These are intentionally project-local outputs and are not part of the package source.

## MCP vs Skill

This package is not currently an MCP server. It is a Unity Editor toolkit plus a Codex Skill workflow.

See:

```text
Documentation~/MCP_VS_SKILL.md
Documentation~/WORKFLOW.md
Skills/unity-ai-builder/SKILL.md
```

