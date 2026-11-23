#!/usr/bin/env python3
"""Writes Unity YAML assets and .meta files for Assets/Skins with stable GUIDs."""

from __future__ import annotations

import os
import shutil
import textwrap

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
REPO = os.path.abspath(os.path.join(ROOT, "..", ".."))
MEDIA = "/cursor/stores/bc-01a1fe41-b815-7474-ade5-6e56d6e6663b/media/skins"
PREVIEWS = "/tmp/skinprev"

G = {
    "folder_Skins": "132ab2bb6eb54e3c8222e297a3027d46",
    "folder_Scripts": "752ad1fb39a64af19871ae928d175be4",
    "folder_Shaders": "57ea5360945f44e9b179833c4fc255ec",
    "folder_Textures": "43c170408155459bbb5b768c2ab761f4",
    "folder_Materials": "247f7a673bf44437b76f6cdf91658fab",
    "folder_Definitions": "4fad3575b3de44f4aeeaf6aabf8e9179",
    "folder_Animated": "7d2fb15f033645eba50d5eb5b37eb6ac",
    "cs_WeaponSkinDefinition": "40c3bfe530754bafb16d4ffde8f0ed62",
    "cs_WeaponSkinApplier": "e72ded460720468694079be126375251",
    "shader_TriplanarStatic": "ad41480938c447dd9144143e52faceeb",
    "shader_ScrollingEnergy": "4affc68f0d5b40f9a92bdd9363f06c3a",
    "shader_PulsingEmissive": "5056d1a250ae4cce8c50fcc64ee6fa97",
    "shader_Holographic": "06831cc8f5a948b5932add094cfe5b7d",
    "shader_FlowingLava": "9071e237fc874b5e9512ae993911d369",
    "tex_WoodlandCamo": "8439f9d2115f45948b00a029b1a3c5f8",
    "tex_ArcticDigital": "f1865b3f6fc94161b4678a7c5fdc6bfc",
    "tex_Honeycomb": "d3911f07d08e4afeb68a1fb207a23967",
    "tex_Prism": "b555d65f6c0e47848511261482595e09",
    "tex_HalftonePop": "e3c31cd2328f42158b4e07f8b4745e3f",
    "tex_Damascus": "55fa57d49729443889893167ffd81e54",
    "tex_SkinNoise": "fd6c145835a048ef8f174a1ac8f5fa0d",
    "tex_SkinCircuitMask": "0338dec1c5c54abc81253945edef99a2",
    "mat_WoodlandCamo": "2d02f4fd906e44639c599edaa62bb8c4",
    "mat_ArcticDigital": "fd03c6bea7644563b072fc40d10a9cfe",
    "mat_Honeycomb": "3874d8c1c49b477eb16f1ab31bb8e1fa",
    "mat_Prism": "8330606ff3ab40eaa3c121ee2a78df3f",
    "mat_HalftonePop": "19458755ddd34d6c9119c51ad4aa7032",
    "mat_Damascus": "6255b2415f324cd2a1309fa434bfd697",
    "mat_ScrollingEnergy": "560a858728fa44478aeecc59cddd2117",
    "mat_PulsingEmissive": "cb61610c5c7644fa8030ddacd86b8639",
    "mat_Holographic": "24e105bb039f459591ab1a5cd0449fe7",
    "mat_FlowingLava": "43692544e4f84fe8a591f6167a8ddcc4",
    "so_WoodlandCamo": "048d579b08534a40a78e0d5cd4f2b792",
    "so_ArcticDigital": "b98855beb6754a51a8f0635105698e79",
    "so_Honeycomb": "948a340e10fd4580a82dc7fd2fce563f",
    "so_Prism": "f79c9f2a9fb04729b55724d422f90d39",
    "so_HalftonePop": "7ca7c19fa5f24a69ac2a4332e5aea811",
    "so_Damascus": "11c23a9489c9491a86f044323ba8efee",
    "so_ScrollingEnergy": "a7e6e189750f4d6eb33f4d4b55120d0e",
    "so_PulsingEmissive": "103493a425f24d2d8fa13214210bcc72",
    "so_Holographic": "35c677469db64db18435035a8379bd99",
    "so_FlowingLava": "54a2a129e08a4f28a6f83b13f0615cc8",
    "readme": "0cd98a35758244cb8f16306c07da4371",
}

