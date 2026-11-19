using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Efeitos sonoros (Client/Assets/Sounds, gerados por Tools/gerar_sons.py — originais, domínio público).
/// Carrega o WAV direto do arquivo (não depende do editor ter importado). Se faltar, fica em silêncio.
/// Play = som de interface (sem posição); PlayAt = som no mundo (diminui com a distância).
/// </summary>
public static class Sfx
{
    private static Node? _root;
    private static readonly Dictionary<string, AudioStream?> Cache = new();
    private static readonly List<AudioStreamPlayer> Ui = new();
    private static readonly List<AudioStreamPlayer3D> World = new();
    private static readonly Dictionary<string, ulong> LastPlayed = new();
    private static readonly Random Rng = new();
    private static AudioStreamPlayer? _ambience;
    private static string _ambienceName = "";
    public static float Volume { get; set; } = 1f;

    // 11/11 ("o jogo tá muito alto"): volumes do Esc → Configurações, salvos em user://som.cfg
    /// <summary>Volume geral (0..1): mexe no barramento principal, vale para tudo.</summary>
    public static float Geral { get; private set; } = 0.7f;
    /// <summary>Volume do ambiente (mar, vento, coro): 0..1.</summary>
    public static float Ambiente { get; private set; } = 0.7f;
    private static readonly Dictionary<AudioStreamPlayer, float> AmbienceLevels = new();
    private const string ConfigPath = "user://som.cfg";

