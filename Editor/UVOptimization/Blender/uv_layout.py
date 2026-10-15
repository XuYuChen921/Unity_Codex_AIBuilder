import collections
import math

import bpy
import numpy as np


def _edge_face_uses(mesh):
    uses = collections.defaultdict(list)
    edge_by_key = {}
    for edge in mesh.edges:
        key = tuple(sorted((int(edge.vertices[0]), int(edge.vertices[1]))))
        edge_by_key[key] = edge.index
    for polygon in mesh.polygons:
        for edge_key in polygon.edge_keys:
            uses[tuple(sorted(edge_key))].append(polygon.index)
    return uses, edge_by_key


def _face_components(mesh):
    uses, _ = _edge_face_uses(mesh)
    adjacency = [[] for _ in mesh.polygons]
    for faces in uses.values():
        if len(faces) != 2:
            continue
        left, right = faces
        adjacency[left].append(right)
        adjacency[right].append(left)

    components = []
    visited = set()
    for start in range(len(mesh.polygons)):
        if start in visited:
            continue
        stack = [start]
        visited.add(start)
        component = []
        while stack:
            face = stack.pop()
            component.append(face)
            for neighbor in adjacency[face]:
                if neighbor not in visited:
                    visited.add(neighbor)
                    stack.append(neighbor)
        components.append(component)
    components.sort(key=len, reverse=True)
    return components


def _region_vertices(mesh, face_indices):
    return sorted(
        {
            mesh.loops[loop_index].vertex_index
            for face_index in face_indices
            for loop_index in mesh.polygons[face_index].loop_indices
        }
    )


def _classify_humanoid_regions(mesh, components):
    coordinates = np.asarray([vertex.co[:] for vertex in mesh.vertices], dtype=np.float64)
    minimum = coordinates.min(axis=0)
    maximum = coordinates.max(axis=0)
    height = float(maximum[2] - minimum[2])
    if height <= 1e-8:
        raise RuntimeError("模型 Z 轴高度为 0，无法按人体结构展开 UV")
    center_x = float((minimum[0] + maximum[0]) * 0.5)

    regions = {}
    main_faces = set(components[0])
    for polygon in mesh.polygons:
        if polygon.index not in main_faces:
            continue
        x, _, z = polygon.center
        normalized_z = (float(z) - float(minimum[2])) / height
        normalized_x = (float(x) - center_x) / height
        if normalized_z >= 0.86 and abs(normalized_x) < 0.16:
            region = "head"
        elif normalized_z >= 0.60 and normalized_x <= -0.14:
            region = "arm_left"
        elif normalized_z >= 0.60 and normalized_x >= 0.14:
            region = "arm_right"
        elif normalized_z < 0.55 and normalized_x < 0.0:
            region = "leg_left"
        elif normalized_z < 0.55:
            region = "leg_right"
        else:
            region = "torso"
        regions[polygon.index] = region

    for component_index, faces in enumerate(components[1:], start=1):
        for face in faces:
            regions[face] = f"detail_{component_index:02d}"
    return regions


def _mark_detail_tree_seams(mesh, detail_faces, uses, edge_by_key):
    detail_faces = set(detail_faces)
    parent = {face: face for face in detail_faces}

    def find(value):
        while parent[value] != value:
            parent[value] = parent[parent[value]]
            value = parent[value]
        return value

    def union(left, right):
        left = find(left)
        right = find(right)
        if left == right:
            return False
        parent[right] = left
        return True

    weighted_edges = []
    for edge_key, faces in uses.items():
        if len(faces) != 2 or not all(face in detail_faces for face in faces):
            continue
        left, right = faces
        similarity = float(mesh.polygons[left].normal.dot(mesh.polygons[right].normal))
        weighted_edges.append((similarity, edge_by_key[edge_key], left, right))

    kept_edges = set()
    for _, edge_index, left, right in sorted(weighted_edges, reverse=True):
        if union(left, right):
            kept_edges.add(edge_index)
    for _, edge_index, _, _ in weighted_edges:
        if edge_index not in kept_edges:
            mesh.edges[edge_index].use_seam = True


