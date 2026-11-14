"""Marcos das cidades novas (13/11): Observatório de Astrália (torre do Merlin, a porta leva ao interior torre_merlin).
Uso: ACERVO_OUT=... python3 build_cidades.py [peça ...]"""
import sys; sys.path.insert(0, "/home/claude/cinzas/acervo")
from kit_acervo import *


def observatorio():
    """Torre redonda de pedra clara (Astrália), cúpula azul-noite com fenda e luneta de bronze, porta em arco virada para -Y
    (= frente do prop com yaw 0), janelas altas, faixa de estrelas douradas e escadinha. Pé: 7 m; altura total ~15 m."""
    K.reset(); r = random.Random(5)
    mp = marmore(); mb = bronze(); mo = ouro(); mw = madeira_velha(); objs = []
    cupula = K.m_plain("cupula_noite", "#22305e", .55, metal=.35, bumpscale=20)
    buraco = K.m_plain("buraco", "#0e0a10", .9)
    R0, H = 3.2, 10.0
    # base escalonada
    for k, (rr, h) in enumerate(((3.7, .35), (3.5, .3))):
        objs.append(cil(f"base{k}", rr, h, (0, 0, h / 2 + k * .35), mp, v=32))
    # corpo em fiadas, afinando de leve
    fi = 14
    for k in range(fi):
        z0 = .65 + k * (H - .65) / fi; R = R0 - .35 * k / fi; n = 18
        for i in range(n):
            a = (i + .5 * (k % 2)) / n * math.tau
            o = B(f"f{k}_{i}", (2 * R * math.tan(math.pi / n) * 1.03, .45, (H - .65) / fi * .96), (math.cos(a) * R, math.sin(a) * R, z0 + (H - .65) / fi / 2), mp, (0, 0, math.degrees(a) + 90), .03)
            objs.append(o)
    corpo = objs[2:]
    for x in bpy.context.view_layer.objects: x.select_set(False)
    for o in corpo: K.apply_all(o)
    for x in bpy.context.view_layer.objects: x.select_set(False)
    for o in corpo: o.select_set(True)
    bpy.context.view_layer.objects.active = corpo[0]; bpy.ops.object.join(); t = bpy.context.view_layer.objects.active
    disp(t, .015, .4, 3); objs = objs[:2] + [t]
    # cinta de bronze com estrelas e luas no topo do corpo
    objs.append(cil("cinta", R0 - .3, .4, (0, 0, H - .1), mb, v=40))
    for i in range(16):
        a = i / 16 * math.tau; c = Vector((math.cos(a) * (R0 - .08), math.sin(a) * (R0 - .08), H - .1))
        if i % 2:
            s = K.crystal(f"estr{i}", mo, .11, .02, .5, 5, seed=i); s.location = c; s.rotation_euler = (math.pi / 2, 0, a + math.pi / 2); objs.append(s)
        else:
            objs.append(toro(f"lua{i}", .1, .025, tuple(c), mo, (math.pi / 2, 0, a + math.pi / 2), 12, 5))
    # cúpula com fenda e luneta saindo
    cp = esfera("cupula", (0, 0, H + .1), (R0 - .25, R0 - .25, R0 - .4), cupula, 32, 16); cortar_abaixo(cp, H + .1); objs.append(cp)
    for i in range(12):
        a = i / 12 * math.tau
        objs.append(T(f"nervura{i}", [(math.cos(a) * (R0 - .2), math.sin(a) * (R0 - .2), H + .1), (math.cos(a) * 2.1, math.sin(a) * 2.1, H + 1.85), (0, 0, H + 2.85)], .045, .03, mb, 5))
    objs.append(B("fenda", (.9, 3.2, 2.4), (0, -1.2, H + 1.6), buraco, (35, 0, 0), .0))
    objs.append(T("luneta", [(0, -.6, H + 1.4), (0, -2.2, H + 2.6), (0, -3.1, H + 3.25)], .32, .22, mb, 16))
    objs.append(cil("lente", .24, .06, (0, -3.12, H + 3.27), K.m_glass("lente_azul", "#9fd6ff"), (math.radians(53), 0, 0)))
    objs.append(esfera("pinaculo", (0, 0, H + 2.95), (.18, .18, .18), mo, 12, 8))
    objs.append(T("haste_ponta", [(0, 0, H + 3.05), (0, 0, H + 3.7)], .03, .015, mo, 6))
    st = K.crystal("estrela_topo", mo, .28, .05, .5, 5, seed=3); st.location = (0, 0, H + 3.95); st.rotation_euler = (math.pi / 2, 0, 0); objs.append(st)
    # porta em arco (frente = -Y), batente e degraus
    objs.append(B("porta", (1.4, .3, 2.2), (0, -R0 + .05, 1.75), K.m_plain("porta_azul", "#26304a", .8)))
    objs.append(toro("arco", .78, .12, (0, -R0 - .02, 2.85), mp, (math.pi / 2, 0, 0), 20, 6))
    for k in range(3): objs.append(B(f"degrau{k}", (2.2 - k * .3, .45, .2), (0, -R0 - .55 + k * .2, .1 + k * .2), mp))
    for s in (-1, 1): objs.append(B(f"batente{s}", (.18, .4, 2.3), (s * .78, -R0 + .02, 1.8), mp))
    # janelas altas em espiral (sobem como a escada de dentro)
    for i in range(7):
        a = -math.pi / 2 + .9 + i * .95; z = 2.6 + i * 1.0; R = R0 - .3 * z / H
        objs.append(B(f"jan{i}", (.45, .5, .9), (math.cos(a) * R, math.sin(a) * R, z), buraco, (0, 0, math.degrees(a) + 90)))
        objs.append(B(f"peit{i}", (.6, .55, .1), (math.cos(a) * (R + .05), math.sin(a) * (R + .05), z - .5), mp, (0, 0, math.degrees(a) + 90)))
    # estandartes azuis com estrela dos lados da porta
    for s in (-1, 1):
        x = s * 1.7
        objs.append(T(f"mastro{s}", [(x, -R0 - .2, 0), (x, -R0 - .2, 3.6)], .04, .035, mw, 6))
        objs.append(B(f"bandeira{s}", (.7, .03, 1.3), (x, -R0 - .22, 2.75), K.m_plain("pano_azul", "#2b3f86", .9)))
        e = K.crystal(f"estr_b{s}", mo, .16, .02, .5, 5, seed=s + 9); e.location = (x, -R0 - .26, 2.8); e.rotation_euler = (math.pi / 2, 0, 0); objs.append(e)
    tingir(objs[:3], 2)
    luz = [chama("lampiao_l", (-1.0, -R0 - .3, 2.4), cor="#bcd8ff", forca=6.0), chama("lampiao_r", (1.0, -R0 - .3, 2.4), cor="#bcd8ff", forca=6.0)]
    exp("cenario", "astralia_observatorio", objs, tex=2048, tris=16000, extra=luz, caixas=[((7.4, 7.4, 10.0), (0, 0, 5.0))])


PECAS = dict(observatorio=observatorio)

if __name__ == "__main__":
    for nome in (sys.argv[1:] or PECAS): PECAS[nome]()
