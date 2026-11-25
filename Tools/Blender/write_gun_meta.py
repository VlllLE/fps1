#!/usr/bin/env python3
"""Writes the Unity .meta files for a generated gun model folder (plain Python, no Blender needed).

    python3 Tools/Blender/write_gun_meta.py Assets/Weapons/Models/BoltActionSniper BoltActionSniper

Creates <Gun>.fbx.meta, <Gun>.glb.meta and <Gun>.anchors.json.meta next to the exported files. GUIDs that
already exist in a .meta are kept, so re-running after a re-export never changes references.

The FBX importer meta:
  * remaps the FBX material names (GunMetal, GunPolymer, SniperGlass, SniperAccent) to the shared project
    materials with externalObjects, and
  * pins the fileID of the model's root GameObject through internalIDToNameTable, so prefabs can reference
    `{fileID: <ROOT_IDS[gun]>, guid: <fbx guid>}` without Unity having to hash it.
"""

import os
import re
import sys
import uuid

MATERIALS = {
    "GunMetal": "9c56cd1a248e4ea8bd9cd090ebfe1b88",
    "GunPolymer": "57976278de9a418096150c9efa43832c",
    "SniperGlass": "5b0c1b7a9e4d4f2c8a3d6e1f7c2b9a40",
    "SniperAccent": "a17c3e5d2b9f4086b4d1c8e7f6a30b52",
}

ROOT_IDS = {
    "BoltActionSniper": 4108250301144107001,
    "DMRSniper": 4108250301144107002,
}

FBX_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
ModelImporter:
  serializedVersion: 22200
  internalIDToNameTable:
  - first:
      1: {root_id}
    second: {name}
  externalObjects:
{externals}  materials:
    materialImportMode: 1
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  animations:
    legacyGenerateAnimations: 4
    bakeSimulation: 0
    resampleCurves: 1
    optimizeGameObjects: 0
    removeConstantScaleCurves: 0
    motionNodeName: 
    rigImportErrors: 
    rigImportWarnings: 
    animationImportErrors: 
    animationImportWarnings: 
    animationRetargetingWarnings: 
    animationDoRetargetingWarnings: 0
    importAnimatedCustomProperties: 0
    importConstraints: 0
    animationCompression: 0
    animationRotationError: 0.5
    animationPositionError: 0.5
    animationScaleError: 0.5
    animationWrapMode: 0
    extraExposedTransformPaths: []
    extraUserProperties: []
    clipAnimations: []
    isReadable: 0
  meshes:
    lODScreenPercentages: []
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    useSRGBMaterialColor: 1
    sortHierarchyByName: 1
    importPhysicalCameras: 0
    importVisibility: 0
    importBlendShapes: 0
    importCameras: 0
    importLights: 0
    nodeNameCollisionStrategy: 1
    fileIdsGeneration: 2
    swapUVChannels: 0
    generateSecondaryUV: 0
    useFileUnits: 1
    keepQuads: 0
    weldVertices: 1
    bakeAxisConversion: 0
    preserveHierarchy: 0
    skinWeightsMode: 0
    maxBonesPerVertex: 4
    minBoneWeight: 0.001
    optimizeBones: 1
    generateMeshLods: 0
    meshLodGenerationFlags: 0
    maximumMeshLod: -1
    meshOptimizationFlags: -1
    indexFormat: 0
    secondaryUVAngleDistortion: 8
    secondaryUVAreaDistortion: 15.000001
    secondaryUVHardAngle: 88
    secondaryUVMarginMethod: 1
    secondaryUVMinLightmapResolution: 40
    secondaryUVMinObjectScale: 1
    secondaryUVPackMargin: 4
    useFileScale: 1
  tangentSpace:
    normalSmoothAngle: 60
    normalImportMode: 0
    tangentImportMode: 3
    normalCalculationMode: 4
    legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes: 0
    blendShapeNormalImportMode: 1
    normalSmoothingSource: 0
  referencedClips: []
  importAnimation: 0
  humanDescription:
    serializedVersion: 3
    human: []
    skeleton: []
    armTwist: 0.5
    foreArmTwist: 0.5
    upperLegTwist: 0.5
    legTwist: 0.5
    armStretch: 0.05
    legStretch: 0.05
    feetSpacing: 0
    globalScale: 1
    rootMotionBoneName: 
    hasTranslationDoF: 0
    hasExtraRoot: 0
    skeletonHasParents: 1
  lastHumanDescriptionAvatarSource: {{fileID: 0}}
  autoGenerateAvatarMappingIfUnspecified: 1
  animationType: 0
  humanoidOversampling: 1
  avatarSetup: 0
  addHumanoidExtraRootOnlyWhenUsingAvatar: 1
  importBlendShapeDeformPercent: 1
  remapMaterialsIfMaterialImportModeIsNone: 0
  additionalBone: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

DEFAULT_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def existing_guid(path):
    if os.path.exists(path):
        m = re.search(r"^guid: ([0-9a-f]{32})", open(path).read(), re.M)
        if m:
            return m.group(1)
    return uuid.uuid4().hex


def write(path, text):
    with open(path, "w", newline="\n") as fh:
        fh.write(text)
    print("wrote", path)


def main():
    folder, gun = sys.argv[1], sys.argv[2]
    externals = ""
    for name in sorted(MATERIALS):
        externals += (
            "  - first:\n"
            "      type: UnityEngine:Material\n"
            "      assembly: UnityEngine.CoreModule\n"
            "      name: %s\n"
            "    second: {fileID: 2100000, guid: %s, type: 2}\n" % (name, MATERIALS[name]))
    fbx = os.path.join(folder, gun + ".fbx.meta")
    write(fbx, FBX_TEMPLATE.format(guid=existing_guid(fbx), root_id=ROOT_IDS[gun], name=gun, externals=externals))
    for suffix in (".glb.meta", ".anchors.json.meta"):
        p = os.path.join(folder, gun + suffix)
        write(p, DEFAULT_TEMPLATE.format(guid=existing_guid(p)))


if __name__ == "__main__":
    main()
