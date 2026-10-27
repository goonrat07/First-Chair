"""Tests for the mountain tool (Story 015).

Run from the repo root: python3 -m pytest tools/mountain -q
Most tests read a tiny mountain (64 m, 129 samples a side) written here, so they run in moments; the two committed
mountains are built once each and compared byte for byte with their committed files.
"""

import copy
import sys
from pathlib import Path

import numpy as np
import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent))
import mountain  # noqa: E402

REPO = mountain.REPO_ROOT
TEST_MOUNTAIN = REPO / "design" / "mountains" / "test-mountain.mountain.toml"
PINE_BOWL = REPO / "design" / "mountains" / "pine-bowl.mountain.toml"


def tiny() -> dict:
    """A small valid mountain file, as parsed TOML: one run down a steady 0.3 gradient, no noise or lift."""
    return {
        "format": 1,
        "name": "Tiny",
        "start_m": [32.0, 6.0],
        "grid": {"resolution": 129, "size_m": 64.0, "max_height_m": 60.0},
        "profile": {"base_height_m": 4.0, "gradient": [[0.0, 0.3], [64.0, 0.3]]},
        "summit": {"centre_m": [32.0, 4.0], "radius_m": 4.0, "blend_m": 4.0, "noise_free_m": [6.0, 12.0]},
        "base": {"area_m": [16.0, 56.0, 48.0, 64.0], "blend_m": 4.0},
        "runs": [{"name": "one", "difficulty": "blue", "width_m": 8.0,
                  "points_m": [[32.0, 8.0], [34.0, 30.0], [32.0, 54.0]]}],
        "trees": {"cell_mm": 4000, "jitter_mm": 500, "branch_mm": [1200, 1300]},
    }


def with_changes(change) -> dict:
    data = copy.deepcopy(tiny())
    change(data)
    return data


def at(m: mountain.Mountain, heights: np.ndarray, x: float, z: float) -> float:
    return float(mountain.bilinear(m, heights, x, z))


# ----------------------------------------------------------------------------------------------
# The committed mountains
# ----------------------------------------------------------------------------------------------

@pytest.mark.parametrize("spec", [TEST_MOUNTAIN, PINE_BOWL], ids=["test-mountain", "pine-bowl"])
def test_committed_mountain_files_match_a_fresh_build(spec, tmp_path):
    # Arrange
    m = mountain.load(spec)

    # Act
    paths = mountain.write(m, tmp_path)

    # Assert: the heightmap and snow map byte for byte, the layout up to line endings
    committed = mountain.TERRAIN_ROOT / m.name
    for name, path in paths.items():
        expected = (committed / name).read_bytes()
        if name.endswith(".txt"):
            expected = expected.replace(b"\r\n", b"\n")
        assert path.read_bytes() == expected, f"{name} is stale or an LFS pointer: rebuild {spec.name}"


def test_test_mountain_heights_and_snow_are_story_010s(tmp_path):
    # Arrange: the SHA-256 of story 010's generator output (make_test_mountain.py before story 015), which the move
    # into the format must keep
    import hashlib
    expected = {
        "TestMountain.raw": "e149685eaf9a2e2c0d3e8b38d3632beecbeb2137451cb4ad202b59c0f7a3dd0d",
        "TestMountainSnow.raw": "7cddced289f392b0bd9cf6c97117605819c594ac8583a428cf38247a05a3db6d",
    }

    # Act
    paths = mountain.write(mountain.load(TEST_MOUNTAIN), tmp_path)

    # Assert
    for name, digest in expected.items():
        assert hashlib.sha256(paths[name].read_bytes()).hexdigest() == digest, name


def test_two_builds_write_byte_identical_files(tmp_path):
    # Arrange
    m = mountain.parse(tiny())

    # Act
    first = mountain.write(m, tmp_path / "a")
    second = mountain.write(m, tmp_path / "b")

    # Assert
    for name in first:
        assert first[name].read_bytes() == second[name].read_bytes(), name


# ----------------------------------------------------------------------------------------------
# Reading the format
# ----------------------------------------------------------------------------------------------

def test_parse_fills_the_documented_defaults():
    # Act
    m = mountain.parse(tiny())

    # Assert
    assert m.title == "Tiny"
    assert m.grid.min_height_m == 0.0
    assert m.centre_x_m == 32.0  # half the map
    assert m.runs[0].shape == "open" and m.runs[0].snow == "groomed"
    assert m.shoulder_m == {"open": 22.0, "smooth": 22.0, "cat": 14.0, "gully": 26.0}
    assert m.trees.seed == 21 and m.trees.keep_near_per_mille == 850
    assert m.snow_background == "crud" and m.snow_above_tree_line == "powder"
    assert m.park is None and m.lifts == () and m.features == ()


