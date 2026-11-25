"""UV unwrap, ambient-occlusion bake, mesh splitting and FBX/GLB export."""

import math
import os

import bmesh
import bpy

from .geometry import BONES, GROUP_NAMES, MATERIAL_NAMES


def _select_only(obj):
    for o in bpy.context.scene.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def unwrap(obj, margin=0.004):
    """Smart UV project followed by an area-weighted pack into the single 0..1 atlas."""
    _select_only(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(62.0), island_margin=margin, correct_aspect=True,
                             scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=margin, rotate=True)
    bpy.ops.object.mode_set(mode="OBJECT")
    uv = obj.data.uv_layers.active
    us = [d.uv.x for d in uv.data]
    vs = [d.uv.y for d in uv.data]
    return min(us), max(us), min(vs), max(vs)


def bake_ao(obj, path, size=1024, samples=24, distance=0.25):
    """Bake ambient occlusion into an image atlas and save it."""
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("W")
    scene.world.light_settings.distance = distance
    image = bpy.data.images.new("Soldier_AO", size, size, alpha=False, float_buffer=False)
    image.generated_color = (1, 1, 1, 1)
    nodes_added = []
    for mat in obj.data.materials:
        nt = mat.node_tree
        node = nt.nodes.new("ShaderNodeTexImage")
        node.image = image
        nt.nodes.active = node
        nodes_added.append((nt, node))
    _select_only(obj)
    bpy.ops.object.bake(type="AO", margin=6, use_clear=True)
    for nt, node in nodes_added:
        nt.nodes.remove(node)
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    return image


def make_part(obj, keep_ids, name, arm, weights, bones=BONES):
    """New object with only the vertices in ``keep_ids`` (copies UVs, weights, materials used)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    kill = [v for v in bm.verts if v.index not in keep_ids]
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    used = sorted({f.material_index for f in bm.faces})
    remap = {old: i for i, old in enumerate(used)}
    for f in bm.faces:
        f.material_index = remap[f.material_index]
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for old in used:
        mesh.materials.append(obj.data.materials[old])
    for p in mesh.polygons:
        p.use_smooth = True
    part = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(part)
    for b in bones:
        part.vertex_groups.new(name=b)
    for i, vi in enumerate(sorted(keep_ids)):
        for b, val in weights[vi].items():
            part.vertex_groups[b].add([i], val, "REPLACE")
    part.parent = arm
    mod = part.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    return part


def export_fbx_glb(objs, arm, base_path):
    for o in bpy.context.scene.objects:
        o.select_set(False)
    for o in objs + [arm]:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=base_path + ".fbx",
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        bake_space_transform=False,
        mesh_smooth_type="EDGE",
        armature_nodetype="NULL",
        path_mode="STRIP",
        use_armature_deform_only=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
    )
    bpy.ops.export_scene.gltf(
        filepath=base_path + ".glb",
        export_format="GLB",
        use_selection=True,
        export_apply=False,
        export_yup=True,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
        export_skins=True,
        export_animations=False,
    )
