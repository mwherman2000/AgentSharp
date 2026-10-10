"""Draw Ray's, Jeff's and RJ's (empty) gumball bowls from the inventory in docs/gumballs.md.

Color = trait type, shade = related collection, size = significance and granularity.
Only the inventory tables are read, so the picture cannot drift from the tags.
Regenerate with: python docs/images/make-gumballs-svg.py
"""
import colorsys
import math
import os
import random
import re
import sys
from xml.sax.saxutils import escape

HERE = os.path.dirname(os.path.abspath(__file__))
DOC = os.path.join(HERE, "..", "gumballs.md")
OUT = os.path.join(HERE, "gumballs-bowls.svg")

HUES = {"Red": 2, "Orange": 28, "Yellow": 50, "Green": 125, "Blue": 212, "Purple": 275, "Pink": 332}
LABELS = {
    "Red": "constraints", "Orange": "stance", "Yellow": "method", "Green": "domain",
    "Blue": "product and modes", "Purple": "voice", "Pink": "identity",
}
# (saturation, lightness) per shade, deep to pale
SHADES = {"Deep": (0.80, 0.34), "Bright": (0.92, 0.52), "Light": (0.85, 0.68), "Pale": (0.75, 0.83)}
RADIUS = {"XL": 21, "L": 15, "M": 10.5, "S": 6.5}

BOWL_R = 150
GAP = 1.6


# Darkened orange and yellow turn brown and olive, which read as different colors,
# so their Deep shades stay saturated and only a little darker.
DEEP_OVERRIDE = {"Orange": (0.97, 0.43), "Yellow": (0.97, 0.42)}


def hexcolor(color, shade):
    s, l = DEEP_OVERRIDE[color] if shade == "Deep" and color in DEEP_OVERRIDE else SHADES[shade]
    r, g, b = colorsys.hls_to_rgb(HUES[color] / 360, l, s)
    return "#%02x%02x%02x" % (round(r * 255), round(g * 255), round(b * 255))


def parse(doc_text):
    bowls = {"R": [], "J": []}
    row = re.compile(r"^\| ([RJ])-(\d+) \| (.*?) \| (?:\S+ )?(\w+)·(\w+) \| (XL|L|M|S) \|")
    for line in doc_text.splitlines():
        m = row.match(line)
        if m:
            who, num, trait, color, shade, size = m.groups()
            bowls[who].append((f"{who}-{num}", trait, color, shade, size))
    return bowls


def pack(items, seed):
    """Greedy random circle packing inside the bowl, largest first."""
    rng = random.Random(seed)
    order = sorted(items, key=lambda it: -RADIUS[it[4]])
    placed = []
    for it in order:
        r = RADIUS[it[4]]
        for _ in range(20000):
            ang = rng.uniform(0, 2 * math.pi)
            dist = (BOWL_R - 8 - r) * math.sqrt(rng.random())
            x, y = dist * math.cos(ang), dist * math.sin(ang)
            if all(math.hypot(x - px, y - py) >= r + pr + GAP for (_, _, _, _, _), px, py, pr in placed):
                placed.append((it, x, y, r))
                break
        else:
            sys.exit(f"could not place {it[0]}")
    # drawing order: big first so small ones sit on top and stay visible
    return [(it, x, y, r) for it, x, y, r in placed]


def bowl_svg(cx, cy, fill, stroke, placed):
    out = [f'<circle cx="{cx}" cy="{cy}" r="{BOWL_R + 10}" fill="{fill}" stroke="{stroke}" stroke-width="5"/>',
           f'<circle cx="{cx}" cy="{cy}" r="{BOWL_R}" fill="#ffffff" fill-opacity="0.55" stroke="{stroke}" stroke-opacity="0.5" stroke-width="2"/>']
    for (gid, trait, color, shade, size), x, y, r in placed:
        c = hexcolor(color, shade)
        out.append(
            f'<g><title>{escape(gid)} · {color}·{shade} · {size}: {escape(trait)}</title>'
            f'<circle cx="{cx + x:.1f}" cy="{cy + y:.1f}" r="{r}" fill="{c}" stroke="#00000033" stroke-width="1"/>'
            f'<ellipse cx="{cx + x - r * 0.35:.1f}" cy="{cy + y - r * 0.4:.1f}" rx="{r * 0.28:.1f}" ry="{r * 0.18:.1f}" fill="#ffffff" fill-opacity="0.55"/></g>')
    return "\n".join(out)


