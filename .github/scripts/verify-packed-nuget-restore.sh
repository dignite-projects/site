#!/usr/bin/env bash
# Restores every NuGet package this workflow just packed, in an isolated scratch project pointed
# at the same sources a real consumer would use: this run's own artifacts/ folder, GitHub
# Packages (where cross-repo abp-modules dependencies live pre-release), and nuget.org. Catches an
# unresolvable transitive dependency - e.g. a cross-repo ProjectReference pinned to an abp-modules
# version that was never published, the exact NU1102 failure documented in CHANGELOG.md's
# 0.1.0-preview.3 entry - in this workflow, instead of a downstream consumer's restore.
set -euo pipefail
shopt -s nullglob

artifacts_dir=${1:?Usage: verify-packed-nuget-restore.sh <artifacts-dir> <version> <github-actor> <github-token>}
version=${2:?Usage: verify-packed-nuget-restore.sh <artifacts-dir> <version> <github-actor> <github-token>}
github_actor=${3:?Usage: verify-packed-nuget-restore.sh <artifacts-dir> <version> <github-actor> <github-token>}
github_token=${4:?Usage: verify-packed-nuget-restore.sh <artifacts-dir> <version> <github-actor> <github-token>}

artifacts_abs=$(cd "$artifacts_dir" && pwd)

package_ids=()
for nupkg in "$artifacts_abs"/*.nupkg; do
  filename=$(basename "$nupkg")
  package_ids+=("${filename%.$version.nupkg}")
done

if [ ${#package_ids[@]} -eq 0 ]; then
  echo "::error::No .nupkg files found in $artifacts_abs - nothing to verify."
  exit 1
fi

workdir=$(mktemp -d)
trap 'rm -rf "$workdir"' EXIT
export NUGET_PACKAGES="$workdir/.nuget-packages"

{
  echo '<Project Sdk="Microsoft.NET.Sdk">'
  echo '  <PropertyGroup>'
  echo '    <TargetFramework>net10.0</TargetFramework>'
  echo '  </PropertyGroup>'
  echo '  <ItemGroup>'
  for id in "${package_ids[@]}"; do
    echo "    <PackageReference Include=\"$id\" Version=\"$version\" />"
  done
  echo '  </ItemGroup>'
  echo '</Project>'
} > "$workdir/RestoreSmoke.csproj"

cat > "$workdir/NuGet.Config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="packed" value="$artifacts_abs" />
    <add key="github" value="https://nuget.pkg.github.com/dignite-projects/index.json" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="$github_actor" />
      <add key="ClearTextPassword" value="$github_token" />
    </github>
  </packageSourceCredentials>
</configuration>
EOF

echo "Restoring ${#package_ids[@]} packed packages at $version against packed + GitHub Packages + nuget.org..."
if ! dotnet restore "$workdir/RestoreSmoke.csproj" --configfile "$workdir/NuGet.Config"; then
  echo "::error::Restoring the packed packages failed - at least one dependency (likely a cross-repo abp-modules version) doesn't resolve on any configured source. Do not push these packages."
  exit 1
fi

echo "All ${#package_ids[@]} packed packages restore cleanly."
