"""
Boxing Day :: Blender pallet ASSEMBLER.

Builds N complete wooden pallets by duplicating and arranging the textured
variety library that `blender_build_pallet_parts.py` produced:
    sources: Pallet_Plank_1..5   (native 4.0 x 0.5 x 0.15, long axis = X)
             Pallet_Runner_1..6  (native 4.0 x 0.5 x 0.5,  long axis = X)
These source objects (and the 'plank'/'runner' templates) are NEVER modified --
every board is a single-user copy.

Each pallet (12 boards), bottom -> top:
    * 4 bottom deck planks   (planks long axis = X, spread across Y)
    * 3 runners              (long axis = Y, one each side + one middle)
    * 5 top deck planks      (long axis = X, spread across Y)
Top & bottom planks share an axis; the runners run on the perpendicular one.
Height is exactly RUNNER_H + 2*PLANK_H  (0.5 + 2*0.15 = 0.80) -- one runner +
two plank thicknesses, no overlap.

Variation (the "hand-nailed" look), all WITHOUT scaling any board:
    * Footprint: fixed at FOOTPRINT (4.0) -- the board length -- so planks and
      runners line up flush. Boards are clamped so nothing ever crosses
      +/- FOOTPRINT/2; a pallet is never wider than 4.0 on either axis.
    * Placement jitter: every plank gets a small sideways nudge (clamped by
      MIN_GAP so no two boards ever sit close, and by the footprint so the
      outer boards can't poke past the edge). Runners get a small nudge too.
      Boards run the full footprint length, so there's no along-length slide.
    * Variety: per pallet a base index is rolled and every plank draws from a
      window of VARIETY_WINDOW consecutive variety indices (runners likewise,
      independent window) -- coherent wood "age" per pallet, slight spread.
    * Orientation: each board may be flipped 180 deg about X, Y and/or Z
      (independent coin flips). A 180 deg turn about a principal axis leaves the
      footprint identical, so it only changes which end/face shows.

Output: N joined objects named Pallet_Asm_1..N, each with its origin at
BOTTOM-CENTER (X/Y centered, Z at the base) so it drops straight onto a floor or
shelf in Unity. They're laid out on an inspection grid clear of the library.

Deterministic: re-running with the same SEED rebuilds identical pallets. It
purges its own previous Pallet_Asm_* output first, so just tweak the CONFIG and
run again.

HOW TO RUN
  Scripting workspace -> Open this file -> Run Script (Alt+P).
  (Needs the Pallet_Plank_* / Pallet_Runner_* objects already in the scene.)
"""

import math
import random

import bpy
from mathutils import Matrix, Vector

# ----------------------------------------------------------------- CONFIG
N_PALLETS = 15
SEED = 0                       # change for a different random batch

ASSEMBLY_PREFIX = "Pallet_Asm_"
ASM_COLLECTION = "Assembled_Pallets"

PLANK_SRC_PREFIX = "Pallet_Plank_"
RUNNER_SRC_PREFIX = "Pallet_Runner_"

# Native board dimensions -- must match what the build script produced.
PLANK_LEN, PLANK_W, PLANK_H = 4.0, 0.5, 0.15
RUNNER_LEN, RUNNER_W, RUNNER_H = 4.0, 0.5, 0.5

N_TOP, N_BOTTOM, N_RUNNERS = 5, 4, 3

# Layer centres (pallet bottom sits at local z = 0).
Z_BOTTOM = PLANK_H / 2.0                      # bottom deck
Z_RUNNER = PLANK_H + RUNNER_H / 2.0           # runners on the bottom deck
Z_TOP = PLANK_H + RUNNER_H + PLANK_H / 2.0    # top deck on the runners
PALLET_HEIGHT = PLANK_H + RUNNER_H + PLANK_H  # 0.80, for reference

# Footprint / placement (NO scaling -- jitter only).
FOOTPRINT = 4.0                     # fixed pallet extent; boards never cross +/- FOOTPRINT/2
MIN_GAP = 0.18                      # min clear gap between adjacent planks
CROSS_JIT = 0.10                    # plank sideways jitter (clamped by MIN_GAP + footprint)
RUNNER_SIDE = FOOTPRINT / 2.0 - RUNNER_W / 2.0   # side-runner cross offset (=1.75)
RUNNER_CROSS_JIT = 0.07             # runner sideways jitter (clamped to footprint)

