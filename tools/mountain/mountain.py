#!/usr/bin/env python3
"""First Chair's mountain tool (Story 015): a mountain is a TOML file; this builds, previews, checks and starts one.

Usage:
    python3 tools/mountain/mountain.py build   <file.mountain.toml> [--out DIR]
    python3 tools/mountain/mountain.py preview <file.mountain.toml> [--png FILE] [--pixels N]
    python3 tools/mountain/mountain.py check   <file.mountain.toml>
    python3 tools/mountain/mountain.py new     <Name> [--out FILE]

`build` writes three files into Assets/Terrain/<Name>/ (or DIR), in the formats the sim and Unity already load:

<Name>.raw
    The heightmap: headerless unsigned 16-bit little-endian samples, N x N, row 0 at z = 0 (the summit side), column
    index along +X.
<Name>Snow.raw
    The snow map: one SnowType byte per heightmap sample (Packages/com.firstchair.sim/Runtime/SnowMap.cs).
<Name>.txt
    The layout: the spec, the zones, the start, the park's kickers, rails and boxes, the runs, the lifts with their
    towers and the trees (Packages/com.firstchair.sim/Runtime/SlopeLayout.cs).

The format is documented field by field in tools/mountain/README.md. The shaping is story 010's test-mountain
generator with its constants read from the file: every shape is built from + - * /, square roots and clipping, and the
noise and the trees come from an integer hash of fixed seeds, so the same file gives byte-identical output on every
run and every IEEE 754 machine. Kickers face straight down the fall line, so no sine or cosine is needed.
"""

from __future__ import annotations

import argparse
import math
import sys
import tomllib
from dataclasses import dataclass
from pathlib import Path

import numpy as np

REPO_ROOT = Path(__file__).resolve().parents[2]
TERRAIN_ROOT = REPO_ROOT / "Assets" / "Terrain"
FORMAT = 1
LAYOUT_FORMAT = 1

# SnowType byte values, in order, from Packages/com.firstchair.sim/Runtime/SnowMap.cs.
SNOW_TYPES = ("groomed", "powder", "crud", "ice", "slush")
SNOW = {name: index for index, name in enumerate(SNOW_TYPES)}
DIFFICULTIES = ("green", "blue", "red", "black", "park")
RUN_SHAPES = ("open", "smooth", "cat", "gully")
FEATURE_KINDS = ("hill", "ridge", "valley", "flatten")
RAIL_KINDS = ("rail", "box")

MASK64 = (1 << 64) - 1


class MountainError(ValueError):
    """A mountain file that cannot be read or built, with a message naming the problem."""


# ----------------------------------------------------------------------------------------------
# The format
# ----------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class Grid:
    resolution: int
    size_m: float
    min_height_m: float
    max_height_m: float

    @property
    def size_mm(self) -> int:
        return int(round(self.size_m * 1000.0))

    @property
    def min_height_mm(self) -> int:
        return int(round(self.min_height_m * 1000.0))

    @property
    def max_height_mm(self) -> int:
        return int(round(self.max_height_m * 1000.0))

    @property
    def cell_m(self) -> float:
        return (self.size_mm / 1000.0) / (self.resolution - 1)


@dataclass(frozen=True)
class NoiseLayer:
    spacing_m: float
    amplitude_m: float
    seed: int


@dataclass(frozen=True)
class Feature:
    kind: str
    centre_m: tuple[float, float] | None
    radius_m: float
    height_m: float | None
    points_m: tuple[tuple[float, float], ...]
    width_m: float
    blend_m: float


@dataclass(frozen=True)
class Run:
    name: str
    difficulty: str
    width_m: float
    shape: str
    snow: str
    points_m: tuple[tuple[float, float], ...]


@dataclass(frozen=True)
class RunPatch:
    run: str
    from_m: float
    to_m: float
    snow: str


@dataclass(frozen=True)
class SnowArea:
    snow: str
    rect_m: tuple[float, float, float, float] | None
    circle_m: tuple[float, float, float] | None


@dataclass(frozen=True)
class Kicker:
    lip_z_m: float
    length_m: float
    width_m: float
    height_m: float


@dataclass(frozen=True)
class Rail:
    kind: str
    from_z_m: float
    to_z_m: float
    height_m: float
    width_m: float


@dataclass(frozen=True)
class Park:
    run: str
    kicker_lane_x_m: float
    jib_lane_x_m: float
    zone_m: tuple[float, float, float, float]
    kicker_shoulder_m: float
    kicker_back_per_height: float
    kickers: tuple[Kicker, ...]
    rails: tuple[Rail, ...]


@dataclass(frozen=True)
class Lift:
    name: str
    bottom_m: tuple[float, float]
    top_m: tuple[float, float]
    station_cable_m: float
    span_min_m: float
    span_max_m: float
    tower_min_m: float
    tower_easy_m: float
    tower_max_m: float
    station_approach_m: float
    min_clearance_m: float
    corridor_half_width_m: float


@dataclass(frozen=True)
class Glade:
    area_m: tuple[float, float, float, float]
    keep_per_mille: int


@dataclass(frozen=True)
class Trees:
    cell_mm: int
    seed: int
    keep_near_per_mille: int
    keep_far_per_mille: int
    near_m: float
    jitter_mm: int
    trunk_mm: tuple[int, int]
    branch_mm: tuple[int, int]
    run_clearance_m: float
    glades: tuple[Glade, ...]


@dataclass(frozen=True)
class Mountain:
    """A mountain file, read and checked: every field the builder uses, with the defaults filled in."""

    source: Path | None
    name: str
    title: str
    start_m: tuple[float, float]
    grid: Grid
    base_height_m: float
    centre_x_m: float
    gradient: tuple[tuple[float, float], ...]
    peak_drop_m: float
    peak_fade_z_m: tuple[float, float]
    ridge_rise_m: float
    ridge_start_off_centre_m: float
    ridge_width_m: float
    ridge_fade_z_m: tuple[float, float]
    noise_layers: tuple[NoiseLayer, ...]
    run_noise_kept: float
    features: tuple[Feature, ...]
    summit_centre_m: tuple[float, float]
    summit_radius_m: float
    summit_blend_m: float
    summit_noise_free_m: tuple[float, float]
    summit_tree_free_m: float
    base_area_m: tuple[float, float, float, float]
    base_blend_m: float
    tree_line_height_m: float
    tree_line_spread_m: float
    gully_depth_m: float
    gully_fade_m: tuple[float, float]
    shoulder_m: dict
    end_fade_m: float
    densify_step_m: float
    centre_smooth_m: float
    cat_cross_kept: float
    runs: tuple[Run, ...]
    snow_background: str
    snow_above_tree_line: str
    run_patches: tuple[RunPatch, ...]
    snow_areas: tuple[SnowArea, ...]
    park: Park | None
    lifts: tuple[Lift, ...]
    trees: Trees

    @property
    def heightmap_file(self) -> str:
        return f"{self.name}.raw"

    @property
    def snow_file(self) -> str:
        return f"{self.name}Snow.raw"

    @property
    def layout_file(self) -> str:
        return f"{self.name}.txt"


_REQUIRED = object()


