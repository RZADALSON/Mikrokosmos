# -*- coding: utf-8 -*-
"""
MikroScope – kreskowa czcionka w stylu generatora znaków z paska danych mikroskopu elektronowego
(np. "F6691 5.0 kV X15.0K 2.00µm"): szerokie, stałej szerokości znaki rysowane jedną kreską o okrągłych
końcach, przekreślone zero, lekko nierówne linie (drgania wiązki / rastra wideo).
Własny projekt glifów (bez cudzych danych) – plik TTF zapisywany bezpośrednio (bez fontTools).
"""
import math, random, struct, sys, time

UPM = 1000
U = 68.0            # jednostek fontu na jednostkę projektu
SW = 1.1            # grubość kreski (jednostki projektu)
ADV = 9.0           # szerokość znaku (monospace)
LSB = 1.0           # margines lewy – korpus znaku x ∈ [0, 7]
ASC, DSC = 900, 260
CAP, XH = 10.0, 7.0

# ------------------------------------------------------------------ prymitywy
def arc(cx, cy, rx, ry, a0, a1, step=6.0):
    n = max(2, int(math.ceil(abs(a1 - a0) / step)))
    return [(cx + rx * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + ry * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]

def ell(cx, cy, rx, ry, step=6.0):
    n = int(round(360 / step))
    return [(cx + rx * math.cos(2 * math.pi * i / n), cy + ry * math.sin(2 * math.pi * i / n)) for i in range(n)]

def S(*parts, w=1.0):
    """Otwarta łamana z punktów i list punktów (łuki)."""
    out = []
    for p in parts:
        pts = [p] if isinstance(p, tuple) and len(p) == 2 and isinstance(p[0], (int, float)) else list(p)
        for q in pts:
            if out and abs(out[-1][0] - q[0]) < 1e-6 and abs(out[-1][1] - q[1]) < 1e-6: continue
            out.append((float(q[0]), float(q[1])))
    return ('open', out, w)

def LOOP(pts, w=1.0): return ('closed', [(float(x), float(y)) for x, y in pts], w)
def DOT(x, y, w=1.25): return ('dot', [(float(x), float(y))], w)

def shift(strokes, dx=0.0, dy=0.0, sc=1.0, ox=0.0, oy=0.0):
    """Przesunięcie/skalowanie glifu (indeksy górne i dolne, znaki złożone)."""
    res = []
    for kind, pts, w in strokes:
        res.append((kind, [((x - ox) * sc + ox + dx, (y - oy) * sc + oy + dy) for x, y in pts], w * (0.85 if sc < 0.8 else 1.0)))
    return res

# ------------------------------------------------------------------ znaki
G = {}
# wielkie litery (wys. 10)
G['A'] = [S((0, 0), (3.5, 10), (7, 0)), S((1.25, 3.5), (5.75, 3.5))]
G['B'] = [S((0, 5.2), (4.5, 5.2), arc(4.5, 7.6, 2.4, 2.4, -90, 90), (0, 10), (0, 0), (4.7, 0), arc(4.7, 2.6, 2.4, 2.6, -90, 90), (4.5, 5.2))]
G['C'] = [S(arc(3.8, 5, 3.8, 5, 38, 322))]
G['D'] = [S((0, 0), (0, 10), (2.8, 10), arc(2.8, 5, 4.2, 5, 90, -90), (0, 0))]
G['E'] = [S((7, 10), (0, 10), (0, 0), (7, 0)), S((0, 5.2), (5.2, 5.2))]
G['F'] = [S((7, 10), (0, 10), (0, 0)), S((0, 5.2), (5.2, 5.2))]
G['G'] = [S(arc(3.8, 5, 3.8, 5, 40, 360), (7.6, 3.6)), S((7.6, 4.8), (4.2, 4.8)), S((7.6, 5), (7.6, 0.4))]
G['G'] = [S(arc(3.7, 5, 3.7, 5, 42, 356), (7.35, 4.6), (4.2, 4.6))]
G['H'] = [S((0, 0), (0, 10)), S((7, 0), (7, 10)), S((0, 5.2), (7, 5.2))]
G['I'] = [S((3.5, 0), (3.5, 10)), S((1.3, 10), (5.7, 10)), S((1.3, 0), (5.7, 0))]
G['J'] = [S((7, 10), (7, 3.2), arc(3.6, 3.2, 3.4, 3.2, 0, -180))]
G['K'] = [S((0, 0), (0, 10)), S((7, 10), (0, 3.4)), S((2.3, 5.6), (7, 0))]
G['L'] = [S((0, 10), (0, 0), (7, 0))]
G['M'] = [S((0, 0), (0, 10), (3.5, 3.6), (7, 10), (7, 0))]
G['N'] = [S((0, 0), (0, 10), (7, 0), (7, 10))]
G['O'] = [LOOP(ell(3.5, 5, 3.6, 5))]
G['P'] = [S((0, 0), (0, 10), (4.4, 10), arc(4.4, 7.4, 2.6, 2.6, 90, -90), (0, 4.8))]
G['Q'] = [LOOP(ell(3.5, 5, 3.6, 5)), S((4.3, 2.4), (7.4, -0.8))]
G['R'] = [S((0, 0), (0, 10), (4.4, 10), arc(4.4, 7.4, 2.6, 2.6, 90, -90), (0, 4.8)), S((3.4, 4.8), (7, 0))]
G['S'] = [S(arc(3.5, 7.5, 3.3, 2.5, 25, 270), arc(3.5, 2.5, 3.5, 2.5, 90, -155))]
G['T'] = [S((0, 10), (7, 10)), S((3.5, 10), (3.5, 0))]
G['U'] = [S((0, 10), (0, 3.4), arc(3.5, 3.4, 3.5, 3.4, 180, 360), (7, 10))]
G['V'] = [S((0, 10), (3.5, 0), (7, 10))]
G['W'] = [S((0, 10), (1.6, 0), (3.5, 6.4), (5.4, 0), (7, 10))]
G['X'] = [S((0, 10), (7, 0)), S((7, 10), (0, 0))]
G['Y'] = [S((0, 10), (3.5, 5), (7, 10)), S((3.5, 5), (3.5, 0))]
G['Z'] = [S((0, 10), (7, 10), (0, 0), (7, 0))]
# małe litery (x 7, wydłużenia 10.5 / −3)
G['a'] = [LOOP(ell(3.25, 3.5, 3.25, 3.5)), S((6.6, 7), (6.6, 0))]
G['b'] = [S((0, 10.5), (0, 0)), LOOP(ell(3.45, 3.5, 3.45, 3.5))]
G['c'] = [S(arc(3.6, 3.5, 3.6, 3.5, 42, 318))]
G['d'] = [LOOP(ell(3.3, 3.5, 3.3, 3.5)), S((6.7, 10.5), (6.7, 0))]
G['e'] = [S((0.05, 3.6), (6.95, 3.6), arc(3.5, 3.5, 3.45, 3.5, 2, 318))]
G['f'] = [S((6, 10.2), (4.6, 10.4), arc(4.6, 8.7, 1.7, 1.7, 90, 180), (2.9, 0)), S((0.7, 7), (5.6, 7))]
G['g'] = [LOOP(ell(3.3, 3.6, 3.3, 3.4)), S((6.6, 7), (6.6, -1.3), arc(3.5, -1.3, 3.1, 1.7, 0, -165))]
G['h'] = [S((0, 10.5), (0, 0)), S((0, 4.2), arc(3.45, 4.2, 3.45, 2.8, 180, 0), (6.9, 0))]
G['i'] = [S((3.5, 0), (3.5, 7)), DOT(3.5, 9.3)]
G['j'] = [S((4.6, 7), (4.6, -1.2), arc(2.6, -1.2, 2.0, 1.8, 0, -150)), DOT(4.6, 9.3)]
G['k'] = [S((0, 10.5), (0, 0)), S((6.2, 7), (0, 2.5)), S((2.3, 4.2), (6.8, 0))]
G['l'] = [S((3.5, 10.5), (3.5, 0))]
G['m'] = [S((0, 7), (0, 0)), S((0, 4.8), arc(1.75, 4.8, 1.75, 2.2, 180, 0), (3.5, 0)), S((3.5, 4.8), arc(5.25, 4.8, 1.75, 2.2, 180, 0), (7, 0))]
G['n'] = [S((0, 7), (0, 0)), S((0, 4.4), arc(3.45, 4.4, 3.45, 2.6, 180, 0), (6.9, 0))]
G['o'] = [LOOP(ell(3.5, 3.5, 3.5, 3.5))]
G['p'] = [S((0, 7), (0, -3)), LOOP(ell(3.45, 3.5, 3.45, 3.5))]
G['q'] = [LOOP(ell(3.3, 3.5, 3.3, 3.5)), S((6.7, 7), (6.7, -3))]
G['r'] = [S((0, 7), (0, 0)), S((0, 4.0), arc(3.6, 4.0, 3.6, 3.0, 180, 55))]
G['s'] = [S(arc(3.5, 5.25, 3.2, 1.75, 25, 270), arc(3.5, 1.75, 3.4, 1.75, 90, -155))]
G['t'] = [S((2.7, 9.6), (2.7, 1.3), arc(4.0, 1.3, 1.3, 1.3, 180, 300)), S((0.6, 7), (5.8, 7))]
G['u'] = [S((0, 7), (0, 2.8), arc(3.45, 2.8, 3.45, 2.8, 180, 360), (6.9, 7)), S((6.9, 2.8), (6.9, 0))]
G['v'] = [S((0, 7), (3.5, 0), (7, 7))]
G['w'] = [S((0, 7), (1.7, 0), (3.5, 5), (5.3, 0), (7, 7))]
G['x'] = [S((0, 7), (7, 0)), S((7, 7), (0, 0))]
G['y'] = [S((0, 7), (3.6, 0.3)), S((7, 7), (2.6, -3))]
G['z'] = [S((0, 7), (7, 7), (0, 0), (7, 0))]
# cyfry (wys. 10); zero przekreślone jak w generatorach znaków aparatury
G['0'] = [LOOP(ell(3.5, 5, 3.5, 5)), S((1.1, 1.6), (5.9, 8.4))]
G['1'] = [S((1.6, 7.9), (3.9, 10), (3.9, 0)), S((1.4, 0), (6.4, 0))]
G['2'] = [S(arc(3.5, 6.9, 3.3, 3.1, 158, -18), (0, 0), (7, 0))]
G['3'] = [S(arc(3.4, 7.6, 3.1, 2.4, 150, -90), arc(3.4, 2.7, 3.5, 2.7, 90, -152))]
G['4'] = [S((5.4, 0), (5.4, 10), (0, 3.1), (7, 3.1))]
G['5'] = [S((6.6, 10), (0.8, 10), (0.4, 5.7), arc(3.5, 3.3, 3.5, 3.3, 138, -150))]
G['6'] = [LOOP(ell(3.5, 3.2, 3.5, 3.2)), S(arc(5.3, 3.2, 5.3, 6.8, 180, 84))]
G['7'] = [S((0, 10), (7, 10), (2.6, 0))]
G['8'] = [LOOP(ell(3.5, 7.6, 3.0, 2.4)), LOOP(ell(3.5, 2.7, 3.5, 2.7))]
G['9'] = [LOOP(ell(3.5, 6.8, 3.5, 3.2)), S(arc(1.7, 6.8, 5.3, 6.8, 0, -96))]
# interpunkcja i symbole ASCII
G[' '] = []
G['.'] = [DOT(3.5, 0.35, 1.35)]
G[','] = [S((3.8, 0.6), (2.8, -1.9), w=1.1)]
G[':'] = [DOT(3.5, 0.35, 1.3), DOT(3.5, 5.6, 1.3)]
G[';'] = [DOT(3.5, 5.6, 1.3), S((3.8, 0.6), (2.8, -1.9))]
G['!'] = [S((3.5, 10), (3.5, 3.1)), DOT(3.5, 0.35, 1.3)]
G['?'] = [S(arc(3.5, 7.5, 3.0, 2.5, 160, -50), (3.6, 4.4), (3.6, 3.1)), DOT(3.6, 0.35, 1.3)]
G["'"] = [S((3.5, 10.2), (3.5, 7.4))]
G['"'] = [S((2.3, 10.2), (2.3, 7.4)), S((4.7, 10.2), (4.7, 7.4))]
G['`'] = [S((2.8, 10.2), (4.2, 8.6))]
G['('] = [S(arc(6.4, 4.8, 3.6, 7.0, 122, 238))]
G[')'] = [S(arc(0.6, 4.8, 3.6, 7.0, 58, -58))]
G['['] = [S((5.2, 11.2), (2.2, 11.2), (2.2, -1.8), (5.2, -1.8))]
G[']'] = [S((1.8, 11.2), (4.8, 11.2), (4.8, -1.8), (1.8, -1.8))]
G['{'] = [S((5.4, 11.2), (4.2, 11.0), (3.4, 10.0), (3.4, 6.2), (2.0, 4.7), (3.4, 3.2), (3.4, -0.6), (4.2, -1.6), (5.4, -1.8))]
G['}'] = [S((1.6, 11.2), (2.8, 11.0), (3.6, 10.0), (3.6, 6.2), (5.0, 4.7), (3.6, 3.2), (3.6, -0.6), (2.8, -1.6), (1.6, -1.8))]
G['-'] = [S((1.2, 4.6), (5.8, 4.6))]
G['_'] = [S((0, -1.4), (7, -1.4))]
G['+'] = [S((3.5, 1.4), (3.5, 7.8)), S((0.4, 4.6), (6.6, 4.6))]
G['='] = [S((0.5, 3.1), (6.5, 3.1)), S((0.5, 6.1), (6.5, 6.1))]
G['<'] = [S((6.4, 8.4), (0.6, 4.6), (6.4, 0.8))]
G['>'] = [S((0.6, 8.4), (6.4, 4.6), (0.6, 0.8))]
G['/'] = [S((0.6, -1.2), (6.4, 11))]
G['\\'] = [S((0.6, 11), (6.4, -1.2))]
G['|'] = [S((3.5, -2.2), (3.5, 11.2))]
G['*'] = [S((3.5, 9.6), (3.5, 3.6)), S((0.9, 8.1), (6.1, 5.1)), S((6.1, 8.1), (0.9, 5.1))]
G['#'] = [S((2.4, 0), (3.2, 10)), S((4.8, 0), (5.6, 10)), S((0.4, 3.4), (6.9, 3.4)), S((0.1, 6.6), (6.6, 6.6))]
G['%'] = [LOOP(ell(1.7, 8.2, 1.5, 1.8)), LOOP(ell(5.3, 1.8, 1.5, 1.8)), S((6.6, 10), (0.4, 0))]
G['&'] = [S((7, 0), (1.3, 6.3), (0.9, 8.1), (1.8, 9.7), (3.6, 10), (4.7, 8.8), (4.4, 7.2), (0.9, 4.6), (0.1, 2.6), (0.9, 0.7), (2.9, 0), (4.9, 0.8), (6.6, 3.5))]
G['@'] = [LOOP(ell(3.9, 4.6, 1.9, 2.3)), S((5.8, 6.9), (5.8, 3.0), arc(6.6, 3.0, 0.8, 0.8, 180, 360), arc(3.6, 4.4, 3.8, 5.0, 0, 330))]
G['$'] = [S(arc(3.5, 7.2, 3.1, 2.3, 25, 270), arc(3.5, 2.6, 3.3, 2.4, 90, -155)), S((3.5, 11), (3.5, -1))]
G['^'] = [S((1.2, 7.4), (3.5, 10.2), (5.8, 7.4))]
G['~'] = [S((0.4, 4.1), (1.6, 5.3), (3.2, 4.8), (4.2, 4.2), (5.6, 4.0), (6.6, 5.0))]

# ------------------------------------------------------------------ znaki diakrytyczne (polskie i inne w tekstach gry)
def acute(top): return [S((3.1, top + 1.3), (4.7, top + 3.0), w=0.95)]
def grave(top): return [S((2.3, top + 3.0), (3.9, top + 1.3), w=0.95)]
def dotab(top): return [DOT(3.5, top + 2.1, 1.3)]
def diaer(top): return [DOT(2.2, top + 2.1, 1.25), DOT(4.8, top + 2.1, 1.25)]
def circ(top): return [S((1.8, top + 1.3), (3.5, top + 3.0), (5.2, top + 1.3), w=0.95)]
def tilde(top): return [S((1.0, top + 1.6), (2.2, top + 2.6), (3.6, top + 2.0), (4.8, top + 1.5), (6.0, top + 2.5), w=0.95)]
def ogonek(x): return [S((x, 0.1), (x - 0.8, -1.2), (x - 0.5, -2.3), (x + 0.7, -2.6), w=0.95)]
def cedil(): return [S((3.5, 0.1), (3.9, -1.0), (4.5, -1.5), (3.8, -2.5), (2.6, -2.7), w=0.95)]
LC, UC = XH, CAP
compose = {
    'ą': ('a', ogonek(6.6)), 'ć': ('c', acute(LC)), 'ę': ('e', ogonek(4.6)), 'ł': ('l', [S((1.3, 3.8), (5.7, 7.0))]),
    'ń': ('n', acute(LC)), 'ó': ('o', acute(LC)), 'ś': ('s', acute(LC)), 'ź': ('z', acute(LC)), 'ż': ('z', dotab(LC)),
    'Ą': ('A', ogonek(7.0)), 'Ć': ('C', acute(UC)), 'Ę': ('E', ogonek(6.2)), 'Ł': ('L', [S((-0.9, 3.8), (2.7, 6.8))]),
    'Ń': ('N', acute(UC)), 'Ó': ('O', acute(UC)), 'Ś': ('S', acute(UC)), 'Ź': ('Z', acute(UC)), 'Ż': ('Z', dotab(UC)),
    'ä': ('a', diaer(LC)), 'ö': ('o', diaer(LC)), 'ü': ('u', diaer(LC)), 'Ä': ('A', diaer(UC)), 'Ö': ('O', diaer(UC)), 'Ü': ('U', diaer(UC)),
    'é': ('e', acute(LC)), 'è': ('e', grave(LC)), 'ê': ('e', circ(LC)), 'á': ('a', acute(LC)), 'à': ('a', grave(LC)), 'â': ('a', circ(LC)),
    'í': ('ı', acute(LC)), 'ú': ('u', acute(LC)), 'ô': ('o', circ(LC)), 'ñ': ('n', tilde(LC)), 'ç': ('c', cedil()), 'É': ('E', acute(UC)),
    'Ø': ('O', [S((0.3, -0.6), (6.7, 10.6))]), 'ø': ('o', [S((0.4, -0.6), (6.6, 7.6))]),
}
G['ı'] = [S((3.5, 0), (3.5, 7))]
for ch, (base, marks) in compose.items(): G[ch] = G[base] + marks

# grecki (w tekstach: α β γ η θ λ π ψ Δ Σ, µ/μ)
G['α'] = [LOOP(ell(3.1, 3.5, 3.1, 3.5)), S((7.0, 7.0), (6.0, 3.6), (7.2, 0.0))]
G['β'] = [S((0.4, -3), (0.4, 7.4), arc(3.2, 7.6, 2.8, 2.4, 180, -90), (2.2, 5.2), (3.4, 5.2), arc(3.4, 2.6, 3.2, 2.6, 90, -90), (2.6, 0), (0.4, 0.8))]
G['γ'] = [S((0, 7), (1.2, 7), (3.5, 0.4)), S((7, 7), (3.5, 0.4), (2.8, -1.6), (3.5, -3), (4.2, -1.6), (3.5, 0.4))]
G['δ'] = [LOOP(ell(3.5, 3.1, 3.3, 3.1)), S((3.0, 6.1), (1.2, 8.3), (2.0, 10.0), (4.4, 10.3), (6.2, 9.6))]
G['ε'] = [S(arc(3.8, 5.2, 3.0, 1.9, 40, 270), arc(3.8, 1.9, 3.3, 1.9, 90, -130)), S((1.6, 3.8), (4.6, 3.8))]
G['η'] = [S((0, 7), (0.4, 0)), S((0.3, 4.4), arc(3.6, 4.4, 3.2, 2.6, 180, 0), (6.8, -3))]
G['θ'] = [LOOP(ell(3.5, 5, 3.3, 5)), S((0.3, 5), (6.7, 5))]
G['κ'] = [S((0.3, 7), (0.3, 0)), S((6.2, 7), (0.3, 2.8)), S((2.3, 4.2), (6.8, 0))]
G['λ'] = [S((0.3, 0), (3.6, 6.0)), S((0.8, 10.5), (2.2, 10.2), (6.9, 0))]
G['µ'] = [S((0, 7), (0, -3)), S((0, 2.8), arc(3.45, 2.8, 3.45, 2.8, 180, 360), (6.9, 7)), S((6.9, 2.8), (6.9, 0))]
G['μ'] = G['µ']
G['π'] = [S((0, 7), (7, 7)), S((2.0, 7), (1.6, 0)), S((5.2, 7), (5.2, 1.0), (6.6, 0.2))]
G['σ'] = [LOOP(ell(3.2, 3.2, 3.2, 3.2)), S((3.2, 6.4), (7.2, 6.4))]
G['τ'] = [S((0, 7), (7, 7)), S((3.4, 7), (3.4, 1.0), (4.8, 0.1))]
G['φ'] = [LOOP(ell(3.5, 3.5, 3.4, 3.1)), S((3.5, 10.2), (3.5, -3))]
G['ψ'] = [S((0, 7), (0, 3.4), arc(3.5, 3.4, 3.5, 3.0, 180, 360), (7, 7)), S((3.5, 10), (3.5, -3))]
G['ω'] = [S((1.0, 7), arc(1.9, 2.4, 1.9, 2.4, 170, 360), (3.8, 4.4)), S((3.8, 4.4), arc(5.7, 2.4, 1.9, 2.4, 180, 370), (6.4, 7))]
G['Δ'] = [S((0, 0), (3.5, 10), (7, 0), (0, 0))]
G['Σ'] = [S((7, 10), (0, 10), (3.6, 5), (0, 0), (7, 0))]
G['Ω'] = [S((0.4, 0), (2.4, 0), (2.4, 1.2), arc(3.5, 5.6, 3.5, 4.4, 250, -70), (4.6, 1.2), (4.6, 0), (6.6, 0))]
G['Φ'] = [LOOP(ell(3.5, 5, 3.5, 3.0)), S((3.5, 10), (3.5, 0))]
G['Ψ'] = [S((0, 10), (0, 6.2), arc(3.5, 6.2, 3.5, 2.8, 180, 360), (7, 10)), S((3.5, 10), (3.5, 0))]
# typografia i symbole naukowe
G['–'] = [S((0.2, 4.6), (6.8, 4.6))]
G['—'] = [S((-0.8, 4.6), (7.8, 4.6))]
G['−'] = [S((0.6, 4.6), (6.4, 4.6))]
G['·'] = [DOT(3.5, 4.6, 1.35)]
G['•'] = [DOT(3.5, 4.6, 2.6)]
G['…'] = [DOT(1.0, 0.35, 1.3), DOT(3.5, 0.35, 1.3), DOT(6.0, 0.35, 1.3)]
G['„'] = [S((2.6, 0.6), (1.8, -1.9)), S((5.2, 0.6), (4.4, -1.9))]
G['”'] = [S((2.8, 10.2), (2.0, 7.8)), S((5.4, 10.2), (4.6, 7.8))]
G['“'] = [S((2.0, 7.8), (2.8, 10.2)), S((4.6, 7.8), (5.4, 10.2))]
G['‘'] = [S((3.0, 7.8), (3.8, 10.2))]
G['’'] = [S((3.8, 10.2), (3.0, 7.8))]
G['«'] = [S((3.4, 7.2), (0.8, 4.4), (3.4, 1.6)), S((6.4, 7.2), (3.8, 4.4), (6.4, 1.6))]
G['»'] = [S((0.6, 7.2), (3.2, 4.4), (0.6, 1.6)), S((3.6, 7.2), (6.2, 4.4), (3.6, 1.6))]
G['×'] = [S((1.0, 1.4), (6.0, 6.8)), S((6.0, 1.4), (1.0, 6.8))]
G['±'] = [S((3.5, 3.4), (3.5, 8.8)), S((0.6, 6.1), (6.4, 6.1)), S((0.6, 0.8), (6.4, 0.8))]
G['≈'] = [S((0.4, 5.6), (1.6, 6.6), (3.2, 6.1), (4.4, 5.5), (5.6, 5.4), (6.6, 6.3)), S((0.4, 2.8), (1.6, 3.8), (3.2, 3.3), (4.4, 2.7), (5.6, 2.6), (6.6, 3.5))]
G['≥'] = [S((0.6, 8.6), (6.4, 5.6), (0.6, 2.6)), S((0.6, 0.6), (6.4, 0.6))]
G['≤'] = [S((6.4, 8.6), (0.6, 5.6), (6.4, 2.6)), S((0.6, 0.6), (6.4, 0.6))]
G['→'] = [S((0, 4.6), (7, 4.6)), S((4.4, 7.1), (7, 4.6), (4.4, 2.1))]
G['←'] = [S((7, 4.6), (0, 4.6)), S((2.6, 7.1), (0, 4.6), (2.6, 2.1))]
G['↑'] = [S((3.5, 0), (3.5, 10)), S((1.0, 7.4), (3.5, 10), (6.0, 7.4))]
G['↓'] = [S((3.5, 10), (3.5, 0)), S((1.0, 2.6), (3.5, 0), (6.0, 2.6))]
G['↔'] = [S((0, 4.6), (7, 4.6)), S((2.3, 6.8), (0, 4.6), (2.3, 2.4)), S((4.7, 6.8), (7, 4.6), (4.7, 2.4))]
G['°'] = [LOOP(ell(3.5, 8.4, 1.5, 1.5, 20))]
G['▶'] = [S((1.2, 9.2), (6.8, 4.6), (1.2, 0.0), (1.2, 9.2)), S((2.4, 7.0), (4.9, 4.6), (2.4, 2.2), (2.4, 7.0))]
G['★'] = [S(*[(3.5 + (3.6 if i % 2 == 0 else 1.5) * math.sin(math.radians(i * 36)), 4.7 + (3.6 if i % 2 == 0 else 1.5) * math.cos(math.radians(i * 36))) for i in range(11)])]
# indeksy górne i dolne: pomniejszone cyfry i znaki
sup = {'⁰': '0', '¹': '1', '²': '2', '³': '3', '⁴': '4', '⁵': '5', '⁶': '6', '⁷': '7', '⁸': '8', '⁹': '9', '⁺': '+', '⁻': '-'}
sub = {'₀': '0', '₁': '1', '₂': '2', '₃': '3', '₄': '4', '₅': '5', '₆': '6', '₇': '7', '₈': '8', '₉': '9', '₊': '+', '₋': '-'}
for k, b in sup.items(): G[k] = shift(G[b], dx=0, dy=4.6, sc=0.58, ox=3.5, oy=0)
for k, b in sub.items(): G[k] = shift(G[b], dx=0, dy=-2.4, sc=0.58, ox=3.5, oy=0)
G['½'] = shift(G['1'], dx=-1.6, dy=4.8, sc=0.5, ox=3.5) + [S((0.4, 0.6), (6.6, 9.4))] + shift(G['2'], dx=1.8, dy=-0.2, sc=0.5, ox=3.5)
G['¼'] = shift(G['1'], dx=-1.6, dy=4.8, sc=0.5, ox=3.5) + [S((0.4, 0.6), (6.6, 9.4))] + shift(G['4'], dx=1.8, dy=-0.2, sc=0.5, ox=3.5)
G['⅓'] = shift(G['1'], dx=-1.6, dy=4.8, sc=0.5, ox=3.5) + [S((0.4, 0.6), (6.6, 9.4))] + shift(G['3'], dx=1.8, dy=-0.2, sc=0.5, ox=3.5)

# ------------------------------------------------------------------ niedoskonałości: drgania kreski, przesunięcia znaków
def imperfect(ch, strokes):
    rnd = random.Random(ord(ch) * 7919 + 17)
    ph = [rnd.uniform(0, 6.3) for _ in range(4)]
    amp = 0.085
    dx, dy = rnd.uniform(-0.06, 0.06), rnd.uniform(-0.09, 0.09)
    shear = rnd.uniform(-0.018, 0.018)
    res = []
    for kind, pts, w in strokes:
        q = []
        for x, y in pts:
            jx = amp * (math.sin(0.93 * x + 1.31 * y + ph[0]) + 0.5 * math.sin(2.1 * x - 0.77 * y + ph[1]))
            jy = amp * (math.sin(1.17 * x - 0.83 * y + ph[2]) + 0.5 * math.sin(-1.9 * x + 1.4 * y + ph[3]))
            q.append((x + jx + dx + shear * y, y + jy + dy))
        res.append((kind, q, w * rnd.uniform(0.94, 1.06)))
    return res

# ------------------------------------------------------------------ kontury TrueType z kresek
def sub2(a, b): return (a[0] - b[0], a[1] - b[1])
def add2(a, b): return (a[0] + b[0], a[1] + b[1])
def mul2(a, k): return (a[0] * k, a[1] * k)
def nrm(a):
    l = math.hypot(a[0], a[1]); return (a[0] / l, a[1] / l) if l > 1e-9 else (0.0, 0.0)
def area(poly):
    s = 0.0
    for i in range(len(poly)):
        x0, y0 = poly[i][0], poly[i][1]; x1, y1 = poly[(i + 1) % len(poly)][0], poly[(i + 1) % len(poly)][1]
        s += x0 * y1 - x1 * y0
    return s / 2

def disc(c, r):
    # okrąg z 8 łuków kwadratowych, zgodnie z ruchem wskazówek zegara (y w górę)
    k = 1 / math.cos(math.pi / 8); pts = []
    for i in range(8):
        a = -i * math.pi / 4; b = a - math.pi / 8
        pts.append((c[0] + r * math.cos(a), c[1] + r * math.sin(a), True))
        pts.append((c[0] + r * k * math.cos(b), c[1] + r * k * math.sin(b), False))
    return pts

def split_sharp(pts, closed, limit_deg=50):
    """Dzieli łamaną w ostrych narożnikach (tam łączenie okrągłe = krążek), zostawiając gładkie odcinki."""
    n = len(pts)
    if n < 3: return [(pts, False)] if not closed else [(pts + [pts[0]], False)]
    def turn(i):
        a, b, c = pts[(i - 1) % n], pts[i], pts[(i + 1) % n]
        d0, d1 = nrm(sub2(b, a)), nrm(sub2(c, b))
        return math.degrees(math.acos(max(-1, min(1, d0[0] * d1[0] + d0[1] * d1[1]))))
    idx = range(n) if closed else range(1, n - 1)
    cuts = [i for i in idx if turn(i) > limit_deg]
    if closed:
        if not cuts: return [(pts, True)]
        res = []
        for k in range(len(cuts)):
            a, b = cuts[k], cuts[(k + 1) % len(cuts)]
            seg = [pts[(a + j) % n] for j in range(((b - a) % n or n) + 1)]
            res.append((seg, False))
        return res
    res, start = [], 0
    for c in cuts: res.append((pts[start:c + 1], False)); start = c
    res.append((pts[start:], False))
    return res

def stroke_contours(pts, closed, hw):
    out = []
    for piece, pc in split_sharp(pts, closed):
        n = len(piece)
        if n < 2: out.append(disc(piece[0], hw)); continue
        Lp, Rp = [], []
        for i in range(n):
            if pc or 0 < i < n - 1:
                a, b, c = piece[(i - 1) % n], piece[i], piece[(i + 1) % n]
                d0, d1 = nrm(sub2(b, a)), nrm(sub2(c, b))
                n0, n1 = (-d0[1], d0[0]), (-d1[1], d1[0])
                m = nrm(add2(n0, n1)); cs = max(0.5, m[0] * n1[0] + m[1] * n1[1])
                Lp.append(add2(b, mul2(m, hw / cs))); Rp.append(sub2(b, mul2(m, hw / cs)))
            else:
                d = nrm(sub2(piece[1], piece[0])) if i == 0 else nrm(sub2(piece[-1], piece[-2]))
                nn = (-d[1], d[0]); Lp.append(add2(piece[i], mul2(nn, hw))); Rp.append(sub2(piece[i], mul2(nn, hw)))
        segs = n if pc else n - 1
        for i in range(segs):
            j = (i + 1) % n
            if math.hypot(*sub2(piece[j], piece[i])) < 1e-6: continue
            q = [Lp[i], Lp[j], Rp[j], Rp[i]]
            if area(q) > 0: q = q[::-1]
            out.append([(x, y, True) for x, y in q])
        if not pc:
            out.append(disc(piece[0], hw)); out.append(disc(piece[-1], hw))
            for i in range(1, n - 1):
                pass
    # okrągłe łączenia w ostrych narożnikach: końce kawałków już mają krążki
    return out

def glyph_outline(ch):
    strokes = imperfect(ch, G[ch])
    cont = []
    for kind, pts, w in strokes:
        hw = SW * w / 2
        if kind == 'dot': cont.append(disc(pts[0], hw))
        else: cont.extend(stroke_contours(pts, kind == 'closed', hw))
    # do jednostek fontu (z marginesem lewym)
    res = []
    for c in cont:
        res.append([(int(round((x + LSB) * U)), int(round(y * U)), on) for x, y, on in c])
    return res

# ------------------------------------------------------------------ zapis TTF
def pad4(b): return b + b'\0' * ((4 - len(b) % 4) % 4)
def checksum(b):
    b = pad4(b); return sum(struct.unpack('>%dI' % (len(b) // 4), b)) & 0xFFFFFFFF

def glyf_bytes(cont):
    if not cont: return b'', (0, 0, 0, 0), 0, 0
    xs = [p[0] for c in cont for p in c]; ys = [p[1] for c in cont for p in c]
    bbox = (min(xs), min(ys), max(xs), max(ys))
    ends, k = [], 0
    for c in cont: k += len(c); ends.append(k - 1)
    d = struct.pack('>hhhhh', len(cont), *bbox) + struct.pack('>%dH' % len(ends), *ends) + struct.pack('>H', 0)
    flags = bytearray(1 if p[2] else 0 for c in cont for p in c); flags[0] |= 0x40  # OVERLAP_SIMPLE
    d += bytes(flags)
    px = py = 0; xb = bytearray(); yb = bytearray()
    for c in cont:
        for x, y, _ in c:
            xb += struct.pack('>h', x - px); px = x
            yb += struct.pack('>h', y - py); py = y
    d += bytes(xb) + bytes(yb)
    return pad4(d), bbox, k, len(cont)

def build(path, family='MikroScope'):
    chars = sorted(G.keys(), key=lambda c: ord(c))
    order = ['.notdef'] + chars
    notdef = [[(int(U * 1.2), 0, True), (int(U * 1.2), int(U * 9.5), True), (int(U * 7.8), int(U * 9.5), True), (int(U * 7.8), 0, True)][::-1],
              [(int(U * 1.9), int(U * 0.7), True), (int(U * 7.1), int(U * 0.7), True), (int(U * 7.1), int(U * 8.8), True), (int(U * 1.9), int(U * 8.8), True)][::-1][::-1]]
    # zewnętrzny zgodnie z ruchem wskazówek zegara, wewnętrzny przeciwnie
    o = notdef[0];
    if area(o) > 0: notdef[0] = o[::-1]
    i = notdef[1]
    if area(i) < 0: notdef[1] = i[::-1]
    glyphs = [notdef] + [glyph_outline(c) for c in chars]
    adv = int(round(ADV * U))
    glyf = b''; loca = [0]; hmtx = b''
    gmax_pts = gmax_cont = 0
    xmin = ymin = 10 ** 6; xmax = ymax = -10 ** 6
    lsbs = []
    for cont in glyphs:
        data, bbox, npts, ncont = glyf_bytes(cont)
        glyf += data; loca.append(len(glyf))
        gmax_pts = max(gmax_pts, npts); gmax_cont = max(gmax_cont, ncont)
        if cont:
            xmin = min(xmin, bbox[0]); ymin = min(ymin, bbox[1]); xmax = max(xmax, bbox[2]); ymax = max(ymax, bbox[3])
        hmtx += struct.pack('>Hh', adv, bbox[0] if cont else 0); lsbs.append(bbox[0] if cont else 0)
    n = len(glyphs)
    head = struct.pack('>IIIIHHqqhhhhHHhhh', 0x00010000, 0x00010000, 0, 0x5F0F3CF5, 0x000B, UPM, 0, 0, xmin, ymin, xmax, ymax, 0, 8, 2, 1, 0)
    hhea = struct.pack('>IhhhHhhhhhhhhhhhH', 0x00010000, ASC, -DSC, 0, adv, min(lsbs), 0, xmax, 1, 0, 0, 0, 0, 0, 0, 0, n)
    maxp = struct.pack('>IHHHHHHHHHHHHHH', 0x00010000, n, gmax_pts, gmax_cont, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0)
    first = min(ord(c) for c in chars if ord(c) >= 32); last = min(0xFFFF, max(ord(c) for c in chars))
    os2 = struct.pack('>HhHHH' + 'h' * 11 + '10sIIII4sHHHhhhHHIIhhHHH', 4, adv, 400, 5, 0,
                      int(UPM * 0.6), int(UPM * 0.6), 0, int(UPM * 0.15), int(UPM * 0.6), int(UPM * 0.6), 0, int(UPM * 0.45), 50, 300, 0,
                      bytes([2, 11, 5, 9, 2, 2, 2, 2, 2, 4]), 0x00000007 | (1 << 7), 0, 0, 0, b'MIKR', 0x40 | 0x80,
                      first, last, ASC, -DSC, 0, ASC + 60, DSC + 60, 0x00000003, 0, int(XH * U), int(CAP * U), 0, 32, 1)
    # cmap format 4 (Unicode BMP)
    mapping = {ord(c): gi + 1 for gi, c in enumerate(chars) if ord(c) <= 0xFFFF}
    segs = []
    for code in sorted(mapping):
        g = mapping[code]
        if segs and code == segs[-1][1] + 1 and g == segs[-1][2] + (code - segs[-1][0]): segs[-1][1] = code
        else: segs.append([code, code, g])
    segs.append([0xFFFF, 0xFFFF, 1])
    sc = len(segs); p2 = 2 ** int(math.floor(math.log2(sc)))
    ends = [s[1] for s in segs]; starts = [s[0] for s in segs]
    deltas = [((s[2] - s[0]) & 0xFFFF) for s in segs[:-1]] + [1]
    body = struct.pack('>%dH' % sc, *ends) + struct.pack('>H', 0) + struct.pack('>%dH' % sc, *starts) + struct.pack('>%dH' % sc, *deltas) + struct.pack('>%dH' % sc, *([0] * sc))
    sub4 = struct.pack('>HHHHHHH', 4, 14 + len(body), 0, sc * 2, p2 * 2, int(math.log2(p2)), sc * 2 - p2 * 2) + body
    cmap = struct.pack('>HH', 0, 2) + struct.pack('>HHI', 0, 3, 20) + struct.pack('>HHI', 3, 1, 20) + sub4
    # name
    names = {1: family, 2: 'Regular', 3: family + ' 1.0', 4: family + ' Regular', 5: 'Version 1.0', 6: family + '-Regular'}
    recs = b''; strs = b''
    for nid, s in names.items():
        e = s.encode('utf-16-be'); recs += struct.pack('>HHHHHH', 3, 1, 0x409, nid, len(e), len(strs)); strs += e
    name = struct.pack('>HHH', 0, len(names), 6 + 12 * len(names)) + recs + strs
    post = struct.pack('>IIhhIIIII', 0x00030000, 0, -120, 60, 1, 0, 0, 0, 0)
    loca_b = struct.pack('>%dI' % len(loca), *loca)
    tables = {'OS/2': os2, 'cmap': cmap, 'glyf': glyf, 'head': head, 'hhea': hhea, 'hmtx': hmtx, 'loca': loca_b, 'maxp': maxp, 'name': name, 'post': post}
    tags = sorted(tables)
    nt = len(tags); sr = 16 * 2 ** int(math.floor(math.log2(nt)))
    out = struct.pack('>IHHHH', 0x00010000, nt, sr, int(math.log2(sr // 16)), nt * 16 - sr)
    off = 12 + 16 * nt; dirs = b''; blob = b''
    for t in tags:
        d = tables[t]
        dirs += struct.pack('>4sIII', t.encode('ascii'), checksum(d), off + len(blob), len(d))
        blob += pad4(d)
    font = bytearray(out + dirs + blob)
    adj = (0xB1B0AFBA - checksum(bytes(font))) & 0xFFFFFFFF
    hoff = off + sum(len(pad4(tables[t])) for t in tags[:tags.index('head')])
    font[hoff + 8:hoff + 12] = struct.pack('>I', adj)
    open(path, 'wb').write(bytes(font))
    return chars

if __name__ == '__main__':
    out = sys.argv[1]
    chars = build(out)
    print(f'{len(chars)} glyphs -> {out}')
    if len(sys.argv) > 2:
        from PIL import Image, ImageDraw, ImageFont
        f = ImageFont.truetype(out, 44); f2 = ImageFont.truetype(out, 22)
        img = Image.new('RGB', (1500, 900), (60, 62, 60)); d = ImageDraw.Draw(img)
        lines = ['F6691  5.0 kV  X15.0K  2.00µm', 'PH  40X  X450  T×240   20.0µm', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789',
                 'abcdefghijklmnopqrstuvwxyz .,:;!?()[]', 'ąćęłńóśźż ĄĆĘŁŃÓŚŹŻ äüñé Ø', 'αβγδεηθκλμπστφψω ΔΣΩ µ°×±≈≥≤→↔…„”',
                 'pO₂ 1.6 mmHg · HIF-1α · IL-1β · Ca²⁺ · ½ ⅓ ¼']
        y = 20
        for s in lines: d.text((30, y), s, font=f, fill=(240, 240, 235)); y += 70
        d.text((30, y + 10), 'Neutrofil · Fagosomy 3/14 · Ziarnistości 79% · Etap 1/4 – Sfagocytuj 5 bakterii', font=f2, fill=(240, 240, 235))
        d.text((30, y + 50), 'The quick brown fox jumps over the lazy dog – Mikroskop kontrastowo-fazowy 400×', font=f2, fill=(240, 240, 235))
        img.save(sys.argv[2]); print('preview', sys.argv[2])
