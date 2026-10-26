#!/usr/bin/env python3
"""Generate the First Chair test mountain (Story 010): a small mountain with a chairlift, runs, trees and a park.

Usage: python3 tools/terrain/make_test_mountain.py [--out DIR]

Writes three files into DIR (default: Assets/Terrain/TestMountain/), in the test slope's formats (see
make_test_slope.py), so the sim and Unity load the mountain with the same code:

TestMountain.raw
    The heightmap: headerless unsigned 16-bit little-endian samples, 2049 x 2049 over 1024 m (0.5 m a sample),
    row 0 at z = 0 (the summit side), column index along +X.
TestMountainSnow.raw
    The snow-type map: one SnowType byte per heightmap sample. Painted from the runs and areas below, not only from
    the layout's rectangles: the zones in the layout name areas for the view.
TestMountain.txt
    The layout: the spec, the zones, the start at the top station, the park's kickers, rails and boxes, the trees, the
    runs (centre line and width) and the chairlift with its towers.

The fall line runs toward +Z, from the summit (z near 0) to the base area (z near 1024). Every shape is built from
+ - * /, square roots (correctly rounded in IEEE 754) and clipping, and the noise and the trees come from an integer
hash of a fixed seed, so the output is byte-identical on every run and every IEEE 754 machine. Every kicker faces
straight down the fall line, so no sine or cosine is needed.
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import numpy as np

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_OUT = REPO_ROOT / "Assets" / "Terrain" / "TestMountain"

HEIGHTMAP_FILE = "TestMountain.raw"
SNOW_FILE = "TestMountainSnow.raw"
LAYOUT_FILE = "TestMountain.txt"
LAYOUT_FORMAT = 1

# --- Grid and height range --------------------------------------------------------------------
RESOLUTION = 2049  # 2^11 + 1 samples per side
SIZE_MM = 1_024_000  # 1024 m square: 0.5 m a sample, fine enough to shape a kicker
MIN_HEIGHT_MM = 0
MAX_HEIGHT_MM = 400_000  # about 6.1 mm per R16 step
SIZE_M = SIZE_MM / 1000.0
CELL_M = SIZE_M / (RESOLUTION - 1)

# SnowType byte values, in order, from Packages/com.firstchair.sim/Runtime/SnowMap.cs.
SNOW_TYPES = ("groomed", "powder", "crud", "ice", "slush")
SNOW = {name: index for index, name in enumerate(SNOW_TYPES)}

# --- The mountain's profile down the fall line (metres) ---------------------------------------
BASE_HEIGHT_M = 12.0  # the base area's level
CENTRE_X_M = 512.0
# Gradient (drop per metre) down the fall line, as (z, gradient) knots; between knots it eases with a smoothstep.
# A rounded summit, a steep upper face (about 24 degrees), a long middle (about 18), an easing lower mountain (about
# 11) and the base area, which dips to a shallow hollow in front of the lift's bottom station so riders stop there.
GRADIENT_KNOTS = (
    (0.0, 0.04),
    (50.0, 0.06),
    (115.0, 0.45),
    (285.0, 0.45),
    (345.0, 0.33),
    (600.0, 0.33),
    (680.0, 0.20),
    (820.0, 0.20),
    (890.0, 0.04),
    (950.0, 0.0),
    (985.0, -0.03),
    (1024.0, -0.03),
)
# The summit is a peak, not a wall: near the top the face falls away to each side as well.
PEAK_DROP_M = 45.0  # at the map's sides, under the summit
PEAK_FADE_Z_M = (60.0, 380.0)  # full at the top, gone below this
# Lower down, the sides rise into wooded ridges, so the face reads as a bowl.
RIDGE_RISE_M = 38.0
RIDGE_START_OFF_CENTRE_M = 300.0
RIDGE_WIDTH_M = 200.0
RIDGE_FADE_Z_M = (240.0, 520.0)  # gone above this, full below

# --- Gentle undulation: hashed value noise, smoothed out of the runs ---------------------------
NOISE_LAYERS = (
    # lattice spacing metres, amplitude metres, seed
    (96.0, 5.0, 11),
    (36.0, 1.6, 12),
)
RUN_NOISE_KEPT = 0.25  # the share of the noise a run's surface keeps: gentle rollers, no bumps

# --- Areas ----------------------------------------------------------------------------------
SUMMIT_CENTRE_M = (512.0, 62.0)
SUMMIT_RADIUS_M = 48.0  # the top station's level area
SUMMIT_FALL = 0.05  # a slight fall toward +Z, so a rider starting there rolls off
BASE_AREA_M = (250.0, 860.0, 774.0, 1024.0)  # x min, z min, x max, z max: shaped level across X
BASE_BLEND_M = 30.0
TREE_LINE_HEIGHT_M = 205.0  # open snow above this, as on GMA2's upper mountains
TREE_LINE_SPREAD_M = 18.0  # trees thin out over this much height below the line

START_M = (512.0, 78.0)  # where a run begins: the top station's unload area, facing down the fall line

# --- Runs -----------------------------------------------------------------------------------
# name, difficulty, width metres, shape, snow, centre line (x, z) metres from top to foot. Shapes:
#   open  - the surface follows the mountain's own fall and cross slope, smoothed, keeping RUN_NOISE_KEPT of the noise
#   cat   - a cat track: level across its width, following the mountain along its centre line
#   gully - an open run carved into a channel GULLY_DEPTH_M deep
#   smooth - an open run with none of the noise: the park, whose features want an even surface
# Runs are shaped in this order: a later run's surface wins where two meet, so each branch comes after its trunk.
# Each centre line is a centripetal Catmull-Rom spline through its points.
RUNS = (
    ("kettle-run", "blue", 50.0, "open", "groomed", (
        (530.0, 90.0), (575.0, 145.0), (610.0, 240.0), (630.0, 360.0), (648.0, 480.0), (642.0, 600.0),
        (612.0, 720.0), (582.0, 820.0), (560.0, 900.0))),
    ("ridge-run", "red", 40.0, "open", "groomed", (
        (494.0, 90.0), (455.0, 160.0), (425.0, 280.0), (405.0, 420.0), (388.0, 560.0), (395.0, 690.0),
        (428.0, 810.0), (468.0, 900.0))),
    ("the-chute", "black", 34.0, "gully", "crud", (
        (548.0, 82.0), (612.0, 112.0), (660.0, 180.0), (676.0, 262.0), (664.0, 340.0), (640.0, 400.0))),
    ("kettle-park", "park", 56.0, "smooth", "groomed", (
        (640.0, 300.0), (685.0, 335.0), (728.0, 372.0), (760.0, 410.0), (772.0, 455.0), (772.0, 700.0),
        (744.0, 780.0), (680.0, 850.0), (610.0, 905.0))),
    ("meadow-way", "green", 11.0, "cat", "groomed", (
        (480.0, 76.0), (430.0, 82.0), (330.0, 96.0), (230.0, 118.0), (140.0, 150.0), (118.0, 172.0),
        (145.0, 192.0), (240.0, 216.0), (330.0, 248.0), (352.0, 268.0), (330.0, 290.0), (230.0, 318.0),
        (140.0, 352.0), (118.0, 374.0), (145.0, 396.0), (240.0, 428.0), (318.0, 466.0), (336.0, 490.0),
        (312.0, 512.0), (222.0, 548.0), (150.0, 590.0), (138.0, 620.0), (170.0, 650.0), (260.0, 700.0),
        (330.0, 760.0), (380.0, 830.0), (420.0, 900.0))),
)
GULLY_DEPTH_M = 7.0
GULLY_FADE_M = (40.0, 70.0)  # the channel deepens over its first metres and fills in over its last
# A branch blends in over its first metres, where it leaves its trunk, and a run that ends on another fades out over
# its last ones, so neither leaves a step or a pit on the run it joins (metres; 0 for none).
RUN_FADES = {"kettle-park": (50.0, 0.0), "the-chute": (0.0, 70.0)}
SHOULDER_M = {"open": 22.0, "smooth": 22.0, "cat": 14.0, "gully": 26.0}  # the surface blends back into the mountain over this
DENSIFY_STEP_M = 2.0  # centre lines are sampled this finely along their splines
CENTRE_SMOOTH_M = 18.0  # a run's height and cross slope along its centre line are averaged over this, each way
CAT_CROSS_KEPT = 0.15  # a cat track keeps this much of the mountain's cross slope (drains, never a wall)
ICE_ON_RUN = ("the-chute", 110.0, 170.0)  # run, from and to metres along it: a polished patch on its steepest part

# --- The park: kickers on the kicker lane, rails and boxes on the jib lane ----------------------
PARK_RUN = "kettle-park"
KICKER_LANE_X_M = 762.0
JIB_LANE_X_M = 784.0
KICKERS = (
    # lip z metres, length, width, height (metres). Each faces straight down the fall line (+Z), on the park's straight.
    (472.0, 4.0, 5.0, 0.8),
    (540.0, 5.0, 6.0, 1.2),
    (612.0, 6.0, 7.0, 1.6),
    (684.0, 8.0, 8.0, 2.2),
)
KICKER_SHOULDER_M = 1.0
KICKER_BACK_PER_HEIGHT = 2.0
RAILS = (
    # kind, z start, z end, height, width (metres). Straight down the jib lane, on the park's straight.
    ("box", 476.0, 491.0, 0.3, 0.6),
    ("rail", 546.0, 564.0, 0.5, 0.08),
    ("box", 620.0, 638.0, 0.3, 0.6),
    ("rail", 688.0, 704.0, 0.5, 0.08),
)

# --- The chairlift ------------------------------------------------------------------------------
LIFT_NAME = "kettle-chair"
LIFT_BOTTOM_M = (512.0, 952.0)
LIFT_TOP_M = (512.0, 50.0)
LIFT_STATION_CABLE_M = 5.0  # cable height above the snow at each station, where riders get on and off
LIFT_SPAN_MIN_M = 40.0
LIFT_SPAN_MAX_M = 110.0
LIFT_TOWER_MIN_M = 8.0
LIFT_TOWER_EASY_M = 14.0  # a span reaches as far as it can with a tower this tall or shorter
LIFT_TOWER_MAX_M = 26.0
LIFT_STATION_APPROACH_M = 25.0  # the cable comes down to each station over this much line, for getting on and off
LIFT_MIN_CLEARANCE_M = 7.0  # the cable clears the snow by at least this everywhere between the stations' approaches
LIFT_CORRIDOR_HALF_WIDTH_M = 12.0  # cleared of trees

# --- Trees: one per kept cell of a grid over the whole map --------------------------------------
TREE_CELL_MM = 8_000
TREE_SEED = 21
TREE_KEEP_NEAR_PER_MILLE = 850  # within TREE_NEAR_M of a run or the lift line: dense, so runs read as runs
TREE_KEEP_FAR_PER_MILLE = 620
TREE_NEAR_M = 40.0
TREE_JITTER_MM = 2_500
TREE_TRUNK_MM = (180, 380)
TREE_BRANCH_MM = (1_400, 1_450)  # the canopy's radius; the jitter and the canopy keep each tree inside its cell
TREE_RUN_CLEARANCE_M = 2.0  # a canopy stays this far off a run's edge
GLADES = (
    # x min, z min, x max, z max metres, kept per mille: open stands to ride through
    (190.0, 610.0, 300.0, 690.0, 380),
    (440.0, 560.0, 490.0, 760.0, 420),
    (690.0, 470.0, 735.0, 690.0, 400),
)

MASK64 = (1 << 64) - 1


# ----------------------------------------------------------------------------------------------
# Helpers
# ----------------------------------------------------------------------------------------------

def sample_coords_m() -> np.ndarray:
    """World coordinate of each row or column of samples, metres."""
    return np.arange(RESOLUTION, dtype=np.float64) * CELL_M


def smoothstep(t):
    """3t^2 - 2t^3 with t clipped to [0, 1]."""
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def mix64(value: int) -> int:
    """SplitMix64's mixing function: a fixed integer hash, the same on every machine."""
    value = (value + 0x9E3779B97F4A7C15) & MASK64
    value = ((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9) & MASK64
    value = ((value ^ (value >> 27)) * 0x94D049BB133111EB) & MASK64
    return value ^ (value >> 31)


def mix64_array(values: np.ndarray) -> np.ndarray:
    """mix64 over a uint64 array (numpy's uint64 arithmetic wraps, as the hash needs)."""
    v = values.astype(np.uint64) + np.uint64(0x9E3779B97F4A7C15)
    v = (v ^ (v >> np.uint64(30))) * np.uint64(0xBF58476D1CE4E5B9)
    v = (v ^ (v >> np.uint64(27))) * np.uint64(0x94D049BB133111EB)
    return v ^ (v >> np.uint64(31))


def lattice_values(seed: int, ix: np.ndarray, iz: np.ndarray) -> np.ndarray:
    """A value in [-1, 1] for each lattice corner, from an integer hash."""
    key = (np.uint64(seed) << np.uint64(48)) ^ (ix.astype(np.uint64) << np.uint64(24)) ^ iz.astype(np.uint64)
    hashed = mix64_array(key) >> np.uint64(11)  # 53 bits
    return hashed.astype(np.float64) * (2.0 / float(1 << 53)) - 1.0


def value_noise(spacing_m: float, seed: int) -> np.ndarray:
    """Smoothstep-interpolated value noise over the grid, [-1, 1]."""
    coords = sample_coords_m() / spacing_m
    cell = np.floor(coords).astype(np.int64)
    frac = smoothstep(coords - cell)
    ix0 = cell[np.newaxis, :]
    iz0 = cell[:, np.newaxis]
    fx = frac[np.newaxis, :]
    fz = frac[:, np.newaxis]
    v00 = lattice_values(seed, ix0, iz0)
    v10 = lattice_values(seed, ix0 + 1, iz0)
    v01 = lattice_values(seed, ix0, iz0 + 1)
    v11 = lattice_values(seed, ix0 + 1, iz0 + 1)
    top = v00 + (v10 - v00) * fx
    bottom = v01 + (v11 - v01) * fx
    return top + (bottom - top) * fz


def noise_m() -> np.ndarray:
    total = np.zeros((RESOLUTION, RESOLUTION))
    for spacing, amplitude, seed in NOISE_LAYERS:
        total = total + amplitude * value_noise(spacing, seed)
    return total


def gradient_at(z):
    """The fall-line gradient at z: the knots joined by smoothsteps."""
    z = np.asarray(z, dtype=np.float64)
    g = np.full_like(z, GRADIENT_KNOTS[0][1])
    for (z0, g0), (z1, g1) in zip(GRADIENT_KNOTS, GRADIENT_KNOTS[1:]):
        g = np.where(z >= z0, g0 + (g1 - g0) * smoothstep((z - z0) / (z1 - z0)), g)
    return g


def drop_profile_m() -> np.ndarray:
    """How far the fall line has dropped at each row, metres (trapezoid sum of the gradient)."""
    z = sample_coords_m()
    g = gradient_at(z)
    steps = (g[1:] + g[:-1]) * (0.5 * CELL_M)
    return np.concatenate(([0.0], np.cumsum(steps)))


def base_heights_m() -> np.ndarray:
    """The mountain before its runs: the fall-line profile, the peak, the side ridges and the noise."""
    coords = sample_coords_m()
    x = coords[np.newaxis, :]
    z = coords[:, np.newaxis]
    drop = drop_profile_m()
    heights = (BASE_HEIGHT_M + drop[-1] - drop)[:, np.newaxis] + np.zeros_like(x)
    off = (x - CENTRE_X_M) / (SIZE_M / 2.0)
    peak = PEAK_DROP_M * off * off * (1.0 - smoothstep((z - PEAK_FADE_Z_M[0]) / (PEAK_FADE_Z_M[1] - PEAK_FADE_Z_M[0])))
    ridge = RIDGE_RISE_M * smoothstep((np.abs(x - CENTRE_X_M) - RIDGE_START_OFF_CENTRE_M) / RIDGE_WIDTH_M)
    ridge = ridge * smoothstep((z - RIDGE_FADE_Z_M[0]) / (RIDGE_FADE_Z_M[1] - RIDGE_FADE_Z_M[0]))
    return heights - peak + ridge


def bilinear(heights: np.ndarray, x, z):
    """Height at (x, z) metres, interpolated between samples."""
    fx = np.clip(np.asarray(x, dtype=np.float64) / CELL_M, 0.0, RESOLUTION - 1.000001)
    fz = np.clip(np.asarray(z, dtype=np.float64) / CELL_M, 0.0, RESOLUTION - 1.000001)
    ix = np.floor(fx).astype(np.int64)
    iz = np.floor(fz).astype(np.int64)
    tx = fx - ix
    tz = fz - iz
    h00 = heights[iz, ix]
    h10 = heights[iz, ix + 1]
    h01 = heights[iz + 1, ix]
    h11 = heights[iz + 1, ix + 1]
    return (h00 + (h10 - h00) * tx) + ((h01 + (h11 - h01) * tx) - (h00 + (h10 - h00) * tx)) * tz


# ----------------------------------------------------------------------------------------------
# Runs
# ----------------------------------------------------------------------------------------------

def catmull_rom(points, step_m: float) -> np.ndarray:
    """A centripetal Catmull-Rom spline through the points, resampled every step_m of its length (the last step
    shorter): (n, 2). Centripetal (knots spaced by the square root of each span's length) keeps a short span next to a
    long one from looping or bunching, and even spacing lets the smoothing along a run treat both sides alike."""
    p = np.asarray(points, dtype=np.float64)
    padded = np.vstack([p[0] + (p[0] - p[1]), p, p[-1] + (p[-1] - p[-2])])
    fine = [p[0]]
    for i in range(1, len(padded) - 2):
        p0, p1, p2, p3 = padded[i - 1], padded[i], padded[i + 1], padded[i + 2]
        t0 = 0.0
        t1 = t0 + np.sqrt(np.sqrt(((p1 - p0) ** 2).sum()))
        t2 = t1 + np.sqrt(np.sqrt(((p2 - p1) ** 2).sum()))
        t3 = t2 + np.sqrt(np.sqrt(((p3 - p2) ** 2).sum()))
        span = np.sqrt(((p2 - p1) ** 2).sum())
        count = max(4, int(span / 0.25))
        for k in range(1, count + 1):
            t = t1 + (t2 - t1) * k / count
            a1 = ((t1 - t) * p0 + (t - t0) * p1) / (t1 - t0)
            a2 = ((t2 - t) * p1 + (t - t1) * p2) / (t2 - t1)
            a3 = ((t3 - t) * p2 + (t - t2) * p3) / (t3 - t2)
            b1 = ((t2 - t) * a1 + (t - t0) * a2) / (t2 - t0)
            b2 = ((t3 - t) * a2 + (t - t1) * a3) / (t3 - t1)
            fine.append(((t2 - t) * b1 + (t - t1) * b2) / (t2 - t1))
    fine = np.array(fine)
    length = along_m(fine)
    wanted = np.concatenate((np.arange(0.0, length[-1], step_m), [length[-1]]))
    if wanted[-1] - wanted[-2] < step_m * 0.25:
        wanted = np.delete(wanted, -2)
    return np.stack([np.interp(wanted, length, fine[:, 0]), np.interp(wanted, length, fine[:, 1])], axis=1)


def along_m(line: np.ndarray) -> np.ndarray:
    """Distance along a sampled line at each of its points, metres."""
    steps = np.sqrt((np.diff(line, axis=0) ** 2).sum(axis=1))
    return np.concatenate(([0.0], np.cumsum(steps)))


def box_smooth(values: np.ndarray, s: np.ndarray, reach_m: float) -> np.ndarray:
    """Each value averaged with as many points before it as after it, up to reach_m of line each way (fewer near the
    ends). A symmetric window keeps a steady slope exactly: a window that took in more points on one side would lift or
    drop a steep run's surface wherever the point count changed, leaving a step across it."""
    count = len(values)
    each_side = max(1, int(round(reach_m / (s[-1] / (count - 1)))))
    out = np.empty_like(values)
    for i in range(count):
        k = min(each_side, i, count - 1 - i)
        out[i] = values[i - k:i + k + 1].mean()
    return out


def run_field(line: np.ndarray, reach_m: float):
    """For every sample within reach_m of the line: the distance to it, the signed offset to its right, and the
    segment it is nearest with the position along that segment (0 to 1). Elsewhere the distance is infinite. Works
    segment by segment."""
    distance = np.full((RESOLUTION, RESOLUTION), np.inf)
    offset = np.zeros((RESOLUTION, RESOLUTION))
    segment = np.zeros((RESOLUTION, RESOLUTION), dtype=np.int64)
    position = np.zeros((RESOLUTION, RESOLUTION))
    coords = sample_coords_m()
    for i in range(len(line) - 1):
        a, b = line[i], line[i + 1]
        lo = np.minimum(a, b) - reach_m
        hi = np.maximum(a, b) + reach_m
        c0, c1 = max(0, int(lo[0] / CELL_M)), min(RESOLUTION - 1, int(hi[0] / CELL_M) + 1)
        r0, r1 = max(0, int(lo[1] / CELL_M)), min(RESOLUTION - 1, int(hi[1] / CELL_M) + 1)
        if c0 > c1 or r0 > r1:
            continue
        px = coords[np.newaxis, c0:c1 + 1]
        pz = coords[r0:r1 + 1, np.newaxis]
        d = b - a
        length2 = d[0] * d[0] + d[1] * d[1]
        t = np.clip(((px - a[0]) * d[0] + (pz - a[1]) * d[1]) / length2, 0.0, 1.0)
        qx = a[0] + t * d[0]
        qz = a[1] + t * d[1]
        dist = np.sqrt((px - qx) ** 2 + (pz - qz) ** 2)
        # Right of travel: the travel direction (dx, dz) turned a quarter clockwise seen from above is (dz, -dx).
        length = np.sqrt(length2)
        side = ((px - qx) * d[1] - (pz - qz) * d[0]) / length
        window = (slice(r0, r1 + 1), slice(c0, c1 + 1))
        closer = dist < distance[window]
        distance[window] = np.where(closer, dist, distance[window])
        offset[window] = np.where(closer, side, offset[window])
        segment[window] = np.where(closer, i, segment[window])
        position[window] = np.where(closer, t, position[window])
    return distance, offset, segment, position


def shape_run(heights: np.ndarray, noise: np.ndarray, run) -> dict:
    """Shapes one run into the heights in place. Returns its sampled centre line and fields for the snow and trees."""
    name, _difficulty, width, shape, _snow, points = run
    line = catmull_rom(points, DENSIFY_STEP_M)
    s = along_m(line)
    total = s[-1]
    half = width / 2.0
    shoulder = SHOULDER_M[shape]
    # The mountain along the centre line now, and its cross slope, both smoothed along the line.
    tangent = np.gradient(line, axis=0)
    tangent = tangent / np.sqrt((tangent ** 2).sum(axis=1))[:, np.newaxis]
    right = np.stack([tangent[:, 1], -tangent[:, 0]], axis=1)
    probe = half * 0.6
    centre_h = box_smooth(bilinear(heights, line[:, 0], line[:, 1]), s, CENTRE_SMOOTH_M)
    cross = (bilinear(heights, line[:, 0] + right[:, 0] * probe, line[:, 1] + right[:, 1] * probe)
             - bilinear(heights, line[:, 0] - right[:, 0] * probe, line[:, 1] - right[:, 1] * probe)) / (2.0 * probe)
    cross = box_smooth(cross, s, CENTRE_SMOOTH_M)
    if shape == "cat":
        cross = cross * CAT_CROSS_KEPT
    distance, offset, segment, position = run_field(line, half + shoulder)
    near = np.isfinite(distance)
    following = np.minimum(segment + 1, len(line) - 1)
    # Interpolated along the segment, so the surface runs smoothly from one centre-line point to the next.
    centre = centre_h[segment] + (centre_h[following] - centre_h[segment]) * position
    slope_across = cross[segment] + (cross[following] - cross[segment]) * position
    s_at = s[segment] + (s[following] - s[segment]) * position
    target = centre + slope_across * offset
    if shape in ("open", "gully"):
        target = target + RUN_NOISE_KEPT * noise
    if shape == "gully":
        u = np.clip(offset / (half + shoulder * 0.5), -1.0, 1.0)
        envelope = smoothstep(s_at / GULLY_FADE_M[0]) * smoothstep((total - s_at) / GULLY_FADE_M[1])
        target = target - GULLY_DEPTH_M * envelope * (1.0 - u * u)
    weight = np.where(near, 1.0 - smoothstep((distance - half) / shoulder), 0.0)
    fade_start, fade_end = RUN_FADES.get(name, (0.0, 0.0))
    if fade_start > 0.0:
        weight = weight * smoothstep(s_at / fade_start)
    if fade_end > 0.0:
        weight = weight * smoothstep((total - s_at) / fade_end)
    heights[:] = heights + (np.where(near, target, heights) - heights) * weight
    return {"name": name, "line": line, "s": s, "distance": distance, "s_at": s_at, "half": half}


def shape_summit(heights: np.ndarray) -> None:
    """The top station's area: level across, falling gently toward +Z."""
    coords = sample_coords_m()
    x = coords[np.newaxis, :]
    z = coords[:, np.newaxis]
    cx, cz = SUMMIT_CENTRE_M
    level = float(bilinear(heights, cx, cz))
    target = level - SUMMIT_FALL * (z - cz) + np.zeros_like(x)
    distance = np.sqrt((x - cx) ** 2 + (z - cz) ** 2)
    weight = 1.0 - smoothstep((distance - SUMMIT_RADIUS_M) / 20.0)
    heights[:] = heights + (target - heights) * weight


def shape_base(heights: np.ndarray) -> None:
    """The base area: the fall line's profile without the noise, level across X, blended in at its edges."""
    coords = sample_coords_m()
    x = coords[np.newaxis, :]
    z = coords[:, np.newaxis]
    x0, z0, x1, z1 = BASE_AREA_M
    drop = drop_profile_m()
    profile = (BASE_HEIGHT_M + drop[-1] - drop)[:, np.newaxis] + np.zeros_like(x)
    inside_x = smoothstep((x - x0) / BASE_BLEND_M) * smoothstep((x1 - x) / BASE_BLEND_M)
    inside_z = smoothstep((z - z0) / BASE_BLEND_M)
    weight = inside_x * inside_z
    heights[:] = heights + (profile - heights) * weight


def kicker_height_m(x, z, kicker) -> np.ndarray:
    """Height a kicker adds above the slope (as on the test slope): a ramp rising as the square of the distance from its
    foot to its height at the lip, a short fall-away behind the lip, and rounded shoulders. Faces +Z."""
    lip_z, length, width, height = kicker
    along = z - lip_z + length  # 0 at the foot, length at the lip
    off_centre = np.abs(x - KICKER_LANE_X_M)
    ratio = along / length
    behind = height * (1.0 - smoothstep((along - length) / (KICKER_BACK_PER_HEIGHT * height)))
    profile = np.where(along <= 0.0, 0.0, np.where(along <= length, height * ratio * ratio, behind))
    across = 1.0 - smoothstep((off_centre - width / 2.0) / KICKER_SHOULDER_M)
    return profile * across


def build():
    """The heights (metres), the snow map, the runs' fields and the run order, all at once."""
    noise = noise_m()
    heights = base_heights_m() + noise
    runs = []
    for run in RUNS:
        runs.append(shape_run(heights, noise, run))
    shape_summit(heights)
    shape_base(heights)
    coords = sample_coords_m()
    x = coords[np.newaxis, :]
    z = coords[:, np.newaxis]
    for kicker in KICKERS:
        heights = heights + kicker_height_m(x, z, kicker)
    snow = paint_snow(heights, runs)
    return heights, snow, runs


# ----------------------------------------------------------------------------------------------
# Snow
# ----------------------------------------------------------------------------------------------

def paint_snow(heights: np.ndarray, runs) -> np.ndarray:
    coords = sample_coords_m()
    x = coords[np.newaxis, :] + np.zeros((RESOLUTION, 1))
    z = coords[:, np.newaxis] + np.zeros((1, RESOLUTION))
    snow = np.full((RESOLUTION, RESOLUTION), SNOW["crud"], dtype=np.uint8)
    snow[heights > TREE_LINE_HEIGHT_M - TREE_LINE_SPREAD_M] = SNOW["powder"]
    by_name = {field["name"]: field for field in runs}
    # Ungroomed runs and the ice first, so a groomer keeps its snow where another run meets it.
    for name, _difficulty, _width, _shape, snow_name, _points in RUNS:
        if snow_name != "groomed":
            field = by_name[name]
            snow[field["distance"] <= field["half"]] = SNOW[snow_name]
    run_name, ice_from, ice_to = ICE_ON_RUN
    field = by_name[run_name]
    s_at = field["s_at"]
    snow[(field["distance"] <= field["half"]) & (s_at >= ice_from) & (s_at <= ice_to)] = SNOW["ice"]
    for name, _difficulty, _width, _shape, snow_name, _points in RUNS:
        if snow_name == "groomed":
            field = by_name[name]
            snow[field["distance"] <= field["half"]] = SNOW["groomed"]
    cx, cz = SUMMIT_CENTRE_M
    snow[(x - cx) ** 2 + (z - cz) ** 2 <= SUMMIT_RADIUS_M * SUMMIT_RADIUS_M] = SNOW["groomed"]
    x0, z0, x1, z1 = BASE_AREA_M
    base = (x >= x0 + BASE_BLEND_M / 2.0) & (x <= x1 - BASE_BLEND_M / 2.0) & (z >= z0 + BASE_BLEND_M / 2.0)
    snow[base] = SNOW["groomed"]
    lx, lz = LIFT_BOTTOM_M
    snow[(np.abs(x - lx) <= 45.0) & (z >= lz - 30.0) & (z <= lz + 8.0)] = SNOW["slush"]
    return snow


# ----------------------------------------------------------------------------------------------
# The lift
# ----------------------------------------------------------------------------------------------

def lift_towers(heights: np.ndarray):
    """Towers bottom to top as (x, z, height) metres. Walking up from the bottom station, each span reaches as far as
    it can (up to LIFT_SPAN_MAX_M) with a tower no taller than LIFT_TOWER_EASY_M, or else as far as the shortest
    tower allows; every span's straight cable clears the snow by LIFT_MIN_CLEARANCE_M, except within
    LIFT_STATION_APPROACH_M of a station, where the cable comes down for riders to get on and off."""
    bx, bz = LIFT_BOTTOM_M
    tx, tz = LIFT_TOP_M
    length = float(np.sqrt((tx - bx) ** 2 + (tz - bz) ** 2))
    samples = int(length / 1.0)
    t_all = np.arange(samples + 1) / samples
    ground = bilinear(heights, bx + (tx - bx) * t_all, bz + (tz - bz) * t_all)
    metres = t_all * length

    def needed(i0: int, cable0: float, i1: int, floor: float = LIFT_TOWER_MIN_M) -> float:
        """The height above the snow at sample i1 for a cable from cable0 at i0 to clear every sample between, and
        never below floor."""
        need = floor
        for i in range(i0 + 1, i1):
            # Half a metre stricter than the approach, so rounding never lets a sample at its edge go unchecked.
            if min(metres[i], length - metres[i]) < LIFT_STATION_APPROACH_M - 0.5:
                continue
            f = (metres[i] - metres[i0]) / (metres[i1] - metres[i0])
            # cable0 + (ground[i1] + h - cable0) * f >= ground[i] + clearance
            h = (ground[i] + LIFT_MIN_CLEARANCE_M - cable0) / f + cable0 - ground[i1]
            need = max(need, h)
        return need

    towers = []
    i0, cable0 = 0, float(ground[0]) + LIFT_STATION_CABLE_M
    while True:
        top_index = samples
        if metres[top_index] - metres[i0] <= LIFT_SPAN_MAX_M and needed(i0, cable0, top_index, 0.0) <= LIFT_STATION_CABLE_M:
            return towers
        best = None
        for i1 in range(i0 + int(LIFT_SPAN_MIN_M), min(top_index - int(LIFT_SPAN_MIN_M / 2), i0 + int(LIFT_SPAN_MAX_M)) + 1):
            h = needed(i0, cable0, i1)
            if h <= LIFT_TOWER_EASY_M:
                best = (i1, h)  # the farthest easy tower so far
            elif best is None or (best[1] > LIFT_TOWER_EASY_M and h < best[1]):
                best = (i1, h) if h <= LIFT_TOWER_MAX_M else best
        if best is None:
            raise ValueError("The lift's cable cannot clear the snow; raise LIFT_TOWER_MAX_M or shorten the spans.")
        i1, h = best
        h = math.ceil(h * 10.0) / 10.0  # whole decimetres, rounded up so the cable only rises
        towers.append((bx + (tx - bx) * t_all[i1], bz + (tz - bz) * t_all[i1], h))
        i0, cable0 = i1, float(ground[i1]) + h


# ----------------------------------------------------------------------------------------------
# Trees
# ----------------------------------------------------------------------------------------------

def cell_hash(seed: int, column: int, row: int, salt: int) -> int:
    return mix64((seed << 48) ^ (column << 32) ^ (row << 16) ^ salt)


def hashed_in_range(value: int, low: int, high: int) -> int:
    return low + value % (high - low + 1)


def run_clearance_m(runs) -> np.ndarray:
    """At each sample, how far it is outside the nearest run's edge, metres (negative inside a run)."""
    clearance = np.full((RESOLUTION, RESOLUTION), np.inf)
    for field in runs:
        clearance = np.minimum(clearance, field["distance"] - field["half"])
    return clearance


def place_trees(heights: np.ndarray, runs):
    """Trees as (x, z, trunk radius, branch radius) millimetres: at most one per cell, each canopy inside its cell,
    none on a run (with clearance), the lift line, the summit or base areas, or above the tree line."""
    clearance = run_clearance_m(runs)
    cells = SIZE_MM // TREE_CELL_MM
    half_cell = TREE_CELL_MM // 2
    if TREE_JITTER_MM + TREE_BRANCH_MM[1] > half_cell:
        raise ValueError("A canopy could leave its cell; shrink the jitter or the branch radius.")
    bx, _bz = LIFT_BOTTOM_M
    trees = []
    for row in range(cells):
        for column in range(cells):
            x_mm = column * TREE_CELL_MM + half_cell + hashed_in_range(cell_hash(TREE_SEED, column, row, 1), -TREE_JITTER_MM, TREE_JITTER_MM)
            z_mm = row * TREE_CELL_MM + half_cell + hashed_in_range(cell_hash(TREE_SEED, column, row, 2), -TREE_JITTER_MM, TREE_JITTER_MM)
            branch = hashed_in_range(cell_hash(TREE_SEED, column, row, 4), *TREE_BRANCH_MM)
            x, z = x_mm / 1000.0, z_mm / 1000.0
            reach = branch / 1000.0
            if abs(x - bx) < LIFT_CORRIDOR_HALF_WIDTH_M + reach:
                continue
            ix, iz = int(round(x / CELL_M)), int(round(z / CELL_M))
            if clearance[iz, ix] < reach + TREE_RUN_CLEARANCE_M:
                continue
            cx, cz = SUMMIT_CENTRE_M
            if (x - cx) ** 2 + (z - cz) ** 2 < (SUMMIT_RADIUS_M + 25.0) ** 2:
                continue
            x0, z0, x1, z1 = BASE_AREA_M
            if x0 - reach <= x <= x1 + reach and z >= z0 - reach:
                continue
            height = heights[iz, ix]
            if height > TREE_LINE_HEIGHT_M:
                continue
            keep = TREE_KEEP_NEAR_PER_MILLE if clearance[iz, ix] < TREE_NEAR_M else TREE_KEEP_FAR_PER_MILLE
            for gx0, gz0, gx1, gz1, glade_keep in GLADES:
                if gx0 <= x <= gx1 and gz0 <= z <= gz1:
                    keep = glade_keep
            # Thinning out toward the tree line.
            if height > TREE_LINE_HEIGHT_M - TREE_LINE_SPREAD_M:
                keep = keep * (TREE_LINE_HEIGHT_M - height) / TREE_LINE_SPREAD_M
            if cell_hash(TREE_SEED, column, row, 0) % 1000 >= keep:
                continue
            trunk = hashed_in_range(cell_hash(TREE_SEED, column, row, 3), *TREE_TRUNK_MM)
            trees.append((x_mm, z_mm, trunk, branch))
    return trees


# ----------------------------------------------------------------------------------------------
# Output
# ----------------------------------------------------------------------------------------------

def encode_r16(heights_m: np.ndarray) -> bytes:
    """Rounds heights to the nearest R16 sample and packs them little-endian."""
    scaled = (heights_m * 1000.0 - MIN_HEIGHT_MM) * (65535.0 / (MAX_HEIGHT_MM - MIN_HEIGHT_MM))
    samples = np.rint(scaled)
    if samples.min() < 0.0 or samples.max() > 65535.0:
        raise ValueError("The terrain leaves the height range; widen MIN_HEIGHT_MM or MAX_HEIGHT_MM.")
    return samples.astype("<u2").tobytes()


def mm(value_m: float) -> int:
    return int(round(value_m * 1000.0))


def layout_text(trees, towers) -> str:
    lines = [
        "# First Chair test mountain layout. Generated by tools/terrain/make_test_mountain.py: do not edit by hand.",
        "# Read by SlopeLayout.Parse (Packages/com.firstchair.sim/Runtime/SlopeLayout.cs).",
        "# One record per line, fields separated by spaces, all numbers whole; a line starting '#' is a comment.",
        "# Lengths and positions are millimetres. x runs along heightmap columns and z along rows; row 0 is",
        "# z = 0, the summit side, and the fall line runs toward +Z.",
        "# Facing is millidegrees clockwise seen from above (Unity's yaw): 0 = +Z, 90000 = +X.",
        "# The snow map is painted from the runs and areas, not only from the zones: the zones name areas for the view.",
        f"format {LAYOUT_FORMAT}",
        f"# spec <sizeX> <sizeZ> <minHeight> <maxHeight>: the HeightmapSpec of {HEIGHTMAP_FILE}",
        f"spec {SIZE_MM} {SIZE_MM} {MIN_HEIGHT_MM} {MAX_HEIGHT_MM}",
        f"# background <snow>: the snow below the tree line off the runs in {SNOW_FILE}",
        "background crud",
        "# zone <name> <snow> <xMin> <zMin> <xMax> <zMax>",
    ]
    cx, cz = SUMMIT_CENTRE_M
    r = SUMMIT_RADIUS_M
    lines.append(f"zone summit groomed {mm(cx - r)} {mm(cz - r)} {mm(cx + r)} {mm(cz + r)}")
    x0, z0, x1, z1 = BASE_AREA_M
    lines.append(f"zone base groomed {mm(x0 + BASE_BLEND_M / 2.0)} {mm(z0 + BASE_BLEND_M / 2.0)} {mm(x1 - BASE_BLEND_M / 2.0)} {mm(z1)}")
    lines.append(f"zone park groomed {mm(KICKER_LANE_X_M - 18.0)} {mm(455.0)} {mm(JIB_LANE_X_M + 12.0)} {mm(720.0)}")
    lines += [
        "# start <x> <z>: where a run begins, at the top station",
        f"start {mm(START_M[0])} {mm(START_M[1])}",
        "# kicker <lipX> <lipZ> <facing> <length> <width> <height>",
    ]
    for lip_z, length, width, height in KICKERS:
        lines.append(f"kicker {mm(KICKER_LANE_X_M)} {mm(lip_z)} 0 {mm(length)} {mm(width)} {mm(height)}")
    lines.append("# rail|box <x0> <z0> <x1> <z1> <height> <width>")
    for kind, z_start, z_end, height, width in RAILS:
        lines.append(f"{kind} {mm(JIB_LANE_X_M)} {mm(z_start)} {mm(JIB_LANE_X_M)} {mm(z_end)} {mm(height)} {mm(width)}")
    lines.append("# run <name> <difficulty> <width> <x0> <z0> <x1> <z1> ...: the centre line from top to foot")
    for name, difficulty, width, _shape, _snow, points in RUNS:
        lines.append(f"run {name} {difficulty} {mm(width)} " + " ".join(f"{mm(px)} {mm(pz)}" for px, pz in points))
    lines.append("# lift <name> <bottomX> <bottomZ> <topX> <topZ>; tower <lift> <x> <z> <height>, bottom to top")
    lines.append(f"lift {LIFT_NAME} {mm(LIFT_BOTTOM_M[0])} {mm(LIFT_BOTTOM_M[1])} {mm(LIFT_TOP_M[0])} {mm(LIFT_TOP_M[1])}")
    for tx, tz, th in towers:
        lines.append(f"tower {LIFT_NAME} {mm(tx)} {mm(tz)} {mm(th)}")
    lines.append("# tree <x> <z> <trunkRadius> <branchRadius>")
    lines += [f"tree {x} {z} {trunk} {branch}" for x, z, trunk, branch in trees]
    return "\n".join(lines) + "\n"


def write(out_dir: Path) -> dict[str, Path]:
    """Generates the mountain and writes its three files into out_dir. Returns their paths by name."""
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    heights, snow, runs = build()
    towers = lift_towers(heights)
    trees = place_trees(heights, runs)
    outputs = {
        HEIGHTMAP_FILE: encode_r16(heights),
        SNOW_FILE: snow.tobytes(),
        LAYOUT_FILE: layout_text(trees, towers).encode("ascii"),
    }
    paths = {}
    for name, data in outputs.items():
        path = out_dir / name
        path.write_bytes(data)
        paths[name] = path
    return paths


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Generate the First Chair test mountain (Story 010).")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT, help="output directory (default: %(default)s)")
    args = parser.parse_args(argv)
    for name, path in write(args.out).items():
        print(f"{path}: {path.stat().st_size} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
