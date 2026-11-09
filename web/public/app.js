const DATA = window.TERRANET && typeof window.TERRANET === "object" ? window.TERRANET : {};
const LANG = DATA.lang || ((navigator.language || "en").startsWith("es") ? "es" : "en");

const TEXT = {
  en: {
    tagline: "Host Terraria worlds from your phone or PC and play with friends anywhere.",
    download: "Download",
    join: "Friends join at",
    soon: "Coming soon",
  },
  es: {
    tagline: "Abre mundos de Terraria desde tu móvil o PC y juega con tus amigos donde sea.",
    download: "Descargar",
    join: "Tus amigos entran en",
    soon: "Muy pronto",
  },
};

const t = (key) => (TEXT[LANG] || TEXT.en)[key] || TEXT.en[key];

const night = () =>
  document.documentElement.dataset.theme === "dark" ||
  (document.documentElement.dataset.theme !== "light" && matchMedia("(prefers-color-scheme: dark)").matches);

let atlas = null;

async function loadAtlas() {
  const response = await fetch("/sprites.json");
  const json = await response.json();
  const palette = json.palette;
  const sprites = {};
  for (const group of ["sprites", "glyphs"])
    Object.assign(sprites, json[group]);
  return { palette, sprites };
}

function drawSprite(ctx, name, x, y, unit, options = {}) {
  const rows = atlas.sprites[name];
  if (!rows) return;
  const flip = options.flip;
  const width = rows[0].length;
  ctx.globalAlpha = options.alpha ?? 1;
  for (let row = 0; row < rows.length; row++) {
    const line = rows[row];
    for (let col = 0; col < line.length; col++) {
      const key = line[col];
      if (key === ".") continue;
      const color = key === "#" ? options.tint || "#3B2F4A" : atlas.palette[key];
      if (!color) continue;
      ctx.fillStyle = color;
      const px = flip ? x + (width - 1 - col) * unit : x + col * unit;
      ctx.fillRect(Math.round(px), Math.round(y + row * unit), Math.ceil(unit), Math.ceil(unit));
    }
  }
  ctx.globalAlpha = 1;
}

function spriteToDataUrl(name, unit, tint) {
  const rows = atlas.sprites[name];
  const canvas = document.createElement("canvas");
  canvas.width = rows[0].length * unit;
  canvas.height = rows.length * unit;
  drawSprite(canvas.getContext("2d"), name, 0, 0, unit, { tint });
  return canvas.toDataURL("image/png");
}

const hash = (seed) => {
  const x = Math.sin(seed * 12.9898 + 78.233) * 43758.5453;
  return x - Math.floor(x);
};

const mix = (a, b, f) => {
  const pa = [1, 3, 5].map((i) => parseInt(a.slice(i, i + 2), 16));
  const pb = [1, 3, 5].map((i) => parseInt(b.slice(i, i + 2), 16));
  return `rgb(${pa.map((v, i) => Math.round(v + (pb[i] - v) * f)).join(",")})`;
};

const PALETTE = {
  day: { top: "#86CBFF", bottom: "#E4F6FF", mountain: "#B7D3F4", rim: "#D3E5FA", snow: "#FFFFFF", hill: "#A6E3A0", hillRim: "#C6F0BC", dirt: "#C08A5E", deep: "#A3714C", stone: "#8F86A3" },
  night: { top: "#141130", bottom: "#4A3F86", mountain: "#2D2860", rim: "#3E377A", snow: "#8E86C7", hill: "#25514C", hillRim: "#2F6A61", dirt: "#69503F", deep: "#59422F", stone: "#4E4866" },
};

