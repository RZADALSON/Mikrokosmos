# Obróbka zdjęć atlasu: atlas/raw/<plik> → atlas/photos/<klucz>.png|.jpg
#  • "cut"   – wycięcie z jednolitego tła: kolor tła z brzegów, zalewanie od krawędzi (dziury w obiekcie zostają),
#              wygładzona krawędź, przycięcie do obiektu, przezroczysty PNG;
#  • "frame" – zdjęcie bez wycinania (tkanki, preparaty z wypełnionym kadrem, EM), przycięte i zmniejszone.
# Gdy wycięcie wygląda na nieudane (obiekt < 4% albo > 92% kadru), skrypt sam przechodzi na "frame".
import json, os, sys
from collections import deque
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
RAW, OUT = os.path.join(HERE, "raw"), os.path.join(HERE, "photos")
MAXPX = 720

def cutout(img, thr=38):
    a = np.asarray(img.convert("RGB")).astype(np.int16)
    h, w, _ = a.shape
    edge = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    bg = np.median(edge, axis=0)
    dist = np.sqrt(((a - bg) ** 2).sum(axis=2))
    near = dist < thr
    # zalewanie od brzegów: tłem jest tylko obszar podobny do tła i połączony z krawędzią
    seen = np.zeros((h, w), bool); q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if near[y, x] and not seen[y, x]: seen[y, x] = True; q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if near[y, x] and not seen[y, x]: seen[y, x] = True; q.append((y, x))
    while q:
        y, x = q.popleft()
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= ny < h and 0 <= nx < w and near[ny, nx] and not seen[ny, nx]: seen[ny, nx] = True; q.append((ny, nx))
    fg = ~seen
    frac = fg.mean()
    if frac < 0.04 or frac > 0.92: return None, frac
    alpha = Image.fromarray((fg * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1.6))
    rgba = img.convert("RGBA"); rgba.putalpha(alpha)
    ys, xs = np.nonzero(fg)
    m = int(0.04 * max(w, h))
    box = (max(0, xs.min() - m), max(0, ys.min() - m), min(w, xs.max() + m), min(h, ys.max() + m))
    return rgba.crop(box), frac

def fit(img):
    s = MAXPX / max(img.size)
    return img.resize((max(1, int(img.width * s)), max(1, int(img.height * s))), Image.LANCZOS) if s < 1 else img

def main():
    plan = json.load(open(os.path.join(HERE, "photo_plan.json"), encoding="utf-8"))
    photos = json.load(open(os.path.join(HERE, "photos.json"), encoding="utf-8")) if os.path.exists(os.path.join(HERE, "photos.json")) else {}
    os.makedirs(OUT, exist_ok=True)
    only = set(sys.argv[1:])
    for key, p in plan.items():
        if only and key not in only: continue
        src = os.path.join(RAW, p["raw"])
        if not os.path.exists(src): print(f"{key}: brak pliku {p['raw']}"); continue
        img = Image.open(src); img.load()
        if img.mode not in ("RGB", "RGBA"): img = img.convert("RGB")
        if "crop" in p: img = img.crop(tuple(p["crop"]))  # [x0, y0, x1, y1] – np. bez podpisu/podziałki
        mode, info = p.get("mode", "frame"), ""
        if mode == "cut":
            c, frac = cutout(img, p.get("thr", 38))
            if c is None: mode, info = "frame", f"(wycięcie nieudane, obiekt {frac:.0%} kadru)"
            else: img, info = c, f"(obiekt {frac:.0%} kadru)"
        img = fit(img)
        name = key + (".png" if mode == "cut" else ".jpg")
        path = os.path.join(OUT, name)
        if mode == "cut": img.save(path, optimize=True)
        else: img.convert("RGB").save(path, quality=86, optimize=True)
        photos[key] = {"photo": name, "credit": p["credit"], "url": p["page"]}
        print(f"{key}: {mode} {info} → {name} {img.size[0]}×{img.size[1]}, {os.path.getsize(path) // 1024} KB")
    json.dump(photos, open(os.path.join(HERE, "photos.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

if __name__ == "__main__": main()
