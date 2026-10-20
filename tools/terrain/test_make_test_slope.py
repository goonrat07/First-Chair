"""Tests for make_test_slope.py (Story 003, acceptance criterion 1).

Run from the repo root: python3 -m pytest tools/terrain -q
The checks read the generated files back, not the generator's tables, so they test what Unity
and the sim will load.
"""

import math
import sys
from pathlib import Path

import numpy as np
import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent))
import make_test_slope as slope  # noqa: E402

FILES = (slope.HEIGHTMAP_FILE, slope.SNOW_FILE, slope.LAYOUT_FILE)

# SnowType byte values from Packages/com.firstchair.sim/Runtime/SnowMap.cs, written out
# independently of the generator's table.
SNOW_BYTES = {"groomed": 0, "powder": 1, "crud": 2, "ice": 3, "slush": 4}

MAX_CELL_MM = 500  # coarser than this cannot shape a kicker
HEIGHT_TOLERANCE_MM = 5.0  # two R16 steps at the slope's height range
RAMP_STEPS = 8


def read_layout(path: Path) -> dict:
    layout = {"zones": [], "kickers": [], "trees": [], "rails": []}
    for line in path.read_text(encoding="ascii").splitlines():
        fields = line.split()
        if not fields or fields[0].startswith("#"):
            continue
        key, values = fields[0], fields[1:]
        if key == "spec":
            layout["spec"] = tuple(int(value) for value in values)
        elif key == "background":
            layout["background"] = values[0]
        elif key == "zone":
            layout["zones"].append((values[0], values[1], tuple(int(value) for value in values[2:])))
        elif key == "start":
            layout["start"] = tuple(int(value) for value in values)
        elif key == "kicker":
            layout["kickers"].append(tuple(int(value) for value in values))
        elif key == "tree":
            layout["trees"].append(tuple(int(value) for value in values))
        elif key in ("rail", "box"):
            layout["rails"].append((key,) + tuple(int(value) for value in values))
    return layout


@pytest.fixture(scope="module")
def generated(tmp_path_factory) -> Path:
    out = tmp_path_factory.mktemp("slope")
    slope.write(out)
    return out


@pytest.fixture(scope="module")
def layout(generated) -> dict:
    return read_layout(generated / slope.LAYOUT_FILE)


