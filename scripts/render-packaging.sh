#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "usage: render-packaging.sh <version> <dist-directory> <output-directory>" >&2
  exit 2
fi

version="$1"
dist_directory="$2"
output_directory="$3"
checksums="$dist_directory/SHA256SUMS"

checksum() {
  awk -v file="$1" '$2 == file { print $1 }' "$checksums"
}

mkdir -p "$output_directory/homebrew" "$output_directory/scoop"

sed \
  -e "s/@@VERSION@@/$version/g" \
  -e "s/@@OSX_ARM64_SHA@@/$(checksum "harnfig-v$version-osx-arm64.tar.gz")/g" \
  -e "s/@@OSX_X64_SHA@@/$(checksum "harnfig-v$version-osx-x64.tar.gz")/g" \
  -e "s/@@LINUX_ARM64_SHA@@/$(checksum "harnfig-v$version-linux-arm64.tar.gz")/g" \
  -e "s/@@LINUX_X64_SHA@@/$(checksum "harnfig-v$version-linux-x64.tar.gz")/g" \
  packaging/homebrew/harnfig.rb.template > "$output_directory/homebrew/harnfig.rb"

sed \
  -e "s/@@VERSION@@/$version/g" \
  -e "s/@@WIN_ARM64_SHA@@/$(checksum "harnfig-v$version-win-arm64.zip")/g" \
  -e "s/@@WIN_X64_SHA@@/$(checksum "harnfig-v$version-win-x64.zip")/g" \
  packaging/scoop/harnfig.json.template > "$output_directory/scoop/harnfig.json"
