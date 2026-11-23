#!/usr/bin/env python3
"""Generates the tileable textures used by the weapon skins in Assets/Skins.

Every texture wraps seamlessly in both directions, because the skin shaders
project them triplanar and repeat them across the weapon. The output is
deterministic (fixed seeds), so re-running this script reproduces the PNGs
that are checked in.

Unity ignores folders whose name ends in "~", so this script is not imported
as an asset.

Requirements: Python 3.9+, numpy, Pillow.

    python3 generate_skin_textures.py                # writes ../Textures/*.png
    python3 generate_skin_textures.py --previews DIR # also writes previews + contact sheet
    python3 generate_skin_textures.py --check        # only verifies the textures tile

To add a pattern: write a function that returns a float array of shape
(SIZE, SIZE, 3) in 0..1, build it only from periodic ingredients
(spectral_noise, integer-frequency sines, lattices that divide SIZE), and
register it in STATIC_TEXTURES.
"""

import argparse
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

SIZE = 512


# --------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------

def rgb(hex_colour):
    hex_colour = hex_colour.lstrip("#")
    return np.array([int(hex_colour[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float64) / 255.0


def spectral_noise(seed, n, peak, sharpness=2.0, stretch=(1.0, 1.0)):
    """Periodic noise made by band-pass filtering white noise in frequency space.

    peak      dominant feature frequency in cycles per tile.
    sharpness higher values keep less high-frequency detail.
    stretch   (sx, sy) > 1 elongates features along that axis.
    """
    rng = np.random.default_rng(seed)
    spectrum = np.fft.fft2(rng.standard_normal((n, n)))
    freq = np.fft.fftfreq(n) * n
    fx = freq[None, :] * stretch[0]
    fy = freq[:, None] * stretch[1]
    radius = np.sqrt(fx * fx + fy * fy) / peak
    band = radius ** 2 * np.exp(-(radius ** sharpness))
    out = np.real(np.fft.ifft2(spectrum * band))
    return out


def fractal_noise(seed, n, peak, octaves=4, gain=0.5):
    total = np.zeros((n, n))
    amplitude = 1.0
    for octave in range(octaves):
        layer = spectral_noise(seed + 101 * octave, n, peak * (2 ** octave))
        total += amplitude * layer / layer.std()
        amplitude *= gain
    return total


def equalize(field):
    """Rank-normalise to a flat 0..1 histogram so thresholds mean coverage."""
    flat = field.ravel()
    order = np.argsort(flat, kind="stable")
    ranks = np.empty_like(order)
    ranks[order] = np.arange(flat.size)
    return (ranks / (flat.size - 1.0)).reshape(field.shape)


def normalize(field):
    lo, hi = field.min(), field.max()
    return (field - lo) / (hi - lo)


def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def downsample(image, factor):
    if factor == 1:
        return image
    h, w = image.shape[0] // factor, image.shape[1] // factor
    shape = (h, factor, w, factor) + image.shape[2:]
    return image.reshape(shape).mean(axis=(1, 3))


def periodic_blur(field, sigma):
    n = field.shape[0]
    freq = np.fft.fftfreq(n)
    kernel = np.exp(-2.0 * (np.pi * sigma) ** 2 * (freq[None, :] ** 2 + freq[:, None] ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(field) * kernel))


def grain(seed, n, amount):
    noise = spectral_noise(seed, n, n / 6.0)
    return 1.0 + amount * noise / noise.std()


def grid(n):
    coords = (np.arange(n) + 0.5) / n
    return np.meshgrid(coords, coords)


def ramp(stops, t):
    """Piecewise-linear colour ramp. stops: list of (position, colour)."""
    t = np.clip(t, 0.0, 1.0)
    positions = np.array([s[0] for s in stops])
    colours = np.stack([s[1] for s in stops])
    return np.stack([np.interp(t, positions, colours[:, c]) for c in range(3)], axis=-1)


def to_image(array):
    data = np.clip(array, 0.0, 1.0)
    return Image.fromarray((data * 255.0 + 0.5).astype(np.uint8))


# --------------------------------------------------------------------------
# Static skins
# --------------------------------------------------------------------------

def woodland_camo(n=SIZE):
    ss = 2
    m = n * ss
    image = np.empty((m, m, 3))
    image[:] = rgb("#9b8c62")
    layers = [
        (rgb("#4b5a31"), 11, 0.46, 3.0),
        (rgb("#5a4028"), 12, 0.30, 3.6),
        (rgb("#16170f"), 13, 0.13, 4.4),
    ]
    for colour, seed, coverage, peak in layers:
        field = equalize(spectral_noise(seed, m, peak, sharpness=2.6, stretch=(1.0, 1.7)))
        image[field > 1.0 - coverage] = colour
    image = downsample(image, ss)
    return image * grain(14, n, 0.035)[..., None]


def arctic_digital(n=SIZE):
    cells = 64
    coarse = spectral_noise(21, cells, 3.0)
    fine = spectral_noise(22, cells, 9.0)
    rng = np.random.default_rng(23)
    field = coarse / coarse.std() + 0.55 * fine / fine.std() + 0.25 * rng.standard_normal((cells, cells))
    field = equalize(field)
    palette = [rgb("#39424d"), rgb("#75818d"), rgb("#b4bec8"), rgb("#e8edf1")]
    thresholds = [0.14, 0.38, 0.68]
    index = np.digitize(field, thresholds)
    image = np.stack(palette)[index]
    image = np.repeat(np.repeat(image, n // cells, axis=0), n // cells, axis=1)
    return image * grain(24, n, 0.025)[..., None]


def honeycomb(n=SIZE):
    ss = 2
    m = n * ss
    columns, rows = 4, 7  # 3s*columns ~= sqrt(3)s*rows keeps hexagons within 1% of regular
    u, v = grid(m)
    px = u * columns * 3.0
    py = v * rows * np.sqrt(3.0)
    period = np.array([3.0, np.sqrt(3.0)])

    def nearest(offset):
        ix = np.round((px - offset[0]) / period[0])
        iy = np.round((py - offset[1]) / period[1])
        qx = px - (ix * period[0] + offset[0])
        qy = py - (iy * period[1] + offset[1])
        return qx, qy, ix.astype(int) % columns, iy.astype(int) % rows

    ax, ay, aix, aiy = nearest((0.0, 0.0))
    bx, by, bix, biy = nearest(period / 2.0)
    use_b = (bx * bx + by * by) < (ax * ax + ay * ay)
    qx = np.where(use_b, bx, ax)
    qy = np.where(use_b, by, ay)
    ix = np.where(use_b, bix, aix)
    iy = np.where(use_b, biy, aiy)

    # 0 at the cell centre, 1 on the hexagon edge
    hexd = np.maximum(np.abs(qy), np.abs(qx) * np.sqrt(3.0) / 2.0 + np.abs(qy) / 2.0) / (np.sqrt(3.0) / 2.0)

    rng = np.random.default_rng(31)
    shade = rng.uniform(0.75, 1.15, size=(2, rows, columns))
    accent = rng.random((2, rows, columns)) < 0.11
    lattice = use_b.astype(int)
    cell_shade = shade[lattice, iy, ix]
    cell_accent = accent[lattice, iy, ix]

    base = ramp([(0.0, rgb("#2b3038")), (0.7, rgb("#1e2229")), (1.0, rgb("#14171c"))], hexd)
    base *= cell_shade[..., None]
    accent_colour = ramp([(0.0, rgb("#ff8a1f")), (1.0, rgb("#c2470a"))], hexd)
    image = np.where(cell_accent[..., None], accent_colour, base)

    bevel = smoothstep(0.72, 0.84, hexd) * (1.0 - smoothstep(0.84, 0.9, hexd))
    image = image + bevel[..., None] * 0.10
    groove = smoothstep(0.88, 0.93, hexd)
    image = image * (1.0 - groove[..., None]) + rgb("#07080a") * groove[..., None]

    image = downsample(image, ss)
    return image * grain(32, n, 0.03)[..., None]


def prism(n=SIZE):
    ss = 2
    m = n * ss
    cells = 8
    pitch = m / cells
    rng = np.random.default_rng(41)
    jitter = rng.uniform(-0.3, 0.3, size=(cells, cells, 2))
    flip = rng.random((cells, cells)) < 0.5
    tone = rng.uniform(-0.1, 0.1, size=(cells, cells, 2))
    light = rng.uniform(0.86, 1.12, size=(cells, cells, 2))
    field = normalize(periodic_blur(equalize(spectral_noise(42, 64, 1.5)), 2.0))
    stops = [
        (0.0, rgb("#141a3a")),
        (0.28, rgb("#2b2f7c")),
        (0.52, rgb("#6a3fb0")),
        (0.76, rgb("#1f9fb0")),
        (1.0, rgb("#8cecd6")),
    ]

    def vertex(i, j):
        jx, jy = jitter[j % cells, i % cells]
        return ((i + jx) * pitch, (j + jy) * pitch)

    canvas = Image.new("RGB", (m, m))
    draw = ImageDraw.Draw(canvas)
    for j in range(-1, cells + 1):
        for i in range(-1, cells + 1):
            a, b, c, d = vertex(i, j), vertex(i + 1, j), vertex(i + 1, j + 1), vertex(i, j + 1)
            triangles = [(a, b, c), (a, c, d)] if flip[j % cells, i % cells] else [(a, b, d), (b, c, d)]
            for k, triangle in enumerate(triangles):
                cx = sum(p[0] for p in triangle) / 3.0 / m
                cy = sum(p[1] for p in triangle) / 3.0 / m
                sample = field[int(cy * 64) % 64, int(cx * 64) % 64]
                t = sample + tone[j % cells, i % cells, k]
                colour = ramp(stops, np.array([t]))[0] * light[j % cells, i % cells, k]
                fill = tuple(int(x) for x in np.clip(colour, 0.0, 1.0) * 255.0 + 0.5)
                draw.polygon(triangle, fill=fill)
    image = np.asarray(canvas, dtype=np.float64) / 255.0
    image = downsample(image, ss)
    return image * grain(43, n, 0.02)[..., None]


def halftone_pop(n=SIZE):
    ss = 2
    m = n * ss
    field = equalize(spectral_noise(51, m, 2.2, sharpness=3.0))
    region = np.digitize(field, [0.36, 0.70])
    fills = np.stack([rgb("#ffd21f"), rgb("#ff2f7e"), rgb("#19c3ff")])
    dots = np.stack([rgb("#f08a00"), rgb("#b3104f"), rgb("#0a6fd1")])

    u, v = grid(m)
    lattice = 32  # even, so the staggered dot grid wraps
    gx = u * lattice
    gy = v * lattice
    ix = np.round(gx)
    iy = np.round(gy)
    odd = (ix + iy) % 2 == 1
    # snap to the nearest lattice point with an even coordinate sum
    dx = gx - ix
    dy = gy - iy
    shift_x = np.where(np.abs(dx) >= np.abs(dy), np.sign(dx) + (dx == 0), 0.0)
    shift_y = np.where(np.abs(dx) >= np.abs(dy), 0.0, np.sign(dy) + (dy == 0))
    ix = np.where(odd, ix + shift_x, ix)
    iy = np.where(odd, iy + shift_y, iy)
    dist = np.sqrt((gx - ix) ** 2 + (gy - iy) ** 2)

    size_field = equalize(spectral_noise(52, m, 1.6, sharpness=3.0))
    dot_radius = 0.18 + 0.42 * size_field
    dot = 1.0 - smoothstep(dot_radius - 0.04, dot_radius + 0.04, dist)

    image = fills[region] * (1.0 - dot[..., None]) + dots[region] * dot[..., None]

    # comic ink line where two colour regions meet
    outline = np.zeros((m, m), dtype=bool)
    reach = 5
    for oy in range(-reach, reach + 1):
        for ox in range(-reach, reach + 1):
            if ox * ox + oy * oy <= reach * reach:
                outline |= np.roll(region, (oy, ox), axis=(0, 1)) != region
    image[outline] = rgb("#14101c")

    return downsample(image, ss)


def damascus(n=SIZE):
    u, v = grid(n)
    warp_a = fractal_noise(61, n, 1.5, octaves=3)
    warp_b = fractal_noise(62, n, 4.0, octaves=3)
    warp_a /= warp_a.std()
    warp_b /= warp_b.std()
    phase = 2.0 * np.pi * (2.0 * u + 7.0 * v) + 4.2 * warp_a + 1.3 * warp_b
    bands = 0.5 + 0.5 * np.sin(phase)
    fine = 0.5 + 0.5 * np.sin(phase * 3.0 + 1.7 * warp_b)
    value = 0.78 * smoothstep(0.15, 0.85, bands) + 0.22 * fine
    stops = [
        (0.0, rgb("#1c1f25")),
        (0.35, rgb("#4a5059")),
        (0.7, rgb("#98a0ab")),
        (1.0, rgb("#dfe4ea")),
    ]
    image = ramp(stops, value)
    return image * grain(63, n, 0.045)[..., None]



def desert_tiger(n=SIZE):
    """Sandy tiger-stripe desert camo with soft organic bands."""
    ss = 2
    m = n * ss
    u, v = grid(m)
    warp = fractal_noise(91, m, 2.0, octaves=3)
    warp /= warp.std()
    # Integer cycle counts so the sine wraps with the tile.
    stripes = 0.5 + 0.5 * np.sin(2.0 * np.pi * (5.0 * v + 1.0 * u) + 2.4 * warp)
    stripes = equalize(periodic_blur(stripes, 1.2))
    blotch = equalize(spectral_noise(92, m, 3.2, sharpness=2.8, stretch=(1.4, 0.7)))
    image = np.empty((m, m, 3))
    image[:] = rgb("#c9a66b")
    image[blotch > 0.58] = rgb("#a8844a")
    image[stripes > 0.62] = rgb("#6e5330")
    image[stripes > 0.82] = rgb("#3a2a16")
    dust = equalize(spectral_noise(93, m, 8.0, sharpness=3.0))
    image = image * (0.92 + 0.08 * dust[..., None])
    return downsample(image, ss) * grain(94, n, 0.04)[..., None]


def urban_hex(n=SIZE):
    """Cool grey hex-grid urban digital camo."""
    cells = 32
    field = equalize(spectral_noise(101, cells, 3.5) + 0.4 * spectral_noise(102, cells, 8.0))
    palette = [rgb("#2a2e34"), rgb("#4b515a"), rgb("#7a828c"), rgb("#aeb6bf")]
    index = np.digitize(field, [0.22, 0.48, 0.72])
    image = np.stack(palette)[index]
    image = np.repeat(np.repeat(image, n // cells, axis=0), n // cells, axis=1)
    u, v = grid(n)
    groove = (
        (1.0 - smoothstep(0.0, 0.015, np.abs(((u * 16) % 1.0) - 0.5)))
        + (1.0 - smoothstep(0.0, 0.015, np.abs(((v * 16) % 1.0) - 0.5)))
    )
    groove = np.clip(groove, 0.0, 1.0) * 0.18
    image = image * (1.0 - groove[..., None]) + rgb("#15181c") * groove[..., None]
    return image * grain(103, n, 0.025)[..., None]


def carbon_fiber(n=SIZE):
    """Interlaced carbon-fiber weave."""
    ss = 2
    m = n * ss
    u, v = grid(m)
    weave = 24
    a = 0.5 + 0.5 * np.sin(2.0 * np.pi * weave * u)
    b = 0.5 + 0.5 * np.sin(2.0 * np.pi * weave * v)
    cell_u = np.floor(u * weave).astype(int)
    cell_v = np.floor(v * weave).astype(int)
    horizontal = ((cell_u + cell_v) % 2) == 0
    fiber = np.where(horizontal, a, b)
    fu = np.abs(((u * weave) % 1.0) - 0.5)
    fv = np.abs(((v * weave) % 1.0) - 0.5)
    ridge = np.where(horizontal, 1.0 - smoothstep(0.42, 0.5, fu), 1.0 - smoothstep(0.42, 0.5, fv))
    value = 0.18 + 0.55 * fiber + 0.22 * ridge
    stops = [
        (0.0, rgb("#0b0c0e")),
        (0.45, rgb("#1a1c20")),
        (0.75, rgb("#2c3036")),
        (1.0, rgb("#4a5058")),
    ]
    image = ramp(stops, value)
    image = downsample(image, ss)
    return image * grain(111, n, 0.02)[..., None]


def copper_patina(n=SIZE):
    """Oxidized copper with turquoise verdigris patches."""
    base = equalize(fractal_noise(121, n, 2.2, octaves=4))
    oxide = equalize(fractal_noise(122, n, 4.0, octaves=3))
    veins = equalize(spectral_noise(123, n, 6.0, sharpness=2.4, stretch=(0.6, 1.8)))
    copper = ramp(
        [(0.0, rgb("#3a1e12")), (0.45, rgb("#8a4a28")), (0.8, rgb("#c97a3c")), (1.0, rgb("#e0a86a"))],
        base,
    )
    patina = ramp(
        [(0.0, rgb("#1f4a42")), (0.55, rgb("#2f7a68")), (1.0, rgb("#7ecfb0"))],
        oxide,
    )
    mask = smoothstep(0.55, 0.78, oxide) * (0.65 + 0.35 * veins)
    image = copper * (1.0 - mask[..., None]) + patina * mask[..., None]
    return image * grain(124, n, 0.035)[..., None]


def kryptek_fracture(n=SIZE):
    """Angular fracture / scale camo in olive and bone."""
    ss = 2
    m = n * ss
    cells = 10
    pitch = m / cells
    rng = np.random.default_rng(131)
    jitter = rng.uniform(-0.28, 0.28, size=(cells, cells, 2))
    flip = rng.random((cells, cells)) < 0.5
    tone = rng.uniform(0.0, 1.0, size=(cells, cells, 2))
    palette = [
        rgb("#2f3424"),
        rgb("#556044"),
        rgb("#8a8660"),
        rgb("#cfc6a4"),
        rgb("#1a1c14"),
    ]

    def vertex(i, j):
        jx, jy = jitter[j % cells, i % cells]
        return ((i + jx) * pitch, (j + jy) * pitch)

    canvas = Image.new("RGB", (m, m))
    draw = ImageDraw.Draw(canvas)
    for j in range(-1, cells + 1):
        for i in range(-1, cells + 1):
            a, b, c, d = vertex(i, j), vertex(i + 1, j), vertex(i + 1, j + 1), vertex(i, j + 1)
            tris = [(a, b, c), (a, c, d)] if flip[j % cells, i % cells] else [(a, b, d), (b, c, d)]
            for k, tri in enumerate(tris):
                t = tone[j % cells, i % cells, k]
                colour = palette[int(t * (len(palette) - 1e-6))]
                fill = tuple(int(x * 255 + 0.5) for x in colour)
                draw.polygon(tri, fill=fill)
                draw.line(tri + (tri[0],), fill=(18, 20, 14), width=2)
    image = np.asarray(canvas, dtype=np.float64) / 255.0
    return downsample(image, ss) * grain(132, n, 0.02)[..., None]


def night_viper(n=SIZE):
    """Near-black digital camo with sparse neon-green ticks."""
    cells = 64
    field = equalize(spectral_noise(141, cells, 2.8) + 0.55 * spectral_noise(142, cells, 7.0))
    palette = [rgb("#050607"), rgb("#12151a"), rgb("#222830"), rgb("#343c46")]
    index = np.digitize(field, [0.18, 0.45, 0.72])
    image = np.stack(palette)[index]
    image = np.repeat(np.repeat(image, n // cells, axis=0), n // cells, axis=1)
    rng = np.random.default_rng(143)
    ticks = rng.random((cells, cells)) < 0.045
    accent = np.zeros((cells, cells, 3))
    accent[ticks] = rgb("#39ff14")
    accent = np.repeat(np.repeat(accent, n // cells, axis=0), n // cells, axis=1)
    glow = periodic_blur(accent[..., 1], 1.5)[..., None]
    image = np.clip(image + accent * 0.85 + glow * np.array(rgb("#1a5c12"))[None, None, :] * 0.35, 0.0, 1.0)
    return image * grain(144, n, 0.02)[..., None]


# --------------------------------------------------------------------------
# Support textures for the animated shaders (data, imported as linear)
# --------------------------------------------------------------------------

def worley_edges(seed, n, points):
    """Periodic cell pattern: 0 on cell borders, rising towards cell centres."""
    rng = np.random.default_rng(seed)
    sites = rng.random((points, 2))
    u, v = grid(n)
    first = np.full((n, n), np.inf)
    second = np.full((n, n), np.inf)
    for sx, sy in sites:
        dx = np.abs(u - sx)
        dy = np.abs(v - sy)
        dx = np.minimum(dx, 1.0 - dx)
        dy = np.minimum(dy, 1.0 - dy)
        dist = np.sqrt(dx * dx + dy * dy)
        closer = dist < first
        second = np.where(closer, first, np.minimum(second, dist))
        first = np.where(closer, dist, first)
    return second - first


def skin_noise(n=SIZE):
    """R: soft clouds, G: independent finer clouds, B: cell borders (0) to cell centres (1)."""
    red = equalize(fractal_noise(71, n, 3.0, octaves=4))
    green = equalize(fractal_noise(72, n, 5.0, octaves=4))
    blue = normalize(worley_edges(73, n, 28)) ** 0.6
    return np.stack([red, green, blue], axis=-1)


def circuit_mask(n=SIZE):
    """R: sharp traces, G: soft halo around traces, B: per-trace random phase."""
    ss = 4
    m = n * ss
    cells = 32
    pitch = m // cells
    rng = np.random.default_rng(81)
    occupied = np.zeros((cells, cells), dtype=bool)
    diagonals = set()
    directions = [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)]
    paths = []
    for _ in range(900):
        x, y = (int(c) for c in rng.integers(0, cells, 2))
        if occupied[y, x]:
            continue
        heading = int(rng.choice([0, 2, 4, 6]))
        path = [(x, y)]
        claimed = [(x, y)]
        occupied[y, x] = True
        for _ in range(int(rng.integers(4, 15))):
            if rng.random() < 0.3:
                heading = (heading + int(rng.choice([-1, 1]))) % 8
            dx, dy = directions[heading]
            nx, ny = x + dx, y + dy
            if occupied[ny % cells, nx % cells]:
                break
            if dx and dy:
                key = (min(x, nx) % cells, min(y, ny) % cells)
                if key in diagonals:
                    break
                diagonals.add(key)
            x, y = nx, ny
            occupied[y % cells, x % cells] = True
            claimed.append((x % cells, y % cells))
            path.append((x, y))
        if len(path) >= 3:
            paths.append(path)
        else:
            for cx, cy in claimed:
                occupied[cy, cx] = False

    trace = Image.new("L", (m, m), 0)
    phase = Image.new("L", (m, m), 128)
    trace_draw = ImageDraw.Draw(trace)
    phase_draw = ImageDraw.Draw(phase)
    width = int(pitch * 0.2)
    pad = pitch * 0.3
    hole = pitch * 0.14
    for path in paths:
        phase_value = int(rng.integers(0, 256))
        for oy in (-m, 0, m):
            for ox in (-m, 0, m):
                points = [((px + 0.5) * pitch + ox, (py + 0.5) * pitch + oy) for px, py in path]
                phase_draw.line(points, fill=phase_value, width=pitch, joint="curve")
                for cx, cy in (points[0], points[-1]):
                    phase_draw.ellipse([cx - pitch * 0.5, cy - pitch * 0.5, cx + pitch * 0.5, cy + pitch * 0.5],
                                       fill=phase_value)
                trace_draw.line(points, fill=255, width=width, joint="curve")
                for cx, cy in points[1:-1]:
                    trace_draw.ellipse([cx - width / 2, cy - width / 2, cx + width / 2, cy + width / 2], fill=255)
                for cx, cy in (points[0], points[-1]):
                    trace_draw.ellipse([cx - pad, cy - pad, cx + pad, cy + pad], fill=255)
                    trace_draw.ellipse([cx - hole, cy - hole, cx + hole, cy + hole], fill=0)

    red = downsample(np.asarray(trace, dtype=np.float64) / 255.0, ss)
    green = np.clip(periodic_blur(red, 5.0) * 2.6, 0.0, 1.0)
    blue = np.asarray(phase.resize((n, n), Image.NEAREST), dtype=np.float64) / 255.0
    return np.stack([red, green, blue], axis=-1)


# name -> (generator, display name)
STATIC_TEXTURES = {
    "Skin_WoodlandCamo": (woodland_camo, "Woodland Camo"),
    "Skin_ArcticDigital": (arctic_digital, "Arctic Digital"),
    "Skin_Honeycomb": (honeycomb, "Honeycomb"),
    "Skin_Prism": (prism, "Prism"),
    "Skin_HalftonePop": (halftone_pop, "Halftone Pop"),
    "Skin_Damascus": (damascus, "Damascus"),
    # Batch 2
    "Skin_DesertTiger": (desert_tiger, "Desert Tiger"),
    "Skin_UrbanHex": (urban_hex, "Urban Hex"),
    "Skin_CarbonFiber": (carbon_fiber, "Carbon Fiber"),
    "Skin_CopperPatina": (copper_patina, "Copper Patina"),
    "Skin_KryptekFracture": (kryptek_fracture, "Kryptek Fracture"),
    "Skin_NightViper": (night_viper, "Night Viper"),
}

BATCH2_STATIC = {
    "Skin_DesertTiger": (desert_tiger, "Desert Tiger"),
    "Skin_UrbanHex": (urban_hex, "Urban Hex"),
    "Skin_CarbonFiber": (carbon_fiber, "Carbon Fiber"),
    "Skin_CopperPatina": (copper_patina, "Copper Patina"),
    "Skin_KryptekFracture": (kryptek_fracture, "Kryptek Fracture"),
    "Skin_NightViper": (night_viper, "Night Viper"),
}

SUPPORT_TEXTURES = {
    "SkinNoise": skin_noise,
    "SkinCircuitMask": circuit_mask,
}


# --------------------------------------------------------------------------
# Verification and previews
# --------------------------------------------------------------------------

def seam_ratio(image):
    """Edge-wrap discontinuity relative to the roughest step between two neighbouring
    columns (or rows) inside the texture.

    At or below about 1.0 the wrap seam is no rougher than the texture's own interior.
    Comparing against the roughest interior step, not the average one, keeps blocky
    patterns (whose block borders land on the wrap) from reading as seams.
    """
    data = image.astype(np.float64)
    wrap_x = np.abs(data[:, 0] - data[:, -1]).mean()
    wrap_y = np.abs(data[0, :] - data[-1, :]).mean()
    inner_x = np.abs(np.diff(data, axis=1)).mean(axis=(0, 2)).max()
    inner_y = np.abs(np.diff(data, axis=0)).mean(axis=(1, 2)).max()
    return wrap_x / max(inner_x, 1e-9), wrap_y / max(inner_y, 1e-9)


def load_font(size):
    for path in (
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf",
    ):
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def contact_sheet(images, labels, columns=3, thumb=384, margin=24, label_height=44):
    rows = (len(images) + columns - 1) // columns
    title_height = 64
    width = margin + columns * (thumb + margin)
    height = title_height + rows * (thumb + label_height + margin) + margin
    sheet = Image.new("RGB", (width, height), (18, 19, 23))
    draw = ImageDraw.Draw(sheet)
    draw.text((margin, 18), "Weapon skins: static textures (one tile each)", fill=(235, 237, 240), font=load_font(26))
    font = load_font(20)
    for index, (image, label) in enumerate(zip(images, labels)):
        column, row = index % columns, index // columns
        x = margin + column * (thumb + margin)
        y = title_height + row * (thumb + label_height + margin)
        sheet.paste(image.resize((thumb, thumb), Image.LANCZOS), (x, y))
        draw.text((x, y + thumb + 10), label, fill=(210, 213, 219), font=font)
    return sheet


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=os.path.join(here, "..", "Textures"), help="texture output folder")
    parser.add_argument("--previews", default=None, help="optional folder for preview PNGs and a contact sheet")
    parser.add_argument("--check", action="store_true", help="generate in memory and report seam ratios only")
    parser.add_argument("--batch2-previews", action="store_true", help="when used with --previews, only emit batch-2 static skins")
    args = parser.parse_args()

    generated = {}
    for name, (generator, _) in STATIC_TEXTURES.items():
        generated[name] = to_image(generator())
    for name, generator in SUPPORT_TEXTURES.items():
        generated[name] = to_image(generator())

    worst = 0.0
    for name, image in generated.items():
        ratio_x, ratio_y = seam_ratio(np.asarray(image))
        worst = max(worst, ratio_x, ratio_y)
        print(f"{name:22s} {image.size[0]}x{image.size[1]}  seam ratio x={ratio_x:.2f} y={ratio_y:.2f}")
    if worst > 1.25:
        raise SystemExit(f"A texture does not tile cleanly (worst seam ratio {worst:.2f}).")

    if args.check:
        return

    os.makedirs(args.out, exist_ok=True)
    for name, image in generated.items():
        image.save(os.path.join(args.out, name + ".png"), optimize=True)

    preview_names = {
        "Skin_WoodlandCamo": "woodland-camo.png",
        "Skin_ArcticDigital": "arctic-digital.png",
        "Skin_Honeycomb": "honeycomb.png",
        "Skin_Prism": "prism.png",
        "Skin_HalftonePop": "halftone-pop.png",
        "Skin_Damascus": "damascus.png",
        "Skin_DesertTiger": "desert-tiger.png",
        "Skin_UrbanHex": "urban-hex.png",
        "Skin_CarbonFiber": "carbon-fiber.png",
        "Skin_CopperPatina": "copper-patina.png",
        "Skin_KryptekFracture": "kryptek-fracture.png",
        "Skin_NightViper": "night-viper.png",
    }

    if args.previews:
        os.makedirs(args.previews, exist_ok=True)
        preview_set = BATCH2_STATIC if args.batch2_previews else STATIC_TEXTURES
        images, labels = [], []
        for name, (_, label) in preview_set.items():
            file_name = preview_names[name]
            generated[name].save(os.path.join(args.previews, file_name), optimize=True)
            images.append(generated[name])
            labels.append(label)
        title = (
            "Weapon skins batch 2: static textures (one tile each)"
            if args.batch2_previews
            else "Weapon skins: static textures (one tile each)"
        )
        sheet = contact_sheet(images, labels)
        draw = ImageDraw.Draw(sheet)
        draw.rectangle((0, 0, sheet.size[0], 64), fill=(18, 19, 23))
        draw.text((24, 18), title, fill=(235, 237, 240), font=load_font(26))
        sheet.save(os.path.join(args.previews, "contact-sheet.png"), optimize=True)


if __name__ == "__main__":
    main()
