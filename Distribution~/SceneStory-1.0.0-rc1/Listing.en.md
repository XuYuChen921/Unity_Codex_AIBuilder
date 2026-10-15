# Scene Story — Actor Routes for Timeline

Create multi-actor sequences with editable scene routes and native Unity Timeline tracks.

Select a scene object, add it as an actor, and build a sequence from Hold and Move clips. Edit route points directly in Scene view while keeping movement, animation and audio on one Timeline.

## Features

- Add scene objects as actors in the current story.
- Insert clips after the current selection and reorder connected routes.
- Edit curved routes in Scene view and choose a route color per actor.
- Switch the active actor track by selecting its scene route point.
- Scan static meshes for ground, with explicit inclusion and vegetation exclusions.
- Blend grounded/free-space heights and animate facing offsets within clips.
- Control movement and animation speed; optionally synchronize gait to calibrated travel distance.
- Integrate native Timeline animation and audio tracks.
- English and Chinese main authoring controls; primitive-based examples and quick-start guides.

## Requirements and limits

Requires Unity Timeline. See the release validation report for exact tested editor/package versions. Do not install alongside the UPM variant or an AIBuilder package containing the same Scene Story classes.

This is authored route playback, not automatic pathfinding, obstacle avoidance or a full dialogue/camera system. Terrain foot locking and automatic animation retargeting are not included. Multiple overlapping walkable levels inside one combined MeshCollider require separate walking surfaces for reliable selection. Vegetation filtering works at object/mesh level, not at triangle level within a combined mesh.

Release-candidate note: some diagnostics, component labels and legacy/example object names remain Chinese. Remove this note only after completing the remaining localization. Tuanjie, XR hardware and untested platforms/render pipelines must not be advertised as verified.

## AI description draft

OpenAI Codex assisted with C# implementation and editor interface development. The tool was iteratively revised against scene-authoring requirements, with automated tests for Timeline behavior, route continuity and ground selection. Validation details are supplied separately. This disclosure should be reviewed by the publisher before submission.

## Publisher fields — required before submission

Publisher name: GINDXRSTU.
Support email: chrowder7@163.com. Website: pending owner input.
Screenshots/video: pending final capture and recording.