class _Table:
    """One TOML table being read: each key taken once, with its type checked; leftover keys are errors (typos)."""

    def __init__(self, data, where: str):
        if not isinstance(data, dict):
            raise MountainError(f"{where}: expected a table")
        self._data = dict(data)
        self._where = where

    def has(self, key: str) -> bool:
        return key in self._data

    def raw(self, key: str, default=_REQUIRED):
        if key not in self._data:
            if default is _REQUIRED:
                raise MountainError(f"{self._where}: '{key}' is required")
            return default
        return self._data.pop(key)

    def number(self, key: str, default=_REQUIRED) -> float:
        if key not in self._data and default is not _REQUIRED:
            return default
        value = self.raw(key)
        if isinstance(value, bool) or not isinstance(value, (int, float)):
            raise MountainError(f"{self._where}.{key}: expected a number, got {value!r}")
        return float(value)

    def integer(self, key: str, default=_REQUIRED) -> int:
        if key not in self._data and default is not _REQUIRED:
            return default
        value = self.raw(key)
        if isinstance(value, bool) or not isinstance(value, int):
            raise MountainError(f"{self._where}.{key}: expected a whole number, got {value!r}")
        return value

    def text(self, key: str, default=_REQUIRED, choices=None) -> str:
        if key not in self._data and default is not _REQUIRED:
            return default
        value = self.raw(key)
        if not isinstance(value, str):
            raise MountainError(f"{self._where}.{key}: expected text, got {value!r}")
        if choices is not None and value not in choices:
            raise MountainError(f"{self._where}.{key}: '{value}' is not one of {', '.join(choices)}")
        return value

    def numbers(self, key: str, count: int, default=_REQUIRED) -> tuple:
        if key not in self._data and default is not _REQUIRED:
            return default
        return _numbers(self.raw(key), count, f"{self._where}.{key}")

    def integers(self, key: str, count: int, default=_REQUIRED) -> tuple:
        if key not in self._data and default is not _REQUIRED:
            return tuple(default)
        value = self.raw(key)
        if (not isinstance(value, (list, tuple)) or len(value) != count
                or any(isinstance(v, bool) or not isinstance(v, int) for v in value)):
            raise MountainError(f"{self._where}.{key}: expected {count} whole numbers, got {value!r}")
        return tuple(value)

    def points(self, key: str, default=_REQUIRED, minimum: int = 2) -> tuple:
        value = self.raw(key, default)
        if not isinstance(value, (list, tuple)) or len(value) < minimum:
            raise MountainError(f"{self._where}.{key}: expected at least {minimum} [x, z] points")
        return tuple(_numbers(point, 2, f"{self._where}.{key}[{i}]") for i, point in enumerate(value))

    def table(self, key: str, default=_REQUIRED):
        value = self.raw(key, default)
        return None if value is None else _Table(value, f"{self._where}.{key}" if self._where else key)

    def tables(self, key: str) -> list:
        value = self.raw(key, [])
        if not isinstance(value, list):
            raise MountainError(f"{self._where}.{key}: expected a list of tables")
        where = f"{self._where}.{key}" if self._where else key
        return [_Table(item, f"{where}[{i}]") for i, item in enumerate(value)]

    def done(self) -> None:
        if self._data:
            raise MountainError(f"{self._where or 'the file'}: unknown key(s) {', '.join(sorted(self._data))}")


def _numbers(value, count: int, where: str) -> tuple:
    if (not isinstance(value, (list, tuple)) or len(value) != count
            or any(isinstance(v, bool) or not isinstance(v, (int, float)) for v in value)):
        raise MountainError(f"{where}: expected {count} numbers, got {value!r}")
    return tuple(float(v) for v in value)


def load(path) -> Mountain:
    """Reads and checks a mountain file. Raises MountainError naming the first problem."""
    path = Path(path)
    try:
        with path.open("rb") as stream:
            data = tomllib.load(stream)
    except tomllib.TOMLDecodeError as error:
        raise MountainError(f"{path}: not valid TOML: {error}") from error
    return parse(data, path)


def parse(data: dict, source: Path | None = None) -> Mountain:
    """Builds a Mountain from a mountain file's parsed TOML (a dict). Raises MountainError naming the first problem."""
    top = _Table(data, "")
    version = top.integer("format")
    if version != FORMAT:
        raise MountainError(f"format {version} is not supported; this tool reads format {FORMAT}")
    name = top.text("name")
    if not name.isidentifier() or not name[0].isupper():
        raise MountainError(f"name '{name}' must be a CamelCase word, such as TestMountain (it names the files)")
    title = top.text("title", name)
    start_m = top.numbers("start_m", 2)

    t = top.table("grid")
    grid = Grid(t.integer("resolution"), t.number("size_m"), t.number("min_height_m", 0.0), t.number("max_height_m"))
    t.done()
    if grid.resolution < 33 or (grid.resolution - 1) & (grid.resolution - 2) != 0:
        raise MountainError(f"grid.resolution {grid.resolution} must be 2^n + 1 (33 to 4097: Unity's terrain sizes)")
    if grid.resolution > 4097:
        raise MountainError(f"grid.resolution {grid.resolution} is above Unity's largest terrain heightmap, 4097")
    if not grid.size_m > 0.0 or not grid.max_height_m > grid.min_height_m:
        raise MountainError("grid: size_m must be positive and max_height_m above min_height_m")

    t = top.table("profile")
    base_height_m = t.number("base_height_m")
    centre_x_m = t.number("centre_x_m", grid.size_m / 2.0)
    gradient = t.points("gradient", minimum=1)
    t.done()
    if any(b[0] <= a[0] for a, b in zip(gradient, gradient[1:])):
        raise MountainError("profile.gradient: the knots' z must rise from one knot to the next")

    t = top.table("peak", None)
    peak_drop_m, peak_fade_z_m = 0.0, (0.0, 1.0)
    if t is not None:
        peak_drop_m = t.number("drop_m")
        peak_fade_z_m = t.numbers("fade_z_m", 2)
        t.done()

    t = top.table("ridges", None)
    ridge = (0.0, 0.0, 1.0, (0.0, 1.0))
    if t is not None:
        ridge = (t.number("rise_m"), t.number("start_off_centre_m"), t.number("width_m"), t.numbers("fade_z_m", 2))
        t.done()

    t = top.table("noise", None)
    noise_layers, run_noise_kept = (), 0.0
    if t is not None:
        run_noise_kept = t.number("run_kept", 0.25)
        layers = []
        for layer in t.tables("layers"):
            layers.append(NoiseLayer(layer.number("spacing_m"), layer.number("amplitude_m"), layer.integer("seed")))
            layer.done()
        noise_layers = tuple(layers)
        t.done()

    features = []
    for f in top.tables("features"):
        kind = f.text("kind", choices=FEATURE_KINDS)
        if kind in ("hill", "flatten"):
            feature = Feature(kind, f.numbers("centre_m", 2), f.number("radius_m"),
                              f.number("height_m") if kind == "hill" else f.number("height_m", None),
                              (), 0.0, f.number("blend_m", 20.0) if kind == "flatten" else 0.0)
        else:
            height = f.number("height_m") if kind == "ridge" else -f.number("depth_m")
            feature = Feature(kind, None, 0.0, height, f.points("points_m"), f.number("width_m"), 0.0)
        f.done()
        features.append(feature)

    t = top.table("summit")
    summit = (t.numbers("centre_m", 2), t.number("radius_m"), t.number("blend_m"), t.numbers("noise_free_m", 2),
              t.number("tree_free_m", 25.0))
    t.done()

    t = top.table("base")
    base_area_m, base_blend_m = t.numbers("area_m", 4), t.number("blend_m")
    t.done()

    t = top.table("tree_line", None)
    tree_line = (grid.max_height_m * 2.0, 1.0)  # no tree line: trees to the top
    if t is not None:
        tree_line = (t.number("height_m"), t.number("spread_m"))
        t.done()

    t = top.table("run_shaping", {})
    shoulder_table = t.table("shoulder_m", {})
    shoulder = {shape: shoulder_table.number(shape, 22.0 if shape in ("open", "smooth") else 14.0 if shape == "cat" else 26.0)
                for shape in RUN_SHAPES}
    shoulder_table.done()
    shaping = (t.number("gully_depth_m", 7.0), t.numbers("gully_fade_m", 2, (40.0, 70.0)), t.number("end_fade_m", 10.0),
               t.number("densify_step_m", 2.0), t.number("centre_smooth_m", 18.0), t.number("cat_cross_kept", 0.15))
    t.done()

    runs = []
    for r in top.tables("runs"):
        run = Run(r.text("name"), r.text("difficulty", choices=DIFFICULTIES), r.number("width_m"),
                  r.text("shape", "open", choices=RUN_SHAPES), r.text("snow", "groomed", choices=SNOW_TYPES), r.points("points_m"))
        r.done()
        if not run.width_m > 0.0:
            raise MountainError(f"run '{run.name}': width_m must be positive")
        runs.append(run)
    names = [run.name for run in runs]
    for run_name in names:
        if names.count(run_name) > 1:
            raise MountainError(f"two runs are named '{run_name}'")
        if not run_name or any(c.isspace() for c in run_name):
            raise MountainError(f"run name '{run_name}' must be one word (the layout separates fields with spaces)")

    t = top.table("snow", {})
    snow_background = t.text("background", "crud", choices=SNOW_TYPES)
    snow_above = t.text("above_tree_line", "powder", choices=SNOW_TYPES)
    patches = []
    for p in t.tables("run_patches"):
        patch = RunPatch(p.text("run"), p.number("from_m"), p.number("to_m"), p.text("snow", choices=SNOW_TYPES))
        p.done()
        if patch.run not in names:
            raise MountainError(f"snow.run_patches: there is no run '{patch.run}'")
        patches.append(patch)
    areas = []
    for a in t.tables("areas"):
        area = SnowArea(a.text("snow", choices=SNOW_TYPES), a.numbers("rect_m", 4, None), a.numbers("circle_m", 3, None))
        a.done()
        if (area.rect_m is None) == (area.circle_m is None):
            raise MountainError("snow.areas: each area has rect_m or circle_m, not both")
        areas.append(area)
    t.done()

    park = None
    t = top.table("park", None)
    if t is not None:
        kickers = []
        for k in t.tables("kickers"):
            kickers.append(Kicker(k.number("lip_z_m"), k.number("length_m"), k.number("width_m"), k.number("height_m")))
            k.done()
        rails = []
        for r in t.tables("rails"):
            rails.append(Rail(r.text("kind", choices=RAIL_KINDS), r.number("from_z_m"), r.number("to_z_m"),
                              r.number("height_m"), r.number("width_m")))
            r.done()
        park = Park(t.text("run"), t.number("kicker_lane_x_m"), t.number("jib_lane_x_m"), t.numbers("zone_m", 4),
                    t.number("kicker_shoulder_m", 1.0), t.number("kicker_back_per_height", 2.0), tuple(kickers), tuple(rails))
        t.done()
        if park.run not in names:
            raise MountainError(f"park.run: there is no run '{park.run}'")

    lifts = []
    for l in top.tables("lifts"):
        lift = Lift(l.text("name"), l.numbers("bottom_m", 2), l.numbers("top_m", 2), l.number("station_cable_m", 5.0),
                    l.number("span_min_m", 40.0), l.number("span_max_m", 110.0), l.number("tower_min_m", 8.0),
                    l.number("tower_easy_m", 14.0), l.number("tower_max_m", 26.0), l.number("station_approach_m", 25.0),
                    l.number("min_clearance_m", 7.0), l.number("corridor_half_width_m", 12.0))
        l.done()
        lifts.append(lift)

    t = top.table("trees", {})
    glades = []
    for g in t.tables("glades"):
        glades.append(Glade(g.numbers("area_m", 4), g.integer("keep_per_mille")))
        g.done()
    trees = Trees(t.integer("cell_mm", 8000), t.integer("seed", 21), t.integer("keep_near_per_mille", 850),
                  t.integer("keep_far_per_mille", 620), t.number("near_m", 40.0), t.integer("jitter_mm", 2500),
                  t.integers("trunk_mm", 2, [180, 380]), t.integers("branch_mm", 2, [1400, 1450]),
                  t.number("run_clearance_m", 2.0), tuple(glades))
    t.done()
    top.done()

    mountain = Mountain(
        source=source, name=name, title=title, start_m=start_m, grid=grid,
        base_height_m=base_height_m, centre_x_m=centre_x_m, gradient=gradient,
        peak_drop_m=peak_drop_m, peak_fade_z_m=peak_fade_z_m,
        ridge_rise_m=ridge[0], ridge_start_off_centre_m=ridge[1], ridge_width_m=ridge[2], ridge_fade_z_m=ridge[3],
        noise_layers=noise_layers, run_noise_kept=run_noise_kept, features=tuple(features),
        summit_centre_m=summit[0], summit_radius_m=summit[1], summit_blend_m=summit[2], summit_noise_free_m=summit[3],
        summit_tree_free_m=summit[4], base_area_m=base_area_m, base_blend_m=base_blend_m,
        tree_line_height_m=tree_line[0], tree_line_spread_m=tree_line[1],
        gully_depth_m=shaping[0], gully_fade_m=shaping[1], shoulder_m=shoulder, end_fade_m=shaping[2],
        densify_step_m=shaping[3], centre_smooth_m=shaping[4], cat_cross_kept=shaping[5], runs=tuple(runs),
        snow_background=snow_background, snow_above_tree_line=snow_above, run_patches=tuple(patches),
        snow_areas=tuple(areas), park=park, lifts=tuple(lifts), trees=trees)
    _check_on_map(mountain)
    return mountain


