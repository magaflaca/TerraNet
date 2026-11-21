import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "art"
OUT = ART / "build"
ANDROID = ROOT / "src" / "TerraNet.Android" / "Resources"
WEB = ROOT / "web" / "public"

SKY_TOP = (143, 208, 255)
SKY_BOTTOM = (226, 245, 255)
ARC = (255, 255, 255)
ARC_GLOW = (255, 207, 92)
DENSITIES = {"mdpi": 1, "hdpi": 1.5, "xhdpi": 2, "xxhdpi": 3, "xxxhdpi": 4}


def load():
    data = json.loads((ART / "sprites.json").read_text(encoding="utf-8"))
    palette = {k: tuple(int(v[i:i + 2], 16) for i in (1, 3, 5)) + (255,) for k, v in data["palette"].items()}
    return palette, {**data["sprites"], **data["glyphs"]}


def stamp(canvas, sprite, palette, ox, oy):
    for y, row in enumerate(sprite):
        for x, key in enumerate(row):
            if key in palette and 0 <= ox + x < canvas.width and 0 <= oy + y < canvas.height:
                canvas.putpixel((ox + x, oy + y), palette[key])


def lerp(a, b, f):
    return tuple(round(a[i] + (b[i] - a[i]) * f) for i in range(3)) + (255,)


def sky(size, bands=6):
    canvas = Image.new("RGBA", (size, size))
    band = size / bands
    for y in range(size):
        canvas.paste(lerp(SKY_TOP, SKY_BOTTOM, min(int(y / band), bands - 1) / (bands - 1)), (0, y, size, y + 1))
    return canvas


def arcs(canvas, cx, cy, radii, palette):
    for index, radius in enumerate(radii):
        color = ARC_GLOW if index == 0 else ARC
        for x in range(cx - radius, cx + radius + 1):
            for y in range(cy - radius, cy + 1):
                d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
                if radius - 0.5 <= d < radius + 0.5 and y <= cy - radius * 0.45:
                    canvas.putpixel((x, y), color + (255,))


