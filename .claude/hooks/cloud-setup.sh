#!/bin/bash
# First Chair cloud setup -- SessionStart hook, Claude Code cloud sessions only.
#
# Installs what cloud sessions need and a fresh container lacks:
#   - Blender 5.2 LTS (headless asset builds), exported as BLENDER_BIN
#   - Mesa EGL, so Workbench renders (the Blender skill's gates) work without a GPU
#   - Pillow, for the skill's compose_review.py
#   - the .NET 10 SDK, for `dotnet test sim/`
# On the user's PC (CLAUDE_CODE_REMOTE unset) it exits at once and installs nothing.
#
# Idempotent: every step checks before installing, so a cached container skips the downloads.
# Synchronous on purpose: the tools are ready before the first prompt. Prints one line for the
# session; the details go to $LOG.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

TOOLS=/opt/first-chair
BLENDER_VERSION=5.2.2
BLENDER_SERIES=5.2
BLENDER_DIR="$TOOLS/blender-$BLENDER_VERSION-linux-x64"
DOTNET_CHANNEL=10.0
DOTNET_DIR="$TOOLS/dotnet"
LOG=/tmp/first-chair-cloud-setup.log
EGL_PACKAGES="libegl1 libegl-mesa0 libgl1-mesa-dri libgbm1"

CURRENT_STEP="start"
trap 'echo "First Chair cloud setup FAILED at: $CURRENT_STEP (see $LOG). Blender or dotnet may be missing this session."' ERR

mkdir -p "$TOOLS"
: > "$LOG"
step() { CURRENT_STEP="$*"; echo "== $*" >> "$LOG"; }

# 1. Mesa EGL for headless Workbench renders
step "Mesa EGL"
# shellcheck disable=SC2086
if ! dpkg -s $EGL_PACKAGES >/dev/null 2>&1; then
  # shellcheck disable=SC2086
  apt-get install -y --no-install-recommends $EGL_PACKAGES >>"$LOG" 2>&1 \
    || { apt-get update >>"$LOG" 2>&1 && apt-get install -y --no-install-recommends $EGL_PACKAGES >>"$LOG" 2>&1; }
fi

# 2. Blender
step "Blender $BLENDER_VERSION"
if [ ! -x "$BLENDER_DIR/blender" ]; then
  archive="$TOOLS/blender-$BLENDER_VERSION-linux-x64.tar.xz"
  curl -fsSL --retry 3 -o "$archive" \
    "https://download.blender.org/release/Blender$BLENDER_SERIES/blender-$BLENDER_VERSION-linux-x64.tar.xz"
  tar -xf "$archive" -C "$TOOLS"
  rm -f "$archive"
fi

# 3. Pillow
step "Pillow"
if ! python3 -c "import PIL" >/dev/null 2>&1; then
  python3 -m pip install --quiet pillow >>"$LOG" 2>&1 \
    || python3 -m pip install --quiet --break-system-packages pillow >>"$LOG" 2>&1
fi

# 4. .NET SDK
step ".NET $DOTNET_CHANNEL SDK"
if [ ! -x "$DOTNET_DIR/dotnet" ] || ! "$DOTNET_DIR/dotnet" --list-sdks 2>/dev/null | grep -q "^$DOTNET_CHANNEL\."; then
  curl -fsSL --retry 3 -o "$TOOLS/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh
  bash "$TOOLS/dotnet-install.sh" --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_DIR" >>"$LOG" 2>&1
fi

# 5. Session environment
step "environment"
if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo "export BLENDER_BIN=\"$BLENDER_DIR/blender\""
    echo "export DOTNET_ROOT=\"$DOTNET_DIR\""
    echo "export PATH=\"$DOTNET_DIR:$BLENDER_DIR:\$PATH\""
    echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1"
    echo "export DOTNET_NOLOGO=1"
  } >> "$CLAUDE_ENV_FILE"
fi

echo "First Chair cloud tools ready: Blender $BLENDER_VERSION (\$BLENDER_BIN), .NET SDK $("$DOTNET_DIR/dotnet" --version), Mesa EGL, Pillow. Log: $LOG"
