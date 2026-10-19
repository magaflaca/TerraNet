#!/usr/bin/env bash
set -euo pipefail

host="${TERRANET_VPS:-root@169.58.128.75}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$root/artifacts/relay"

dotnet publish "$root/src/TerraNet.Relay" -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o "$out"

ssh "$host" 'id terranet >/dev/null 2>&1 || useradd --system --home /var/lib/terranet --shell /usr/sbin/nologin terranet; mkdir -p /opt/terranet/relay /etc/terranet /var/lib/terranet; chown terranet:terranet /var/lib/terranet'
scp "$out/TerraNet.Relay" "$host:/opt/terranet/relay/TerraNet.Relay.new"
scp "$root/deploy/relay/relay.json" "$host:/etc/terranet/relay.json"
scp "$root/deploy/relay/terranet-relay.service" "$host:/etc/systemd/system/terranet-relay.service"
if [ -f "$root/secrets/relay.pfx" ]; then
  scp "$root/secrets/relay.pfx" "$host:/etc/terranet/relay.pfx"
fi
ssh "$host" 'chown root:terranet /etc/terranet/relay.pfx && chmod 640 /etc/terranet/relay.pfx && mv /opt/terranet/relay/TerraNet.Relay.new /opt/terranet/relay/TerraNet.Relay && chmod 755 /opt/terranet/relay/TerraNet.Relay && systemctl daemon-reload && systemctl enable --now terranet-relay && systemctl restart terranet-relay && sleep 1 && systemctl --no-pager --lines=5 status terranet-relay'
