#!/usr/bin/env bash
set -euo pipefail

BINARY_URL="https://git.getpinegrove.eu/pinegrove-community/pinegrove-cli/releases/latest/download/pinegrove-cli"
INSTALL_DIR="${PINEGROVE_INSTALL_DIR:-$HOME/.local/share/pinegrove-cli}"
BIN_DIR="${PINEGROVE_BIN_DIR:-$HOME/.local/bin}"
VENV_DIR="$INSTALL_DIR/runtime/python"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BOLD='\033[1m'; NC='\033[0m'
info()  { echo -e "${GREEN}==>${NC} ${BOLD}$*${NC}"; }
warn()  { echo -e "${YELLOW}warning:${NC} $*"; }
die()   { echo -e "${RED}error:${NC} $*" >&2; exit 1; }

# ── Prerequisites ──────────────────────────────────────────────────────────────

[[ "$(uname -s)" == "Linux" ]] || die "pinegrove-cli only runs on Linux."

for cmd in curl python3; do
    command -v "$cmd" &>/dev/null || die "'$cmd' is required but not installed."
done

# Require Python 3.9+
python3 -c '
import sys
if sys.version_info < (3, 9):
    print(f"Python 3.9+ required, found {sys.version}", file=sys.stderr)
    sys.exit(1)
' || die "Please upgrade Python and re-run this script."

# ── CUDA detection ─────────────────────────────────────────────────────────────

detect_cuda_major() {
    if command -v nvidia-smi &>/dev/null; then
        nvidia-smi --query-gpu=driver_version --format=csv,noheader 2>/dev/null | head -1 | grep -oP '^\d+' || true
    fi
}

resolve_vllm_install_args() {
    local cuda_major
    cuda_major=$(detect_cuda_major)

    if [[ -z "$cuda_major" ]]; then
        warn "No NVIDIA GPU detected. Installing CPU-only vllm (inference will be slow)."
        echo "--extra-index-url https://download.pytorch.org/whl/cpu"
    elif [[ "$cuda_major" -le 11 ]]; then
        info "Detected CUDA $cuda_major.x — using CUDA 11.8 wheels."
        echo "--extra-index-url https://download.pytorch.org/whl/cu118"
    else
        info "Detected CUDA $cuda_major.x — using default vllm wheels (CUDA 12)."
        echo ""
    fi
}

# ── Install ────────────────────────────────────────────────────────────────────

info "Installing pinegrove-cli to $INSTALL_DIR"
mkdir -p "$INSTALL_DIR" "$BIN_DIR"

info "Downloading binary..."
curl -fsSL "$BINARY_URL" -o "$INSTALL_DIR/pinegrove-cli"
chmod +x "$INSTALL_DIR/pinegrove-cli"

info "Creating Python runtime..."
python3 -m venv "$VENV_DIR"

info "Installing vllm (this may take a few minutes)..."
VLLM_ARGS=$(resolve_vllm_install_args)
# shellcheck disable=SC2086
"$VENV_DIR/bin/pip" install --quiet --upgrade pip
# shellcheck disable=SC2086
"$VENV_DIR/bin/pip" install vllm $VLLM_ARGS

# ── Symlink ────────────────────────────────────────────────────────────────────

ln -sf "$INSTALL_DIR/pinegrove-cli" "$BIN_DIR/pinegrove-cli"

# ── PATH setup ─────────────────────────────────────────────────────────────────

ensure_path() {
    local rc="$1"
    local line='export PATH="$HOME/.local/bin:$PATH"'
    if [[ -f "$rc" ]] && ! grep -qF '.local/bin' "$rc"; then
        echo "" >> "$rc"
        echo "# Added by pinegrove-cli installer" >> "$rc"
        echo "$line" >> "$rc"
        echo "  updated $rc"
    fi
}

if [[ ":$PATH:" != *":$BIN_DIR:"* ]]; then
    warn "$BIN_DIR is not in your PATH. Adding it to your shell config..."
    ensure_path "$HOME/.bashrc"
    ensure_path "$HOME/.zshrc"
    warn "Restart your shell or run: export PATH=\"\$HOME/.local/bin:\$PATH\""
fi

# ── Done ───────────────────────────────────────────────────────────────────────

echo ""
echo -e "${GREEN}✓ pinegrove-cli installed successfully!${NC}"
echo ""
echo "  Run:  pinegrove-cli init"
echo ""