def _check_on_map(m: Mountain) -> None:
    size = m.grid.size_m

    def on_map(point, what):
        if not (0.0 <= point[0] <= size and 0.0 <= point[1] <= size):
            raise MountainError(f"{what} {list(point)} is off the {size:g} m map")

    on_map(m.start_m, "start_m")
    on_map(m.summit_centre_m, "summit.centre_m")
    for run in m.runs:
        for point in run.points_m:
            on_map(point, f"run '{run.name}' point")
    for lift in m.lifts:
        on_map(lift.bottom_m, f"lift '{lift.name}' bottom_m")
        on_map(lift.top_m, f"lift '{lift.name}' top_m")
    for feature in m.features:
        for point in ((feature.centre_m,) if feature.centre_m else feature.points_m):
            on_map(point, f"{feature.kind} feature")
    if m.trees.jitter_mm + m.trees.branch_mm[1] > m.trees.cell_mm // 2:
        raise MountainError("trees: a canopy could leave its cell; shrink jitter_mm or branch_mm, or grow cell_mm")


# ----------------------------------------------------------------------------------------------
# Helpers
# ----------------------------------------------------------------------------------------------

def sample_coords_m(m: Mountain) -> np.ndarray:
    """World coordinate of each row or column of samples, metres."""
    return np.arange(m.grid.resolution, dtype=np.float64) * m.grid.cell_m


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


def value_noise(m: Mountain, spacing_m: float, seed: int) -> np.ndarray:
    """Smoothstep-interpolated value noise over the grid, [-1, 1]."""
    coords = sample_coords_m(m) / spacing_m
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


def noise_m(m: Mountain) -> np.ndarray:
    resolution = m.grid.resolution
    total = np.zeros((resolution, resolution))
    for layer in m.noise_layers:
        total = total + layer.amplitude_m * value_noise(m, layer.spacing_m, layer.seed)
    return total


def gradient_at(m: Mountain, z):
    """The fall-line gradient at z: the knots joined by smoothsteps."""
    z = np.asarray(z, dtype=np.float64)
    g = np.full_like(z, m.gradient[0][1])
    for (z0, g0), (z1, g1) in zip(m.gradient, m.gradient[1:]):
        g = np.where(z >= z0, g0 + (g1 - g0) * smoothstep((z - z0) / (z1 - z0)), g)
    return g


def drop_profile_m(m: Mountain) -> np.ndarray:
    """How far the fall line has dropped at each row, metres (trapezoid sum of the gradient)."""
    z = sample_coords_m(m)
    g = gradient_at(m, z)
    steps = (g[1:] + g[:-1]) * (0.5 * m.grid.cell_m)
    return np.concatenate(([0.0], np.cumsum(steps)))


def base_heights_m(m: Mountain) -> np.ndarray:
    """The mountain before its runs: the fall-line profile, the peak and the side ridges."""
    coords = sample_coords_m(m)
    x = coords[np.newaxis, :]
    z = coords[:, np.newaxis]
    drop = drop_profile_m(m)
    heights = (m.base_height_m + drop[-1] - drop)[:, np.newaxis] + np.zeros_like(x)
    off = (x - m.centre_x_m) / ((m.grid.size_mm / 1000.0) / 2.0)
    f0, f1 = m.peak_fade_z_m
    peak = m.peak_drop_m * off * off * (1.0 - smoothstep((z - f0) / (f1 - f0)))
    ridge = m.ridge_rise_m * smoothstep((np.abs(x - m.centre_x_m) - m.ridge_start_off_centre_m) / m.ridge_width_m)
    r0, r1 = m.ridge_fade_z_m
    ridge = ridge * smoothstep((z - r0) / (r1 - r0))
    return heights - peak + ridge