function scene() {
  const canvas = document.getElementById("scene");
  const ctx = canvas.getContext("2d");
  let width = 0;
  let height = 0;
  let unit = 3;
  let backdrop = null;
  let backdropKey = "";
  let celestial = { x: 0, y: 0, w: 0, h: 0 };
  let layout = null;

  const resize = () => {
    const ratio = Math.min(window.devicePixelRatio || 1, 2);
    width = Math.ceil(window.innerWidth * ratio);
    height = Math.ceil(window.innerHeight * ratio);
    canvas.width = width;
    canvas.height = height;
    unit = Math.max(3, Math.round(Math.min(width, height) / 150));
    layout = null;
  };

  const snap = (v) => Math.round(v / unit) * unit;

  const contentBox = () => {
    const ratio = width / window.innerWidth;
    const rects = [...document.querySelectorAll(".sign, .address")].map((el) => el.getBoundingClientRect());
    if (!rects.length) return null;
    return {
      left: Math.min(...rects.map((r) => r.left)) * ratio,
      right: Math.max(...rects.map((r) => r.right)) * ratio,
      bottom: Math.max(...rects.map((r) => r.bottom)) * ratio,
    };
  };

  const computeLayout = () => {
    const u = unit;
    const landscape = width > height;
    const box = contentBox();
    let h = snap(height * (landscape ? 0.8 : 0.78));
    if (!landscape && box) h = snap(Math.min(Math.max(height - 60 * u, box.bottom + 32 * u), height - 26 * u));
    const ground = h + u * 2;
    const blocked = !!box && box.bottom > ground - 30 * u && box.right - box.left < width - 60 * u;
    const center = snap(width * 0.5);
    return {
      h,
      ground,
      blocked,
      center,
      left: blocked ? snap(box.left - 6 * u) : center - 12 * u,
      right: blocked ? snap(box.right + 6 * u) : center + 12 * u,
    };
  };

  const current = () => (layout ??= computeLayout());
  const horizon = () => current().h;
  const slimeX = () => (current().blocked ? current().right : current().center - 8 * unit);

  const buildBackdrop = () => {
    const key = `${width}x${height}:${night()}`;
    if (backdrop && key === backdropKey) return backdrop;
    const off = document.createElement("canvas");
    off.width = width;
    off.height = height;
    const g = off.getContext("2d");
    const p = night() ? PALETTE.night : PALETTE.day;
    const h = horizon();
    const u = unit;

    const bands = 8;
    const band = Math.ceil(h / bands / u) * u;
    for (let i = 0; i < bands; i++) {
      g.fillStyle = mix(p.top, p.bottom, i / (bands - 1));
      g.fillRect(0, i * band, width, band + 1);
    }

    const amp = Math.min(h * 0.4, 70 * u);
    for (let x = 0; x < width; x += u) {
      const n = Math.sin(x * 0.0061 / (u / 3) + 1.3) * 0.55 + Math.sin(x * 0.0173 / (u / 3) + 0.4) * 0.3;
      const mh = Math.round((amp * (0.55 + 0.45 * n)) / u) * u;
      g.fillStyle = p.mountain;
      g.fillRect(x, h - mh, u, mh);
      g.fillStyle = p.rim;
      g.fillRect(x, h - mh, u, u);
      if (mh > amp * 0.86) {
        g.fillStyle = p.snow;
        g.fillRect(x, h - mh, u, u * 3);
      }
      const hn = Math.sin(x * 0.0093 / (u / 3) + 2.1) * 0.6 + Math.sin(x * 0.023 / (u / 3) + 0.7) * 0.4;
      const hh = Math.round((Math.min(h * 0.2, 36 * u) * (0.5 + 0.5 * hn)) / u) * u + u * 2;
      g.fillStyle = p.hill;
      g.fillRect(x, h - hh, u, hh);
      g.fillStyle = p.hillRim;
      g.fillRect(x, h - hh, u, u);
    }

    const tile = night() ? "grassNight" : "grass";
    for (let x = 0; x < width; x += 16 * u) drawSprite(g, tile, x, h, u);
    const dirtTop = h + 16 * u;
    const depth = Math.max(height - dirtTop, u * 6);
    const layers = [[p.dirt, 0], [p.deep, 0.62], [p.stone, 0.88]];
    layers.forEach(([color, at], i) => {
      const top = dirtTop + snap(depth * at);
      for (let x = 0; x < width; x += u) {
        const jag = i === 0 ? 0 : Math.round(Math.sin(x * 0.05 + i) * 1.5 + hash(x / u + i * 977) * 2) * u;
        g.fillStyle = color;
        g.fillRect(x, top + jag, u, height - top - jag + 1);
      }
    });
    const gems = ["#FFD56B", "#7CC4F7", "#FF8AAE", "#B98BFF"];
    const count = Math.max(6, Math.floor((width * depth) / (u * u * 420)));
    for (let i = 0; i < count; i++) {
      const x = Math.floor((hash(i * 31 + 5) * width) / u) * u;
      const y = h + u * 8 + Math.floor((hash(i * 17 + 9) * Math.max(depth * 0.62 + u * 3, u)) / u) * u;
      g.fillStyle = gems[i % gems.length];
      g.fillRect(x, y, u * 3, u);
      g.fillRect(x + u, y - u, u, u * 3);
      g.fillStyle = "#fff";
      g.fillRect(x + u, y - u, u, u);
    }

    const ground = h + u * 2;
    const place = (name, x) => {
      const rows = atlas.sprites[name];
      if (rows) drawSprite(g, name, Math.round(x / u) * u, ground - rows.length * u + u, u);
    };
    const lit = night();
    const row = (items, start, direction) => {
      let x = start;
      for (const [name, gap] of items) {
        const w = (name ? atlas.sprites[name][0].length : 0) * u;
        if (direction < 0) x -= w;
        if (name && x > -w && x < width) place(name, x);
        x = direction > 0 ? x + w + gap * u : x - gap * u;
      }
    };
    const { left, right, blocked } = current();
    for (let i = 0; i < 18; i++) place(hash(i * 3) > 0.5 ? "flower" : "tuft", hash(i * 7 + 1) * width);
    row([[lit ? "lampLit" : "lamp", 3], [lit ? "houseLit" : "house", 3], ["bush", 2], ["treeBig", 4], ["flower", 6], ["tree", 3], ["bush", 5], ["treeBig", 3], ["mushroom", 4], ["tree", 3]], left, -1);
    row([[null, blocked ? 22 : 0], ["mushroom", 3], ["tree", 3], ["bush", 4], ["treeBig", 4], ["tuft", 6], ["tree", 3], ["bush", 5], ["treeBig", 3], ["mushroom", 4]], right, 1);

    backdrop = off;
    backdropKey = key;
    return off;
  };

  const clouds = [
    [0.05, 0.1, 0.008, "cloudSmall"], [0.4, 0.18, 0.006, "cloud"], [0.7, 0.08, 0.01, "cloudSmall"], [0.85, 0.3, 0.012, "cloud"], [0.22, 0.34, 0.007, "cloudSmall"],
  ];

  const frame = (time) => {
    const t = time / 1000;
    const isNight = night();
    const h = horizon();
    const u = unit;
    ctx.drawImage(buildBackdrop(), 0, 0);

    if (isNight)
      for (let i = 0; i < 48; i++) {
        const glow = 0.3 + 0.7 * Math.max(0, Math.sin(t * 1.2 + hash(i * 3 + 3) * 6.28));
        drawSprite(ctx, "twinkle", Math.round((hash(i * 3 + 1) * width) / u) * u, Math.round((hash(i * 3 + 2) * h * 0.55) / u) * u, u, { alpha: glow });
      }

    const body = isNight ? "moon" : "sun";
    const bodyX = Math.round((width * 0.8) / u) * u;
    const bodyY = Math.round((h * 0.1 + Math.sin(t * 0.5) * u * 1.5) / u) * u;
    celestial = { x: bodyX - u * 4, y: bodyY - u * 4, w: (atlas.sprites[body][0].length + 8) * u, h: (atlas.sprites[body].length + 8) * u };
    drawSprite(ctx, body, bodyX, bodyY, u);

    for (const [x, y, speed, name] of clouds) {
      const span = width + 30 * u;
      const px = (((x + t * speed) % 1) * span) - 26 * u;
      drawSprite(ctx, name, Math.round(px / u) * u, Math.round((y * h) / u) * u, u, { alpha: isNight ? 0.25 : 0.95 });
    }

    const islandX = Math.round((width * 0.12) / u) * u;
    const islandY = Math.round((h * 0.42 + Math.sin(t * 0.7) * u * 2) / u) * u;
    if (width > 700) {
      for (let row = 0; row < 8; row++) {
        const inset = row < 2 ? 0 : (row - 1) * 2;
        ctx.fillStyle = row === 0 ? (isNight ? "#8AE8D0" : "#9BE58A") : row === 1 ? (isNight ? "#45BFA0" : "#5FC970") : (isNight ? "#69503F" : "#C08A5E");
        ctx.fillRect(islandX + inset * u, islandY + row * u, (22 - inset * 2) * u, u);
      }
      drawSprite(ctx, "tree", islandX + 4 * u, islandY - 11 * u, u);
      drawSprite(ctx, "chest", islandX + 12 * u, islandY - 8 * u, u);
    }

    if (!isNight)
      for (let i = 0; i < 3; i++) {
        const cycle = (t * 0.03 + i * 0.37) % 1;
        drawSprite(ctx, Math.floor(t * 5 + i) % 2 ? "bird" : "birdUp", Math.round((cycle * (width + 200) - 100) / u) * u, Math.round((h * (0.2 + i * 0.07) + Math.sin(t * 1.5 + i) * u * 3) / u) * u, u, { alpha: 0.75 });
      }

    const { ground } = current();
    const sx = slimeX();
    const period = 2.4;
    const phase = (t % period) / period;
    const lift = phase < 0.35 ? Math.sin((phase / 0.35) * Math.PI) * 5 : 0;
    const slime = t % 4.7 < 0.14 ? "slimeBlink" : "slime";
    drawSprite(ctx, slime, sx, ground - 14 * u - Math.round(lift) * u, u);

    const bunnyPhase = (t * 0.025) % 2;
    const forward = bunnyPhase < 1;
    const travel = forward ? bunnyPhase : 2 - bunnyPhase;
    const hop = Math.abs(Math.sin(t * 6)) * 3;
    drawSprite(ctx, "bunny", snap(u * 6 + travel * (width - u * 20)), ground - 7 * u - Math.round(hop) * u + u, u, { flip: forward });

    if (isNight)
      for (let i = 0; i < 14; i++) {
        const x = hash(i * 11 + 2) * width + Math.sin(t * 0.6 + i) * u * 12;
        const y = ground - u * (6 + hash(i * 5 + 1) * 40) + Math.cos(t * 0.8 + i * 2) * u * 5;
        ctx.globalAlpha = 0.4 + 0.6 * Math.max(0, Math.sin(t * 2 + i));
        ctx.fillStyle = "rgba(255,242,160,0.3)";
        ctx.fillRect(Math.round(x / u) * u - u, Math.round(y / u) * u - u, u * 3, u * 3);
        ctx.fillStyle = "#FFF2A0";
        ctx.fillRect(Math.round(x / u) * u, Math.round(y / u) * u, u, u);
        ctx.globalAlpha = 1;
      }
    else
      for (let i = 0; i < 3; i++) {
        const name = Math.floor(t * 6 + i) % 2 ? "butterfly" : "butterflyUp";
        drawSprite(ctx, name, Math.round((sx + 8 * u + (i - 1) * 60 * u + Math.sin(t * 0.4 + i * 2) * u * 30) / u) * u, Math.round((ground - u * (18 + i * 7) + Math.sin(t * 1.3 + i) * u * 6) / u) * u, u);
      }

    requestAnimationFrame(frame);
  };

  document.addEventListener("pointerdown", (event) => {
    if (event.target.closest("a, button")) return;
    const ratio = canvas.width / window.innerWidth;
    const x = event.clientX * ratio;
    const y = event.clientY * ratio;
    if (x < celestial.x || y < celestial.y || x > celestial.x + celestial.w || y > celestial.y + celestial.h) return;
    toggleTheme();
  });

  const invalidate = () => {
    layout = null;
    backdrop = null;
  };
  resize();
  window.addEventListener("resize", () => {
    resize();
    invalidate();
  });
  document.addEventListener("themechange", invalidate);
  document.fonts?.ready.then(invalidate);
  new ResizeObserver(invalidate).observe(document.querySelector("main"));
  matchMedia("(prefers-color-scheme: dark)").addEventListener("change", () => document.dispatchEvent(new Event("themechange")));
  requestAnimationFrame(frame);
}

