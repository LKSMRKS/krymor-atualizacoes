#!/usr/bin/env python3
"""
Costa do Cal maior e mais viva (pedido do usuário, 08/11):
- mapa ×1,6 (140 → 224 m de lado): tudo reposicionado na mesma proporção (portões, zonas, baús, NPCs, monstros);
  as outras referências à Costa (portais do Porto/Vale/Galeria, barco, missões com ponto) acompanham;
- ravina fechada até o penhasco leste: a ponte (tábuas cortadas com machado de ofício) vira o único caminho;
- portão do atalho das Dunas com muro até a borda leste e até o oeste do acampamento: o "atalho" agora é atalho;
- muito mais monstros: goblins (a cada 3 caranguejos, 1 goblin), arqueiros goblins, chefe goblin, javalis;
- árvores secas para cortar tábuas, mais pinheiros, rochas e um acampamento goblin.
Roda uma vez (marca "escala" no mapa); rodar de novo só refaz as peças marcadas "extra".
Uso (raiz do projeto): python3 Tools/expandir_costa.py
"""
import json, math, random
C = 'Content/'
S = 1.6
random.seed(1108)

def load(f): return json.load(open(C + f, encoding='utf-8'))
def save(f, d): open(C + f, 'w', encoding='utf-8').write(json.dumps(d, ensure_ascii=False, indent=2) + '\n')

m = load('map_costa.json')
others = {f: load(f) for f in ['map_porto.json', 'map_vale.json', 'map_galeria.json', 'quests.json', 'quests_novas.json']}

def sc(v): return round(v * S, 2)

if m.get('escala', 1) == 1:
    m['half_size'] = sc(m['half_size'])
    m['spawn_x'], m['spawn_y'] = sc(m['spawn_x']), sc(m['spawn_y'])
    m['safe_radius'] = sc(m['safe_radius'])
    for z in m['zones']:
        for k in ('x', 'y', 'w', 'h'): z[k] = sc(z[k])
    for o in m['obstacles']:
        o['x'], o['y'] = sc(o['x']), sc(o['y'])
        if o['shape'] == 'box':
            if o['w'] >= o['h']: o['w'] = sc(o['w'])
            else: o['h'] = sc(o['h'])
    for g in m['gates']:
        g['x'], g['y'], g['lever_x'], g['lever_y'] = sc(g['x']), sc(g['y']), sc(g['lever_x']), sc(g['lever_y'])
        if g['w'] >= g['h']: g['w'] = sc(g['w'])
        else: g['h'] = sc(g['h'])
    for key in ('chests', 'spawns', 'props', 'portals', 'waypoints', 'npcs', 'objects'):
        for o in m.get(key, []):
            o['x'], o['y'] = sc(o['x']), sc(o['y'])
            if 'alt_points' in o: o['alt_points'] = [[sc(a), sc(b)] for a, b in o['alt_points']]
    # quem aponta para a Costa
    for f in ('map_porto.json', 'map_vale.json', 'map_galeria.json'):
        mm = others[f]
        for p in mm.get('portals', []):
            if p.get('to_map') == 'costa_do_cal': p['to_x'], p['to_y'] = sc(p['to_x']), sc(p['to_y'])
        for n in mm.get('npcs', []):
            for t in n.get('travel', []) or []:
                if t.get('to_map') == 'costa_do_cal': t['to_x'], t['to_y'] = sc(t['to_x']), sc(t['to_y'])
        if mm.get('exit_map') == 'costa_do_cal' and 'exit_x' in mm: mm['exit_x'], mm['exit_y'] = sc(mm['exit_x']), sc(mm['exit_y'])
    for f in ('quests.json', 'quests_novas.json'):
        for q in others[f]:
            if q.get('map') == 'costa_do_cal' and 'x' in q:
                q['x'], q['y'] = sc(q['x']), sc(q['y'])
                if 'radius' in q: q['radius'] = sc(q['radius'])
    m['escala'] = S

H = m['half_size']
# tira o que este script pôs antes
for key in ('obstacles', 'spawns', 'props', 'objects'):
    m[key] = [o for o in m.get(key, []) if not o.get('extra')]
X = {'extra': True}

