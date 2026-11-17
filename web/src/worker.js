const PLATFORMS = ["android", "windows"];
const SPANISH = new Set(["ES", "MX", "AR", "CO", "CL", "PE", "VE", "EC", "GT", "CU", "BO", "DO", "HN", "PY", "SV", "NI", "CR", "PA", "UY", "PR", "GQ"]);
const FILENAMES = { android: "TerraNet.apk", windows: "TerraNet.exe" };
const CORS = { "access-control-allow-origin": "*" };

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const path = url.pathname;

    if (path === "/api/latest")
      return latest(env, url.searchParams.get("platform"), url.origin);
    if (path.startsWith("/download/"))
      return download(env, path.slice("/download/".length));
    if (path === "/" || path === "/index.html")
      return page(request, env);
    return env.ASSETS.fetch(request);
  },
};

async function release(env, platform) {
  if (!PLATFORMS.includes(platform)) return null;
  return env.RELEASES.get(`latest:${platform}`, "json");
}

async function latest(env, platform, origin) {
  const info = await release(env, platform);
  if (!info) return json({ error: "unknown platform" }, 404);
  return json({
    version: info.version,
    notes: info.notes,
    url: `${origin}/download/${platform}`,
    size: info.size,
    sha256: info.sha256,
    published: info.published,
  });
}

async function download(env, platform) {
  const info = await release(env, platform);
  if (!info) return new Response("Not found", { status: 404 });
  return proxy(info.asset, FILENAMES[platform]);
}

async function proxy(asset, filename) {
  const upstream = await fetch(asset, { redirect: "follow", cf: { cacheEverything: true, cacheTtl: 86400 } });
  if (!upstream.ok) return new Response("Unavailable", { status: 502 });
  const headers = new Headers({
    "content-type": upstream.headers.get("content-type") ?? "application/octet-stream",
    "content-disposition": `attachment; filename="${filename}"`,
    "cache-control": "public, max-age=300",
  });
  const length = upstream.headers.get("content-length");
  if (length) headers.set("content-length", length);
  return new Response(upstream.body, { headers });
}

async function page(request, env) {
  const asset = await env.ASSETS.fetch(new URL("/index.html", request.url));
  const country = request.cf?.country ?? "";
  const accept = request.headers.get("accept-language") ?? "";
  const lang = SPANISH.has(country) || (!country && accept.toLowerCase().startsWith("es")) ? "es" : "en";
  const [android, windows] = await Promise.all(PLATFORMS.map((p) => release(env, p)));
  const data = JSON.stringify({
    lang,
    android: android && { version: android.version, size: android.size },
    windows: windows && { version: windows.version, size: windows.size },
  });
  const html = (await asset.text())
    .replace('lang="en"', `lang="${lang}"`)
    .replace("__TERRANET__", data.replace(/</g, "\\u003c"));
  return new Response(html, { headers: { "content-type": "text/html; charset=utf-8", "cache-control": "public, max-age=60" } });
}

function json(body, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json", ...CORS } });
}