    public static void LoadSettings()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(ConfigPath) == Error.Ok)
        {
            Geral = (float)cfg.GetValue("som", "geral", 0.7f);
            Volume = (float)cfg.GetValue("som", "efeitos", 1f);
            Ambiente = (float)cfg.GetValue("som", "ambiente", 0.7f);
        }
        ApplyGeral();
    }

    public static void SetVolumes(float geral, float efeitos, float ambiente)
    {
        Geral = Mathf.Clamp(geral, 0, 1); Volume = Mathf.Clamp(efeitos, 0, 1); Ambiente = Mathf.Clamp(ambiente, 0, 1);
        ApplyGeral();
        foreach (var (player, baseDb) in AmbienceLevels) player.VolumeDb = baseDb + AmbienceDb();
        var cfg = new ConfigFile();
        cfg.SetValue("som", "geral", Geral); cfg.SetValue("som", "efeitos", Volume); cfg.SetValue("som", "ambiente", Ambiente);
        cfg.Save(ConfigPath);
    }

    private static void ApplyGeral()
    {
        AudioServer.SetBusMute(0, Geral <= 0.001f);
        AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Math.Max(0.001f, Geral)));
    }

    private static float AmbienceDb() => Mathf.LinearToDb(Math.Max(0.001f, Ambiente));

    public static void Init(Node root)
    {
        _root = root;
        for (var i = 0; i < 10; i++) { var p = new AudioStreamPlayer(); root.AddChild(p); Ui.Add(p); }
        for (var i = 0; i < 16; i++) { var p = new AudioStreamPlayer3D { UnitSize = 6f, MaxDistance = 40f }; root.AddChild(p); World.Add(p); }
        _ambience = new AudioStreamPlayer { VolumeDb = -14f };
        root.AddChild(_ambience);
        // diagnóstico (usuário: "o jogo está sem som?"): placa de som usada, volume geral e se os arquivos carregam
        var test = Load("ui_clique");
        CinzasDaMare.Shared.Diagnostics.GameLog.Info("cliente.som",
            $"saída '{AudioServer.OutputDevice}' ({AudioServer.GetOutputDeviceList().Length} disponíveis), mix {AudioServer.GetMixRate()} Hz, " +
            $"volume geral {AudioServer.GetBusVolumeDb(0):0.#} dB{(AudioServer.IsBusMute(0) ? " MUDO" : "")}, arquivo de teste {(test is null ? "NÃO carregou" : "ok")}");
    }

    private static int _plays;
    private static float _peakDb = -200f;

    /// <summary>Diagnóstico de som: quantos tocaram desde o último relatório e o pico de saída do Master (dB).</summary>
    public static string Report()
    {
        var r = $"som: {_plays} tocados, pico {_peakDb:0} dB, ambiente '{_ambienceName}'";
        _plays = 0; _peakDb = -200f;
        return r;
    }

    /// <summary>Chamar todo quadro: guarda o maior nível que saiu para a placa de som.</summary>
    public static void Sample()
    {
        var l = AudioServer.GetBusPeakVolumeLeftDb(0, 0);
        if (l > _peakDb) _peakDb = l;
    }

    /// <summary>
    /// 11/11 (pacote de sons do trabalhador, Assets/Audio/*.ogg, livres de direitos): onde existe a versão nova, ela toca no
    /// lugar do .wav antigo; com várias variantes (passos), sorteia uma a cada vez. Se o .ogg faltar, fica o .wav.
    /// </summary>
    private static readonly Dictionary<string, string[]> Novos = new()
    {
        ["acerto_lamina"] = new[] { "golpes/espada_corte" },
        ["acerto_contundente"] = new[] { "golpes/impacto_pesado" },
        ["acerto"] = new[] { "golpes/impacto_carne" },
        ["golpe"] = new[] { "golpes/espada_pesada" },
        ["bloqueio"] = new[] { "golpes/impacto_metal" },
        ["esquiva"] = new[] { "golpes/esquiva_rolamento" },
        ["acerto_flecha"] = new[] { "arco/flecha_acerto" },
        ["flecha"] = new[] { "arco/arco_disparo" },
        ["moeda"] = new[] { "interface/moedas" },
        ["nivel"] = new[] { "interface/subir_nivel" },
        ["missao_aceita"] = new[] { "interface/missao_nova" },
        ["pegar"] = new[] { "interface/pegar_item" },
        ["ui_clique"] = new[] { "interface/clique" },
        ["ui_erro"] = new[] { "interface/erro" },
        ["ui_abrir"] = new[] { "interface/abrir_painel" },
        ["cura"] = new[] { "magia/cura" },
        ["hab_cura"] = new[] { "magia/cura" },
        ["explosao"] = new[] { "magia/explosao_fogo" },
        ["hab_fogo"] = new[] { "magia/explosao_fogo" },
        ["hab_gelo"] = new[] { "magia/gelo" },
        ["hab_raio"] = new[] { "magia/faisca" },
        ["hab_sombra"] = new[] { "magia/vazio" },
        ["passo_areia"] = new[] { "passos/passo_areia_1", "passos/passo_areia_2", "passos/passo_areia_3", "passos/passo_areia_4" },
        ["passo_pedra"] = new[] { "passos/passo_pedra_1", "passos/passo_pedra_2", "passos/passo_pedra_3", "passos/passo_pedra_4" },
        ["passo_madeira"] = new[] { "passos/passo_madeira_1", "passos/passo_madeira_2", "passos/passo_madeira_3", "passos/passo_madeira_4" },
        ["amb2_mar"] = new[] { "ambiente/ambiente_mar_ondas_loop" },
        ["amb2_vento"] = new[] { "ambiente/ambiente_vento_loop" },
        ["amb2_caverna"] = new[] { "ambiente/ambiente_caverna_gotejar_loop" },
        ["loop_fogueira"] = new[] { "ambiente/ambiente_fogueira_loop" },
        ["musica_porto"] = new[] { "musica/musica_porto_calma_loop" },
    };

    private static AudioStream? LoadOgg(string rel)
    {
        var key = "ogg:" + rel;
        if (Cache.TryGetValue(key, out var s)) return s;
        AudioStream? stream = null;
        try
        {
            var path = "res://Assets/Audio/" + rel + ".ogg";
            if (Godot.FileAccess.FileExists(path)) stream = AudioStreamOggVorbis.LoadFromFile(path);
        }
        catch (Exception e) { GD.PrintErr($"Som {rel}: {e.Message}"); }
        Cache[key] = stream;
        return stream;
    }

    private static AudioStream? Load(string name)
    {
        if (Novos.TryGetValue(name, out var variants) && LoadOgg(variants[Rng.Next(variants.Length)]) is { } novo) return novo;
        if (Cache.TryGetValue(name, out var s)) return s;
        AudioStream? stream = null;
        try
        {
            var path = "res://Assets/Sounds/" + name + ".wav";
            if (Godot.FileAccess.FileExists(path)) stream = AudioStreamWav.LoadFromFile(path);
        }
        catch (Exception e) { GD.PrintErr($"Som {name}: {e.Message}"); }
        Cache[name] = stream;
        return stream;
    }

    /// <summary>Evita o mesmo som disparar várias vezes no mesmo instante (ex.: 5 moedas juntas).</summary>
    private static bool Throttle(string name, ulong minMs)
    {
        var now = Time.GetTicksMsec();
        if (LastPlayed.TryGetValue(name, out var last) && now - last < minMs) return false;
        LastPlayed[name] = now;
        return true;
    }

    public static void Play(string name, float volumeDb = 0f, float pitchJitter = 0.06f, ulong throttleMs = 40)
    {
        if (_root is null || Volume <= 0.001f || !Throttle(name, throttleMs)) return;
        var stream = Load(name);
        if (stream is null) return;
        var player = Ui.Find(p => !p.Playing) ?? Ui[0];
        player.Stream = stream;
        // 13/11 (áudio "melhorar sem mudar o que está bom"): golpe repetido nunca soa idêntico — tom e volume variam mais
        if (name.StartsWith("acerto", StringComparison.Ordinal) || name is "golpe" or "bloqueio") { pitchJitter = Mathf.Max(pitchJitter, 0.12f); volumeDb += (float)(Rng.NextDouble() * 3 - 1.5); }
        player.VolumeDb = volumeDb + Mathf.LinearToDb(Volume);
        player.PitchScale = 1f + (float)(Rng.NextDouble() * 2 - 1) * pitchJitter;
        player.Play();
        _plays++;
    }

    public static void PlayAt(string name, Vector3 position, float volumeDb = 0f, float pitchJitter = 0.08f, ulong throttleMs = 30)
    {
        if (_root is null || Volume <= 0.001f || !Throttle(name + position.Round(), throttleMs)) return;
        var stream = Load(name);
        if (stream is null) return;
        var player = World.Find(p => !p.Playing) ?? World[0];
        player.Stream = stream;
        player.GlobalPosition = position;
        if (name.StartsWith("acerto", StringComparison.Ordinal) || name is "golpe" or "bloqueio") { pitchJitter = Mathf.Max(pitchJitter, 0.12f); volumeDb += (float)(Rng.NextDouble() * 3 - 1.5); }
        player.VolumeDb = volumeDb + Mathf.LinearToDb(Volume);
        player.PitchScale = 1f + (float)(Rng.NextDouble() * 2 - 1) * pitchJitter;
        player.Play();
        _plays++;
    }

    /// <summary>
    /// Voz de NPC (resmungo sem palavras): som no lugar do NPC com a altura própria dele (timbre); "f" usa o conjunto
    /// feminino. Um por vez por NPC (não empilha).
    /// </summary>
    public static void Voice(string kind, bool female, float pitch, Vector3 position)
    {
        var name = $"voz_{(female ? "f" : "m")}_{kind}";
        if (_root is null || Volume <= 0.001f || !Throttle("voz" + position.Round(), 350)) return;
        var stream = Load(name);
        if (stream is null) return;
        var player = World.Find(p => !p.Playing) ?? World[0];
        player.Stream = stream;
        player.GlobalPosition = position + Vector3.Up * 1.6f;
        player.VolumeDb = -2f + Mathf.LinearToDb(Volume);
        player.PitchScale = pitch * (1f + (float)(Rng.NextDouble() * 2 - 1) * 0.03f);
        player.Play();
        _plays++;
    }

    private static readonly List<AudioStreamPlayer> Layers = new();

    /// <summary>
    /// Som de fundo em laço, em camadas separadas por "+" (ex.: "amb2_mar+amb2_gaivotas+amb2_vento"). Sem trilha sonora:
    /// só o lugar (estilo MU). A 1ª camada é a base; as outras entram mais baixas. Troca só quando muda.
    /// </summary>
    public static void Ambience(string name)
    {
        if (_ambience is null || name == _ambienceName) return;
        _ambienceName = name;
        foreach (var l in Layers) l.Stop();
        _ambience.Stop();
        var names = name.Split('+', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < names.Length; i++)
        {
            AudioStreamPlayer player;
            if (i == 0) player = _ambience;
            else
            {
                while (Layers.Count < i) { var p = new AudioStreamPlayer(); _root!.AddChild(p); Layers.Add(p); }
                player = Layers[i - 1];
            }
            var stream = Looping(names[i]);
            player.Stream = stream;
            // coro: bem baixinho, só acompanha (pedido do usuário)
            var baseDb = names[i] == "amb2_coro" ? -27f : names[i] == "musica_porto" ? -11f : i == 0 ? -12f : -15f;
            AmbienceLevels[player] = baseDb;
            player.VolumeDb = baseDb + AmbienceDb();
            if (stream is not null) player.Play();
        }
    }

    private static AudioStream? Looping(string name)
    {
        var stream = Load(name);
        if (stream is AudioStreamWav wav) { wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward; wav.LoopEnd = (int)(wav.GetLength() * wav.MixRate); }
        if (stream is AudioStreamOggVorbis ogg) ogg.Loop = true;
        return stream;
    }

    /// <summary>Laço preso a um objeto do mundo (forja, caldeirão, fogueira): só se ouve de perto.</summary>
    private static readonly List<(AudioStreamPlayer3D P, float Max)> Lacos3D = new();

    /// <summary>17/11 noite: chamado todo quadro — laço 3D longe da câmera fica mudo (pausado), perto volta.</summary>
    public static void Lacos3DPerto(Vector3 ouvinte)
    {
        for (var i = Lacos3D.Count - 1; i >= 0; i--)
        {
            var (p, max) = Lacos3D[i];
            if (!GodotObject.IsInstanceValid(p)) { Lacos3D.RemoveAt(i); continue; }
            if (!p.IsInsideTree()) continue;
            var perto = p.GlobalPosition.DistanceTo(ouvinte) <= max && p.IsVisibleInTree();
            if (p.StreamPaused == perto) p.StreamPaused = !perto;
        }
    }

    public static void Loop3D(Node3D parent, string name, float volumeDb, float unitSize, float maxDistance)
    {
        if (Looping(name) is not { } stream) return;
        var p = new AudioStreamPlayer3D
        {
            Stream = stream, VolumeDb = volumeDb + Mathf.LinearToDb(Math.Max(0.001f, Volume)), UnitSize = unitSize, MaxDistance = maxDistance,
            Position = new Vector3(0, 0.8f, 0),
            // 17/11 noite (dono: "o som do ferreiro acompanha sempre, mesmo fora da base — o ferreiro só tem som onde tem ele"):
            // queda logarítmica (some rápido) e corte duro: longe da câmera ele PARA de tocar (ver Sfx.Lacos3D)
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Logarithmic,
        };
        Lacos3D.Add((p, maxDistance));
        parent.AddChild(p);
        // começa em ponto diferente para duas forjas não baterem juntas
        p.Ready += () => p.Play((float)(Rng.NextDouble() * Math.Max(0.1, stream.GetLength() - 0.2)));
    }

    // 14/11 ("o barulho de mar acompanha o jogo todo; lugares com mar, outros com fogo, outros com sons estranhos"):
    // camadas do LUGAR, que sobem e descem conforme você anda (perto do mar, da lava, de ruína/cemitério, de água)
    private static readonly Dictionary<string, (AudioStreamPlayer P, float W)> Locais = new();
    private static readonly Dictionary<string, float> LocalDb = new()
    {
        ["amb2_mar"] = -9f, ["amb2_gaivotas"] = -16f, ["amb2_lava"] = -10f, ["amb2_criaturas"] = -13f, ["amb_masmorra"] = -17f,
        ["amb2_sapos"] = -15f, ["loop_fogueira"] = -12f,
    };

    /// <summary>Peso 0–1 de cada som do lugar (o que não vier vai a zero). Chamar todo quadro; o volume anda suave.</summary>
    public static void Lugar(IReadOnlyDictionary<string, float> pesos, float dt)
    {
        if (_root is null) return;
        foreach (var nome in pesos.Keys)
            if (!Locais.ContainsKey(nome) && LocalDb.ContainsKey(nome) && Looping(nome) is { } st)
            {
                var pl = new AudioStreamPlayer { Stream = st, VolumeDb = -80f };
                _root.AddChild(pl);
                pl.Play((float)(Rng.NextDouble() * Math.Max(0.1, st.GetLength() - 0.2)));
                Locais[nome] = (pl, 0f);
            }
        foreach (var nome in Locais.Keys.ToList())
        {
            var (pl, w) = Locais[nome];
            var alvo = pesos.TryGetValue(nome, out var a) ? Mathf.Clamp(a, 0f, 1f) : 0f;
            w = Mathf.MoveToward(w, alvo, dt * 0.6f);   // ~1,5 s para entrar ou sair
            Locais[nome] = (pl, w);
            pl.VolumeDb = w <= 0.01f ? -80f : LocalDb[nome] + Mathf.LinearToDb(w) + AmbienceDb();
        }
    }

    /// <summary>Camadas de ambiente por tema do mapa (vento, mar, pássaros, grilos, sapos, gotas, criaturas ao longe).</summary>
    public static string AmbienceFor(string theme, bool instanced, bool town) => AmbienceBase(theme, instanced, town) + "+amb2_coro";

    private static string AmbienceBase(string theme, bool instanced, bool town) => instanced ? theme switch
    {
        "lava" => "amb2_lava+amb2_caverna",
        "cidade" => "amb2_vento+loop_fogueira",          // casa do jogador: lareira e vento lá fora
        _ => "amb2_caverna+amb_masmorra",
    } : theme switch
    {
        _ when town => "amb2_sinos+amb2_vento+musica_porto",   // 14/11: mar e gaivotas só perto do cais (Sfx.Lugar)   // 11/11: música calma do porto, baixinha
        "praia" => "amb2_vento+amb2_passaros",   // 14/11: o mar vem pelo lugar (mais forte perto da água)
        "vale" => "amb2_vento+amb2_passaros+amb2_grilos",
        "mata" => "amb2_floresta+amb2_criaturas+amb2_grilos",
        "canion" => "amb2_vento_forte+amb2_criaturas",
        "brejo" => "amb2_sapos+amb2_grilos+amb2_vento",
        "vespera" or "fantasma" => "amb2_vento+amb_masmorra+amb2_criaturas",
        "neve" => "amb2_vento_forte",
        "lava" => "amb2_lava+amb2_vento",
        "acropole" => "amb2_vento+amb2_passaros",
        "gruta" => "amb2_caverna+amb_masmorra",
        "cidade" => "amb2_sinos+amb2_vento",
        _ => "amb2_vento+amb2_passaros",
    };
}
