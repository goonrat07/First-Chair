"""Tests for make_test_mountain.py (Story 010).

Run from the repo root: python3 -m pytest tools/terrain -q
The checks read the generated files back, not the generator's tables, so they test what Unity and the sim will load.
Where a check needs a run's centre line, it rebuilds it from the layout's points with the generator's own spline.
"""

import math
import sys
from pathlib import Path

import numpy as np
import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent))
import make_test_mountain as mountain  # noqa: E402

FILES = (mountain.HEIGHTMAP_FILE, mountain.SNOW_FILE, mountain.LAYOUT_FILE)

# SnowType byte values from Packages/com.firstchair.sim/Runtime/SnowMap.cs, written out independently of the generator.
SNOW_BYTES = {"groomed": 0, "powder": 1, "crud": 2, "ice": 3, "slush": 4}

MIN_DROP_MM = 250_000  # from the start to the lift's bottom station: a mountain, not a slope
MAX_GRADIENT = 0.6  # no run is steeper than about 31 degrees over any 20 m
MIN_GRADIENT = -0.1  # nor climbs more than 10% over any 20 m: a slow rider keeps moving
MAX_GRADIENT_CHANGE_PER_M = 0.2  # no lip or kink along a run's centre line, metre to metre
CAT_TRACK_LEVEL_MM = 1_000  # a cat track's edges sit within this of each other across 90% of its width
CABLE_CLEARANCE_MM = 7_000
STATION_APPROACH_MM = 25_000  # near a station the cable comes down for riders to get on and off
LIFT_CORRIDOR_MM = 12_000
TREE_LINE_MM = 205_000
HEIGHT_TOLERANCE_MM = 13.0  # two R16 steps at the mountain's height range
RAMP_STEPS = 8
EDGE_SKIP_M = 6.0  # a run's first and last metres meet the summit, the base or a trunk run


def read_layout(path: Path) -> dict:
    layout = {"zones": [], "kickers": [], "trees": [], "rails": [], "runs": [], "lifts": []}
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
        elif key == "run":
            numbers = [int(value) for value in values[2:]]
            points = [(numbers[i], numbers[i + 1]) for i in range(1, len(numbers), 2)]
            layout["runs"].append({"name": values[0], "difficulty": values[1], "width": numbers[0], "points": points})
        elif key == "lift":
            layout["lifts"].append({"name": values[0], "bottom": (int(values[1]), int(values[2])),
                                    "top": (int(values[3]), int(values[4])), "towers": []})
        elif key == "tower":
            lift = next(lift for lift in layout["lifts"] if lift["name"] == values[0])
            lift["towers"].append(tuple(int(value) for value in values[1:]))
    return layout


@pytest.fixture(scope="module")
def generated(tmp_path_factory) -> Path:
    out = tmp_path_factory.mktemp("mountain")
    mountain.write(out)
    return out


@pytest.fixture(scope="module")
def layout(generated) -> dict:
    return read_layout(generated / mountain.LAYOUT_FILE)


