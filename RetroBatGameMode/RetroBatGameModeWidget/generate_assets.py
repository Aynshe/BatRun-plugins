"""
Generate UWP Game Bar Widget assets + signing key.

Outputs:
- RetroBatGameModeWidget/Assets/StoreLogo.png         (50x50)
- RetroBatGameModeWidget/Assets/Square44x44Logo.png   (44x44)
- RetroBatGameModeWidget/Assets/Square150x150Logo.png (150x150)
- RetroBatGameModeWidget/Assets/Wide310x150Logo.png   (310x150)
- RetroBatGameModeWidget/Assets/LockVisualLogo.png    (24x24)
- RetroBatGameModeWidget_TemporaryKey.pfx             (dev signing cert)

Re-uses the existing icon.ico design (gradient blue->purple, neon-green lightning bolt,
cyan speed bars, rounded corners) so the icon is consistent across the suite.
"""
import os
import sys
import subprocess
from PIL import Image, ImageDraw

BASE = r"Z:\.Code\.vsCodeProject\RetroBatGameMode"
OUT = os.path.join(BASE, "RetroBatGameModeWidget", "Assets")
ICON_SRC = os.path.join(BASE, "Resources", "icon.ico")
os.makedirs(OUT, exist_ok=True)

def draw_icon(size, with_alpha=True):
    """Re-renders the suite's icon at any size. Same DNA as `Resources/generate_icon.py`."""
    radius = max(4, size // 6)
    top_color = (24, 24, 60, 255) if with_alpha else (24, 24, 60)
    bot_color = (90, 30, 130, 255) if with_alpha else (90, 30, 130)
    accent = (74, 222, 128, 255) if with_alpha else (74, 222, 128)
    accent2 = (59, 220, 235, 255) if with_alpha else (59, 220, 235)

    mask = Image.new("L", (size, size), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle([(0, 0), (size - 1, size - 1)], radius=radius, fill=255)

    grad = Image.new("RGBA" if with_alpha else "RGB", (size, size), top_color)
    for y in range(size):
        t = y / max(1, size - 1)
        r = int(top_color[0] + (bot_color[0] - top_color[0]) * t)
        g = int(top_color[1] + (bot_color[1] - top_color[1]) * t)
        b = int(top_color[2] + (bot_color[2] - top_color[2]) * t)
        for x in range(size):
            grad.putpixel((x, y), (r, g, b) + ((255,) if with_alpha else ()))

    base = Image.new("RGBA" if with_alpha else "RGB", (size, size), (0,) * (4 if with_alpha else 3))
    base.paste(grad, (0, 0) if not with_alpha else (0, 0), mask if with_alpha else None)

    draw = ImageDraw.Draw(base)
    p = size / 100.0
    bolt = [
        (48 * p, 18 * p), (32 * p, 50 * p), (46 * p, 50 * p),
        (38 * p, 82 * p), (66 * p, 44 * p), (52 * p, 44 * p),
        (60 * p, 18 * p),
    ]
    draw.polygon(bolt, fill=accent)

    bar_x = int(78 * p)
    bar_y0 = int(60 * p)
    for i in range(3):
        draw.rounded_rectangle(
            [(bar_x, bar_y0 + i * int(6 * p)),
             (bar_x + int(8 * p), bar_y0 + i * int(6 * p) + int(3 * p))],
            radius=1, fill=accent2)

    glow = Image.new("RGBA" if with_alpha else "RGB", (size, size), (0,)*4 if with_alpha else (0,)*3)
    gdraw = ImageDraw.Draw(glow)
    gdraw.rounded_rectangle([(0, 0), (size - 1, size - 1)], radius=radius, outline=accent2, width=max(1, size // 64))
    base = Image.alpha_composite(base.convert("RGBA"), glow) if with_alpha else base
    return base.convert("RGBA") if with_alpha else base

def save_icon(size, path, force_alpha=True):
    img = draw_icon(size, with_alpha=force_alpha)
    if not force_alpha:
        img = img.convert("RGB")
    img.save(path, format="PNG")
    print(f"Wrote {path} ({size}x{size}, {os.path.getsize(path)} bytes)")

save_icon(50, os.path.join(OUT, "StoreLogo.png"))
save_icon(44, os.path.join(OUT, "Square44x44Logo.png"))
save_icon(150, os.path.join(OUT, "Square150x150Logo.png"))
save_icon(310, os.path.join(OUT, "Wide310x150Logo.png"), force_alpha=False)
save_icon(24, os.path.join(OUT, "LockVisualLogo.png"))
