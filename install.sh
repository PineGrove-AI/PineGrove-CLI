#!/usr/bin/env bash
set -euo pipefail

GITEA_API="https://git.getpinegrove.eu/api/v1/repos/pinegrove-community/pinegrove-cli"
INSTALL_DIR="${PINEGROVE_INSTALL_DIR:-$HOME/.local/share/pinegrove-cli}"
BIN_DIR="${PINEGROVE_BIN_DIR:-$HOME/.local/bin}"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BOLD='\033[1m'; NC='\033[0m'
info()  { echo -e "${GREEN}==>${NC} ${BOLD}$*${NC}" >&2; }
warn()  { echo -e "${YELLOW}warning:${NC} $*" >&2; }
die()   { echo -e "${RED}error:${NC} $*" >&2; exit 1; }

# ── Prerequisites ──────────────────────────────────────────────────────────────

[[ "$(uname -s)" == "Linux" ]] || die "pinegrove-cli only runs on Linux."

command -v curl &>/dev/null || die "'curl' is required but not installed."
command -v docker &>/dev/null || die "'docker' is required. Install it from: https://docs.docker.com/get-docker/"
docker info &>/dev/null 2>&1 || die "Docker daemon is not running. Start it with: sudo systemctl start docker"

# Warn if NVIDIA Container Toolkit is likely missing (needed for GPU passthrough)
if command -v nvidia-smi &>/dev/null; then
    if ! docker info 2>/dev/null | grep -qi "nvidia"; then
        warn "NVIDIA GPU detected but the NVIDIA Container Toolkit may not be installed."
        warn "Install it: https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/install-guide.html"
    fi
fi

# ── Resolve release (before welcome, so version is shown upfront) ──────────────

RELEASE_JSON=$(curl -fsSL "${GITEA_API}/releases?limit=1") \
    || die "Could not reach release API."
RELEASE_VERSION=$(echo "$RELEASE_JSON" | python3 -c "import sys,json; print(json.load(sys.stdin)[0]['tag_name'])") \
    || die "Could not parse release version."
BINARY_URL=$(echo "$RELEASE_JSON" | python3 -c "
import sys, json
assets = json.load(sys.stdin)[0]['assets']
print(next(a['browser_download_url'] for a in assets if a['name'] == 'pinegrove-cli'))
") || die "Could not resolve download URL. Does a release exist?"

# ── Intro & confirmation ───────────────────────────────────────────────────────

echo -e ""
echo -e "${BOLD}Welcome to PineGrove CLI ($RELEASE_VERSION)${NC}"
echo -e ""
echo -e "This installer will set up:"
echo -e "  • The pinegrove-cli binary"
echo -e "  • vLLM runs as a Docker container — no Python setup required"
echo -e ""
echo -e "  Install location: ${BOLD}$INSTALL_DIR${NC}"
echo -e ""
read -r -p "Proceed with installation? [Y/n] " CONFIRM
[[ "${CONFIRM,,}" != "n" ]] || { echo "Aborted."; exit 0; }
echo ""

# ── Install ────────────────────────────────────────────────────────────────────

info "Installing pinegrove-cli to $INSTALL_DIR"
mkdir -p "$INSTALL_DIR" "$BIN_DIR"

info "Downloading binary ($RELEASE_VERSION)..."
curl -fsSL "$BINARY_URL" -o "$INSTALL_DIR/pinegrove-cli"
chmod +x "$INSTALL_DIR/pinegrove-cli"

info "Pulling vLLM Docker image (this may take a few minutes on first install)..."
docker pull vllm/vllm-openai:latest

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
