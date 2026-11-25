"""Final render set: body views, head close-ups, A-pose, aiming pose, bent-pose skinning checks, FP arms."""

import os

import bpy

from . import preview, rig


def _show(objs, visible):
    for o in objs:
        o.hide_render = not visible
        o.hide_viewport = not visible


def _shot(cam, cam_data, scene, render_dir, name, loc, tgt, lens, res):
    scene.render.resolution_x, scene.render.resolution_y = res
    path = os.path.join(render_dir, "character-%s.png" % name)
    preview.render(cam, cam_data, path, loc, tgt, lens)
    print("SOLDIER_RENDER", path)


A_POSE = {"UpperArmR": (0, 0, 38), "UpperArmL": (0, 0, -38), "ForearmR": (-8, 0, 0), "ForearmL": (-8, 0, 0)}
AIM_POSE = {"Spine": (4, 0, 0), "Chest": (3, 8, 0), "Head": (0, 6, 0),
            "UpperArmR": (-78, 0, -4), "ForearmR": (-30, 0, 0), "UpperArmL": (-66, 0, 24), "ForearmL": (-52, 0, 0),
            "ThighL": (-8, 0, 0), "ThighR": (6, 0, 0)}
BEND_A = {"Spine": (28, 0, 0), "Chest": (16, 0, 0), "UpperArmR": (-95, 0, 0), "ForearmR": (-80, 0, 0),
          "UpperArmL": (-55, 0, 0), "ForearmL": (-110, 0, 0), "Head": (0, 35, 0), "Neck": (10, 0, 0)}
BEND_B = {"ThighR": (-85, 0, 0), "ShinR": (95, 0, 0), "ThighL": (25, 0, 0), "ShinL": (15, 0, 0),
          "UpperArmR": (0, 0, 35), "UpperArmL": (0, 0, -35), "ForearmR": (-110, 0, 0), "ForearmL": (-45, 0, 0),
          "Hips": (0, 15, 0)}


def run_renders(render_dir, parts, arm, fp_parts, fp_arm, samples, res_percent, quick):
    os.makedirs(render_dir, exist_ok=True)
    engine = "BLENDER_WORKBENCH" if quick else "CYCLES"
    cam, cam_data = preview.setup_scene(engine=engine, samples=samples, res=(1000, 1400))
    scene = bpy.context.scene
    scene.render.resolution_percentage = res_percent
    body = parts + [arm]
    fp = fp_parts + [fp_arm]

    _show(fp, False)
    _show(body, True)
    full = (1000, 1400)
    sq = (1100, 1100)
    rig.reset_pose(arm)
    _shot(cam, cam_data, scene, render_dir, "front", (0, -4.4, 1.0), (0, 0, 0.92), 55, full)
    _shot(cam, cam_data, scene, render_dir, "side", (4.4, 0, 1.0), (0, 0, 0.92), 55, full)
    _shot(cam, cam_data, scene, render_dir, "back", (0, 4.4, 1.0), (0, 0, 0.92), 55, full)
    _shot(cam, cam_data, scene, render_dir, "three-quarter", (2.7, -3.4, 1.4), (0, 0, 0.95), 55, full)
    _shot(cam, cam_data, scene, render_dir, "head-front", (0.25, -1.15, 1.80), (0, 0, 1.68), 85, sq)
    _shot(cam, cam_data, scene, render_dir, "head-three-quarter", (-0.95, -0.85, 1.85), (0, 0, 1.68), 85, sq)
    _shot(cam, cam_data, scene, render_dir, "head-side", (1.2, -0.2, 1.80), (0, 0, 1.68), 85, sq)
    _shot(cam, cam_data, scene, render_dir, "upper-body", (0.9, -2.0, 1.55), (0, 0, 1.25), 70, sq)
    _shot(cam, cam_data, scene, render_dir, "backpack", (-1.4, 2.1, 1.5), (0, 0, 1.25), 70, sq)
    _shot(cam, cam_data, scene, render_dir, "legs-boots", (-1.8, -2.2, 0.8), (0, 0, 0.45), 65, sq)
    _shot(cam, cam_data, scene, render_dir, "hand-glove", (-0.95, -0.7, 0.95), (-0.215, 0, 0.80), 85, sq)

    rig.set_pose(arm, A_POSE)
    _shot(cam, cam_data, scene, render_dir, "a-pose", (0, -4.4, 1.0), (0, 0, 0.92), 55, full)
    _shot(cam, cam_data, scene, render_dir, "a-pose-three-quarter", (2.7, -3.4, 1.4), (0, 0, 0.95), 55, full)
    rig.set_pose(arm, AIM_POSE)
    _shot(cam, cam_data, scene, render_dir, "aiming-three-quarter", (2.7, -3.0, 1.35), (0, 0, 1.0), 55, full)
    _shot(cam, cam_data, scene, render_dir, "aiming-side", (4.4, 0.0, 1.2), (0, 0, 1.0), 55, full)
    rig.set_pose(arm, BEND_A)
    _shot(cam, cam_data, scene, render_dir, "skinning-bend-a", (2.6, -3.2, 1.3), (0, 0, 0.95), 55, full)
    rig.set_pose(arm, BEND_B)
    _shot(cam, cam_data, scene, render_dir, "skinning-bend-b", (2.6, -3.2, 1.3), (0, 0, 0.95), 55, full)
    rig.reset_pose(arm)

    _show(body, False)
    _show(fp, True)
    rig.reset_pose(fp_arm)
    _shot(cam, cam_data, scene, render_dir, "fp-arms-rest", (1.1, -1.8, 1.0), (0, 0, 1.0), 55, full)
    _shot(cam, cam_data, scene, render_dir, "fp-hands-closeup", (-0.95, -0.7, 0.95), (-0.215, 0, 0.80), 85, sq)
    rig.set_pose(fp_arm, {"UpperArmR": (-75, 0, 0), "ForearmR": (-35, 0, 0), "UpperArmL": (-70, 0, 10),
                          "ForearmL": (-40, 0, 0)})
    _shot(cam, cam_data, scene, render_dir, "fp-arms-forward", (2.4, -2.0, 1.4), (0, 0, 1.05), 55, full)
    rig.reset_pose(fp_arm)
