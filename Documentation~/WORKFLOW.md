# AIBuilder Workflow

## 1. Build The Asset Index

Use Unity menu:

```text
AIBuilder/Prefab Asset Library
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
AIBuilder/Scene From Reference
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

