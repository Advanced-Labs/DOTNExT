#!/bin/bash
#
# DOTNExT SessionStart Hook
#
# This hook runs automatically when a new Claude Code session starts.
# It checks if we're on Linux (web session) and runs quick setup.
#
# Keep this script FAST - it runs on every session start.
#

# Only run on Linux (Claude Code web sessions)
if [[ "$OSTYPE" != "linux"* ]]; then
    exit 0
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SETUP_SCRIPT="$REPO_ROOT/.claude/scripts/setup-dotnext-env.sh"

# Check if setup script exists
if [ ! -f "$SETUP_SCRIPT" ]; then
    echo "⚠️ DOTNExT setup script not found"
    exit 0
fi

# Run quick setup (idempotent, skips already-done work)
echo "🔧 DOTNExT: Checking build environment..."

# Export environment variables to persist for session
if [ -n "$CLAUDE_ENV_FILE" ]; then
    RUNTIME_DIR="$REPO_ROOT/src/runtime"
    cat >> "$CLAUDE_ENV_FILE" << EOF
export DOTNET_ROOT="$RUNTIME_DIR/.dotnet"
export PATH="\$DOTNET_ROOT:\$PATH"
export NUGET_PACKAGES="/tmp/nuget-packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
EOF
fi

# Check system deps (don't install automatically - that needs sudo and is slow)
missing_deps=()
for pkg in libkrb5-dev libicu-dev liblttng-ust-dev; do
    if ! dpkg -l "$pkg" &>/dev/null 2>&1; then
        missing_deps+=("$pkg")
    fi
done

if [ ${#missing_deps[@]} -gt 0 ]; then
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "📦 DOTNExT: Missing system packages for CLR build:"
    echo "   ${missing_deps[*]}"
    echo ""
    echo "   To set up the full build environment, run:"
    echo "   ./.claude/scripts/setup-dotnext-env.sh"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""
else
    echo "✓ DOTNExT: Build environment ready"
fi

exit 0
