#!/usr/bin/env bash
# Publishes the version in EquipmentExperience.csproj to Thunderstore, then tags it as v<version> and pushes the tag.
# See "Releasing" in DEVELOPMENT.md.
set -euo pipefail

cd "$(dirname "$0")/.."
export PATH="$PATH:$HOME/.dotnet"

die() { echo "release: $*" >&2; exit 1; }

version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' EquipmentExperience.csproj)
tag="v$version"
zip="dist/EquipmentExperience-$version.zip"
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "<Version> '$version' isn't Major.Minor.Patch"

[[ $(git branch --show-current) == main ]] || die "not on main"
[[ -z $(git status --porcelain) ]] || die "working tree has uncommitted changes"
git fetch --quiet --tags origin
[[ $(git rev-parse HEAD) == $(git rev-parse origin/main) ]] || die "main isn't in sync with origin/main"
git rev-parse --quiet --verify "refs/tags/$tag" >/dev/null && die "tag $tag already exists; bump <Version> first"
grep -q "^## $version\$" CHANGELOG.md || die "CHANGELOG.md has no '## $version' entry"
status=$(curl -s -o /dev/null -w '%{http_code}' \
  "https://thunderstore.io/api/experimental/package/Nerdtastic/EquipmentExperience/$version/")
[[ $status == 404 ]] || die "Thunderstore already has $version (HTTP $status)"

token=${TCLI_AUTH_TOKEN:-$(security find-generic-password -s thunderstore-token -w 2>/dev/null || true)}
[[ -n $token ]] || die "no Thunderstore token; see DEVELOPMENT.md"

dotnet run --project tools/GameApiCheck || die "game API check failed"
dotnet build -c Release -p:Package=true
dotnet tool restore >/dev/null

read -r -p "Publish $zip to Thunderstore as $tag? [y/N] " answer
[[ $answer == [yY] ]] || die "cancelled"

TCLI_AUTH_TOKEN=$token dotnet tcli publish --file "$zip"
git tag -a "$tag" -m "EquipmentExperience $version"
git push origin "$tag"
echo "Released $tag"