RARITY = {"Common": 0, "Uncommon": 1, "Rare": 2, "Epic": 3, "Legendary": 4}


def write(path: str, contents: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(contents)


def folder_meta(guid: str) -> str:
    return textwrap.dedent(f"""\
        fileFormatVersion: 2
        guid: {guid}
        folderAsset: yes
        DefaultImporter:
          externalObjects: {{}}
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """)


def mono_meta(guid: str) -> str:
    return textwrap.dedent(f"""\
        fileFormatVersion: 2
        guid: {guid}
        MonoImporter:
          externalObjects: {{}}
          serializedVersion: 2
          defaultReferences: []
          executionOrder: 0
          icon: {{fileID: 0}}
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """)


def shader_meta(guid: str) -> str:
    return textwrap.dedent(f"""\
        fileFormatVersion: 2
        guid: {guid}
        ShaderImporter:
          externalObjects: {{}}
          defaultTextures: []
          nonModifiableTextures: []
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """)


def default_meta(guid: str) -> str:
    return textwrap.dedent(f"""\
        fileFormatVersion: 2
        guid: {guid}
        DefaultImporter:
          externalObjects: {{}}
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """)


def texture_meta(guid: str, *, srgb: bool, readable: bool = False) -> str:
    # sRGB for albedo skins; linear for data masks/noise.
    color_space = 1 if srgb else 0
    return textwrap.dedent(f"""\
        fileFormatVersion: 2
        guid: {guid}
        TextureImporter:
          internalIDToNameTable: []
          externalObjects: {{}}
          serializedVersion: 13
          mipmaps:
            mipMapMode: 0
            enableMipMap: 1
            sRGBTexture: {color_space}
            linearTexture: 0
            fadeOut: 0
            borderMipMap: 0
            mipMapsPreserveCoverage: 0
            alphaTestReferenceValue: 0.5
            mipMapFadeDistanceStart: 1
            mipMapFadeDistanceEnd: 3
          bumpmap:
            convertToNormalMap: 0
            externalNormalMap: 0
            heightScale: 0.25
            normalMapFilter: 0
            flipGreenChannel: 0
          isReadable: {1 if readable else 0}
          streamingMipmaps: 0
          streamingMipmapsPriority: 0
          vTOnly: 0
          ignoreMipmapLimit: 0
          grayScaleToAlpha: 0
          generateCubemap: 6
          cubemapConvolution: 0
          seamlessCubemap: 0
          textureFormat: 1
          maxTextureSize: 2048
          textureSettings:
            serializedVersion: 2
            filterMode: 1
            aniso: 4
            mipBias: 0
            wrapU: 0
            wrapV: 0
            wrapW: 0
          nPOTScale: 1
          lightmap: 0
          compressionQuality: 50
          spriteMode: 0
          spriteExtrude: 1
          spriteMeshType: 1
          alignment: 0
          spritePivot: {{x: 0.5, y: 0.5}}
          spritePixelsToUnits: 100
          spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
          spriteGenerateFallbackPhysicsShape: 1
          alphaUsage: 1
          alphaIsTransparency: 0
          spriteTessellationDetail: -1
          textureType: 0
          textureShape: 1
          singleChannelComponent: 0
          flipbookRows: 1
          flipbookColumns: 1
          maxTextureSizeSet: 0
          compressionQualitySet: 0
          textureFormatSet: 0
          ignorePngGamma: 0
          applyGammaDecoding: 0
          swizzle: 50462976
          cookieLightType: 0
          platformSettings:
          - serializedVersion: 3
            buildTarget: DefaultTexturePlatform
            maxTextureSize: 2048
            resizeAlgorithm: 0
            textureFormat: -1
            textureCompression: 1
            compressionQuality: 50
            crunchedCompression: 0
            allowsAlphaSplitting: 0
            overridden: 0
            ignorePlatformSupport: 0
            androidETC2FallbackOverride: 0
            forceMaximumCompressionQuality_BC7: 0
          spriteSheet:
            serializedVersion: 2
            sprites: []
            outline: []
            physicsShape: []
            bones: []
            spriteID: 
            internalID: 0
            vertices: []
            indices: 
            edges: []
            weights: []
            secondaryTextures: []
            nameFileIdTable: {{}}
          mipmapLimitGroupName: 
          pSDRemoveMatte: 0
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """)


def native_meta(guid: str) -> str:
    return textwrap.dedent(f"""\
        fileFormatVersion: 2
        guid: {guid}
        NativeFormatImporter:
          externalObjects: {{}}
          mainObjectFileID: 2100000
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """)


def scriptable_meta(guid: str) -> str:
    return textwrap.dedent(f"""\
        fileFormatVersion: 2
        guid: {guid}
        NativeFormatImporter:
          externalObjects: {{}}
          mainObjectFileID: 11400000
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """)


def color(r, g, b, a=1.0) -> str:
    return f"{{r: {r}, g: {g}, b: {b}, a: {a}}}"


def material_yaml(
    name: str,
    shader_guid: str,
    floats: dict[str, float],
    colors: dict[str, tuple],
    textures: dict[str, str | None],
) -> str:
    tex_lines = []
    for prop, tex_guid in textures.items():
        if tex_guid:
            tex_ref = f"{{fileID: 2800000, guid: {tex_guid}, type: 3}}"
        else:
            tex_ref = "{fileID: 0}"
        tex_lines.append(
            f"    - {prop}:\n"
            f"        m_Texture: {tex_ref}\n"
            f"        m_Scale: {{x: 1, y: 1}}\n"
            f"        m_Offset: {{x: 0, y: 0}}"
        )

    float_lines = [f"    - {k}: {v}" for k, v in floats.items()]
    color_lines = []
    for k, (r, g, b, a) in colors.items():
        color_lines.append(f"    - {k}: {{r: {r}, g: {g}, b: {b}, a: {a}}}")

    return (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!21 &2100000\n"
        "Material:\n"
        "  serializedVersion: 8\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_Name: {name}\n"
        f"  m_Shader: {{fileID: 4800000, guid: {shader_guid}, type: 3}}\n"
        "  m_Parent: {fileID: 0}\n"
        "  m_ModifiedSerializedProperties: 0\n"
        "  m_ValidKeywords: []\n"
        "  m_InvalidKeywords: []\n"
        "  m_LightmapFlags: 4\n"
        "  m_EnableInstancingVariants: 0\n"
        "  m_DoubleSidedGI: 0\n"
        "  m_CustomRenderQueue: -1\n"
        "  stringTagMap: {}\n"
        "  disabledShaderPasses: []\n"
        "  m_LockedProperties: \n"
        "  m_SavedProperties:\n"
        "    serializedVersion: 3\n"
        "    m_TexEnvs:\n"
        + "\n".join(tex_lines)
        + "\n"
        "    m_Ints: []\n"
        "    m_Floats:\n"
        + "\n".join(float_lines)
        + "\n"
        "    m_Colors:\n"
        + "\n".join(color_lines)
        + "\n"
        "  m_BuildTextureStacks: []\n"
        "  m_AllowLocking: 1\n"
    )


def skin_asset(
    name: str,
    display: str,
    description: str,
    rarity: str,
    material_guid: str,
    *,
    overrides: bool,
    skin_color=(1, 1, 1, 1),
    accent=(1, 1, 1, 1),
    speed=1.0,
    intensity=1.0,
    scale=1.0,
) -> str:
    sc = skin_color
    ac = accent
    return (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {G['cs_WeaponSkinDefinition']}, type: 3}}\n"
        "  m_Name: "
        f"{name}\n"
        "  m_EditorClassIdentifier: \n"
        f"  skinName: {display}\n"
        f"  description: {description}\n"
        f"  rarity: {RARITY[rarity]}\n"
        "  icon: {fileID: 0}\n"
        f"  material: {{fileID: 2100000, guid: {material_guid}, type: 2}}\n"
        f"  applyParameterOverrides: {1 if overrides else 0}\n"
        f"  skinColor: {{r: {sc[0]}, g: {sc[1]}, b: {sc[2]}, a: {sc[3]}}}\n"
        f"  accentColor: {{r: {ac[0]}, g: {ac[1]}, b: {ac[2]}, a: {ac[3]}}}\n"
        f"  speed: {speed}\n"
        f"  intensity: {intensity}\n"
        f"  scale: {scale}\n"
    )