@pytest.mark.parametrize("change, message", [
    (lambda d: d.update(format=2), "format 2"),
    (lambda d: d["grid"].update(resolution=100), "2^n + 1"),
    (lambda d: d["grid"].update(colour="red"), "unknown key(s) colour"),
    (lambda d: d["runs"][0].pop("width_m"), "'width_m' is required"),
    (lambda d: d["runs"][0].update(width_m="wide"), "expected a number"),
    (lambda d: d["runs"][0].update(difficulty="purple"), "not one of"),
    (lambda d: d["runs"].append(dict(d["runs"][0])), "two runs are named 'one'"),
    (lambda d: d["runs"][0]["points_m"].append([80.0, 60.0]), "off the 64 m map"),
    (lambda d: d.update(name="tiny"), "CamelCase"),
    (lambda d: d["profile"].update(gradient=[[10.0, 0.3], [5.0, 0.3]]), "must rise"),
    (lambda d: d.update(snow={"areas": [{"snow": "ice", "rect_m": [0, 0, 1, 1], "circle_m": [0, 0, 1]}]}), "not both"),
    (lambda d: d.update(park={"run": "two", "kicker_lane_x_m": 30.0, "jib_lane_x_m": 34.0, "zone_m": [0, 0, 1, 1]}), "no run 'two'"),
    (lambda d: d["trees"].update(jitter_mm=1500), "canopy could leave its cell"),
])
def test_parse_names_the_problem(change, message):
    # Arrange
    data = with_changes(change)

    # Act / Assert
    with pytest.raises(mountain.MountainError, match=message.replace("(", r"\(").replace(")", r"\)").replace("^", r"\^").replace("+", r"\+")):
        mountain.parse(data)


def test_load_rejects_text_that_is_not_toml(tmp_path):
    # Arrange
    path = tmp_path / "broken.mountain.toml"
    path.write_text("format = = 1\n", encoding="ascii")

    # Act / Assert
    with pytest.raises(mountain.MountainError, match="not valid TOML"):
        mountain.load(path)


# ----------------------------------------------------------------------------------------------
# Features
# ----------------------------------------------------------------------------------------------

def feature_change(feature: dict):
    return lambda d: d.update(features=[feature])


def test_hill_rises_by_its_height_at_its_centre_and_not_beyond_its_radius():
    # Arrange
    plain = mountain.parse(tiny())
    hilly = mountain.parse(with_changes(feature_change(
        {"kind": "hill", "centre_m": [20.0, 30.0], "radius_m": 8.0, "height_m": 3.0})))
    before = mountain.base_heights_m(plain)

    # Act
    after = mountain.apply_features(hilly, before.copy())

    # Assert
    assert at(hilly, after, 20.0, 30.0) - at(plain, before, 20.0, 30.0) == pytest.approx(3.0, abs=1e-9)
    assert at(hilly, after, 20.0, 40.0) - at(plain, before, 20.0, 40.0) == pytest.approx(0.0, abs=1e-12)


def test_valley_sinks_along_its_line_and_ridge_rises():
    # Arrange
    line = [[10.0, 10.0], [12.0, 30.0], [10.0, 50.0]]
    plain = mountain.parse(tiny())
    valley = mountain.parse(with_changes(feature_change({"kind": "valley", "points_m": line, "width_m": 6.0, "depth_m": 2.0})))
    ridge = mountain.parse(with_changes(feature_change({"kind": "ridge", "points_m": line, "width_m": 6.0, "height_m": 2.0})))
    before = mountain.base_heights_m(plain)

    # Act
    sunk = mountain.apply_features(valley, before.copy())
    raised = mountain.apply_features(ridge, before.copy())

    # Assert: on the line (its first point is a sample) by the full depth or height; 10 m off it, untouched
    assert at(valley, sunk, 10.0, 10.0) - at(plain, before, 10.0, 10.0) == pytest.approx(-2.0, abs=1e-9)
    assert at(ridge, raised, 10.0, 10.0) - at(plain, before, 10.0, 10.0) == pytest.approx(2.0, abs=1e-9)
    assert at(ridge, raised, 24.0, 30.0) == pytest.approx(at(plain, before, 24.0, 30.0), abs=1e-12)


def test_flatten_levels_its_pad_at_the_ground_height_of_its_centre():
    # Arrange
    plain = mountain.parse(tiny())
    flat = mountain.parse(with_changes(feature_change({"kind": "flatten", "centre_m": [20.0, 30.0], "radius_m": 4.0})))
    before = mountain.base_heights_m(plain)
    level = at(plain, before, 20.0, 30.0)

    # Act
    after = mountain.apply_features(flat, before.copy())

    # Assert: level across the pad, where the slope dropped 0.3 a metre
    for x, z in ((20.0, 27.0), (20.0, 33.0), (17.0, 30.0), (23.0, 30.0)):
        assert at(flat, after, x, z) == pytest.approx(level, abs=1e-9)


