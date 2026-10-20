"""Builds the ride viewer page: tools/ride-preview/viewer.html with out/rides.json inlined.

Run the tool first (dotnet run --project tools/ride-preview -c Release), then:
    python3 tools/ride-preview/build_viewer.py [output.html]
The default output is tools/ride-preview/out/ride-viewer.html (gitignored, like rides.json).
"""
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
PLACEHOLDER = "/*__RIDES_JSON__*/"


def main() -> None:
    template = (HERE / "viewer.html").read_text(encoding="utf-8")
    rides = (HERE / "out" / "rides.json").read_text(encoding="utf-8")
    if PLACEHOLDER not in template:
        sys.exit("viewer.html has no " + PLACEHOLDER + " placeholder")
    # "</" would end the inline <script> early; "<\/" is the same JSON string.
    page = template.replace(PLACEHOLDER, rides.replace("</", "<\\/"))
    out = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else HERE / "out" / "ride-viewer.html"
    out.write_text(page, encoding="utf-8")
    print(f"Wrote {out} ({out.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
