# Box face art

Hand-authored **SVG** box faces for *Boxing Day*. Each square texture maps onto one face of
the cube box. Three box types, six faces each (front + 5 sides).

## Files

| Box type | Front | Back | Left | Right | Top | Bottom |
|---|---|---|---|---|---|---|
| Regular (normal) | `box_regular.svg` | `box_regular_back.svg` | `box_regular_left.svg` | `box_regular_right.svg` | `box_regular_top.svg` | `box_regular_bottom.svg` |
| Heavy | `box_heavy.svg` | `box_heavy_back.svg` | `box_heavy_left.svg` | `box_heavy_right.svg` | `box_heavy_top.svg` | `box_heavy_bottom.svg` |
| Fragile (light) | `box_fragile.svg` | `box_fragile_back.svg` | `box_fragile_left.svg` | `box_fragile_right.svg` | `box_fragile_top.svg` | `box_fragile_bottom.svg` |

Face design language (shared per type, so the box reads as one object):
- **Front** — the main label/markings (unchanged originals).
- **Back** — plainer; faded receiving stamp / repeated warnings, recycle mark.
- **Left / Right** — corrugation + a wrap-around tape/strap; hand-hole or side stamps.
- **Top** — flap creases + a center tape seam; "TOP" / up-arrows / "DO NOT STACK".
- **Bottom** — H-tape seal + "DO NOT DROP" / "FORKLIFT ONLY" + maker mark.

## Getting them into Unity (pixelated 1990 look)

SVG isn't a raster texture, so convert each to PNG first (or use Unity's **Vector Graphics**
package, `com.unity.vectorgraphics`, which imports SVG directly).

**Convert to PNG** (any of):
- Inkscape: `File > Export` → PNG, e.g. **128×128** (small = chunky retro pixels). Batch via CLI:
  `inkscape box_regular_top.svg --export-type=png --export-filename=box_regular_top.png -w 128 -h 128`
- A browser / online SVG→PNG converter.
- ImageMagick: `magick -background none box_regular.svg -resize 128x128 box_regular.png`

> **Inkscape alpha note:** Inkscape's PNG exporter ignores 8-digit `#RRGGBBAA` hex colors
> (it renders them fully **opaque** — you'd get solid black blotches). All faces in this folder
> therefore use SVG 1.1 `fill="#RRGGBB" fill-opacity="…"` instead, which every renderer honors.
> Keep that convention if you edit them.

**Import settings on the PNG** (select it in Unity → Inspector):
- **Filter Mode: Point (no filter)** — hard pixels, no blur.
- **Compression: None** (keeps the colors crisp).
- Wrap Mode: Clamp.
- A low **Max Size** (128/256) reinforces the retro look.

## Assigning per-face art on the cube box

The box is a built-in Unity **cube** (single mesh, single material) — its UVs put the *whole*
texture on every face, so one material shows the same image on all six sides. To show a
different PNG per face you need one of:

1. **Six child quads** — make the box a parent with 6 `Quad` children, each rotated onto a
   face with its own material/PNG. No modeling; most editable. Keep the `BoxCollider` +
   `Rigidbody` + `GenericBoxBehaviour` on the parent and disable the cube's own `MeshRenderer`.
2. **Six-submesh cube + 6 materials** — a custom cube mesh split into 6 submeshes (one per
   face); the `MeshRenderer` gets 6 material slots. One GameObject, generated via editor script.
3. **Texture atlas + remapped cube UVs** — pack the 6 faces into one atlas image and a custom
   cube mesh whose per-face UVs sample the right tile. One material, one draw call (best perf).

Set each material's **Albedo color tint to white** so the texture shows its true colors.

## Notes

- These are flat/retro by design (no gradients) so they pixelate cleanly at low res.
- Easy to recolor or add box IDs — they're plain SVG markup.
- The `CT-####` codes and "CARTWRIGHT TRADING CO. / HARBOR GLEN, BELLMONT" tie into the
  game's lore; tweak per box type or per crate as you like.
