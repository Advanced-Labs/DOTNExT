#!/bin/bash
# Download Arcade SDK and dependencies for offline VMR builds
# Bypasses proxy restrictions by using wget directly

set -e

LOCAL_SDK_CACHE="/tmp/arcade-sdk-cache"
mkdir -p "$LOCAL_SDK_CACHE"

# Download function for Azure DevOps packages
download_ado_pkg() {
    local feed="$1"
    local name="$2"
    local version="$3"
    local name_lower=$(echo "$name" | tr '[:upper:]' '[:lower:]')
    local nupkg_file="$LOCAL_SDK_CACHE/$name_lower.$version.nupkg"

    if [ -f "$nupkg_file" ]; then
        echo "Already cached: $name $version"
        return 0
    fi

    echo "Downloading $name $version from $feed..."
    local url="https://pkgs.dev.azure.com/dnceng/public/_packaging/$feed/nuget/v3/flat2/$name_lower/$version/$name_lower.$version.nupkg"

    wget -q -O "$nupkg_file" "$url" || {
        echo "Failed to download $name $version"
        return 1
    }

    echo "Downloaded: $name $version"
}

# Download function for nuget.org packages
download_nuget_pkg() {
    local name="$1"
    local version="$2"
    local name_lower=$(echo "$name" | tr '[:upper:]' '[:lower:]')
    local nupkg_file="$LOCAL_SDK_CACHE/$name_lower.$version.nupkg"

    if [ -f "$nupkg_file" ]; then
        echo "Already cached: $name $version"
        return 0
    fi

    echo "Downloading $name $version from nuget.org..."
    local url="https://api.nuget.org/v3-flatcontainer/$name_lower/$version/$name_lower.$version.nupkg"

    wget -q -O "$nupkg_file" "$url" || {
        echo "Failed to download $name $version"
        return 1
    }

    echo "Downloaded: $name $version"
}

echo "=== Downloading Arcade SDK Packages ==="
echo ""

# From global.json - msbuild-sdks section
# Using dotnet-eng feed for Microsoft.DotNet.* packages
download_ado_pkg "dotnet-eng" "Microsoft.DotNet.Arcade.Sdk" "9.0.0-beta.25515.2"
download_ado_pkg "dotnet-eng" "Microsoft.DotNet.Helix.Sdk" "9.0.0-beta.25515.2"
download_ado_pkg "dotnet-eng" "Microsoft.DotNet.SharedFramework.Sdk" "9.0.0-beta.25515.2"

# These are typically on nuget.org
download_nuget_pkg "Microsoft.Build.NoTargets" "3.7.0"
download_nuget_pkg "Microsoft.Build.Traversal" "3.4.0"

# Microsoft.NET.Sdk.IL might be on dotnet9 feed
download_ado_pkg "dotnet9" "Microsoft.NET.Sdk.IL" "9.0.0-rtm.24511.16"

echo ""
echo "=== Downloads Complete ==="
echo "Packages saved to: $LOCAL_SDK_CACHE"
echo ""

# Now extract to MSBuild SDK location
MSBUILD_SDK_DIR="$HOME/.dotnet/sdk/9.0.111/Sdks"
echo "=== Installing SDKs to: $MSBUILD_SDK_DIR ==="

install_sdk() {
    local name="$1"
    local version="$2"
    local name_lower=$(echo "$name" | tr '[:upper:]' '[:lower:]')
    local nupkg_file="$LOCAL_SDK_CACHE/$name_lower.$version.nupkg"
    local sdk_dir="$MSBUILD_SDK_DIR/$name"

    if [ -d "$sdk_dir" ]; then
        echo "Already installed: $name"
        return 0
    fi

    echo "Installing $name..."
    mkdir -p "$sdk_dir"
    unzip -q -o "$nupkg_file" -d "$sdk_dir" 2>/dev/null || true
    echo "Installed: $name"
}

# Install the SDKs
install_sdk "Microsoft.DotNet.Arcade.Sdk" "9.0.0-beta.25515.2"
install_sdk "Microsoft.DotNet.Helix.Sdk" "9.0.0-beta.25515.2"
install_sdk "Microsoft.DotNet.SharedFramework.Sdk" "9.0.0-beta.25515.2"
install_sdk "Microsoft.Build.NoTargets" "3.7.0"
install_sdk "Microsoft.Build.Traversal" "3.4.0"
install_sdk "Microsoft.NET.Sdk.IL" "9.0.0-rtm.24511.16"

echo ""
echo "=== SDK Installation Complete ==="
echo ""
echo "You can now try building with:"
echo "  cd /home/user/DOTNExT/src/runtime"
echo "  ./build.sh clr -c Debug"
