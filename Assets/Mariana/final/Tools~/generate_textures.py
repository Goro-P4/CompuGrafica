"""Procedural textures for the final VFX demo.

Run from any folder:  python generate_textures.py
Writes PNG files into ../Textures (relative to this script).
Only Pillow and the standard library are used, everything is generated from math.
"""
import math
import os
import random

from PIL import Image, ImageFilter

OUT_DIR = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "Textures"))


def clamp01(v):
    return 0.0 if v < 0.0 else 1.0 if v > 1.0 else v


def to_byte(v):
    return int(round(clamp01(v) * 255.0))


def gauss(x, sigma):
    return math.exp(-(x * x) / (2.0 * sigma * sigma))


def smoothstep(e0, e1, x):
    t = clamp01((x - e0) / (e1 - e0))
    return t * t * (3.0 - 2.0 * t)


class TileNoise:
    """Value noise that tiles on an integer period (periodic lattice)."""

    def __init__(self, period_x, period_y, seed):
        rng = random.Random(seed)
        self.px, self.py = period_x, period_y
        self.grid = [[rng.random() for _ in range(period_x)] for _ in range(period_y)]

    def sample(self, x, y):
        x0, y0 = math.floor(x), math.floor(y)
        fx, fy = x - x0, y - y0
        fx = fx * fx * (3 - 2 * fx)
        fy = fy * fy * (3 - 2 * fy)
        g = self.grid
        a = g[y0 % self.py][x0 % self.px]
        b = g[y0 % self.py][(x0 + 1) % self.px]
        c = g[(y0 + 1) % self.py][x0 % self.px]
        d = g[(y0 + 1) % self.py][(x0 + 1) % self.px]
        return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy


def fbm(noises, u, v):
    total, amp, norm = 0.0, 1.0, 0.0
    for n in noises:
        total += n.sample(u * n.px, v * n.py) * amp
        norm += amp
        amp *= 0.5
    return total / norm


def save(img, name):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name)
    img.save(path)
    print("wrote", path)


# ---------------------------------------------------------------- laser
def laser_mask(w=256, h=64):
    """R = hot core, G = soft glow, B = broken outer rims. Tiles along U."""
    img = Image.new("RGBA", (w, h))
    px = img.load()
    glow_noise = [TileNoise(6, 3, 11), TileNoise(12, 6, 12), TileNoise(24, 12, 13)]
    rim_noise = [TileNoise(10, 2, 21), TileNoise(20, 4, 22)]
    for y in range(h):
        v = (y + 0.5) / h
        dv = v - 0.5
        for x in range(w):
            u = (x + 0.5) / w
            # core: thin line that breathes with 3 and 5 harmonics (period 1 -> tiles)
            flicker = 0.82 + 0.1 * math.sin(u * math.tau * 3) + 0.08 * math.sin(u * math.tau * 5 + 1.3)
            core = gauss(dv, 0.045) * flicker
            # glow: wide, clumpy
            gn = fbm(glow_noise, u, v)
            glow = gauss(dv, 0.17) * (0.45 + 0.75 * gn)
            # rims: two thin bands near the edges, broken into dashes
            band = max(gauss(abs(dv) - 0.3, 0.035), 0.0)
            rn = fbm(rim_noise, u, v)
            rim = band * smoothstep(0.42, 0.62, rn)
            r, g, b = clamp01(core * 1.15), clamp01(glow), clamp01(rim * 1.3)
            a = clamp01(max(r, g * 0.9, b))
            px[x, y] = (to_byte(r), to_byte(g), to_byte(b), to_byte(a))
    save(img, "T_LaserMask_RGB.png")


# ---------------------------------------------------------------- orb
def white_alpha(size, alpha_fn):
    img = Image.new("RGBA", (size, size))
    px = img.load()
    for y in range(size):
        for x in range(size):
            u = (x + 0.5) / size * 2 - 1
            v = (y + 0.5) / size * 2 - 1
            px[x, y] = (255, 255, 255, to_byte(alpha_fn(u, v)))
    return img


def orb_circle():
    def fn(u, v):
        r = math.hypot(u, v)
        body = (1 - smoothstep(0.0, 0.62, r)) * 0.55
        ring = gauss(r - 0.62, 0.06)
        halo = (1 - smoothstep(0.55, 1.0, r)) * 0.25
        return body + ring + halo
    save(white_alpha(128, fn), "T_OrbCircle.png")


def orb_spark():
    def fn(u, v):
        r = math.hypot(u, v)
        dot = 1 - smoothstep(0.0, 0.45, r)
        glint = gauss(u, 0.05) * gauss(v, 0.5) + gauss(v, 0.05) * gauss(u, 0.5)
        return dot + glint * 0.6
    save(white_alpha(64, fn), "T_OrbSpark.png")


def orb_trail_streak(w=128, h=32):
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for y in range(h):
        dv = (y + 0.5) / h - 0.5
        for x in range(w):
            u = (x + 0.5) / w
            a = gauss(dv, 0.16) * (1 - u) ** 0.6
            px[x, y] = (255, 255, 255, to_byte(a))
    save(img, "T_OrbSparkTrail.png")


def bolt_path(rng, steps, spread):
    """Midpoint-displacement lightning from top to bottom, returns list of (x, y) in 0..1."""
    pts = [(0.5, 0.04), (0.5, 0.96)]
    disp = spread
    for _ in range(steps):
        nxt = []
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            mx = (x0 + x1) / 2 + rng.uniform(-disp, disp)
            my = (y0 + y1) / 2
            nxt += [(x0, y0), (min(0.85, max(0.15, mx)), my)]
        nxt.append(pts[-1])
        pts = nxt
        disp *= 0.55
    return pts


