"""Stylized (casual/toon) LEXIO table textures, matched to the in-game screenshot."""
import os, numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "textures_toon")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(3)
FONT = "/usr/share/fonts/opentype/inter/InterDisplay-Black.otf"

def normal_from_height(h, strength):
    h = h.astype(np.float32) / 255.0
    dx = np.gradient(h, axis=1) * strength
    dy = np.gradient(h, axis=0) * strength
    n = np.dstack([-dx, dy, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8))

# ---------------- Felt (UV disk = felt radius R) ----------------
S = 2048; c = S / 2; R = S / 2
yy, xx = np.mgrid[0:S, 0:S]
rr = np.hypot(xx - c, yy - c) / R

center, edge = np.array([98, 198, 124], np.float32), np.array([50, 158, 80], np.float32)
t = np.clip(rr, 0, 1) ** 1.6
alb = center * (1 - t[..., None]) + edge * t[..., None]
alb *= (1 - 0.18 * np.clip((rr - 0.93) / 0.07, 0, 1))[..., None]   # soft shadow under the rim
img = Image.fromarray(np.clip(alb, 0, 255).astype(np.uint8)).convert("RGBA")

DARK = (44, 140, 72, 255)
ov = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(ov)
H = Image.new("L", (S, S), 128); dh = ImageDraw.Draw(H)

def ring(rad, w, col, hv):
    box = [c - rad * R, c - rad * R, c + rad * R, c + rad * R]
    d.ellipse(box, outline=col, width=w); dh.ellipse(box, outline=hv, width=w)

# dashed guide ring
rad = 0.63 * R; n = 44
for k in range(n):
    a0 = 360 * k / n; a1 = a0 + 360 / n * 0.55
    box = [c - rad, c - rad, c + rad, c + rad]
    d.arc(box, a0, a1, fill=(112, 206, 136, 255), width=12); dh.arc(box, a0, a1, fill=140, width=12)

# emblem rings (engraved)
ring(0.375, 18, DARK, 70)
ring(0.320, 18, DARK, 70)

# triangles (top points up, bottom points down) – image Y grows downward
def tri(base_y, tip_y, hw):
    pts = [(c - hw * R, c - base_y * R), (c + hw * R, c - base_y * R), (c, c - tip_y * R)]
    d.polygon(pts, fill=DARK); dh.polygon(pts, fill=70)
tri(0.225, 0.315, 0.20)
tri(-0.215, -0.305, 0.21)

# LEXIO wordmark (raised, light fill with dark outline)
font = ImageFont.truetype(FONT, 10)
target_w = 0.58 * R
size = int(10 * target_w / d.textlength("LEXIO", font=font))
font = ImageFont.truetype(FONT, size)
bb = d.textbbox((0, 0), "LEXIO", font=font, anchor="mm")
d.text((c + 6, c + 8), "LEXIO", font=font, anchor="mm", fill=(36, 120, 60, 160), stroke_width=14, stroke_fill=(36, 120, 60, 160))  # drop shadow
d.text((c, c), "LEXIO", font=font, anchor="mm", fill=(104, 212, 132, 255), stroke_width=12, stroke_fill=DARK)
dh.text((c, c), "LEXIO", font=font, anchor="mm", fill=230, stroke_width=12, stroke_fill=170)

img = Image.alpha_composite(img, ov).convert("RGB")
noise = rng.normal(0, 2.0, (S, S, 1))           # very light grain, keeps it toon-clean
img = Image.fromarray(np.clip(np.asarray(img).astype(np.float32) + noise, 0, 255).astype(np.uint8))
img.save(f"{OUT}/Felt_BaseColor.png")
normal_from_height(np.asarray(H.filter(ImageFilter.GaussianBlur(3))), 5).save(f"{OUT}/Felt_Normal.png")

# ---------------- Blue rim band (wraps circumference, 12 panels) ----------------
W, Hh = 2048, 256
blue = np.zeros((Hh, W, 3), np.float32) + np.array([58, 88, 200], np.float32)
seam = np.zeros((Hh, W), np.float32)
for k in range(12):
    x = int(round(k * W / 12))
    for dx in range(-4, 5):
        seam[:, (x + dx) % W] = np.maximum(seam[:, (x + dx) % W], 1.0 if abs(dx) < 3 else 0.5)
blue = blue * (1 - seam[..., None]) + np.array([36, 54, 150], np.float32) * seam[..., None]
Image.fromarray(blue.astype(np.uint8)).save(f"{OUT}/BluePanel_BaseColor.png")
normal_from_height(128 - seam * 60, 4).save(f"{OUT}/BluePanel_Normal.png")
print("->", OUT, sorted(os.listdir(OUT)))