def _radial_angle(position, center, axis):
    axis = np.asarray(axis, dtype=np.float64)
    axis /= np.linalg.norm(axis)
    helper = np.array([0.0, 0.0, 1.0])
    if abs(np.dot(helper, axis)) > 0.9:
        helper = np.array([0.0, 1.0, 0.0])
    radial_x = np.cross(axis, helper)
    radial_x /= np.linalg.norm(radial_x)
    radial_y = np.cross(axis, radial_x)
    centered = np.asarray(position, dtype=np.float64) - center
    return math.atan2(float(np.dot(centered, radial_y)), float(np.dot(centered, radial_x)))


def _mark_humanoid_seams(mesh, components, regions):
    for edge in mesh.edges:
        edge.use_seam = False
    uses, edge_by_key = _edge_face_uses(mesh)
    faces_by_region = collections.defaultdict(list)
    for face, region in regions.items():
        faces_by_region[region].append(face)

    centers = {}
    for region, faces in faces_by_region.items():
        vertices = _region_vertices(mesh, faces)
        centers[region] = np.asarray(
            [mesh.vertices[index].co[:] for index in vertices], dtype=np.float64
        ).mean(axis=0)

    main_regions = {"head", "torso", "arm_left", "arm_right", "leg_left", "leg_right"}
    for edge_key, faces in uses.items():
        edge = mesh.edges[edge_by_key[edge_key]]
        if len(faces) != 2:
            edge.use_seam = True
            continue
        left_region = regions[faces[0]]
        right_region = regions[faces[1]]
        if left_region != right_region:
            edge.use_seam = True
            continue
        if left_region not in main_regions:
            continue
        axis = (1.0, 0.0, 0.0) if left_region.startswith("arm_") else (0.0, 0.0, 1.0)
        left_angle = _radial_angle(
            mesh.vertices[edge.vertices[0]].co, centers[left_region], axis
        )
        right_angle = _radial_angle(
            mesh.vertices[edge.vertices[1]].co, centers[left_region], axis
        )
        if abs(left_angle - right_angle) > math.pi:
            edge.use_seam = True

    for component in components[1:]:
        _mark_detail_tree_seams(mesh, component, uses, edge_by_key)
    mesh.update()


def _set_active_object(obj):
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def uv_island_count(mesh, uv_layer=None, tolerance=1e-6):
    uv_layer = uv_layer or mesh.uv_layers.active
    parent = list(range(len(mesh.polygons)))

    def find(value):
        while parent[value] != value:
            parent[value] = parent[parent[value]]
            value = parent[value]
        return value

    def union(left, right):
        left = find(left)
        right = find(right)
        if left != right:
            parent[right] = left

    uses = collections.defaultdict(list)
    for polygon in mesh.polygons:
        loops = list(polygon.loop_indices)
        for offset, loop_index in enumerate(loops):
            next_loop = loops[(offset + 1) % len(loops)]
            vertex_a = mesh.loops[loop_index].vertex_index
            vertex_b = mesh.loops[next_loop].vertex_index
            uv_a = np.asarray(uv_layer.data[loop_index].uv[:], dtype=np.float64)
            uv_b = np.asarray(uv_layer.data[next_loop].uv[:], dtype=np.float64)
            if vertex_a <= vertex_b:
                key, pair = (vertex_a, vertex_b), (uv_a, uv_b)
            else:
                key, pair = (vertex_b, vertex_a), (uv_b, uv_a)
            uses[key].append((polygon.index, pair))

    for edge_uses in uses.values():
        for left_offset, (left_face, left_pair) in enumerate(edge_uses):
            for right_face, right_pair in edge_uses[left_offset + 1 :]:
                if (
                    np.linalg.norm(left_pair[0] - right_pair[0]) <= tolerance
                    and np.linalg.norm(left_pair[1] - right_pair[1]) <= tolerance
                ):
                    union(left_face, right_face)
    return len({find(index) for index in range(len(mesh.polygons))})


