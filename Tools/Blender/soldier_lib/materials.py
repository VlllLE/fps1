"""Blender materials. Colours mirror the default palette of the Unity materials (Mat_Soldier_*)."""

import bpy

# name, base colour, metallic, roughness
SPECS = [
    ("Soldier_Armor", (0.09, 0.10, 0.11), 0.35, 0.45),
    ("Soldier_Cloth", (0.20, 0.26, 0.19), 0.0, 0.88),
    ("Soldier_Skin", (0.74, 0.55, 0.45), 0.0, 0.55),
    ("Soldier_Gear", (0.30, 0.25, 0.17), 0.05, 0.75),
    ("Soldier_Accent", (0.80, 0.42, 0.08), 0.1, 0.6),
    ("Soldier_Lens", (0.04, 0.10, 0.14), 0.6, 0.10),
    ("Soldier_Eye", (0.90, 0.90, 0.88), 0.0, 0.25),
]


def make_materials(ao_image=None):
    mats = []
    for name, col, metal, rough in SPECS:
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.use_nodes = True
        nt = mat.node_tree
        for n in list(nt.nodes):
            if n.type != "OUTPUT_MATERIAL":
                nt.nodes.remove(n)
        out = [n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"][0]
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Base Color"].default_value = (col[0], col[1], col[2], 1.0)
        bsdf.inputs["Metallic"].default_value = metal
        bsdf.inputs["Roughness"].default_value = rough
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
        mat.diffuse_color = (col[0], col[1], col[2], 1.0)
        mat.metallic = metal
        mat.roughness = rough
        mats.append(mat)
    return mats
