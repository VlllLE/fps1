"""Scene setup and rendering helpers (Workbench for quick checks, Cycles for the final renders)."""

import math
import os

import bpy
from mathutils import Vector


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup_scene(engine="CYCLES", samples=48, res=(1000, 1400), floor=True, ground_y=0.0):
    scene = bpy.context.scene
    scene.render.engine = engine
    if engine == "CYCLES":
        scene.cycles.device = "CPU"
        scene.cycles.samples = samples
        scene.cycles.use_denoising = True
    else:
        scene.display.shading.light = "STUDIO"
        scene.display.shading.color_type = "MATERIAL"
        scene.display.shading.show_shadows = True
        scene.display.shading.show_cavity = True
    scene.render.resolution_x = res[0]
    scene.render.resolution_y = res[1]
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"

    world = bpy.data.worlds.new("Studio") if scene.world is None else scene.world
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.16, 0.18, 0.22, 1.0)
    bg.inputs["Strength"].default_value = 0.7

    if floor:
        bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0, 0, ground_y - 0.002))
        fl = bpy.context.active_object
        fl.name = "Floor"
        fm = bpy.data.materials.new("Floor")
        fm.use_nodes = True
        fb = fm.node_tree.nodes["Principled BSDF"]
        fb.inputs["Base Color"].default_value = (0.20, 0.21, 0.23, 1.0)
        fb.inputs["Roughness"].default_value = 0.8
        fl.data.materials.append(fm)

    def area(name, loc, energy, size, color=(1, 1, 1), target=(0, 0, 1.0)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.size = size
        data.color = color
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = loc
        look_at(obj, target)
        return obj

    area("Key", (-1.8, -2.2, 2.6), 520.0, 1.8)
    area("Rim", (2.2, 2.0, 2.2), 420.0, 1.2, (0.75, 0.85, 1.0))
    area("Fill", (2.4, -1.8, 1.0), 160.0, 2.0, (1.0, 0.92, 0.85))
    area("Top", (0.0, 0.2, 3.5), 220.0, 2.0)

    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam, cam_data


def render(cam, cam_data, path, loc, target, lens=50.0, ortho=None):
    scene = bpy.context.scene
    cam_data.lens = lens
    cam.location = loc
    look_at(cam, target)
    scene.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)
    print("SOLDIER_RENDER", path)
