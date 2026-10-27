#!/usr/bin/env bash
# EN_Conifer: every tree-kit phase for this asset, rebuilt from EN_Conifer_params.json by the shared tree driver.
# Usage: bash art/source/EN_Conifer/build/phases.sh [1|2|3|4|5|billboard|final|6|all]   (default all)
# Phase 6 exports snowy_spruce v2 (approved for the test build) to Assets/Art/Environment/EN_Conifer/.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
VERSION=2 STATUS="approved for use in the test build" \
INFERRED="base height 12 m and the 8 to 14 m game range (user decision),tier count and droop and plane widths (our notes and the user's review),all colours" \
  exec bash "$ROOT/tools/blender/trees/phases.sh" EN_Conifer "${1:-all}"
