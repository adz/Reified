#!/usr/bin/env python3
"""Regenerate every Reified logo asset in docs/content/img/.

The wordmark ships as outlines rather than an SVG <text> element, so the lockup
renders identically everywhere and never depends on a font being installed. That
also means these files cannot be edited by hand — change the constants here and
re-run instead.

    python3 -m pip install --user fonttools uharfbuzz
    python3 scripts/build-logo.py

The mark is four plates climbing up and to the right, resolving from ghost to solid:
the declaration becoming the concrete, typed value. Plate corners are rounded to sit
with Comfortaa's rounded terminals.

HarfBuzz shapes the wordmark, so kerning and the `fi` ligature apply; fontTools draws
each glyph through a transform that scales it to the target cap height and flips the
y axis into SVG's coordinate space.

Font: Comfortaa Bold (SIL Open Font License 1.1), which permits embedding outlines.
"""

from math import hypot
from pathlib import Path

import uharfbuzz as hb
from fontTools.misc.transform import Transform
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

FONT = "/usr/share/fonts/aajohan-comfortaa-fonts/Comfortaa-Bold.otf"
TEXT = "Reified"

# --- geometry, in the 64-unit grid ------------------------------------------

PLATES = [(23, 47), (29, 37), (35, 27), (41, 17)]   # centres, climbing up and right
HALF_W, HALF_H = 14.0, 4.6
CORNER = 2.0          # how far the round-off cuts back along each edge

CAP_HEIGHT = 31.0     # wordmark cap height
BASELINE_Y = 45.5
WORD_X = 80.0         # the mark occupies 9..55, leaving a 25-unit gap
TRACKING = -0.4
RIGHT_PAD = 10.0

# --- palettes ---------------------------------------------------------------

MARK_BLUE_LIGHT = "#3469b8"
MARK_BLUE_DARK = "#6d9de0"
FAVICON_BLUE_DARK = "#599bfd"
WORD_LIGHT = "#12181c"
WORD_DARK = "#eef2f3"

# The faintest plates are lifted in the favicon builds so all four survive at 16px.
FADE_LIGHT = (0.22, 0.45, 0.70, 1.0)
FADE_DARK = (0.26, 0.48, 0.72, 1.0)
FADE_FAVICON_LIGHT = (0.32, 0.54, 0.76, 1.0)
FADE_FAVICON_DARK = (0.34, 0.56, 0.78, 1.0)

IMG_DIR = Path(__file__).resolve().parent.parent / "docs" / "content" / "img"


def rounded_plate(cx, cy, half_w=HALF_W, half_h=HALF_H, corner=CORNER):
    """A rhombus whose corners are cut back by `corner` and closed with quadratic arcs."""
    vertices = [(cx, cy - half_h), (cx + half_w, cy), (cx, cy + half_h), (cx - half_w, cy)]

    def toward(origin, target):
        dx, dy = target[0] - origin[0], target[1] - origin[1]
        length = hypot(dx, dy)
        return origin[0] + dx / length * corner, origin[1] + dy / length * corner

    n = len(vertices)
    entries = []
    for i, vertex in enumerate(vertices):
        previous, following = vertices[i - 1], vertices[(i + 1) % n]
        entries.append((toward(vertex, previous), vertex, toward(vertex, following)))

    def fmt(point):
        return f"{point[0]:.2f} {point[1]:.2f}"

    parts = [f"M{fmt(entries[0][2])}"]
    for entry in entries[1:]:
        into, vertex, out = entry
        parts.append(f"L{fmt(into)}")
        parts.append(f"Q{fmt(vertex)} {fmt(out)}")
    into, vertex, out = entries[0]
    parts.append(f"L{fmt(into)}")
    parts.append(f"Q{fmt(vertex)} {fmt(out)}")
    parts.append("Z")
    return " ".join(parts)


def mark_group(fill, fades, indent="  "):
    rows = []
    for (cx, cy), opacity in zip(PLATES, fades):
        d = rounded_plate(cx, cy)
        suffix = "" if opacity == 1.0 else f' opacity="{opacity}"'
        rows.append(f'{indent}  <path d="{d}"{suffix}/>')
    body = "\n".join(rows)
    return f'{indent}<g fill="{fill}">\n{body}\n{indent}</g>'