def bilinear(m: Mountain, heights: np.ndarray, x, z):
    """Height at (x, z) metres, interpolated between samples."""
    resolution, cell = m.grid.resolution, m.grid.cell_m
    fx = np.clip(np.asarray(x, dtype=np.float64) / cell, 0.0, resolution - 1.000001)
    fz = np.clip(np.asarray(z, dtype=np.float64) / cell, 0.0, resolution - 1.000001)
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
# Lines: runs and the line features
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


def line_field(m: Mountain, line: np.ndarray, reach_m: float):
    """For every sample within reach_m of the line: the distance to it, the signed offset to its right, and the
    segment it is nearest with the position along that segment (0 to 1). Elsewhere the distance is infinite. Works
    segment by segment."""
    resolution, cell = m.grid.resolution, m.grid.cell_m
    distance = np.full((resolution, resolution), np.inf)
    offset = np.zeros((resolution, resolution))
    segment = np.zeros((resolution, resolution), dtype=np.int64)
    position = np.zeros((resolution, resolution))
    coords = sample_coords_m(m)
    for i in range(len(line) - 1):
        a, b = line[i], line[i + 1]
        lo = np.minimum(a, b) - reach_m
        hi = np.maximum(a, b) + reach_m
        c0, c1 = max(0, int(lo[0] / cell)), min(resolution - 1, int(hi[0] / cell) + 1)
        r0, r1 = max(0, int(lo[1] / cell)), min(resolution - 1, int(hi[1] / cell) + 1)
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


def shape_run(m: Mountain, heights: np.ndarray, noise: np.ndarray, run: Run, claimed: np.ndarray) -> dict:
    """Shapes one run into the heights in place, except where earlier runs have claimed the surface (1 on their cores,
    easing to 0 across their shoulders), and adds its own claim. Returns its sampled centre line and fields for the snow
    and trees."""
    line = catmull_rom(run.points_m, m.densify_step_m)
    s = along_m(line)
    total = s[-1]
    half = run.width_m / 2.0
    shoulder = m.shoulder_m[run.shape]
    # The mountain along the centre line now, and its cross slope, both smoothed along the line.
    tangent = np.gradient(line, axis=0)
    tangent = tangent / np.sqrt((tangent ** 2).sum(axis=1))[:, np.newaxis]
    right = np.stack([tangent[:, 1], -tangent[:, 0]], axis=1)
    probe = half * 0.6
    centre_h = box_smooth(bilinear(m, heights, line[:, 0], line[:, 1]), s, m.centre_smooth_m)
    cross = (bilinear(m, heights, line[:, 0] + right[:, 0] * probe, line[:, 1] + right[:, 1] * probe)
             - bilinear(m, heights, line[:, 0] - right[:, 0] * probe, line[:, 1] - right[:, 1] * probe)) / (2.0 * probe)
    cross = box_smooth(cross, s, m.centre_smooth_m)
    if run.shape == "cat":
        cross = cross * m.cat_cross_kept
    distance, offset, segment, position = line_field(m, line, half + shoulder)
    near = np.isfinite(distance)
    following = np.minimum(segment + 1, len(line) - 1)
    # Interpolated along the segment, so the surface runs smoothly from one centre-line point to the next.
    centre = centre_h[segment] + (centre_h[following] - centre_h[segment]) * position
    slope_across = cross[segment] + (cross[following] - cross[segment]) * position
    s_at = s[segment] + (s[following] - s[segment]) * position
    target = centre + slope_across * offset
    if run.shape in ("open", "gully"):
        target = target + m.run_noise_kept * noise
    if run.shape == "gully":
        u = np.clip(offset / (half + shoulder * 0.5), -1.0, 1.0)
        envelope = smoothstep(s_at / m.gully_fade_m[0]) * smoothstep((total - s_at) / m.gully_fade_m[1])
        target = target - m.gully_depth_m * envelope * (1.0 - u * u)
    weight = np.where(near, 1.0 - smoothstep((distance - half) / shoulder), 0.0)
    # Beyond its ends the run's level would carry on as a flat disc, and its cross slope as a tilted plane (as they did
    # into the summit behind the runs' starts, cutting a trough where riders start), so it fades out there.
    coords = sample_coords_m(m)
    x = coords[np.newaxis, :]
    z = coords[:, np.newaxis]
    before = -((x - line[0, 0]) * tangent[0, 0] + (z - line[0, 1]) * tangent[0, 1])
    after = (x - line[-1, 0]) * tangent[-1, 0] + (z - line[-1, 1]) * tangent[-1, 1]
    beyond = np.where((segment == 0) & (position <= 0.0), before,
                      np.where((segment == len(line) - 2) & (position >= 1.0), after, 0.0))
    weight = weight * (1.0 - smoothstep(beyond / m.end_fade_m))
    heights[:] = heights + (np.where(near, target, heights) - heights) * weight * (1.0 - claimed)
    claimed[:] = np.maximum(claimed, weight)
    return {"name": run.name, "line": line, "s": s, "distance": distance, "s_at": s_at, "half": half}


# ----------------------------------------------------------------------------------------------
# Features, the summit and the base
# ----------------------------------------------------------------------------------------------

def apply_features(m: Mountain, heights: np.ndarray) -> np.ndarray:
    """Sculpts the features into the heights, in the file's order: hills and hollows, ridges and valleys along lines,
    and level pads. Each eases in and out with a smoothstep, so nothing leaves a crease."""
    coords = sample_coords_m(m)
    x = coords[np.newaxis, :]
    z = coords[:, np.newaxis]
    for feature in m.features:
        if feature.kind == "hill":
            cx, cz = feature.centre_m
            distance = np.sqrt((x - cx) ** 2 + (z - cz) ** 2)
            heights = heights + feature.height_m * (1.0 - smoothstep(distance / feature.radius_m))
        elif feature.kind == "flatten":
            cx, cz = feature.centre_m
            level = feature.height_m if feature.height_m is not None else float(bilinear(m, heights, cx, cz))
            distance = np.sqrt((x - cx) ** 2 + (z - cz) ** 2)
            weight = 1.0 - smoothstep((distance - feature.radius_m) / feature.blend_m)
            heights = heights + (level - heights) * weight
        else:  # a ridge or, with a negative height, a valley along a line
            line = catmull_rom(feature.points_m, m.densify_step_m)
            half = feature.width_m / 2.0
            distance, _offset, _segment, _position = line_field(m, line, half)
            profile = np.where(np.isfinite(distance), 1.0 - smoothstep(distance / half), 0.0)
            heights = heights + feature.height_m * profile
    return heights


def shape_summit(m: Mountain, heights: np.ndarray) -> np.ndarray:
    """The top station's area: the fall line's own gentle top without the noise or the peak, so it is level across X
    and rounds over into the face as the mountain does. Returns how much of each sample it shaped (1 on the level
    area, easing to 0 across the blend), the summit's claim: the runs leave its surface alone, as a later run leaves
    an earlier one's."""
    coords = sample_coords_m(m)
    x = coords[np.newaxis, :]
    z = coords[:, np.newaxis]
    cx, cz = m.summit_centre_m
    drop = drop_profile_m(m)
    target = (m.base_height_m + drop[-1] - drop)[:, np.newaxis] + np.zeros_like(x)
    distance = np.sqrt((x - cx) ** 2 + (z - cz) ** 2)
    weight = 1.0 - smoothstep((distance - m.summit_radius_m) / m.summit_blend_m)
    heights[:] = heights + (target - heights) * weight
    return weight


def shape_base(m: Mountain, heights: np.ndarray) -> None:
    """The base area: the fall line's profile without the noise, level across X, blended in at its edges."""
    coords = sample_coords_m(m)
    x = coords[np.newaxis, :]
    z = coords[:, np.newaxis]
    x0, z0, x1, z1 = m.base_area_m
    drop = drop_profile_m(m)
    profile = (m.base_height_m + drop[-1] - drop)[:, np.newaxis] + np.zeros_like(x)
    inside_x = smoothstep((x - x0) / m.base_blend_m) * smoothstep((x1 - x) / m.base_blend_m)
    inside_z = smoothstep((z - z0) / m.base_blend_m)
    weight = inside_x * inside_z
    heights[:] = heights + (profile - heights) * weight


