#!/usr/bin/env python3
"""Gera os tomos de habilidade (skills_tomos, consumables_tomos, shops_tomos, missões do primeiro tomo) e liga as lojas dos mentores no Porto. Rodar da raiz do projeto."""
import json
C='Content/'
def save(f,d): open(C+f,'w',encoding='utf-8').write(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
def sk(id,cls,name,mode,rng,rad,cd,cost,coef,eff,cast,rec,desc):
    return {"id":id,"class_id":cls,"display_name":name,"unlock_level":1,"target_mode":mode,"range":rng,"radius":rad,"cooldown_ms":cd,
            "resource_cost":cost,"resource_generated":0,"coefficient":coef,"effects":eff,"cast_ms":cast,"recovery_ms":rec,"description":desc}
W0={"move_mult_windup":0,"move_mult_recovery":0.5}
skills=[
 # Guerreiro
 sk("skill_guerreiro_giro","guerreiro","Giro de Lâmina","nova",0,3.0,8000,25,1.3,{"stagger":1,**W0},350,450,"Gira com a arma: acerta todos em volta (3 m)."),
 sk("skill_guerreiro_salto","guerreiro","Salto Esmagador","dash",6.5,0,10000,25,1.5,{"width":1.6,"stagger":1,**W0},350,500,"Salta 6,5 m e esmaga quem estiver no caminho."),
 sk("skill_guerreiro_furia_colosso","guerreiro","Fúria do Colosso","nova",0,5.5,60000,50,3.0,{"stagger":1,**W0},700,700,"Final: crava a arma no chão; a terra explode em volta (5,5 m)."),
 # Mago
 sk("skill_mago_lanca_gelo","mago","Lança de Gelo","projectile",18,0.5,6000,20,1.4,{"projectile_speed":26,"pierce":1,**W0},300,350,"Lança que atravessa todos os inimigos da linha."),
 sk("skill_mago_chuva_meteoros","mago","Chuva de Meteoros","ground",14,4.0,12000,40,2.4,{"stagger":1,**W0},800,400,"Pedras em brasa caem no ponto mirado (raio 4 m)."),
 sk("skill_mago_cometa","mago","Cometa","ground",16,5.5,60000,60,4.0,{"stagger":1,**W0},1100,500,"Final: um cometa cai do céu no ponto mirado (raio 5,5 m)."),
 # Bruxa
 sk("skill_bruxa_espinhos","bruxa","Espinhos da Terra","ground",12,3.0,8000,25,1.5,{"stagger":1,**W0},450,350,"Espinhos brotam do chão no ponto mirado."),
 sk("skill_bruxa_floresta_viva","bruxa","Floresta Viva","heal_area",0,10,60000,50,2.5,{"restore_resource_pct":0.3,**W0},700,500,"Final: a floresta cura todo o grupo em volta (10 m) e devolve fôlego/mana."),
 # Arqueiro
 sk("skill_arqueiro_rajada","arqueiro","Rajada Tripla","projectile",18,0.4,5000,15,0.75,{"projectile_speed":28,"projectile_count":3,"move_mult_windup":0.3,"move_mult_recovery":0.7},250,300,"Três flechas rápidas em leque."),
 sk("skill_arqueiro_flecha_perfurante","arqueiro","Flecha Perfurante","projectile",26,0.55,8000,35,2.2,{"projectile_speed":34,"pierce":1,"stagger":1,**W0},900,350,"Tiro carregado: a flecha atravessa todos os inimigos da linha."),
 sk("skill_arqueiro_chuva","arqueiro","Chuva de Flechas","ground",16,3.5,10000,35,1.8,{"stagger":1,**W0},600,350,"Marca uma área: flechas caem do céu."),
 sk("skill_arqueiro_fenix","arqueiro","Fênix de Fogo","projectile",24,1.1,60000,60,3.2,{"projectile_speed":14,"pierce":1,"explode_radius":2.5,"explode_ratio":0.6,**W0},800,500,"Final: uma fênix de fogo voa à frente queimando tudo pelo caminho."),
 sk("skill_arqueiro_serpente","arqueiro","Serpente do Mar","cone",11,0,75000,60,2.8,{"arc_degrees":80,"stagger":1,**W0},900,600,"Final: uma serpente do mar ergue um tsunami à frente (11 m)."),
]
save('skills_tomos.json',skills)
names={x['id']:x['display_name'] for x in skills}
for f in ['skills.json','skills_novas.json']:
    for x in json.load(open(C+f,encoding='utf-8')): names[x['id']]=x['display_name']
# (skill, classe, nível, preço)
T=[("skill_guerreiro_golpe_de_cunha","guerreiro",1,200),("skill_guerreiro_guarda_de_escudo","guerreiro",3,300),("skill_guerreiro_giro","guerreiro",6,1200),
   ("skill_guerreiro_salto","guerreiro",10,3500),("skill_guerreiro_furia_colosso","guerreiro",14,9000),
   ("skill_mago_explosao_de_cinzas","mago",1,200),("skill_mago_passo_etereo","mago",3,300),("skill_mago_lanca_gelo","mago",6,1200),
   ("skill_mago_chuva_meteoros","mago",10,3500),("skill_mago_cometa","mago",14,9000),
   ("skill_bruxa_toque_alquimico","bruxa",1,200),("skill_bruxa_barreira_de_eter","bruxa",3,300),("skill_bruxa_aura_primal","bruxa",5,700),
   ("skill_bruxa_espinhos","bruxa",8,2000),("skill_bruxa_floresta_viva","bruxa",14,9000),
   ("skill_arqueiro_emboscada","arqueiro",1,200),("skill_arqueiro_passos_fantasma","arqueiro",3,300),("skill_arqueiro_olhar_do_cacador","arqueiro",5,700),
   ("skill_arqueiro_rajada","arqueiro",6,1200),("skill_arqueiro_flecha_perfurante","arqueiro",8,2000),("skill_arqueiro_chuva","arqueiro",10,3500),
   ("skill_arqueiro_fenix","arqueiro",14,9000),("skill_arqueiro_serpente","arqueiro",16,12000)]
# cada classe aprende de um objeto próprio (estilo MU): Guerreiro = esfera, Mago = livro, Bruxa = grimório, Arqueiro = pergaminho
KIND={"guerreiro":"Esfera","mago":"Livro","bruxa":"Grimório","arqueiro":"Pergaminho"}
WHO={"guerreiro":"ao Guerreiro","mago":"ao Mago","bruxa":"à Bruxa","arqueiro":"ao Arqueiro"}
tomes=[]; shops={}
for skid,cls,lvl,price in T:
    tid="tomo_"+skid[len("skill_"):]
    tomes.append({"id":tid,"display_name":KIND[cls]+": "+names[skid],"heal_percent_max_health":0,"heal_duration_ms":0,"cooldown_group":"tomo",
                  "cooldown_ms":0,"stack_limit":5,"price":price,"classes":[cls],"teach_skill":skid,"min_level":lvl,
                  "summary":f"Ensina {WHO[cls]}: {names[skid]} (nível {lvl}+). Use na mochila; depois escolha a tecla no Livro de Poderes (K)."})
    shops.setdefault(cls,[]).append(tid)
# nível 2 (pedido do usuário, 09/11): depois da troca de classe (nível 15 desde 11/11), a mesma esfera "II" fortalece o poder já aprendido
for skid,cls,lvl,price in T:
    tid="tomo_"+skid[len("skill_"):]+"_ii"
    tomes.append({"id":tid,"display_name":KIND[cls]+" II: "+names[skid],"heal_percent_max_health":0,"heal_duration_ms":0,"cooldown_group":"tomo",
                  "cooldown_ms":0,"stack_limit":5,"price":max(6000,price*5),"classes":[cls],"upgrade_skill":skid,"min_level":15,
                  "summary":f"Nível 2 de {names[skid]}: +40% de força e recarga 20% menor. Só depois da troca de classe (nível 15); precisa já saber o poder."})
    shops.setdefault(cls,[]).append(tid)
save('consumables_tomos.json',tomes)
lab={"guerreiro":"Biblioteca do Mestre Dorval","mago":"Grimórios de Isaura","bruxa":"Cadernos de Hazel","arqueiro":"Manuais de Kaelen"}
save('shops_tomos.json',[{"id":"loja_tomos_"+c,"name":lab[c],"items":v} for c,v in shops.items()])
mentor={"guerreiro":("mestre_dorval","q_tomo_g","O Primeiro Golpe","Antes de qualquer livro, prove que aguenta. Derrube 5 Caranguejinhos no Areal (portão norte) e eu te dou o Tomo do Golpe de Cunha."),
        "mago":("isaura_arcanista","q_tomo_m","A Primeira Chama","Magia se aprende fazendo. Derrube 5 Caranguejinhos no Areal (portão norte) com suas faíscas e o Tomo da Explosão é seu."),
        "bruxa":("bruxa_hazel","q_tomo_b","As Mãos que Curam","Sinta o éter na luta. Derrube 5 Caranguejinhos no Areal (portão norte) e eu te dou o caderno do Toque Alquímico."),
        "arqueiro":("batedor_kaelen","q_tomo_a","O Primeiro Rastro","Mostre pontaria. Derrube 5 Caranguejinhos no Areal (portão norte) e o Manual da Emboscada é seu.")}
first={"guerreiro":"tomo_guerreiro_golpe_de_cunha","mago":"tomo_mago_explosao_de_cinzas","bruxa":"tomo_bruxa_toque_alquimico","arqueiro":"tomo_arqueiro_emboscada"}
qs=json.load(open(C+'quests_novas.json',encoding='utf-8'))
qs=[q for q in qs if not q['id'].startswith('q_tomo_')]
# variedade (pendência "menos mate caranguejo"): cada mentor pede uma coisa diferente no Areal
AREAL="Areal da Maré (Costa do Cal, portão norte): os esqueletos de goblin rondam entre os caranguejos. Depois, volte ao seu mentor no Porto."
prova={"guerreiro":dict(kind="kill",targets=["monster_r1_goblin_esqueleto"],count=3,hint=AREAL,
           description="Um guerreiro aprende batendo em quem bate de volta. Derrube 3 Esqueletos de Goblin no Areal (portão norte) e a Esfera do Golpe de Cunha é sua."),
       "mago":dict(kind="collect",targets=["mat_retalho"],count=2,
           hint="Retalhos caem dos goblins e esqueletos de goblin do Areal da Maré (Costa do Cal, portão norte). Traga 2 ao seu mentor no Porto.",
           description="Magia se aprende fazendo — e todo mago precisa de pano para os pergaminhos. Traga 2 Retalhos (caem dos goblins e esqueletos de goblin do Areal) e o Tomo da Explosão é seu."),
       "bruxa":dict(kind="kill",targets=["monster_r0_caranguejinho","monster_r1_goblin_esqueleto"],count=6,hint=AREAL,
           description="Sinta o éter na luta. Derrube 6 criaturas no Areal (caranguejinhos ou esqueletos de goblin) e eu te dou o caderno do Toque Alquímico."),
       "arqueiro":dict(kind="kill",targets=["monster_r1_goblin_esqueleto"],count=4,hint=AREAL,
           description="Mostre pontaria. Derrube 4 Esqueletos de Goblin no Areal (portão norte) — de longe, sem deixar que cheguem perto — e o Manual da Emboscada é seu.")}
for c,(npc,qid,name,desc) in mentor.items():
    pv=prova[c]
    qs.insert(0,{"id":qid,"name":name,"giver":npc,"kind":pv["kind"],"targets":pv["targets"],"count":pv["count"],"description":pv["description"],
                 "hint":pv["hint"],
                 "min_level":1,"class":c,"reward_xp":120,"reward_coins":10,"reward_items":[first[c]],"category":"historia","map":"costa_do_cal"})
save('quests_novas.json',qs)
p=json.load(open(C+'map_porto.json',encoding='utf-8'))
greet={"mestre_dorval":"Força sem técnica é barulho. Venha: tenho livros de combate — e uma prova para o primeiro.",
       "isaura_arcanista":"Faíscas qualquer um solta. Meus grimórios ensinam o resto — comece pela prova da primeira chama.",
       "bruxa_hazel":"Cajado é muleta. A cura vem das mãos e da terra; meus cadernos ensinam. Comece pela prova.",
       "batedor_kaelen":"Quem vê primeiro, vive mais. Meus manuais ensinam a caçar; comece pela prova do primeiro rastro."}
shopof={"mestre_dorval":"loja_tomos_guerreiro","isaura_arcanista":"loja_tomos_mago","bruxa_hazel":"loja_tomos_bruxa","batedor_kaelen":"loja_tomos_arqueiro"}
for n in p['npcs']:
    if n['id'] in shopof: n['shop']=shopof[n['id']]; n['greeting']=greet[n['id']]
open(C+'map_porto.json','w',encoding='utf-8').write(json.dumps(p,ensure_ascii=False,indent=2)+'\n')
print(len(skills),'skills',len(tomes),'tomos')
