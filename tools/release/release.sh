#!/usr/bin/env bash
set -euo pipefail

version="${1:?usage: release.sh <version>}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$root/artifacts/release/$version"
set -a
source "$root/secrets/keystore.env"
source "$root/secrets/cloudflare.env"
set +a

IFS=. read -r major minor patch <<< "$version"
code=$((major * 10000 + minor * 100 + patch))
rm -rf "$out" && mkdir -p "$out"

dotnet run --project "$root/tools/TerraNet.PackTool" -c Release -- android-arm64
dotnet run --project "$root/tools/TerraNet.PackTool" -c Release -- win-x64

dotnet publish "$root/src/TerraNet.Android" -c Release -o "$out/android" \
  -p:Version="$version" -p:ApplicationDisplayVersion="$version" -p:ApplicationVersion="$code" \
  -p:AndroidKeyStore=true -p:AndroidSigningKeyStore="$root/secrets/terranet.keystore" \
  -p:AndroidSigningKeyAlias=terranet \
  -p:AndroidSigningKeyPass="env:TERRANET_KEYSTORE_PASSWORD" -p:AndroidSigningStorePass="env:TERRANET_KEYSTORE_PASSWORD"
cp "$out/android/win.hacks.terranet-Signed.apk" "$out/TerraNet-$version.apk"

dotnet publish "$root/src/TerraNet.Desktop" -c Release -o "$out/desktop" -p:Version="$version"
cp "$out/desktop/TerraNet.exe" "$out/TerraNet.exe"

python "$root/tools/release/publish.py" "$version" "$out"