def base_zone_m(m: Mountain) -> tuple[float, float, float, float]:
    """The base zone the layout writes (x0, z0, x1, z1): the base area inside the middle of its blend on its three open
    sides, to the map's foot."""
    x0, z0, x1, z1 = m.base_area_m
    half = m.base_blend_m / 2.0
    return x0 + half, z0 + half, x1 - half, z1


def kicker_height_m(park: Park, x, z, kicker: Kicker) -> np.ndarray:
    """Height a kicker adds above the slope (as on the test slope): a ramp rising as the square of the distance from its
    foot to its height at the lip, a short fall-away behind the lip, and rounded shoulders. Faces +Z."""
    length, height = kicker.length_m, kicker.height_m
    along = z - kicker.lip_z_m + length  # 0 at the foot, length at the lip
    off_centre = np.abs(x - park.kicker_lane_x_m)
    ratio = along / length
    behind = height * (1.0 - smoothstep((along - length) / (park.kicker_back_per_height * height)))
    profile = np.where(along <= 0.0, 0.0, np.where(along <= length, height * ratio * ratio, behind))
    across = 1.0 - smoothstep((off_centre - kicker.width_m / 2.0) / park.kicker_shoulder_m)
    return profile * across


def build(m: Mountain):
    """The heights (metres), the snow map and the runs' fields, all at once."""
    coords = sample_coords_m(m)
    x = coords[np.newaxis, :]
    z = coords[:, np.newaxis]
    # No undulation on the summit, so the runs leave the top station downhill every way.
    cx, cz = m.summit_centre_m
    n0, n1 = m.summit_noise_free_m
    noise = noise_m(m) * smoothstep((np.sqrt((x - cx) ** 2 + (z - cz) ** 2) - n0) / (n1 - n0))
    heights = base_heights_m(m) + noise
    if m.features:
        heights = apply_features(m, heights)
    # The summit first, so the runs leaving it take their heights from it (and leave its surface, where riders start,
    # as it is); the base last, so every run ends on it.
    claimed = shape_summit(m, heights)
    runs = []
    for run in m.runs:
        runs.append(shape_run(m, heights, noise, run, claimed))
    shape_base(m, heights)
    if m.park is not None:
        for kicker in m.park.kickers:
            heights = heights + kicker_height_m(m.park, x, z, kicker)
    snow = paint_snow(m, heights, runs)
    return heights, snow, runs


# ----------------------------------------------------------------------------------------------
# Snow
# ----------------------------------------------------------------------------------------------

def paint_snow(m: Mountain, heights: np.ndarray, runs) -> np.ndarray:
    """The snow map, painted in this order (a later layer wins): the background below the tree line and the snow above
    it; ungroomed runs; the run patches; groomed runs (so a groomer keeps its snow where another run meets it); the
    summit and base areas, groomed; the snow areas."""
    resolution = m.grid.resolution
    coords = sample_coords_m(m)
    x = coords[np.newaxis, :] + np.zeros((resolution, 1))
    z = coords[:, np.newaxis] + np.zeros((1, resolution))
    snow = np.full((resolution, resolution), SNOW[m.snow_background], dtype=np.uint8)
    snow[heights > m.tree_line_height_m - m.tree_line_spread_m] = SNOW[m.snow_above_tree_line]
    by_name = {field["name"]: field for field in runs}
    for run in m.runs:
        if run.snow != "groomed":
            field = by_name[run.name]
            snow[field["distance"] <= field["half"]] = SNOW[run.snow]
    for patch in m.run_patches:
        field = by_name[patch.run]
        s_at = field["s_at"]
        snow[(field["distance"] <= field["half"]) & (s_at >= patch.from_m) & (s_at <= patch.to_m)] = SNOW[patch.snow]
    for run in m.runs:
        if run.snow == "groomed":
            field = by_name[run.name]
            snow[field["distance"] <= field["half"]] = SNOW["groomed"]
    cx, cz = m.summit_centre_m
    snow[(x - cx) ** 2 + (z - cz) ** 2 <= m.summit_radius_m * m.summit_radius_m] = SNOW["groomed"]
    x0, z0, x1, z1 = m.base_area_m
    base = (x >= x0 + m.base_blend_m / 2.0) & (x <= x1 - m.base_blend_m / 2.0) & (z >= z0 + m.base_blend_m / 2.0)
    snow[base] = SNOW["groomed"]
    for area in m.snow_areas:
        if area.rect_m is not None:
            ax0, az0, ax1, az1 = area.rect_m
            snow[(x >= ax0) & (x <= ax1) & (z >= az0) & (z <= az1)] = SNOW[area.snow]
        else:
            acx, acz, radius = area.circle_m
            snow[(x - acx) ** 2 + (z - acz) ** 2 <= radius * radius] = SNOW[area.snow]
    return snow


# ----------------------------------------------------------------------------------------------
# Lifts
# ----------------------------------------------------------------------------------------------

def lift_profile(m: Mountain, heights: np.ndarray, lift: Lift):
    """The snow under the lift line every metre: (fractions along it, ground heights, metres along it, length)."""
    bx, bz = lift.bottom_m
    tx, tz = lift.top_m
    length = float(np.sqrt((tx - bx) ** 2 + (tz - bz) ** 2))
    samples = int(length / 1.0)
    t_all = np.arange(samples + 1) / samples
    ground = bilinear(m, heights, bx + (tx - bx) * t_all, bz + (tz - bz) * t_all)
    return t_all, ground, t_all * length, length


def lift_towers(m: Mountain, heights: np.ndarray, lift: Lift):
    """Towers bottom to top as (x, z, height) metres. Walking up from the bottom station, each span reaches as far as
    it can (up to span_max_m) with a tower no taller than tower_easy_m, or else as far as the shortest tower allows;
    every span's straight cable clears the snow by min_clearance_m, except within station_approach_m of a station,
    where the cable comes down for riders to get on and off."""
    bx, bz = lift.bottom_m
    tx, tz = lift.top_m
    t_all, ground, metres, length = lift_profile(m, heights, lift)
    samples = len(t_all) - 1

    def needed(i0: int, cable0: float, i1: int, floor: float = lift.tower_min_m) -> float:
        """The height above the snow at sample i1 for a cable from cable0 at i0 to clear every sample between, and
        never below floor."""
        need = floor
        for i in range(i0 + 1, i1):
            # Half a metre stricter than the approach, so rounding never lets a sample at its edge go unchecked.
            if min(metres[i], length - metres[i]) < lift.station_approach_m - 0.5:
                continue
            f = (metres[i] - metres[i0]) / (metres[i1] - metres[i0])
            # cable0 + (ground[i1] + h - cable0) * f >= ground[i] + clearance
            h = (ground[i] + lift.min_clearance_m - cable0) / f + cable0 - ground[i1]
            need = max(need, h)
        return need

    towers = []
    i0, cable0 = 0, float(ground[0]) + lift.station_cable_m
    while True:
        top_index = samples
        if metres[top_index] - metres[i0] <= lift.span_max_m and needed(i0, cable0, top_index, 0.0) <= lift.station_cable_m:
            return towers
        best = None
        for i1 in range(i0 + int(lift.span_min_m), min(top_index - int(lift.span_min_m / 2), i0 + int(lift.span_max_m)) + 1):
            h = needed(i0, cable0, i1)
            if h <= lift.tower_easy_m:
                best = (i1, h)  # the farthest easy tower so far
            elif best is None or (best[1] > lift.tower_easy_m and h < best[1]):
                best = (i1, h) if h <= lift.tower_max_m else best
        if best is None:
            raise MountainError(f"lift '{lift.name}': the cable cannot clear the snow; raise tower_max_m or shorten the spans")
        i1, h = best
        h = math.ceil(h * 10.0) / 10.0  # whole decimetres, rounded up so the cable only rises
        towers.append((bx + (tx - bx) * t_all[i1], bz + (tz - bz) * t_all[i1], h))
        i0, cable0 = i1, float(ground[i1]) + h


