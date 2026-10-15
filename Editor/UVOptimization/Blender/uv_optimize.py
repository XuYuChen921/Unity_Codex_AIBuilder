import argparse
import hashlib
import json
import math
import os
import sys
import traceback

import bpy
import numpy as np


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPT_DIR)
import uv_layout


def stage(message):
    print("AIBUILDER_UV_STAGE=" + message, flush=True)


def load_config():
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", required=True)
    parsed = parser.parse_args(arguments)
    with open(parsed.config, "r", encoding="utf-8") as config_file:
        config = json.load(config_file)
    config["config_path"] = parsed.config
    return config


def install_fbx_armature_repair():
    """兼容少量 Tripo FBX 在 Blender 5.x 导入器中的骨架挂接异常。"""
    from io_scene_fbx import import_fbx

    original = import_fbx.FbxImportHelperNode.link_hierarchy
    if getattr(original, "_aibuilder_uv_repair", False):
        return

    def repaired(self, *args, **kwargs):
        try:
            return original(self, *args, **kwargs)
        except KeyError:
            repaired_any = False
            for mesh_node in self.meshes:
                setup = mesh_node.armature_setup
                if self not in setup and None in setup:
                    setup[self] = setup[None]
                    repaired_any = True
            if not repaired_any:
                raise
            return original(self, *args, **kwargs)

    repaired._aibuilder_uv_repair = True
    import_fbx.FbxImportHelperNode.link_hierarchy = repaired


def clear_scene():
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def select_character(mesh_object_name):
    candidates = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and obj.find_armature() is not None
    ]
    if mesh_object_name:
        selected = bpy.data.objects.get(mesh_object_name)
        if selected is None or selected.type != "MESH":
            raise RuntimeError(f"找不到指定网格对象: {mesh_object_name}")
        if selected.find_armature() is None:
            raise RuntimeError(f"指定网格没有骨架: {mesh_object_name}")
        return selected, candidates
    if not candidates:
        raise RuntimeError("FBX 中没有找到带骨架的蒙皮网格")
    candidates.sort(key=lambda item: len(item.data.polygons), reverse=True)
    if len(candidates) > 1 and len(candidates[1].data.polygons) >= len(candidates[0].data.polygons) * 0.25:
        names = ", ".join(item.name for item in candidates)
        raise RuntimeError(f"检测到多个主要蒙皮网格，请在高级设置中指定目标网格: {names}")
    return candidates[0], candidates


def weight_snapshot(obj):
    group_names = [group.name for group in obj.vertex_groups]
    snapshot = []
    for vertex in obj.data.vertices:
        snapshot.append(
            tuple(
                sorted(
                    (group_names[item.group], round(float(item.weight), 8))
                    for item in vertex.groups
                )
            )
        )
    return tuple(snapshot)


def snapshot_digest(snapshot):
    return hashlib.sha256(repr(snapshot).encode("utf-8")).hexdigest()


def action_ranges():
    return {action.name: list(action.frame_range) for action in bpy.data.actions}


def sample_frames(ranges):
    if not ranges:
        return [int(bpy.context.scene.frame_current)]
    starts = [value[0] for value in ranges.values()]
    ends = [value[1] for value in ranges.values()]
    start = int(round(min(starts)))
    end = int(round(max(ends)))
    return sorted({start, int(round((start + end) * 0.5)), end})


def evaluated_bounds(obj, frame):
    bpy.context.scene.frame_set(frame)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        coordinates = np.asarray(
            [evaluated.matrix_world @ vertex.co for vertex in mesh.vertices],
            dtype=np.float64,
        )
        return {"min": coordinates.min(axis=0), "max": coordinates.max(axis=0)}
    finally:
        evaluated.to_mesh_clear()


def pose_matrices(armature, frame):
    bpy.context.scene.frame_set(frame)
    return {
        bone.name: np.asarray(bone.matrix, dtype=np.float64).copy()
        for bone in armature.pose.bones
    }


