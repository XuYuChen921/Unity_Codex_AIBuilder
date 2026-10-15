# Scene Story: installation and workflow

## 1. Install
Use a backup or a clean project. Install Timeline from Window > Package Manager > Unity Registry. Import the entire unitypackage, including the Editor scripts and Resources. The installation root is Assets/SceneStory. Do not combine it with the old com.xuyuchen.scene-story UPM package or another AIBuilder installation containing the same types.

Open Tools > Scene Story > Open Story Editor. Choose English or 简体中文 in the header. The language switch changes labels and newly created names; it does not rename existing scene objects.

## 2. Try a sample
Open Samples/StoreExamples/MultiActor.unity to see three actors on one Director. GroundLayers demonstrates ground projection. AudioAndFacing contains a generated test tone and a turn-and-return heading curve. BasicExample/BasicStory.unity also demonstrates scale and free-space movement.

For a sample created with the current render pipeline, use Tools > Scene Story > Create Basic Example and save into a new folder. Saved store examples use Built-in Standard materials; do not assume they are ready for URP/HDRP without conversion.

## 3. Create your story
Select an individual scene model in Hierarchy. Choose Tools > Scene Story > Create Story for Selection and save the Timeline outside Samples. To add another actor, select the model and click + Actor in the story window. Use the actor dropdown to switch tracks and the color chip to distinguish routes.

Select the outermost Prefab root when adding a Prefab instance. Avoid scene roots that contain other actors or Directors. The parent world scale must be one. An object with multiple Animators must be narrowed to one actor model.

## 4. Arrange clips
Select a clip and click + Hold or + Move to insert after it; an empty track starts with its first clip. Drag row handles to reorder. Route starts reconnect in Timeline order. A hold can follow the preceding endpoint. Disable following or auto-connect for independent placement.

The list and properties scroll separately. The selected clip header remains visible. Expand property groups to edit timing, placement, movement speed, facing and gait. Ripple later clips determines whether a duration edit shifts subsequent clips.

## 5. Edit paths and ground
Use Locate, select the route in Scene view and move the route points. A connected start is controlled by the preceding endpoint. Select a route point to switch the active actor track. Turn off ground following to edit world-space heights for flying motion.

Open Ground binding. Choose Static meshes and optionally an environment root. Set excluded keywords or excluded parent objects for vegetation. Add stone or slope roots to the include list when needed; explicit exclusions take priority. Scan and inspect the results before Apply. Applying reuses MeshColliders or adds missing colliders; Undo is supported.

Adjust allowed rise, drop and maximum slope. Keep route points near the intended walking level. Ground and vegetation in one combined mesh need separate walking surfaces. Ground transitions blend from the preceding segment height over the configured time.

## 6. Animation and heading
Use the linked native Timeline animation track for animation clips. Overlap animation clips in that track to blend. Movement clips themselves must not overlap. Facing transition time controls the turn from the preceding clip. Enable animated facing and edit the curve in seconds/degrees for turn, hold and return within one clip; the 90-degree preset provides a starting point.

Distance-synchronized gait is for cyclic walking/running. Calibrate the metres travelled in one animation cycle before enabling it. It overrides manual animation speed; holds pause gait phase. It is not automatic foot locking.

## 7. Preview and sound
Use the editor Preview button or native Timeline Preview separately. Stop preview to restore the actor before structural edits. The colored overview selects clips on click and scrubs time when dragged. In Play mode, StageStoryPlayback advances the Director with its native clock.

For audio, bind a Timeline AudioTrack to an enabled AudioSource and keep an enabled AudioListener in the scene. Check Game-view Mute Audio, AudioSource volume, clip timing and mixer settings. The example test tone is intentionally quiet.

## 8. Save and upgrade
Save both scene and Timeline after changes. Keep your production assets outside Samples. Import future packages over Assets/SceneStory with all files selected. unitypackage imports do not remove obsolete files; follow release notes if a future version supplies migration steps.

## Troubleshooting
- Duplicate types: remove the duplicate installation after backing up and checking bindings; do not keep both UPM and Assets copies.
- StageLanguage missing: reimport the complete package; do not import only StageStoryWindow.cs.
- Empty track cannot be edited: exit Play/Timeline Preview and check track locking.
- Route jumps to another level: move points close to the intended surface, review rise/drop limits and split stacked walking geometry if necessary.
- Missing ground: scan again after changing scene filters, check colliders and increase limits only as needed.
- Floating/sliding feet: calibrate cycle distance and use suitable animation. This tool does not supply foot IK.

Support: chrowder7@163.com. Include package version, Unity version, Timeline version, render pipeline, exact Console stack trace and reproduction steps. Send a small reproduction without confidential project content when possible.