def lift_least_clearance_m(m: Mountain, heights: np.ndarray, lift: Lift, towers) -> float:
    """The cable's least height above the snow between the stations' approaches, metres."""
    t_all, ground, metres, length = lift_profile(m, heights, lift)
    bx, bz = lift.bottom_m
    tx, tz = lift.top_m
    # Cable anchors along the line: the bottom station, the towers, the top station.
    anchors = [(0.0, float(ground[0]) + lift.station_cable_m)]
    for x, z, h in towers:
        along = float(np.sqrt((x - bx) ** 2 + (z - bz) ** 2))
        anchors.append((along, float(bilinear(m, heights, x, z)) + h))
    anchors.append((length, float(ground[-1]) + lift.station_cable_m))
    cable = np.interp(metres, [a for a, _ in anchors], [c for _, c in anchors])
    inside = np.minimum(metres, length - metres) >= lift.station_approach_m
    return float((cable - ground)[inside].min()) if inside.any() else float("inf")


def in_lift_corridor(lift: Lift, x: float, z: float, reach: float) -> bool:
    """Whether a tree's canopy, reach metres around (x, z), comes into the lift line's corridor."""
    bx, bz = lift.bottom_m
    tx, tz = lift.top_m
    limit = lift.corridor_half_width_m + reach
    if bx == tx:  # straight down the fall line: the distance across is exact
        return abs(x - bx) < limit and min(bz, tz) - limit <= z <= max(bz, tz) + limit
    dx, dz = tx - bx, tz - bz
    t = min(1.0, max(0.0, ((x - bx) * dx + (z - bz) * dz) / (dx * dx + dz * dz)))
    return math.hypot(x - (bx + t * dx), z - (bz + t * dz)) < limit


# ----------------------------------------------------------------------------------------------
# Trees
# ----------------------------------------------------------------------------------------------

def cell_hash(seed: int, column: int, row: int, salt: int) -> int:
    return mix64((seed << 48) ^ (column << 32) ^ (row << 16) ^ salt)


def hashed_in_range(value: int, low: int, high: int) -> int:
    return low + value % (high - low + 1)


def run_clearance_m(m: Mountain, runs) -> np.ndarray:
    """At each sample, how far it is outside the nearest run's edge, metres (negative inside a run)."""
    resolution = m.grid.resolution
    clearance = np.full((resolution, resolution), np.inf)
    for field in runs:
        clearance = np.minimum(clearance, field["distance"] - field["half"])
    return clearance


def place_trees(m: Mountain, heights: np.ndarray, runs):
    """Trees as (x, z, trunk radius, branch radius) millimetres: at most one per cell, each canopy inside its cell,
    none on a run (with clearance), a lift line, the summit or base areas, or above the tree line."""
    trees_spec = m.trees
    clearance = run_clearance_m(m, runs)
    cell_m = m.grid.cell_m
    cells = m.grid.size_mm // trees_spec.cell_mm
    half_cell = trees_spec.cell_mm // 2
    seed, jitter = trees_spec.seed, trees_spec.jitter_mm
    trees = []
    for row in range(cells):
        for column in range(cells):
            x_mm = column * trees_spec.cell_mm + half_cell + hashed_in_range(cell_hash(seed, column, row, 1), -jitter, jitter)
            z_mm = row * trees_spec.cell_mm + half_cell + hashed_in_range(cell_hash(seed, column, row, 2), -jitter, jitter)
            branch = hashed_in_range(cell_hash(seed, column, row, 4), *trees_spec.branch_mm)
            x, z = x_mm / 1000.0, z_mm / 1000.0
            reach = branch / 1000.0
            if any(in_lift_corridor(lift, x, z, reach) for lift in m.lifts):
                continue
            ix, iz = int(round(x / cell_m)), int(round(z / cell_m))
            if clearance[iz, ix] < reach + trees_spec.run_clearance_m:
                continue
            cx, cz = m.summit_centre_m
            if (x - cx) ** 2 + (z - cz) ** 2 < (m.summit_radius_m + m.summit_tree_free_m) ** 2:
                continue
            x0, z0, x1, z1 = m.base_area_m
            if x0 - reach <= x <= x1 + reach and z >= z0 - reach:
                continue
            height = heights[iz, ix]
            if height > m.tree_line_height_m:
                continue
            keep = trees_spec.keep_near_per_mille if clearance[iz, ix] < trees_spec.near_m else trees_spec.keep_far_per_mille
            for glade in trees_spec.glades:
                gx0, gz0, gx1, gz1 = glade.area_m
                if gx0 <= x <= gx1 and gz0 <= z <= gz1:
                    keep = glade.keep_per_mille
            # Thinning out toward the tree line.
            if height > m.tree_line_height_m - m.tree_line_spread_m:
                keep = keep * (m.tree_line_height_m - height) / m.tree_line_spread_m
            if cell_hash(seed, column, row, 0) % 1000 >= keep:
                continue
            trunk = hashed_in_range(cell_hash(seed, column, row, 3), *trees_spec.trunk_mm)
            trees.append((x_mm, z_mm, trunk, branch))
    return trees


# ----------------------------------------------------------------------------------------------
# Output
# ----------------------------------------------------------------------------------------------

def encode_r16(m: Mountain, heights_m: np.ndarray) -> bytes:
    """Rounds heights to the nearest R16 sample and packs them little-endian."""
    min_mm, max_mm = m.grid.min_height_mm, m.grid.max_height_mm
    scaled = (heights_m * 1000.0 - min_mm) * (65535.0 / (max_mm - min_mm))
    samples = np.rint(scaled)
    if samples.min() < 0.0 or samples.max() > 65535.0:
        low, high = float(heights_m.min()), float(heights_m.max())
        raise MountainError(f"the terrain runs from {low:.1f} m to {high:.1f} m, outside grid's "
                            f"{m.grid.min_height_m:g} to {m.grid.max_height_m:g} m; widen the height range")
    return samples.astype("<u2").tobytes()


def mm(value_m: float) -> int:
    return int(round(value_m * 1000.0))


def source_label(m: Mountain) -> str:
    if m.source is None:
        return "a mountain file"
    try:
        return Path(m.source).resolve().relative_to(REPO_ROOT).as_posix()
    except ValueError:
        return Path(m.source).name


def layout_text(m: Mountain, trees, towers_by_lift) -> str:
    size_mm = m.grid.size_mm
    lines = [
        f"# First Chair mountain layout: {m.title}. Generated by tools/mountain/mountain.py from {source_label(m)}: do not edit by hand.",
        "# Read by SlopeLayout.Parse (Packages/com.firstchair.sim/Runtime/SlopeLayout.cs).",
        "# One record per line, fields separated by spaces, all numbers whole; a line starting '#' is a comment.",
        "# Lengths and positions are millimetres. x runs along heightmap columns and z along rows; row 0 is",
        "# z = 0, the summit side, and the fall line runs toward +Z.",
        "# Facing is millidegrees clockwise seen from above (Unity's yaw): 0 = +Z, 90000 = +X.",
        "# The snow map is painted from the runs and areas, not only from the zones: the zones name areas for the view.",
        f"format {LAYOUT_FORMAT}",
        f"# spec <sizeX> <sizeZ> <minHeight> <maxHeight>: the HeightmapSpec of {m.heightmap_file}",
        f"spec {size_mm} {size_mm} {m.grid.min_height_mm} {m.grid.max_height_mm}",
        f"# background <snow>: the snow below the tree line off the runs in {m.snow_file}",
        f"background {m.snow_background}",
        "# zone <name> <snow> <xMin> <zMin> <xMax> <zMax>",
    ]
    cx, cz = m.summit_centre_m
    r = m.summit_radius_m
    lines.append(f"zone summit groomed {mm(cx - r)} {mm(cz - r)} {mm(cx + r)} {mm(cz + r)}")
    bx0, bz0, bx1, bz1 = base_zone_m(m)
    lines.append(f"zone base groomed {mm(bx0)} {mm(bz0)} {mm(bx1)} {mm(bz1)}")
    if m.park is not None:
        px0, pz0, px1, pz1 = m.park.zone_m
        lines.append(f"zone park groomed {mm(px0)} {mm(pz0)} {mm(px1)} {mm(pz1)}")
    lines += [
        "# start <x> <z>: where a run begins, at the top station",
        f"start {mm(m.start_m[0])} {mm(m.start_m[1])}",
        "# kicker <lipX> <lipZ> <facing> <length> <width> <height>",
    ]
    if m.park is not None:
        for kicker in m.park.kickers:
            lines.append(f"kicker {mm(m.park.kicker_lane_x_m)} {mm(kicker.lip_z_m)} 0 {mm(kicker.length_m)} {mm(kicker.width_m)} {mm(kicker.height_m)}")
    lines.append("# rail|box <x0> <z0> <x1> <z1> <height> <width>")
    if m.park is not None:
        lane = m.park.jib_lane_x_m
        for rail in m.park.rails:
            lines.append(f"{rail.kind} {mm(lane)} {mm(rail.from_z_m)} {mm(lane)} {mm(rail.to_z_m)} {mm(rail.height_m)} {mm(rail.width_m)}")
    lines.append("# run <name> <difficulty> <width> <x0> <z0> <x1> <z1> ...: the centre line from top to foot")
    for run in m.runs:
        lines.append(f"run {run.name} {run.difficulty} {mm(run.width_m)} " + " ".join(f"{mm(px)} {mm(pz)}" for px, pz in run.points_m))
    lines.append("# lift <name> <bottomX> <bottomZ> <topX> <topZ>; tower <lift> <x> <z> <height>, bottom to top")
    for lift in m.lifts:
        lines.append(f"lift {lift.name} {mm(lift.bottom_m[0])} {mm(lift.bottom_m[1])} {mm(lift.top_m[0])} {mm(lift.top_m[1])}")
        for tx, tz, th in towers_by_lift[lift.name]:
            lines.append(f"tower {lift.name} {mm(tx)} {mm(tz)} {mm(th)}")
    lines.append("# tree <x> <z> <trunkRadius> <branchRadius>")
    lines += [f"tree {x} {z} {trunk} {branch}" for x, z, trunk, branch in trees]
    return "\n".join(lines) + "\n"


