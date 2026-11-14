using System.Text.Json;
using System.Text.Json.Nodes;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// Editor dentro do jogo (pedido de 11/11, "estilo Mario Maker", fase 1), só no modo deus. Cada mudança grava direto no
/// map_*.json (fica definitivo) e guarda antes a versão anterior em Content/editor_historico/&lt;mapa&gt;/ — "Voltar versão"
/// desfaz a última. O mapa é recarregado na hora: cenário, colisão e monstros novos já valem para todos.
/// </summary>
public sealed partial class GameServer
{
    /// <summary>Pasta Content/ do servidor (o ServerHost informa). Sem ela o editor fica desligado.</summary>
    public string? ContentDir { get; set; }

    private static readonly JsonSerializerOptions EditorJson = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private sealed partial class Session
    {
        public bool Editing;
        public bool InvulBeforeEdit;
    }

    private string? MapFilePath(string mapId)
    {
        if (ContentDir is null) return null;
        var world = Path.Combine(ContentDir, WorldContentLoader.WorldFile);
        if (!File.Exists(world)) return null;
        var files = JsonNode.Parse(File.ReadAllText(world))?["map_files"]?.AsArray().Select(n => n!.GetValue<string>()) ?? Enumerable.Empty<string>();
        foreach (var f in files)
        {
            var path = Path.Combine(ContentDir, f);
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path))?["id"]?.GetValue<string>() == mapId) return path;
        }
        return null;
    }

    private string HistoryDir(string mapId) => Path.Combine(ContentDir!, "editor_historico", mapId);

    internal int MapVersionCount(string? mapId)
        => mapId is null || ContentDir is null || !Directory.Exists(HistoryDir(mapId)) ? 0 : Directory.GetFiles(HistoryDir(mapId), "*.json").Length;

    /// <summary>Todas as peças de cenário que algum mapa usa (lista do editor).</summary>
    internal List<string> EditorPropKinds()
        => _world?.Maps.Values.SelectMany(l => l.Props.Select(p => p.Kind)).Distinct().OrderBy(k => k).ToList() ?? new List<string>();

    // ============================================================ editor v2 (11/11 noite): rascunho, Salvar, Testar
    // Pedido do dono ("botão de salvar, testar, igual Shadowline"): cada mudança já aparece no mapa na hora (para todos que
    // estão nele), mas o arquivo map_*.json só muda ao apertar SALVAR. DESCARTAR volta ao que está salvo. TESTAR sai do
    // editor e deixa jogar com o rascunho aplicado; voltar ao editor continua de onde parou. Ctrl+Z desfaz passo a passo.

    private sealed class EditorDraft
    {
        public string MapId = "";
        public JsonObject Root = null!;
        public readonly List<string> Undo = new();
        public readonly List<string> UndoWhat = new();   // 13/11: o que cada passo fez (para dizer o que foi desfeito)
        public int Unsaved;
        public (string List, double X, double Y)? Sel;
    }

    private readonly Dictionary<string, EditorDraft> _drafts = new();   // por mapa: quem edita junto vê o mesmo rascunho

    private EditorDraft DraftFor(Zone zone, string path)
    {
        if (_drafts.TryGetValue(zone.Layout!.Id, out var d)) return d;
        d = new EditorDraft { MapId = zone.Layout.Id, Root = JsonNode.Parse(File.ReadAllText(path))!.AsObject() };
        _drafts[zone.Layout.Id] = d;
        return d;
    }

    private void SendEditorState(Session s, EditorDraft? d, string? info = null)
    {
        JsonNode? sel = null;
        if (d?.Sel is { } sl && d.Root[sl.List] is JsonArray arr)
            sel = arr.OfType<JsonObject>().FirstOrDefault(n => Same(n, sl.X, sl.Y));
        SendTo(s, new EditorStateMessage(d?.Unsaved ?? 0, !s.Editing && (d?.Unsaved ?? 0) > 0, d?.Sel?.List, d?.Sel?.X ?? 0, d?.Sel?.Y ?? 0,
            sel?.ToJsonString(EditorJson), info));
    }

    private static bool Same(JsonObject n, double x, double y)
        => n["x"] is not null && n["y"] is not null && Math.Abs(n["x"]!.GetValue<double>() - x) < 0.02 && Math.Abs(n["y"]!.GetValue<double>() - y) < 0.02;

    private static double Num(JsonNode? n) => n is null ? 0 : n.GetValueKind() == JsonValueKind.Number ? n.GetValue<double>() : double.TryParse(n.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

    private void HandleEditor(Session s, GodIntent m)
    {
        var zone = s.Zone;
        if (zone?.Layout is null || zone.Instanced) { Reply(s, "editor", false, "so_em_mapa_aberto"); return; }
        var op = m.Arg ?? "";
        var path = MapFilePath(zone.Layout.Id);
        if (op is "on" or "off")
        {
            // 11/11 noite (bug "fiquei esquivando para sempre, nada me acertava"): entrar no editor ligava a invulnerabilidade
            // de deus e sair NÃO desligava — o personagem ficava imortal e todo golpe virava "esquivou". Agora volta como estava.
            if (op == "on" && !s.Editing) s.InvulBeforeEdit = s.GodInvulnerable;
            if (op == "off" && s.Editing) s.GodInvulnerable = s.InvulBeforeEdit;
            s.Editing = op == "on";
            if (s.Editing) s.GodInvulnerable = true;
            s.DirX = s.DirY = 0;
            Reply(s, "editor", true, op);
            SendEditorState(s, _drafts.GetValueOrDefault(zone.Layout.Id));
            return;
        }
        if (path is null) { Reply(s, "editor", false, "arquivo_do_mapa_nao_achado"); return; }
        var d = DraftFor(zone, path);
        string what;
        switch (op)
        {
            case "save":
            {
                if (d.Unsaved == 0) { SendEditorState(s, d, "Nada para salvar."); return; }
                Directory.CreateDirectory(HistoryDir(zone.Layout.Id));
                File.Copy(path, Path.Combine(HistoryDir(zone.Layout.Id), DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json"), overwrite: true);
                var json = d.Root.ToJsonString(EditorJson);
                File.WriteAllText(path, json.EndsWith('\n') ? json : json + "\n");
                GameLog.Info("servidor.editor", $"{Who(s)} salvou {zone.Layout.Id} ({d.Unsaved} mudanças)");
                d.Unsaved = 0; d.Undo.Clear(); d.UndoWhat.Clear();
                foreach (var p in zone.Players) SendEditorState(p, d, "Salvo no mapa do projeto.");
                SendGodCatalog(s);
                return;
            }
            case "discard":
            {
                _drafts.Remove(zone.Layout.Id);
                var layout0 = WorldContentLoader.ParseMap(File.ReadAllText(path));
                ApplyEditedLayout(zone, layout0);
                foreach (var p in zone.Players) SendEditorState(p, null, "Rascunho descartado: o mapa voltou ao que está salvo.");
                return;
            }
            case "restore":
            {
                // volta para a última versão SALVA antes (pasta editor_historico)
                var dir = HistoryDir(zone.Layout.Id);
                var last = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal).LastOrDefault() : null;
                if (last is null) { SendTo(s, new NoticeMessage("Não há versão anterior deste mapa.", "warn")); return; }
                File.Copy(last, path, overwrite: true);
                File.Delete(last);
                _drafts.Remove(zone.Layout.Id);
                ApplyEditedLayout(zone, WorldContentLoader.ParseMap(File.ReadAllText(path)));
                foreach (var p in zone.Players) SendEditorState(p, null, "Voltou para a versão salva anterior.");
                SendGodCatalog(s);
                return;
            }
            case "select":
            {
                // clique em cima de um monstro vivo = seleciona o ponto dele (o bicho pode estar longe do ponto)
                var mon = zone.Monsters.Where(x => x.Spawn is not null && x.Master is null && !x.GodSpawned && x.State.Alive)
                    .Select(x => (M: x, D: Math.Sqrt((x.X - m.X) * (x.X - m.X) + (x.Y - m.Y) * (x.Y - m.Y))))
                    .Where(t => t.D <= 1.8).OrderBy(t => t.D).Select(t => t.M).FirstOrDefault();
                if (mon is not null && d.Root["spawns"] is JsonArray spArr
                    && spArr.OfType<JsonObject>().FirstOrDefault(n => n["id"]?.ToString() == mon.Spawn!.Id) is { } spNode)
                {
                    d.Sel = ("spawns", Num(spNode["x"]), Num(spNode["y"]));
                    SendEditorState(s, d);
                    return;
                }
                var hit = NearestNode(d.Root, m.X, m.Y, false);
                d.Sel = hit is { } h ? (h.List, Num(h.Node["x"]), Num(h.Node["y"])) : null;
                SendEditorState(s, d, hit is null ? "Nada perto do clique." : null);
                return;
            }
            case "undo":
            {
                if (d.Undo.Count == 0) { SendEditorState(s, d, "Nada para desfazer no rascunho."); return; }
                d.Root = JsonNode.Parse(d.Undo[^1])!.AsObject();
                d.Undo.RemoveAt(d.Undo.Count - 1);
                d.Unsaved = Math.Max(0, d.Unsaved - 1);
                // 13/11 ("um texto mostrando o que foi desfeito"): diz qual passo voltou
                var desfeito = d.UndoWhat.Count > 0 ? d.UndoWhat[^1] : "o último passo";
                if (d.UndoWhat.Count > 0) d.UndoWhat.RemoveAt(d.UndoWhat.Count - 1);
                what = "DESFEITO: " + desfeito + (d.UndoWhat.Count > 0 ? $" (antes disso: {d.UndoWhat[^1]})" : "");
                break;
            }
            default:
            {
                var before = d.Root.ToJsonString();
                var w = ApplyEditorOp(s, zone, d, op, m);
                if (w is null) return;
                d.Undo.Add(before);
                d.UndoWhat.Add(w);
                if (d.Undo.Count > 200) { d.Undo.RemoveAt(0); d.UndoWhat.RemoveAt(0); }
                d.Unsaved++;
                what = w;
                break;
            }
        }
        MapLayout layout;
        try { layout = WorldContentLoader.ParseMap(d.Root.ToJsonString()); }
        catch (Exception e)
        {
            GameLog.Error("servidor.editor", "mapa editado ficou inválido", e);
            if (op != "undo" && d.Undo.Count > 0) { d.Root = JsonNode.Parse(d.Undo[^1])!.AsObject(); d.Undo.RemoveAt(d.Undo.Count - 1); if (d.UndoWhat.Count > 0) d.UndoWhat.RemoveAt(d.UndoWhat.Count - 1); d.Unsaved--; }
            SendTo(s, new NoticeMessage("Não deu: o mapa ficaria inválido.", "warn"));
            return;
        }
        ApplyEditedLayout(zone, layout);
        GameLog.Info("servidor.editor", $"{Who(s)} editou {layout.Id} (rascunho): {what}");
        foreach (var p in zone.Players) SendEditorState(p, d, p == s ? (op == "undo" ? $"↶ {what}." : $"Editor: {what}. Ainda não salvo (Salvar grava no mapa).") : null);
    }

    /// <summary>Uma mudança no rascunho. Devolve o texto do que fez, ou null se não fez nada (aviso já enviado).</summary>
    private string? ApplyEditorOp(Session s, Zone zone, EditorDraft d, string op, GodIntent m)
    {
        var root = d.Root;
        var r = Math.Round(m.X, 2); var y = Math.Round(m.Y, 2);
        JsonObject? Selected() => d.Sel is { } sl && root[sl.List] is JsonArray arr ? arr.OfType<JsonObject>().FirstOrDefault(n => Same(n, sl.X, sl.Y)) : null;
        switch (op)
        {
            case "prop":
            {
                if (string.IsNullOrEmpty(m.Extra)) return null;
                var node = new JsonObject { ["kind"] = m.Extra, ["x"] = r, ["y"] = y, ["yaw"] = Math.Round(m.Yaw), ["editor"] = true };
                if (m.Amount > 0) node["solid"] = Math.Round(m.Amount / 100.0, 2);   // raio de colisão (cm) medido pelo cliente no modelo
                root["props"]!.AsArray().Add(node);
                d.Sel = ("props", r, y);
                return "pôs " + m.Extra;
            }
            case "tree" or "rock":
            {
                var tree = op == "tree";
                root["obstacles"]!.AsArray().Add(new JsonObject
                {
                    ["kind"] = tree ? "arvore" : "rocha", ["shape"] = "circle", ["x"] = r, ["y"] = y, ["w"] = 0, ["h"] = 0,
                    ["r"] = tree ? 0.6 : 1.0, ["height"] = tree ? 5.5 : 1.6, ["deco"] = true, ["editor"] = true,
                });
                d.Sel = ("obstacles", r, y);
                return tree ? "pôs uma árvore" : "pôs uma rocha";
            }
            case "spawn":
            {
                if (m.Extra is null || !_content.Monsters.TryGetValue(m.Extra, out var def) || !_world!.Behaviors.ContainsKey(def.BehaviorId))
                { Reply(s, "editor", false, "monstro_desconhecido"); return null; }
                if (!zone.Geo.IsFree(r, y, def.Radius)) { SendTo(s, new NoticeMessage("Aí não cabe (parede, árvore ou pedra).", "warn")); return null; }
                var level = m.Amount > 0 ? (int)Math.Clamp(m.Amount, 1, 60) : def.Level;
                var id = "ed" + DateTime.UtcNow.ToString("HHmmssfff");
                root["spawns"]!.AsArray().Add(new JsonObject
                {
                    ["id"] = id, ["monster"] = def.Id, ["x"] = r, ["y"] = y, ["level"] = level, ["group"] = id, ["respawn_seconds"] = 30, ["editor"] = true,
                });
                d.Sel = ("spawns", r, y);
                return $"pôs {def.SpeciesName} nv {level}";
            }
            case "move":
            {
                // com algo selecionado move o selecionado; senão (Extra "x;y") o que estiver perto do ponto de origem
                var node = Selected();
                if (node is null)
                {
                    var parts = (m.Extra ?? "").Split(';');
                    if (parts.Length != 2) return null;
                    var fx = Num(JsonValue.Create(parts[0])); var fy = Num(JsonValue.Create(parts[1]));
                    if (NearestNode(root, fx, fy, false) is { } h) { node = h.Node; d.Sel = (h.List, fx, fy); }
                }
                if (node is null) { SendTo(s, new NoticeMessage("Nada selecionado para mover.", "warn")); return null; }
                var label = Label(node);
                node["x"] = r; node["y"] = y;
                d.Sel = (d.Sel!.Value.List, r, y);
                return "moveu " + label;
            }
            case "rotate":
            {
                var node = Selected();
                if (node is null && (m.Extra ?? "").Split(';') is { Length: 2 } pr && NearestNode(root, Num(JsonValue.Create(pr[0])), Num(JsonValue.Create(pr[1])), true) is { } h2)
                { node = h2.Node; d.Sel = (h2.List, Num(node["x"]), Num(node["y"])); }
                if (node is null) { SendTo(s, new NoticeMessage("Nada selecionado para girar.", "warn")); return null; }
                var key = d.Sel?.List == "obstacles" ? "rot" : node["facing"] is not null ? "facing" : "yaw";
                node[key] = Math.Round((Num(node[key]) + m.Yaw + 360) % 360);
                return "girou " + Label(node);
            }
            case "rotate_to":
            {
                // 13/11 ("girar livre: clicar na alça da diagonal e eu mesmo girar"): ângulo exato (graus)
                if (Selected() is not { } node) { SendTo(s, new NoticeMessage("Nada selecionado para girar.", "warn")); return null; }
                var key = d.Sel?.List == "obstacles" ? "rot" : node["facing"] is not null ? "facing" : "yaw";
                node[key] = Math.Round((m.Yaw % 360 + 360) % 360, 1);
                return $"girou {Label(node)} para {Num(node[key]):0}°";
            }
            case "scale":
            {
                if (Selected() is not { } node || d.Sel!.Value.List != "props") { SendTo(s, new NoticeMessage("Selecione uma peça de cenário para mudar o tamanho.", "warn")); return null; }
                var sc = Math.Clamp(Math.Round((node["scale"] is null ? 1 : Num(node["scale"])) * m.Yaw, 2), 0.2, 6);
                node["scale"] = sc;
                return $"tamanho {sc:0.##}× em {Label(node)}";
            }
            case "set":
            {
                // campo do selecionado: Extra = {"key": "...", "value": <json>}
                if (Selected() is not { } node || m.Extra is null) return null;
                var o = JsonNode.Parse(m.Extra)!.AsObject();
                var key = o["key"]!.GetValue<string>();
                if (key is "x" or "y") { var v = Num(o["value"]); node[key] = v; d.Sel = (d.Sel!.Value.List, key == "x" ? v : d.Sel.Value.X, key == "y" ? v : d.Sel.Value.Y); }
                else if (o["value"] is null || (o["value"] is JsonValue jv && jv.ToString() == "")) node.Remove(key);
                else node[key] = o["value"]!.DeepClone();
                return $"mudou {key} de {Label(node)}";
            }
            case "replace":
            {
                // 13/11 ("editar como programador"): o JSON inteiro do selecionado, editado à mão (Extra = o objeto novo)
                if (Selected() is not { } node || root[d.Sel!.Value.List] is not JsonArray arr || m.Extra is null) return null;
                JsonObject novo;
                try { novo = JsonNode.Parse(m.Extra)!.AsObject(); }
                catch (Exception) { SendTo(s, new NoticeMessage("JSON inválido — confira vírgulas, aspas e chaves.", "warn")); return null; }
                if (novo["x"] is null || novo["y"] is null) { novo["x"] = node["x"]!.DeepClone(); novo["y"] = node["y"]!.DeepClone(); }
                arr[arr.IndexOf(node)] = novo;
                d.Sel = (d.Sel.Value.List, Num(novo["x"]), Num(novo["y"]));
                return "trocou o JSON de " + Label(novo);
            }
            case "lote":
            {
                // 13/11 (pincel do editor: pintar o chão / espalhar peças arrastando): Extra = {"list": "props", "nodes": [...]}
                // — tudo de uma vez, um passo só no desfazer
                if (m.Extra is null) return null;
                var o = JsonNode.Parse(m.Extra)!.AsObject();
                var list = o["list"]!.GetValue<string>();
                if (root[list] is not JsonArray arr || o["nodes"] is not JsonArray nodes || nodes.Count == 0) return null;
                var n = 0;
                foreach (var src in nodes.OfType<JsonObject>().Take(400))
                {
                    if (src["x"] is null || src["y"] is null) continue;
                    arr.Add(src.DeepClone());
                    n++;
                }
                d.Sel = null;
                var primeiro = nodes.OfType<JsonObject>().FirstOrDefault();
                var nome = primeiro is null ? "peças" : Label(primeiro);
                return nome.StartsWith("pintura_", StringComparison.Ordinal) ? $"pintou o chão ({nome[8..]}, {n} pinceladas)" : $"espalhou {n}× {nome}";
            }
            case "apagar_area":
            {
                // 13/11 (borracha do pincel): tira as peças com o prefixo (pinceladas) perto de qualquer ponto do traço
                if (m.Extra is null || root["props"] is not JsonArray props) return null;
                var o = JsonNode.Parse(m.Extra)!.AsObject();
                var prefixo = o["prefixo"]?.GetValue<string>() ?? "pintura_";
                var raio = o["r"] is { } rr ? Num(rr) : 2.0;
                var pontos = (o["pontos"] as JsonArray)?.OfType<JsonArray>().Select(a => (X: Num(a[0]), Y: Num(a[1]))).ToList() ?? new();
                var tirar = props.OfType<JsonObject>().Where(n => n["kind"]?.ToString().StartsWith(prefixo, StringComparison.Ordinal) == true
                    && pontos.Any(pt => Math.Pow(Num(n["x"]) - pt.X, 2) + Math.Pow(Num(n["y"]) - pt.Y, 2) <= raio * raio)).ToList();
                if (tirar.Count == 0) { SendTo(s, new NoticeMessage("Não tem tinta aí para tirar.", "info")); return null; }
                foreach (var n in tirar) props.Remove(n);
                d.Sel = null;
                return $"tirou {tirar.Count} pincelada(s) de tinta";
            }
            case "paste":
            {
                // Extra = {"list": "props", "node": {...}} — cola no ponto do clique
                if (m.Extra is null) return null;
                var o = JsonNode.Parse(m.Extra)!.AsObject();
                var list = o["list"]!.GetValue<string>();
                if (root[list] is not JsonArray arr || o["node"] is not JsonObject src) return null;
                var copy = src.DeepClone().AsObject();
                copy["x"] = r; copy["y"] = y;
                if (copy["id"] is not null) copy["id"] = copy["id"]!.GetValue<string>() + "_c" + DateTime.UtcNow.ToString("HHmmssfff");
                if (copy["group"] is not null && list == "spawns") copy["group"] = copy["id"]!.DeepClone();
                arr.Add(copy);
                d.Sel = (list, r, y);
                return "colou " + Label(copy);
            }
            case "remove":
            {
                if (Selected() is { } node && root[d.Sel!.Value.List] is JsonArray arr)
                {
                    arr.Remove(node);
                    var l = Label(node);
                    d.Sel = null;
                    return "apagou " + l;
                }
                var w = RemoveNearest(root, r, y);
                if (w.Length == 0) { SendTo(s, new NoticeMessage("Nada perto do clique para apagar.", "warn")); return null; }
                return w;
            }
            default:
                Reply(s, "editor", false, "acao_desconhecida");
                return null;
        }
    }

    /// <summary>
    /// 13/11 ("quero poder editar as funções, missões, drops, itens — tudo pelo editor, porque vou viajar sem internet"):
    /// lista, lê e grava os arquivos de conteúdo (Content/*.json). Ao salvar: confere se o JSON é válido, guarda a versão
    /// anterior em Content/editor_historico/arquivos e grava. Vale na próxima vez que o servidor abrir (mapas, na hora).
    /// </summary>
    private void HandleArquivo(Session s, GodIntent m)
    {
        if (ContentDir is null) { SendTo(s, new NoticeMessage("Pasta de conteúdo não encontrada.", "warn")); return; }
        var files = Directory.GetFiles(ContentDir, "*.json").Select(Path.GetFileName).Where(f => f is not null).Select(f => f!).OrderBy(f => f).ToList();
        string? Seguro(string? nome) => nome is not null && files.Contains(nome) ? Path.Combine(ContentDir, nome) : null;
        switch (m.Action)
        {
            case "arquivos":
                SendTo(s, new ContentFileMessage(files));
                return;
            case "arquivo_achar":
            {
                // 13/11: "editar o monstro / as missões deste NPC" — abre o primeiro arquivo que tem esse id
                var giver = (m.Arg ?? "").StartsWith("giver:", StringComparison.Ordinal);
                var alvo = giver ? "\"giver\": \"" + m.Arg![6..] + "\"" : "\"" + (m.Arg ?? "") + "\"";
                var achou = files.FirstOrDefault(f => !f.StartsWith("map_", StringComparison.Ordinal) && File.ReadAllText(Path.Combine(ContentDir, f)).Contains(alvo));
                if (achou is null) { SendTo(s, new ContentFileMessage(files, Info: $"Nenhum arquivo tem \"{m.Arg}\".")); return; }
                SendTo(s, new ContentFileMessage(files, achou, File.ReadAllText(Path.Combine(ContentDir, achou)), "procurar:" + (giver ? alvo : m.Arg)));
                return;
            }
            case "arquivo_ler":
            {
                if (Seguro(m.Arg) is not { } path) { SendTo(s, new ContentFileMessage(files, Info: "Arquivo não encontrado.")); return; }
                SendTo(s, new ContentFileMessage(files, m.Arg, File.ReadAllText(path)));
                return;
            }
            case "arquivo_salvar":
            {
                if (Seguro(m.Arg) is not { } path || m.Extra is null) { SendTo(s, new ContentFileMessage(files, Info: "Arquivo não encontrado.")); return; }
                try { JsonNode.Parse(m.Extra); }
                catch (Exception e) { SendTo(s, new ContentFileMessage(files, m.Arg, m.Extra, "NÃO salvou: o JSON tem erro — " + e.Message)); return; }
                var hist = Path.Combine(ContentDir, "editor_historico", "arquivos");
                Directory.CreateDirectory(hist);
                File.Copy(path, Path.Combine(hist, Path.GetFileNameWithoutExtension(path) + "_" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"), overwrite: true);
                File.WriteAllText(path, m.Extra.EndsWith('\n') ? m.Extra : m.Extra + "\n");
                GameLog.Info("servidor.editor", $"{Who(s)} salvou o arquivo {m.Arg}");
                var mapa = m.Arg!.StartsWith("map_", StringComparison.Ordinal);
                if (mapa)
                    try
                    {
                        var layout = WorldContentLoader.ParseMap(m.Extra);
                        if (_zones.TryGetValue(layout.Id, out var z) && !z.Instanced) { _drafts.Remove(layout.Id); ApplyEditedLayout(z, layout); }
                    }
                    catch (Exception) { }
                SendTo(s, new ContentFileMessage(files, m.Arg, m.Extra, mapa ? "Salvo — o mapa já mudou." : "Salvo (a versão anterior ficou em editor_historico/arquivos). Feche e abra o jogo para valer."));
                return;
            }
        }
    }

    private static bool SameSpawn(MapSpawn a, MapSpawn b)
        => a.Monster == b.Monster && a.Level == b.Level && Math.Abs(a.X - b.X) < 0.01 && Math.Abs(a.Y - b.Y) < 0.01 && a.Night == b.Night;

    private static string Label(JsonObject n) => (n["name"] ?? n["label"] ?? n["monster"] ?? n["kind"] ?? n["id"])?.ToString() ?? "item";

    /// <summary>O que estiver mais perto (até 3 m) em qualquer lista do mapa — para selecionar, mover ou girar.</summary>
    private static (JsonObject Node, string List)? NearestNode(JsonObject root, double x, double y, bool rotatable)
    {
        (JsonObject Node, double D, string List)? best = null;
        foreach (var key in rotatable ? new[] { "props", "npcs" } : new[] { "spawns", "props", "obstacles", "npcs", "portals", "objects", "waypoints", "chests", "gates" })
        {
            if (root[key] is not JsonArray arr) continue;
            foreach (var n in arr.OfType<JsonObject>())
            {
                if (n["x"] is null || n["y"] is null) continue;
                var d = Math.Sqrt(Math.Pow(Num(n["x"]) - x, 2) + Math.Pow(Num(n["y"]) - y, 2));
                // 13/11: pincelada de chão só é pega se não houver outra coisa perto (não atrapalha selecionar o resto)
                if (n["kind"]?.ToString().StartsWith("pintura_", StringComparison.Ordinal) == true) d += 2.5;
                if (d <= 3 && (best is null || d < best.Value.D)) best = (n, d, key);
            }
        }
        return best is null ? null : (best.Value.Node, best.Value.List);
    }

    /// <summary>Apaga o que estiver mais perto do clique (até 2,5 m): monstro, peça de cenário, árvore ou rocha.</summary>
    private static string RemoveNearest(JsonObject root, double x, double y)
    {
        (JsonArray Arr, JsonNode Node, double D, string What)? best = null;
        void Look(string key, Func<JsonNode, bool> ok, Func<JsonNode, string> name)
        {
            if (root[key] is not JsonArray arr) return;
            foreach (var n in arr)
            {
                if (n is null || !ok(n)) continue;
                var d = Math.Sqrt(Math.Pow(n["x"]!.GetValue<double>() - x, 2) + Math.Pow(n["y"]!.GetValue<double>() - y, 2));
                if (d <= 2.5 && (best is null || d < best.Value.D)) best = (arr, n, d, name(n));
            }
        }
        Look("spawns", _ => true, n => "tirou o monstro " + n["monster"]);
        Look("props", _ => true, n => "tirou " + n["kind"]);
        Look("obstacles", n => n["kind"]?.GetValue<string>() is "arvore" or "rocha" || n["deco"] is not null, n => "tirou " + n["kind"]);
        if (best is null) return "";
        best.Value.Arr.Remove(best.Value.Node);
        return best.Value.What;
    }

    /// <summary>Troca o mapa da zona sem derrubar ninguém: colisão nova, monstros novos entram, os apagados saem.</summary>
    private void ApplyEditedLayout(Zone zone, MapLayout layout)
    {
        _world!.ReplaceMap(layout);
        zone.Layout = layout;
        zone.Geo = new MapGeometry(layout);
        var ids = layout.Spawns.Select(sp => "m_" + sp.Id).ToHashSet();
        zone.Monsters.RemoveAll(mon => mon.Spawn is not null && mon.Master is null && !ids.Contains(mon.Id));
        // 12/11: ponto mudado no editor (posição, nível, monstro…) = o monstro daquele ponto renasce já do jeito novo
        var byId = layout.Spawns.ToDictionary(sp => "m_" + sp.Id);
        zone.Monsters.RemoveAll(mon => mon.Spawn is { } old && mon.Master is null && byId.TryGetValue(mon.Id, out var now) && !SameSpawn(old, now));
        foreach (var spawn in layout.Spawns.Where(sp => zone.Monsters.All(mon => mon.Id != "m_" + sp.Id)))
            if (_content.Monsters.TryGetValue(spawn.Monster, out var def) && _world.Behaviors.ContainsKey(def.BehaviorId))
                zone.Monsters.Add(NewSpawnedMonster(zone, spawn, def));
        foreach (var p in zone.Players) SendMapState(p);
    }
}