# ---------------- muros que fecham os atalhos
rav = [o for o in m['obstacles'] if o['kind'] == 'ravina']
ry = rav[0]['y']; east_end = max(o['x'] + o['w'] / 2 for o in rav)
edge = H - 3.5
m['obstacles'].append({'kind': 'ravina', 'shape': 'box', 'x': round((east_end + edge) / 2, 2), 'y': ry, 'w': round(edge - east_end + 1, 2), 'h': 3.8, 'r': 0, 'height': 1.2, **X})
gate = [g for g in m['gates'] if g['id'] == 'atalho_dunas'][0]
gy = gate['y']
walls = [o for o in m['obstacles'] if o['kind'] == 'muro' and abs(o['y'] - gy) < 0.5]
w_east = max(o['x'] + o['w'] / 2 for o in walls); w_west = min(o['x'] - o['w'] / 2 for o in walls)
m['obstacles'].append({'kind': 'muro', 'shape': 'box', 'x': round((w_east + edge) / 2, 2), 'y': gy, 'w': round(edge - w_east + 0.5, 2), 'h': 1.2, 'r': 0, 'height': 3.2, **X})
# a ampliação abriu uma fresta entre o paredão da Gruta e a borda oeste: fecha
gr = [o for o in m['obstacles'] if o['kind'] == 'penhasco' and o['shape'] == 'box' and o['w'] > o['h'] and o['y'] < 0 and o['x'] < -40 and abs(o['y']) < H - 10]
for o in gr:
    left = o['x'] - o['w'] / 2
    if left > -H + 3:
        m['obstacles'].append({'kind': 'penhasco', 'shape': 'box', 'x': round((left + (-H + 2)) / 2, 2), 'y': o['y'], 'w': round(left + H - 1.5, 2), 'h': o['h'], 'r': 0, 'height': o.get('height', 6), **X})
west_stop = -52.0
m['obstacles'].append({'kind': 'muro', 'shape': 'box', 'x': round((w_west + west_stop) / 2, 2), 'y': gy, 'w': round(w_west - west_stop + 0.5, 2), 'h': 1.2, 'r': 0, 'height': 3.2, **X})

# ---------------- utilidades de lugar livre
def solids():
    out = []
    for o in m['obstacles']:
        if o.get('solid', True) is False: continue
        out.append(('c', o['x'], o['y'], o['r']) if o['shape'] == 'circle' else ('b', o['x'], o['y'], o['w'] / 2, o['h'] / 2))
    for g in m['gates']: out.append(('b', g['x'], g['y'], g['w'] / 2, g['h'] / 2))
    for p in m['props']: out.append(('c', p['x'], p['y'], 1.2))
    for p in m['portals']: out.append(('c', p['x'], p['y'], 4))
    for c in m['chests']: out.append(('c', c['x'], c['y'], 2))
    for n in m['npcs']: out.append(('c', n['x'], n['y'], 2.5))
    for o in m['objects']: out.append(('c', o['x'], o['y'], 2))
    for sp in m['spawns']: out.append(('c', sp['x'], sp['y'], 1.5))
    return out
def free(x, y, r):
    if abs(x) > H - 6 or abs(y) > H - 6: return False
    for s in solids():
        if s[0] == 'c':
            if (x - s[1]) ** 2 + (y - s[2]) ** 2 < (s[3] + r) ** 2: return False
        elif abs(x - s[1]) < s[3] + r and abs(y - s[2]) < s[4] + r: return False
    return True
def zone(name): return [z for z in m['zones'] if z['name'].startswith(name)][0]
SAFE = [z for z in m['zones'] if z['kind'] in ('seguro', 'refugio')]
def in_safe(x, y): return any(abs(x - z['x']) < z['w'] / 2 + 4 and abs(y - z['y']) < z['h'] / 2 + 4 for z in SAFE) or \
    (x - m['spawn_x']) ** 2 + (y - m['spawn_y']) ** 2 < (m['safe_radius'] + 6) ** 2
def pick_in(z, r, tries=400, margin=4, avoid=()):
    for _ in range(tries):
        x = random.uniform(z['x'] - z['w'] / 2 + margin, z['x'] + z['w'] / 2 - margin)
        y = random.uniform(z['y'] - z['h'] / 2 + margin, z['y'] + z['h'] / 2 - margin)
        if free(x, y, r) and not in_safe(x, y) and all((x - a) ** 2 + (y - b) ** 2 > 36 for a, b in avoid): return round(x, 1), round(y, 1)
    return None

# ---------------- cenário: pinheiros, rochas, acampamento goblin
for zn, trees, rocks in [('Areal', 8, 6), ('Dunas', 14, 10), ('Ruínas', 8, 8), ('Baía', 4, 4)]:
    z = zone(zn)
    for _ in range(trees):
        p = pick_in(z, 2.5)
        if p: m['props'].append({'kind': 'pinheiros', 'x': p[0], 'y': p[1], 'yaw': random.randint(0, 359), **X})
    for _ in range(rocks):
        p = pick_in(z, 3)
        if p: m['obstacles'].append({'kind': 'rocha', 'shape': 'circle', 'x': p[0], 'y': p[1], 'w': 0, 'h': 0, 'r': round(random.uniform(1.0, 2.2), 1), 'height': round(random.uniform(1.4, 2.8), 1), **X})