@dataclass(frozen=True)
class Built:
    """A built mountain: the arrays and records the files are written from."""

    mountain: Mountain
    heights: np.ndarray
    snow: np.ndarray
    runs: list
    towers: dict
    trees: list


def make(m: Mountain) -> Built:
    """Builds a mountain in memory."""
    heights, snow, runs = build(m)
    towers = {lift.name: lift_towers(m, heights, lift) for lift in m.lifts}
    trees = place_trees(m, heights, runs)
    return Built(m, heights, snow, runs, towers, trees)


def write(m: Mountain, out_dir=None, built: Built | None = None) -> dict[str, Path]:
    """Builds the mountain and writes its three files into out_dir (default Assets/Terrain/<Name>/). Returns their
    paths by file name."""
    out_dir = Path(out_dir) if out_dir is not None else TERRAIN_ROOT / m.name
    built = built if built is not None else make(m)
    outputs = {
        m.heightmap_file: encode_r16(m, built.heights),
        m.snow_file: built.snow.tobytes(),
        m.layout_file: layout_text(m, built.trees, built.towers).encode("ascii"),
    }
    out_dir.mkdir(parents=True, exist_ok=True)
    paths = {}
    for name, data in outputs.items():
        path = out_dir / name
        path.write_bytes(data)
        paths[name] = path
    return paths


# ----------------------------------------------------------------------------------------------
# Check
# ----------------------------------------------------------------------------------------------

SLOPE_WINDOW_M = 20.0  # a run's steepest and least slope are measured over this much of its centre line
# The sim's every-mountain test rides each run until this far into the base zone (sim/FirstChair.Sim.Tests/MountainRider.cs).
BASE_RIDE_IN_M = 10.0


def run_report(m: Mountain, heights: np.ndarray, run: Run) -> dict:
    """A run's length, drop, average slope, its steepest and least slope over SLOPE_WINDOW_M windows, and how far into
    the base zone its end lies (None when it ends outside it)."""
    line = catmull_rom(run.points_m, 1.0)
    s = along_m(line)
    h = bilinear(m, heights, line[:, 0], line[:, 1])
    window = max(1, int(SLOPE_WINDOW_M))
    grades = (h[:-window] - h[window:]) / (s[window:] - s[:-window]) if len(h) > window else np.array([0.0])
    end_x, end_z = run.points_m[-1]
    bx0, bz0, bx1, bz1 = base_zone_m(m)
    in_base = bx0 <= end_x <= bx1 and bz0 <= end_z <= bz1
    return {
        "name": run.name,
        "difficulty": run.difficulty,
        "length_m": float(s[-1]),
        "drop_m": float(h[0] - h[-1]),
        "average_deg": math.degrees(math.atan2(float(h[0] - h[-1]), float(s[-1]))),
        "steepest_deg": math.degrees(math.atan(float(grades.max()))),
        "least_deg": math.degrees(math.atan(float(grades.min()))),
        "end_into_base_m": end_z - bz0 if in_base else None,
    }


def report(built: Built) -> list[str]:
    """The check's report, one line per item."""
    m = built.mountain
    lines = [f"{m.title} ({m.name}): {m.grid.size_m:g} m square, {m.grid.resolution} samples a side, "
             f"heights {float(built.heights.min()):.1f} to {float(built.heights.max()):.1f} m"]
    for run in m.runs:
        r = run_report(m, built.heights, run)
        depth = r["end_into_base_m"]
        if depth is None:
            end = "ends outside the base area"
        else:
            end = f"ends {depth:.1f} m into the base area"
            if depth < BASE_RIDE_IN_M:
                end += f", short of the {BASE_RIDE_IN_M:g} m the sim's ride test goes in"
        lines.append(f"run {r['name']} ({r['difficulty']}): {r['length_m']:.0f} m long, drops {r['drop_m']:.0f} m, "
                     f"average {r['average_deg']:.1f} deg, steepest {r['steepest_deg']:.1f} deg, "
                     f"least {r['least_deg']:.1f} deg over {SLOPE_WINDOW_M:g} m, {end}")
    for lift in m.lifts:
        towers = built.towers[lift.name]
        least = lift_least_clearance_m(m, built.heights, lift, towers)
        tallest = max((h for _x, _z, h in towers), default=0.0)
        lines.append(f"lift {lift.name}: {len(towers)} towers, tallest {tallest:.1f} m, cable at least {least:.1f} m "
                     f"above the snow between the stations' approaches")
    counts = np.bincount(built.snow.ravel(), minlength=len(SNOW_TYPES)) / built.snow.size
    lines.append("snow: " + ", ".join(f"{name} {100.0 * counts[i]:.1f}%" for i, name in enumerate(SNOW_TYPES)))
    lines.append(f"trees: {len(built.trees)}")
    return lines


# ----------------------------------------------------------------------------------------------
# Preview
# ----------------------------------------------------------------------------------------------

SNOW_COLOURS = {  # map colours, sRGB
    "groomed": (242, 244, 248),
    "powder": (226, 236, 252),
    "crud": (214, 216, 222),
    "ice": (178, 204, 224),
    "slush": (214, 204, 186),
}
DIFFICULTY_COLOURS = {
    "green": (40, 158, 68),
    "blue": (40, 108, 212),
    "red": (212, 44, 44),
    "black": (24, 24, 24),
    "park": (240, 138, 0),
}
TREE_COLOUR = (38, 84, 52)
LIFT_COLOUR = (70, 70, 80)
CONTOUR_M = 10.0
SUN_AZIMUTH_DEG = 315.0  # from the map's top left, as maps are shaded
SUN_ELEVATION_DEG = 40.0