# ----------------------------------------------------------------------------------------------
# Building, checking, previewing and starting
# ----------------------------------------------------------------------------------------------

def test_run_report_measures_a_steady_slope():
    # Arrange: a 0.3 gradient is 16.7 degrees
    m = mountain.parse(tiny())
    built = mountain.make(m)

    # Act
    report = mountain.run_report(m, built.heights, m.runs[0])

    # Assert: the run keeps a quarter of no noise, so its middle is the mountain's own slope
    assert report["length_m"] == pytest.approx(46.0, abs=1.0)
    assert 14.0 < report["average_deg"] < 18.0


def test_run_report_says_how_far_into_the_base_area_a_run_ends():
    # Arrange: the base zone the layout writes starts at z = 58, the area's 56 plus half its 4 m blend
    m = mountain.parse(with_changes(lambda d: d["runs"][0].update(points_m=[[32.0, 8.0], [34.0, 30.0], [32.0, 62.0]])))
    built = mountain.make(m)

    # Act
    report = mountain.run_report(m, built.heights, m.runs[0])

    # Assert
    assert report["end_into_base_m"] == pytest.approx(4.0)


def test_run_report_of_a_run_ending_above_the_base_area_has_no_depth():
    # Arrange: the tiny run ends at z = 54, above the base zone
    m = mountain.parse(tiny())
    built = mountain.make(m)

    # Act
    report = mountain.run_report(m, built.heights, m.runs[0])

    # Assert
    assert report["end_into_base_m"] is None


def test_a_lift_the_cable_cannot_span_is_an_error():
    # Arrange: a lift up the fall line with towers no taller than 2 m over a dip it must cross
    m = mountain.parse(with_changes(lambda d: (
        d.update(lifts=[{"name": "short", "bottom_m": [12.0, 60.0], "top_m": [12.0, 4.0], "tower_max_m": 2.0,
                         "tower_easy_m": 1.0, "tower_min_m": 1.0, "span_min_m": 10.0, "span_max_m": 20.0,
                         "station_approach_m": 5.0}],
                 features=[{"kind": "valley", "points_m": [[0.0, 30.0], [64.0, 30.0]], "width_m": 20.0, "depth_m": 12.0}]))))
    built_heights = mountain.build(m)[0]

    # Act / Assert
    with pytest.raises(mountain.MountainError, match="cannot clear the snow"):
        mountain.lift_towers(m, built_heights, m.lifts[0])


def test_heights_outside_the_grid_range_are_an_error():
    # Arrange: a 0.3 gradient over 64 m rises 19 m, above a 10 m range
    m = mountain.parse(with_changes(lambda d: d["grid"].update(max_height_m=10.0)))

    # Act / Assert
    with pytest.raises(mountain.MountainError, match="widen the height range"):
        mountain.write(m, Path("/nonexistent/never-written"))


def test_check_reports_each_run_lift_and_the_trees():
    # Arrange
    m = mountain.parse(with_changes(lambda d: d.update(
        lifts=[{"name": "chair", "bottom_m": [50.0, 60.0], "top_m": [50.0, 4.0], "min_clearance_m": 4.0}])))

    # Act
    lines = mountain.report(mountain.make(m))

    # Assert
    text = "\n".join(lines)
    assert "run one (blue)" in text and "lift chair:" in text and "trees:" in text


def test_preview_draws_a_square_map_of_the_asked_size():
    # Arrange
    built = mountain.make(mountain.parse(tiny()))

    # Act
    image = mountain.preview_image(built, 256)

    # Assert
    assert image.size == (256, 256)


def test_new_writes_a_starter_that_builds_and_checks(tmp_path):
    # Arrange
    path = tmp_path / "Starter.mountain.toml"

    # Act
    status = mountain.main(["new", "Starter", "--out", str(path)])
    m = mountain.load(path)
    built = mountain.make(m)

    # Assert
    assert status == 0
    assert m.name == "Starter" and len(m.runs) == 1 and len(m.lifts) == 1
    assert len(built.trees) > 0 and built.towers["first-chair"]
    mountain.encode_r16(m, built.heights)  # inside its height range


def test_new_refuses_to_overwrite(tmp_path, capsys):
    # Arrange
    path = tmp_path / "Taken.mountain.toml"
    path.write_text("# mine\n", encoding="ascii")

    # Act
    status = mountain.main(["new", "Taken", "--out", str(path)])

    # Assert
    assert status == 1
    assert "already exists" in capsys.readouterr().err
    assert path.read_text(encoding="ascii") == "# mine\n"
