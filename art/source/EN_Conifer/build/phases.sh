#!/usr/bin/env bash
# EN_Conifer: every tree-kit phase for this asset, rebuilt from EN_Conifer_params.json.
# Usage: bash art/source/EN_Conifer/build/phases.sh [1|2|3|4|5|final|6|all]   (default all)
# Phase 6 exports snowy_spruce v2 (approved for the test build) to Assets/Art/Environment/EN_Conifer/.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
A="$ROOT/art/source/EN_Conifer"
K="$ROOT/.claude/skills/tree-kit/scripts"
PK="$ROOT/.claude/skills/blender-image-to-3d/scripts"
B="${BLENDER_BIN:-blender}"
P="$A/EN_Conifer_params.json"
M="$A/EN_Conifer_master.blend"
V=snowy_spruce
G="$A/review/03_gates"

ref() { python3 -c "import json; r=json.load(open('$A/ref/refs.json'))['$1']; print(r['m_per_px'], r['axis_col'], r['ground_row'])"; }

# world_gate.py <view> <collection> <name>
wg() {
  local MPP COL ROW AZ=0
  read -r MPP COL ROW <<< "$(ref "$1")"
  [ "$1" = side ] && AZ=90
  "$B" --background --python "$PK/world_gate.py" -- --blend "$M" --out "$G" --matte "$A/ref/${1}_matte.png" \
    --az "$AZ" --m-per-px "$MPP" --axis-col "$COL" --ground-row "$ROW" --collections "$2" --name "$3" > "$G/log_wg_$3.txt" 2>&1
  python3 -c "import json; g=json.load(open('$G/gate_$3.json')); print('world_gate $3 iou', round(g['iou'],4))"
}

gate() {
  "$B" --background --python "$PK/validate.py" -- --blend "$M" --collections LOW --budget-tris 1500 \
    --out "$G/validate.json" > "$G/log_validate.txt" 2>&1 || echo "validate.py reported FAIL: see $G/validate.json"
  "$B" --background --python "$K/tree_gate.py" -- --blend "$M" --params "$P" --refs "$A/ref/refs.json" \
    --out "$G" --lods "$1" --world-gate-dir "$G" 2>&1 | grep -E "^(GATE|VERDICT)" || true
}

phase1() { python3 "$K/make_profiles.py" --params "$P" --out "$A/ref" --lod 0; }
phase2() { "$B" --background --python "$K/build_tree.py" -- --params "$P" --out "$M" --lods 0 --variant "$V" | grep -E "^(BUILT|SKIP)"; }
phase3() { mkdir -p "$G"; wg front LOD0 front; wg side LOD0 side; gate 0; }
phase4() { "$B" --background --python "$K/look_render.py" -- --blend "$M" --params "$P" --out "$A/review/04_look" \
             --collections LOD0 > "$A/review/log_look.txt" 2>&1; grep "^SHOT" "$A/review/log_look.txt"; }
phase5() {
  "$B" --background --python "$K/build_tree.py" -- --params "$P" --out "$M" --lods 0,1 --variant "$V" | grep -E "^(BUILT|SKIP)"
  mkdir -p "$G"; wg front LOD0 front; wg side LOD0 side; wg front LOD1 front_lod1; gate 0,1
}
final() {
  local F="$A/review/final"; mkdir -p "$F"
  cp "$A/review/04_look/"{threequarter,front,top,game,cluster_game,cluster_low}.png "$A/review/04_look/render_info.json" "$F/"
  cp "$G/"{gates.json,validate.json,diff_front.png,diff_side.png,diff_top_lod0.png,diff_front_lod1.png,diff_top_lod1.png} "$F/"
}

phase6() {
  local X="$ROOT/Assets/Art/Environment/EN_Conifer" E="$A/review/export" F="$A/review/final" H
  H="$(python3 -c "import json; print(json.load(open('$P'))['shape']['height_m'])")"
  mkdir -p "$E" "$F"
  "$B" --background --python "$PK/export_delivery.py" -- --blend "$M" --out "$X" --name EN_Conifer --format FBX \
    --collections LOW --engine unity --no-animation \
    --inferred "base height 12 m and the 8 to 14 m game range (user decision),tier count and droop and plane widths (our notes and the user's review),all colours" \
    > "$E/log_export.txt" 2>&1
  "$B" --background --python "$A/build/manifest_extras.py" -- --blend "$M" --params "$P" --manifest "$X/asset-manifest.json" \
    --variant "$V" --root "$ROOT" >> "$E/log_export.txt" 2>&1
  "$B" --background --python "$PK/roundtrip.py" -- --file "$X/EN_Conifer.fbx" --out "$E" --expect-height "$H" \
    > "$E/log_roundtrip.txt" 2>&1 || echo "roundtrip.py reported errors: see $E/roundtrip.json"
  "$B" --background --python "$A/build/export_check.py" -- --fbx "$X/EN_Conifer.fbx" --manifest "$X/asset-manifest.json" \
    --params "$P" --variant "$V" --out "$E" > "$E/log_check.txt" 2>&1 || echo "export_check.py reported FAIL: see $E/export_check.json"
  grep -E "^(CHECK|VERDICT)" "$E/log_check.txt"
  cp "$E/"{export_check.json,export_clay.png,export_colour.png,roundtrip.json} "$F/"
}

case "${1:-all}" in
  1) phase1 ;; 2) phase2 ;; 3) phase3 ;; 4) phase4 ;; 5) phase5 ;; final) final ;; 6) phase6 ;;
  all) phase1; phase2; phase3; phase4; phase5; final; phase6 ;;
  *) echo "usage: $0 [1|2|3|4|5|final|6|all]"; exit 2 ;;
esac
