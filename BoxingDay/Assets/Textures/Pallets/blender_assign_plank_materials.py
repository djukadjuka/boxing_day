"""
Boxing Day :: Blender auto-material tool for pallet deck planks.

What it does, per plank, with NO manual clicking:
  * loads the plank PNG textures,
  * builds materials (pixel-art "Closest" filtering, matte),
  * assigns the right material to each of the 6 box faces *by face normal*
    (top/bottom get the face texture, the 4 edges get the side texture),
  * UV-maps every face to the full 0..1 image, oriented so the wood grain
    (image X) runs along the plank's LONG axis.

HOW TO RUN
  A) On your own meshes (recommended):
       1. Select your 5 plank objects in the viewport.
       2. Scripting workspace -> Open this file -> Run Script (Alt+P).
     Planks are matched to texture sets 1..5 in NAME order (sorted).
  B) Quick demo: run with nothing selected -> it builds 5 sample plank
     cuboids so you can see the result, then materials them.

Re-runnable: it clears each target's material slots and rebuilds, so tweak
the CONFIG below and just run again.

NOTE on "6 materials": a Blender material can cover many faces, so 1 face +
1 side texture = 2 materials fully skin a plank. Want all 6 faces distinct?
Give each direction its own filename in tex_map_for() below.
"""

import os
import bmesh
import bpy

# ----------------------------------------------------------------- CONFIG
# Folder holding the exported PNGs (edit if your .blend pulls from elsewhere).
TEX_DIR = r"D:\BIG_Projects\boxing_day\BoxingDay\Assets\Textures\Pallets"

PLANK_COUNT = 5  # how many texture sets exist (thinplank_face_1..N / _side_1..N)


def tex_map_for(i):
    """Direction -> texture filename for plank set i (1-based).
    Directions: +Z up(top), -Z down(bottom), +/-Y long edges, +/-X end caps.
    Default: face on top+bottom, side on all four edges."""
    face = "thinplank_face_%d.png" % i
    side = "thinplank_side_%d.png" % i
    return {"+Z": face, "-Z": face,
            "+Y": side, "-Y": side,
            "+X": side, "-X": side}


# ----------------------------------------------------------------- helpers
def dominant_dir(normal):
    """Return '+X'/'-X'/'+Y'/'-Y'/'+Z'/'-Z' and the axis index for a normal."""
    ax = max(range(3), key=lambda k: abs(normal[k]))
    sign = "+" if normal[ax] >= 0 else "-"
    return sign + "XYZ"[ax], ax


def load_image(filename):
    path = os.path.join(TEX_DIR, filename)
    if not os.path.exists(path):
        raise FileNotFoundError("Texture not found: " + path)
    return bpy.data.images.load(path, check_existing=True)


def make_material(name, image):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"          # crisp pixels, no blur
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    bsdf.inputs["Roughness"].default_value = 0.9
    for key in ("Specular IOR Level", "Specular"):   # name differs 3.x vs 4.x
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = 0.1
            break
    # tidy node layout
    out.location = (300, 0); bsdf.location = (0, 0); tex.location = (-350, 0)
    return mat


def axis_extents(bm):
    """Total local extent per axis, used to decide which in-plane axis is U."""
    lo = [1e9, 1e9, 1e9]
    hi = [-1e9, -1e9, -1e9]
    for v in bm.verts:
        for k in range(3):
            lo[k] = min(lo[k], v.co[k])
            hi[k] = max(hi[k], v.co[k])
    return [hi[k] - lo[k] for k in range(3)]


def setup_plank(obj, tex_map):
    # --- materials & slots (deduped by texture filename) ---
    obj.data.materials.clear()
    uniq = {}                  # filename -> material
    dir_mat = {}               # direction -> material
    for d, fname in tex_map.items():
        if fname not in uniq:
            img = load_image(fname)
            uniq[fname] = make_material("%s__%s" % (obj.name, fname[:-4]), img)
        dir_mat[d] = uniq[fname]
    slots = list(dict.fromkeys(uniq.values()))   # ordered unique materials
    for m in slots:
        obj.data.materials.append(m)
    dir_index = {d: slots.index(m) for d, m in dir_mat.items()}

    # --- per-face material index + UV unwrap ---
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    uv = bm.loops.layers.uv.verify()
    extent = axis_extents(bm)

    for face in bm.faces:
        d, ax = dominant_dir(face.normal)
        if d in dir_index:
            face.material_index = dir_index[d]
        # in-plane axes; U = the larger object dimension (grain runs along it)
        others = [k for k in range(3) if k != ax]
        u_ax, v_ax = (others if extent[others[0]] >= extent[others[1]]
                      else others[::-1])
        umin = min(l.vert.co[u_ax] for l in face.loops)
        umax = max(l.vert.co[u_ax] for l in face.loops)
        vmin = min(l.vert.co[v_ax] for l in face.loops)
        vmax = max(l.vert.co[v_ax] for l in face.loops)
        du = (umax - umin) or 1.0
        dv = (vmax - vmin) or 1.0
        for loop in face.loops:
            loop[uv].uv = ((loop.vert.co[u_ax] - umin) / du,
                           (loop.vert.co[v_ax] - vmin) / dv)

    bm.to_mesh(me)
    bm.free()
    me.update()
    print("  %s -> %d materials (%s)"
          % (obj.name, len(slots), ", ".join(m.name.split("__")[-1] for m in slots)))


def make_demo_planks():
    planks = []
    for i in range(PLANK_COUNT):
        bpy.ops.mesh.primitive_cube_add(size=1.0)
        o = bpy.context.active_object
        o.name = "DeckPlank_%d" % (i + 1)
        o.scale = (0.5, 0.06, 0.011)          # ~ 1.0 x 0.12 x 0.022 m
        o.location = (0.0, i * 0.18, 0.0)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        planks.append(o)
    return planks


# ----------------------------------------------------------------- main
def main():
    targets = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    if not targets:
        print("No mesh selected -> creating %d demo planks." % PLANK_COUNT)
        targets = make_demo_planks()
    targets.sort(key=lambda o: o.name)

    print("Texturing %d plank(s) from: %s" % (len(targets), TEX_DIR))
    for idx, obj in enumerate(targets):
        set_i = idx % PLANK_COUNT + 1        # cycle 1..PLANK_COUNT
        setup_plank(obj, tex_map_for(set_i))
    print("Done.")


if __name__ == "__main__":
    main()