const audio = {
  context: null,
  enabled: localStorageGet("terranet-sound") !== "off",
  ensure() {
    if (!this.context) this.context = new (window.AudioContext || window.webkitAudioContext)();
    if (this.context.state === "suspended") this.context.resume();
    return this.context;
  },
  tone(freq, start, length, type = "square", volume = 0.05, sweep = null) {
    const ctx = this.ensure();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, ctx.currentTime + start);
    if (sweep) osc.frequency.linearRampToValueAtTime(sweep, ctx.currentTime + start + length);
    gain.gain.setValueAtTime(0.0001, ctx.currentTime + start);
    gain.gain.exponentialRampToValueAtTime(volume, ctx.currentTime + start + 0.005);
    gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + start + length);
    osc.connect(gain).connect(ctx.destination);
    osc.start(ctx.currentTime + start);
    osc.stop(ctx.currentTime + start + length + 0.02);
  },
  play(kind) {
    if (!this.enabled) return;
    const notes = {
      hover: () => this.tone(1568, 0, 0.04, "triangle", 0.03),
      click: () => this.tone(1319, 0, 0.06, "square", 0.04, 1760),
      toggle: () => [1047, 1568].forEach((f, i) => this.tone(f, i * 0.05, 0.06, "square", 0.04)),
      download: () => [523, 659, 784, 1047, 1319].forEach((f, i) => this.tone(f, i * 0.07, 0.18, i % 2 ? "triangle" : "square", 0.045)),
    };
    notes[kind]?.();
  },
};

