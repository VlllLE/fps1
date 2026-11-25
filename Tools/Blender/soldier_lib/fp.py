"""First-person arms: sleeves, tactical gloves and forearm gear on the 6-bone FP IK rig.

The rig is ProceduralFirstPersonArms: per arm UpperArm, Forearm and Hand transforms that are posed by a
two-bone IK. The mesh is authored in the hanging 'design pose' used by the body (arm axis along -Y at
x = +-0.215). In bone space of the C# rig the glove palm normal is +Z and the thumb sits on local
+side X, which is how build_glove models it.
"""

import math

from mathutils import Vector

from . import body
from .geometry import (ARMOR, CLOTH, GEAR, ACCENT, G_ARM_L, G_ARM_R, R_ARM_L, R_ARM_R, Model, euler_matrix,
                       smoothstep)
from .body import V3, SHOULDER_HALF, ELBOW_Y, WRIST_Y

FP_BONES = ["UpperArmL", "UpperArmR", "ForearmL", "ForearmR", "HandL", "HandR"]


def build_fp_extras(m, side):
    reg = R_ARM_R if side > 0 else R_ARM_L
    m.base(reg, grp=G_ARM_R if side > 0 else G_ARM_L)
    x = side * SHOULDER_HALF
    # Elbow pad (back of the elbow, visible when the arm folds).
    m.box(V3(x, ELBOW_Y + 0.004, -0.054), (0.080, 0.090, 0.030), bevel=0.012, seg=2, mat=GEAR)
    m.box(V3(x, ELBOW_Y + 0.004, -0.068), (0.056, 0.060, 0.014), bevel=0.006, mat=ARMOR)
    # Rolled-sleeve seam rings and forearm webbing.
    for dy in (0.050, 0.075):
        m.tube([V3(x, ELBOW_Y - dy, 0.0), V3(x, ELBOW_Y - dy - 0.007, 0.0)], [0.046, 0.046], [0.044, 0.044], n=22,
               mat=CLOTH, caps=("flat", "flat"))
    # Accent velcro identification patch on the outside of the forearm.
    m.box(V3(x + side * 0.046, ELBOW_Y - 0.10, 0.0), (0.006, 0.060, 0.040), bevel=0.0015, mat=ACCENT)
    # Wrist watch / wrist computer on the left arm.
    if side < 0:
        m.box(V3(x, WRIST_Y + 0.060, 0.0), (0.078, 0.036, 0.074), bevel=0.006, mat=GEAR)
        m.box(V3(x, WRIST_Y + 0.060, 0.0405), (0.044, 0.026, 0.004), bevel=0.002, mat=ARMOR)
        m.box(V3(x, WRIST_Y + 0.060, -0.0405), (0.050, 0.020, 0.004), bevel=0.002, mat=ACCENT)


def build_fp_model():
    m = Model()
    for side in (1, -1):
        body.build_sleeve(m, side)
        body.build_glove(m, side, detail=2.0)
        build_fp_extras(m, side)
    return m
