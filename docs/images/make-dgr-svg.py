"""Draw Ray's, Jeff's and RJ's gumball bowls from the inventory in docs/DGR.md.

Writes dgr-bowls.svg (RJ's bowl empty) and dgr-bowls-rj.svg (RJ's proposed bowl filled).

Color = trait type, shade = related collection, size = significance and granularity.
Only the inventory tables are read, so the picture cannot drift from the tags.
Regenerate with: python docs/images/make-dgr-svg.py
"""
import colorsys
import math
import os
import random
import re
import sys
from xml.sax.saxutils import escape

HERE = os.path.dirname(os.path.abspath(__file__))
DOC = os.path.join(HERE, "..", "DGR.md")
OUT = os.path.join(HERE, "dgr-bowls.svg")
OUT_RJ = os.path.join(HERE, "dgr-bowls-rj.svg")

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
    """Items are (id, trait, color, shade, size, color2, shade2, rule)."""
    bowls = {"R": [], "J": [], "RJ": []}
    row = re.compile(r"^\| ([RJ])-(\d+) \| (.*?) \| (?:<img[^>]*> )?(\w+)·(\w+) \| (XL|L|M|S) \|")
    rj_row = re.compile(r"^\| RJ-(\d+) \| (.*?) \| (.*?) \| (XL|L|M|S) \| (.*?) \| (\w+) \|")
    for line in doc_text.splitlines():
        m = row.match(line)
        if m:
            who, num, trait, color, shade, size = m.groups()
            bowls[who].append((f"{who}-{num}", trait, color, shade, size, None, None, ""))
            continue
        m = rj_row.match(line)
        if m:
            num, trait, cell, size, origin, rule = m.groups()
            cs = re.findall(r"(\w+)·(\w+)", cell)
            c2, s2 = cs[1] if len(cs) > 1 else (None, None)
            bowls["RJ"].append((f"RJ-{num}", trait, cs[0][0], cs[0][1], size, c2, s2, rule, origin))
    return bowls


def pack(items, seed, bowl_r=BOWL_R):
    """Greedy random circle packing inside the bowl, largest first."""
    rng = random.Random(seed)
    order = sorted(items, key=lambda it: -RADIUS[it[4]])
    placed = []
    for it in order:
        r = RADIUS[it[4]]
        for _ in range(20000):
            ang = rng.uniform(0, 2 * math.pi)
            dist = (bowl_r - 8 - r) * math.sqrt(rng.random())
            x, y = dist * math.cos(ang), dist * math.sin(ang)
            if all(math.hypot(x - px, y - py) >= r + pr + GAP for _, px, py, pr in placed):
                placed.append((it, x, y, r))
                break
        else:
            return pack_dense(items, bowl_r)
    # drawing order: big first so small ones sit on top and stay visible
    return [(it, x, y, r) for it, x, y, r in placed]


def pack_dense(items, bowl_r):
    """Fallback for crowded bowls: put each gumball, largest first, at the free grid spot nearest the center."""
    order = sorted(items, key=lambda it: -RADIUS[it[4]])
    spots = sorted(((x, y) for x in range(-bowl_r, bowl_r + 1, 2) for y in range(-bowl_r, bowl_r + 1, 2)),
                   key=lambda p: p[0] ** 2 + p[1] ** 2)
    placed = []
    for it in order:
        r = RADIUS[it[4]]
        for x, y in spots:
            if math.hypot(x, y) > bowl_r - 8 - r:
                continue
            if all(math.hypot(x - px, y - py) >= r + pr + GAP for _, px, py, pr in placed):
                placed.append((it, x, y, r))
                break
        else:
            sys.exit(f"could not place {it[0]}")
    return placed


def gumball_svg(item, x, y, r, cx, cy, uid):
    gid, trait, color, shade, size, color2, shade2, rule = item[:8]
    c = hexcolor(color, shade)
    px, py = cx + x, cy + y
    tip = f"{escape(gid)} · {color}·{shade}" + (f" + {color2}·{shade2}" if color2 else "") + f" · {size}"
    if rule:
        tip += f" · {rule} ({escape(item[8])})"
    stroke = 'stroke="#00000033" stroke-width="1"'
    parts = [f"<g><title>{tip}: {escape(trait)}</title>"]
    if color2:
        c2 = hexcolor(color2, shade2)
        cid = f"sw{uid}"
        parts.append(f'<clipPath id="{cid}"><circle cx="{px:.1f}" cy="{py:.1f}" r="{r}"/></clipPath>')
        parts.append(f'<circle cx="{px:.1f}" cy="{py:.1f}" r="{r}" fill="{c}"/>')
        parts.append(f'<polygon points="{px - r:.1f},{py + r:.1f} {px + r:.1f},{py - r:.1f} {px + r:.1f},{py + r:.1f}" fill="{c2}" clip-path="url(#{cid})"/>')
        parts.append(f'<circle cx="{px:.1f}" cy="{py:.1f}" r="{r}" fill="none" {stroke}/>')
    else:
        parts.append(f'<circle cx="{px:.1f}" cy="{py:.1f}" r="{r}" fill="{c}" {stroke}/>')
    parts.append(f'<ellipse cx="{px - r * 0.35:.1f}" cy="{py - r * 0.4:.1f}" rx="{r * 0.28:.1f}" ry="{r * 0.18:.1f}" fill="#ffffff" fill-opacity="0.55"/></g>')
    return "".join(parts)