function localStorageGet(key) {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function localStorageSet(key, value) {
  try {
    localStorage.setItem(key, value);
  } catch {}
}

function formatSize(bytes) {
  return bytes ? `${(bytes / 1048576).toFixed(0)} MB` : "";
}

function applyText() {
  document.querySelectorAll("[data-i18n]").forEach((el) => (el.textContent = t(el.dataset.i18n)));
  for (const platform of ["android", "windows"]) {
    const info = DATA[platform];
    const meta = document.querySelector(`[data-meta="${platform}"]`);
    const link = document.querySelector(`[data-platform="${platform}"]`);
    if (info) meta.textContent = [`v${info.version}`, formatSize(info.size)].filter(Boolean).join(" · ");
    else {
      meta.textContent = t("soon");
      link.removeAttribute("href");
      link.style.opacity = "0.7";
    }
  }
}

function setupCursor() {
  const arrow = spriteToDataUrl("cursor", 2);
  const hand = spriteToDataUrl("cursorHand", 2);
  const style = document.createElement("style");
  style.textContent = `html, body { cursor: url(${arrow}) 1 1, auto; } a, button, .sign { cursor: url(${hand}) 8 1, pointer; }`;
  document.head.appendChild(style);
}

function setupDevices() {
  document.querySelectorAll("canvas[data-sprite]").forEach((canvas) => {
    const rows = atlas.sprites[canvas.dataset.sprite];
    canvas.width = rows[0].length;
    canvas.height = rows.length;
    drawSprite(canvas.getContext("2d"), canvas.dataset.sprite, 0, 0, 1);
  });
}

function toggleTheme() {
  const next = night() ? "light" : "dark";
  document.documentElement.dataset.theme = next;
  localStorageSet("terranet-theme", next);
  audio.play("toggle");
  document.dispatchEvent(new Event("themechange"));
}

const iconUrl = (name) => `url(${spriteToDataUrl(name, 3, getComputedStyle(document.body).color)})`;

function setupTheme() {
  const button = document.querySelector(".theme-toggle");
  const icon = button.querySelector("span");
  const paint = () => (icon.style.backgroundImage = iconUrl(night() ? "moonIcon" : "sunIcon"));
  paint();
  button.addEventListener("click", toggleTheme);
  document.addEventListener("themechange", () => requestAnimationFrame(paint));
}

function setupSound() {
  const toggle = document.querySelector(".sound-toggle");
  const icon = toggle.querySelector("span");
  const paint = () => (icon.style.backgroundImage = iconUrl(audio.enabled ? "speaker" : "speakerMute"));
  paint();
  document.addEventListener("themechange", () => requestAnimationFrame(paint));
  toggle.addEventListener("click", () => {
    audio.enabled = !audio.enabled;
    localStorageSet("terranet-sound", audio.enabled ? "on" : "off");
    paint();
    audio.play("toggle");
  });
  document.querySelectorAll("a, button:not(.toggle)").forEach((el) => {
    el.addEventListener("pointerenter", () => audio.context && audio.play("hover"));
    el.addEventListener("click", () => audio.play(el.dataset.sound || "click"));
  });
}

(async () => {
  const theme = localStorageGet("terranet-theme");
  if (theme) document.documentElement.dataset.theme = theme;
  applyText();
  atlas = await loadAtlas();
  setupCursor();
  setupDevices();
  setupSound();
  setupTheme();
  scene();
})();
