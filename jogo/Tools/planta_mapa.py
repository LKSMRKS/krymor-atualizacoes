#!/usr/bin/env python3
# Planta vista de cima de um mapa (zonas, monstros por nível, portais, NPCs). Uso: python3 Tools/planta_mapa.py Content/map_costa.json saida.png
import json,sys,collections
import matplotlib; matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Circle
f=sys.argv[1]; out=sys.argv[2]
m=json.load(open(f)); H=m['half_size']
mons={}
for fn in ['monsters.json','monsters_bestiario.json','monsters_novos.json']:
    for x in json.load(open('/home/claude/proj/Content/'+fn)): mons[x['id']]=x
fig,ax=plt.subplots(figsize=(12,12),dpi=110)
ax.set_facecolor('#e8dcc0')
kc={'seguro':'#7fd17f','fraco':'#d9e7a0','medio':'#f2d27a','forte':'#f0a060','perigoso':'#e06050','chefe':'#c03030','refugio':'#7fd1c0','segredo':'#b090e0','caminho':'#ccbb99','elite':'#e08040'}
for z in m['zones']:
    ax.add_patch(Rectangle((z['x']-z['w']/2,z['y']-z['h']/2),z['w'],z['h'],fc=kc.get(z['kind'],'#ddd'),alpha=0.45,ec='k',lw=0.5))
    ax.text(z['x'],z['y']+z['h']/2-3,z.get('name',''),ha='center',fontsize=8,weight='bold')
for o in m['obstacles']:
    if o.get('deco'): continue
    c={'penhasco':'#555','agua':'#4a7fc0','mar':'#3a6fb0','rocha':'#777'}.get(o['kind'],'#8a6a4a')
    if o['shape']=='box': ax.add_patch(Rectangle((o['x']-o['w']/2,o['y']-o['h']/2),o['w'],o['h'],fc=c,alpha=0.7))
    else: ax.add_patch(Circle((o['x'],o['y']),o['r'],fc=c,alpha=0.7))
cmap=plt.get_cmap('turbo')
groups=collections.defaultdict(list)
for s in m['spawns']: groups[(s['monster'],round(s['x']),round(s['y']))].append(s)
for (mid,x,y),ss in groups.items():
    lv=ss[0]['level']; d=mons.get(mid,{})
    ax.scatter(x,y,s=20+12*len(ss),c=[cmap(min(lv,16)/16)],edgecolors='k',linewidths=0.4,marker='*' if d.get('boss') else 'o',zorder=5)
    ax.text(x+1.2,y+0.8,f"{d.get('species_name',mid)[:14]} {lv}"+(f"x{len(ss)}" if len(ss)>1 else ''),fontsize=5)
for p in m.get('portals',[]):
    ax.add_patch(Circle((p['x'],p['y']),p['radius']*1.5,fc='#a050ff',ec='k')); ax.text(p['x'],p['y']-5,'→ '+p['label'],ha='center',fontsize=8,color='#5010a0',weight='bold')
ax.add_patch(Circle((m['spawn_x'],m['spawn_y']),m['safe_radius'],fc='none',ec='#00a000',lw=2))
for n in m.get('npcs',[]): ax.plot(n['x'],n['y'],'s',c='#0060ff'); ax.text(n['x']+1.5,n['y'],n['name'].split(',')[0],fontsize=6,color='#0040c0')
for g in m.get('gates',[]): ax.add_patch(Rectangle((g['x']-g['w']/2,g['y']-g['h']/2),g['w'],g['h'],fc='#ff00aa'))
for o in m.get('objects',[]):
    if o.get('kind')!='veio': ax.plot(o['x'],o['y'],'D',c='#a000a0',ms=4)
ax.set_xlim(-H,H); ax.set_ylim(-H,H); ax.set_aspect('equal')
ax.set_title(m['display_name']+' — vista de cima (norte = portão do Porto)')
sm=plt.cm.ScalarMappable(cmap=cmap,norm=plt.Normalize(0,16)); plt.colorbar(sm,ax=ax,fraction=0.03,label='nível do monstro')
plt.savefig(out,bbox_inches='tight')
