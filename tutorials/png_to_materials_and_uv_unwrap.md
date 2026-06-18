# SVG → PNG → per-face materials via Blender UV unwrap

How to take the hand-authored box-face **SVGs** and end up with a textured mesh in
Unity where each face shows its own image — using a real **UV unwrap + per-face
materials** workflow (not the box-only `BoxFaceArt` quad hack). This generalizes
to any mesh later, not just cubes.

Worked example uses the **regular** box (`box_regular*`). Repeat for `box_heavy*`
and `box_fragile*` (or duplicate the finished mesh and swap textures).

Source SVGs live in `BoxingDay/Assets/Textures/Boxes/`. There are 18: 3 box types
× 6 faces. The base `box_<type>.svg` is the **front**; the rest are
`_back / _left / _right / _top / _bottom`.

---

## 1. Convert the SVGs to PNG (Inkscape 1.4.2)

Export each face to a 256×256 PNG (small = chunky retro pixels).

### CLI (fastest — all 18 at once)

The Inkscape `bin` must be on your PATH. Open a terminal **in the Boxes folder**:

```
cd "D:\BIG_Projects\boxing_day\BoxingDay\Assets\Textures\Boxes"
```

Test one file first:

```
inkscape box_regular.svg --export-type=png --export-filename=box_regular.png -w 256 -h 256
```

Then convert everything:

- **PowerShell:**
  ```powershell
  Get-ChildItem *.svg | ForEach-Object { inkscape $_.FullName --export-type=png --export-filename="$($_.BaseName).png" -w 256 -h 256 }
  ```
- **cmd.exe:**
  ```cmd
  for %f in (*.svg) do inkscape "%f" --export-type=png --export-filename="%~nf.png" -w 256 -h 256
  ```
  (In a `.bat` file, double the percents: `%%f`, `%%~nf`.)

### GUI alternative

`File → Open` an SVG, then `File → Export` (Shift+Ctrl+E) → **Single** tab →
Export area **Page** → set unit **px**, Width/Height **256** → format **PNG** →
confirm the filename → **Export**. The **Batch Export** tab can do many at once.

> **Inkscape alpha note:** Inkscape's PNG exporter renders 8-digit `#RRGGBBAA`
> hex as fully opaque (solid black blotches). The SVGs use `fill-opacity` instead,
> which exports correctly — keep that convention if you edit them. After the first
> export, open the PNG and confirm a transparent background and correct colors.

---

## 2. Unity import settings on the PNGs (retro look)

Select the PNGs in Unity → Inspector → set, then **Apply**:

- **Filter Mode: Point (no filter)** — hard pixels, no blur.
- **Compression: None** — keeps colors crisp.
- **Wrap Mode: Clamp**.

You can multi-select all the PNGs and apply at once.

---

## 3. Blender — cube + 6 material slots

