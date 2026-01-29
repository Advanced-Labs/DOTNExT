#!/bin/bash
# DOTNExT Build Environment Setup for Claude Code Web Sessions
# This script sets up the environment for building the runtime

set -e

echo "=== DOTNExT Build Environment Setup ==="

# Check/Install .NET SDK
if ! command -v dotnet &> /dev/null || [ ! -f "$HOME/.dotnet/dotnet" ]; then
    echo "Installing .NET SDK..."
    curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    chmod +x /tmp/dotnet-install.sh
    /tmp/dotnet-install.sh --channel 9.0
fi

export PATH="$HOME/.dotnet:$PATH"
echo "dotnet version: $(dotnet --version)"

# Check required tools
echo ""
echo "=== Checking Required Tools ==="
echo "cmake: $(cmake --version 2>/dev/null | grep -o 'cmake version [0-9.]*' || echo 'NOT FOUND')"
echo "clang: $(clang --version 2>/dev/null | grep -o 'clang version [0-9.]*' || echo 'NOT FOUND')"
echo "ninja: $(ninja --version 2>/dev/null || echo 'NOT FOUND')"

echo ""
echo "=== Environment Ready ==="
echo ""
echo "To build CLR (native runtime):"
echo "  cd /home/user/DOTNExT/src/runtime"
echo "  export PATH=\"\$HOME/.dotnet:\$PATH\""
echo "  ./build.sh clr -c Debug"
echo ""
echo "To build libs (managed libraries):"
echo "  ./build.sh libs -c Debug"
echo ""
echo "To build both:"
echo "  ./build.sh clr+libs -c Debug"
echo ""
