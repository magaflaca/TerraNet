import hashlib
import json
import os
import subprocess
import sys
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

REPO = "magaflaca/terranet"
API = "https://api.github.com"
UPLOADS = "https://uploads.github.com"


def github_token():
    result = subprocess.run(["git", "credential", "fill"], input="protocol=https\nhost=github.com\n\n",
                            capture_output=True, text=True, check=True)
    return dict(line.split("=", 1) for line in result.stdout.splitlines() if "=" in line)["password"]


def request(method, url, token=None, body=None, headers=None, data=None):
    all_headers = {"Accept": "application/vnd.github+json", "User-Agent": "terranet-release", **(headers or {})}
    if token:
        all_headers["Authorization"] = f"Bearer {token}"
    if body is not None:
        data = json.dumps(body).encode()
        all_headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, method=method, headers=all_headers)
    try:
        with urllib.request.urlopen(req) as response:
            text = response.read().decode()
            return json.loads(text) if text else {}
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return None
        raise RuntimeError(f"{method} {url}: {error.code} {error.read().decode()}") from error


def release(token, version):
    tag = f"v{version}"
    existing = request("GET", f"{API}/repos/{REPO}/releases/tags/{tag}", token)
    if existing:
        return existing
    return request("POST", f"{API}/repos/{REPO}/releases", token, {"tag_name": tag, "name": f"TerraNet {version}", "target_commitish": "main"})


def upload(token, rel, path, name=None):
    name = name or path.name
    for asset in rel.get("assets", []):
        if asset["name"] == name:
            request("DELETE", f"{API}/repos/{REPO}/releases/assets/{asset['id']}", token)
    url = f"{UPLOADS}/repos/{REPO}/releases/{rel['id']}/assets?name={urllib.request.quote(name)}"
    result = request("POST", url, token, data=path.read_bytes(), headers={"Content-Type": "application/octet-stream"})
    print("uploaded", name)
    return result["browser_download_url"]


def kv_put(key, value):
    account, namespace = os.environ["CLOUDFLARE_ACCOUNT_ID"], os.environ["TERRANET_KV_ID"]
    url = f"https://api.cloudflare.com/client/v4/accounts/{account}/storage/kv/namespaces/{namespace}/values/{urllib.request.quote(key, safe='')}"
    req = urllib.request.Request(url, data=value.encode(), method="PUT", headers={
        "X-Auth-Email": os.environ["CLOUDFLARE_EMAIL"],
        "X-Auth-Key": os.environ["CLOUDFLARE_API_KEY"],
        "Content-Type": "text/plain",
    })
    with urllib.request.urlopen(req) as response:
        assert json.loads(response.read())["success"]


def kv_get(key):
    account, namespace = os.environ["CLOUDFLARE_ACCOUNT_ID"], os.environ["TERRANET_KV_ID"]
    url = f"https://api.cloudflare.com/client/v4/accounts/{account}/storage/kv/namespaces/{namespace}/values/{urllib.request.quote(key, safe='')}"
    req = urllib.request.Request(url, headers={"X-Auth-Email": os.environ["CLOUDFLARE_EMAIL"], "X-Auth-Key": os.environ["CLOUDFLARE_API_KEY"]})
    try:
        with urllib.request.urlopen(req) as response:
            return response.read().decode()
    except urllib.error.HTTPError:
        return None


def manifest(version, notes, asset_url, path):
    return json.dumps({
        "version": version,
        "notes": notes,
        "asset": asset_url,
        "size": path.stat().st_size,
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "published": datetime.now(timezone.utc).isoformat(),
    })


def main():
    version, out = sys.argv[1], Path(sys.argv[2])
    root = Path(__file__).resolve().parents[2]
    notes = json.loads((root / "releases" / "notes.json").read_text(encoding="utf-8")).get(version, {"en": [], "es": []})
    token = github_token()
    rel = release(token, version)

    apk = out / f"TerraNet-{version}.apk"
    apk_url = upload(token, rel, apk)
    kv_put("latest:android", manifest(version, notes, apk_url, apk))

    velopack = out / "velopack"
    setup = next(velopack.glob("*Setup.exe"))
    setup_url = upload(token, rel, setup, "TerraNet-Setup.exe")
    kv_put("latest:windows", manifest(version, notes, setup_url, setup))

    assets = json.loads(kv_get("feed:assets") or "{}")
    for package in velopack.glob("*.nupkg"):
        assets[package.name] = upload(token, rel, package)
    feed = next(velopack.glob("releases.*.json"))
    upload(token, rel, feed)
    kv_put("feed:assets", json.dumps(assets))
    kv_put("feed:windows", feed.read_text(encoding="utf-8"))
    print("published", version)


if __name__ == "__main__":
    main()