dunas = zone('Dunas')
camp = pick_in({'x': dunas['x'] - 8, 'y': dunas['y'] - 10, 'w': 24, 'h': 24}, 7) or (dunas['x'], dunas['y'])
for dx, dy, k in [(-3.5, -2, 'tenda'), (3.5, -2.5, 'tenda'), (0, 3, 'fogueira'), (-5, 3, 'barril'), (5, 3.5, 'caixotes'), (0, -6, 'estacas')]:
    m['props'].append({'kind': k, 'x': round(camp[0] + dx, 1), 'y': round(camp[1] + dy, 1), 'yaw': random.randint(0, 359), **X})

# ---------------- árvores secas para tábuas (machado de ofício)
trees = []
for zn, n in [('Areal', 3), ('Dunas', 5)]:
    for _ in range(n):
        p = pick_in(zone(zn), 2.5, avoid=trees)
        if p: trees.append(p)
for i, (x, y) in enumerate(trees):
    m['objects'].append({'id': f'costa_arvore{i}', 'kind': 'veio', 'x': x, 'y': y, 'label': 'Árvore seca (machado)', 'yields': 'mat_tabua',
                         'yield_min': 1, 'yield_max': 2, 'respawn_seconds': 900, 'requires_flag': 'tool:machado', **X})

# ---------------- monstros: mais e mais variados
spawns = m['spawns']; n_extra = 0
def add(monster, x, y, level, group, respawn=25, chance=None):
    global n_extra
    n_extra += 1
    sp = {'id': f'x{n_extra}', 'monster': monster, 'x': x, 'y': y, 'level': level, 'group': group, 'respawn_seconds': respawn, **X}
    spawns.append(sp)
# monstro original em cima do novo trecho da ravina: passa para o lado sul
for sp in spawns:
    if abs(sp['y'] - ry) < 3.5 and sp['x'] > east_end - 1: sp['y'] = round(ry + 4.5, 1)
# novos grupos longe dos antigos e das moitas/veios (dá para coletar em paz se limpar a área)
far = [(s['x'], s['y']) for s in spawns] + [(o['x'], o['y']) for o in m['objects']]
used = list(far)
def pick_far(z, r):
    for _ in range(600):
        c = pick_in(z, r, tries=1)
        if c and all((c[0] - a) ** 2 + (c[1] - b) ** 2 > 100 for a, b in used): return c
    return None
def place(zn, monster, level, count, group_prefix, per_group=1, respawn=25):
    z = zone(zn)
    for gi in range(count):
        c = pick_far(z, 2)
        if c is None: continue
        used.append(c)
        for k in range(per_group):
            x, y = (c[0] + random.uniform(-2.5, 2.5), c[1] + random.uniform(-2.5, 2.5)) if k else c
            if not free(x, y, 0.8): x, y = c
            add(monster if isinstance(monster, str) else monster[k % len(monster)], round(x, 1), round(y, 1), level, f'{group_prefix}{gi}', respawn)
# Areal (1–3): caranguejinhos em trios com 1 goblin
place('Areal', ['monster_r0_caranguejinho', 'monster_r0_caranguejinho', 'monster_r0_caranguejinho', 'monster_r1_goblin_saqueador'], 2, 6, 'xa', per_group=4, respawn=22)
# Dunas (4–6): caranguejos, javalis, bando goblin com arqueiros e chefe no acampamento
place('Dunas', ['monster_r1_caranguejo_de_cal', 'monster_r1_caranguejo_de_cal', 'monster_r1_goblin_saqueador'], 4, 5, 'xd', per_group=3)
place('Dunas', 'monster_r1_javali_duna', 5, 4, 'xj')
for k, (dx, dy, mon, lv) in enumerate([(-2, -6, 'monster_r1_goblin_saqueador', 5), (2, -6, 'monster_r1_goblin_saqueador', 5),
                                        (-6, 0, 'monster_r1_goblin_arqueiro', 5), (6, 0, 'monster_r1_goblin_arqueiro', 5),
                                        (0, -1, 'monster_r1_goblin_chefe', 7)]):
    add(mon, round(camp[0] + dx, 1), round(camp[1] + dy, 1), lv, 'xcamp', 60 if 'chefe' in mon else 35)
# Ruínas (7–9): arqueiros goblins e javalis grandes
place('Ruínas', ['monster_r1_goblin_arqueiro', 'monster_r1_goblin_saqueador'], 8, 4, 'xr', per_group=2)
place('Ruínas', 'monster_r1_javali_duna', 8, 2, 'xrj')
save('map_costa.json', m)
for f, d in others.items(): save(f, d)
print('Costa: lado', 2 * H, 'm,', len(m['spawns']), 'monstros,', len(trees), 'árvores de tábua')
