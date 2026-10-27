#!/usr/bin/env bash
# EN_ConiferBroad (story 016, a shape variant of EN_Conifer): every tree-kit phase for this asset, rebuilt from
# EN_ConiferBroad_params.json (written by tools/blender/trees/make_variant_params.py) by the shared tree driver.
# Usage: bash art/source/EN_ConiferBroad/build/phases.sh [1|2|3|4|5|billboard|final|6|all]   (default all)
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
exec bash "$ROOT/tools/blender/trees/phases.sh" EN_ConiferBroad "${1:-all}"