1. New file; keep the default cube.
2. Press **N** → **Item** → set **Dimensions** to **1 m × 1 m × 1 m** (matches
   Unity's unit cube).
3. Apply the scale so it exports clean: **Object → Apply → Scale** (Ctrl+A → Scale).
4. **Material Properties** (sphere icon) → click **+** six times → for each slot
   click **New** and rename: `front`, `back`, `left`, `right`, `top`, `bottom`.

---

## 4. Blender — assign each face to its slot

1. **Tab** into Edit Mode, press **3** for Face select, then **A** / **Alt+A** to
   deselect all.
2. For each face: snap to its orthographic view, click the face that fills the
   screen, select the matching slot in Material Properties, click **Assign**.

| View | Face | Slot |
|---|---|---|
| Numpad 1 | front | `front` |
| Ctrl+Numpad 1 | back | `back` |
| Numpad 3 | right | `right` |
| Ctrl+Numpad 3 | left | `left` |
| Numpad 7 | top | `top` |
| Ctrl+Numpad 7 | bottom | `bottom` |

Verify with a slot selected → **Select** button highlights its faces (exactly one
each).

> Blender's "front" may become Unity's "back" after export — that's fine; only
> **top stays on top** matters for gameplay (the "TOP"/up-arrow art).

---

## 5. Blender — put a PNG on each material

For each slot in **Material Properties**:

1. Select the slot.
2. **Surface → Base Color** → click the yellow dot to the left of the swatch →
   **Image Texture**.
3. **Open** → pick that face's PNG:

| Slot | PNG |
|---|---|
| `front` | `box_regular.png` |
| `back` | `box_regular_back.png` |
| `left` | `box_regular_left.png` |
| `right` | `box_regular_right.png` |
| `top` | `box_regular_top.png` |
| `bottom` | `box_regular_bottom.png` |

4. Switch the viewport to **Material Preview** (third sphere icon, top-right) to
   see it. The art will look stretched/rotated until the next step.

> Optional: in the **Shading** workspace, set each Image Texture node's
> interpolation `Linear → Closest` for crisp pixels in Blender's preview. Unity's
> Point filter controls the final look regardless.

---

## 6. Blender — unwrap each face to fill + orient it

1. Open the **UV Editing** workspace tab (top of window).
2. 3D view: Edit Mode, Face select (**3**).
3. For each face, select **only** that face, then in the 3D viewport press
   **U → Reset**. `Reset` snaps the quad's UVs exactly to the image corners
   `(0,0)–(1,1)`, so it fills the whole image with no stretching.
   - (`U → Project From View (Bounds)` from the face's ortho view also fills — note
     the **(Bounds)** variant; plain "Project From View" does not normalize.)
4. Fix orientation per face in the **UV editor**: select the face, hover its island,
   press **A** to select all its UVs, then **R 90 Enter** to rotate (repeat to
   taste) or **UV → Mirror → X/Y** to flip. Watch the 3D view update live.
   - Only **top**/**bottom** orientation really matters; the four sides just need
     to be upright.
5. Manual stretch (only for non-quad islands): select UVs, **S** to scale
   (`S X`/`S Y` per axis), **G** to move. For square faces, `Reset` is exact and
   one keystroke — prefer it.

---

## 7. Blender — export FBX into Unity

1. **Object Mode**, select the cube.
2. **File → Export → FBX (.fbx)**.
3. In the export sidebar:
   - **Include → Limit to: Selected Objects** ✔
   - **Transform → Scale: 1.00**; **Forward: -Z Forward**, **Up: Y Up**.
   - **Transform → Apply Transform ✔** ← important (see the orientation note below).
   - **Geometry → Smoothing: Face** (keeps the cube's hard edges crisp).
   - **Path Mode: Auto** — the PNGs already live in Unity, no need to embed.
4. Save into the Unity project, e.g. `BoxingDay/Assets/Models/box_regular.fbx`
   (make a `Models` folder).

> **Why Apply Transform (orientation):** Blender is right-handed/Z-up, Unity is
> left-handed/Y-up. Without **Apply Transform**, the FBX export converts handedness
> by mirroring/rotating, which lands the UVs on each face *differently* in Unity
> (some 90°, some 180°, some fine) — impossible to fix with one rotation, and
> "correct in Blender" ends up wrong in Unity. With **Apply Transform ✔** the
> geometry + UVs bake into Unity's coordinate system, so **what looks right in
> Blender looks right in Unity** — no per-face flipping to chase.

> **Scale gotcha (pairs with the above):** **Apply Transform also bakes the unit
> scale**, so the raw mesh already imports at ~1 unit. Therefore set the FBX
> importer's **Scale Factor = 1** (**Model** tab → Apply). If you instead set
> Scale Factor = 100 (which the *non*-Apply-Transform raw mesh needs), the box
> becomes ~100 units huge — you end up *inside* it in the prefab view, seeing
> culled backfaces, and it looks invisible. Scale Factor lives on the FBX importer
> and bakes into the mesh data, so every prefab using that mesh updates
> automatically.

---

## 8. Unity — materials + wiring onto the box

### 8a. Extract the materials

1. Select the imported FBX → Inspector → **Materials** tab → **Extract Materials…**
   → choose a folder (e.g. `Assets/Materials/Boxes`). This creates 6 editable
   `.mat` assets named after the Blender slots (`front`, `back`, …).
2. Check one material's **Albedo** slot. Often the texture rode along with the FBX;
   if a material's Albedo is empty, drag the matching `box_<type>*` PNG into the
   **Albedo** texture slot. Make sure the **tint is white** on all six.

### 8b. Build a per-type prefab

The existing `GenericBox` prefab already has the tuned **BoxCollider**,
**Rigidbody**, and **GenericBoxBehaviour** (and a root scale of 0.6). Reuse it
rather than rebuilding — duplicate it per box type and just swap the mesh +
materials:

1. In **Project**, select `Assets/Prefabs/GenericBox`, **Ctrl+D** to duplicate,
   rename to e.g. **`Box_Regular`**.
2. **Double-click** it to open Prefab Mode; select the root object.
3. **Swap the mesh:** expand the FBX in Project (**►** arrow) to reveal its **Mesh**
   sub-asset; drag it into the root's **Mesh Filter → Mesh** slot, replacing the
   built-in Cube.
4. **Assign the 6 materials** on **Mesh Renderer → Materials**: set **Size = 6** and
   drag the `.mat`s in, in submesh order (= the Blender slot order):

   | Element | Material |
   |---|---|
   | 0 | front |
   | 1 | back |
   | 2 | left |
   | 3 | right |
   | 4 | top |
   | 5 | bottom |

   If a face shows the wrong art, swap that element — the preview updates live.
5. Confirm all faces show art and **top is upright**. Leave the 0.6 scale and the
   1×1×1 collider (the 1-unit mesh renders at 0.6 to match the old box). If
   front/back came in swapped, ignore it (interchangeable) or yaw the mesh 180°.
6. Set the `Weight` field on `GenericBoxBehaviour` for this type (regular ≈ 1,
   heavy higher, fragile lighter).
7. **Ctrl+S**, exit Prefab Mode (back arrow), and drag the prefab into the scene to
   test pickup/stacking.

### 8c. The other box types

Duplicate `Box_Regular` → swap its 6 materials for the heavy/fragile set and adjust
`Weight`. (You only need to do the Blender unwrap once; all three types share the
same mesh/UVs and differ only in textures.)

---

## Why this and not the `BoxFaceArt` quad script

`BoxFaceArt` spawns six textured quads on a cube at runtime — quick, but it's a
cube-only trick that won't carry to shelves, pallet jacks, or characters. UV
unwrapping + per-face materials is the standard mesh-texturing workflow and the
same skill applies to any mesh. For *organic* meshes later you'll usually take it
one step further — a single UV unwrap baked/painted into **one** atlas texture and
one material — but per-face materials are the natural fit for these flat,
pre-made box faces.
```
