"""
Boxing Day :: Blender -> Unity pallet exporter.

Exports the 15 assembled pallets (the 'Assembled_Pallets' collection that
blender_assemble_pallets.py builds) to a single Unity-ready FBX:

    BoxingDay/Assets/Models/Pallets.fbx

Each pallet keeps its own name (Pallet_Asm_1..15) and its BOTTOM-CENTER origin,
so in Unity each becomes a clean child mesh pivoted at its base -- ready for the
'Boxing Day > Build Pallet Prefabs' editor tool to turn into prefab variants.

Why these settings:
  * bake_space_transform=True with axis_up=Y / axis_forward=-Z bakes Blender's
    Z-up into Unity's Y-up *in the mesh data itself*. That means the exported
    meshes are already upright, so the prefabs can sit at identity rotation
    (no -90 deg import wobble to undo). Safe here because the joined pallets
    have clean transforms (scale 1, rotation 0).
  * use_selection=True + object_types={'MESH'} -> only the pallets, nothing else.

HOW TO RUN
  Run blender_assemble_pallets.py first (so Assembled_Pallets exists), then:
  Scripting workspace -> Open this file -> Run Script (Alt+P).
"""

import os

import bpy

# ----------------------------------------------------------------- CONFIG
ASM_COLLECTION = "Assembled_Pallets"
OUT_PATH = r"D:\BIG_Projects\boxing_day\BoxingDay\Assets\Models\Pallets.fbx"


def main():
    coll = bpy.data.collections.get(ASM_COLLECTION)
    if coll is None or not coll.objects:
        raise RuntimeError(
            "Collection '%s' not found / empty. Run blender_assemble_pallets.py first."
            % ASM_COLLECTION)

    os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)

    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    # Select exactly the pallets.
    bpy.ops.object.select_all(action="DESELECT")
    meshes = [o for o in coll.objects if o.type == "MESH"]
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]

    bpy.ops.export_scene.fbx(
        filepath=OUT_PATH,
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="OFF",
        add_leaf_bones=False,
        bake_space_transform=True,      # bake Z-up -> Y-up into the mesh data
        axis_up="Y",
        axis_forward="-Z",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        path_mode="AUTO",
    )
    print("Exported %d pallets -> %s" % (len(meshes), OUT_PATH))


if __name__ == "__main__":
    main()
