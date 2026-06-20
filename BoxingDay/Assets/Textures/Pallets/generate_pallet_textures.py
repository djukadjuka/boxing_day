#!/usr/bin/env python3
"""
Boxing Day :: wooden pallet plank texture generator.

Emits flat / retro SVG wood textures into this folder, matching the box-art
convention (see ../Boxes/README.md):
  * NO gradients, NO SVG filters  -> pixelates cleanly at low res.
  * Colour alpha is expressed as fill-opacity / stroke-opacity, never as an
    8-digit #RRGGBBAA hex (Inkscape's PNG exporter renders those fully opaque).

Pallet build target: a 4x4 deck pallet (fits 2x2 = 4 boxes on the bottom).
  * 5 top deck boards + 4 bottom deck boards  -> "thin planks"
  * 3 runners along the length                -> "runners"

Output set (19 files):
  thinplank_face_1..5   long top/bottom face of a deck board (grain + nail holes)
  thinplank_side_1..5   the narrow long edge of a deck board
  runner_face_1..6      the long visible side of a runner beam
  runner_side_1..3      sawn end-cap of a runner (end-grain rings + cracks)

Each group varies the "worn" amount fresh(0) -> weathered grey(1).

Run:  python generate_pallet_textures.py
Then: convert_svg_to_png.bat   (Inkscape SVG -> PNG, same folder)
"""

import math
import os
import random

OUT = os.path.dirname(os.path.abspath(__file__))


# ---------------------------------------------------------------- colour utils
def lerp(a, b, t):
    return a + (b - a) * t


def mix(c1, c2, t):
    return tuple(lerp(c1[i], c2[i], t) for i in range(3))


def hx(c):
    return "#%02x%02x%02x" % (max(0, min(255, int(c[0]))),
                              max(0, min(255, int(c[1]))),
                              max(0, min(255, int(c[2]))))


def palette(wear):
    """Fresh warm pine -> silvered/weathered grey as wear 0->1."""
    base = mix((182, 139, 80), (170, 159, 139), wear)   # plank body
    dark = mix((120, 86, 44), (108, 98, 82), wear)      # dark grain
    light = mix((208, 172, 117), (200, 192, 175), wear)  # light grain / silvering
    return base, dark, light


# ------------------------------------------------------------- drawing helpers
def grain_lines(rng, w, h, dark, light, wear):
    """Wavy grain running along the long (x) axis, banded across the width."""
    els = []
    y = rng.uniform(1, 4)
    while y < h:
        gap = rng.uniform(0.04, 0.11) * h + rng.uniform(1.5, 4.0)
        to_dark = rng.random() < 0.58
        col = dark if to_dark else light
        op = (rng.uniform(0.07, 0.24) if to_dark else rng.uniform(0.05, 0.15))
        op *= (1.0 - 0.35 * wear)                 # grain fades as wood greys out
        sw = rng.uniform(0.8, 2.6)
        amp = rng.uniform(1.0, 0.06 * h + 1.5)
        ph = rng.uniform(0, 2 * math.pi)
        freq = rng.uniform(0.5, 1.7) * 2 * math.pi / max(w, 1)
        step = max(20.0, w / 48.0)
        pts, x = [], 0.0
        while x <= w:
            yy = y + amp * math.sin(freq * x + ph)
            pts.append("%.0f,%.1f" % (x, yy))
            x += step
        els.append(
            '<polyline points="%s" fill="none" stroke="%s" '
            'stroke-opacity="%.3f" stroke-width="%.2f"/>'
            % (" ".join(pts), hx(col), op, sw)
        )
        y += gap
    return els


def knot(rng, cx, cy, dark, scale=1.0):
    els = []
    rx = rng.uniform(7, 16) * scale
    ry = rx * rng.uniform(0.55, 0.9)
    rings = rng.randint(3, 5)
    for i in range(rings):
        f = 1.0 - i / (rings + 1.0)
        els.append(
            '<ellipse cx="%.0f" cy="%.0f" rx="%.1f" ry="%.1f" fill="none" '
            'stroke="%s" stroke-opacity="%.2f" stroke-width="%.1f"/>'
            % (cx, cy, rx * f, ry * f, hx(dark), 0.16 + 0.09 * i,
               rng.uniform(1.1, 2.4))
        )
    els.append(
        '<ellipse cx="%.0f" cy="%.0f" rx="%.1f" ry="%.1f" fill="%s" '
        'fill-opacity="0.55"/>' % (cx, cy, rx * 0.22, ry * 0.22, hx(dark))
    )
    return els


