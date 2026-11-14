import sys, glob
from PIL import Image, ImageDraw, ImageFont
d, out = sys.argv[1], sys.argv[2]; cols = int(sys.argv[3]) if len(sys.argv) > 3 else 5
fs = sorted(glob.glob(f"{d}/ico_*_256.png")); C = 276
s = Image.new("RGBA", (cols * C, ((len(fs) + cols - 1) // cols) * (C + 24)), (16, 14, 14, 255)); dr = ImageDraw.Draw(s)
F = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 15)
for i, f in enumerate(fs):
    x, y = (i % cols) * C, (i // cols) * (C + 24)
    s.alpha_composite(Image.open(f), (x + 10, y + 6)); dr.text((x + C // 2, y + C + 8), f.split("/")[-1][4:-8], font=F, fill=(200, 190, 170), anchor="mm")
s.convert("RGB").save(out)
