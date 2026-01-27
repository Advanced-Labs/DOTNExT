#!/bin/bash
# Setup local NuGet feed for offline builds
# This allows managed code (C#) to build without network access to Azure DevOps feeds

set -e

LOCAL_FEED="/tmp/nuget-feed"
ARCADE_CACHE="/tmp/arcade-sdk-cache"

echo "=== Setting up Local NuGet Feed ==="
echo ""

# Create feed directory
mkdir -p "$LOCAL_FEED"

# Copy Arcade SDK packages if available
if [ -d "$ARCADE_CACHE" ]; then
    echo "Copying Arcade SDK packages..."
    cp -v "$ARCADE_CACHE"/*.nupkg "$LOCAL_FEED/" 2>/dev/null || true
fi

# List packages in local feed
echo ""
echo "=== Packages in Local Feed ==="
ls -la "$LOCAL_FEED"/*.nupkg 2>/dev/null || echo "No packages found"

# Create a minimal NuGet.config that uses local feed
cat > "$LOCAL_FEED/NuGet.config" << 'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <!-- Local cache for offline builds -->
    <add key="local-cache" value="/tmp/nuget-feed" />
    <!-- Fallback to nuget.org for packages not in local cache -->
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <fallbackPackageFolders>
    <clear />
  </fallbackPackageFolders>
</configuration>
EOF

echo ""
echo "=== Created NuGet.config at $LOCAL_FEED/NuGet.config ==="
cat "$LOCAL_FEED/NuGet.config"

echo ""
echo "=== To use this NuGet config, run builds with: ==="
echo "  dotnet restore --configfile $LOCAL_FEED/NuGet.config"
echo ""
echo "Or copy this NuGet.config to the project directory."