def preview_image(built: Built, pixels: int = 1024):
    """The mountain as a map: shaded relief over the snow colours, 10 m contours, the runs in their difficulty colours
    with their names, the trees, the lifts and towers, the park features and the start, with a scale bar."""
    from PIL import Image, ImageDraw, ImageFont

    m = built.mountain
    heights, snow = built.heights, built.snow
    gz, gx = np.gradient(heights, m.grid.cell_m)
    # The way to the sun: azimuth clockwise from the image's top (-z), so 315 degrees is the top left.
    az, el = math.radians(SUN_AZIMUTH_DEG), math.radians(SUN_ELEVATION_DEG)
    to_x, to_z, to_y = math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el)
    # A height field's normal is (-dh/dx, 1, -dh/dz), normalized; flat ground lights as sin(elevation).
    lit = (-gx * to_x - gz * to_z + to_y) / np.sqrt(gx * gx + gz * gz + 1.0)
    shade = np.clip(0.45 + 0.55 * lit / to_y, 0.4, 1.08)
    palette = np.array([SNOW_COLOURS[name] for name in SNOW_TYPES], dtype=np.float64)
    rgb = palette[snow] * shade[:, :, np.newaxis]
    band = np.floor(heights / CONTOUR_M)
    edge = (np.diff(band, axis=0, prepend=band[:1, :]) != 0) | (np.diff(band, axis=1, prepend=band[:, :1]) != 0)
    rgb[edge] = rgb[edge] * 0.82
    image = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).resize((pixels, pixels), Image.LANCZOS)
    draw = ImageDraw.Draw(image)
    scale = pixels / m.grid.size_m

    def px(point):
        return (point[0] * scale, point[1] * scale)

    for x_mm, z_mm, _trunk, branch in built.trees:
        cx, cz = px((x_mm / 1000.0, z_mm / 1000.0))
        r = max(1.0, branch / 1000.0 * scale)
        draw.ellipse((cx - r, cz - r, cx + r, cz + r), fill=TREE_COLOUR)
    font_size = max(10, pixels // 70)
    try:
        font = ImageFont.truetype("DejaVuSans.ttf", font_size)
    except OSError:
        font = ImageFont.load_default(font_size)
    for field, run in zip(built.runs, m.runs):
        points = [px(p) for p in field["line"][::4]] + [px(field["line"][-1])]
        width = max(2, int(run.width_m * scale * 0.25))
        draw.line(points, fill=DIFFICULTY_COLOURS[run.difficulty], width=width, joint="curve")
        label_at = px(field["line"][min(len(field["line"]) - 1, 40)])
        draw.text((label_at[0] + 6, label_at[1] - 6), run.name, fill=DIFFICULTY_COLOURS[run.difficulty], font=font)
    if m.park is not None:
        for kicker in m.park.kickers:
            cx, cz = px((m.park.kicker_lane_x_m, kicker.lip_z_m))
            w = max(2.0, kicker.width_m * scale / 2.0)
            draw.polygon([(cx - w, cz), (cx + w, cz), (cx, cz - 2.5 * w)], fill=(90, 90, 100))
        for rail in m.park.rails:
            draw.line([px((m.park.jib_lane_x_m, rail.from_z_m)), px((m.park.jib_lane_x_m, rail.to_z_m))],
                      fill=(60, 60, 70), width=2)
    for lift in m.lifts:
        draw.line([px(lift.bottom_m), px(lift.top_m)], fill=LIFT_COLOUR, width=2)
        for x, z, _h in built.towers[lift.name]:
            cx, cz = px((x, z))
            draw.rectangle((cx - 3, cz - 3, cx + 3, cz + 3), fill=LIFT_COLOUR)
        for station in (lift.bottom_m, lift.top_m):
            cx, cz = px(station)
            draw.rectangle((cx - 6, cz - 6, cx + 6, cz + 6), outline=LIFT_COLOUR, width=2)
    sx, sz = px(m.start_m)
    draw.ellipse((sx - 7, sz - 7, sx + 7, sz + 7), outline=(220, 30, 120), width=3)
    bar_m = 100.0 if m.grid.size_m >= 400.0 else 50.0
    x0, y0 = 16, pixels - 24
    draw.line([(x0, y0), (x0 + bar_m * scale, y0)], fill=(20, 20, 20), width=4)
    draw.text((x0, y0 - 22), f"{bar_m:g} m", fill=(20, 20, 20), font=font)
    draw.text((16, 12), f"{m.title}: the fall line runs down the image", fill=(20, 20, 20), font=font)
    return image


# ----------------------------------------------------------------------------------------------
# New
# ----------------------------------------------------------------------------------------------

STARTER = """# {name}: a new mountain, started by tools/mountain/mountain.py new. Edit it, then:
#   python3 tools/mountain/mountain.py check   {path}
#   python3 tools/mountain/mountain.py preview {path}
#   python3 tools/mountain/mountain.py build   {path}
# The format, field by field: tools/mountain/README.md. Positions are metres on the map: x across, z down the fall line
# from the summit side (z = 0) toward the base.

format = 1
name = "{name}"
title = "{name}"
start_m = [256.0, 40.0]

[grid]
resolution = 1025                     # 0.5 m a sample over 512 m
size_m = 512.0
max_height_m = 200.0

[profile]
base_height_m = 8.0
gradient = [[0.0, 0.03], [40.0, 0.03], [90.0, 0.30], [400.0, 0.30], [470.0, 0.02], [512.0, 0.0]]

[noise]
layers = [{{ spacing_m = 64.0, amplitude_m = 2.0, seed = 7 }}]

[[features]]                          # a rounded knoll beside the run
kind = "hill"
centre_m = [150.0, 260.0]
radius_m = 60.0
height_m = 10.0

[summit]
centre_m = [256.0, 30.0]
radius_m = 26.0
blend_m = 30.0
noise_free_m = [40.0, 100.0]

[base]
area_m = [140.0, 460.0, 372.0, 512.0]
blend_m = 20.0

[tree_line]
height_m = 150.0
spread_m = 12.0

[[runs]]
name = "first-run"
difficulty = "blue"
width_m = 36.0
shape = "open"
points_m = [[262.0, 50.0], [290.0, 150.0], [270.0, 280.0], [240.0, 400.0], [250.0, 465.0]]

[[lifts]]
name = "first-chair"
bottom_m = [200.0, 480.0]
top_m = [200.0, 34.0]

[trees]
glades = [{{ area_m = [330.0, 200.0, 400.0, 320.0], keep_per_mille = 380 }}]
"""


def starter_text(name: str, path: str) -> str:
    return STARTER.format(name=name, path=path)


# ----------------------------------------------------------------------------------------------
# Command line
# ----------------------------------------------------------------------------------------------

def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="First Chair's mountain tool: build, preview, check or start a mountain file.")
    commands = parser.add_subparsers(dest="command", required=True)
    build_cmd = commands.add_parser("build", help="write the heightmap, snow map and layout")
    build_cmd.add_argument("file", type=Path)
    build_cmd.add_argument("--out", type=Path, help="output directory (default: Assets/Terrain/<Name>/)")
    preview_cmd = commands.add_parser("preview", help="draw the mountain as a map")
    preview_cmd.add_argument("file", type=Path)
    preview_cmd.add_argument("--png", type=Path, help="the image (default: <file> with .png)")
    preview_cmd.add_argument("--pixels", type=int, default=1024)
    check_cmd = commands.add_parser("check", help="report the runs, lifts, snow and trees; fail on a problem")
    check_cmd.add_argument("file", type=Path)
    new_cmd = commands.add_parser("new", help="write a starter mountain file to edit")
    new_cmd.add_argument("name", help="a CamelCase name, such as PracticeHill")
    new_cmd.add_argument("--out", type=Path, help="the file (default: design/mountains/<name>.mountain.toml)")
    args = parser.parse_args(argv)

    try:
        if args.command == "new":
            out = args.out or REPO_ROOT / "design" / "mountains" / f"{args.name}.mountain.toml"
            if out.exists():
                raise MountainError(f"{out} already exists")
            shown = out.resolve().relative_to(REPO_ROOT).as_posix() if out.resolve().is_relative_to(REPO_ROOT) else str(out)
            text = starter_text(args.name, shown)
            parse(tomllib.loads(text), out)  # the starter must itself be a valid mountain
            out.parent.mkdir(parents=True, exist_ok=True)
            out.write_text(text, encoding="ascii")
            print(f"{out}: a starter mountain; check, preview and build it")
            return 0
        mountain = load(args.file)
        built = make(mountain)
        if args.command == "build":
            for path in write(mountain, args.out, built).values():
                print(f"{path}: {path.stat().st_size} bytes")
        elif args.command == "preview":
            png = args.png or args.file.with_suffix("").with_suffix(".png")
            preview_image(built, args.pixels).save(png)
            print(f"{png}: the map")
        else:
            print("\n".join(report(built)))
            encode_r16(mountain, built.heights)  # the heights fit the grid's range
    except MountainError as error:
        print(f"mountain: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