def readme() -> str:
    return textwrap.dedent(
        """\
        # Weapon Skins

        Mesh-agnostic weapon skins for the Built-in Render Pipeline (Unity 6).
        Materials project tileable textures in **object-space triplanar**, so any
        gun mesh works without custom UVs.

        ## Layout

        | Folder | Contents |
        | --- | --- |
        | `Scripts/` | `WeaponSkinDefinition` (ScriptableObject) and `WeaponSkinApplier` |
        | `Shaders/` | Built-in surface shaders (`Skins/*`) |
        | `Textures/` | Generated tileable PNGs (+ data maps for animated skins) |
        | `Materials/` | Ready-to-use materials |
        | `Definitions/` | Skin assets (static + `Animated/`) |
        | `Tools~/` | Python texture generator (ignored by Unity) |

        ## Equip a skin at runtime

        1. Add `WeaponSkinApplier` to the weapon root (same object as `WeaponController`, or a parent).
        2. Assign a default `WeaponSkinDefinition`, or call from code:

        ```csharp
        var applier = weapon.GetComponent<WeaponSkinApplier>();
        applier.ApplySkin(skinDefinition);   // apply
        applier.ClearSkin();                 // restore original materials
        ```

        The applier replaces materials on Mesh/SkinnedMesh renderers under the
        target root and skips particle/trail/line renderers.

        ## Add a static skin

        1. Create a seamless tileable albedo PNG (512² or 1024², wrap mode Repeat).
           Prefer object-space-friendly patterns: camo, noise, geometric lattices.
           You can extend `Tools~/generate_skin_textures.py` and re-run it.
        2. Drop the PNG in `Textures/` and let Unity import it (sRGB, mipmaps on).
        3. Duplicate `Materials/Mat_Skin_WoodlandCamo.mat` (or create a material
           with shader `Skins/TriplanarStatic`), assign your texture to `_MainTex`,
           tune `_Scale`, `_Metallic`, `_Glossiness`.
        4. **Assets → Create → Skins → Weapon Skin**. Point `material` at the new
           material. Set name / rarity. Leave `applyParameterOverrides` off unless
           you want runtime tint/scale overrides.
        5. Save under `Definitions/`.

        ## Add an animated skin

        1. Pick a shader under `Skins/`:
           - `ScrollingEnergy` — circuit traces scroll; uses `SkinCircuitMask` + `SkinNoise`
           - `PulsingEmissive` — cell glow pulse; uses `SkinNoise`
           - `Holographic` — fresnel + iridescent shift; uses `SkinNoise`
           - `FlowingLava` — dual-layer flowing heat; uses `SkinNoise`
        2. Create a material with that shader, assign the data textures, set
           `_Speed`, `_Intensity`, colours.
        3. Create a `WeaponSkinDefinition` under `Definitions/Animated/`.
           Enable `applyParameterOverrides` if loadouts should drive speed/colour/intensity
           through the ScriptableObject fields (`speed`, `skinColor`, `accentColor`, `intensity`, `scale`).

        Shared property names (`_SkinColor`, `_EnergyColor` / `_EmissionColor` /
        `_AccentColor`, `_Speed`, `_Intensity`, `_Scale`) are written by
        `WeaponSkinDefinition.WritePropertyBlock`.

        ## Notes

        - Pipeline: **Built-in** (`m_CustomRenderPipeline` is unset). These shaders
          will not work as-is under URP/HDRP.
        - Shaders / materials were authored without the Unity editor and are
          **uncompiled and unverified** in-engine until opened locally.
        - Preview images for the static textures live in the project store under
          `media/skins/`.
        """
    )