def draw_bolt(px, size, ox, oy, pts, thickness):
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        steps = 40
        for i in range(steps + 1):
            t = i / steps
            cx = (x0 + (x1 - x0) * t) * size
            cy = (y0 + (y1 - y0) * t) * size
            rad = thickness * 3
            for yy in range(int(cy - rad), int(cy + rad) + 1):
                for xx in range(int(cx - rad), int(cx + rad) + 1):
                    if 0 <= xx < size and 0 <= yy < size:
                        d = math.hypot(xx + 0.5 - cx, yy + 0.5 - cy)
                        a = max(1 - smoothstep(0, thickness, d), 0.35 * gauss(d, thickness * 1.6))
                        cur = px[ox + xx, oy + yy][3] / 255.0
                        if a > cur:
                            px[ox + xx, oy + yy] = (255, 255, 255, to_byte(a))


def orb_bolt():
    """2x2 flipbook of jagged bolts, each with a small fork."""
    cell = 128
    img = Image.new("RGBA", (cell * 2, cell * 2), (255, 255, 255, 0))
    px = img.load()
    rng = random.Random(4242)
    for i in range(4):
        ox, oy = (i % 2) * cell, (i // 2) * cell
        main = bolt_path(rng, 6, 0.22)
        draw_bolt(px, cell, ox, oy, main, 1.6)
        start = main[rng.randint(len(main) // 4, len(main) // 2)]
        fork = [(start[0] + (p[0] - 0.5) * 0.8 + rng.uniform(0.05, 0.15) * (1 if i % 2 else -1),
                 start[1] + (p[1] - 0.04) * 0.45) for p in bolt_path(rng, 4, 0.18)]
        draw_bolt(px, cell, ox, oy, [(min(0.95, max(0.05, x)), min(0.97, y)) for x, y in fork], 1.0)
    save(img, "T_OrbBolt.png")


def orb_smoke(size=128):
    noises = [TileNoise(4, 4, 31), TileNoise(8, 8, 32), TileNoise(16, 16, 33), TileNoise(32, 32, 34)]

    def fn(u, v):
        r = math.hypot(u, v)
        n = fbm(noises, (u + 1) / 2, (v + 1) / 2)
        falloff = 1 - smoothstep(0.25, 1.0, r + (n - 0.5) * 0.5)
        return falloff * smoothstep(0.3, 0.75, n) * 0.9
    img = white_alpha(size, fn).filter(ImageFilter.GaussianBlur(1.2))
    save(img, "T_OrbSmoke.png")


# ---------------------------------------------------------------- trail
def trail_streak(w=512, h=256):
    """Trail texture: black background, white zigzag strokes of decreasing size and opacity,
    wrapped horizontally, then a horizontal motion blur and soft erased borders. Tiles along U."""
    rng = random.Random(2024)
    canvas = Image.new("L", (w, h), 0)
    layers = [  # (brush radius px, opacity, number of strokes)
        (30, 0.16, 2), (18, 0.28, 3), (10, 0.5, 3), (5, 0.95, 4),
    ]
    for radius, opacity, count in layers:
        layer = Image.new("L", (w, h), 0)
        px = layer.load()
        for _ in range(count):
            # zigzag: random points across the whole width, kept away from the borders
            x = rng.uniform(0, w)
            pts = []
            for _ in range(rng.randint(4, 7)):
                pts.append((x, rng.uniform(h * 0.28, h * 0.72)))
                x += rng.uniform(w * 0.08, w * 0.22)
            for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
                steps = int(math.hypot(x1 - x0, y1 - y0) / 2) + 1
                for i in range(steps + 1):
                    cx = x0 + (x1 - x0) * i / steps
                    cy = y0 + (y1 - y0) * i / steps
                    for yy in range(int(cy - radius), int(cy + radius) + 1):
                        if not 0 <= yy < h:
                            continue
                        for xx in range(int(cx - radius), int(cx + radius) + 1):
                            d = math.hypot(xx - cx, yy - cy)
                            if d > radius:
                                continue
                            a = (1 - smoothstep(radius * 0.2, radius, d)) * opacity
                            wx = xx % w  # wrap-around mode, so it tiles
                            if a * 255 > px[wx, yy]:
                                px[wx, yy] = int(a * 255)
        # screen blend: keeps the brightest values of both layers
        a = canvas.load(); b = layer.load()
        for yy in range(h):
            for xx in range(w):
                va, vb = a[xx, yy] / 255.0, b[xx, yy] / 255.0
                a[xx, yy] = to_byte(1 - (1 - va) * (1 - vb))
    # horizontal motion blur with wrap (angle 180, long length)
    src = canvas.load()
    blurred = Image.new("L", (w, h))
    dst = blurred.load()
    length = 38
    for yy in range(h):
        row = [src[xx, yy] for xx in range(w)]
        acc = sum(row[(xx) % w] for xx in range(-length, length + 1))
        for xx in range(w):
            dst[xx, yy] = int(acc / (2 * length + 1))
            acc += row[(xx + length + 1) % w] - row[(xx - length) % w]
    blurred = blurred.filter(ImageFilter.GaussianBlur(2))
    # eraser pass: soften top and bottom and boost contrast a little
    out = Image.new("RGB", (w, h))
    bp, op = blurred.load(), out.load()
    for yy in range(h):
        v = (yy + 0.5) / h
        edge = smoothstep(0.12, 0.38, v) * smoothstep(0.88, 0.62, v)
        for xx in range(w):
            c = to_byte(clamp01((bp[xx, yy] / 255.0) * 1.3) ** 1.1 * edge)
            op[xx, yy] = (c, c, c)
    save(out, "T_TrailStreak.png")


if __name__ == "__main__":
    laser_mask()
    orb_circle()
    orb_spark()
    orb_trail_streak()
    orb_bolt()
    orb_smoke()
    trail_streak()