def emblem(size, palette, sprites, with_sky):
    canvas = sky(size) if with_sky else Image.new("RGBA", (size, size), (0, 0, 0, 0))
    slime = sprites["slime"]
    grass = sprites["grass"]
    ground = size - 7
    for x in range(0, size, len(grass[0])):
        stamp(canvas, grass[:7], palette, x, ground)
    sx = (size - len(slime[0])) // 2
    sy = ground - len(slime) + 2
    stamp(canvas, slime, palette, sx, sy)
    arcs(canvas, size // 2, sy - 1, [3, 6, 9], palette)
    return canvas


def island(palette, sprites, width=26, depth=7):
    canvas = Image.new("RGBA", (width, depth + 3), (0, 0, 0, 0))
    grass = sprites["grass"]
    for y in range(depth + 3):
        inset = 0 if y < 3 else (y - 2) * 2
        for x in range(inset, width - inset):
            key = grass[min(y, len(grass) - 1)][x % len(grass[0])]
            if y == depth + 2 or x in (inset, width - inset - 1) and y >= 3:
                key = "e"
            canvas.putpixel((x, y), palette[key])
    return canvas


def floating(size, palette, sprites):
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    slime = sprites["slime"]
    land = island(palette, sprites)
    top = size // 2 + 5
    canvas.paste(land, ((size - land.width) // 2, top), land)
    sx = (size - len(slime[0])) // 2
    sy = top - len(slime) + 2
    stamp(canvas, slime, palette, sx, sy)
    arcs(canvas, size // 2, sy - 1, [3, 6, 9], palette)
    return canvas


def badge(palette, sprites):
    size = 16
    canvas = sky(size, bands=4)
    for x in range(size):
        canvas.putpixel((x, 14), palette["g" if x % 3 else "G"])
        canvas.putpixel((x, 15), palette["G" if x % 4 else "h"])
    stamp(canvas, sprites["slimeIcon"], palette, 2, 5)
    stamp(canvas, ["..y..", ".yYy.", "..y.."], {**palette}, 11, 1)
    return notch(canvas, 1)


def tray(palette, sprites, online):
    canvas = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    stamp(canvas, sprites["slimeIcon"], palette, 1, 6)
    if online:
        stamp(canvas, [".kkk.", "kGGGk", "kGgGk", "kGGGk", ".kkk."], palette, 11, 0)
    else:
        stamp(canvas, ["KKK", "..K", ".K.", "KKK"], palette, 12, 1)
    return canvas


def outlined(canvas, position, text, font, fill, outline, shadow, thickness=1):
    draw = ImageDraw.Draw(canvas)
    draw.fontmode = "1"
    x, y = position
    draw.text((x, y + thickness * 2), text, font=font, fill=shadow)
    for dx in range(-thickness, thickness + 1):
        for dy in range(-thickness, thickness * 2 + 1):
            draw.text((x + dx, y + dy), text, font=font, fill=outline)
    draw.text((x, y), text, font=font, fill=fill)


def og(palette, sprites, lines=("Host Terraria worlds", "from your phone or PC"), width=240, scale=5):
    height = 126
    dx = (width - 240) // 2
    banner = sky(126).resize((width, height), Image.NEAREST)
    ground = height - 12
    for x in range(0, width, 16):
        stamp(banner, sprites["grass"], palette, x, ground)
    stamp(banner, sprites["cloud"], palette, dx + 14, 10)
    stamp(banner, sprites["cloudSmall"], palette, dx + 186, 30)
    stamp(banner, sprites["sun"], palette, dx + 212, 6)
    stamp(banner, sprites["house"], palette, dx + 14, ground - 20)
    stamp(banner, sprites["lamp"], palette, dx + 41, ground - 11)
    stamp(banner, sprites["treeBig"], palette, dx + 196, ground - 27)
    stamp(banner, sprites["bush"], palette, dx + 178, ground - 5)
    stamp(banner, sprites["slime"], palette, dx + 112, ground - 12)
    stamp(banner, sprites["bunny"], palette, dx + 150, ground - 6)
    stamp(banner, sprites["flower"], palette, dx + 70, ground - 4)
    stamp(banner, sprites["mushroom"], palette, dx + 92, ground - 6)

    ink = (59, 47, 74, 255)
    title = ImageFont.truetype(str(ART / "fonts" / "PixelifySans-Bold.ttf"), 30)
    body = ImageFont.truetype(str(ART / "fonts" / "PixelifySans-SemiBold.ttf"), 13)
    measure = ImageDraw.Draw(banner)
    terra = measure.textlength("Terra", font=title)
    total = terra + measure.textlength("Net", font=title)
    left = round((width - total) / 2)
    outlined(banner, (left, 14), "Terra", title, (155, 229, 138, 255), ink, ink)
    outlined(banner, (left + round(terra), 14), "Net", title, (255, 207, 92, 255), ink, ink)
    for index, line in enumerate(lines):
        size = measure.textlength(line, font=body)
        tagline(banner, (round((width - size) / 2), 54 + index * 16), line, body, (255, 255, 255, 255), ink)
    return banner.resize((width * scale, height * scale), Image.NEAREST)


def tagline(canvas, position, text, font, fill, ink):
    pad = 4
    measure = ImageDraw.Draw(canvas)
    mask = Image.new("L", (round(measure.textlength(text, font=font)) + pad * 2, font.size * 2), 0)
    draw = ImageDraw.Draw(mask)
    draw.fontmode = "1"
    draw.text((pad, pad), text, font=font, fill=255)
    if text.endswith("C"):
        start = pad + round(draw.textlength(text[:-1], font=font))
        glyph = mask.crop((start, 0, mask.width, mask.height))
        left, top, right, bottom = glyph.getbbox()
        middle = (left + right) // 2
        for y in range(top + 2, bottom - 2):
            for x in range(middle, right):
                mask.putpixel((start + x, y), 0)
    x, y = position[0] - pad, position[1] - pad
    solid = lambda color: Image.new("RGBA", mask.size, color)
    canvas.paste(solid(ink), (x, y + 2), mask)
    for dx in range(-1, 2):
        for dy in range(-1, 3):
            canvas.paste(solid(ink), (x + dx, y + dy), mask)
    canvas.paste(solid(fill), (x, y), mask)


def logo(palette, sprites):
    ink = (59, 47, 74, 255)
    font = ImageFont.truetype(str(ART / "fonts" / "PixelifySans-Bold.ttf"), 30)
    measure = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    terra = measure.textlength("Terra", font=font)
    words = round(terra + measure.textlength("Net", font=font))
    slime = Image.new("RGBA", (len(sprites["slime"][0]), len(sprites["slime"])), (0, 0, 0, 0))
    stamp(slime, sprites["slime"], palette, 0, 0)
    slime = slime.resize((slime.width * 2, slime.height * 2), Image.NEAREST)
    canvas = Image.new("RGBA", (slime.width + words + 24, 48), (0, 0, 0, 0))
    canvas.paste(slime, (2, canvas.height - slime.height - 6), slime)
    stamp(canvas, sprites["sparkle"], palette, slime.width - 4, 4)
    text = slime.width + 10
    outlined(canvas, (text, 6), "Terra", font, (155, 229, 138, 255), ink, ink)
    outlined(canvas, (text + round(terra), 6), "Net", font, (255, 207, 92, 255), ink, ink)
    cropped = canvas.crop(canvas.getbbox())
    return cropped.resize((cropped.width * 4, cropped.height * 4), Image.NEAREST)


def notch(image, radius):
    mask = Image.new("L", image.size, 255)
    w, h = image.size
    for y in range(radius):
        cut = radius - y
        for x in range(cut):
            for px, py in ((x, y), (w - 1 - x, y), (x, h - 1 - y), (w - 1 - x, h - 1 - y)):
                mask.putpixel((px, py), 0)
    image.putalpha(mask)
    return image


def scaled(image, size):
    return image.resize((size, size), Image.NEAREST)


PLAY = ROOT / "docs" / "playstore" / "graphics"


def main():
    palette, sprites = load()
    OUT.mkdir(parents=True, exist_ok=True)

    icon = notch(emblem(32, palette, sprites, True), 3)
    scaled(icon, 512).save(OUT / "icon-512.png")
    small = badge(palette, sprites)
    frames = [scaled(small, side) for side in (16, 32, 48, 64, 128, 256)]
    frames[-1].save(OUT / "terranet.ico", sizes=[f.size for f in frames], append_images=frames[:-1])
    scaled(small, 256).save(OUT / "badge-256.png")
    for state in ("idle", "online"):
        image = tray(palette, sprites, state == "online")
        layers = [scaled(image, side) for side in (16, 32, 48)]
        layers[-1].save(OUT / f"tray-{state}.ico", sizes=[f.size for f in layers], append_images=layers[:-1])
        scaled(image, 128).save(OUT / f"tray-{state}.png")

    foreground = floating(54, palette, sprites)
    background = sky(54)
    for name, factor in DENSITIES.items():
        folder = ANDROID / f"mipmap-{name}"
        folder.mkdir(parents=True, exist_ok=True)
        side = round(108 * factor)
        scaled(foreground, side).save(folder / "ic_launcher_foreground.png")
        scaled(background, side).save(folder / "ic_launcher_background.png")
        scaled(icon, round(48 * factor)).save(folder / "ic_launcher.png")
        monochrome = Image.new("RGBA", foreground.size, (0, 0, 0, 0))
        for y in range(foreground.height):
            for x in range(foreground.width):
                if foreground.getpixel((x, y))[3] > 0:
                    monochrome.putpixel((x, y), (255, 255, 255, 255))
        scaled(monochrome, side).save(folder / "ic_launcher_monochrome.png")

    WEB.mkdir(parents=True, exist_ok=True)
    scaled(icon, 180).save(WEB / "apple-touch-icon.png")
    scaled(icon, 64).save(WEB / "favicon.png")
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {icon.width} {icon.height}" shape-rendering="crispEdges">']
    for y in range(icon.height):
        x = 0
        while x < icon.width:
            pixel = icon.getpixel((x, y))
            run = 1
            while x + run < icon.width and icon.getpixel((x + run, y)) == pixel:
                run += 1
            if pixel[3] > 0:
                svg.append(f'<rect x="{x}" y="{y}" width="{run}" height="1" fill="#{pixel[0]:02x}{pixel[1]:02x}{pixel[2]:02x}"/>')
            x += run
    svg.append("</svg>")
    (WEB / "favicon.svg").write_text("".join(svg), encoding="utf-8")

    og(palette, sprites).save(WEB / "og.png")
    logo(palette, sprites).save(OUT / "logo.png")
    PLAY.mkdir(parents=True, exist_ok=True)
    scaled(icon, 512).convert("RGBA").save(PLAY / "icon-512.png")
    features = {"en": ("Host Terraria worlds", "from your phone or PC"), "es": ("Abre mundos de Terraria", "desde tu celular o PC")}
    for language, lines in features.items():
        scene = og(palette, sprites, lines, width=256, scale=1).crop((0, 1, 256, 126))
        scene.resize((1024, 500), Image.NEAREST).convert("RGB").save(PLAY / f"feature-{language}.png")
    print("rendered", OUT, file=sys.stderr)


if __name__ == "__main__":
    main()
