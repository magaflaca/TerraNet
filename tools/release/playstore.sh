#!/usr/bin/env bash
set -euo pipefail

version="${1:?usage: playstore.sh <version>}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$root/artifacts/playstore/$version"
set -a
source "$root/secrets/keystore.env"
set +a

IFS=. read -r major minor patch <<< "$version"
code=$((major * 10000 + minor * 100 + patch))
rm -rf "$out" && mkdir -p "$out"

if [ "${REUSE_PACKS:-0}" != "1" ]; then
  dotnet run --project "$root/tools/TerraNet.PackTool" -c Release -- android-arm64
fi

dotnet publish "$root/src/TerraNet.Android" -c Release -o "$out/android" \
  -p:AndroidPackageFormat=aab \
  -p:Version="$version" -p:ApplicationDisplayVersion="$version" -p:ApplicationVersion="$code" \
  -p:AndroidKeyStore=true -p:AndroidSigningKeyStore="$root/secrets/terranet.keystore" \
  -p:AndroidSigningKeyAlias=terranet \
  -p:AndroidSigningKeyPass="env:TERRANET_KEYSTORE_PASSWORD" -p:AndroidSigningStorePass="env:TERRANET_KEYSTORE_PASSWORD"
cp "$out/android/win.hacks.terranet-Signed.aab" "$out/TerraNet-$version.aab"
echo "$out/TerraNet-$version.aab"
