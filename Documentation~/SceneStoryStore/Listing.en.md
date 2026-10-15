# Scene Story - Actor Routes for Timeline

## Summary
Stage multiple actors on one Unity Timeline. Edit routes, revise Hold and Move clips, control facing, and sync calibrated walk/run animations to travel distance.

## Description
Plan the performance. Revise the blocking.

Scene Story is a Unity Timeline tool for multi-actor staging and editable character routes. Arrange where actors move, when they wait, and how they turn in one sequence. Edit paths directly in Scene view and scrub the Timeline to inspect a moment without replaying the whole scene.

When the scene changes, revise the sequence where you work: insert a Hold or Move after the selected clip, reorder route clips, and keep automatically connected route starts following the preceding endpoint. Use ripple timing when changing a clip's duration to shift subsequent route clips on that actor's track. Independent animation, audio and other actor tracks remain separately authored.

### Control the performance
- **Stage several actors together.** Add scene objects to the current story and coordinate their timing on a shared Timeline.
- **Revise connected routes.** Edit straight or curved paths, insert clips and rearrange movement without manually reconnecting each automatic route start.
- **Direct facing over time.** Set heading offsets within a clip for a turn, a hold and a return; adjust the transition into the next clip. Facing controls the actor's overall orientation.
- **Link gait to distance.** After calibrating distance per animation cycle, synchronize looping walk/run playback to traveled distance, including movement-speed changes. Calibration and suitable animation are required; foot locking is not included.
- **Work on ground or in free space.** Bind walkable static meshes, exclude vegetation objects, and adjust grounded/free-space height transitions.
- **Find the right actor quickly.** Use per-actor route colors and select a route point to switch the active actor track.
- **Keep native Timeline workflows.** Combine authored movement with Unity's animation and audio tracks.

### Where it helps
Use Scene Story for cutscene blocking, narrative scene previews, scripted character performances and fixed exhibit sequences. Its focus is repeated authoring and revision of timed movement, rather than autonomous NPC decisions.

### Technical details
Requires Unity Timeline. Consult the release's compatibility and validation reports for exact tested Unity versions, rigs and render pipelines. Main authoring controls support English and Simplified Chinese. The release includes primitive-based examples and quick-start documentation. No external AI service or API key is needed to use the tool.

### Scope
Automatic pathfinding, dynamic avoidance, dialogue, camera direction, head-look IK, foot locking and automatic animation retargeting are not included. Vegetation filtering works at object/mesh level. Combined meshes containing overlapping walkable floors or both vegetation and ground may need separate walking surfaces. Do not install alongside another package containing the same Scene Story classes.

Promotional humanoid characters are demonstration assets and are not included in the Scene Story package. Camera shots used in promotional videos are demo-specific. The final compatibility table takes precedence over promotional examples.

### Support
GINDXRSTU · chrowder7@163.com · https://www.gindxrstu.com/