def corner_normals_by_vertex(mesh):
    result = [[] for _ in mesh.vertices]
    for loop_index, loop in enumerate(mesh.loops):
        result[loop.vertex_index].append(
            np.asarray(mesh.corner_normals[loop_index].vector, dtype=np.float64).copy()
        )
    return result


def capture_character(character, frames):
    armature = character.find_armature()
    if armature is None:
        raise RuntimeError("目标网格没有骨架")
    mesh = character.data
    weights = weight_snapshot(character)
    uv_layer = mesh.uv_layers.active
    uv_values = [item.uv[:] for item in uv_layer.data] if uv_layer else []
    return {
        "character_name": character.name,
        "armature_name": armature.name,
        "vertices": len(mesh.vertices),
        "polygons": len(mesh.polygons),
        "loops": len(mesh.loops),
        "uv_layers": [layer.name for layer in mesh.uv_layers],
        "uv_islands": uv_layout.uv_island_count(mesh, uv_layer) if uv_layer else 0,
        "uv_min": [min(value[index] for value in uv_values) for index in (0, 1)] if uv_values else [],
        "uv_max": [max(value[index] for value in uv_values) for index in (0, 1)] if uv_values else [],
        "bones": sorted(bone.name for bone in armature.data.bones),
        "vertex_groups": sorted(group.name for group in character.vertex_groups),
        "weighted_vertices": sum(bool(item) for item in weights),
        "max_influences": max((len(item) for item in weights), default=0),
        "weights": weights,
        "weights_sha256": snapshot_digest(weights),
        "actions": action_ranges(),
        "bounds": {str(frame): evaluated_bounds(character, frame) for frame in frames},
        "pose": {str(frame): pose_matrices(armature, frame) for frame in frames},
        "corner_normals": corner_normals_by_vertex(mesh),
        "has_custom_normals": bool(getattr(mesh, "has_custom_normals", False)),
    }


def serializable_capture(captured):
    result = dict(captured)
    result.pop("weights")
    result.pop("pose")
    result.pop("corner_normals")
    result["bounds"] = {
        frame: {key: value.tolist() for key, value in bounds.items()}
        for frame, bounds in result["bounds"].items()
    }
    return result


def compare_weights(source, output):
    max_error = 0.0
    for vertex_index in range(min(len(source), len(output))):
        source_weights = dict(source[vertex_index])
        output_weights = dict(output[vertex_index])
        for group in set(source_weights) | set(output_weights):
            max_error = max(
                max_error,
                abs(source_weights.get(group, 0.0) - output_weights.get(group, 0.0)),
            )
    return max_error


