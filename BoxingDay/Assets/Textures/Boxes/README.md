# Box face art

Hand-authored **SVG** box faces for *Boxing Day*. One square texture maps onto every face
of the cube box (built-in Unity cube UVs put the full image on each face).

| File | Box type | Material to apply to |
|---|---|---|
| `box_regular.svg` | Regular cardboard | `Materials/GenericBoxMaterial.mat` (existing) |
| `box_heavy.svg`   | Heavy (reinforced + strapped) | `Materials/box_heavy_material.mat` |
| `box_fragile.svg` | Fragile (red markings) | `Materials/box_fragile_material.mat` |

## Getting them into Unity (pixelated 1990 look)

SVG isn't a raster texture, so convert each to PNG first (or use Unity's **Vector Graphics**
package, `com.unity.vectorgraphics`, which imports SVG directly).

**Convert to PNG** (any of):
- Inkscape: `File > Export` → PNG, e.g. **128×128** (small = chunky retro pixels).
- A browser / online SVG→PNG converter.
- ImageMagick: `magick -background none box_regular.svg -resize 128x128 box_regular.png`

**Import settings on the PNG** (select it in Unity → Inspector):
- **Filter Mode: Point (no filter)** — hard pixels, no blur.
- **Compression: None** (keeps the colors crisp).
- Wrap Mode: Clamp.
- A low **Max Size** (128/256) reinforces the retro look.

**Assign** the PNG to the matching material's **Albedo / `_MainTex`** slot (table above).
Then set the material's **Albedo color tint to white** so the texture shows its true colors
(the materials currently carry a representative brown/tan tint so untextured boxes still read
as the right type).

## Notes

- These are flat/retro by design (no gradients) so they pixelate cleanly at low res.
- Easy to recolor or add box IDs — they're plain SVG markup.
- The `CT-####` codes and "CARTWRIGHT TRADING CO. / HARBOR GLEN, BELLMONT" tie into the
  game's lore; tweak per box type or per crate as you like.
