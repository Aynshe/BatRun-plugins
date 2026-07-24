from PIL import Image, ImageDraw

ICON_PATH = r"Z:\.Code\.vsCodeProject\RetroBatGameMode\Resources\icon.ico"

import os
os.makedirs(os.path.dirname(ICON_PATH), exist_ok=True)

SIZES = [256, 128, 64, 48, 32, 16]

def draw_icon(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Rounded square background with dark blue/purple gradient (simulate via two solid colors)
    # Use RGBA color stops manually by drawing strips... but Pillow needs numpy or similar for gradient.
    # We'll fake a vertical gradient by mixing dark navy at top and purple at bottom.
    n = size
    top_color = (24, 24, 60, 255)      # dark navy
    bot_color = (90, 30, 130, 255)     # deep purple

    # Border radius (in px)
    radius = max(4, size // 6)

    # Draw rounded rect background
    d.rounded_rectangle([(0, 0), (size - 1, size - 1)], radius=radius, fill=top_color)
    # Overlay gradient slices
    for y in range(size):
        t = y / max(1, size - 1)
        r = int(top_color[0] + (bot_color[0] - top_color[0]) * t)
        g = int(top_color[1] + (bot_color[1] - top_color[1]) * t)
        b = int(top_color[2] + (bot_color[2] - top_color[2]) * t)
        d.line([(0, y), (size, y)], fill=(r, g, b, 255))
    # Re-draw rounded corners (overlay) to keep corners transparent on the gradient... but Pillow can't easily mask.
    # We'll just leave the gradient inside the square—corners will be slightly visible. To keep "rounded" look,
    # we draw a transparent background then re-stroke the shape on top.
    # Actually simpler: create a mask from rounded rect and apply gradient only inside.
    mask = Image.new("L", (size, size), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle([(0, 0), (size - 1, size - 1)], radius=radius, fill=255)

    # Build gradient image
    grad = Image.new("RGBA", (size, size), top_color)
    for y in range(size):
        t = y / max(1, size - 1)
        r = int(top_color[0] + (bot_color[0] - top_color[0]) * t)
        g = int(top_color[1] + (bot_color[1] - top_color[1]) * t)
        b = int(top_color[2] + (bot_color[2] - top_color[2]) * t)
        for x in range(size):
            grad.putpixel((x, y), (r, g, b, 255))

    base = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    base.paste(grad, (0, 0), mask)

    draw = ImageDraw.Draw(base)

    # Neon green/cyan accent: lightning bolt (representing "Game Mode" / boost / power)
    accent = (74, 222, 128, 255)   # neon green
    accent2 = (59, 220, 235, 255)  # neon cyan

    # Lightning bolt polygon (centered)
    p = size / 100.0  # coordinate scaling (with 100-unit grid)
    bolt = [
        (48 * p, 18 * p),    # top
        (32 * p, 50 * p),
        (46 * p, 50 * p),
        (38 * p, 82 * p),
        (66 * p, 44 * p),
        (52 * p, 44 * p),
        (60 * p, 18 * p),
    ]
    draw.polygon(bolt, fill=accent)

    # Small cyan "speed" bars on the right
    bar_x = int(78 * p)
    bar_y0 = int(60 * p)
    for i in range(3):
        draw.rounded_rectangle(
            [(bar_x, bar_y0 + i * int(6 * p)), (bar_x + int(8 * p), bar_y0 + i * int(6 * p) + int(3 * p))],
            radius=1,
            fill=accent2,
        )

    # Optional: thin glow border
    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(glow)
    gdraw.rounded_rectangle([(0, 0), (size - 1, size - 1)], radius=radius, outline=accent2, width=max(1, size // 64))
    base.alpha_composite(glow)

    return base


frames = [draw_icon(s) for s in SIZES]

# Save .ico with multiple sizes
frames[0].save(
    ICON_PATH,
    format="ICO",
    sizes=[(s, s) for s in SIZES],
    append_images=frames[1:],
)

print(f"Wrote {ICON_PATH}: size={os.path.getsize(ICON_PATH)} bytes")