def compare_captures(source, output, frames):
    common_bones = sorted(set(source["bones"]) & set(output["bones"]))
    pose_errors = []
    for frame in frames:
        source_pose = source["pose"][str(frame)]
        output_pose = output["pose"][str(frame)]
        for bone in common_bones:
            pose_errors.extend(np.abs(source_pose[bone] - output_pose[bone]).ravel())

    bound_errors = []
    for frame in frames:
        for key in ("min", "max"):
            bound_errors.extend(
                np.abs(source["bounds"][str(frame)][key] - output["bounds"][str(frame)][key])
            )

    normal_angle_errors = []
    if source["vertices"] == output["vertices"]:
        for vertex_index, output_normals in enumerate(output["corner_normals"]):
            source_normals = source["corner_normals"][vertex_index]
            for output_normal in output_normals:
                if not source_normals:
                    continue
                best_dot = max(
                    float(np.clip(np.dot(output_normal, source_normal), -1.0, 1.0))
                    for source_normal in source_normals
                )
                normal_angle_errors.append(math.degrees(math.acos(best_dot)))

    pose_max = float(max(pose_errors, default=0.0))
    bounds_max = float(max(bound_errors, default=0.0))
    normal_max = float(max(normal_angle_errors, default=0.0))
    weight_max = compare_weights(source["weights"], output["weights"])
    comparison = {
        "topology_identical": (
            source["vertices"] == output["vertices"]
            and source["polygons"] == output["polygons"]
            and source["loops"] == output["loops"]
        ),
        "bone_names_identical": source["bones"] == output["bones"],
        "vertex_group_names_identical": source["vertex_groups"] == output["vertex_groups"],
        "all_output_vertices_weighted": output["weighted_vertices"] == output["vertices"],
        "action_ranges_identical": source["actions"] == output["actions"],
        "weight_max_absolute_error": float(weight_max),
        "pose_matrix_max_absolute_error": pose_max,
        "deformed_bounds_max_absolute_error": bounds_max,
        "corner_normal_max_angle_error_degrees": normal_max,
        "output_has_single_uv_layer": len(output["uv_layers"]) == 1,
        "output_uv_in_0_1": (
            bool(output["uv_min"])
            and min(output["uv_min"]) >= -1e-6
            and max(output["uv_max"]) <= 1.0 + 1e-6
        ),
    }
    comparison["passed"] = all(
        (
            comparison["topology_identical"],
            comparison["bone_names_identical"],
            comparison["vertex_group_names_identical"],
            comparison["all_output_vertices_weighted"],
            comparison["action_ranges_identical"],
            comparison["weight_max_absolute_error"] <= 1e-4,
            comparison["pose_matrix_max_absolute_error"] <= 1e-3,
            comparison["deformed_bounds_max_absolute_error"] <= 1e-4,
            comparison["corner_normal_max_angle_error_degrees"] <= 1.0,
            comparison["output_has_single_uv_layer"],
            comparison["output_uv_in_0_1"],
        )
    )
    return comparison


