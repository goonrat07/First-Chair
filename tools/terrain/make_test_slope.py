#!/usr/bin/env python3
"""Generate the First Chair test slope (Story 003, acceptance criterion 1).

Usage: python3 tools/terrain/make_test_slope.py [--out DIR]

Writes three files into DIR (default: Assets/Terrain/TestSlope/):

TestSlope.raw
    The heightmap. Headerless unsigned 16-bit little-endian samples, row-major, N x N with
    N = 2^n + 1: Unity Terrain's RAW import format and Heightmap.FromR16 in the sim. Row index
    runs along +Z with row 0 at z = 0 (the top of the slope); column index runs along +X.
    Sample 0 is MIN_HEIGHT_MM and sample 65535 is MAX_HEIGHT_MM, linearly.
TestSlopeSnow.raw
    The snow-type map in Story 002's format (SnowMap.FromBytes): one byte per heightmap sample,
    in the heightmap's order, each a SnowType value: 0 groomed, 1 powder, 2 crud, 3 ice, 4 slush.
TestSlope.txt
    The layout (SlopeLayout.Parse in the sim): the HeightmapSpec, the snow zones, the start
    point, the kickers, the trees and the rails and boxes, one plain-text record per line. Its
    header documents it.

The fall line runs toward +Z. The terrain is a level run-out at the bottom, a main slope with
a steeper pitch, a powder bowl carved into it and kickers shaped onto it. Every shape is built
from + - * / and clipping only (no sin, exp or pow), and trees are placed with an integer hash
from a fixed seed, so the output is byte-identical on every run and every IEEE 754 machine.
The one exception is a kicker whose facing is not a multiple of 90 degrees, which needs a sine
and cosine; every kicker here faces straight down the fall line.
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import numpy as np

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_OUT = REPO_ROOT / "Assets" / "Terrain" / "TestSlope"

HEIGHTMAP_FILE = "TestSlope.raw"
SNOW_FILE = "TestSlopeSnow.raw"
LAYOUT_FILE = "TestSlope.txt"
LAYOUT_FORMAT = 1

# --- Grid and height range --------------------------------------------------------------------
RESOLUTION = 1025  # 2^10 + 1 samples per side
SIZE_MM = 512_000  # 512 m square, so 0.5 m per sample: fine enough to shape a kicker
MIN_HEIGHT_MM = 0
MAX_HEIGHT_MM = 160_000  # about 2.4 mm per R16 step

# SnowType byte values, in order, from Packages/com.firstchair.sim/Runtime/SnowMap.cs.
SNOW_TYPES = ("groomed", "powder", "crud", "ice", "slush")

# --- Slope profile (metres; the fall line is +Z) ----------------------------------------------
RUNOUT_HEIGHT_M = 10.0  # height of the level run-out
BASE_GRADIENT = 0.30  # drop per metre on the main slope, about 16.7 degrees
PITCH_Z_M = (160.0, 260.0)  # a steeper pitch across the whole slope
PITCH_EXTRA_GRADIENT = 0.12  # added at the middle of the pitch: about 22.8 degrees there
ROLLOUT_Z_M = (400.0, 460.0)  # the gradient eases to zero over this span; level below it
BOWL_ZONE = "bowl"  # the bowl is carved into this zone's rectangle
BOWL_DEPTH_M = 18.0  # deepest point of the bowl below the main slope
BOWL_HEADWALL = 0.3  # fraction of the bowl's length taken by its steep headwall

# --- Snow zones (millimetres) -----------------------------------------------------------------
BACKGROUND_SNOW = "crud"  # tracked-out snow between the runs
ZONES = (
    # name, snow, x min, z min, x max, z max. Edges included; painted in order, a later zone wins.
    ("groomer", "groomed", 40_000, 0, 140_000, 460_000),
    ("park", "groomed", 170_000, 60_000, 230_000, 400_000),
    ("bowl", "powder", 260_000, 60_000, 440_000, 340_000),
    ("glade", "powder", 300_000, 340_000, 420_000, 448_000),
    ("runout", "groomed", 0, 460_000, 512_000, 512_000),
    ("ice", "ice", 60_000, 180_000, 120_000, 230_000),  # polished patch on the groomer's pitch
    ("slush", "slush", 50_000, 420_000, 130_000, 450_000),  # sun-softened foot of the groomer
    ("crud", "crud", 320_000, 290_000, 380_000, 335_000),  # tracked-out exit of the bowl
)
START_MM = (90_000, 10_000)  # the top of the groomer, on its centre line

# --- Kickers (millimetres; facing in millidegrees, 0 = +Z, 90000 = +X) --------------------------
KICKERS = (
    # lip x, lip z, facing, length, width, height. The ramp rises `height` above the slope over
    # `length`, ending at the lip.
    (200_000, 150_000, 0, 4_000, 5_000, 800),
    (200_000, 230_000, 0, 5_000, 6_000, 1_200),
    (200_000, 310_000, 0, 6_000, 7_000, 1_600),
)
# --- Rails and boxes on the kicker line (millimetres; Story 006) -------------------------------
RAILS = (
    # kind, x0, z0, x1, z1, height, width. A straight run seen from above whose top follows the snow `height` above
    # it. Each sits on the park's kicker line, behind one kicker's landing and before the next kicker's ramp: boxes
    # low enough to ride onto (the sim's GrindStepUpHeight is 0.4 m), the rail high enough to need a pop.
    ("box", 200_000, 185_000, 200_000, 200_000, 300, 600),
    ("rail", 200_000, 265_000, 200_000, 283_000, 500, 80),
    ("box", 200_000, 345_000, 200_000, 365_000, 300, 600),
)
LANDING_CLEARANCE_MM = 30_000  # a kicker's landing runs this far past its lip; rails and boxes stay out of it

KICKER_SHOULDER_M = 1.0  # the sides fall back to the slope over this distance
KICKER_BACK_PER_HEIGHT = 2.0  # behind the lip, the kicker falls to the slope over this many heights

# --- Trees: one per kept cell of a grid laid over the glade (millimetres) -----------------------
TREE_ZONE = "glade"
TREE_CELL_MM = 12_000
TREE_KEEP_PER_MILLE = 550  # share of cells that get a tree
TREE_JITTER_MM = 3_000  # largest offset from the cell centre, each axis
TREE_TRUNK_RADIUS_MM = (150, 350)
TREE_BRANCH_RADIUS_MM = (1_200, 2_500)
TREE_SEED = 3

MASK64 = (1 << 64) - 1


def zone_rect(name: str) -> tuple[int, int, int, int]:
    """The (x min, z min, x max, z max) rectangle of the named zone, in millimetres."""
    for zone_name, _snow, *rect in ZONES:
        if zone_name == name:
            return tuple(rect)
    raise KeyError(name)


def sample_coords_m() -> np.ndarray:
    """World coordinate of each row or column of samples, metres."""
    return np.arange(RESOLUTION, dtype=np.float64) * (SIZE_MM / 1000.0 / (RESOLUTION - 1))


def smoothstep(t):
    """3t^2 - 2t^3 with t clipped to [0, 1]: 0 to 1 with a level start and end."""
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def smoothstep_integral(t):
    """Integral of smoothstep from 0 to t, t clipped to [0, 1]: t^3 - t^4 / 2."""
    t = np.clip(t, 0.0, 1.0)
    t3 = t * t * t
    return t3 - 0.5 * t3 * t


def bump_integral(t):
    """Integral from 0 to t of the bump 16 t^2 (1 - t)^2, which peaks at 1 when t = 0.5."""
    t = np.clip(t, 0.0, 1.0)
    t3 = t * t * t
    return 16.0 * (t3 / 3.0 - 0.5 * t3 * t + t3 * t * t / 5.0)


def drop_m(z):
    """How far the main slope has fallen at z, metres: the base gradient easing to level over
    the roll-out, plus the pitch."""
    rollout_start, rollout_end = ROLLOUT_Z_M
    rollout_length = rollout_end - rollout_start
    t = (z - rollout_start) / rollout_length
    base = BASE_GRADIENT * (np.minimum(z, rollout_start) + rollout_length * (np.clip(t, 0.0, 1.0) - smoothstep_integral(t)))
    pitch_start, pitch_end = PITCH_Z_M
    pitch_length = pitch_end - pitch_start
    pitch = PITCH_EXTRA_GRADIENT * pitch_length * bump_integral((z - pitch_start) / pitch_length)
    return base + pitch


def bowl_depth_m(x, z):
    """Depth of the bowl below the main slope: a smooth U across its width, deepening fast down
    the headwall and easing out toward its foot so it always drains downhill."""
    x_min, z_min, x_max, z_max = (value / 1000.0 for value in zone_rect(BOWL_ZONE))
    v = 2.0 * (x - x_min) / (x_max - x_min) - 1.0
    across = np.where(np.abs(v) < 1.0, (1.0 - v * v) * (1.0 - v * v), 0.0)
    t = (z - z_min) / (z_max - z_min)
    down = np.where(t < BOWL_HEADWALL, smoothstep(t / BOWL_HEADWALL), 1.0 - smoothstep((t - BOWL_HEADWALL) / (1.0 - BOWL_HEADWALL)))
    down = np.where((t >= 0.0) & (t <= 1.0), down, 0.0)
    return BOWL_DEPTH_M * across * down


def kicker_height_m(x, z, kicker):
    """Height a kicker adds above the slope: a ramp rising as the square of the distance from
    its foot to `height` at the lip, a short fall-away behind the lip, and rounded shoulders."""
    lip_x, lip_z, facing, length, width, height = kicker
    theta = math.radians(facing / 1000.0)
    forward_x, forward_z = math.sin(theta), math.cos(theta)
    length_m, width_m, height_m = length / 1000.0, width / 1000.0, height / 1000.0
    dx = x - lip_x / 1000.0
    dz = z - lip_z / 1000.0
    along = dx * forward_x + dz * forward_z + length_m  # 0 at the foot, length_m at the lip
    off_centre = np.abs(dx * forward_z - dz * forward_x)
    ratio = along / length_m
    behind = height_m * (1.0 - smoothstep((along - length_m) / (KICKER_BACK_PER_HEIGHT * height_m)))
    profile = np.where(along <= 0.0, 0.0, np.where(along <= length_m, height_m * ratio * ratio, behind))
    across = 1.0 - smoothstep((off_centre - width_m / 2.0) / KICKER_SHOULDER_M)
    return profile * across


def build_heights_m() -> np.ndarray:
    """Terrain height at every sample, metres, indexed [row, column]."""
    coords = sample_coords_m()
    x = coords[np.newaxis, :]
    z = coords[:, np.newaxis]
    top = RUNOUT_HEIGHT_M + drop_m(coords[-1])
    heights = (top - drop_m(z)) + np.zeros_like(x)
    heights = heights - bowl_depth_m(x, z)
    for kicker in KICKERS:
        heights = heights + kicker_height_m(x, z, kicker)
    return heights


def encode_r16(heights_m: np.ndarray) -> bytes:
    """Rounds heights to the nearest R16 sample and packs them little-endian."""
    scaled = (heights_m * 1000.0 - MIN_HEIGHT_MM) * (65535.0 / (MAX_HEIGHT_MM - MIN_HEIGHT_MM))
    samples = np.rint(scaled)
    if samples.min() < 0.0 or samples.max() > 65535.0:
        raise ValueError("The terrain leaves the height range; widen MIN_HEIGHT_MM or MAX_HEIGHT_MM.")
    return samples.astype("<u2").tobytes()


def zone_mask(rect) -> np.ndarray:
    """True for every sample inside the rectangle, edges included, in exact integer arithmetic:
    sample i sits at i * size / (N - 1)."""
    x_min, z_min, x_max, z_max = rect
    scaled = np.arange(RESOLUTION, dtype=np.int64) * SIZE_MM  # position * (N - 1), millimetres
    cells = RESOLUTION - 1
    in_x = (scaled >= x_min * cells) & (scaled <= x_max * cells)
    in_z = (scaled >= z_min * cells) & (scaled <= z_max * cells)
    return in_z[:, np.newaxis] & in_x[np.newaxis, :]


def build_snow() -> bytes:
    """The snow map: the background, then each zone painted over it in order."""
    snow = np.full((RESOLUTION, RESOLUTION), SNOW_TYPES.index(BACKGROUND_SNOW), dtype=np.uint8)
    for _name, snow_name, *rect in ZONES:
        snow[zone_mask(rect)] = SNOW_TYPES.index(snow_name)
    return snow.tobytes()


def mix64(value: int) -> int:
    """SplitMix64's mixing function: a fixed integer hash, the same on every machine."""
    value = (value + 0x9E3779B97F4A7C15) & MASK64
    value = ((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9) & MASK64
    value = ((value ^ (value >> 27)) * 0x94D049BB133111EB) & MASK64
    return value ^ (value >> 31)


def cell_hash(column: int, row: int, salt: int) -> int:
    return mix64((TREE_SEED << 48) ^ (column << 32) ^ (row << 16) ^ salt)


def hashed_in_range(value: int, low: int, high: int) -> int:
    return low + value % (high - low + 1)


def place_trees() -> list[tuple[int, int, int, int]]:
    """Trees as (x, z, trunk radius, branch radius) in millimetres: at most one per grid cell,
    each canopy wholly inside its own cell, so every tree stands inside the glade and no two
    canopies overlap."""
    half_cell = TREE_CELL_MM // 2
    if TREE_JITTER_MM + TREE_BRANCH_RADIUS_MM[1] > half_cell:
        raise ValueError("A canopy could leave its cell; shrink TREE_JITTER_MM or the branch radius.")
    x_min, z_min, x_max, z_max = zone_rect(TREE_ZONE)
    trees = []
    for row in range((z_max - z_min) // TREE_CELL_MM):
        for column in range((x_max - x_min) // TREE_CELL_MM):
            if cell_hash(column, row, 0) % 1000 >= TREE_KEEP_PER_MILLE:
                continue
            x = x_min + column * TREE_CELL_MM + half_cell + hashed_in_range(cell_hash(column, row, 1), -TREE_JITTER_MM, TREE_JITTER_MM)
            z = z_min + row * TREE_CELL_MM + half_cell + hashed_in_range(cell_hash(column, row, 2), -TREE_JITTER_MM, TREE_JITTER_MM)
            trunk = hashed_in_range(cell_hash(column, row, 3), *TREE_TRUNK_RADIUS_MM)
            branch = hashed_in_range(cell_hash(column, row, 4), *TREE_BRANCH_RADIUS_MM)
            trees.append((x, z, trunk, branch))
    return trees


def layout_text(trees) -> str:
    """The layout file: plain text, LF line endings, one record per line."""
    lines = [
        "# First Chair test slope layout. Generated by tools/terrain/make_test_slope.py: do not edit by hand.",
        "# Read by SlopeLayout.Parse (Packages/com.firstchair.sim/Runtime/SlopeLayout.cs).",
        "# One record per line, fields separated by spaces, all numbers whole; a line starting '#' is a comment.",
        "# Lengths and positions are millimetres. x runs along heightmap columns and z along rows; row 0 is",
        "# z = 0, the top of the slope, and the fall line runs toward +Z.",
        "# Facing is millidegrees clockwise seen from above (Unity's yaw): 0 = +Z, 90000 = +X.",
        f"format {LAYOUT_FORMAT}",
        f"# spec <sizeX> <sizeZ> <minHeight> <maxHeight>: the HeightmapSpec of {HEIGHTMAP_FILE}",
        f"spec {SIZE_MM} {SIZE_MM} {MIN_HEIGHT_MM} {MAX_HEIGHT_MM}",
        f"# background <snow>: the snow outside every zone in {SNOW_FILE}",
        f"background {BACKGROUND_SNOW}",
        "# zone <name> <snow> <xMin> <zMin> <xMax> <zMax>: edges included; painted in order, a later zone wins",
    ]
    lines += [f"zone {name} {snow} {x_min} {z_min} {x_max} {z_max}" for name, snow, x_min, z_min, x_max, z_max in ZONES]
    lines += [
        "# start <x> <z>: where a run begins, at the top of the groomer",
        f"start {START_MM[0]} {START_MM[1]}",
        "# kicker <lipX> <lipZ> <facing> <length> <width> <height>: the ramp rises <height> above the slope",
        "# over <length> to the lip at (lipX, lipZ), and is <width> wide",
    ]
    lines += ["kicker " + " ".join(str(value) for value in kicker) for kicker in KICKERS]
    lines += ["# tree <x> <z> <trunkRadius> <branchRadius>"]
    lines += [f"tree {x} {z} {trunk} {branch}" for x, z, trunk, branch in trees]
    lines += [
        "# rail|box <x0> <z0> <x1> <z1> <height> <width>: a straight run from (x0, z0) to (x1, z1) whose top is <height>",
        "# above the snow and <width> wide",
    ]
    lines += [" ".join(str(value) for value in rail) for rail in RAILS]
    return "\n".join(lines) + "\n"


def write(out_dir: Path) -> dict[str, Path]:
    """Generates the slope and writes its three files into out_dir. Returns their paths by name."""
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    outputs = {
        HEIGHTMAP_FILE: encode_r16(build_heights_m()),
        SNOW_FILE: build_snow(),
        LAYOUT_FILE: layout_text(place_trees()).encode("ascii"),
    }
    paths = {}
    for name, data in outputs.items():
        path = out_dir / name
        path.write_bytes(data)
        paths[name] = path
    return paths


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Generate the First Chair test slope (Story 003).")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT, help="output directory (default: %(default)s)")
    args = parser.parse_args(argv)
    for name, path in write(args.out).items():
        print(f"{path}: {path.stat().st_size} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
