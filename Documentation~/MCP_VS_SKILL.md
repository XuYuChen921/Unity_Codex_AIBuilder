# MCP vs Skill Decision

## Decision

This project should currently be shipped as:

- A Unity editor toolkit.
- A Codex Skill that documents the scene-building workflow.

It should not currently be shipped as an MCP server.

## Why Not MCP Yet

MCP is appropriate when an AI client needs callable tools exposed by a running service, for example:

- Query Unity assets through a live tool call.
- Trigger Unity scene generation from Codex without manual editor interaction.
- Request prefab screenshots or metadata through a protocol boundary.
- Inspect open Unity scenes interactively.

The current project does not run a persistent Unity-side server. Its functions are Unity Editor menu tools and C# editor scripts. They are useful and functional, but they are not exposed as external callable MCP tools.

## Why A Skill Fits

A Skill is appropriate because the critical value is procedural:

- How to analyze a reference image.
- How to coarse-filter prefabs using metadata.
- When to generate screenshots.
- How to perform final visual selection.
- How to update `SceneSpec` and regenerate a Unity scene.
- When to fall back to ProBuilder/procedural geometry.

Those are workflow rules that Codex should follow, not a network protocol.

## Future MCP Path

This could become an MCP later if we add a Unity bridge process that exposes tools such as:

- `scan_prefabs`
- `build_fast_metadata`
- `render_prefab_candidates`
- `query_asset_library`
- `generate_scene_from_spec`
- `capture_scene_views`

That would let Codex operate Unity more directly. Until then, the standard packaging is Unity editor tooling plus a Codex Skill.

## Maturity Assessment

Current functionality is good enough for a V1 workflow:

- Editable scene spec data model.
- Reference-scene generator.
- Project asset matching.
- Asset catalog layout and labeled multi-view capture.
- Project-wide prefab screenshot library.
- Fast metadata-only prefab indexing.
- Incremental detection with GUID and dependency hash.
- Documentation and skill workflow.

Known limitations:

- No live Unity MCP server yet.
- No embedded vision model inside Unity.
- Metadata-only matching is approximate and cannot replace visual comparison.
- Procedural/ProBuilder fallback is still basic and should be expanded by scene type.
- Large full-project screenshot builds can take time.

