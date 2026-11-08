import { copyFileSync, mkdirSync, readdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const web = join(dirname(fileURLToPath(import.meta.url)), "..");
const art = join(web, "..", "art");
mkdirSync(join(web, "public", "fonts"), { recursive: true });
copyFileSync(join(art, "sprites.json"), join(web, "public", "sprites.json"));
for (const font of readdirSync(join(art, "fonts")).filter((f) => f.endsWith(".ttf")))
  copyFileSync(join(art, "fonts", font), join(web, "public", "fonts", font));
