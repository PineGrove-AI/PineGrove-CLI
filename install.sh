#!/usr/bin/env bash
set -euo pipefail

GITEA_API="https://git.getpinegrove.eu/api/v1/repos/pinegrove-community/pinegrove-cli"
INSTALL_DIR="${PINEGROVE_INSTALL_DIR:-$HOME/.local/share/pinegrove-cli}"
BIN_DIR="${PINEGROVE_BIN_DIR:-$HOME/.local/bin}"
VENV_DIR="$INSTALL_DIR/runtime/python"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BOLD='\033[1m'; NC='\033[0m'
info()  { echo -e "${GREEN}==>${NC} ${BOLD}$*${NC}" >&2; }
warn()  { echo -e "${YELLOW}warning:${NC} $*" >&2; }
die()   { echo -e "${RED}error:${NC} $*" >&2; exit 1; }

# ── Prerequisites ──────────────────────────────────────────────────────────────

[[ "$(uname -s)" == "Linux" ]] || die "pinegrove-cli only runs on Linux."

for cmd in curl python3; do
    command -v "$cmd" &>/dev/null || die "'$cmd' is required but not installed."
done

if ! python3 -c "import ensurepip" &>/dev/null; then
    PY_VER=$(python3 -c "import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')")
    warn "python3-venv not found — installing python${PY_VER}-venv now (requires sudo)..."
    sudo apt-get install -y "python${PY_VER}-venv" || die "Failed to install python${PY_VER}-venv. Try: sudo apt install python${PY_VER}-venv"
fi

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

# ── Intro & confirmation ───────────────────────────────────────────────────────

echo -e ""
echo -e "${BOLD}Welcome to PineGrove CLI${NC}"
echo -e ""
echo -e "This installer will set up:"
echo -e "  • The pinegrove-cli binary"
echo -e "  • A self-contained Python runtime (no system Python conflicts)"
echo -e "  • vllm and its dependencies, with CUDA auto-detected for your GPU"
echo -e ""
echo -e "  Install location: ${BOLD}$INSTALL_DIR${NC}"
echo -e ""
read -r -p "Proceed with installation? [Y/n] " CONFIRM
[[ "${CONFIRM,,}" != "n" ]] || { echo "Aborted."; exit 0; }
echo ""

# ── Install ────────────────────────────────────────────────────────────────────

info "Installing pinegrove-cli to $INSTALL_DIR"
mkdir -p "$INSTALL_DIR" "$BIN_DIR"

info "Downloading binary..."
BINARY_URL=$(curl -fsSL "${GITEA_API}/releases?limit=1" \
    | python3 -c "
import sys, json
assets = json.load(sys.stdin)[0]['assets']
print(next(a['browser_download_url'] for a in assets if a['name'] == 'pinegrove-cli'))
") || die "Could not resolve download URL. Does a release exist?"
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