@pytest.fixture(scope="module")
def resolution(generated) -> int:
    return math.isqrt((generated / slope.HEIGHTMAP_FILE).stat().st_size // 2)


@pytest.fixture(scope="module")
def heights_mm(generated, layout, resolution) -> np.ndarray:
    _size_x, _size_z, min_mm, max_mm = layout["spec"]
    samples = np.frombuffer((generated / slope.HEIGHTMAP_FILE).read_bytes(), dtype="<u2").reshape(resolution, resolution)
    return min_mm + samples.astype(np.float64) * ((max_mm - min_mm) / 65535.0)


@pytest.fixture(scope="module")
def snow(generated, resolution) -> np.ndarray:
    return np.frombuffer((generated / slope.SNOW_FILE).read_bytes(), dtype=np.uint8).reshape(resolution, resolution)


def zone(layout: dict, name: str) -> tuple:
    return next(rect for zone_name, _snow, rect in layout["zones"] if zone_name == name)


def inside(rect, x, z) -> bool:
    x_min, z_min, x_max, z_max = rect
    return x_min <= x <= x_max and z_min <= z <= z_max


def mask(rect, layout, resolution) -> np.ndarray:
    size_x, size_z, _min, _max = layout["spec"]
    x_min, z_min, x_max, z_max = rect
    index = np.arange(resolution, dtype=np.int64)
    cells = resolution - 1
    in_x = (index * size_x >= x_min * cells) & (index * size_x <= x_max * cells)
    in_z = (index * size_z >= z_min * cells) & (index * size_z <= z_max * cells)
    return in_z[:, np.newaxis] & in_x[np.newaxis, :]


def height_at(heights, layout, x_mm, z_mm) -> float:
    """Bilinear height at a point, millimetres: the sim's Heightmap.Sample rule."""
    size_x, size_z, _min, _max = layout["spec"]
    cells = heights.shape[0] - 1
    grid_x, grid_z = x_mm * cells / size_x, z_mm * cells / size_z
    column, row = min(int(grid_x), cells - 1), min(int(grid_z), cells - 1)
    u, v = grid_x - column, grid_z - row
    near = (1 - u) * heights[row, column] + u * heights[row, column + 1]
    far = (1 - u) * heights[row + 1, column] + u * heights[row + 1, column + 1]
    return (1 - v) * near + v * far


def test_two_runs_write_byte_identical_files(generated, tmp_path):
    slope.write(tmp_path)

    for name in FILES:
        assert (tmp_path / name).read_bytes() == (generated / name).read_bytes(), name


def test_committed_files_match_a_fresh_run(generated):
    for name in FILES:
        committed = (slope.DEFAULT_OUT / name).read_bytes()
        if name.endswith(".txt"):
            committed = committed.replace(b"\r\n", b"\n")  # a CRLF checkout holds the same layout
        assert committed == (generated / name).read_bytes(), f"{name} is stale or an LFS pointer: rerun make_test_slope.py"


def test_heightmap_is_square_r16_at_half_a_metre_or_finer(generated, layout, resolution):
    size_x, size_z, min_mm, max_mm = layout["spec"]

    assert 2 * resolution * resolution == (generated / slope.HEIGHTMAP_FILE).stat().st_size
    assert resolution >= 3 and (resolution - 1) & (resolution - 2) == 0, "N must be 2^n + 1"
    assert size_x <= MAX_CELL_MM * (resolution - 1) and size_z <= MAX_CELL_MM * (resolution - 1)
    assert max_mm > min_mm
    assert (generated / slope.SNOW_FILE).stat().st_size == resolution * resolution


def test_every_snow_type_is_on_the_slope(snow):
    assert set(np.unique(snow).tolist()) == set(SNOW_BYTES.values())


def test_snow_map_is_the_layout_zones_painted_in_order(layout, snow, resolution):
    expected = np.full_like(snow, SNOW_BYTES[layout["background"]])
    for _name, snow_name, rect in layout["zones"]:
        expected[mask(rect, layout, resolution)] = SNOW_BYTES[snow_name]

    assert np.array_equal(snow, expected)
    for name in ("groomer", "park", "bowl", "glade", "runout"):
        x_min, z_min, x_max, z_max = zone(layout, name)
        assert x_min < x_max and z_min < z_max, name


def test_each_kicker_is_a_ramp_in_the_park(layout, heights_mm):
    park = zone(layout, "park")
    assert layout["kickers"]
    for lip_x, lip_z, facing, length, width, height in layout["kickers"]:
        assert inside(park, lip_x, lip_z)
        theta = math.radians(facing / 1000.0)
        forward_x, forward_z = math.sin(theta), math.cos(theta)
        across_x, across_z = forward_z * width, -forward_x * width
        rises = []
        for step in range(RAMP_STEPS + 1):
            back = length * (RAMP_STEPS - step) / RAMP_STEPS
            x, z = lip_x - forward_x * back, lip_z - forward_z * back
            beside = (height_at(heights_mm, layout, x + across_x, z + across_z) + height_at(heights_mm, layout, x - across_x, z - across_z)) / 2
            rises.append(height_at(heights_mm, layout, x, z) - beside)

        assert abs(rises[0]) <= HEIGHT_TOLERANCE_MM, "the ramp starts level with the slope"
        assert abs(rises[-1] - height) <= HEIGHT_TOLERANCE_MM, "the lip stands its height above the slope"
        assert all(later > earlier for earlier, later in zip(rises, rises[1:])), "the ramp rises all the way to the lip"


def test_each_tree_canopy_is_inside_the_glade(layout):
    glade = zone(layout, "glade")
    assert layout["trees"]
    for x, z, trunk, branch in layout["trees"]:
        assert 0 < trunk <= branch
        assert inside(glade, x - branch, z - branch) and inside(glade, x + branch, z + branch)


def test_each_rail_and_box_is_on_the_kicker_line_clear_of_every_ramp_and_landing(layout):
    """Story 006: rails and boxes stand on the park's kicker line, never on a ramp or in a landing."""
    assert {rail[0] for rail in layout["rails"]} == {"rail", "box"}
    park = next(rect for name, _snow, rect in layout["zones"] if name == "park")
    line_xs = {kicker[0] for kicker in layout["kickers"]}
    for kind, x0, z0, x1, z1, height, width in layout["rails"]:
        assert height > 0 and width > 0 and (x0, z0) != (x1, z1)
        for x, z in ((x0, z0), (x1, z1)):
            assert park[0] <= x <= park[2] and park[1] <= z <= park[3], (kind, x, z)
            assert x in line_xs, (kind, x)
        for lip_x, lip_z, facing, length, _width, _height in layout["kickers"]:
            assert facing == 0  # every kicker launches down +Z, so z alone orders ramp, lip and landing
            ramp_start = lip_z - length
            landing_end = lip_z + slope.LANDING_CLEARANCE_MM
            assert max(z0, z1) < ramp_start or min(z0, z1) > landing_end, (kind, z0, z1, lip_z)


def test_runout_is_level(layout, heights_mm, resolution):
    runout = heights_mm[mask(zone(layout, "runout"), layout, resolution)]

    assert runout.size > 0
    assert np.all(runout == runout[0])


def test_groomer_falls_toward_plus_z_from_the_start(layout, heights_mm, resolution):
    size_x, size_z, _min, _max = layout["spec"]
    start_x, start_z = layout["start"]
    assert inside(zone(layout, "groomer"), start_x, start_z)
    column = round(start_x * (resolution - 1) / size_x)
    first_row = round(start_z * (resolution - 1) / size_z)
    last_row = round(zone(layout, "runout")[1] * (resolution - 1) / size_z)

    profile = heights_mm[first_row:last_row + 1, column]

    assert np.all(np.diff(profile) <= 0)
    assert profile[-1] < profile[0] - 100_000