def create_bake_material(source_image, target_image, material_name):
    material = bpy.data.materials.new(material_name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    source_uv = nodes.new("ShaderNodeUVMap")
    source_uv.uv_map = "UV_Source"
    source_node = nodes.new("ShaderNodeTexImage")
    source_node.image = source_image
    source_node.interpolation = "Linear"
    source_node.extension = "REPEAT"
    emission = nodes.new("ShaderNodeEmission")
    output = nodes.new("ShaderNodeOutputMaterial")
    target_node = nodes.new("ShaderNodeTexImage")
    target_node.image = target_image
    links = material.node_tree.links
    links.new(source_uv.outputs["UV"], source_node.inputs["Vector"])
    links.new(source_node.outputs["Color"], emission.inputs["Color"])
    links.new(emission.outputs["Emission"], output.inputs["Surface"])
    for node in nodes:
        node.select = False
    target_node.select = True
    nodes.active = target_node
    return material


def fill_texture_background(image, valid):
    width, height = image.size
    flat = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(flat)
    pixels = flat.reshape((height, width, 4))
    colors = pixels[:, :, :3].copy()
    valid = np.asarray(valid, dtype=bool)
    if valid.shape != (height, width):
        raise RuntimeError("UV 掩码尺寸与烘焙贴图不一致")
    if not np.any(valid):
        raise RuntimeError("烘焙贴图没有有效像素")
    filled_pixels = int(np.count_nonzero(~valid))

    pyramid = [(colors, valid)]
    while pyramid[-1][0].shape[0] > 1 or pyramid[-1][0].shape[1] > 1:
        current_colors, current_valid = pyramid[-1]
        current_height, current_width = current_valid.shape
        padded_height = current_height + current_height % 2
        padded_width = current_width + current_width % 2
        padded_colors = np.zeros((padded_height, padded_width, 3), dtype=np.float32)
        padded_valid = np.zeros((padded_height, padded_width), dtype=bool)
        padded_colors[:current_height, :current_width] = current_colors
        padded_valid[:current_height, :current_width] = current_valid
        grouped_colors = padded_colors.reshape(
            padded_height // 2, 2, padded_width // 2, 2, 3
        )
        grouped_valid = padded_valid.reshape(padded_height // 2, 2, padded_width // 2, 2)
        counts = grouped_valid.sum(axis=(1, 3))
        weighted = (grouped_colors * grouped_valid[:, :, :, :, None]).sum(axis=(1, 3))
        next_colors = np.zeros((padded_height // 2, padded_width // 2, 3), dtype=np.float32)
        next_valid = counts > 0
        next_colors[next_valid] = weighted[next_valid] / counts[next_valid, None]
        pyramid.append((next_colors, next_valid))

    filled = pyramid[-1][0]
    if not pyramid[-1][1][0, 0]:
        raise RuntimeError("扩边金字塔没有有效顶层像素")
    for level in range(len(pyramid) - 2, -1, -1):
        original_colors, original_valid = pyramid[level]
        upsampled = np.repeat(np.repeat(filled, 2, axis=0), 2, axis=1)
        upsampled = upsampled[: original_colors.shape[0], : original_colors.shape[1]]
        filled = np.where(original_valid[:, :, None], original_colors, upsampled)

    pixels[:, :, :3] = filled
    pixels[:, :, 3] = 1.0
    image.pixels.foreach_set(pixels.reshape(-1))
    image.update()
    return {
        "method": "NUMPY_PUSH_PULL",
        "filled_pixels": filled_pixels,
        "filled_fraction": float(filled_pixels / (width * height)),
        "remaining_transparent_pixels": 0,
    }


def bake_base_color(character, optimized_uv, config):
    source_image = bpy.data.images.load(config["source_texture"], check_existing=False)
    source_image.colorspace_settings.name = "sRGB"
    target_image = bpy.data.images.new(
        os.path.splitext(os.path.basename(config["output_texture"]))[0],
        width=int(config["texture_size"]),
        height=int(config["texture_size"]),
        alpha=True,
        float_buffer=False,
        is_data=False,
    )
    target_image.generated_color = (0.0, 0.0, 0.0, 0.0)
    target_image.colorspace_settings.name = "sRGB"
    material = create_bake_material(source_image, target_image, config["material_name"])
    character.data.materials.clear()
    character.data.materials.append(material)

    bpy.ops.object.select_all(action="DESELECT")
    character.select_set(True)
    bpy.context.view_layer.objects.active = character
    character.data.uv_layers.active_index = next(
        index
        for index, layer in enumerate(character.data.uv_layers)
        if layer.name == optimized_uv.name
    )
    optimized_uv.active_render = True

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.bake.use_clear = True
    scene.render.bake.use_selected_to_active = False
    scene.render.bake.margin = int(config["bake_margin"])
    scene.render.bake.margin_type = "EXTEND"
    bpy.ops.object.bake(type="EMIT", margin=int(config["bake_margin"]))

    # 单独烘焙白色 UV 掩码，避免把贴图中合法的纯黑像素误判为空白。
    mask_image = bpy.data.images.new(
        "AIBUILDER_UV_BakeMask",
        width=int(config["texture_size"]),
        height=int(config["texture_size"]),
        alpha=True,
        float_buffer=False,
        is_data=True,
    )
    mask_image.generated_color = (0.0, 0.0, 0.0, 0.0)
    emission = next(node for node in material.node_tree.nodes if node.type == "EMISSION")
    for link in list(material.node_tree.links):
        if link.to_node == emission and link.to_socket == emission.inputs["Color"]:
            material.node_tree.links.remove(link)
    emission.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    mask_node = material.node_tree.nodes.new("ShaderNodeTexImage")
    mask_node.image = mask_image
    for node in material.node_tree.nodes:
        node.select = False
    mask_node.select = True
    material.node_tree.nodes.active = mask_node
    bpy.ops.object.bake(type="EMIT", margin=int(config["bake_margin"]))
    mask_flat = np.empty(mask_image.size[0] * mask_image.size[1] * 4, dtype=np.float32)
    mask_image.pixels.foreach_get(mask_flat)
    mask_rgb = mask_flat.reshape((mask_image.size[1], mask_image.size[0], 4))[:, :, :3]
    valid_mask = np.any(mask_rgb > 0.5, axis=2)
    fill_report = fill_texture_background(target_image, valid_mask)
    material.node_tree.nodes.remove(mask_node)
    bpy.data.images.remove(mask_image)
    target_image.filepath_raw = config["output_texture"]
    target_image.file_format = "PNG"
    target_image.save()
    return material, target_image, fill_report


def configure_export_material(material, target_image):
    nodes = material.node_tree.nodes
    nodes.clear()
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = target_image
    texture.interpolation = "Linear"
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.inputs["Roughness"].default_value = 0.9
    output = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(texture.outputs["Color"], principled.inputs["Base Color"])
    material.node_tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])


def action_fcurves(action):
    if hasattr(action, "fcurves"):
        return list(action.fcurves)
    result = []
    for layer in action.layers:
        for strip in layer.strips:
            if strip.type != "KEYFRAME":
                continue
            for channelbag in strip.channelbags:
                result.extend(channelbag.fcurves)
    return result


def shift_animations_for_fbx_roundtrip(ranges):
    if not ranges:
        return
    for action in bpy.data.actions:
        for fcurve in action_fcurves(action):
            for keyframe in fcurve.keyframe_points:
                keyframe.co[0] -= 1.0
                keyframe.handle_left[0] -= 1.0
                keyframe.handle_right[0] -= 1.0
            fcurve.update()
    scene = bpy.context.scene
    take_names = {
        action_name.split("|", 1)[1]
        for action_name in ranges
        if "|" in action_name
    }
    if len(take_names) == 1:
        # FBX 导出的 Action 名由“对象名|场景名”组成，恢复源 Take 名。
        scene.name = next(iter(take_names))
    scene.frame_start = int(math.floor(min(value[0] for value in ranges.values()) - 1.0))
    scene.frame_end = int(math.ceil(max(value[1] for value in ranges.values()) - 1.0))
    scene.frame_set(scene.frame_start)


def export_fbx(character, armature, output_fbx, has_animation):
    bpy.ops.object.select_all(action="DESELECT")
    export_objects = {armature, character}
    export_objects.update(armature.children_recursive)
    ancestor = armature.parent
    while ancestor is not None:
        export_objects.add(ancestor)
        ancestor = ancestor.parent
    for obj in export_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=output_fbx,
        use_selection=True,
        object_types={"ARMATURE", "MESH", "EMPTY"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=False,
        mesh_smooth_type="OFF",
        use_tspace=True,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=False,
        bake_anim=has_animation,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="AUTO",
        embed_textures=False,
    )
    return sorted(obj.name for obj in export_objects)


def write_report(config, report):
    os.makedirs(os.path.dirname(config["output_report"]), exist_ok=True)
    with open(config["output_report"], "w", encoding="utf-8") as report_file:
        json.dump(report, report_file, ensure_ascii=False, indent=2)


def run(config):
    for key in ("source_fbx", "source_texture"):
        if not os.path.isfile(config[key]):
            raise RuntimeError(f"输入文件不存在: {config[key]}")
    for key in ("output_fbx", "output_texture", "output_report"):
        os.makedirs(os.path.dirname(config[key]), exist_ok=True)

    install_fbx_armature_repair()
    clear_scene()
    stage("导入源 FBX")
    bpy.ops.import_scene.fbx(filepath=config["source_fbx"], use_anim=True)
    character, candidates = select_character(config.get("mesh_object_name", ""))
    candidate_names = [item.name for item in candidates]
    armature = character.find_armature()
    mesh = character.data
    if mesh.shape_keys and len(mesh.shape_keys.key_blocks) > 1:
        raise RuntimeError("检测到形态键。当前安全流程不会修改带形态键的模型")
    if not mesh.uv_layers:
        raise RuntimeError("源模型没有 UV")

    ranges = action_ranges()
    frames = sample_frames(ranges)
    source_capture = capture_character(character, frames)
    source_uv = mesh.uv_layers.active
    source_uv.name = "UV_Source"
    source_weights = weight_snapshot(character)
    source_normals = np.asarray(
        [normal.vector[:] for normal in mesh.corner_normals], dtype=np.float64
    )

    stage("按人体结构重展 UV")
    pack_margin = float(config["bake_margin"]) / float(config["texture_size"])
    optimized_uv, layout_report = uv_layout.generate_humanoid_uv(character, pack_margin)
    if (
        len(mesh.vertices) != source_capture["vertices"]
        or len(mesh.polygons) != source_capture["polygons"]
        or len(mesh.loops) != source_capture["loops"]
    ):
        raise RuntimeError("UV 展开改变了模型拓扑，已停止")
    if weight_snapshot(character) != source_weights:
        raise RuntimeError("UV 展开改变了蒙皮权重，已停止")
    current_normals = np.asarray(
        [normal.vector[:] for normal in mesh.corner_normals], dtype=np.float64
    )
    if np.max(np.abs(current_normals - source_normals)) > 1e-7:
        raise RuntimeError("UV 展开改变了顶点法线，已停止")

    stage("重烘焙并扩边贴图")
    material, target_image, gutter_report = bake_base_color(character, optimized_uv, config)
    raster_report = uv_layout.raster_metrics(mesh, optimized_uv)

    if config.get("save_blend", True):
        stage("保存可复查 Blend 文件")
        bpy.ops.wm.save_as_mainfile(filepath=config["output_blend"], check_existing=False)

    source_uv = mesh.uv_layers.get("UV_Source")
    if source_uv is None:
        raise RuntimeError("导出前找不到源 UV")
    mesh.uv_layers.remove(source_uv)
    optimized_uv = mesh.uv_layers.get("UV_Optimized")
    optimized_uv.name = "UVMap"
    mesh.uv_layers.active = optimized_uv
    configure_export_material(material, target_image)
    shift_animations_for_fbx_roundtrip(ranges)

    stage("导出独立 FBX")
    exported_objects = export_fbx(character, armature, config["output_fbx"], bool(ranges))

    stage("重新导入并验证骨骼动画")
    clear_scene()
    bpy.ops.import_scene.fbx(filepath=config["output_fbx"], use_anim=True)
    output_character, _ = select_character(source_capture["character_name"])
    output_capture = capture_character(output_character, frames)
    comparison = compare_captures(source_capture, output_capture, frames)
    status = "success" if comparison["passed"] else "failed"
    report = {
        "status": status,
        "error": "" if comparison["passed"] else "导出 FBX 未通过骨骼、权重、动画或拓扑校验",
        "source_fbx": config["source_fbx"],
        "source_texture": config["source_texture"],
        "output_fbx": config["output_fbx"],
        "output_texture": config["output_texture"],
        "output_blend": config["output_blend"] if config.get("save_blend", True) else "",
        "before": serializable_capture(source_capture),
        "after": serializable_capture(output_capture),
        "uv_layout": layout_report,
        "uv_raster": raster_report,
        "gutter_fill": gutter_report,
        "validation": comparison,
        "mesh_candidates": candidate_names,
        "exported_objects": exported_objects,
        "source_files_overwritten": False,
    }
    write_report(config, report)
    print("AIBUILDER_UV_RESULT=" + json.dumps(report, ensure_ascii=False), flush=True)
    if comparison["passed"]:
        stage("完成")
    return report


def main():
    config = load_config()
    try:
        report = run(config)
        if report["status"] != "success":
            print("AIBUILDER_UV_ERROR=" + report["error"], file=sys.stderr, flush=True)
            sys.exit(1)
    except Exception as exception:
        report = {
            "status": "failed",
            "error": str(exception),
            "traceback": traceback.format_exc(),
            "source_fbx": config.get("source_fbx", ""),
            "source_texture": config.get("source_texture", ""),
            "source_files_overwritten": False,
        }
        write_report(config, report)
        print("AIBUILDER_UV_ERROR=" + str(exception), file=sys.stderr, flush=True)
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
