#!/bin/bash
#
# DOTNExT Build Environment Setup for Claude Code Web Sessions
#
# This script sets up everything needed to build the .NET runtime
# in the restricted Claude Code web environment (Linux with proxy).
#
# Usage:
#   ./setup-dotnext-env.sh           # Full setup
#   ./setup-dotnext-env.sh --quick   # Skip already-done steps
#   ./setup-dotnext-env.sh --status  # Just check status
#
# This script is idempotent - safe to run multiple times.
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
RUNTIME_DIR="$REPO_ROOT/src/runtime"
SDK_DIR="$RUNTIME_DIR/.dotnet/sdk/9.0.111"
LOCAL_FEED="/tmp/nuget-feed"
ARCADE_CACHE="/tmp/arcade-sdk-cache"

# Arcade SDK packages to download
declare -A ARCADE_PACKAGES=(
    ["Microsoft.DotNet.Arcade.Sdk"]="9.0.0-beta.25515.2"
    ["Microsoft.DotNet.Helix.Sdk"]="9.0.0-beta.25515.2"
    ["Microsoft.DotNet.SharedFramework.Sdk"]="9.0.0-beta.25515.2"
    ["Microsoft.Build.NoTargets"]="3.7.0"
    ["Microsoft.Build.Traversal"]="3.4.0"
    ["Microsoft.NET.Sdk.IL"]="9.0.0-rtm.24511.16"
)

# System packages needed for CLR build
SYSTEM_PACKAGES=(
    "libkrb5-dev"      # Kerberos authentication
    "libicu-dev"       # Internationalization
    "liblttng-ust-dev" # Linux tracing
)

echo_status() {
    echo -e "${BLUE}==>${NC} $1"
}

echo_success() {
    echo -e "${GREEN}✓${NC} $1"
}

echo_warning() {
    echo -e "${YELLOW}!${NC} $1"
}

echo_error() {
    echo -e "${RED}✗${NC} $1"
}

# Check if running on Linux (Claude Code web environment)
check_platform() {
    if [[ "$OSTYPE" != "linux"* ]]; then
        echo_error "This script is for Linux (Claude Code web sessions) only."
        echo "On Windows, use the standard build commands."
        exit 1
    fi
}