def nail(cx, cy, dark):
    return [
        '<circle cx="%.0f" cy="%.0f" r="2.8" fill="%s" fill-opacity="0.50"/>'
        % (cx, cy, hx(dark)),
        '<circle cx="%.0f" cy="%.0f" r="2.8" fill="none" stroke="#000000" '
        'stroke-opacity="0.28" stroke-width="1"/>' % (cx, cy),
        '<circle cx="%.1f" cy="%.1f" r="0.9" fill="#ffffff" '
        'fill-opacity="0.30"/>' % (cx - 0.9, cy - 0.9),
    ]


def wear_overlay(rng, w, h, wear):
    """Speckle, scuffs, scratches, and edge darkening that grow with wear."""
    els = []
    area = w * h
    for _ in range(int(wear * area / 2600) + int(area / 14000)):
        x, y, r = rng.uniform(0, w), rng.uniform(0, h), rng.uniform(0.5, 1.7)
        if rng.random() < 0.6:
            els.append('<circle cx="%.0f" cy="%.0f" r="%.1f" fill="#000000" '
                       'fill-opacity="%.3f"/>' % (x, y, r, rng.uniform(0.04, 0.12)))
        else:
            els.append('<circle cx="%.0f" cy="%.0f" r="%.1f" fill="#ffffff" '
                       'fill-opacity="%.3f"/>' % (x, y, r, rng.uniform(0.04, 0.10)))
    for _ in range(int(2 + wear * 6)):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        rx, ry = rng.uniform(w * 0.03, w * 0.10), rng.uniform(h * 0.06, h * 0.20)
        els.append('<ellipse cx="%.0f" cy="%.0f" rx="%.0f" ry="%.0f" '
                   'fill="#000000" fill-opacity="%.3f"/>'
                   % (x, y, rx, ry, rng.uniform(0.02, 0.06)))
    for _ in range(int(wear * 9)):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        L, a = rng.uniform(w * 0.03, w * 0.13), rng.uniform(-0.18, 0.18)
        col = "#ffffff" if rng.random() < 0.5 else "#000000"
        els.append('<line x1="%.0f" y1="%.0f" x2="%.0f" y2="%.0f" stroke="%s" '
                   'stroke-opacity="%.3f" stroke-width="%.1f"/>'
                   % (x, y, x + L * math.cos(a), y + L * math.sin(a), col,
                      rng.uniform(0.05, 0.14), rng.uniform(0.6, 1.3)))
    eo = 0.06 + 0.13 * wear
    els.append('<rect x="0" y="0" width="%g" height="%g" fill="none" '
               'stroke="#000000" stroke-opacity="%.2f" stroke-width="6"/>'
               % (w, h, eo))
    return els


def svg_doc(w, h, body):
    return ('<svg xmlns="http://www.w3.org/2000/svg" width="%g" height="%g" '
            'viewBox="0 0 %g %g">\n%s\n</svg>\n' % (w, h, w, h, "\n".join(body)))


# --------------------------------------------------------------- plank builders
def plank(w, h, wear, seed, nails_at=None, n_knots=1):
    rng = random.Random(seed)
    base, dark, light = palette(wear)
    body = ['  <rect width="%g" height="%g" fill="%s"/>' % (w, h, hx(base))]
    # broad tonal streaks so the board isn't a flat slab
    for _ in range(rng.randint(2, 4)):
        y = rng.uniform(0, h)
        body.append('  <rect x="0" y="%.0f" width="%g" height="%.0f" fill="%s" '
                    'fill-opacity="%.3f"/>'
                    % (y, w, rng.uniform(h * 0.15, h * 0.4),
                       hx(dark if rng.random() < 0.5 else light),
                       rng.uniform(0.04, 0.09)))
    body += ["  " + e for e in grain_lines(rng, w, h, dark, light, wear)]
    for _ in range(n_knots):
        if rng.random() < 0.75:
            body += ["  " + e for e in
                     knot(rng, rng.uniform(w * 0.1, w * 0.9),
                          rng.uniform(h * 0.25, h * 0.75), dark,
                          scale=min(1.0, h / 90.0))]
    if nails_at:
        for nx in nails_at:
            body += ["  " + e for e in nail(nx, h * 0.5 + rng.uniform(-h*0.12, h*0.12), dark)]
    body += ["  " + e for e in wear_overlay(rng, w, h, wear)]
    return svg_doc(w, h, body)


