#!/usr/bin/env python3
"""Generate the First Chair test mountain (Story 010), now from its mountain file (Story 015).

Usage: python3 tools/terrain/make_test_mountain.py [--out DIR]

The test mountain is design/mountains/test-mountain.mountain.toml, built by the mountain tool,
tools/mountain/mountain.py; this script is that build under its story 010 name, so its tests and anyone's habit keep
working. Writes TestMountain.raw, TestMountainSnow.raw and TestMountain.txt into DIR (default
Assets/Terrain/TestMountain/); see tools/mountain/mountain.py for the formats.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "mountain"))
import mountain as tool  # noqa: E402

REPO_ROOT = tool.REPO_ROOT
SPEC = REPO_ROOT / "design" / "mountains" / "test-mountain.mountain.toml"
MOUNTAIN = tool.load(SPEC)

DEFAULT_OUT = tool.TERRAIN_ROOT / MOUNTAIN.name
HEIGHTMAP_FILE = MOUNTAIN.heightmap_file
SNOW_FILE = MOUNTAIN.snow_file
LAYOUT_FILE = MOUNTAIN.layout_file
LIFT_STATION_CABLE_M = MOUNTAIN.lifts[0].station_cable_m

# The spline and the distances along it, as the layout's runs are drawn (the tests rebuild centre lines with them).
catmull_rom = tool.catmull_rom
along_m = tool.along_m


def write(out_dir: Path) -> dict[str, Path]:
    """Generates the mountain and writes its three files into out_dir. Returns their paths by name."""
    return tool.write(MOUNTAIN, out_dir)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Generate the First Chair test mountain (Story 010).")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT, help="output directory (default: %(default)s)")
    args = parser.parse_args(argv)
    for name, path in write(args.out).items():
        print(f"{path}: {path.stat().st_size} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