# Check and install system dependencies
install_system_deps() {
    echo_status "Checking system dependencies..."

    local missing_packages=()

    for pkg in "${SYSTEM_PACKAGES[@]}"; do
        if ! dpkg -l "$pkg" &>/dev/null; then
            missing_packages+=("$pkg")
        fi
    done

    if [ ${#missing_packages[@]} -eq 0 ]; then
        echo_success "All system packages already installed"
        return 0
    fi

    echo_status "Installing missing packages: ${missing_packages[*]}"
    apt-get update -qq
    apt-get install -y -qq "${missing_packages[@]}"
    echo_success "System packages installed"
}

# Download Arcade SDK packages via wget (bypasses proxy)
download_arcade_packages() {
    echo_status "Downloading Arcade SDK packages..."

    mkdir -p "$ARCADE_CACHE"

    local all_present=true
    for pkg in "${!ARCADE_PACKAGES[@]}"; do
        local version="${ARCADE_PACKAGES[$pkg]}"
        local name_lower=$(echo "$pkg" | tr '[:upper:]' '[:lower:]')
        local filename="$name_lower.$version.nupkg"

        if [ ! -f "$ARCADE_CACHE/$filename" ]; then
            all_present=false
            break
        fi
    done

    if $all_present; then
        echo_success "All Arcade packages already downloaded"
        return 0
    fi

    # Azure DevOps feed URLs
    local AZURE_FEEDS=(
        "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/flat2"
        "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/flat2"
        "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/flat2"
    )

    for pkg in "${!ARCADE_PACKAGES[@]}"; do
        local version="${ARCADE_PACKAGES[$pkg]}"
        local name_lower=$(echo "$pkg" | tr '[:upper:]' '[:lower:]')
        local filename="$name_lower.$version.nupkg"

        if [ -f "$ARCADE_CACHE/$filename" ]; then
            echo_success "$pkg already downloaded"
            continue
        fi

        echo "  Downloading $pkg $version..."
        local downloaded=false

        for feed in "${AZURE_FEEDS[@]}"; do
            local url="$feed/$name_lower/$version/$filename"
            if wget -q --timeout=30 -O "$ARCADE_CACHE/$filename" "$url" 2>/dev/null; then
                downloaded=true
                echo_success "  Downloaded $pkg"
                break
            fi
        done

        if ! $downloaded; then
            echo_warning "  Failed to download $pkg (may not be critical)"
        fi
    done
}

# Install Arcade SDKs to the repo's local .dotnet directory
install_arcade_sdks() {
    echo_status "Installing Arcade SDKs to repo SDK directory..."

    if [ ! -d "$SDK_DIR" ]; then
        echo_warning "SDK directory not found: $SDK_DIR"
        echo "  Run the build once to bootstrap the SDK, or download manually."
        return 1
    fi

    local sdks_dir="$SDK_DIR/Sdks"
    mkdir -p "$sdks_dir"

    for pkg in "${!ARCADE_PACKAGES[@]}"; do
        local version="${ARCADE_PACKAGES[$pkg]}"
        local name_lower=$(echo "$pkg" | tr '[:upper:]' '[:lower:]')
        local filename="$name_lower.$version.nupkg"
        local nupkg_path="$ARCADE_CACHE/$filename"
        local sdk_install_dir="$sdks_dir/$pkg"

        if [ ! -f "$nupkg_path" ]; then
            echo_warning "  Package not found: $filename"
            continue
        fi

        # Check if already installed with correct structure
        if [ -d "$sdk_install_dir/Sdk" ]; then
            echo_success "$pkg already installed"
            continue
        fi

        echo "  Installing $pkg..."
        rm -rf "$sdk_install_dir"
        mkdir -p "$sdk_install_dir"
        unzip -q -o "$nupkg_path" -d "$sdk_install_dir" 2>/dev/null || true

        # Fix folder case: sdk -> Sdk (required on Linux)
        if [ -d "$sdk_install_dir/sdk" ] && [ ! -d "$sdk_install_dir/Sdk" ]; then
            mv "$sdk_install_dir/sdk" "$sdk_install_dir/Sdk"
            echo_success "  $pkg installed (with case fix)"
        else
            echo_success "  $pkg installed"
        fi
    done
}

# Set up local NuGet feed
setup_local_nuget_feed() {
    echo_status "Setting up local NuGet feed..."

    mkdir -p "$LOCAL_FEED"

    # Copy packages to local feed
    if [ -d "$ARCADE_CACHE" ]; then
        cp -n "$ARCADE_CACHE"/*.nupkg "$LOCAL_FEED/" 2>/dev/null || true
    fi

    # Create NuGet.config for the local feed
    cat > "$LOCAL_FEED/NuGet.config" << 'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-cache" value="/tmp/nuget-feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <fallbackPackageFolders>
    <clear />
  </fallbackPackageFolders>
</configuration>
EOF

    local pkg_count=$(ls -1 "$LOCAL_FEED"/*.nupkg 2>/dev/null | wc -l)
    echo_success "Local NuGet feed ready with $pkg_count packages"
}

# Export environment variables for builds
setup_environment() {
    echo_status "Setting up environment variables..."

    # If CLAUDE_ENV_FILE exists (SessionStart hook), persist env vars
    if [ -n "$CLAUDE_ENV_FILE" ]; then
        cat >> "$CLAUDE_ENV_FILE" << EOF
export DOTNET_ROOT="$RUNTIME_DIR/.dotnet"
export PATH="\$DOTNET_ROOT:\$PATH"
export NUGET_PACKAGES="/tmp/nuget-packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
EOF
        echo_success "Environment variables persisted for session"
    else
        # Export for current shell
        export DOTNET_ROOT="$RUNTIME_DIR/.dotnet"
        export PATH="$DOTNET_ROOT:$PATH"
        export NUGET_PACKAGES="/tmp/nuget-packages"
        export DOTNET_CLI_TELEMETRY_OPTOUT=1
        export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
        echo_success "Environment variables set (current shell only)"
    fi
}

# Print status summary
print_status() {
    echo ""
    echo "======================================"
    echo "DOTNExT Build Environment Status"
    echo "======================================"
    echo ""

    # System packages
    echo "System Packages:"
    for pkg in "${SYSTEM_PACKAGES[@]}"; do
        if dpkg -l "$pkg" &>/dev/null 2>&1; then
            echo_success "  $pkg"
        else
            echo_error "  $pkg (not installed)"
        fi
    done
    echo ""

    # Arcade SDKs
    echo "Arcade SDKs (in $SDK_DIR/Sdks/):"
    if [ -d "$SDK_DIR/Sdks" ]; then
        for pkg in "${!ARCADE_PACKAGES[@]}"; do
            if [ -d "$SDK_DIR/Sdks/$pkg/Sdk" ]; then
                echo_success "  $pkg"
            else
                echo_error "  $pkg (not installed)"
            fi
        done
    else
        echo_error "  SDK directory not found"
    fi
    echo ""

    # Local NuGet feed
    echo "Local NuGet Feed ($LOCAL_FEED):"
    if [ -d "$LOCAL_FEED" ]; then
        local pkg_count=$(ls -1 "$LOCAL_FEED"/*.nupkg 2>/dev/null | wc -l)
        echo_success "  $pkg_count packages available"
    else
        echo_error "  Not set up"
    fi
    echo ""

    # Build capability
    echo "Build Capability:"
    if [ -f "$RUNTIME_DIR/build.sh" ]; then
        echo_success "  Native CLR build (./build-runtime.sh -component runtime)"
    else
        echo_error "  Runtime source not found"
    fi
    echo ""
}

# Main
main() {
    echo ""
    echo "========================================"
    echo "DOTNExT Build Environment Setup"
    echo "========================================"
    echo ""

    check_platform

    case "${1:-}" in
        --status)
            print_status
            exit 0
            ;;
        --quick)
            # Quick mode: skip slow operations if already done
            install_system_deps
            setup_local_nuget_feed
            setup_environment
            print_status
            ;;
        *)
            # Full setup
            install_system_deps
            download_arcade_packages
            install_arcade_sdks
            setup_local_nuget_feed
            setup_environment
            print_status
            ;;
    esac

    echo ""
    echo "======================================"
    echo "Setup complete!"
    echo ""
    echo "To build native CLR:"
    echo "  cd $RUNTIME_DIR/src/coreclr"
    echo "  ./build-runtime.sh -component runtime -c Debug"
    echo ""
    echo "Output will be at:"
    echo "  $RUNTIME_DIR/artifacts/bin/coreclr/linux.x64.Debug/"
    echo "======================================"
}

main "$@"