# --- wordmark ---------------------------------------------------------------


def cap_height(font):
    os2 = font.get("OS/2")
    if os2 is not None and getattr(os2, "sCapHeight", 0):
        return os2.sCapHeight
    glyph_set = font.getGlyphSet()
    pen = BoundsPen(glyph_set)
    glyph_set[font.getBestCmap()[ord("R")]].draw(pen)
    return pen.bounds[3]


def wordmark_outline():
    """Returns (path_data, advance_width) for TEXT laid out at CAP_HEIGHT."""
    data = Path(FONT).read_bytes()
    face = hb.Face(data)
    hb_font = hb.Font(face)
    hb_font.scale = (face.upem, face.upem)

    buf = hb.Buffer()
    buf.add_str(TEXT)
    buf.guess_segment_properties()
    hb.shape(hb_font, buf, {"kern": True, "liga": True})

    font = TTFont(FONT, lazy=True)
    glyph_set = font.getGlyphSet()
    glyph_order = font.getGlyphOrder()
    scale = CAP_HEIGHT / cap_height(font)

    parts = []
    pen_x = 0.0
    for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
        transform = Transform(
            scale, 0, 0, -scale,
            WORD_X + (pen_x + pos.x_offset) * scale,
            BASELINE_Y - pos.y_offset * scale,
        )
        pen = SVGPathPen(glyph_set, ntos=lambda v: f"{v:.2f}")
        glyph_set[glyph_order[info.codepoint]].draw(TransformPen(pen, transform))
        if commands := pen.getCommands():
            parts.append(commands)
        pen_x += pos.x_advance + TRACKING / scale

    return " ".join(parts), pen_x * scale


# --- files ------------------------------------------------------------------


def write(name, content):
    target = IMG_DIR / name
    target.write_text(content)
    print(f"wrote {target.relative_to(IMG_DIR.parents[2])} ({target.stat().st_size} bytes)")


def square(name, fill, fades, purpose):
    write(name, f"""<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" \
viewBox="0 0 64 64" role="img" aria-label="Reified">
  <!-- {purpose}
       Generated by scripts/build-logo.py — do not edit by hand. -->
{mark_group(fill, fades)}
</svg>
""")


def lockup(name, mark_fill, word_fill, fades, path_data, width, ground):
    view_w = round(WORD_X + width + RIGHT_PAD)
    write(name, f"""<svg xmlns="http://www.w3.org/2000/svg" width="{view_w}" height="64" \
viewBox="0 0 {view_w} 64" role="img" aria-label="Reified">
  <title>Reified</title>
  <!-- Horizontal lockup for {ground} grounds. Wordmark is Comfortaa Bold, outlined.
       Generated by scripts/build-logo.py — do not edit by hand. -->
{mark_group(mark_fill, fades)}
  <path fill="{word_fill}" d="{path_data}"/>
</svg>
""")


def main():
    path_data, width = wordmark_outline()

    lockup("reified-logo-light.svg", MARK_BLUE_LIGHT, WORD_LIGHT, FADE_LIGHT,
           path_data, width, "light")
    lockup("reified-logo-dark.svg", MARK_BLUE_DARK, WORD_DARK, FADE_DARK,
           path_data, width, "dark")

    square("reified-mark-light.svg", MARK_BLUE_LIGHT, FADE_LIGHT, "The mark alone, light grounds.")
    square("reified-mark-dark.svg", MARK_BLUE_DARK, FADE_DARK, "The mark alone, dark grounds.")
    square("favicon-light.svg", MARK_BLUE_LIGHT, FADE_FAVICON_LIGHT,
           "Favicon build: faint plates lifted so all four survive at 16px.")
    square("favicon-dark.svg", FAVICON_BLUE_DARK, FADE_FAVICON_DARK,
           "Favicon build for dark browser chrome.")


if __name__ == "__main__":
    main()
