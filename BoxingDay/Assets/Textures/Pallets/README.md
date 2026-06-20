# Pallet plank art

Flat / retro **SVG** wood textures for the warehouse stacking **pallet** in *Boxing Day*.
Same art convention as `../Boxes/` — no gradients, no SVG filters, alpha via
`fill-opacity`/`stroke-opacity` (never 8-digit `#RRGGBBAA`, which Inkscape exports opaque)
so everything pixelates cleanly at low res.

## The pallet it's for

A **4×4 deck pallet** (fits 2×2 = 4 boxes on the bottom layer, then stack up):
- **5 top** deck boards + **4 bottom** deck boards → *thin planks*
- **6 runners** along the length → *runners*

## Files (22)

| Group | Files | Size px | What it is |
|---|---|---|---|
| Thin plank **face** | `thinplank_face_1..5` | 1024×128 | top/bottom face of a deck board — grain + 3 nail holes at the runner crossings |
| Thin plank **side** | `thinplank_side_1..5` | 1024×48 | the narrow long edge of a deck board |
| Runner **face** | `runner_face_1..6` | 1024×96 | the long visible side of a runner beam |
| Runner **side** | `runner_side_1..6` | 192×288 | sawn **end-cap** — end-grain growth rings + checking cracks |

Within each group the wood goes **fresh warm pine (…_1) → silvered/weathered grey (last)**.
Grain runs along the **long (x) axis** of every face texture, so map image-x to plank-length.

## Regenerating / tweaking

`generate_pallet_textures.py` (Python 3) produces all 22 SVGs deterministically (seeded).
Edit the palette, sizes, wear curve, knot/nail counts there and re-run:

```
python generate_pallet_textures.py
```

## SVG → PNG

`convert_svg_to_png.bat` — double-click (or run from a terminal here) to batch-convert
every `.svg` in this folder to a `.png` of the same name via Inkscape 1.x. Each SVG keeps
its own document size, so no `-w/-h` is needed. Edit the script if Inkscape lives elsewhere.

**Unity import settings on the PNGs** (retro look): Filter Mode **Point**, Compression
**None**, Wrap **Clamp** (or **Repeat** if you tile a single board art along a long plank).
