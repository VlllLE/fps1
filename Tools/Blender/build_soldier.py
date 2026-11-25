"""Builds the detailed soldier (third-person body + first-person arms) headlessly and exports it for Unity.

Run:

    blender -b --factory-startup --python Tools/Blender/build_soldier.py -- \
        --out Assets/Characters/Models --renders /path/to/render/dir [--samples 64] [--res 100] [--no-render]

Outputs (all procedural, nothing downloaded):
    Soldier.fbx / Soldier.glb        skinned body, 19 bones named like the C# HumanoidBone enum
    SoldierArmsFP.fbx / .glb         first-person arms (6 bones: UpperArm/Forearm/Hand L+R)
    Soldier_AO.png                   baked ambient occlusion (grey, tint friendly albedo)

Authoring space is Unity metres (+Z forward, +Y up, +X = the character's right); see geometry.py.
Joint heights mirror Assets/Scripts/Characters/HumanoidProportions.cs so the existing IK, hitboxes and
camera setup keep working with the skinned mesh.
"""

import importlib
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        if i + 1 < len(ARGS):
            return ARGS[i + 1]
    return default


def flag(name):
    return name in ARGS


import soldier_lib.pipeline as pipeline  # noqa: E402

importlib.reload(pipeline)

pipeline.run(
    out_dir=arg("--out", os.path.abspath("soldier_out")),
    render_dir=arg("--renders", None),
    samples=int(arg("--samples", "48")),
    res_percent=int(arg("--res", "100")),
    skip_render=flag("--no-render"),
    quick=flag("--quick"),
    stage=arg("--stage", "all"),
)