def raster_metrics(mesh, uv_layer, resolution=512):
    occupancy = np.zeros((resolution, resolution), dtype=np.uint16)
    mesh.calc_loop_triangles()
    for triangle in mesh.loop_triangles:
        uv = np.asarray(
            [uv_layer.data[index].uv[:] for index in triangle.loops], dtype=np.float64
        )
        uv *= resolution
        minimum = np.maximum(np.floor(uv.min(axis=0)).astype(int), 0)
        maximum = np.minimum(np.ceil(uv.max(axis=0)).astype(int), resolution - 1)
        if np.any(maximum < minimum):
            continue
        x_values = np.arange(minimum[0], maximum[0] + 1) + 0.5
        y_values = np.arange(minimum[1], maximum[1] + 1) + 0.5
        grid_x, grid_y = np.meshgrid(x_values, y_values)
        point_x = grid_x - uv[2, 0]
        point_y = grid_y - uv[2, 1]
        edge_a = uv[0] - uv[2]
        edge_b = uv[1] - uv[2]
        denominator = edge_a[0] * edge_b[1] - edge_b[0] * edge_a[1]
        if abs(denominator) <= 1e-12:
            continue
        bary_a = (point_x * edge_b[1] - edge_b[0] * point_y) / denominator
        bary_b = (edge_a[0] * point_y - point_x * edge_a[1]) / denominator
        bary_c = 1.0 - bary_a - bary_b
        inside = (bary_a > 1e-6) & (bary_b > 1e-6) & (bary_c > 1e-6)
        region = occupancy[
            minimum[1] : maximum[1] + 1,
            minimum[0] : maximum[0] + 1,
        ]
        region[inside] += 1
    return {
        "resolution": resolution,
        "coverage_fraction": float(np.mean(occupancy > 0)),
        "overlap_fraction": float(np.mean(occupancy > 1)),
        "max_depth": int(occupancy.max()),
    }


def generate_humanoid_uv(obj, pack_margin):
    mesh = obj.data
    components = _face_components(mesh)
    if not components:
        raise RuntimeError("目标网格没有可展开的面")
    regions = _classify_humanoid_regions(mesh, components)
    optimized = mesh.uv_layers.new(name="UV_Optimized", do_init=False)
    mesh.uv_layers.active_index = len(mesh.uv_layers) - 1
    optimized.active_render = True
    _mark_humanoid_seams(mesh, components, regions)
    _set_active_object(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.unwrap(
        method="MINIMUM_STRETCH",
        fill_holes=True,
        correct_aspect=True,
        margin_method="FRACTION",
        margin=0.0,
        iterations=80,
    )
    bpy.ops.uv.average_islands_scale(shear=True)
    bpy.ops.uv.pack_islands(
        rotate=True,
        rotate_method="ANY",
        scale=True,
        margin_method="FRACTION",
        margin=pack_margin,
        shape_method="CONVEX",
    )
    bpy.ops.object.mode_set(mode="OBJECT")
    optimized = mesh.uv_layers.get("UV_Optimized")
    if optimized is None:
        raise RuntimeError("展开后找不到 UV_Optimized 层")
    mesh.uv_layers.active_index = next(
        index for index, layer in enumerate(mesh.uv_layers) if layer.name == optimized.name
    )
    optimized.active_render = True
    mesh.update()
    return optimized, {
        "method": "HUMANOID_MINIMUM_STRETCH",
        "topology_components": len(components),
        "main_component_faces": len(components[0]),
        "uv_islands": uv_island_count(mesh, optimized),
        "seam_edges": sum(edge.use_seam for edge in mesh.edges),
        "pack_margin_uv_fraction": pack_margin,
        "regions": dict(collections.Counter(regions.values())),
    }