VARIETY_WINDOW = 3                  # planks/runners drawn from this many consecutive indices
FLIP_CHANCE = 0.5                   # per-axis chance of a 180 deg flip

# Inspection grid for the finished pallets (kept clear of the source library).
GRID_ORIGIN = Vector((14.0, 0.0, 0.0))
GRID_COLS = 5
GRID_SPACING = 6.5


# ----------------------------------------------------------------- helpers
def count_sources(prefix):
    """How many Pallet_<Prefix>_1..N mesh objects exist (contiguous from 1)."""
    n = 0
    while True:
        o = bpy.data.objects.get("%s%d" % (prefix, n + 1))
        if o is None or o.type != "MESH":
            break
        n += 1
    return n


def source(prefix, idx):
    return bpy.data.objects["%s%d" % (prefix, idx)]


def window_indices(base, count, total):
    """A clamped window of VARIETY_WINDOW consecutive indices starting at base."""
    lo = base
    hi = min(total, base + count - 1)
    lo = max(1, hi - count + 1)
    return list(range(lo, hi + 1))


def new_board(src, coll, location, rot):
    """Single-user copy of a source board, centred on its own bbox, placed."""
    obj = src.copy()
    me = src.data.copy()                 # single-user mesh -> sources stay safe
    obj.data = me
    obj.animation_data_clear()

    # Bake the source's own rotation+scale into the mesh so the copy keeps its
    # true world dimensions. The templates size their boards via object scale;
    # setting matrix_world below would otherwise discard that and collapse every
    # board to its raw (unit-cube) mesh -- the "tiny crosses / stretched" bug.
    me.transform(src.matrix_world.to_3x3().to_4x4())   # rotation+scale, no translation

    co = [v.co for v in me.vertices]
    mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    me.transform(Matrix.Translation(-(mn + mx) / 2.0))   # origin -> geometric centre

    coll.objects.link(obj)
    obj.matrix_world = Matrix.Translation(location) @ rot.to_4x4()
    obj.name = "tmp_board"
    return obj


def flip_rotation(base_z_deg, rng):
    """base yaw (0 for planks, 90 for runners) + random 180 deg flips per axis."""
    rot = Matrix.Rotation(math.radians(base_z_deg), 4, "Z")
    for axis in "XYZ":
        if rng.random() < FLIP_CHANCE:
            rot = Matrix.Rotation(math.pi, 4, axis) @ rot
    return rot


def even_centres(count, span):
    """`count` plank centres spread so the outer edges hit +/- span/2."""
    half = span / 2.0 - PLANK_W / 2.0
    if count == 1:
        return [0.0], 0.0
    step = (2.0 * half) / (count - 1)
    return [-half + i * step for i in range(count)], step


def deck_rows(count, layer_z, rng, plank_window):
    """Build (src_obj, location, rot) specs for one deck (top or bottom).
    Planks span the full FOOTPRINT length, so they only jitter sideways."""
    centres, step = even_centres(count, FOOTPRINT)
    gap = step - PLANK_W
    cross_j = max(0.0, min(CROSS_JIT, (gap - MIN_GAP) / 2.0))   # never close two boards
    limit = FOOTPRINT / 2.0 - PLANK_W / 2.0                     # keep edges inside footprint
    specs = []
    for cy in centres:
        idx = rng.choice(plank_window)
        y = cy + rng.uniform(-cross_j, cross_j)
        y = max(-limit, min(limit, y))                          # never poke past +/- 2.0
        loc = Vector((0.0, y, layer_z))                         # full-length -> no slide
        specs.append((source(PLANK_SRC_PREFIX, idx),
                      loc, flip_rotation(0.0, rng)))
    return specs


def runner_row(rng, runner_window):
    """Runners span the full FOOTPRINT length, so they only jitter sideways."""
    limit = FOOTPRINT / 2.0 - RUNNER_W / 2.0
    specs = []
    for cx in (-RUNNER_SIDE, 0.0, RUNNER_SIDE):
        idx = rng.choice(runner_window)
        x = cx + rng.uniform(-RUNNER_CROSS_JIT, RUNNER_CROSS_JIT)
        x = max(-limit, min(limit, x))
        loc = Vector((x, 0.0, Z_RUNNER))                        # full-length -> no slide
        specs.append((source(RUNNER_SRC_PREFIX, idx),
                      loc, flip_rotation(90.0, rng)))   # 90 deg -> long axis along Y
    return specs


