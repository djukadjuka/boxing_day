"""
Boxing Day :: Blender pallet-parts builder.

Reads two TEMPLATE objects in the scene -- named exactly 'plank' and 'runner'
-- and, WITHOUT touching them, duplicates them into:
    * 5 textured deck planks  (Pallet_Plank_1..5)
    * 6 textured runners       (Pallet_Runner_1..6)

Each copy gets single-user mesh data, per-face materials assigned by face
normal, and UVs unwrapped so the wood grain (image X) runs along the long
(X) axis.

Geometry assumption (your dims): long axis = X.
  plank  = 4.0 x 0.5 x 0.15      runner = 4.0 x 0.5 x 0.5

Face mapping
  PLANK  (thinplank_*; the side texture is a normal long-edge strip)
      top/bottom (+Z/-Z)  -> thinplank_face_i
      front/back (+X/-X)  -> thinplank_face_i     (the small end caps)
      left/right (+Y/-Y)  -> thinplank_side_i     (the long edges)
  RUNNER (runner_*; route (b) -- end-grain on the REAL ends)
      top/bottom (+Z/-Z)  -> runner_face_i        (long grain)
      left/right (+Y/-Y)  -> runner_face_i        (long grain, the big faces)
      front/back (+X/-X)  -> runner_side_i        (sawn end-grain cap)

HOW TO RUN
  Scripting workspace -> Open this file -> Run Script (Alt+P).
  (No selection needed; it finds 'plank' and 'runner' by name.)

Re-runnable: it first deletes any previous Pallet_Plank_* / Pallet_Runner_*
copies, then rebuilds. The 'plank' and 'runner' templates are never modified.
"""

import os
import bmesh
import bpy
from mathutils import Vector

# ----------------------------------------------------------------- CONFIG
TEX_DIR = r"D:\BIG_Projects\boxing_day\BoxingDay\Assets\Textures\Pallets"

PLANK_TEMPLATE = "plank"
RUNNER_TEMPLATE = "runner"
N_PLANKS = 5
N_RUNNERS = 6

PLANK_PREFIX = "Pallet_Plank_"
RUNNER_PREFIX = "Pallet_Runner_"


def plank_map(i):
    face = "thinplank_face_%d.png" % i
    side = "thinplank_side_%d.png" % i
    return {"+Z": face, "-Z": face,       # top / bottom  (big flat faces)
            "+X": face, "-X": face,        # front / back  (end caps)
            "+Y": side, "-Y": side}        # left / right  (long edges)


def runner_map(i):
    face = "runner_face_%d.png" % i        # long grain
    end = "runner_side_%d.png" % i         # end-grain cap
    return {"+Z": face, "-Z": face,        # top / bottom
            "+Y": face, "-Y": face,        # left / right (the long faces)
            "+X": end, "-X": end}          # front / back (the real ends)


# ----------------------------------------------------------------- helpers
def dominant_dir(normal):
    ax = max(range(3), key=lambda k: abs(normal[k]))
    return ("+" if normal[ax] >= 0 else "-") + "XYZ"[ax]


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
    tex.interpolation = "Closest"          # crisp pixels
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    bsdf.inputs["Roughness"].default_value = 0.9
    for key in ("Specular IOR Level", "Specular"):   # 4.x vs 3.x
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = 0.1
            break
    out.location = (300, 0); bsdf.location = (0, 0); tex.location = (-350, 0)
    return mat


def axis_extents(bm):
    lo = [1e9, 1e9, 1e9]
    hi = [-1e9, -1e9, -1e9]
    for v in bm.verts:
        for k in range(3):
            lo[k] = min(lo[k], v.co[k])
            hi[k] = max(hi[k], v.co[k])
    return [hi[k] - lo[k] for k in range(3)]


def texture_object(obj, tex_map):
    """Build deduped materials, assign per face, and UV-unwrap."""
    obj.data.materials.clear()
    uniq = {}                              # filename -> material
    dir_mat = {}                           # direction -> material
    for d, fname in tex_map.items():
        if fname not in uniq:
            uniq[fname] = make_material("%s__%s" % (obj.name, fname[:-4]),
                                        load_image(fname))
        dir_mat[d] = uniq[fname]
    slots = list(dict.fromkeys(uniq.values()))
    for m in slots:
        obj.data.materials.append(m)
    dir_index = {d: slots.index(m) for d, m in dir_mat.items()}

    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    uv = bm.loops.layers.uv.verify()
    extent = axis_extents(bm)
    for face in bm.faces:
        d = dominant_dir(face.normal)
        ax = "XYZ".index(d[1])
        if d in dir_index:
            face.material_index = dir_index[d]
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


def duplicate(template, name, location):
    obj = template.copy()
    obj.data = template.data.copy()        # single-user mesh -> templates safe
    obj.animation_data_clear()
    obj.name = name
    obj.data.name = name
    cols = template.users_collection or [bpy.context.scene.collection]
    for c in cols:
        c.objects.link(obj)
    obj.location = location
    return obj


def purge(prefix):
    for o in [o for o in bpy.data.objects if o.name.startswith(prefix)]:
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data and data.users == 0:
            bpy.data.meshes.remove(data)


def require(name):
    obj = bpy.data.objects.get(name)
    if obj is None or obj.type != "MESH":
        raise RuntimeError("Template mesh '%s' not found in the scene." % name)
    return obj


# ----------------------------------------------------------------- main
def main():
    plank_tpl = require(PLANK_TEMPLATE)
    runner_tpl = require(RUNNER_TEMPLATE)

    purge(PLANK_PREFIX)
    purge(RUNNER_PREFIX)

    # planks fan out along +Y from the plank template; runners along -Y from
    # the runner template, so the two rows never collide and templates are clear.
    p0 = plank_tpl.location.copy()
    pstep = max(plank_tpl.dimensions.y, 0.3) * 1.4
    for i in range(N_PLANKS):
        name = "%s%d" % (PLANK_PREFIX, i + 1)
        obj = duplicate(plank_tpl, name, p0 + Vector((0, 1.0 + i * pstep, 0)))
        texture_object(obj, plank_map(i + 1))
        print("  %s -> %s" % (name, [s.name.split("__")[-1] for s in obj.data.materials]))

    r0 = runner_tpl.location.copy()
    rstep = max(runner_tpl.dimensions.y, 0.3) * 1.4
    for i in range(N_RUNNERS):
        name = "%s%d" % (RUNNER_PREFIX, i + 1)
        obj = duplicate(runner_tpl, name, r0 + Vector((0, -1.0 - i * rstep, 0)))
        texture_object(obj, runner_map(i + 1))
        print("  %s -> %s" % (name, [s.name.split("__")[-1] for s in obj.data.materials]))

    print("Done: built %d planks + %d runners (templates untouched)."
          % (N_PLANKS, N_RUNNERS))


if __name__ == "__main__":
    main()