@pytest.fixture(scope="module")
def resolution(generated) -> int:
    return math.isqrt((generated / mountain.HEIGHTMAP_FILE).stat().st_size // 2)


@pytest.fixture(scope="module")
def heights_mm(generated, layout, resolution) -> np.ndarray:
    _size_x, _size_z, min_mm, max_mm = layout["spec"]
    samples = np.frombuffer((generated / mountain.HEIGHTMAP_FILE).read_bytes(), dtype="<u2").reshape(resolution, resolution)
    return min_mm + samples.astype(np.float64) * ((max_mm - min_mm) / 65535.0)


@pytest.fixture(scope="module")
def snow(generated, resolution) -> np.ndarray:
    return np.frombuffer((generated / mountain.SNOW_FILE).read_bytes(), dtype=np.uint8).reshape(resolution, resolution)


def zone(layout: dict, name: str) -> tuple:
    return next(rect for zone_name, _snow, rect in layout["zones"] if zone_name == name)


def inside(rect, x, z) -> bool:
    x_min, z_min, x_max, z_max = rect
    return x_min <= x <= x_max and z_min <= z <= z_max


def height_at(heights, layout, x_mm, z_mm):
    """Bilinear height at points, millimetres: the sim's Heightmap.Sample rule."""
    size_x, size_z, _min, _max = layout["spec"]
    cells = heights.shape[0] - 1
    grid_x = np.asarray(x_mm, dtype=np.float64) * cells / size_x
    grid_z = np.asarray(z_mm, dtype=np.float64) * cells / size_z
    column = np.minimum(grid_x.astype(np.int64), cells - 1)
    row = np.minimum(grid_z.astype(np.int64), cells - 1)
    u, v = grid_x - column, grid_z - row
    near = (1 - u) * heights[row, column] + u * heights[row, column + 1]
    far = (1 - u) * heights[row + 1, column] + u * heights[row + 1, column + 1]
    return (1 - v) * near + v * far


def snow_at(snow, layout, x_mm, z_mm):
    size_x, size_z, _min, _max = layout["spec"]
    cells = snow.shape[0] - 1
    return snow[np.rint(np.asarray(z_mm) * cells / size_z).astype(np.int64), np.rint(np.asarray(x_mm) * cells / size_x).astype(np.int64)]


def centre_line_mm(run: dict, step_m: float = 1.0) -> np.ndarray:
    """The run's centre line every step_m, millimetres: the generator's spline through the layout's points."""
    line = mountain.catmull_rom([(x / 1000.0, z / 1000.0) for x, z in run["points"]], step_m)
    return line * 1000.0


def distance_to_line_mm(line: np.ndarray, x: float, z: float) -> float:
    a, b = line[:-1], line[1:]
    d = b - a
    t = np.clip(((x - a[:, 0]) * d[:, 0] + (z - a[:, 1]) * d[:, 1]) / (d ** 2).sum(axis=1), 0.0, 1.0)
    return float(np.sqrt((x - (a[:, 0] + t * d[:, 0])) ** 2 + (z - (a[:, 1] + t * d[:, 1])) ** 2).min())


def run_profile(heights, layout, run):
    """Distance along the run (metres) and the height there (millimetres), every metre, ends trimmed."""
    line = centre_line_mm(run)
    s = mountain.along_m(line / 1000.0)
    keep = (s >= EDGE_SKIP_M) & (s <= s[-1] - EDGE_SKIP_M)
    return s[keep], height_at(heights, layout, line[keep, 0], line[keep, 1])


def test_two_runs_write_byte_identical_files(generated, tmp_path):
    mountain.write(tmp_path)

    for name in FILES:
        assert (tmp_path / name).read_bytes() == (generated / name).read_bytes(), name


def test_committed_files_match_a_fresh_run(generated):
    for name in FILES:
        committed = (mountain.DEFAULT_OUT / name).read_bytes()
        if name.endswith(".txt"):
            committed = committed.replace(b"\r\n", b"\n")  # a CRLF checkout holds the same layout
        assert committed == (generated / name).read_bytes(), f"{name} is stale or an LFS pointer: rerun make_test_mountain.py"


def test_heightmap_is_square_r16_at_half_a_metre(generated, layout, resolution):
    size_x, size_z, min_mm, max_mm = layout["spec"]

    assert 2 * resolution * resolution == (generated / mountain.HEIGHTMAP_FILE).stat().st_size
    assert (resolution - 1) & (resolution - 2) == 0, "N must be 2^n + 1"
    assert size_x == size_z == 500 * (resolution - 1)
    assert max_mm > min_mm
    assert (generated / mountain.SNOW_FILE).stat().st_size == resolution * resolution


def test_every_snow_type_is_on_the_mountain(snow):
    assert set(np.unique(snow)) == set(SNOW_BYTES.values())


def test_start_is_on_the_summit_above_the_lifts_top_and_the_bottom_station_is_in_the_base(layout, heights_mm):
    summit, base = zone(layout, "summit"), zone(layout, "base")
    lift = layout["lifts"][0]

    assert inside(summit, *layout["start"])
    assert inside(summit, *lift["top"])
    assert inside(base, *lift["bottom"])
    assert lift["top"][1] < layout["start"][1], "the rider starts below the top station, facing down the fall line"
    drop = height_at(heights_mm, layout, *layout["start"]) - height_at(heights_mm, layout, *lift["bottom"])
    assert drop >= MIN_DROP_MM


def test_the_bottom_station_sits_in_a_hollow_so_riders_stop_there(layout, heights_mm):
    x, z = layout["lifts"][0]["bottom"]
    at = height_at(heights_mm, layout, x, z)

    assert height_at(heights_mm, layout, x, z - 60_000) > at
    assert height_at(heights_mm, layout, x, z + 60_000) > at


def test_runs_cover_every_difficulty(layout):
    assert {run["difficulty"] for run in layout["runs"]} == {"green", "blue", "red", "black", "park"}


def test_each_run_goes_from_the_summit_toward_the_base(layout, heights_mm):
    for run in layout["runs"]:
        top, foot = run["points"][0], run["points"][-1]
        assert height_at(heights_mm, layout, *top) > height_at(heights_mm, layout, *foot) + 50_000, run["name"]


def test_each_run_is_smooth_and_never_too_steep_or_uphill(layout, heights_mm):
    for run in layout["runs"]:
        s, h = run_profile(heights_mm, layout, run)
        gradient = -np.diff(h) / 1000.0 / np.diff(s)
        over_20 = np.convolve(gradient, np.ones(20) / 20.0, mode="valid")

        assert over_20.max() <= MAX_GRADIENT, (run["name"], float(over_20.max()))
        assert over_20.min() >= MIN_GRADIENT, (run["name"], float(over_20.min()))
        assert np.abs(np.diff(gradient)).max() <= MAX_GRADIENT_CHANGE_PER_M, (run["name"], float(np.abs(np.diff(gradient)).max()))


def test_the_green_run_is_the_gentlest(layout, heights_mm):
    mean = {}
    for run in layout["runs"]:
        s, h = run_profile(heights_mm, layout, run)
        mean[run["difficulty"]] = (h[0] - h[-1]) / 1000.0 / (s[-1] - s[0])

    assert mean["green"] == min(mean.values()), mean
    assert mean["green"] <= 0.16, mean


def test_the_cat_track_is_level_across(layout, heights_mm):
    """Level across its width wherever it is on its own: where it leaves the summit or crosses another run, it rides
    that run's surface."""
    run = next(run for run in layout["runs"] if run["difficulty"] == "green")
    line = centre_line_mm(run) / 1000.0
    tangent = np.gradient(line, axis=0)
    tangent = tangent / np.sqrt((tangent ** 2).sum(axis=1))[:, np.newaxis]
    reach = run["width"] * 0.45
    s = mountain.along_m(line)
    others = [(centre_line_mm(other, 2.0), other["width"] / 2.0) for other in layout["runs"] if other is not run]
    on_another = np.array([any(distance_to_line_mm(other_line, x * 1000.0, z * 1000.0) < half + 30_000 for other_line, half in others)
                           for x, z in line])
    keep = (s >= 30.0) & (s <= s[-1] - 30.0) & ~on_another
    assert keep.sum() > 0.7 * len(line), "the cat track is mostly on its own"
    left = height_at(heights_mm, layout, line[:, 0] * 1000.0 - tangent[:, 1] * reach, line[:, 1] * 1000.0 + tangent[:, 0] * reach)
    right = height_at(heights_mm, layout, line[:, 0] * 1000.0 + tangent[:, 1] * reach, line[:, 1] * 1000.0 - tangent[:, 0] * reach)

    assert np.abs(left - right)[keep].max() <= CAT_TRACK_LEVEL_MM


def test_each_run_has_its_snow_down_its_centre_line(layout, snow):
    expected = {"green": "groomed", "blue": "groomed", "red": "groomed", "park": "groomed", "black": "crud"}
    for run in layout["runs"]:
        line = centre_line_mm(run)[10:-10]
        found = snow_at(snow, layout, line[:, 0], line[:, 1])
        allowed = {SNOW_BYTES[expected[run["difficulty"]]], SNOW_BYTES["groomed"]}  # where it crosses another groomer
        if run["difficulty"] == "black":
            allowed.add(SNOW_BYTES["ice"])
        assert set(np.unique(found)) <= allowed, (run["name"], set(np.unique(found)))


def test_no_tree_canopy_reaches_a_run(layout):
    lines = [(centre_line_mm(run, 2.0), run["width"] / 2.0) for run in layout["runs"]]
    assert len(layout["trees"]) > 2000
    for x, z, trunk, branch in layout["trees"]:
        assert 0 < trunk <= branch
        for line, half in lines:
            assert distance_to_line_mm(line, x, z) >= half + branch, (x, z)


def test_no_tree_stands_on_the_lift_line_the_summit_the_base_or_above_the_tree_line(layout, heights_mm):
    lift = layout["lifts"][0]
    summit, base = zone(layout, "summit"), zone(layout, "base")
    for x, z, _trunk, branch in layout["trees"]:
        assert abs(x - lift["bottom"][0]) >= LIFT_CORRIDOR_MM + branch, (x, z)
        assert not inside(summit, x, z) and not inside(base, x, z), (x, z)
        assert height_at(heights_mm, layout, x, z) <= TREE_LINE_MM, (x, z)


def test_the_lifts_cable_clears_the_snow_between_the_stations(layout, heights_mm):
    lift = layout["lifts"][0]
    (bx, bz), (tx, tz) = lift["bottom"], lift["top"]
    length = math.hypot(tx - bx, tz - bz)
    supports = [(0.0, mountain.LIFT_STATION_CABLE_M * 1000.0)]
    for x, z, height in lift["towers"]:
        along = ((x - bx) * (tx - bx) + (z - bz) * (tz - bz)) / length
        assert abs((x - bx) * (tz - bz) - (z - bz) * (tx - bx)) / length < 1.0, "every tower stands on the lift line"
        supports.append((along, float(height)))
    supports.append((length, mountain.LIFT_STATION_CABLE_M * 1000.0))
    assert [along for along, _h in supports] == sorted(along for along, _h in supports), "towers run bottom to top"
    assert len(lift["towers"]) >= 5

    for (a0, h0), (a1, h1) in zip(supports, supports[1:]):
        g0 = height_at(heights_mm, layout, bx + (tx - bx) * a0 / length, bz + (tz - bz) * a0 / length) + h0
        g1 = height_at(heights_mm, layout, bx + (tx - bx) * a1 / length, bz + (tz - bz) * a1 / length) + h1
        for along in np.arange(a0, a1, 1000.0):
            if min(along, length - along) < STATION_APPROACH_MM:
                continue
            cable = g0 + (g1 - g0) * (along - a0) / (a1 - a0)
            ground = height_at(heights_mm, layout, bx + (tx - bx) * along / length, bz + (tz - bz) * along / length)
            assert cable - ground >= CABLE_CLEARANCE_MM - HEIGHT_TOLERANCE_MM, (along, cable - ground)


def test_each_kicker_is_a_ramp_on_the_park(layout, heights_mm):
    park_run = next(run for run in layout["runs"] if run["difficulty"] == "park")
    line = centre_line_mm(park_run, 2.0)
    assert len(layout["kickers"]) == 4
    for lip_x, lip_z, facing, length, width, height in layout["kickers"]:
        assert facing == 0
        assert distance_to_line_mm(line, lip_x, lip_z) + width / 2.0 <= park_run["width"] / 2.0
        rises = []
        for step in range(RAMP_STEPS + 1):
            z = lip_z - length * (RAMP_STEPS - step) / RAMP_STEPS
            beside = (height_at(heights_mm, layout, lip_x + width, z) + height_at(heights_mm, layout, lip_x - width, z)) / 2
            rises.append(height_at(heights_mm, layout, lip_x, z) - beside)

        assert abs(rises[0]) <= 3 * HEIGHT_TOLERANCE_MM, "the ramp starts level with the slope"
        assert abs(rises[-1] - height) <= 3 * HEIGHT_TOLERANCE_MM, "the lip stands its height above the slope"
        assert all(later > earlier for earlier, later in zip(rises, rises[1:])), "the ramp rises all the way to the lip"


def test_each_rail_and_box_is_on_the_park_beside_the_kicker_lane(layout):
    park_run = next(run for run in layout["runs"] if run["difficulty"] == "park")
    line = centre_line_mm(park_run, 2.0)
    kicker_xs = {kicker[0] for kicker in layout["kickers"]}
    assert {rail[0] for rail in layout["rails"]} == {"rail", "box"}
    for kind, x0, z0, x1, z1, height, width in layout["rails"]:
        assert height > 0 and width > 0 and (x0, z0) != (x1, z1)
        for x, z in ((x0, z0), (x1, z1)):
            assert distance_to_line_mm(line, x, z) <= park_run["width"] / 2.0 - 2_000, (kind, x, z)
            assert all(abs(x - kx) >= 10_000 for kx in kicker_xs), "the jib lane keeps clear of the kickers' lane"