def main():
    text = open(DOC, encoding="utf-8").read()
    bowls = parse(text)
    if len(bowls["R"]) != 42 or len(bowls["J"]) != 40:
        sys.exit(f"unexpected inventory sizes: Ray {len(bowls['R'])}, Jeff {len(bowls['J'])}")

    width, height = 1080, 760
    cy = 215
    xs = [190, 540, 890]
    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" font-family="Segoe UI, Helvetica, Arial, sans-serif">',
        f'<rect width="{width}" height="{height}" fill="#f7f7f9"/>',
        '<text x="540" y="34" text-anchor="middle" font-size="22" font-weight="600" fill="#2b2b33">Ray × Jeff → RJ: the gumball bowls</text>',
    ]
    parts.append(bowl_svg(xs[0], cy, "#ecd9e4", "#b98aa8", pack(bowls["R"], 11)))
    parts.append(bowl_svg(xs[1], cy, "#d9e6ec", "#8aa8b9", pack(bowls["J"], 12)))
    parts.append(bowl_svg(xs[2], cy, "#e3dcf0", "#a393c4", []))
    for x, name, sub in [(xs[0], "Ray", "42 gumballs"), (xs[1], "Jeff", "40 gumballs"), (xs[2], "RJ (offspring)", "empty, to be instantiated")]:
        parts.append(f'<text x="{x}" y="{cy + BOWL_R + 44}" text-anchor="middle" font-size="19" font-weight="600" fill="#2b2b33">{name}</text>')
        parts.append(f'<text x="{x}" y="{cy + BOWL_R + 64}" text-anchor="middle" font-size="13" fill="#5a5a66">{sub}</text>')

    # legend
    ly = 505
    parts.append(f'<line x1="40" y1="{ly - 20}" x2="1040" y2="{ly - 20}" stroke="#d0d0d8"/>')
    parts.append(f'<text x="40" y="{ly}" font-size="15" font-weight="600" fill="#2b2b33">Color = kind of trait; shade = related collection (deep, bright, light, pale)</text>')
    for i, color in enumerate(HUES):
        x = 40 + i * 145
        parts.append(f'<text x="{x}" y="{ly + 28}" font-size="13" font-weight="600" fill="#2b2b33">{color}</text>')
        parts.append(f'<text x="{x}" y="{ly + 44}" font-size="11.5" fill="#5a5a66">{LABELS[color]}</text>')
        for j, shade in enumerate(SHADES):
            parts.append(f'<circle cx="{x + 10 + j * 30}" cy="{ly + 68}" r="11" fill="{hexcolor(color, shade)}" stroke="#00000033"/>')
    parts.append(f'<text x="40" y="{ly + 106}" font-size="11.5" fill="#5a5a66">Swatches left to right: Deep, Bright, Light, Pale. Colors are never mixed.</text>')

    sy = ly + 150
    parts.append(f'<text x="40" y="{sy}" font-size="15" font-weight="600" fill="#2b2b33">Size = significance and granularity together</text>')
    sizes = [("XL", "defines the persona"), ("L", "a major capability or commitment"), ("M", "a distinct, meaningful trait"), ("S", "a fine-grained or minor trait")]
    x = 40
    for name, desc in sizes:
        r = RADIUS[name]
        parts.append(f'<circle cx="{x + 24}" cy="{sy + 42}" r="{r}" fill="#9a9aa6" stroke="#00000033"/>')
        parts.append(f'<text x="{x + 52}" y="{sy + 38}" font-size="13" font-weight="600" fill="#2b2b33">{name}</text>')
        parts.append(f'<text x="{x + 52}" y="{sy + 54}" font-size="11.5" fill="#5a5a66">{desc}</text>')
        x += 255
    parts.append(f'<text x="40" y="{sy + 92}" font-size="11.5" fill="#5a5a66">Generated from the inventory tables in gumballs.md. Hover a gumball in a browser for its trait.</text>')
    parts.append("</svg>")

    open(OUT, "w", encoding="utf-8").write("\n".join(parts))
    print("wrote", OUT, "Ray", len(bowls["R"]), "Jeff", len(bowls["J"]))


main()
