#!/usr/bin/env bash
# Every tree-kit phase for one tree asset, rebuilt from art/source/<ASSET>/<ASSET>_params.json. Each tree asset's own
# build/phases.sh calls this with its name (EN_Conifer for story 007; its shape variants for story 016).
# Usage: bash tools/blender/trees/phases.sh <ASSET> [1|2|3|4|5|billboard|final|6|all]   (default all)
# Phase 6 exports the params' "variant" to Assets/Art/Environment/<ASSET>/. Optional environment for the export
# manifest: INFERRED, its list of inferred values (comma-separated); VERSION and STATUS, the tree's version and status.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
ASSET="${1:?usage: phases.sh <ASSET> [phase]}"
PHASE="${2:-all}"
A="$ROOT/art/source/$ASSET"
T="$ROOT/tools/blender/trees"
K="$ROOT/.claude/skills/tree-kit/scripts"
PK="$ROOT/.claude/skills/blender-image-to-3d/scripts"
B="${BLENDER_BIN:-blender}"
P="$A/${ASSET}_params.json"
M="$A/${ASSET}_master.blend"
X="$ROOT/Assets/Art/Environment/$ASSET"
G="$A/review/03_gates"
param() { python3 -c "import json; p=json.load(open('$P')); print($1)"; }
V="$(param "p['variant']")"
BUDGET="$(param "p['lods'][0]['budget_tris'][1]")"
INFERRED="${INFERRED:-all colours and the shape values (design choices, provisional until the look review)}"
VERSION="${VERSION:-1}"
STATUS="${STATUS:-built; provisional until the look review}"

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
  "$B" --background --python "$PK/validate.py" -- --blend "$M" --collections LOW --budget-tris "$BUDGET" \
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
  local E="$A/review/export" F="$A/review/final" H
  H="$(param "p['shape']['height_m']")"
  mkdir -p "$E" "$F"
  "$B" --background --python "$PK/export_delivery.py" -- --blend "$M" --out "$X" --name "$ASSET" --format FBX \
    --collections LOW --engine unity --no-animation --inferred "$INFERRED" > "$E/log_export.txt" 2>&1
  "$B" --background --python "$T/manifest_extras.py" -- --blend "$M" --params "$P" --manifest "$X/asset-manifest.json" \
    --variant "$V" --version "$VERSION" --status "$STATUS" --root "$ROOT" >> "$E/log_export.txt" 2>&1
  "$B" --background --python "$PK/roundtrip.py" -- --file "$X/$ASSET.fbx" --out "$E" --expect-height "$H" \
    > "$E/log_roundtrip.txt" 2>&1 || echo "roundtrip.py reported errors: see $E/roundtrip.json"
  "$B" --background --python "$T/export_check.py" -- --fbx "$X/$ASSET.fbx" --manifest "$X/asset-manifest.json" \
    --params "$P" --variant "$V" --out "$E" > "$E/log_check.txt" 2>&1 || echo "export_check.py reported FAIL: see $E/export_check.json"
  grep -E "^(CHECK|VERDICT)" "$E/log_check.txt"
  cp "$E/"{export_check.json,export_clay.png,export_colour.png,roundtrip.json} "$F/"
}

# The far LOD (story 007): LOD0 rendered front and side into a two-cell alpha atlas, gated by IoU against LOD0's
# silhouette; the Unity scene builder makes the crossed quads from <ASSET>_Billboard.json.
billboard() {
  local R="$A/review/05_billboard"
  "$B" --background --python "$T/billboard.py" -- --master "$M" --out "$R" | grep -E "^BILLBOARD"
  python3 "$T/billboard_atlas.py" --renders "$R" --params "$P" --out "$X"
  mkdir -p "$A/review/final"; cp "$R/gate_billboard.json" "$A/review/final/"
}

case "$PHASE" in
  1) phase1 ;; 2) phase2 ;; 3) phase3 ;; 4) phase4 ;; 5) phase5 ;; billboard) billboard ;; final) final ;; 6) phase6 ;;
  all) phase1; phase2; phase3; phase4; phase5; billboard; final; phase6 ;;
  *) echo "usage: $0 <ASSET> [1|2|3|4|5|billboard|final|6|all]"; exit 2 ;;
esac
