using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 14/11 ("no TAB, uma lojinha com missões — mate mil monstros, mate mil cabras, jogue tantas horas, faça tantas
/// missões, login diário... tipo as missões do Master Duel que dão cristais; diárias, semanais e as da conta; libera
/// CRISTAIS AZUIS que dá pra trocar por coisas picas: capas, skins, pets, sets lendários"). Tudo por CONTA (vale para os
/// personagens da conta). Contadores somam em memória e vão para o banco a cada 20 s (e ao sair).
/// </summary>
public sealed partial class GameServer
{
    private sealed record ObjetivoDef(string Id, string Tipo, string Titulo, string Contador, long Meta, int Cristais);

    private static readonly ObjetivoDef[] Objetivos =
    {
        // diários (zeram à meia-noite de Brasília)
        new("d_login", "dia", "Entrar no jogo hoje", "login", 1, 5),
        new("d_kills", "dia", "Derrotar 100 monstros", "kills", 100, 10),
        new("d_missoes", "dia", "Completar 3 missões", "missoes", 3, 10),
        new("d_tempo", "dia", "Jogar 30 minutos", "minutos", 30, 10),
        new("d_chefe", "dia", "Derrotar 1 chefe", "chefes", 1, 15),
        // semanais (zeram na segunda-feira)
        new("s_kills", "semana", "Derrotar 1.500 monstros", "kills", 1500, 60),
        new("s_missoes", "semana", "Completar 15 missões", "missoes", 15, 60),
        new("s_tempo", "semana", "Jogar 5 horas", "minutos", 300, 80),
        new("s_chefes", "semana", "Derrotar 7 chefes", "chefes", 7, 80),
        new("s_dias", "semana", "Entrar em 5 dias diferentes", "login", 5, 50),
        // da conta (uma vez só)
        new("c_kills1", "conta", "Derrotar 1.000 monstros", "kills", 1000, 50),
        new("c_kills2", "conta", "Derrotar 10.000 monstros", "kills", 10000, 150),
        new("c_kills3", "conta", "Derrotar 50.000 monstros", "kills", 50000, 400),
        new("c_cabras", "conta", "Derrotar 1.000 Cabras-da-Serra", "kill:monster_monte_cabra", 1000, 100),
        new("c_caranguejos", "conta", "Derrotar 1.000 caranguejos da Costa", "kill:monster_r1_caranguejo_de_cal", 1000, 100),
        new("c_chefes1", "conta", "Derrotar 50 chefes", "chefes", 50, 120),
        new("c_chefes2", "conta", "Derrotar 300 chefes", "chefes", 300, 300),
        new("c_missoes1", "conta", "Completar 50 missões", "missoes", 50, 80),
        new("c_missoes2", "conta", "Completar 200 missões", "missoes", 200, 200),
        new("c_tempo1", "conta", "Jogar 10 horas", "minutos", 600, 60),
        new("c_tempo2", "conta", "Jogar 50 horas", "minutos", 3000, 200),
        new("c_tempo3", "conta", "Jogar 200 horas", "minutos", 12000, 500),
        new("c_login1", "conta", "Entrar em 7 dias diferentes", "login", 7, 50),
        new("c_login2", "conta", "Entrar em 30 dias diferentes", "login", 30, 200),
        new("c_crafts", "conta", "Fabricar 100 itens", "crafts", 100, 80),
    };

    /// <summary>Loja de Cristais: item → preço. Só entra o que existe no conteúdo.</summary>
    private static readonly (string Item, int Preco, string Grupo)[] LojaCristais =
    {
        ("caixa_cinzas_3", 40, "caixas"), ("caixa_cinzas_5", 120, "caixas"),
        ("mochila:cristal1", 100, "conforto"), ("mochila:cristal2", 200, "conforto"), ("mochila:cristal3", 400, "conforto"),
        ("capa_folhas", 60, "capas"), ("capa_gelo", 120, "capas"), ("capa_abismo", 200, "capas"), ("capa_brasa_viva", 260, "capas"), ("capa_dourada", 320, "capas"),
        ("bracelete_pedra", 150, "skins"), ("bracelete_samurai", 220, "skins"), ("bracelete_sem_cabeca", 250, "skins"), ("bracelete_cristal", 300, "skins"), ("bracelete_ouro", 500, "skins"),
        ("pet_corvo", 120, "pets"), ("pet_sapinho", 90, "pets"), ("pet_coruja", 200, "pets"), ("pet_dragao_anciao", 600, "pets"),
        ("montaria_pantera", 250, "montarias"), ("montaria_raptor_rubro", 300, "montarias"), ("montaria_grifo", 600, "montarias"), ("montaria_condor", 600, "montarias"),
        // 14/11 noite: a coleção do dono (asas raras → lendárias, capas e skins raras)
        ("asas_tempestade", 300, "asas"), ("asas_gelo", 320, "asas"), ("asas_fogo", 340, "asas"), ("asas_sombrias", 360, "asas"), ("asas_celestiais", 380, "asas"),
        ("asas_draconicas", 600, "asas"), ("asas_demoniacas", 650, "asas"), ("asas_vazio", 700, "asas"), ("asas_runicas", 720, "asas"), ("asas_arcanjo", 750, "asas"),
        ("asas_set_cavaleiro_sagrado", 700, "asas"), ("asas_set_gelo_eterno", 700, "asas"), ("asas_set_dragao_fogo", 760, "asas"),
        ("asas_set_celestial", 800, "asas"), ("asas_set_demoniaco", 800, "asas"), ("asas_set_anjo_caido", 800, "asas"),
        ("asas_dragao_anciao", 1200, "asas"), ("asas_fenrir", 1200, "asas"), ("asas_eclipse", 1350, "asas"), ("asas_tempo", 1500, "asas"), ("asas_criacao", 1600, "asas"),
        ("capa_dragao", 300, "capas"), ("capa_celestial", 330, "capas"), ("capa_morte", 380, "capas"), ("capa_luz", 420, "capas"), ("capa_tempo", 480, "capas"),
        ("bracelete_tubarao", 260, "skins"), ("bracelete_lava", 420, "skins"),
        ("lendaria_machado_presa_javali", 150, "lendarias"), ("lendaria_lanca_valquiria", 300, "lendarias"), ("lendaria_glaive_carmesim", 350, "lendarias"),
        ("lendaria_cutelo_abissal", 420, "lendarias"), ("lendaria_martelo_carneiro_fundido", 520, "lendarias"), ("lendaria_montante_dragao_rubro", 650, "lendarias"),
    };

    private sealed partial class Session { public double MinutoAcc; public readonly Dictionary<string, long> ContaPendente = new(); public long ContaFlushEm; }

    private string DiaChave() => HojeBr().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private string SemanaChave()
    {
        var d = HojeBr().ToDateTime(TimeOnly.MinValue);
        return $"{ISOWeek.GetYear(d)}-W{ISOWeek.GetWeekOfYear(d):00}";
    }

    /// <summary>Soma num contador da conta (total, do dia e da semana). "kill:x" só no total.</summary>
    private void Contar(Session s, string contador, long n = 1)
    {
        if (!MapMode || s.AccountId is null || n == 0) return;
        void Add(string k) => s.ContaPendente[k] = s.ContaPendente.GetValueOrDefault(k) + n;
        Add("t:" + contador);
        if (contador.StartsWith("kill:", StringComparison.Ordinal)) return;
        Add($"d:{DiaChave()}:{contador}");
        Add($"w:{SemanaChave()}:{contador}");
    }

    private void FlushConta(Session s)
    {
        if (s.AccountId is null || s.ContaPendente.Count == 0) return;
        foreach (var (k, v) in s.ContaPendente) _db.SomarContador(s.AccountId, k, v);
        s.ContaPendente.Clear();
    }

    /// <summary>Por tique: minutos jogados e gravação a cada 20 s.</summary>
    private void TickConta(Session s, long now, double dt)
    {
        if (s.AccountId is null) return;
        s.MinutoAcc += dt;
        if (s.MinutoAcc >= 60) { s.MinutoAcc -= 60; Contar(s, "minutos"); }
        if (now >= s.ContaFlushEm) { s.ContaFlushEm = now + 20_000; FlushConta(s); }
    }

    /// <summary>Ao entrar: o login do dia conta uma vez.</summary>
    private void LoginDoDia(Session s)
    {
        if (!MapMode || s.AccountId is null) return;
        var k = $"d:{DiaChave()}:login";
        if (_db.LerContadores(s.AccountId, k).GetValueOrDefault(k) > 0) return;
        Contar(s, "login");
        FlushConta(s);
    }

    private long Progresso(Dictionary<string, long> c, ObjetivoDef o) => o.Tipo switch
    {
        "dia" => c.GetValueOrDefault($"d:{DiaChave()}:{o.Contador}"),
        "semana" => c.GetValueOrDefault($"w:{SemanaChave()}:{o.Contador}"),
        _ => c.GetValueOrDefault("t:" + o.Contador),
    };

    private string Periodo(ObjetivoDef o) => o.Tipo switch { "dia" => DiaChave(), "semana" => SemanaChave(), _ => "sempre" };

    private void SendObjetivos(Session s)
    {
        if (s.AccountId is null) return;
        FlushConta(s);
        var c = _db.LerContadores(s.AccountId);
        var lista = Objetivos.Select(o => new ObjetivoView(o.Id, o.Tipo, o.Titulo, Math.Min(Progresso(c, o), o.Meta), o.Meta, o.Cristais,
            c.GetValueOrDefault($"claim:{o.Id}:{Periodo(o)}") > 0)).ToList();
        var loja = LojaCristais.Where(l => l.Item.StartsWith("mochila:", StringComparison.Ordinal) || _content.Items.ContainsKey(l.Item) || _content.Consumables.ContainsKey(l.Item))
            .Select(l =>
            {
                var nome = l.Item.StartsWith("mochila:", StringComparison.Ordinal) ? "+1 fileira na mochila (" + l.Item[^1] + "/3)" : ItemName(l.Item);
                var nivel = _content.Items.TryGetValue(l.Item, out var d) ? d.MinLevel : 1;
                var ja = l.Item.StartsWith("mochila:", StringComparison.Ordinal) && s.Flags.Contains(l.Item);
                return new LojaCristalView(l.Item, nome, l.Preco, l.Grupo, nivel, ja);
            }).ToList();
        SendTo(s, new ObjetivosMessage(c.GetValueOrDefault("cristais"), lista, loja));
    }

    private void HandleObjetivos(Session s, ObjetivosIntent m)
    {
        if (!MapMode || s.AccountId is null) { Reply(s, "objetivos", false, "unavailable"); return; }
        switch (m.Acao)
        {
            case "abrir": break;
            case "resgatar":
            {
                var o = Objetivos.FirstOrDefault(x => x.Id == m.Id);
                if (o is null) { Reply(s, "objetivos", false, "unknown"); return; }
                FlushConta(s);
                var c = _db.LerContadores(s.AccountId);
                var claim = $"claim:{o.Id}:{Periodo(o)}";
                if (c.GetValueOrDefault(claim) > 0) { Reply(s, "objetivos", false, "already"); break; }
                if (Progresso(c, o) < o.Meta) { Reply(s, "objetivos", false, "not_done"); break; }
                _db.SomarContador(s.AccountId, claim, 1);
                var saldo = _db.SomarContador(s.AccountId, "cristais", o.Cristais);
                SendTo(s, new NoticeMessage($"💎 +{o.Cristais} cristais azuis ({o.Titulo}). Agora você tem {saldo}.", "reward"));
                GameLog.Info("servidor.objetivos", $"{Who(s)} resgatou {o.Id} (+{o.Cristais})");
                break;
            }
            case "comprar":
            {
                var l = LojaCristais.FirstOrDefault(x => x.Item == m.Id);
                if (l.Item is null) { Reply(s, "objetivos", false, "unknown"); return; }
                var mochila = l.Item.StartsWith("mochila:", StringComparison.Ordinal);
                if (mochila && s.Flags.Contains(l.Item)) { Reply(s, "objetivos", false, "already"); break; }
                if (!mochila && _content.Items.TryGetValue(l.Item, out var def) && def.MinLevel > s.Level)
                { SendTo(s, new NoticeMessage($"Esse item pede nível {def.MinLevel}.", "warn")); Reply(s, "objetivos", false, "level"); break; }
                if (!_db.GastarContador(s.AccountId, "cristais", l.Preco)) { SendTo(s, new NoticeMessage("Cristais azuis insuficientes.", "warn")); Reply(s, "objetivos", false, "poor"); break; }
                if (mochila) GrantFlags(s, new[] { l.Item });
                else if (!_db.TryGrantItems(s.CharacterId!, new List<GrantItem> { new(l.Item, 1, "comum") }, $"cristal:{s.CharacterId}:{l.Item}:{NowMs}"))
                {
                    _db.SomarContador(s.AccountId, "cristais", l.Preco);   // devolve
                    SendTo(s, new NoticeMessage("Mochila cheia: os cristais voltaram.", "warn")); Reply(s, "objetivos", false, "inventory_full"); break;
                }
                else SendInventory(s);
                SendTo(s, new NoticeMessage($"💎 Comprou {(mochila ? "+1 fileira na mochila" : ItemName(l.Item))} por {l.Preco} cristais.", "reward"));
                GameLog.Info("servidor.objetivos", $"{Who(s)} comprou {l.Item} por {l.Preco} cristais");
                break;
            }
            default: Reply(s, "objetivos", false, "bad_action"); return;
        }
        SendObjetivos(s);
    }
}
