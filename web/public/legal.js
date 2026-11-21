(() => {
  const root = document.documentElement;

  const stored = (key) => {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  };
  const store = (key, value) => {
    try {
      localStorage.setItem(key, value);
    } catch {}
  };

  const theme = stored("terranet-theme");
  if (theme === "light" || theme === "dark") root.dataset.theme = theme;

  const requested = new URLSearchParams(location.search).get("lang");
  const browser = ((navigator.languages && navigator.languages[0]) || navigator.language || "").toLowerCase();
  const initial = requested === "es" || requested === "en" ? requested : stored("terranet-lang") || (browser.startsWith("es") ? "es" : "en");

  const apply = (lang) => {
    root.lang = lang;
    document.querySelectorAll("[data-lang]").forEach((el) => el.classList.toggle("on", el.dataset.lang === lang));
    document.querySelectorAll(".switch button").forEach((b) => b.setAttribute("aria-pressed", String(b.dataset.set === lang)));
    const title = document.body.dataset["title" + lang[0].toUpperCase() + lang.slice(1)];
    if (title) document.title = title;
  };

  document.querySelectorAll(".switch button").forEach((button) =>
    button.addEventListener("click", () => {
      store("terranet-lang", button.dataset.set);
      apply(button.dataset.set);
    })
  );
  apply(initial === "es" ? "es" : "en");

  document.querySelectorAll("h1, h2, .foot a").forEach((el) => {
    const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
    const nodes = [];
    while (walker.nextNode()) if (walker.currentNode.nodeValue.includes("C")) nodes.push(walker.currentNode);
    nodes.forEach((node) => {
      const parts = node.nodeValue.split("C");
      const fragment = document.createDocumentFragment();
      parts.forEach((part, index) => {
        if (index > 0) {
          const glyph = document.createElement("span");
          glyph.className = "open-c";
          glyph.textContent = "C";
          fragment.append(glyph);
        }
        if (part) fragment.append(part);
      });
      node.replaceWith(fragment);
    });
  });

  fetch("/sprites.json")
    .then((r) => r.json())
    .then(({ palette, sprites }) => {
      const paint = (ctx, rows, x, y, unit) =>
        rows.forEach((row, ry) =>
          [...row].forEach((ch, rx) => {
            const color = palette[ch];
            if (!color) return;
            ctx.fillStyle = color;
            ctx.fillRect(x + rx * unit, y + ry * unit, unit, unit);
          })
        );

      const brand = document.querySelector(".brand canvas");
      if (brand && sprites.slime) {
        const rows = sprites.slime;
        brand.width = rows[0].length;
        brand.height = rows.length;
        paint(brand.getContext("2d"), rows, 0, 0, 1);
      }

      const ground = document.querySelector(".ground");
      const grass = sprites.grass;
      if (!ground || !grass) return;
      const draw = () => {
        const unit = 3;
        const tile = grass[0].length;
        const height = grass.length;
        ground.width = Math.ceil(ground.clientWidth / unit);
        ground.height = height;
        ground.style.height = height * unit + "px";
        const ctx = ground.getContext("2d");
        ctx.clearRect(0, 0, ground.width, ground.height);
        for (let x = 0; x < ground.width; x += tile) paint(ctx, grass, x, 0, 1);
      };
      draw();
      addEventListener("resize", draw);
    })
    .catch(() => {});
})();