def main() -> None:
    # Folders
    write(os.path.join(ROOT, "Scripts.meta"), folder_meta(G["folder_Scripts"]))
    write(os.path.join(ROOT, "Shaders.meta"), folder_meta(G["folder_Shaders"]))
    write(os.path.join(ROOT, "Textures.meta"), folder_meta(G["folder_Textures"]))
    write(os.path.join(ROOT, "Materials.meta"), folder_meta(G["folder_Materials"]))
    write(os.path.join(ROOT, "Definitions.meta"), folder_meta(G["folder_Definitions"]))
    write(os.path.join(ROOT, "Definitions", "Animated.meta"), folder_meta(G["folder_Animated"]))
    write(os.path.join(REPO, "Assets", "Skins.meta"), folder_meta(G["folder_Skins"]))

    # Scripts
    write(os.path.join(ROOT, "Scripts", "WeaponSkinDefinition.cs.meta"), mono_meta(G["cs_WeaponSkinDefinition"]))
    write(os.path.join(ROOT, "Scripts", "WeaponSkinApplier.cs.meta"), mono_meta(G["cs_WeaponSkinApplier"]))

    # Shaders
    for key, filename in [
        ("shader_TriplanarStatic", "TriplanarStatic.shader"),
        ("shader_ScrollingEnergy", "ScrollingEnergy.shader"),
        ("shader_PulsingEmissive", "PulsingEmissive.shader"),
        ("shader_Holographic", "Holographic.shader"),
        ("shader_FlowingLava", "FlowingLava.shader"),
    ]:
        write(os.path.join(ROOT, "Shaders", filename + ".meta"), shader_meta(G[key]))

    # Textures
    static_tex = [
        ("tex_WoodlandCamo", "Skin_WoodlandCamo.png", True),
        ("tex_ArcticDigital", "Skin_ArcticDigital.png", True),
        ("tex_Honeycomb", "Skin_Honeycomb.png", True),
        ("tex_Prism", "Skin_Prism.png", True),
        ("tex_HalftonePop", "Skin_HalftonePop.png", True),
        ("tex_Damascus", "Skin_Damascus.png", True),
        ("tex_SkinNoise", "SkinNoise.png", False),
        ("tex_SkinCircuitMask", "SkinCircuitMask.png", False),
    ]
    for key, filename, srgb in static_tex:
        write(os.path.join(ROOT, "Textures", filename + ".meta"), texture_meta(G[key], srgb=srgb))

    # Static materials
    static_mats = [
        ("Mat_Skin_WoodlandCamo", "mat_WoodlandCamo", "tex_WoodlandCamo", 1.0, 0.12, 0.28),
        ("Mat_Skin_ArcticDigital", "mat_ArcticDigital", "tex_ArcticDigital", 1.15, 0.18, 0.4),
        ("Mat_Skin_Honeycomb", "mat_Honeycomb", "tex_Honeycomb", 1.35, 0.55, 0.55),
        ("Mat_Skin_Prism", "mat_Prism", "tex_Prism", 1.1, 0.25, 0.62),
        ("Mat_Skin_HalftonePop", "mat_HalftonePop", "tex_HalftonePop", 1.0, 0.05, 0.3),
        ("Mat_Skin_Damascus", "mat_Damascus", "tex_Damascus", 1.25, 0.85, 0.72),
    ]
    for name, mat_key, tex_key, scale, metallic, gloss in static_mats:
        yaml = material_yaml(
            name,
            G["shader_TriplanarStatic"],
            {
                "_BlendSharpness": 4.0,
                "_Glossiness": gloss,
                "_Metallic": metallic,
                "_Scale": scale,
                "_Speed": 0.0,
                "_Intensity": 1.0,
            },
            {
                "_Color": (1, 1, 1, 1),
                "_SkinColor": (1, 1, 1, 1),
                "_EnergyColor": (1, 1, 1, 1),
                "_EmissionColor": (0, 0, 0, 1),
                "_AccentColor": (1, 1, 1, 1),
            },
            {
                "_MainTex": G[tex_key],
                "_BumpMap": None,
                "_DetailAlbedoMap": None,
                "_DetailMask": None,
                "_DetailNormalMap": None,
                "_EmissionMap": None,
                "_MetallicGlossMap": None,
                "_OcclusionMap": None,
                "_ParallaxMap": None,
            },
        )
        path = os.path.join(ROOT, "Materials", name + ".mat")
        write(path, yaml)
        write(path + ".meta", native_meta(G[mat_key]))

    # Animated materials
    animated = [
        (
            "Mat_Skin_ScrollingEnergy",
            "mat_ScrollingEnergy",
            G["shader_ScrollingEnergy"],
            {
                "_BlendSharpness": 4.0,
                "_Glossiness": 0.55,
                "_Metallic": 0.65,
                "_Scale": 1.25,
                "_Speed": 1.5,
                "_Intensity": 2.0,
            },
            {
                "_Color": (0.05, 0.07, 0.1, 1),
                "_SkinColor": (0.05, 0.07, 0.1, 1),
                "_EnergyColor": (0.2, 1.0, 0.55, 1),
                "_AccentColor": (0.2, 1.0, 0.55, 1),
                "_EmissionColor": (0.2, 1.0, 0.55, 1),
            },
            {"_CircuitTex": G["tex_SkinCircuitMask"], "_NoiseTex": G["tex_SkinNoise"], "_MainTex": None},
        ),
        (
            "Mat_Skin_PulsingEmissive",
            "mat_PulsingEmissive",
            G["shader_PulsingEmissive"],
            {
                "_BlendSharpness": 4.0,
                "_Glossiness": 0.45,
                "_Metallic": 0.4,
                "_Scale": 1.0,
                "_Speed": 1.25,
                "_Intensity": 2.5,
            },
            {
                "_Color": (0.08, 0.08, 0.1, 1),
                "_SkinColor": (0.08, 0.08, 0.1, 1),
                "_EnergyColor": (0.35, 0.75, 1.0, 1),
                "_AccentColor": (0.35, 0.75, 1.0, 1),
                "_EmissionColor": (0.35, 0.75, 1.0, 1),
            },
            {"_NoiseTex": G["tex_SkinNoise"], "_MainTex": None},
        ),
        (
            "Mat_Skin_Holographic",
            "mat_Holographic",
            G["shader_Holographic"],
            {
                "_BlendSharpness": 4.0,
                "_FresnelPower": 3.0,
                "_Glossiness": 0.85,
                "_Metallic": 0.2,
                "_Scale": 1.5,
                "_Speed": 1.0,
                "_Intensity": 1.75,
            },
            {
                "_Color": (0.05, 0.12, 0.18, 1),
                "_SkinColor": (0.05, 0.12, 0.18, 1),
                "_EnergyColor": (1.0, 0.25, 0.85, 1),
                "_AccentColor": (0.2, 0.95, 1.0, 1),
                "_EmissionColor": (0.4, 0.9, 1.0, 1),
            },
            {"_NoiseTex": G["tex_SkinNoise"], "_MainTex": None},
        ),
        (
            "Mat_Skin_FlowingLava",
            "mat_FlowingLava",
            G["shader_FlowingLava"],
            {
                "_BlendSharpness": 4.0,
                "_Glossiness": 0.25,
                "_Metallic": 0.05,
                "_Scale": 0.85,
                "_Speed": 0.85,
                "_Intensity": 3.0,
            },
            {
                "_Color": (0.08, 0.04, 0.03, 1),
                "_SkinColor": (0.08, 0.04, 0.03, 1),
                "_EnergyColor": (1.0, 0.35, 0.05, 1),
                "_AccentColor": (0.45, 0.12, 0.04, 1),
                "_EmissionColor": (1.0, 0.55, 0.1, 1),
            },
            {"_NoiseTex": G["tex_SkinNoise"], "_MainTex": None},
        ),
    ]
    for name, mat_key, shader_guid, floats, colors, textures in animated:
        path = os.path.join(ROOT, "Materials", name + ".mat")
        write(path, material_yaml(name, shader_guid, floats, colors, textures))
        write(path + ".meta", native_meta(G[mat_key]))

    # Definitions
    static_defs = [
        ("Skin_WoodlandCamo", "so_WoodlandCamo", "mat_WoodlandCamo", "Woodland Camo", "Organic forest camo blotches. Common static skin.", "Common", 1.0),
        ("Skin_ArcticDigital", "so_ArcticDigital", "mat_ArcticDigital", "Arctic Digital", "Pixel digital camo in cold greys.", "Uncommon", 1.15),
        ("Skin_Honeycomb", "so_Honeycomb", "mat_Honeycomb", "Honeycomb", "Dark hexagonal plating with sparse amber cells.", "Rare", 1.35),
        ("Skin_Prism", "so_Prism", "mat_Prism", "Prism", "Low-poly crystalline facets in cool hues.", "Rare", 1.1),
        ("Skin_HalftonePop", "so_HalftonePop", "mat_HalftonePop", "Halftone Pop", "Comic halftone blobs in CMY colours.", "Uncommon", 1.0),
        ("Skin_Damascus", "so_Damascus", "mat_Damascus", "Damascus", "Folded steel banding with a metallic finish.", "Epic", 1.25),
    ]
    for asset, so_key, mat_key, display, desc, rarity, scale in static_defs:
        path = os.path.join(ROOT, "Definitions", asset + ".asset")
        write(
            path,
            skin_asset(
                asset,
                display,
                desc,
                rarity,
                G[mat_key],
                overrides=False,
                scale=scale,
            ),
        )
        write(path + ".meta", scriptable_meta(G[so_key]))

    animated_defs = [
        (
            "Skin_ScrollingEnergy",
            "so_ScrollingEnergy",
            "mat_ScrollingEnergy",
            "Scrolling Energy",
            "Animated circuit energy scroll. Tweak speed, accent colour, intensity.",
            "Epic",
            True,
            (0.05, 0.07, 0.1, 1),
            (0.2, 1.0, 0.55, 1),
            1.5,
            2.0,
            1.25,
        ),
        (
            "Skin_PulsingEmissive",
            "so_PulsingEmissive",
            "mat_PulsingEmissive",
            "Pulsing Emissive",
            "Cell-driven emissive pulse across the weapon body.",
            "Epic",
            True,
            (0.08, 0.08, 0.1, 1),
            (0.35, 0.75, 1.0, 1),
            1.25,
            2.5,
            1.0,
        ),
        (
            "Skin_Holographic",
            "so_Holographic",
            "mat_Holographic",
            "Holographic",
            "Iridescent holographic shift with fresnel edges and scan lines.",
            "Legendary",
            True,
            (0.05, 0.12, 0.18, 1),
            (0.2, 0.95, 1.0, 1),
            1.0,
            1.75,
            1.5,
        ),
        (
            "Skin_FlowingLava",
            "so_FlowingLava",
            "mat_FlowingLava",
            "Flowing Lava",
            "Crust and flowing magma with flickering heat.",
            "Legendary",
            True,
            (0.08, 0.04, 0.03, 1),
            (1.0, 0.35, 0.05, 1),
            0.85,
            3.0,
            0.85,
        ),
    ]
    for asset, so_key, mat_key, display, desc, rarity, overrides, skin_c, accent, speed, intensity, scale in animated_defs:
        path = os.path.join(ROOT, "Definitions", "Animated", asset + ".asset")
        write(
            path,
            skin_asset(
                asset,
                display,
                desc,
                rarity,
                G[mat_key],
                overrides=overrides,
                skin_color=skin_c,
                accent=accent,
                speed=speed,
                intensity=intensity,
                scale=scale,
            ),
        )
        write(path + ".meta", scriptable_meta(G[so_key]))

    # README
    write(os.path.join(ROOT, "README.md"), readme())
    write(os.path.join(ROOT, "README.md.meta"), default_meta(G["readme"]))

    # Media previews
    os.makedirs(MEDIA, exist_ok=True)
    mapping = [
        ("woodlandcamo.png", "woodland-camo.png"),
        ("arcticdigital.png", "arctic-digital.png"),
        ("honeycomb.png", "honeycomb.png"),
        ("prism.png", "prism.png"),
        ("halftonepop.png", "halftone-pop.png"),
        ("damascus.png", "damascus.png"),
        ("contact-sheet.png", "contact-sheet.png"),
    ]
    for src_name, dest_name in mapping:
        src = os.path.join(PREVIEWS, src_name)
        dest = os.path.join(MEDIA, dest_name)
        if not os.path.isfile(src):
            raise SystemExit(f"Missing preview source: {src}")
        shutil.copy2(src, dest)
        print("preview", dest, os.path.getsize(dest))

    print("Assets written under", ROOT)


if __name__ == "__main__":
    main()