def set_bottom_center_origin(obj, scene):
    """Move obj's origin to (footprint centre X/Y, base Z)."""
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    xs = [c.x for c in corners]
    ys = [c.y for c in corners]
    zs = [c.z for c in corners]
    target = Vector(((min(xs) + max(xs)) / 2.0, (min(ys) + max(ys)) / 2.0, min(zs)))
    prev = scene.cursor.location.copy()
    scene.cursor.location = target
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    scene.cursor.location = prev


def get_assembly_collection(scene):
    coll = bpy.data.collections.get(ASM_COLLECTION)
    if coll is None:
        coll = bpy.data.collections.new(ASM_COLLECTION)
        scene.collection.children.link(coll)
    return coll


def purge():
    dead = [o for o in bpy.data.objects
            if o.name.startswith(ASSEMBLY_PREFIX) or o.name.startswith("tmp_board")]
    for o in dead:
        me = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if isinstance(me, bpy.types.Mesh) and me.users == 0:
            bpy.data.meshes.remove(me)


def build_pallet(index, world_origin, coll, scene, rng, n_plank_src, n_runner_src):
    plank_base = rng.randint(1, max(1, n_plank_src - VARIETY_WINDOW + 1))
    runner_base = rng.randint(1, max(1, n_runner_src - VARIETY_WINDOW + 1))
    plank_window = window_indices(plank_base, VARIETY_WINDOW, n_plank_src)
    runner_window = window_indices(runner_base, VARIETY_WINDOW, n_runner_src)

    specs = []
    specs += deck_rows(N_BOTTOM, Z_BOTTOM, rng, plank_window)
    specs += runner_row(rng, runner_window)
    specs += deck_rows(N_TOP, Z_TOP, rng, plank_window)

    boards = [new_board(src, coll, world_origin + loc, rot) for src, loc, rot in specs]

    # Join into a single clean-transform object via an empty mesh at the origin.
    me = bpy.data.meshes.new("%s%d" % (ASSEMBLY_PREFIX, index))
    base = bpy.data.objects.new("%s%d" % (ASSEMBLY_PREFIX, index), me)
    base.location = world_origin
    coll.objects.link(base)

    bpy.ops.object.select_all(action="DESELECT")
    for b in boards:
        b.select_set(True)
    base.select_set(True)
    bpy.context.view_layer.objects.active = base
    bpy.ops.object.join()

    set_bottom_center_origin(base, scene)
    print("  %s%d  planks%s  runners%s"
          % (ASSEMBLY_PREFIX, index, plank_window, runner_window))
    return base


# ----------------------------------------------------------------- main
def main():
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    n_plank_src = count_sources(PLANK_SRC_PREFIX)
    n_runner_src = count_sources(RUNNER_SRC_PREFIX)
    if n_plank_src == 0 or n_runner_src == 0:
        raise RuntimeError(
            "No source boards found. Run blender_build_pallet_parts.py first "
            "(need %s1.. and %s1.. in the scene)."
            % (PLANK_SRC_PREFIX, RUNNER_SRC_PREFIX))

    scene = bpy.context.scene
    rng = random.Random(SEED)
    purge()
    coll = get_assembly_collection(scene)

    print("Assembling %d pallets from %d plank + %d runner varieties (seed %d)..."
          % (N_PALLETS, n_plank_src, n_runner_src, SEED))
    for i in range(N_PALLETS):
        col = i % GRID_COLS
        row = i // GRID_COLS
        origin = GRID_ORIGIN + Vector((col * GRID_SPACING, row * GRID_SPACING, 0.0))
        build_pallet(i + 1, origin, coll, scene, rng,
                     n_plank_src, n_runner_src)

    print("Done: %d pallets in collection '%s' (height %.2f, sources untouched)."
          % (N_PALLETS, ASM_COLLECTION, PALLET_HEIGHT))


if __name__ == "__main__":
    main()