def endgrain(w, h, wear, seed):
    """Sawn beam end: concentric growth rings + radial checking cracks."""
    rng = random.Random(seed)
    base, dark, light = palette(wear)
    # end-grain reads a touch paler & greyer than the face
    base = mix(base, (200, 190, 170), 0.25)
    body = ['  <rect width="%g" height="%g" fill="%s"/>' % (w, h, hx(base))]
    # chamfered sawn border
    body.append('  <rect x="3" y="3" width="%g" height="%g" fill="none" '
                'stroke="%s" stroke-opacity="0.35" stroke-width="4"/>'
                % (w - 6, h - 6, hx(dark)))
    # pith (ring centre) offset from the middle, often near an edge
    cx = w * rng.uniform(0.35, 0.65)
    cy = h * rng.uniform(0.2, 0.5)
    maxr = math.hypot(w, h)
    r = rng.uniform(4, 8)
    i = 0
    while r < maxr:
        squash = rng.uniform(0.8, 1.15)
        op = (0.10 + 0.12 * (i % 2)) * (1.0 - 0.3 * wear)
        body.append('  <ellipse cx="%.0f" cy="%.0f" rx="%.1f" ry="%.1f" '
                    'fill="none" stroke="%s" stroke-opacity="%.3f" '
                    'stroke-width="%.1f"/>'
                    % (cx, cy, r, r * squash, hx(dark), op,
                       rng.uniform(1.0, 2.4)))
        r += rng.uniform(5, 12)
        i += 1
    body += ["  " + e for e in knot(rng, cx, cy, dark, scale=0.5)]
    # radial checking cracks from the pith (more with wear)
    for _ in range(rng.randint(1, 2) + int(wear * 2)):
        a = rng.uniform(0, 2 * math.pi)
        L = rng.uniform(h * 0.4, h * 0.95)
        body.append('  <line x1="%.0f" y1="%.0f" x2="%.0f" y2="%.0f" '
                    'stroke="#000000" stroke-opacity="%.2f" stroke-width="%.1f"/>'
                    % (cx, cy, cx + L * math.cos(a), cy + L * math.sin(a),
                       0.18 + 0.12 * wear, rng.uniform(1.2, 2.6)))
    body += ["  " + e for e in wear_overlay(rng, w, h, wear)]
    return svg_doc(w, h, body)


# ----------------------------------------------------------------------- emit
def write(name, text):
    path = os.path.join(OUT, name)
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)
    print("wrote", name)


def main():
    # thin plank FACE: 1024x128, nails at the 3 runner crossings
    FW, FH = 1024, 128
    nails = [FW * 0.10, FW * 0.50, FW * 0.90]
    for i in range(1, 6):
        wear = (i - 1) / 4.0
        write("thinplank_face_%d.svg" % i,
              plank(FW, FH, wear, seed=1000 + i, nails_at=nails, n_knots=2))

    # thin plank SIDE (narrow long edge): 1024x48
    SW, SH = 1024, 48
    for i in range(1, 6):
        wear = (i - 1) / 4.0
        write("thinplank_side_%d.svg" % i,
              plank(SW, SH, wear, seed=2000 + i, nails_at=None, n_knots=1))

    # runner FACE (long beam side): 1024x96
    RW, RH = 1024, 96
    for i in range(1, 7):
        wear = (i - 1) / 5.0
        write("runner_face_%d.svg" % i,
              plank(RW, RH, wear, seed=3000 + i,
                    nails_at=[RW * 0.18, RW * 0.5, RW * 0.82], n_knots=2))

    # runner SIDE (sawn end-cap, end-grain): 192x288 (taller than wide)
    # 6 runners -> 6 end-caps. Sets 1-3 keep their original wear/seed exactly;
    # 4-6 get their own distinct wear so every runner end reads differently.
    EW, EH = 192, 288
    runner_side_wear = {1: 0.0, 2: 0.5, 3: 1.0, 4: 0.25, 5: 0.6, 6: 0.85}
    for i in range(1, 7):
        write("runner_side_%d.svg" % i,
              endgrain(EW, EH, runner_side_wear[i], seed=4000 + i))

    print("done -> 19 svg files in", OUT)


if __name__ == "__main__":
    main()
