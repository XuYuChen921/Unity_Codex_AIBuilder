# Compatibility and validation

Candidate: Scene Story 1.0.0-rc3, 16 September 2026. Windows, Built-in renderer.

| Environment | Timeline | Final test result |
|---|---|---|
| Unity 2022.3.62f3 | 1.7.7 | 82/82 passed |
| Unity 6000.3.21f1 | 1.7.7 | 82/82 passed |

Tests cover route/ground behavior, editing, actor bindings, scene sampling, language switching and an actual EnterPlayMode animation/audio scheduling smoke test. A regression verifies that destroyed route anchors do not throw when selecting unrelated scene objects. Test files are excluded from the product archive.

The Unity 2022 Windows development player built with 0 errors and 0 warnings. Its runtime probe observed forward actor motion, an advancing Director and scheduled audio, with 0 logged errors. This is not a human listening test or XR/device acceptance. The last change after this build was editor-only null-reference handling plus documentation; runtime code was unchanged.

Asset Store Tools 12.0.0 (Unity's official public GitHub source): 34 passed, 0 warnings, 0 failures. The tool UI reported a newer version, 12.0.5. Recheck with the current publishing tools when signing in to submit; local validation does not mean store approval.

Final package assets/meta match the release host. Unity 2022 retains an already-existing root folder meta but all 114 child asset/meta payloads match. Unity 6 also reserializes the three StoreExamples scenes to its engine format; all scripts, remaining assets and child GUID metadata match. Scene binding/playback tests pass. No existing RC2 asset GUID was changed or removed.

The first concurrent Unity 6 test run had one audio scheduling failure. Its isolated replay and both later full runs passed. Final runs are sequential to avoid editor/audio contention; the cause of that initial failure is not asserted as proven.

URP/HDRP, other Unity versions, macOS/Linux, mobile, XR and Tuanjie are not validated. Saved samples use Standard materials. Generic workflows are the advertised animation scope; Humanoid retargeting is not certified. Root-motion gait calibration is required. Multiple stacked walkable levels inside one MeshCollider and ground/vegetation merged into one mesh may need separate surfaces.

English narrow and wide windows were visually inspected in the actual editor. Legacy component display names and some authored example names remain Chinese; runtime tooltips/diagnostics can be bilingual. No automatic renaming of user content occurs.

The static support site's local links were checked. The browser tool blocked local-file preview, so desktop/mobile browser visual acceptance is pending. The site is not hosted.