def bowl_svg(cx, cy, fill, stroke, placed, bowl_r=BOWL_R):
    out = [f'<circle cx="{cx}" cy="{cy}" r="{bowl_r + 10}" fill="{fill}" stroke="{stroke}" stroke-width="5"/>',
           f'<circle cx="{cx}" cy="{cy}" r="{bowl_r}" fill="#ffffff" fill-opacity="0.55" stroke="{stroke}" stroke-opacity="0.5" stroke-width="2"/>']
    for n, (item, x, y, r) in enumerate(placed):
        out.append(gumball_svg(item, x, y, r, cx, cy, f"{cx}_{n}"))
    return "\n".join(out)


def build(bowls, rj, out_path):
    """Draw the three bowls and legend. rj is the list of RJ gumballs, or empty for an empty bowl."""
    dy = 20 if rj else 0
    width, height = 1080, 760 + (75 + dy if rj else 0)
    cy = 215 + dy
    rj_r = 168
    xs = [190, 540, 890]
    title = "Ray × Jeff → RJ: the gumball bowls" + (" (RJ proposed)" if rj else "")
    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" font-family="Segoe UI, Helvetica, Arial, sans-serif">',
        f'<rect width="{width}" height="{height}" fill="#f7f7f9"/>',
        f'<text x="540" y="34" text-anchor="middle" font-size="22" font-weight="600" fill="#2b2b33">{title}</text>',
    ]
    parts.append(bowl_svg(xs[0], cy, "#ecd9e4", "#b98aa8", pack(bowls["R"], 11)))
    parts.append(bowl_svg(xs[1], cy, "#d9e6ec", "#8aa8b9", pack(bowls["J"], 12)))
    parts.append(bowl_svg(xs[2], cy, "#e3dcf0", "#a393c4", pack(rj, 13, rj_r) if rj else [], rj_r if rj else BOWL_R))
    rj_sub = f"{len(rj)} gumballs (proposed)" if rj else "empty, to be instantiated"
    for x, name, sub in [(xs[0], "Ray", "42 gumballs"), (xs[1], "Jeff", "40 gumballs"), (xs[2], "RJ (offspring)", rj_sub)]:
        parts.append(f'<text x="{x}" y="{cy + BOWL_R + 44}" text-anchor="middle" font-size="19" font-weight="600" fill="#2b2b33">{name}</text>')
        parts.append(f'<text x="{x}" y="{cy + BOWL_R + 64}" text-anchor="middle" font-size="13" fill="#5a5a66">{sub}</text>')

    # legend
    ly = 505 + dy
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
    foot = sy + 92
    if rj:
        ry = sy + 100
        parts.append(f'<text x="40" y="{ry}" font-size="15" font-weight="600" fill="#2b2b33">Swirl = complementary traits of two colors fused into one gumball</text>')
        cxs, cys = 54, ry + 30
        parts.append(f'<clipPath id="legend-swirl"><circle cx="{cxs}" cy="{cys}" r="12"/></clipPath>')
        parts.append(f'<circle cx="{cxs}" cy="{cys}" r="12" fill="{hexcolor("Orange", "Bright")}"/>')
        parts.append(f'<polygon points="{cxs - 12},{cys + 12} {cxs + 12},{cys - 12} {cxs + 12},{cys + 12}" fill="{hexcolor("Yellow", "Light")}" clip-path="url(#legend-swirl)"/>')
        parts.append(f'<circle cx="{cxs}" cy="{cys}" r="12" fill="none" stroke="#00000033" stroke-width="1"/>')
        parts.append(f'<text x="76" y="{cys + 4}" font-size="12" fill="#2b2b33">Hover a gumball in a browser to see which parent gumballs it came from and how (carried, merged, fused, swirl, resolution).</text>')
        foot = ry + 62
    parts.append(f'<text x="40" y="{foot}" font-size="11.5" fill="#5a5a66">Generated from the inventory tables in DGR.md. Hover a gumball in a browser for its trait.</text>')
    parts.append("</svg>")

    open(out_path, "w", encoding="utf-8").write("\n".join(parts))
    print("wrote", out_path, "Ray", len(bowls["R"]), "Jeff", len(bowls["J"]), "RJ", len(rj))


def main():
    text = open(DOC, encoding="utf-8").read()
    bowls = parse(text)
    if len(bowls["R"]) != 42 or len(bowls["J"]) != 40:
        sys.exit(f"unexpected inventory sizes: Ray {len(bowls['R'])}, Jeff {len(bowls['J'])}")
    if bowls["RJ"] and len(bowls["RJ"]) != len({g[0] for g in bowls["RJ"]}):
        sys.exit("duplicate RJ ids")
    build(bowls, [], OUT)
    if bowls["RJ"]:
        build(bowls, bowls["RJ"], OUT_RJ)


main()
