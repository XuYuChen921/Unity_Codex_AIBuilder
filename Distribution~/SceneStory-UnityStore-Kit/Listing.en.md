# Scene Story - Actor Routes for Timeline

## Summary
Build multi-actor sequences with editable scene routes, Hold and Move clips, and native Timeline animation and audio tracks.

## Description
Scene Story helps you place actors, edit their movement through a scene and arrange the sequence on one Timeline. Select a scene object, add it as an actor, then insert Hold or Move clips. Drag route points in Scene view and preview the result while editing.

Use it for authored character staging, interactive exhibits and scripted scene sequences. It provides a route-editing workflow alongside Unity Timeline, with per-actor route colors and a compact sequence editor.

### Included features
- Add scene objects as actors in the current Director.
- Insert after the selected clip; reorder clips while reconnecting route starts.
- Edit straight or curved paths and select the active actor by clicking a route point.
- Follow ground, slopes and steps, or use free-space routes.
- Scan static meshes and exclude vegetation by object roots or name keywords.
- Blend ground height and facing between clips. Key facing offsets within a clip.
- Adjust movement speed curves independently from animation speed.
- Synchronize looping walk/run animations with calibrated travel distance.
- Use Timeline animation and audio tracks alongside movement.
- English and Simplified Chinese authoring interface, offline guide and primitive-based examples.

### Included examples
BasicStory: holding, movement, scale animation, turning and free-space movement.
MultiActor: three actors scheduled on one Timeline.
GroundLayers: a step, a slope and an overhead collider.
AudioAndFacing: animated heading offsets and a generated audio test tone.

### Requirements
Unity 2022.3 or later and Unity Timeline. Consult Compatibility.md for exact tested versions. Install com.unity.timeline through Package Manager before importing. No external online service, API key or AI service is needed to use this tool.

### Scope and limitations
Movement follows authored routes. Automatic pathfinding, obstacle avoidance, dialogue, camera direction, foot IK and automatic animation retargeting are not included. Embedded clip animation workflows are validated with Generic rigs; Humanoid retargeting is not advertised as tested. Distance synchronization requires a calibrated distance per animation cycle.

Vegetation filtering operates on objects/meshes. Grass and walkable ground merged into a single mesh need separate walking surfaces. Multiple overlapping walkable levels inside one combined MeshCollider are not guaranteed to select the desired surface.

Some runtime diagnostics and component tooltips are bilingual English/Chinese. Existing scene and clip names are not renamed when switching language. The included saved BasicStory example retains some original Chinese object names.

Do not install alongside a UPM or AIBuilder installation containing the same Scene Story classes. Update by importing over Assets/SceneStory with matching GUIDs. Keep your own scenes and assets outside the supplied Samples folder.

No third-party character models, textures or music are included. Screenshots and videos show included primitive-based examples. Unity Timeline is a separate Unity Registry dependency; see Third-Party Notices.txt.

Publisher: GINDXRSTU
Support: chrowder7@163.com
