using CinzasDaMare.Client.Sandbox.Core;
using CinzasDaMare.Client.Controllers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinzasDaMare.Client.Visuals;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Cliente (E8). É só uma casca: lê teclado/mouse, converte em INTENÇÕES (o servidor decide tudo) e desenha o que o
/// servidor mandou. Coordenadas: (x, y) do servidor = (X, Z) do Godot.
/// Fluxo: tela de conexão → lobby (criar/escolher Guerreiro ou Mago) → mundo em 3ª pessoa.
/// Visual: bonecos articulados de formas simples (Visuals/), substituíveis por modelos depois (IEntityVisual).
/// </summary>
public partial class Main : Node3D
{
    private const long MoveRefreshMs = 400;
    private const long InputBufferMs = 450;

    private sealed class EntityState
    {
        public IEntityVisual Visual = null!;
        public float Lift;                       // 11/11 noite: gárgula no poleiro / no mergulho
        public bool? Sombra;                     // 13/11: monstro longe (26 m+) não projeta sombra
        public Node3D? Beam;                     // raio do olho alado / chefes
        public Node3D? Chicote;                  // 18/11: chicotada que puxa
        public CpuParticles3D? Fire;             // 12/11: sopro de fogo (gárgula)
        public float ChillT;
        public OmniLight3D? FireLight;
        public Node3D? Carry;                    // 11/11: barril de pólvora nas mãos
        public Node3D? Espadas;                  // 18/11 19h: Espadas Voadoras (Campeão)
        public Label3D? Zz;                      // monstro dormindo
        public List<(MeshInstance3D Mesh, int Surface, Material? Before)>? ClosedEyes;   // 11/11 noite: olhos fechados dormindo
        public Vector3 Pos, PrevPos;
        public float Yaw, Speed;
        public bool Placed;
        public string Gear = "";
        public string FormKey = "";              // 12/11: transformação ativa (troca o visual)
        public string Def = "";                  // 13/11: definição do monstro (quem sangra no golpe)
        public string LastAction = "";
        public int LastSeq;
        public TelegraphView? Telegraph;
        public string ClassId = "";
        public float DustT;                       // próxima poeira de passo
        public int XisFase;                       // 18/11: garras da unhada em X já desenhadas neste golpe
        public float Ground;                     // 11/11: altura do chão (escada/assoalho do casarão), suavizada
        public float Lean, Bank;                 // inclinação ao arrancar/frear e ao fazer curva (peso no corpo)
        public Vector3 SnapPos, SnapVel;         // última posição do servidor e velocidade estimada entre fotos
        public double SnapTime;
        public long SnapTick = -1;
        public float Fade;                       // 0..1 ao virar cinza
        public Node3D? Barrel, Mound;            // caranguejos do lote 2: barril explosivo em voo, monte de terra do Escavador
        public string FxAction = "";
        public float FxDust;
        public float DeadFor;                    // monstro: segundos desde a morte (some afundando, como no MU)
        public ShaderMaterial? Rim;              // contorno do monstro (fica vermelho de leve quando é o alvo)
        public bool RimTarget;
        public bool WasAlive = true;
        public bool WasEnraged;
        public OmniLight3D? Rage;
        public bool Dourado;   // 14/11 tarde: monstro da Invasão Dourada (pintado de ouro uma vez)
        public IEntityVisual? Mount;
        public string? MountKind;
        public float MountRise = 1f;   // 13/11: altura da sela da montaria 3D
        public float GotaT;                      // 13/11: gotas do status (sangue, veneno, maldição, fogo)
        public Node3D? Bubble, Mist, Mark;   // Barreira de Éter, Aura Primal (enfraquecido), Olhar do Caçador (marcado)
        public bool Stealth;
        public int LastLevel;
    }

    private sealed class PetState
    {
        public IEntityVisual Visual = null!;
        public string Kind = "";
        public Vector3 Pos;
        public float Yaw;
        public float Speed;   // 15/11: velocidade de verdade (suavizada), não a distância até o alvo
    }

    private readonly Dictionary<string, PetState> _pets = new();

    /// <summary>Efeito no chão vindo do servidor (E9: área em chamas da Explosão do Arcanista).</summary>
    private sealed class GroundEffectState
    {
        public Node3D Root = null!;
        public MeshInstance3D Disc = null!;
        public float Phase;
        public string Kind = "";
    }

    private readonly Dictionary<string, GroundEffectState> _groundEffects = new();

    private sealed class ItemState
    {
        public Node3D Root = null!;
        public Node3D Model = null!;
        public float Phase;
        public float Pop = 1f;   // 0 → acabou de cair (salta do chão), 1 → parado
    }
    private ulong _itemsQuietUntil;

    private sealed class ProjectileState
    {
        public float Ground;   // 13/11: altura do chão por baixo (morro, rampa), suavizada
        public Node3D Root = null!;
        public ProjectileView View = null!;
        public double ReceivedAt;
    }

    private readonly ClientModel _model = new();
    private readonly CooldownTracker _cooldowns = new();
    private readonly Effects _effects = new();
    private readonly ConcurrentQueue<NetMessage> _incoming = new();
    private readonly FaceThrottle _faceThrottle = new();
    private readonly MoveSender _moveSender = new();
    private readonly ThirdPersonCamera _cam = new();
    private Desobstrucao? _desobstrucao;
    private MapView? _desobstrucaoMapa;
    private readonly ChatView _chat = new();
    private readonly LivroPanel _livro = new();
    private readonly PesquisaPanel _pesquisa = new();
    private readonly MesclarPanel _mesclar = new();   // 18/11: Máquina de Mesclar Visual (Astrália)   // 17/11: Catálogo da Biblioteca
    private readonly Dicas _dicas = new();   // 13/11
    private readonly TrilhaPanel _trilha = new();   // 13/11: Trilha do Herói (F3)
    private readonly AmigosPanel _amigos = new();   // 18/11: lista de amigos (F4)
    private bool _menuDemo;
    private readonly MenuTabPanel _menuTab = new();   // 14/11: menu do TAB (objetivos, cristais)
    private readonly LivroMonstrosPanel _bestiario = new();   // 12/11: Livro dos Monstros pessoal (L)       // 12/11: livros da Biblioteca do Continente
    private readonly VinteUmPanel _vinteUm = new();   // 12/11: mesa de Vinte-e-Um (2º andar do casarão)     // 12/11: chat global / privado (Enter)
    private readonly TrocaPanel _troca = new();   // 11/11: troca entre jogadores (tecla U)
    private readonly Dictionary<string, EntityState> _players = new();
    private readonly Dictionary<string, EntityState> _monsters = new();
    private readonly Dictionary<string, ItemState> _items = new();
    private readonly Dictionary<string, ProjectileState> _projectiles = new();
    private readonly Node3D _world = new() { Name = "Mundo" };

    private TcpGameClient? _client;
    private int _disconnected;
    private int _generation;
    private volatile string _statusText = "";
    private bool _connecting;
    private long _lastMoveSentMs;
    private int _lastLevel;
    private double _time;

    private MapView? _mapView;
    private ArenaScene? _arena;
    private float _arenaHalf;
    private PanelContainer _loginPanel = null!;
    private Control _loginBackground = null!;
    private ColorRect _loginDim = null!;
    private LineEdit _hostEdit = null!;
    private LineEdit _portEdit = null!;
    private LineEdit _tokenEdit = null!;
    private Label _statusLabel = null!;
    private HudView _hud = null!;
    private LobbyView _lobby = null!;
    private InventoryPanel _inventory = null!;
    private WorldPanels _panels = null!;
    private GodPanel? _god;
    // 12/11: contrato do jogador (o oficial de sempre atrás de IPlayerController) + laboratório de protótipos (F1 → Laboratório)
    private NetworkPlayerController? _official;
    private PlayerSandboxManager? _lab;
    private bool _labOwnedLast;
    private bool LabOwnsHero => _lab?.OwnsHero == true;
    /// <summary>12/11: pacote do amigo (arquivo amigo.txt na pasta do cliente) — sem F1 (modo deus), editor e laboratório.</summary>
    private static readonly bool AmigoBuild = Godot.FileAccess.FileExists("res://amigo.txt");
    private EditorView _editor = null!;   // 11/11: editor no jogo (F1 → EDITAR AGORA)
    private bool _forceMapRebuild;
    private bool _bossPerto;
    // 13/11: troca de teclas e histórico grande (Teclas.cs)
    private PanelContainer _teclasPanel = null!, _histPanel = null!;
    private VBoxContainer _teclasLista = null!, _teclasCorpo = null!;
    private RichTextLabel _histTexto = null!;
    private Key? _teclaEsperando;
    private RodaPoses _posesPanel = null!;

    private void CriarOpcoes(CanvasLayer layer)
    {
        _teclasPanel = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(430, 0) };
        _teclasPanel.AddThemeStyleboxOverride("panel", HudView.PanelStyle(12f, 0.92f));
        _teclasPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        _teclasPanel.Position = new Vector2(-215, -300);
        var col = _teclasCorpo = new VBoxContainer();
        col.AddChild(HudView.MakeLabel("Teclas", 18, new Color(0.95f, 0.78f, 0.30f)));
        col.AddChild(HudView.MakeLabel("Clique na tecla de uma ação e aperte a nova (ESC cancela).", 12));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(410, 440), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _teclasLista = new VBoxContainer();
        scroll.AddChild(_teclasLista);
        col.AddChild(scroll);
        var padrao = new Button { Text = "Voltar ao padrão" };
        padrao.Pressed += () => { Teclas.Padrao(); MontarTeclas(); };
        col.AddChild(padrao);
        _teclasPanel.AddChild(col);
        layer.AddChild(_teclasPanel);
        Arrastador.Ligar(_teclasPanel, "teclas", 40f, fechar: () => { _teclasPanel.Visible = false; _teclaEsperando = null; });

        _histPanel = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(720, 0) };
        _histPanel.AddThemeStyleboxOverride("panel", HudView.PanelStyle(12f, 0.92f));
        _histPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        _histPanel.Position = new Vector2(-360, -280);
        var hc = new VBoxContainer();
        hc.AddChild(HudView.MakeLabel("Histórico — chat e avisos (O fecha)", 18, new Color(0.95f, 0.78f, 0.30f)));
        _histTexto = new RichTextLabel { BbcodeEnabled = true, ScrollFollowing = true, CustomMinimumSize = new Vector2(700, 500), SelectionEnabled = true };
        _histTexto.AddThemeFontSizeOverride("normal_font_size", 14);
        hc.AddChild(_histTexto);
        _histPanel.AddChild(hc);
        layer.AddChild(_histPanel);
        Arrastador.Ligar(_histPanel, "historico", 40f, fechar: () => AbrirHistorico(false));

        // 13/11 (pedido: "ao apertar Z abre um círculo com as animações"): roda de poses (RodaPoses.cs)
        _posesPanel = new RodaPoses();
        _posesPanel.Escolheu += pose =>
        {
            if (pose.Length > 0) Send(new InteractIntent("pose:" + pose));
            CaptureMouse(!_inventory.Visible && !_panels.AnyOpen && !GodOpen);
        };
        layer.AddChild(_posesPanel);
    }

    private void MontarTeclas()
    {
        foreach (var c in _teclasLista.GetChildren()) c.QueueFree();
        foreach (var (acao, padrao) in Teclas.Lista)
        {
            var row = new HBoxContainer();
            var l = HudView.MakeLabel(acao, 14);
            l.CustomMinimumSize = new Vector2(250, 0);
            row.AddChild(l);
            var b = new Button { Text = _teclaEsperando == padrao ? "aperte..." : Teclas.Nome(Teclas.De(padrao)), CustomMinimumSize = new Vector2(120, 0), FocusMode = Control.FocusModeEnum.None };
            var p = padrao;
            b.Pressed += () => { _teclaEsperando = p; MontarTeclas(); };
            row.AddChild(b);
            _teclasLista.AddChild(row);
        }
    }

    private void AbrirHistorico(bool abrir)
    {
        _histPanel.Visible = abrir;
        if (abrir)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (hora, canal, texto) in _model.Historico)
            {
                var tl = texto.ToLowerInvariant();   // 18/11 19h: morreu/perdeu/gastou = vermelho; entrou num lugar = amarelo
                var cor = (tl.Contains("morreu") || tl.Contains("perdeu") || tl.Contains("gastou") || tl.Contains("pagou") || tl.Contains("derrotado")) ? "#ff6b5b"
                    : (tl.Contains("entrou") || tl.Contains("chegou") || tl.Contains("viajou") || tl.Contains("bem-vindo")) ? "#f2d15a" : canal switch
                {
                    "privado" => "#f29af2", "grupo" => "#8ce6ff", "sistema" => "#f2c066",
                    _ when canal.StartsWith("aviso:reward") => "#9be27a", _ when canal.StartsWith("aviso:warn") => "#ff8a70",
                    _ when canal.StartsWith("aviso:legend") => "#ffd36b", _ when canal.StartsWith("aviso") => "#c9c2b4", "registro" => "#9fb8d6", _ => "#eeeae0",
                };
                sb.Append($"[color=#8a8478]{hora}[/color]  [color={cor}]{texto.Replace("[", "[lb]")}[/color]\n");
            }
            _histTexto.Text = sb.Length == 0 ? "Nada ainda." : sb.ToString();
        }
        CaptureMouse(!abrir && !_inventory.Visible && !_panels.AnyOpen && !GodOpen);
    }

    private VBoxContainer _opcoesBox = null!;   // 14/11 tarde: miolo das opções (ESC e aba Opções do TAB)
    private bool _pausePedido;   // 13/11: o menu do ESC só abre pelo ESC   // 13/11 ("chefão de evento fica com várias barras de vida na frente"): com chefe perto, só a barra dele
    private bool GodOpen => _god?.Visible == true;
    private NpcDialogMessage? _lastDialog;
    private PanelContainer _pausePanel = null!;
    private MeshInstance3D _aimMarker = null!;
    private Node3D _lockMarker = null!;

    // entrada
    private string? _queued;
    private long _queuedAt;
    private long _actionSentAt;
    private long _aimUntil;
    private string? _lockedId;
    private (double X, double Y) _moveDir;
    private (double X, double Y)? _desiredFace;
    private long _dodgeReadyAt;
    private int _esqUsadas;
    private long _esqVoltaAt;
    private string? _zoneId;

    // automação (atalho e verificação): --character, --create, --shots, --quit-after, --cam-yaw
    private string? _autoCharacter;
    private string? _autoCreate;
    private readonly List<(double At, string Path)> _shots = new();
    private double _quitAfter = -1;
    private string? _demo;            // --demo luta: anda até o monstro mais próximo e ataca (teste automático)
    private bool _demoInventory;      // --demo-mochila: abre a mochila (captura de tela)
    private string _demoPanel = "";   // --demo-painel poderes|stats|journal: abre um painel (captura de tela)
    private string _demoTalk = "";
    private float _demoAuraAt = -1f;  // --demo-aura seg: mostra a aura de nível no próprio personagem (captura de tela)    // --demo-falar npc_id: fala com o NPC (perto) — captura da loja
    private readonly List<(double At, string What)> _demoUi = new(); // --abrir "2=npc;5=mapa": abre telas (verificação visual)
    private (double X, double Y)? _demoWalk;                          // --abrir "2=andar:0,-1": anda numa direção do mundo
    private double _sessionTime;
    private readonly GuideHints _guide = new();

    // ------------------------------------------------------------ ciclo de vida

    private double _fpsSum, _fpsMin = double.MaxValue, _fpsTime;
    private int _fpsFrames;

    // 14/11 ("quando sentar, uma bolinha carregando para totalizar o buff")
    private double _sentadoHa = -1, _cheioHa;
    private void BolinhaDoDescanso(float dt)
    {
        var sentado = _model.Me is { } me && me.Action is "sit" or "deitado";
        var dur = _model.DescansoSegundos;
        if (!sentado) { _sentadoHa = -1; _hud.Descanso(null, false); return; }
        if (_sentadoHa < 0) { _sentadoHa = 0; _cheioHa = 0; }
        _sentadoHa += dt;
        var t = (float)(_sentadoHa / dur);
        if (t >= 1f) { _cheioHa += dt; if (_cheioHa > 2.0) { _hud.Descanso(null, false); return; } }
        Vector2? pes = MyState() is { } eu && !_cam.Camera.IsPositionBehind(eu.Pos) ? _cam.Camera.UnprojectPosition(eu.Pos) : null;
        _hud.Descanso(t, _model.DescansoNaCama, pes);
    }

    // ------------------------------------------------------------ 18/11: decoração — escolher o lugar
    // (dono: "a decoração eu tenho que escolher onde quero colocar! clica e aparece a seleção; cada objeto tem seu lugar,
    // ou parede, ou chão, ou sobre algo"). Usou a peça em casa → ela segue o mouse; parede gruda na parede mais perto
    // virada para dentro; "sobre" sobe em cima de mesa/prateleira/estante perto; a rodinha gira; clique confirma.
    private string? _decorColocando;
    private Node3D? _decorFantasma;
    private float _decorGiro;
    private (float X, float Y, float Yaw, float Alt) _decorOnde;

    private bool EmCasaPropria() => _mapView is { } mv && CinzasDaMare.Shared.World.AssentosCasa.TerrenoOuCasa(mv.Map.Id)
        && _model.Flags.Contains("casa:propria");

    /// <summary>18/11 15h (dono: "o F é o mesmo que interage com as coisas e o mesmo que retira os itens colocados — fica muito
    /// fácil de tirar e polui a tela; pra editar é Ctrl+F, aí habilita o mover no lugar do F"): modo editar da casa.</summary>
    private bool _editandoCasa;

    private (string Id, Node3D No)? DecorPerto()
    {
        if (_editandoCasa && !EmCasaPropria()) _editandoCasa = false;   // saiu de casa: sai do modo editar
        if (!_editandoCasa || _decorColocando is not null || _mapView is null || !EmCasaPropria() || MyState() is not { } eu) return null;
        (string, Node3D)? melhor = null; var md = 1.8f;
        foreach (var (id, no) in _mapView.DecorNos)
        {
            if (!GodotObject.IsInstanceValid(no)) continue;
            var d = new Vector2(no.GlobalPosition.X - eu.Pos.X, no.GlobalPosition.Z - eu.Pos.Z).Length();
            if (d < md) { md = d; melhor = (id, no); }
        }
        return melhor;
    }

    private void ComecarDecor(string id)
    {
        if (_mapView is null || !CasaDecor.Conhece(id)) return;
        CancelarDecor();
        _decorColocando = id;
        _mapView.DecorEmPosicionamento = id;
        _decorFantasma = new Node3D { Name = "DecorFantasma" };
        _mapView.Root.AddChild(_decorFantasma);
        CasaDecor.Build(_decorFantasma, id, (_, _) => Vector3.Zero);
        var salvo = CasaDecor.Salvo(_model.Flags, id, _mapView.Map.Id);
        _decorGiro = salvo?.Yaw ?? 0f;
        _decorNova = salvo is null;   // peça nova (sem lugar): cancelar devolve para a mochila
        // 18/11 19h: a mochila fecha sozinha — o Esc de fechar a mochila não pode cancelar (e sumir com) a peça
        _inventory.Visible = false;
        CaptureMouse(false);
        Sfx.Play("ui_clique");
    }

    private bool _decorNova;
    private CinzasDaMare.Shared.World.MapGeometry? _geoDecor;
    private string? _geoDecorMapa;

    /// <summary>18/11 19h: Esc / botão direito — peça nova volta para a mochila (antes ficava "colocada" em lugar nenhum).</summary>
    private void DesistirDecor()
    {
        if (_decorColocando is { } id && _decorNova)
        {
            Send(new DecorPlaceIntent(id, 0, 0, 0, -1));
            _hud.ShowNotice(ClientModel.DisplayName(id) + " voltou para a mochila.");
        }
        CancelarDecor();
    }

    private void CancelarDecor()
    {
        if (_decorFantasma is not null && GodotObject.IsInstanceValid(_decorFantasma)) _decorFantasma.QueueFree();
        _decorFantasma = null;
        _decorColocando = null;
        if (_mapView is not null) _mapView.DecorEmPosicionamento = null;
    }

    private bool DecorInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape }) { DesistirDecor(); return true; }
        // 18/11 19h (dono: "não vi a tecla que é pra virar"): Q e E também giram (e a dica fica na tela)
        if (e is InputEventKey { Pressed: true, Keycode: Key.Q }) { _decorGiro += 15f; return true; }
        if (e is InputEventKey { Pressed: true, Keycode: Key.E }) { _decorGiro -= 15f; return true; }
        if (e is not InputEventMouseButton { Pressed: true } mb) return false;
        if (mb.ButtonIndex == MouseButton.WheelUp) { _decorGiro += 15f; return true; }
        if (mb.ButtonIndex == MouseButton.WheelDown) { _decorGiro -= 15f; return true; }
        if (mb.ButtonIndex == MouseButton.Right) { DesistirDecor(); return true; }
        if (mb.ButtonIndex == MouseButton.Left && _decorColocando is { } id)
        {
            Send(new DecorPlaceIntent(id, _decorOnde.X, _decorOnde.Y, _decorOnde.Yaw, _decorOnde.Alt));
            Sfx.Play("equipar");
            CancelarDecor();
            return true;
        }
        return false;
    }

    private void DecorLoop()
    {
        while (_model.DecorNovo.Count > 0)
        {
            var novo = _model.DecorNovo.Dequeue();
            // 18/11 15h: peça de fora só se coloca no terreno; a de dentro, só nos cômodos
            if (EmCasaPropria() && CinzasDaMare.Shared.World.AssentosCasa.DeFora(novo) == CinzasDaMare.Shared.World.AssentosCasa.Lote(_mapView!.Map.Id)) ComecarDecor(novo);
        }
        if (_decorColocando is null || _decorFantasma is null || _mapView is null) return;
        if (!EmCasaPropria()) { CancelarDecor(); return; }
        if (GroundUnderPointer() is not { } g) return;
        var L = _mapView.Map;
        // parede interna: o muro mais perto (caixas "muro"/"parede" do cômodo)
        var lote = CinzasDaMare.Shared.World.AssentosCasa.Lote(L.Id);   // 18/11 15h: no quintal não há "cômodo" para limitar
        var paredes = lote ? new List<CinzasDaMare.Shared.World.MapObstacle>() : L.Obstacles.Where(o => o.Shape == "box" && (o.W > 4 || o.H > 4)).ToList();
        float x = g.X, y = g.Z, yaw = _decorGiro, alt = 0f;
        var cat = CasaDecor.Categoria(_decorColocando);
        if (cat == "parede" && paredes.Count > 0)
        {
            double Dist(CinzasDaMare.Shared.World.MapObstacle o) => o.W > o.H ? Math.Abs(y - o.Y) - o.H / 2 : Math.Abs(x - o.X) - o.W / 2;
            var w = paredes.OrderBy(Dist).First();
            if (w.W > w.H)
            {
                var face = (float)(w.Y + (y > w.Y ? 1 : -1) * (w.H / 2 + 0.06));
                y = face; x = (float)Math.Clamp(x, w.X - w.W / 2 + 0.8, w.X + w.W / 2 - 0.8);
                yaw = (float)(face > w.Y ? 0 : 180);
            }
            else
            {
                var face = (float)(w.X + (x > w.X ? 1 : -1) * (w.W / 2 + 0.06));
                x = face; y = (float)Math.Clamp(y, w.Y - w.H / 2 + 0.8, w.Y + w.H / 2 - 0.8);
                yaw = (float)(face > w.X ? 90 : -90);
            }
        }
        else if (cat == "sobre")
        {
            // em cima de mesa/prateleira/estante perto (até 1,2 m), senão no chão
            var apoio = L.Props.Where(p => p.Kind is "movel_mesa" or "movel_prateleira" or "movel_caixas" or "movel_banqueta")
                .OrderBy(p => (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y)).FirstOrDefault();
            if (apoio is not null && Math.Sqrt((apoio.X - x) * (apoio.X - x) + (apoio.Y - y) * (apoio.Y - y)) < 1.2)
            {
                x = (float)apoio.X; y = (float)apoio.Y;
                alt = apoio.Kind switch { "movel_mesa" => 0.82f, "movel_prateleira" => 1.1f, "movel_banqueta" => 0.5f, _ => 0.7f };
            }
        }
        // dentro do cômodo
        var lim = (float)L.HalfSize - (lote ? 2f : 0.6f);
        if (paredes.Count > 0)
            lim = (float)paredes.Select(o => o.W > o.H ? Math.Abs(o.Y) - o.H / 2 : Math.Abs(o.X) - o.W / 2).Where(v => v > 2).DefaultIfEmpty(lim).Min() - (cat == "parede" ? -0.07f : 0.4f);
        x = Mathf.Clamp(x, -lim, lim); y = Mathf.Clamp(y, -lim, lim);
        // 18/11 19h (dono: "apliquei um torto e ele meio que sumiu na parede"): no chão a peça não entra em parede nem em
        // móvel — se o mouse aponta para dentro de um, ela fica no último lugar livre
        if (cat != "parede")
        {
            if (_geoDecorMapa != L.Id) { _geoDecor = new CinzasDaMare.Shared.World.MapGeometry(L); _geoDecorMapa = L.Id; }
            if (_geoDecor is { } gd && !gd.IsFree(x, y, 0.45) && cat == "chão") { x = _decorOnde.X; y = _decorOnde.Y; }
        }
        _decorOnde = (x, y, yaw, alt);
        if (cat != "parede") _decorOnde.Yaw = _decorGiro;
        _decorFantasma.Position = new Vector3(x, alt, y);
        _decorFantasma.RotationDegrees = new Vector3(0, _decorOnde.Yaw, 0);
    }

    // 18/11 (dono: "enquanto estiver carregando a vida no círculo seguro, um efeitinho verde em volta do personagem, bem
    // sutil; quando encher, para"): anel verde fraco no chão + faíscas verdes subindo devagar, só enquanto a vida enche
    private CinzasDaMare.Shared.World.MapGeometry? _geoSeguro;
    private string? _geoSeguroMapa;
    private MeshInstance3D? _anelCura;
    private StandardMaterial3D? _anelCuraMat;
    private float _anelCuraA, _faiscaT;
    private void AuraDeCura(float dt)
    {
        var me = _model.Me; var st = MyState();
        var curando = false;
        if (me is not null && st is not null && _model.Map is { } L && me.Alive && me.Health < me.MaxHealth - 0.5)
        {
            if (_geoSeguroMapa != L.Id) { _geoSeguro = new CinzasDaMare.Shared.World.MapGeometry(L); _geoSeguroMapa = L.Id; }
            curando = _geoSeguro!.InSafeZone(me.X, me.Y);
        }
        _anelCuraA = Mathf.MoveToward(_anelCuraA, curando ? 1f : 0f, dt * 2.5f);
        if (_anelCura is null)
        {
            if (_anelCuraA <= 0f) return;
            _anelCuraMat = VisualKit.Unshaded(new Color(0.4f, 1f, 0.5f, 0f), true);
            _anelCura = new MeshInstance3D { Mesh = VisualKit.Ring(0.75f, 0.05f, 40), MaterialOverride = _anelCuraMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true };
            AddChild(_anelCura);
        }
        _anelCura.Visible = _anelCuraA > 0.01f && st is not null;
        if (!_anelCura.Visible || st is null) return;
        var t = (float)Time.GetTicksMsec() / 1000f;
        _anelCura.GlobalPosition = st.Pos + new Vector3(0, 0.05f, 0);
        _anelCura.Scale = Vector3.One * (1f + 0.06f * Mathf.Sin(t * 2.4f));
        _anelCuraMat!.AlbedoColor = new Color(0.4f, 1f, 0.5f, 0.22f * _anelCuraA);
        if (!curando) return;
        _faiscaT -= dt;
        if (_faiscaT > 0) return;
        _faiscaT = 0.18f;
        var ang = (float)GD.RandRange(0, Mathf.Tau);
        var p0 = st.Pos + new Vector3(Mathf.Cos(ang) * 0.6f, 0.15f, Mathf.Sin(ang) * 0.6f);
        var f = VisualKit.Part(_world, VisualKit.Sphere(0.025f), VisualKit.Glow(new Color(0.5f, 1f, 0.6f), 2.2f), p0);
        f.TopLevel = true; f.GlobalPosition = p0; f.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var tw = f.CreateTween().SetParallel();
        tw.TweenProperty(f, "global_position", p0 + new Vector3(0, (float)GD.RandRange(0.9, 1.5), 0), 1.1f).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(f, "scale", Vector3.One * 0.2f, 1.1f).SetEase(Tween.EaseType.In);
        tw.Chain().TweenCallback(Callable.From(f.QueueFree));
    }

    // 14/11 ("o barulho de mar acompanha o jogo todo"): sons do lugar conforme você anda — mar perto da água, lava,
    // ruína/cemitério/covil com sons estranhos, sapos no charco. O tema do mapa fica só com o fundo (vento, pássaros...).
    private readonly Dictionary<string, float> _lugarPesos = new();
    private double _lugarEm;
    private void SonsDoLugar(PlayerView? me, float dt)
    {
        _lugarEm -= dt;
        if (_lugarEm <= 0 && me is not null && _model.Map is { } L)
        {
            _lugarEm = 0.4;
            _lugarPesos.Clear();
            float Perto(double d, double cheio, double nada) => (float)Math.Clamp((nada - d) / Math.Max(0.1, nada - cheio), 0, 1);
            void Soma(string n, float w) { if (w > 0.01f) _lugarPesos[n] = Math.Max(_lugarPesos.GetValueOrDefault(n), w); }
            var x = me.X; var y = me.Y;
            // mar: na Costa (praia) e no Porto ele fica a leste
            if (!L.Instanced && (L.Theme == "praia" || L.Id.StartsWith("porto", StringComparison.Ordinal)))
            {
                var d = L.HalfSize - x;
                Soma("amb2_mar", Perto(d, 6, 45));
                Soma("amb2_gaivotas", Perto(d, 10, 60) * 0.9f);
            }
            foreach (var o in L.Obstacles)
            {
                var d = Math.Sqrt((o.X - x) * (o.X - x) + (o.Y - y) * (o.Y - y)) - Math.Max(o.R, Math.Max(o.W, o.H) / 2);
                if (d > 22) continue;
                switch (o.Kind)
                {
                    case "lago": Soma("amb2_mar", Perto(d, 2, 18) * 0.45f); break;
                    case "rocha_lava": Soma("amb2_lava", Perto(d, 2, 16)); break;
                    case "lapide" or "nicho_almas" or "ossada_gigante": Soma("amb2_criaturas", Perto(d, 3, 20)); Soma("amb_masmorra", Perto(d, 3, 16) * 0.7f); break;
                    case "ruina" or "coluna_quebrada": Soma("amb2_criaturas", Perto(d, 4, 18) * 0.5f); break;
                }
            }
            foreach (var p in L.Props)
            {
                if (p.Kind is not ("fogueira" or "tocha_poste" or "movel_lareira" or "glb:cenario/acropole_braseiro" or "cascata_reinos" or "fonte_praca")) continue;
                var d = Math.Sqrt((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y));
                if (p.Kind is "cascata_reinos" or "fonte_praca") Soma("amb2_mar", Perto(d, 2, 12) * 0.35f);
                else Soma("loop_fogueira", Perto(d, 1.5, 9) * 0.8f);
            }
            foreach (var z in L.Zones)
            {
                if (Math.Abs(z.X - x) > z.W / 2 || Math.Abs(z.Y - y) > z.H / 2) continue;
                if (z.Kind is "secreto" or "segredo" or "chefe") { Soma("amb2_criaturas", 0.8f); Soma("amb_masmorra", 0.7f); }
                var nome = z.Name.ToLowerInvariant();
                if (nome.Contains("cemit") || nome.Contains("cripta") || nome.Contains("covil") || nome.Contains("ruína") || nome.Contains("ruina")) { Soma("amb2_criaturas", 0.9f); Soma("amb_masmorra", 0.6f); }
                if (nome.Contains("charco") || nome.Contains("pântano") || nome.Contains("lago")) Soma("amb2_sapos", 0.8f);
            }
        }
        Sfx.Lugar(_lugarPesos, dt);
        if (MyState() is { } euSom) Sfx.Lacos3DPerto(euSom.Pos);   // 17/11 noite: forja/caldeirão só perto
    }

    /// <summary>
    /// 14/11 ("meu amigo está na u55 no Esc"): atualização baixada/entregue que ficou sem aplicar = o jogo foi aberto sem
    /// passar pelo JOGAR KRYMOR.cmd (é ele que aplica). Avisa grande na tela, com o que fazer.
    /// </summary>
    private void AvisoAtualizacaoParada()
    {
        try
        {
            var client = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
            var raiz = System.IO.Directory.GetParent(client)?.FullName ?? client;
            var zip = System.IO.Directory.GetFiles(raiz, "atualizacao_pendente*.zip*").Length > 0;   // também "_1", "(1)"
            var pasta = System.IO.Directory.Exists(System.IO.Path.Combine(raiz, "atualizacao_pendente"));
            if (!zip && !pasta) return;
            GameLog.Warn("cliente", $"atualização parada sem aplicar (zip={zip}, pasta={pasta}) em {raiz}");
            var layer = new CanvasLayer { Layer = 120 };
            AddChild(layer);
            var box = new PanelContainer { Position = new Vector2(20, 20) };
            box.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.45f, 0.05f, 0.04f, 0.95f), ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10 });
            var lbl = new Label { Text = "ATUALIZAÇÃO NÃO APLICADA!\nFeche o jogo e abra pelo arquivo \"JOGAR KRYMOR.cmd\" (ou Jogar.cmd) na pasta do jogo —\né ele que instala a versão nova. Abrindo direto pelo Godot, você continua na versão " + CinzasDaMare.Shared.VersaoJogo.Atual + "." };
            lbl.AddThemeFontSizeOverride("font_size", 18);
            box.AddChild(lbl);
            layer.AddChild(box);
        }
        catch (Exception e) { GameLog.Warn("cliente", "aviso de atualização: " + e.Message); }
    }

    public override void _Ready()
    {
        // 14/11 19h (dono: "tudo que acontece deve ir pro O"): recados do lobby e avisos locais também entram no histórico
        LobbyView.StatusMudou += t => _model.Registrar(t);
        HudView.AvisoLocal += t => _model.Registrar(t);
        ConfigureLog();
        Sfx.Init(this);
        Sfx.LoadSettings();   // volumes salvos (Esc → Configurações de som)
        ItemIconRenderer.Init(this);
        ItemTooltip.Model = _model;
        GraphicsQuality.ApplyViewport(GetViewport());
        GraphicsQuality.CarregarFps();   // 15/11 noite: limite de FPS e V-Sync escolhidos nas Opções
        GameLog.Info("cliente", $"Gráficos: {GraphicsQuality.Label(GraphicsQuality.Current)} ({RenderingServer.GetCurrentRenderingMethod()}), FPS {GraphicsQuality.FpsLabel(GraphicsQuality.FpsLimite)}, V-Sync {(GraphicsQuality.VSync ? "ligado" : "desligado")}");
        RenderingServer.SetDefaultClearColor(new Color(0.03f, 0.035f, 0.05f));
        AvisoAtualizacaoParada();
        AddChild(_world);
        AddChild(_cam.Camera);
        _cam.Camera.MakeCurrent();
        _cam.Camera.Position = new Vector3(0, 6, 8);
        AddChild(_effects.Root);
        _aimMarker = new MeshInstance3D { Mesh = VisualKit.Ring(2.8f, 0.08f), MaterialOverride = VisualKit.Unshaded(new Color(0.5f, 0.75f, 1f, 0.6f), true), Visible = false };
        AddChild(_aimMarker);
        _lockMarker = new Node3D { Visible = false };
        VisualKit.Part(_lockMarker, VisualKit.Cylinder(0f, 0.16f, 0.28f, 4), VisualKit.Glow(new Color(1f, 0.85f, 0.3f), 2f), Vector3.Zero, new Vector3(180, 0, 0));
        AddChild(_lockMarker);
        GetWindow().Theme = HudView.BuildTheme();
        BuildUi();

        // Argumentos depois de "--": --host 127.0.0.1 --port 7777 --token XXXX --autoconnect [--character Nome] [--create Nome:classe]
        var args = OS.GetCmdlineUserArgs();
        string Arg(string key, string fallback)
        {
            var i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        _hostEdit.Text = Arg("--host", "127.0.0.1");
        _portEdit.Text = Arg("--port", "7777");
        _tokenEdit.Text = Arg("--token", "");
        _autoCharacter = Arg("--character", "");
        if (_autoCharacter.Length == 0) _autoCharacter = null;
        _autoCreate = Arg("--create", "");
        if (_autoCreate.Length == 0) _autoCreate = null;
        foreach (var part in Arg("--shots", "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && double.TryParse(kv[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var at)) _shots.Add((at, kv[1]));
        }
        if (double.TryParse(Arg("--quit-after", "-1"), NumberStyles.Float, CultureInfo.InvariantCulture, out var quit)) _quitAfter = quit;
        if (float.TryParse(Arg("--cam-yaw", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var yaw)) _cam.Yaw = Mathf.DegToRad(yaw);
        if (float.TryParse(Arg("--cam-pitch", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var pitch) && pitch != 0) _cam.Rotate(new Vector2(0, -Mathf.DegToRad(pitch) / _cam.Sensitivity));
        if (float.TryParse(Arg("--cam-zoom", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var zoom) && zoom != 0) _cam.Zoom(zoom);
        _demo = Arg("--demo", "") is { Length: > 0 } demo ? demo : null;
        _demoInventory = Array.IndexOf(args, "--demo-mochila") >= 0;
        _demoPanel = Arg("--demo-painel", "");
        _demoTalk = Arg("--demo-falar", "");
        if (float.TryParse(Arg("--demo-aura", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var aura)) _demoAuraAt = aura;
        if (Array.IndexOf(args, "--boneco-classico") >= 0) Visuals.VisualFactory.UseMannequin = false;
        foreach (var part in Arg("--abrir", "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && double.TryParse(kv[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var at)) _demoUi.Add((at, kv[1]));
        }
        if (Arg("--vitrine", "") is { Length: > 0 } vitrine)
        {
            // vitrine visual offline (comparar com as pranchas): sem login, sem HUD, câmera fixa
            foreach (var child in GetChildren()) if (child is CanvasLayer ui) ui.Visible = false;
            Showcase.Build(this, _cam.Camera, vitrine, Arg("--vitrine-acao", "idle"));
            _vitrine = true;
            return;
        }
        // 12/11: abertura do KRYMOR (pula com qualquer tecla); nunca em automação
        if ((_shots.Count == 0 && _quitAfter < 0 || OS.GetEnvironment("CDM_ABERTURA") == "1") && Array.IndexOf(args, "--sem-abertura") < 0) AberturaView.Tocar(this);
        if (Array.IndexOf(args, "--autoconnect") >= 0 && _tokenEdit.Text.Length > 0)
            _ = ConnectAsync();
    }

    public override void _ExitTree()
    {
        Explorado.Salvar();   // 16/11: névoa do mapa
        GameLog.Info("cliente", "fechado");
        Interlocked.Increment(ref _generation); // a queda da conexão a partir daqui é o próprio fechamento
        _client?.Dispose();
        GameLog.Close();
    }

    /// <summary>logs\cliente-AAAAMMDD-HHMMSS-pid.log na raiz do projeto (ou na pasta de dados do usuário, se exportado).</summary>
    private static void ConfigureLog()
    {
        var projectDir = ProjectSettings.GlobalizePath("res://");
        var dir = projectDir.Length > 0 && System.IO.Directory.Exists(projectDir)
            ? System.IO.Path.GetFullPath(System.IO.Path.Combine(projectDir, "..", "logs"))
            : System.IO.Path.Combine(OS.GetUserDataDir(), "logs");
        var debug = Array.IndexOf(OS.GetCmdlineUserArgs(), "--log-debug") >= 0;
        GameLog.Configure(dir, "cliente", perRun: true, debug ? LogLevel.Debug : LogLevel.Info);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => GameLog.Error("cliente", "exceção fatal", e.ExceptionObject as Exception);
        var info = Engine.GetVersionInfo();
        GameLog.Info("cliente", $"iniciado: Godot {info["string"]}, {OS.GetName()}, vídeo {RenderingServer.GetVideoAdapterName()}, " +
                                $"tela {DisplayServer.ScreenGetRefreshRate():0} Hz");
    }

    public override void _Process(double delta) => GameLog.Guard("cliente.quadro", () => ProcessFrame(delta));

    private ulong _lastNetMs;
    private bool _netSilentLogged;

    private void LogFps(double delta)
    {
        if (!_model.LoggedIn) return;
        // 11/11 (amigo: "travou na cidade"): toda travada de mais de 0,4 s vai para o log com onde estava e o que tinha na tela
        if (delta > 0.4)
        {
            var me = _model.Me;
            GameLog.Warn("cliente.travada", $"quadro levou {delta * 1000:0} ms; mapa {_mapView?.Map.Id ?? "-"}, " +
                (me is null ? "sem personagem" : $"posição ({me.X:0.0}; {me.Y:0.0})") +
                $", monstros {_monsters.Count}, jogadores {_players.Count}, itens {_items.Count}, nós {GetTree().GetNodeCount()}, " +
                $"vídeo {Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1048576:0} MB, memória {OS.GetStaticMemoryUsage() / 1048576} MB");
        }
        // servidor calado (rede caiu ou travou do outro lado)
        var nowMs = Time.GetTicksMsec();
        if (nowMs - _lastNetMs > 3000 && _lastNetMs > 0 && !_netSilentLogged)
        {
            GameLog.Warn("cliente.rede", $"servidor sem mandar nada há {(nowMs - _lastNetMs) / 1000.0:0.0} s");
            _netSilentLogged = true;
        }
        var fps = Engine.GetFramesPerSecond();
        _fpsSum += fps;
        _fpsFrames++;
        if (fps > 0 && fps < _fpsMin) _fpsMin = fps;
        _fpsTime += delta;
        if (_fpsTime < 60) return;
        GameLog.Info("cliente.saude", $"FPS médio {_fpsSum / Math.Max(1, _fpsFrames):0} mín {_fpsMin:0}; jogadores {_players.Count}, monstros {_monsters.Count}, " +
                                      $"itens {_items.Count}, projéteis {_projectiles.Count}, nós {GetTree().GetNodeCount()}; {Sfx.Report()}; " +
                                      // perfil: tempo de lógica (C#) x desenho por quadro — mostra onde o FPS está indo
                                      $"lógica {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0} ms, física {Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.0} ms, " +
                                      $"desenhos {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0}, objetos {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame):0}, " +
                                      $"triângulos {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame) / 1000:0}k, vídeo {Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1048576:0} MB");
        if (Perfil.Resumo() is { Length: > 0 } perfil) GameLog.Info("cliente.perfil", "ms por quadro: " + perfil);
        _fpsSum = 0; _fpsFrames = 0; _fpsMin = double.MaxValue; _fpsTime = 0;
    }

    private void ProcessFrame(double delta)
    {
        if (_vitrine) { RunAutomation(delta); return; }
        _ultimoDelta = delta;
        Perfil.Inicio();
        LogFps(delta);
        // painel aberto por mensagem do servidor (estação, armazém) depois de soltar o mouse: o aviso "mouse livre" sai da frente
        if (_pausePanel.Visible && (_panels.AnyOpen || _inventory.Visible)) _pausePanel.Visible = false;
        var dt = (float)Math.Min(delta, 0.1);
        if (_model.Map is not null && _dicas.Checar(_model, delta) is { } dica) { _hud?.ShowNotice(dica); Sfx.Play("ui_aviso", -6f); }   // 13/11: dicas únicas
        if (_hitStopUntil > 0 && Time.GetTicksMsec() >= _hitStopUntil) { Engine.TimeScale = 1.0; _hitStopUntil = 0; }
        Sfx.Sample();
        _time += delta;
        DrainNetwork();
        Perfil.Marca("rede");
        if (_statusLabel.Text != _statusText) { _statusLabel.Text = _statusText; _model.Registrar(_statusText); }
        RunAutomation(delta);

        if (!_model.LoggedIn)
        {
            if (_model.Lobby is { Ok: true } lobby && _client is not null) _lobby.Show(lobby);
            // 14/11: barrado pela versão — o atualizador baixa no lobby e o status mostra o andamento
            if (_updaterOn)
            {
                _updater.Process(delta);
                if (_updater.Status.Length > 0 && _updater.Status != _updaterShown) { _updaterShown = _updater.Status; _lobby.SetStatus(_updater.Status); }
                // 14/11 18h: baixou no lobby → reabre sozinho já atualizado (o Jogar.cmd aplica e entra de novo)
                if (_updater.Done && _updater.Baixados > 0 && !_reabrindo)
                {
                    _reabrindo = true;
                    if (_updater.Reabrir())
                    {
                        _lobby.SetStatus("Atualização baixada! O jogo vai fechar e abrir de novo já atualizado (uns segundos)...");
                        GetTree().CreateTimer(2.5).Timeout += () => GetTree().Quit();
                    }
                    else _lobby.SetStatus("Atualização baixada! Feche o jogo e abra de novo pelo Jogar.cmd para aplicar.");
                }
            }
            return;
        }

        EnsureWorld();
        AtualizarCarga();
        AvisoNoite.Tick(this, _carga is { Visible: true } || _mapMontando is not null || _mapView is null);   // 16/11: anúncio da noite depois da carga
        // 16/11: o que o personagem já andou (névoa do mapa M)
        if (_model.Map is { } mapaExp && _model.Me is { } euExp && Engine.GetProcessFrames() % 15 == 0)
            Explorado.Marcar(_model.CharacterId ?? "", mapaExp.Id, euExp.X, euExp.Y);
        // 13/11: o líquido dos orbes balança com o giro da câmera e com arrancadas/paradas do personagem
        {
            var dyaw = Mathf.Wrap(_cam.Yaw - _yawAnt, -Mathf.Pi, Mathf.Pi);
            _yawAnt = _cam.Yaw;
            var vel = _model.Me is { } eu ? new Vector2((float)eu.X, (float)eu.Y) : Vector2.Zero;
            var v = dt > 0 ? (vel - _posAnt).Length() / (float)dt : 0f;
            _posAnt = vel;
            var dv = Mathf.Clamp(v - _velAnt, -8f, 8f); _velAnt = v;
            OrbeHud.Sacudir(Mathf.Clamp(dyaw * 1.2f + (v > 12f ? 0f : dv * 0.02f), -0.3f, 0.3f));
        }
        // 13/11: foto do mundo (sem interface) para a tela de carregamento — 40 s depois de chegar no mapa e a cada 4 min,
        // só em mapa aberto, fora do editor e sem tela de carregamento na frente
        if (_mapView is { } mvf && _carga is not { Visible: true } && !_editor.Active && mvf.Map.HalfSize > 22 && mvf.Map.Kind != "dungeon")
        {
            _fotoEm -= dt;
            if (_fotoEm <= 0) { _fotoEm = 240; FotosDeCarga.Tirar(this, _cam.Camera); }
        }
        // 13/11 ("tira print de um ângulo maneiro da cidade, de cima, ou da biblioteca do Merlin"): nas cidades, 12 s depois
        // de chegar, cartões-postais aéreos — a praça e o prédio marcante (biblioteca, observatório, moinho, forja, astrolábio)
        if (_mapView is { } mvp && _carga is not { Visible: true } && !_editor.Active && mvp.Map.Kind == "cidade")
        {
            _postalEm -= dt;
            if (_postalEm <= 0)
            {
                _postalEm = 6;
                var mp = mvp.Map;
                var marcos = new[] { "predio_biblioteca", "astralia_observatorio", "mata_moinho", "forja", "dg_astrolabio", "acropole_estatua_juramento" };
                var pontos = new List<(string Nome, double X, double Y, float Dist, float Alt)> { ("centro", 0, 0, mp.HalfSize > 30 ? 26f : 9f, mp.HalfSize > 30 ? 20f : 6.5f) };
                foreach (var pr in mp.Props)
                    if (marcos.FirstOrDefault(k => pr.Kind.EndsWith(k, StringComparison.Ordinal)) is { } mk && pontos.All(q => q.Nome != mk))
                        pontos.Add((mk, pr.X, pr.Y, mp.HalfSize > 30 ? 20f : 7f, mp.HalfSize > 30 ? 13f : 5f));
                var falta = pontos.FirstOrDefault(q => !FotosDeCarga.TemPostal(mp.Id + "_" + q.Nome));
                if (falta.Nome is not null)
                {
                    // ângulo de 3/4 vindo do lado do centro do mapa (o prédio fica de frente)
                    var yaw = falta.Nome == "centro" ? 35f : Mathf.RadToDeg(Mathf.Atan2((float)-falta.X, (float)-falta.Y)) + 25f;
                    FotosDeCarga.Postal(this, _cam.Camera, mp.Id + "_" + falta.Nome, new Vector3((float)falta.X, 0, (float)falta.Y), yaw, falta.Dist, falta.Alt);
                }
                else _postalEm = 600;
            }
        }
        // sem mapa 6 s depois de entrar: servidor antigo (arena) — tira a tela e deixa montar o cenário de arena
        if (_carga is { Visible: true } && _model.Map is null && _cargaAte == ulong.MaxValue && _loginAt > 0 && Time.GetTicksMsec() - _loginAt > 6000) _carga.Visible = false;
        var now = (long)Time.GetTicksMsec();
        Perfil.Marca("mundo/carga");
        SyncWorld(dt, now);
        Perfil.Marca("sync");
        var me = MyState();
        if (me is not null)
        {
            var locked = LockedMonster();
            // 11/11: voando, a câmera sobe junto (o boneco fica no centro, não no alto da tela)
            _camLift = Mathf.MoveToward(_camLift, _model.Me?.Flying == true ? MannequinVisual.FlyHeight : 0f, (float)dt * 2.4f);
            var heroPos = LabOwnsHero ? new Vector3(_lab!.HeroPosition.X, 0, _lab.HeroPosition.Z) : me.Pos;
            _cam.Update(dt, heroPos + new Vector3(0, _camLift + me.Ground, 0), locked is null ? null : new Vector3((float)locked.X, 0, (float)locked.Y),
                GetWorld3D().DirectSpaceState);
            _mapView?.LuzDoHeroi(heroPos + new Vector3(0, me.Ground, 0));   // 15/11: luz fraca no herói à noite
            // 16/11: poste/parede entre a câmera e o herói fica translúcido
            if (_mapView is not null)
            {
                if (_desobstrucao is null || !ReferenceEquals(_desobstrucaoMapa, _mapView)) { _desobstrucao = new Desobstrucao(_mapView.Root); _desobstrucaoMapa = _mapView; }
                _desobstrucao.Atualizar(dt, _cam.Camera.GlobalPosition, heroPos + new Vector3(0, me.Ground, 0));
            }
            // câmera colada no herói (parede atrás): esconde o próprio boneco para não tapar a visão
            if (me.WasAlive) me.Visual.Root.Visible = !_cam.TooCloseToHero;
        }
        if (_official is not null) _official.ProcessFrame(delta);   // pausado quando o laboratório tem o controle
        else HandleContinuousInput(now);
        if (_demoAuraAt >= 0 && (float)_time >= _demoAuraAt && MyState() is { } auraSt && _model.Me is { } auraMe) { _demoAuraAt = -1f; LevelUpAura(auraSt, auraMe.Level, true); }
        if (_mapView is not null) { _mapView.PlayerPos = MyState()?.Pos; _mapView.NodeDepleted ??= id => _model.NodeSecondsLeft(id) > 0; _mapView.HasFlag ??= f => _model.Flags.Contains(f); }
        if (_mapView is not null && _model.Me is { } euNv) _mapView.NivelJogador = euNv.Level;
        Perfil.Marca("camera/entrada");
        _mapView?.Update(_model.Flags, (float)_time, dt, _model.MapDecor);
        if (_model.Me is { } viewer) _mapView?.SetViewer(viewer.X, viewer.Y);
        Perfil.Marca("mapa");
        // 11/11: quadrado de uso sob o mouse (mouse solto) ou sob a mira (câmera presa)
        if (_mapView is not null)
        {
            var vpm = GetViewport();
            var miraTela = Input.MouseMode == Input.MouseModeEnum.Captured ? vpm.GetVisibleRect().Size / 2f : vpm.GetMousePosition();
            _mapView.HoverAt(_editor.Active ? null : GroundUnderPointer(), _editor.Active ? null : _cam.Camera, miraTela);
            _model.AimedObjId = _mapView.HoveredObjId;
        }
        // 11/11 noite ("o contorno do NPC aparece e some conforme o ângulo"): o NPC é achado pela posição dele NA TELA
        if (_mapView is not null && !_editor.Active && _cam.Camera.IsInsideTree())
        {
            var vp = GetViewport();
            _mapView.HoverNpcOnScreen(_cam.Camera, Input.MouseMode == Input.MouseModeEnum.Captured ? vp.GetVisibleRect().Size / 2f : vp.GetMousePosition());
            // 13/11 ("voando, para falar com os NPCs fica bugado, como se não mirasse"): voando ou montado a mira fica longe do
            // NPC lá embaixo — vale o NPC mais perto, sem precisar mirar
            _model.RequireNpcAim = _model.Me is not { Flying: true } && _model.Me?.Mount is null;
            _model.AimedNpcId = _mapView.HoveredNpc;
        }
        if (_model.HasClock)
        {
            var hourNow = _model.HourNow;
            _mapView?.SetHour((float)hourNow);
            var night = hourNow < 6 || hourNow >= 18.5;
            // 16/11: das 19h às 5h (a noite do servidor) o relógio lembra o bônus: "☾ noite · +25% XP"
            var noiteServidor = hourNow >= 19 || hourNow < 5;
            HudView.NoiteXp = noiteServidor;
            if (_hud is not null) _hud.ClockText = $"{(int)hourNow:00}:{(int)(hourNow % 1 * 60):00} · " + (noiteServidor ? "☾ noite · +25% XP" : night ? "noite" : hourNow < 8 ? "amanhecer" : hourNow >= 17 ? "entardecer" : "dia");
        }
        Perfil.Marca("mira/hora");
        SavePinsIfChanged();
        LoadBindsIfNeeded();
        if (_model.BindsVersion != _bindsSaved) SaveBinds();   // poder aprendido foi sozinho para uma tecla
        _mapView?.SetMarkers(_model.NpcMarkers);
        _mapView?.UpdateVisibility(_model.NpcVisible, _model.ObjectVisible);
        _mapView?.SetRuneFlags(_model.Flags);
        _mapView?.SetAlavancas(_model.Alavancas, _model.Flags, (float)delta);   // 18/11
        _mapView?.UpdatePlots(_model.Plots, (float)Time.GetTicksMsec() / 1000f);
        if (_arena is not null) AnimateArenaFlames();
        Perfil.Marca("marcadores");
        _effects.Update(delta);
        UpdateWhirls(dt);
        Perfil.Marca("efeitos");
        UpdateHud(now, delta);
        Perfil.Marca("hud");
        UpdateWorldUi(delta);
        Perfil.Marca("janelas");
        _inventory.Update(_model);
        Perfil.Marca("mochila");
        Perfil.Fim();
    }

    /// <summary>E9: abre a conversa quando o servidor responde ao NPC; fecha ao se afastar; loja liga o "Vender".</summary>
    private void UpdateWorldUi(double delta)
    {
        if (_model.Dialog is { } dialog && !ReferenceEquals(dialog, _lastDialog))
        {
            if (_lastDialog is null || !_panels.DialogOpen) { _panels.ShowDialog(); CaptureMouse(false); }
            _lastDialog = dialog;
        }
        if (_model.Dialog is { } open && _model.Me is { } me && _model.Map?.Npcs.FirstOrDefault(n => n.Id == open.NpcId) is { } npc
            && ClientModel.Distance(me.X, me.Y, npc.X, npc.Y) > 6.5)
        {
            _model.Dialog = null;
        }
        if (_model.Dialog is null && _lastDialog is not null)
        {
            _lastDialog = null;
            _closingPanels = true; // conversa fechada (Esc, botão ou se afastou): volta a controlar a câmera
        }
        var sellTo = _model.Dialog is { Shop.Count: > 0 } shop ? shop.NpcId : null;
        // loja abriu: a mochila abre junto, ao lado, para vender arrastando ou com o botão direito
        if (sellTo is not null && _inventory.SellTo is null && !_inventory.Visible) { _inventory.Visible = true; Sfx.Play("ui_abrir"); }
        else if (sellTo is null && _inventory.SellTo is not null && _inventory.Visible) _inventory.Visible = false;
        _inventory.SellTo = sellTo;
        _inventory.NaForja = _model.Station?.Kind == "forja";
        _panels.Update(_model, delta);
        _troca.Update(_model);
        _chat.Update(_model, _time);
        _vinteUm.Update();
        _livro.Update();
        _bestiario.Update();
        _trilha.Atualizar(_model.Trilha, _model.TrilhaSeq, _model);
        _amigos.Atualizar(_model.Amigos, _model.AmigosSeq);
        _menuTab.Atualizar(_model.Objetivos);
        if (!_menuDemo && _model.LoggedIn && OS.GetEnvironment("CDM_MENU") is { Length: > 0 } aba) { _menuDemo = true; _menuTab.Mostrar(_model, aba); }   // verificação visual
        _god?.Update(_model);
        _editor.Update(_model, delta);
        // 14/11 21h (dono: "o mouse não aparece quando aperto TAB, tenho que abrir e fechar algumas vezes"): a marca
        // "_closingPanels" ficava ligada de um painel fechado antes e prendia o mouse logo depois de o TAB abrir
        if (_menuTab.Visible || _trilha.Visible || _pausePanel.Visible || GodOpen) _closingPanels = false;
        if (!_panels.AnyOpen && !_inventory.Visible && Input.MouseMode != Input.MouseModeEnum.Captured && _closingPanels)
        {
            _closingPanels = false;
            CaptureMouse(true);
        }

        // nomes dos NPCs (sem barra de vida)
        if (_model.Map is { } map)
            foreach (var n in map.Npcs)
            {
                var pos = new Vector3((float)n.X, 2.25f, (float)n.Y);
                _hud.UpdatePlate("n:" + n.Id, n.Name, 1.0, false, new Color(0.95f, 0.85f, 0.55f), pos, _cam.Camera, IsNear(pos, 22f) && _model.NpcVisible(n));
            }
    }

    private bool _closingPanels;
    private bool _vitrine;

    // ------------------------------------------------------------ automação (atalho / verificação)

    private void RunAutomation(double delta)
    {
        _sessionTime += delta; // conta desde que o cliente abriu
        foreach (var shot in _shots.Where(s => s.At <= _sessionTime).ToList())
        {
            _shots.Remove(shot);
            var image = GetViewport().GetTexture().GetImage();
            image.SavePng(shot.Path);
            GD.Print($"[captura] {shot.Path}");
        }
        if (_quitAfter > 0 && _sessionTime >= _quitAfter) GetTree().Quit();
        if (_demoInventory && _model.LoggedIn && _sessionTime > 1.0 && !_inventory.Visible) { _inventory.Visible = true; _demoInventory = false; }
        if (_demoPanel.Length > 0 && _model.LoggedIn && _model.Catalog is not null && _sessionTime > 2.0) { TogglePanel(_demoPanel); _demoPanel = ""; }
        if (_demoTalk.Length > 0 && _model.LoggedIn && _model.Catalog is not null && _sessionTime > 2.0) { Send(new InteractIntent("npc:" + _demoTalk)); _demoTalk = ""; }
        foreach (var ui in _demoUi.Where(u => u.At <= _sessionTime && _model.LoggedIn).ToList())
        {
            _demoUi.Remove(ui);
            switch (ui.What)
            {
                case "npc": Interact(); break;
                case "mapa": TogglePanel("map"); break;
                case "diario": TogglePanel("journal"); break;
                case "atributos": TogglePanel("stats"); break;
                case "grupo": TogglePanel("party"); break;
                case "mochila": _inventory.Visible = true; CaptureMouse(false); break;
                case "fechar": _panels.CloseAll(); _inventory.Visible = false; _model.Dialog = null; break;
                case "montar": Send(new MountIntent(true)); break;
                case "voar": Send(new FlyIntent(true)); break;
                case "pet": if (_model.Companions?.Pets is { Count: > 0 } pl) Send(new PetIntent("summon", pl[0])); break;
                case "oficio": Interact(); break;
                case "editar": { var me = _model.Me; _editor.Enter(me is null ? Vector3.Zero : new Vector3((float)me.X, 0, (float)me.Y), 0f); } break;
                case "aceitar": if (_model.PendingInvite is { } pi0) { Send(new PartyIntent(pi0.Kind == "duelo" ? "duelo_aceitar" : "accept")); _model.PendingInvite = null; } break;
                default:
                    if (ui.What.StartsWith("celula:", StringComparison.Ordinal)) _inventory.SelectCell(int.Parse(ui.What[7..]));
                    if (ui.What.StartsWith("convidar:", StringComparison.Ordinal)) Send(new PartyIntent("invite", ui.What[9..]));
                    if (ui.What.StartsWith("deus:", StringComparison.Ordinal) && !AmigoBuild) { _god?.Toggle(); CaptureMouse(false); _god?.Page(int.Parse(ui.What[5..])); }
                    if (ui.What.StartsWith("godcmd:", StringComparison.Ordinal))
                    {
                        var g = ui.What[7..].Split(':');
                        Send(new GodIntent(g[0], g.Length > 1 && g[1].Length > 0 ? g[1] : null, g.Length > 2 ? long.Parse(g[2], CultureInfo.InvariantCulture) : 0));
                    }
                    if (ui.What.StartsWith("tirar:", StringComparison.Ordinal)) Send(new UnequipIntent(ui.What[6..]));
                    if (ui.What.StartsWith("irpara:", StringComparison.Ordinal) && ui.What.Split(':') is { Length: 4 } ip)
                        Send(new GodIntent("travel", ip[1], X: double.Parse(ip[2], CultureInfo.InvariantCulture), Y: double.Parse(ip[3], CultureInfo.InvariantCulture)));
                    if (ui.What.StartsWith("equipar:", StringComparison.Ordinal) && _model.FirstInventoryItemWithPrefix(ui.What[8..]) is { } eq) Send(new EquipIntent(eq.ItemId));
                    if (ui.What.StartsWith("hora:", StringComparison.Ordinal)) Send(new GodIntent("time", Amount: (long)(double.Parse(ui.What[5..], CultureInfo.InvariantCulture) * 100)));
                    if (ui.What.StartsWith("andar:", StringComparison.Ordinal))
                    {
                        var dir = ui.What[6..].Split(',');
                        _demoWalk = (double.Parse(dir[0], CultureInfo.InvariantCulture), double.Parse(dir[1], CultureInfo.InvariantCulture));
                    }
                    break;
            }
        }
    }

    /// <summary>Demonstração automática (só com --demo): usa o mesmo caminho da entrada do jogador.</summary>
    private (double X, double Y) DemoInput(PlayerView me, long now)
    {
        var target = _model.Snapshot?.Monsters.Where(m => m.Alive)
            .OrderBy(m => ClientModel.Distance(me.X, me.Y, m.X, m.Y)).FirstOrDefault();
        if (target is null || ClientModel.Distance(me.X, me.Y, target.X, target.Y) > 30) return (0, 0);
        _lockedId = target.Id;
        var d = ClientModel.Distance(me.X, me.Y, target.X, target.Y);
        var reach = ClientModel.IsRanged(_model.ClassId) ? 9.0 : 2.0;
        if (_queued is null && d <= reach + 1.5)
        {
            var cycle = (long)(_sessionTime * 1000) % 4000;
            Queue(cycle < 300 ? "skill1" : "basic");
        }
        if (target.Action.StartsWith("windup") && target.ActionT > 0.75 && now >= _dodgeReadyAt && _demo == "esquiva")
        {
            Dodge();
        }
        if (d > reach) return AimMath.FacingTo(me.X, me.Y, target.X, target.Y) ?? (0, 0);
        return (0, 0);
    }

    private void TryAutoEnter(LobbyMessage lobby)
    {
        if (_autoCharacter is null && _autoCreate is null) return;
        var wanted = _autoCharacter ?? _autoCreate!.Split(':')[0];
        var match = wanted == "primeiro"
            ? lobby.Characters.FirstOrDefault()
            : lobby.Characters.FirstOrDefault(c => string.Equals(c.Name, wanted, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            _autoCharacter = null;
            _autoCreate = null;
            Send(new EnterWorldIntent(match.Id, CinzasDaMare.Shared.VersaoJogo.Atual));
            return;
        }
        if (_autoCreate is not null)
        {
            var parts = _autoCreate.Split(':');
            _autoCharacter = parts[0];
            Send(new CreateCharacterIntent(parts[0], parts.Length > 1 ? parts[1] : "guerreiro"));
            _autoCreate = null;
        }
    }

    // ------------------------------------------------------------ entrada

    public override void _UnhandledInput(InputEvent @event) => GameLog.Guard("cliente.entrada", () => HandleInput(@event));

    /// <summary>Alt+Enter ou F11 alternam tela cheia (vale em qualquer tela, mesmo com janela aberta).</summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode == Key.F11 || (k.Keycode == Key.Enter && k.AltPressed))
        {
            var mode = DisplayServer.WindowGetMode();
            var full = mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
            DisplayServer.WindowSetMode(full ? DisplayServer.WindowMode.Maximized : DisplayServer.WindowMode.Fullscreen);
            GetViewport().SetInputAsHandled();
        }
    }

    private void HandleInput(InputEvent @event)
    {
        if (!_model.LoggedIn) return;
        if (_editor.HandleInput(@event)) return;
        if (_lab?.HandleInput(@event) == true) return;   // laboratório ligado: ataque/esquiva vão para o protótipo
        if (_decorColocando is not null && DecorInput(@event)) return;   // 18/11: colocando decoração

        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _cam.Rotate(motion.Relative);
            return;
        }
        if (@event is InputEventMouseButton mouse && mouse.Pressed)
        {
            // travado no alvo: a rodinha troca de alvo (cima = para um lado, baixo = para o outro); destravado: zoom
            if (mouse.ButtonIndex == MouseButton.WheelUp) { if (_lockedId is not null && Input.MouseMode == Input.MouseModeEnum.Captured) CycleLock(1); else _cam.Zoom(1); return; }
            if (mouse.ButtonIndex == MouseButton.WheelDown) { if (_lockedId is not null && Input.MouseMode == Input.MouseModeEnum.Captured) CycleLock(-1); else _cam.Zoom(-1); return; }
            if (Input.MouseMode != Input.MouseModeEnum.Captured)
            {
                if (!_inventory.Visible && !_panels.AnyOpen && !GodOpen && !_troca.Open && !_vinteUm.Open && !_livro.Open && !_bestiario.Open && !_trilha.Visible && !_amigos.Visible && !_menuTab.Visible && !_chat.Typing
                    && !_pesquisa.Root.Visible && !_mesclar.Open && mouse.ButtonIndex == MouseButton.Left) CaptureMouse(true);   // 18/11: busca/mesclar abertas = mouse livre
                return;
            }
            if (mouse.ButtonIndex == MouseButton.Left) Queue("basic");
            // 10/11: botão direito usa o poder escolhido para ele no Livro de Poderes (K, tecla "Dir.")
            if (mouse.ButtonIndex == MouseButton.Right && _model.SkillForKey("Dir") is not null) Queue("key:Dir");
            if (mouse.ButtonIndex == MouseButton.Middle) ToggleLock();
            return;
        }
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
        // 13/11: trocando tecla no menu de opções — a próxima tecla apertada vai para a ação escolhida
        if (_teclaEsperando is { } esperando)
        {
            if (key.Keycode != Key.Escape) Teclas.Trocar(esperando, key.Keycode);
            _teclaEsperando = null;
            MontarTeclas();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.F && key.CtrlPressed && EmCasaPropria())   // 18/11 15h: modo editar a decoração
        {
            _editandoCasa = !_editandoCasa;
            _hud.ShowNotice(_editandoCasa ? "Modo EDITAR a casa: chegue perto de uma peça e aperte F para mover. Ctrl+F de novo sai." : "Modo editar desligado.");
            Sfx.Play("ui_clique");
            GetViewport().SetInputAsHandled();
            return;
        }
        switch (Teclas.Traduz(key.Keycode))   // 13/11: teclas trocadas pelo jogador viram as teclas-padrão aqui
        {
            case Key.O:
                AbrirHistorico(!_histPanel.Visible);
                break;
            case Key.Z:
                _posesPanel.Visible = !_posesPanel.Visible;
                CaptureMouse(!_posesPanel.Visible && !_inventory.Visible && !_panels.AnyOpen && !GodOpen);
                break;
            case Key.X:
                // 12/11: habilidade do item vestido (Máscara do Pandemônio → Furacão), qualquer classe
                Send(new CastIntent("skill_mascara_furacao", null));
                break;
            case Key.L:
                _bestiario.Toggle();
                if (_bestiario.Open) { Send(new BestiaryIntent()); Sfx.Play("ui_abrir"); }
                else Sfx.Play("ui_fechar");
                CaptureMouse(!_bestiario.Open && !_inventory.Visible && !_panels.AnyOpen && !GodOpen);
                break;
            case Key.Enter or Key.KpEnter when !key.AltPressed:
                _chat.OpenInput();
                CaptureMouse(false);
                break;
            case Key.F3:
                // 13/11: Trilha do Herói (o que abre em cada nível)
                if (_menuTab.Visible) _menuTab.Visible = false;
                _trilha.Visible = !_trilha.Visible;
                if (_trilha.Visible) { Send(new TrilhaIntent()); Sfx.Play("ui_abrir"); } else Sfx.Play("ui_fechar");
                CaptureMouse(!_trilha.Visible && !_inventory.Visible && !_panels.AnyOpen && !GodOpen);
                break;
            case Key.F4:
                // 18/11: lista de amigos
                _amigos.Visible = !_amigos.Visible;
                if (_amigos.Visible) { Send(new AmigosIntent("listar")); Sfx.Play("ui_abrir"); } else Sfx.Play("ui_fechar");
                CaptureMouse(!_amigos.Visible && !_trilha.Visible && !_inventory.Visible && !_panels.AnyOpen && !GodOpen);
                break;
            case Key.F2:
                // 13/11 ("F2 entra e sai do modo edição direto"): fora do editor, F2 entra; dentro, o próprio editor sai
                if (_god is null || AmigoBuild || _editor.Active) break;
                { _god.Hide(); var me2 = _model.Me; _editor.Enter(me2 is null ? Vector3.Zero : new Vector3((float)me2.X, 0, (float)me2.Y), Mathf.RadToDeg(_cam.Yaw)); }
                break;
            case Key.F1:
                if (_god is null || AmigoBuild) break;
                _god.Toggle();
                CaptureMouse(!GodOpen && !_inventory.Visible && !_panels.AnyOpen);
                break;
            case Key.Escape:
                if (_histPanel.Visible) { AbrirHistorico(false); break; }
                if (_posesPanel.Visible) { _posesPanel.Visible = false; CaptureMouse(true); break; }
                if (_teclasPanel.Visible) { _teclasPanel.Visible = false; break; }
                if (_livro.Open) { _livro.Close(); break; }
                if (_mesclar.Open) { _mesclar.Close(); CaptureMouse(!_inventory.Visible && !_panels.AnyOpen); break; }
                if (_hud.GanhouAberto) { _hud.FecharGanhouPorFora(); break; }   // 13/11: ESC fecha a caixa "Parabéns"
                if (_bestiario.Open) { _bestiario.Hide(); CaptureMouse(!_inventory.Visible && !_panels.AnyOpen && !GodOpen); break; }
                if (_trilha.Visible) { _trilha.Visible = false; CaptureMouse(!_inventory.Visible && !_panels.AnyOpen && !GodOpen); break; }
                if (_amigos.Visible) { _amigos.Visible = false; CaptureMouse(!_inventory.Visible && !_panels.AnyOpen && !GodOpen); break; }
                if (_menuTab.Visible) { _menuTab.Visible = false; CaptureMouse(!_inventory.Visible && !_panels.AnyOpen && !GodOpen); break; }
                if (_vinteUm.Open) { _vinteUm.Leave(); break; }
                if (_troca.Open) { Send(new TradeIntent("cancelar")); break; }
                if (_inventory.Visible || _panels.AnyOpen || _model.Dialog is not null || GodOpen)
                {
                    _god?.Hide();
                    _inventory.Visible = false;
                    _panels.CloseAll();
                    _model.Dialog = null;
                    _model.Station = null;
                    _model.Storage = null;
                    CaptureMouse(true);
                }
                else { _pausePedido = Input.MouseMode == Input.MouseModeEnum.Captured; CaptureMouse(Input.MouseMode != Input.MouseModeEnum.Captured); }
                break;
            case Key.I:
                _inventory.Visible = !_inventory.Visible;
                Sfx.Play(_inventory.Visible ? "ui_abrir" : "ui_fechar");
                CaptureMouse(!_inventory.Visible && !_panels.AnyOpen);
                break;
            case Key.J:
                TogglePanel("journal");
                break;
            case Key.C:
                TogglePanel("stats");
                break;
            case Key.M:
                TogglePanel("map");
                break;
            case Key.P:
                TogglePanel("party");
                break;
            case Key.U:
            {
                // 11/11: pede troca com o jogador mais perto (até 8 m)
                var me = _model.Me;
                var near = me is null ? null : _model.Snapshot?.Players.Where(p => p.CharacterId != me.CharacterId && p.Alive)
                    .OrderBy(p => (p.X - me.X) * (p.X - me.X) + (p.Y - me.Y) * (p.Y - me.Y)).FirstOrDefault();
                if (near is null || me is null || Math.Sqrt((near.X - me.X) * (near.X - me.X) + (near.Y - me.Y) * (near.Y - me.Y)) > 8) _hud.ShowNotice("Troca: chegue perto de outro jogador (até 8 m) e aperte U.");
                else Send(new TradeIntent("pedir", Target: near.CharacterId));
                break;
            }
            case Key.Y:
                if (_troca.Asking) _troca.Accept();
                else if (_model.PendingInvite is { } pi1) { Send(new PartyIntent(pi1.Kind == "duelo" ? "duelo_aceitar" : "accept")); _model.PendingInvite = null; }
                break;
            case Key.N:
                if (_troca.Asking) _troca.Refuse();
                else if (_model.PendingInvite is { } pi2) { Send(new PartyIntent(pi2.Kind == "duelo" ? "duelo_recusar" : "decline")); _model.PendingInvite = null; }
                else _hud.ToggleMinimap();   // 11/11: minimapa
                break;
            case Key.Q or Key.E or Key.R or Key.T:
            {
                var bindKey = key.Keycode.ToString();
                if (_model.SkillForKey(bindKey) is not null) Queue("key:" + bindKey);
                else _hud?.ShowNotice("Nenhum poder nesta tecla. Aprenda com um tomo (mentor no Porto) e escolha a tecla no Livro de Poderes (K).");
                break;
            }
            case Key.K:
                TogglePanel("poderes");
                break;
            case Key.Space:
                Dodge();
                break;
            case Key.Quoteleft:   // 14/11: trocar alvo saiu do TAB
                CycleLock();
                break;
            case Key.Tab:
                // 14/11 ("TAB como menu, igual Skyrim/Diablo"): objetivos, loja de cristais e atalhos
                if (_menuTab.Visible) { _menuTab.Visible = false; Sfx.Play("ui_fechar"); }
                else { _menuTab.Mostrar(_model); Sfx.Play("ui_abrir"); }
                CaptureMouse(!_menuTab.Visible && !_inventory.Visible && !_panels.AnyOpen && !GodOpen);
                break;
            case Key.F:
                Interact();
                break;
            case Key.H:
                _hud.ToggleHints();
                break;
            case Key.F10:
            {
                var level = GraphicsQuality.Cycle();
                GraphicsQuality.ApplyViewport(GetViewport());
                _mapView?.ApplyQuality();
                _hud.ShowNotice(GraphicsQuality.Modern
                    ? $"Gráficos: {GraphicsQuality.Label(level)}"
                    : $"Gráficos: {GraphicsQuality.Label(level)} (seu vídeo está no modo de compatibilidade: efeitos de luz desligados)");
                break;
            }
            case Key.V:
                // montar/desmontar (o servidor confere: só fora de combate, não em dungeon/casa)
                Send(new MountIntent(_model.Me?.Mount is null));
                break;
            case Key.G:
            {
                // 11/11: G = voar / pousar (asas). Shift+G = o impulso curto de antes.
                if (Input.IsKeyPressed(Key.Shift))
                {
                    var dir = _moveDir.X != 0 || _moveDir.Y != 0 ? _moveDir : CameraForward();
                    Send(new WingDashIntent(dir.X, dir.Y));
                }
                else Send(new FlyIntent(_model.Me?.Flying != true));
                break;
            }
            case Key.B:
                // companheiro: chama o primeiro (ou o próximo); com todos chamados, dispensa
                var pets = _model.Companions?.Pets ?? Array.Empty<string>();
                if (pets.Count == 0) { _hud.ShowNotice("Você ainda não tem companheiro (a Lia, no Acampamento das Dunas, conhece um vagalume perdido)."); break; }
                var active = _model.Companions?.ActivePet;
                var idx = active is null ? 0 : pets.ToList().IndexOf(active) + 1;
                Send(idx >= pets.Count ? new PetIntent("dismiss") : new PetIntent("summon", pets[idx]));
                break;
            case Key.Key1:
                // 13/11: a poção escolhida (arrastada para o quadro 1) vem primeiro; sem ela, a de sempre
                if (Pocao1() is { } esc) UseFirst(esc, "consumable_c01", "consumable_c02", "consumable_c05");
                else UseFirst("consumable_c01", "consumable_c02", "consumable_c05");
                break;
            case Key.Key2:
                if (Tecla2() is { } t2 && _model.CountInInventory(t2) > 0) UseFirst(t2);
                else if (_model.ClassId is "mago" or "bruxa") UseFirst("consumable_c03", "consumable_c04");
                else if (_model.ClassId == "arqueiro" && (_model.FirstInventoryItemWithPrefix("consumable_c08") is not null || _model.FirstInventoryItemWithPrefix("consumable_c09") is not null))
                    UseFirst("consumable_c08", "consumable_c09");
                else UseFirst("food_");
                break;
        }
    }

    /// <summary>Ponto do chão (y = 0) sob o mouse; com o mouse preso, sob a mira do centro da tela.</summary>
    private Vector3? GroundUnderPointer()
    {
        var cam = _cam.Camera;
        if (!cam.IsInsideTree()) return null;
        var vp = GetViewport();
        var at = Input.MouseMode == Input.MouseModeEnum.Captured ? vp.GetVisibleRect().Size / 2f : vp.GetMousePosition();
        var from = cam.ProjectRayOrigin(at);
        var dir = cam.ProjectRayNormal(at);
        if (dir.Y > -1e-3f) return null;
        var t = -from.Y / dir.Y;
        return t is > 0 and < 60 ? from + dir * t : null;
    }

    private static StandardMaterial3D? _ouro;

    /// <summary>13/11: banho de ouro brilhante por cima do bicho (Goblin de Ouro e os outros dourados raros).</summary>
    private static void Dourar(Node n)
    {
        _ouro ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.78f, 0.22f, 0.62f), Metallic = 1f, Roughness = 0.25f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, EmissionEnabled = true, Emission = new Color(0.55f, 0.38f, 0.05f),
            EmissionEnergyMultiplier = 0.6f,
        };
        foreach (var c in n.GetChildren())
        {
            if (c is GeometryInstance3D g && g.MaterialOverlay is null) g.MaterialOverlay = _ouro;
            Dourar(c);
        }
    }

    private static void SetShadows(Node n, bool on)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is GeometryInstance3D g)
            {
                if (!on && g.CastShadow == GeometryInstance3D.ShadowCastingSetting.On) { g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; g.SetMeta("sombra_off", true); }
                else if (on && g.HasMeta("sombra_off")) { g.CastShadow = GeometryInstance3D.ShadowCastingSetting.On; g.RemoveMeta("sombra_off"); }
            }
            SetShadows(c, on);
        }
    }

    private void CaptureMouse(bool capture)
    {
        if (capture && _hud.GanhouAberto) capture = false;   // 13/11: caixa "Parabéns" aberta: o mouse fica na tela
        Input.MouseMode = capture ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        // 13/11 ("abrindo os livros da biblioteca abre junto o menu do ESC"): com livro, 21, troca ou bestiário abertos o menu não aparece
        // 13/11 ("entrei e o ESC está ativado na frente"): o menu só aparece quando você aperta ESC (não toda vez que o mouse solta)
        if (capture) _pausePedido = false;
        _pausePanel.Visible = _pausePedido && !capture && !_inventory.Visible && !_panels.AnyOpen && !GodOpen && _model.LoggedIn
                              && !_livro.Open && !_vinteUm.Open && !_troca.Open && !_bestiario.Open && !_trilha.Visible && !_amigos.Visible && !_menuTab.Visible;
    }

    private string? _bindsFor;

    /// <summary>Teclas do Livro de Poderes, por personagem (user://poderes.cfg — só conveniência; o servidor confere tudo).</summary>
    private void LoadBindsIfNeeded()
    {
        if (_model.CharacterId is not { } id || id == _bindsFor) return;
        _bindsFor = id;
        _model.Binds.Clear();
        var cfg = new ConfigFile();
        if (cfg.Load("user://poderes.cfg") != Error.Ok) return;
        foreach (var k in new[] { "Q", "E", "R", "T", "Dir" })
            if (cfg.HasSectionKey(id, k)) _model.Binds[k] = (string)cfg.GetValue(id, k);
        _model.UnpinnedQuests.Clear();
        if (cfg.HasSectionKey(id, "desmarcadas"))
            foreach (var q in ((string)cfg.GetValue(id, "desmarcadas")).Split(',', StringSplitOptions.RemoveEmptyEntries)) _model.UnpinnedQuests.Add(q);
        _pinsSaved = _model.PinsVersion;
    }

    private int _pinsSaved;

    /// <summary>Missões fixadas (diário J) também ficam guardadas por personagem.</summary>
    private void SavePinsIfChanged()
    {
        if (_model.PinsVersion == _pinsSaved || _model.CharacterId is not { } id) return;
        _pinsSaved = _model.PinsVersion;
        var cfg = new ConfigFile();
        cfg.Load("user://poderes.cfg");
        cfg.SetValue(id, "desmarcadas", string.Join(",", _model.UnpinnedQuests));
        cfg.Save("user://poderes.cfg");
    }

    private int _bindsSaved;

    private void SaveBinds()
    {
        _bindsSaved = _model.BindsVersion;
        if (_model.CharacterId is not { } id) return;
        var cfg = new ConfigFile();
        cfg.Load("user://poderes.cfg");
        foreach (var (k, v) in _model.Binds) cfg.SetValue(id, k, v);
        cfg.Save("user://poderes.cfg");
    }

    private void TogglePanel(string which)
    {
        if (_menuTab.Visible) _menuTab.Visible = false;   // devolve o que o TAB pegou emprestado (ex.: o mapa)
        Sfx.Play(_panels.IsOpen(which) ? "ui_fechar" : "ui_abrir");
        _model.Dialog = null;
        _panels.Toggle(which);
        CaptureMouse(!_panels.AnyOpen && !_inventory.Visible);
    }

    private bool _sprinting;
    private bool _shiftAntes;
    private bool _runLatched, _runBlocked;

    private void Queue(string slot)
    {
        _queued = slot;
        _queuedAt = (long)Time.GetTicksMsec();
    }

    private (double X, double Y) CameraForward() => CameraMove.Forward(_cam.Yaw);

    private void HandleContinuousInput(long now)
    {
        var me = _model.Me;
        if (me is null) return;
        var typing = GetViewport().GuiGetFocusOwner() is LineEdit;
        var keys = !typing && !_inventory.Visible && !_panels.AnyOpen && !_editor.Active && !_vinteUm.Open && !_livro.Open && !_mesclar.Open;
        _mesclar.Update();
        // 18/11 ("entrei no Bosque das Corujas, deu lag, atravessei o portão e não tinha carregado"): enquanto a tela de
        // carga está de pé (ou o mapa ainda monta), o boneco não anda — antes a tecla segurada seguia andando às cegas
        if (_carga is { Visible: true } || _mapMontando is not null) keys = false;

        _moveDir = keys
            ? CameraMove.Direction(Teclas.Pressionada(Key.W) || Input.IsKeyPressed(Key.Up), Teclas.Pressionada(Key.S) || Input.IsKeyPressed(Key.Down),
                Teclas.Pressionada(Key.A) || Input.IsKeyPressed(Key.Left), Teclas.Pressionada(Key.D) || Input.IsKeyPressed(Key.Right), _cam.Yaw)
            : (0, 0);
        if (_demo is not null && me.Alive) _moveDir = DemoInput(me, now);
        if (_demoWalk is { } walk) _moveDir = walk;
        var refresh = (_moveDir.X != 0 || _moveDir.Y != 0) && now - _lastMoveSentMs > MoveRefreshMs;
        // Shift liga a corrida e ela fica ligada enquanto o personagem continuar andando (não precisa segurar);
        // parou de andar, volta a caminhar. Fora de combate a corrida rende mais (o servidor decide).
        var moving = _moveDir.X != 0 || _moveDir.Y != 0;
        var shiftDown = keys && Input.IsKeyPressed(Key.Shift);
        // 18/11 (dono: "correndo, se apertar Shift de novo, volta a andar; apertando de novo, corre — pra controlar a
        // estamina sem parar o boneco"): o Shift ALTERNA (cada toque troca correr/andar enquanto anda)
        var shiftToque = shiftDown && !_shiftAntes;
        _shiftAntes = shiftDown;
        if (!shiftDown) _runBlocked = false;
        if (shiftToque && moving) { if (_runLatched) { _runLatched = false; _runBlocked = true; } else if (!_runBlocked || me.Stamina > 0.5) { _runLatched = true; _runBlocked = false; } }
        // 18/11 19h (dono: "se ficar com o Shift apertado não corre — Shift apertado corre; soltou, continua correndo; tocou
        // no Shift de novo ou parou de andar, para"): Shift SEGURADO também liga a corrida (antes só valia o toque no
        // instante em que já estava andando — quem apertava Shift antes do W nunca corria)
        else if (shiftDown && moving && !_runLatched && !_runBlocked && me.Stamina > 0.5) _runLatched = true;
        if (!moving) _runLatched = false;
        // 11/11: fôlego acabou → volta a andar; só corre de novo apertando Shift outra vez
        if (_runLatched && me.Stamina <= 0.5) { _runLatched = false; _runBlocked = true; }
        var sprint = _runLatched || _demo is not null || _demoWalk is not null;
        var sprintChanged = sprint != _sprinting;
        if (_moveSender.ShouldSend(_moveDir.X, _moveDir.Y, now) || refresh || sprintChanged)
        {
            _sprinting = sprint;
            Send(new MoveIntent(_moveDir.X, _moveDir.Y, sprint, Walk: !sprint));
            _lastMoveSentMs = now;
        }

        // segurar o botão: ataques em sequência (o servidor dita o ritmo)
        if (keys && _decorColocando is null && Input.MouseMode == Input.MouseModeEnum.Captured && Input.IsMouseButtonPressed(MouseButton.Left) && _queued is null)
            Queue("basic");

        // orientação: alvo travado > mirando/atacando (frente da câmera) > direção do movimento
        var locked = LockedMonster();
        if (locked is null && _lockedId is not null) RelockAfterDeath(me);
        (double X, double Y)? face = null;
        if (locked is not null) face = AimMath.FacingTo(me.X, me.Y, locked.X, locked.Y);
        else if (now < _aimUntil || Input.IsMouseButtonPressed(MouseButton.Right)) face = CameraForward();
        else if (_moveDir.X != 0 || _moveDir.Y != 0) face = _moveDir;
        if (face is { } f)
        {
            _desiredFace = f;
            if (_faceThrottle.ShouldSend(f.X, f.Y, now)) Send(new FaceIntent(f.X, f.Y));
        }

        // fila de ações (buffer curto): sai assim que o personagem estiver livre
        if (_queued is not null)
        {
            if (now - _queuedAt > InputBufferMs || !me.Alive) _queued = null;
            else if (now - _actionSentAt > 110 && IsFree(me)) SendQueued(me, now);
        }

        // marca de mira do Mago (Explosão de Cinzas)
        // marca de mira: o primeiro poder de área (no chão) que estiver numa tecla
        var ground = new[] { "Q", "E", "R", "T", "Dir" }.Select(_model.SkillForKey).FirstOrDefault(sk => sk is { TargetMode: "ground" });
        if (ground is not null && me.Alive)
        {
            var aim = AimPoint(me, ground.Range);
            _aimMarker.Visible = true;
            _aimMarker.Position = new Vector3((float)aim.X, 0.06f, (float)aim.Y);
            var r = (float)_model.GroundRadius(ground) / 2.8f;
            _aimMarker.Scale = new Vector3(r, 1, r);
            var ready = _cooldowns.RemainingMs(ground.Id, now) == 0 && me.Resource >= ground.ResourceCost;
            ((StandardMaterial3D)_aimMarker.MaterialOverride).AlbedoColor = ready ? new Color(0.5f, 0.75f, 1f, 0.55f) : new Color(0.5f, 0.5f, 0.6f, 0.18f);
        }
        else _aimMarker.Visible = false;
    }

    private static bool IsFree(PlayerView me)
        => me.Action.Length == 0 || (me.Action.StartsWith("recover", StringComparison.Ordinal) && me.ActionT > 0.92)
           || me.Action == "dodge";   // 18/11: o servidor guarda o golpe e solta no fim da esquiva (sem esperar a rede)

    private (double X, double Y) AimPoint(PlayerView me, double range)
    {
        var ground = _cam.GroundUnderCrosshair(GetViewport());
        var (fx, fy) = CameraForward();
        double ax = me.X + fx * range * 0.6, ay = me.Y + fy * range * 0.6;
        if (ground is { } g) { ax = g.X; ay = g.Z; }
        var dx = ax - me.X;
        var dy = ay - me.Y;
        var d = Math.Sqrt(dx * dx + dy * dy);
        if (d > range) { ax = me.X + dx / d * range; ay = me.Y + dy / d * range; }
        return (ax, ay);
    }

    private void SendQueued(PlayerView me, long now)
    {
        var slot = _queued!;
        _queued = null;
        var skill = slot switch
        {
            "basic" => _model.BasicSkill,
            "weapon" => _model.WeaponSkill,
            _ when slot.StartsWith("key:", StringComparison.Ordinal) => _model.SkillForKey(slot[4..]),
            _ => _model.SkillFor(slot),
        };
        var skillId = skill?.Id ?? slot switch
        {
            "skill1" => ClientModel.SkillGolpe,
            "skill2" => ClientModel.SkillGuarda,
            _ => null,
        };
        if (skillId is null) return;

        var locked = LockedMonster();
        var face = locked is not null ? AimMath.FacingTo(me.X, me.Y, locked.X, locked.Y) ?? CameraForward() : CameraForward();
        // sem alvo travado, golpe de alvo único usa o monstro mais alinhado à frente da câmera
        var target = locked ?? (skill?.TargetMode == "single" ? _model.BestMonsterInDirection(face.X, face.Y, (skill?.Range ?? 2.5) + 2.0, 60) : null);
        if (target is not null && locked is null) face = AimMath.FacingTo(me.X, me.Y, target.X, target.Y) ?? face;
        _desiredFace = face;
        _faceThrottle.Reset();
        Send(new FaceIntent(face.X, face.Y));
        _faceThrottle.ShouldSend(face.X, face.Y, now);
        if (skill?.TargetMode == "ground")
        {
            var aim = AimPoint(me, skill.Range);
            Send(new CastIntent(skillId, null, aim.X, aim.Y));
        }
        else
        {
            Send(new CastIntent(skillId, target?.Id));
        }
        _actionSentAt = now;
        _aimUntil = now + 650;
    }

    private void Dodge()
    {
        var me = _model.Me;
        if (me is null || !me.Alive) return;
        var now = (long)Time.GetTicksMsec();
        Send(new DodgeIntent(_moveDir.X, _moveDir.Y));
        // 18/11 15h: evoluído (classe 2+) tem 2 esquivas seguidas; as duas voltam juntas depois da recarga
        var max = _model.ClassStage >= 2 ? 2 : 1;
        if (now >= _esqVoltaAt) _esqUsadas = 0;
        if (now < _dodgeReadyAt || _esqUsadas >= max) return;
        if (MyState() is { } st) Sfx.PlayAt("esquiva", st.Pos + Vector3.Up, -2f);
        _esqUsadas++;
        _esqVoltaAt = now + 850;
        _dodgeReadyAt = _esqUsadas < max ? now + 420 : now + 850;
    }

    /// <summary>O alvo travado caiu: trava sozinho no inimigo vivo mais perto (até 10 m); senão solta.</summary>
    private void RelockAfterDeath(PlayerView me)
    {
        _lockedId = _model.Snapshot?.Monsters.Where(m => m.Alive && ClientModel.Distance(me.X, me.Y, m.X, m.Y) < 10)
            .OrderBy(m => ClientModel.Distance(me.X, me.Y, m.X, m.Y)).FirstOrDefault()?.Id;
    }

    private void ToggleLock()
    {
        if (_lockedId is not null) { _lockedId = null; return; }
        var (fx, fy) = CameraForward();
        _lockedId = _model.BestMonsterInDirection(fx, fy, 22, 50)?.Id;
        if (_lockedId is not null) Sfx.Play("ui_clique", -8f);
    }

    /// <summary>
    /// Tab: trava no alvo à frente; já travado, pula para o próximo. Rodinha (side ±1): só os que estão daquele lado do
    /// alvo atual; sem outro, continua no mesmo. Tab sem outro alvo destrava.
    /// </summary>
    private void CycleLock(int side = 0)
    {
        if (_lockedId is null) { ToggleLock(); return; }
        if (_model.Me is not { } me || _model.Snapshot is null) return;
        var (fx, fy) = CameraForward();
        var cur = _model.Snapshot.Monsters.FirstOrDefault(m => m.Id == _lockedId);
        var all = _model.Snapshot.Monsters
            .Where(m => m.Alive && m.Id != _lockedId && ClientModel.Distance(me.X, me.Y, m.X, m.Y) < 22).ToList();
        if (side != 0 && cur is not null)
        {
            var (cx, cy) = (cur.X - me.X, cur.Y - me.Y);
            var sided = all.Where(m => Math.Sign(cx * (m.Y - me.Y) - cy * (m.X - me.X)) == -side).ToList();
            if (sided.Count > 0) all = sided;
        }
        var candidates = all
            .Select(m =>
            {
                var dx = m.X - me.X; var dy = m.Y - me.Y; var d = Math.Sqrt(dx * dx + dy * dy);
                var ang = Math.Acos(Math.Clamp((dx * fx + dy * fy) / Math.Max(1e-6, d), -1, 1)) * 180 / Math.PI;
                return (m, score: d + ang * 0.12);
            })
            .Where(x => x.score < 40)
            .OrderBy(x => x.score).ToList();
        if (candidates.Count == 0 && side != 0) return;
        _lockedId = candidates.Count > 0 ? candidates[0].m.Id : null;
        if (_lockedId is not null) Sfx.Play("ui_clique", -8f);
    }

    private MonsterView? LockedMonster()
    {
        if (_lockedId is null || _model.Snapshot is null || _model.Me is not { } me) return null;
        var m = _model.Snapshot.Monsters.FirstOrDefault(x => x.Id == _lockedId);
        if (m is null || !m.Alive || ClientModel.Distance(me.X, me.Y, m.X, m.Y) > 28) return null;
        return m;
    }

    /// <summary>
    /// Voz de NPC (pedido 10/11): resmungos sem palavras com o timbre de cada um (altura própria pelo id; mulheres usam o
    /// conjunto feminino), escolhidos pelo jeito do NPC e pelo momento (olá, contente, concorda, bravo, dúvida).
    /// </summary>
    private void NpcSay(string npcId, string mood, string gesture = "", float gestureLen = 0f)
    {
        var npc = _model.Map?.Npcs.FirstOrDefault(n => n.Id == npcId);
        if (npc is null) return;
        var at = (gesture.Length > 0 ? _mapView?.NpcReact(npcId, gesture, gestureLen) : null) ?? new Vector3((float)npc.X, 0, (float)npc.Y);
        Sfx.Voice(NpcVoiceKind(npc, mood), NpcFemale(npc), NpcPitch(npc), at);
    }

    private static readonly HashSet<string> FemaleLooks = new() { "curandeira", "capita", "arcanista", "bruxa", "batedora", "agricultora" };

    private static bool NpcFemale(CinzasDaMare.Shared.World.MapNpc n)
    {
        if (FemaleLooks.Contains(n.Look)) return true;
        foreach (var w in n.Name.Split(' ', ',').Take(2))
            if (w.Length > 1 && (w.EndsWith('a') || w.EndsWith('ã')) && w is not "Velha" and not "da" and not "Guarda") return true;
        return false;
    }

    private static float NpcPitch(CinzasDaMare.Shared.World.MapNpc n)
    {
        var h = 0u;
        foreach (var c in n.Id) h = h * 31 + c;
        var p = 0.86f + (h % 1000) / 1000f * 0.32f;            // 0,86–1,18: cada um com sua voz
        if (n.Look == "bobo") p *= 1.18f;
        if (n.Look is "guarda" or "ferreiro") p *= 0.88f;
        if (n.Look == "mentor" || n.Name.Contains("Mestre") || n.Name.Contains("Velh")) p *= 0.85f;
        return p;
    }

    private static string NpcVoiceKind(CinzasDaMare.Shared.World.MapNpc n, string mood)
    {
        var grump = n.Look is "guarda" or "ferreiro";
        var jolly = n.Look == "bobo" || n.Name.Contains("Pescador");
        var pick = Random.Shared.NextDouble();
        return mood switch
        {
            "ola" => jolly ? (pick < 0.5 ? "haha" : "ola") : grump ? (pick < 0.6 ? "hum" : "aham") : pick < 0.6 ? "ola" : pick < 0.8 ? "oh" : "hum",
            "feliz" => jolly || pick < 0.5 ? "haha" : "risada",
            "concorda" => pick < 0.5 ? "aham" : "oh",
            "bravo" => grump || pick < 0.6 ? "bravo" : "ai",
            "duvida" => pick < 0.6 ? "hein" : "hum",
            _ => "hum",
        };
    }

    private void Interact()
    {
        if (DecorPerto() is { } dp) { ComecarDecor(dp.Id); return; }   // 18/11: F perto de uma peça da casa = mover
        var target = _model.NearestInteractable(_model.Map is null ? 3.0 : 2.6);
        if (target is null) return;
        // 11/11 noite: F na pedra de viagem abre o mapa (M) com a viagem para as pedras que você já descobriu
        // 14/11 19h (dono: "F na pedra pela primeira vez abre o mapa"): a pedra ainda não ativada vai para o servidor (ativa)
        if (target.Value.Kind == "waypoint" && target.Value.Label.StartsWith("Ativar", StringComparison.Ordinal))
        {
            Send(new InteractIntent(target.Value.Id));
            return;
        }
        if (target.Value.Kind == "waypoint")
        {
            if (!_panels.IsOpen("map")) _panels.Toggle("map");
            CaptureMouse(false);
            return;
        }
        if (target.Value.Kind == "item")
        {
            _lastClaimLabel = target.Value.Label.StartsWith("Pegar ", StringComparison.Ordinal) ? target.Value.Label[6..] : target.Value.Label;
            Send(new ClaimItemIntent(Guid.NewGuid().ToString("N"), target.Value.Id));
        }
        else Send(new InteractIntent(target.Value.Id));
    }

    private bool _lastUseMana;
    private string? _lastClaimLabel;
    private float _camLift;
    private readonly Atualizador _updater = new();
    private bool _reabrindo;
    private bool _updaterOn;
    private string _updaterShown = "";
    private double _updaterNoticeIn;

    private HashSet<string>? _parabenizados;
    private string _parabensDe = "";
    private string ArqParabens() => "user://parabens_" + string.Concat((_model.Me?.Name ?? "geral").Where(char.IsLetterOrDigit));
    private HashSet<string> Parabenizados()
    {
        var quem = _model.Me?.Name ?? "geral";
        if (_parabenizados is not null && _parabensDe == quem) return _parabenizados;
        _parabensDe = quem;
        _parabenizados = new HashSet<string>();
        try
        {
            using var f = Godot.FileAccess.Open(ArqParabens(), Godot.FileAccess.ModeFlags.Read);
            if (f is not null) foreach (var l in f.GetAsText().Split('\n', StringSplitOptions.RemoveEmptyEntries)) _parabenizados.Add(l.Trim());
        }
        catch { }
        return _parabenizados;
    }

    private static string? _pocao1;
    private static bool _pocao1Lida;

    /// <summary>13/11: o tônico que a tecla 1 usa (preferência do cliente, user://pocao_1).</summary>
    internal static string? Pocao1()
    {
        if (_pocao1Lida) return _pocao1;
        _pocao1Lida = true;
        try { using var f = Godot.FileAccess.Open("user://pocao_1", Godot.FileAccess.ModeFlags.Read); var t = f?.GetAsText().Trim(); _pocao1 = string.IsNullOrEmpty(t) ? null : t; }
        catch { _pocao1 = null; }
        return _pocao1;
    }

    private void EscolherPocao1(string itemId)
    {
        var it = _model.Inventory.FirstOrDefault(i => i.ItemId == itemId);
        if (it is null) return;
        var def = it.DefinitionId;
        var cura = def.StartsWith("consumable_", StringComparison.Ordinal) && (def.Contains("tonico", StringComparison.Ordinal) || def.Contains("pocao", StringComparison.Ordinal) || def.Contains("vida", StringComparison.Ordinal));
        if (!cura) { _hud.ShowNotice("Só tônico (poção de vida) vai para a tecla 1."); Sfx.Play("ui_erro", -8f); return; }
        _pocao1 = def; _pocao1Lida = true;
        try { using var f = Godot.FileAccess.Open("user://pocao_1", Godot.FileAccess.ModeFlags.Write); f?.StoreString(def); } catch { }
        _hud.ShowNotice("Tecla 1 agora usa primeiro: " + (_model.ItemInfoFor(def)?.DisplayName ?? def) + ".");
        Sfx.Play("ui_clique");
    }

    private static string? _tecla2;
    private static bool _tecla2Lida;

    /// <summary>14/11 ("não conseguimos vincular nada no botão 2, o item cai no chão"): o consumível que a tecla 2 usa
    /// (arrastado da mochila/bolsinha para o quadro 2). Preferência do cliente, user://tecla_2.</summary>
    internal static string? Tecla2()
    {
        if (_tecla2Lida) return _tecla2;
        _tecla2Lida = true;
        try { using var f = Godot.FileAccess.Open("user://tecla_2", Godot.FileAccess.ModeFlags.Read); var t = f?.GetAsText().Trim(); _tecla2 = string.IsNullOrEmpty(t) ? null : t; }
        catch { _tecla2 = null; }
        return _tecla2;
    }

    private void EscolherTecla2(string itemId)
    {
        var it = _model.Inventory.FirstOrDefault(i => i.ItemId == itemId);
        if (it is null) return;
        var def = it.DefinitionId;
        var info = _model.ItemInfoFor(def);
        var usavel = info?.Slot == "consumable" || def.StartsWith("consumable_", StringComparison.Ordinal) || def.StartsWith("food_", StringComparison.Ordinal);
        if (!usavel) { _hud.ShowNotice("Só item de usar (poção, frasco, comida...) vai para a tecla 2."); Sfx.Play("ui_erro", -8f); return; }
        _tecla2 = def; _tecla2Lida = true;
        try { using var f = Godot.FileAccess.Open("user://tecla_2", Godot.FileAccess.ModeFlags.Write); f?.StoreString(def); } catch { }
        _hud.ShowNotice("Tecla 2 agora usa: " + (info?.DisplayName ?? def) + ".");
        Sfx.Play("ui_clique");
    }

    private void UseFirst(params string[] prefixes)
    {
        var item = prefixes.Select(p => _model.FirstInventoryItemWithPrefix(p)).FirstOrDefault(i => i is not null);
        if (item is null) { Sfx.Play("ui_erro", -8f); return; }
        _lastUseMana = item.DefinitionId.Contains("mana", StringComparison.Ordinal);
        Send(new UseItemIntent(item.ItemId));
    }

    // ------------------------------------------------------------ rede

    private async Task ConnectAsync()
    {
        if (_connecting || _client is not null) return;
        var host = _hostEdit.Text.Trim();
        var token = _tokenEdit.Text.Trim();
        if (!int.TryParse(_portEdit.Text.Trim(), out var port) || host.Length == 0 || token.Length == 0)
        {
            _statusText = "Preencha servidor, porta e token.";
            return;
        }

        _connecting = true;
        _statusText = "Conectando…";
        GameLog.Info("cliente.rede", $"conectando em {host}:{port}");
        var generation = Interlocked.Increment(ref _generation);
        var client = new TcpGameClient();
        client.MessageReceived += message => _incoming.Enqueue(message);
        client.Disconnected += () =>
        {
            if (Volatile.Read(ref _generation) != generation) return;
            GameLog.Warn("cliente.rede", "conexão caiu: " + (client.DisconnectReason.Length > 0 ? client.DisconnectReason : "motivo desconhecido"));
            Interlocked.Exchange(ref _disconnected, 1);
        };
        try
        {
            await client.ConnectAsync(host, port);
            _client = client;
            client.Send(new LobbyRequest(token, Protocol.Version));
            _statusText = "Autenticando…";
        }
        catch (Exception e)
        {
            client.Dispose();
            GameLog.Warn("cliente.rede", $"falha ao conectar em {host}:{port}: {e.GetType().Name}: {e.Message}");
            _statusText = "Falha ao conectar: " + e.Message + "\n(O servidor está aberto? Use o atalho Jogar.cmd.)";
        }
        finally
        {
            _connecting = false;
        }
    }

    private void DrainNetwork()
    {
        while (_incoming.TryDequeue(out var message))
        {
            if (_netSilentLogged) GameLog.Info("cliente.rede", $"servidor voltou a responder depois de {(Time.GetTicksMsec() - _lastNetMs) / 1000.0:0.0} s");
            _lastNetMs = Time.GetTicksMsec();
            _netSilentLogged = false;
            _model.Apply(message);
            OnVisualEvent(message);
            switch (message)
            {
                case LobbyMessage lobby:
                    // 14/11: o atualizador começa já no lobby (quem está em versão errada é barrado antes de entrar no mapa)
                    if (lobby.Ok && _hostEdit.Text.Trim() is not ("127.0.0.1" or "localhost" or "") && !_updaterOn && !AmigoBuild) { _updaterOn = true; _updater.Send += Send; _updater.Start(); }
                    GameLog.Info("cliente.login", lobby.Ok
                        ? $"lobby: conta {lobby.AccountName}, {lobby.Characters.Count} personagem(ns)"
                        : $"lobby recusado: {lobby.Error}");
                    if (!lobby.Ok)
                    {
                        _statusText = "Entrada recusada: " + (lobby.Error == "invalid_token" ? "token inválido" :
                            lobby.Error == "already_logged_in" ? "esta conta já está no jogo" : lobby.Error);
                        DropClient();
                        break;
                    }
                    _loginPanel.Visible = false;
                    _statusText = "";
                    _lobby.Show(lobby);
                    TryAutoEnter(lobby);
                    break;
                case LoginResult login:
                    GameLog.Info("cliente.login", login.Ok ? $"entrou como {login.Name} (nível {login.Level})" : $"entrada recusada: {login.Error}");
                    if (login.Ok) OnLoggedIn();
                    // 14/11 22h: no PC do amigo quem atualiza é o Jogar.cmd (GitHub) — o atualizador de dentro do jogo ficou
                    // desligado (baixava do servidor do anfitrião arquivos às vezes mais velhos, no meio da partida)
                    // 15/11 noite (amigo às 8h: "teve att o game?" — barrado o dia todo): o GitHub estava numa versão MAIS NOVA
                    // que a do servidor do anfitrião (que ainda não tinha aplicado), e o Jogar.cmd só sabia baixar do GitHub.
                    // Agora, versão diferente → baixa a versão EXATA do servidor do anfitrião (que é quem manda), marca para o
                    // Jogar.cmd não trocar de novo pela do GitHub nas próximas horas, e reabre sozinho.
                    else if (AmigoBuild && login.Error?.Contains("versão") == true)
                    {
                        if (!_updaterOn) { _updaterOn = true; _updater.Send += Send; _updater.Start(); }
                        _updater.MarcarVersaoDoAnfitriao = true;
                        _lobby.SetStatus(_updater.Done && _updater.Baixados == 0
                            ? "Seu jogo e o servidor estão em versões diferentes e não achei o que baixar. Avise o anfitrião."
                            : "Seu jogo e o servidor estão em versões diferentes: baixando a versão do servidor agora... (não feche; ele reabre sozinho)");
                    }
                    else _lobby.SetStatus(login.Error?.Contains("versão") == true && _updaterOn && !_updater.Done
                        ? "Seu jogo está desatualizado: baixando a versão do servidor agora... (não feche; ele reabre sozinho)"
                        : "Não foi possível entrar: " + login.Error);
                    break;
                case ErrorMessage error:
                    GameLog.Warn("cliente.rede", $"o servidor recusou uma mensagem: {error.Reason}");
                    break;
                case ActionResultMessage { Success: false } failed when failed.Action != "create_character" && failed.Action != "dodge":
                    GameLog.Debug("cliente.acao", $"{failed.Action} recusado: {failed.Reason}");
                    break;
                case CastResultMessage { Success: false } cast when cast.Failure != "Busy":
                    GameLog.Debug("cliente.acao", $"{cast.SkillId} falhou: {cast.Failure}");
                    break;
                case ActionResultMessage { Action: "delete_character" } deleted:
                    _lobby.SetStatus(deleted.Success ? "Personagem excluído. A vaga está livre." :
                        deleted.Reason == "name_mismatch" ? "O nome digitado não confere — nada foi excluído." : "Não foi possível excluir.");
                    break;
                case ActionResultMessage { Action: "create_character" } created:
                    GameLog.Info("cliente.login", created.Success ? "personagem criado" : $"criação recusada: {created.Reason}");
                    _lobby.SetStatus(created.Success ? "Personagem criado." : "Não foi possível criar: " + (_model.Log.LastOrDefault() ?? created.Reason));
                    break;
            }
        }

        if (Interlocked.Exchange(ref _disconnected, 0) == 1 && _client is not null)
        {
            var me = _model.Me;
            if (me is not null)
                GameLog.Info("cliente.rede", $"último estado: {me.Name} em ({me.X:0.0}; {me.Y:0.0}), vida {me.Health:0}, ação '{me.Action}'");
            _statusText = "Desconectado do servidor.";
            DropClient();
        }
    }

    // 13/11 ("quando o jogo carrega aparece um 3D mal feito da vila — usa uma arte"): tela de carregamento com a arte do
    // login enquanto o mapa chega e é montado; some suave quando o mundo já está de pé
    private CanvasLayer? _carga;
    private Control? _cargaRaiz;
    private Label? _cargaTexto;
    private TextureRect? _cargaFoto;
    private ulong _cargaDesde;
    private ulong _cargaAte = ulong.MaxValue;
    private ulong _cargaSaida;
    private int _quadrosLisos;
    private double _ultimoDelta;
    private ulong _loginAt;
    private double _fotoEm = 40;
    private double _postalEm = 12;
    private float _yawAnt, _velAnt;
    private Vector2 _posAnt;

    private void MostrarCarga(string texto)
    {
        _cargaSaida = 0; _quadrosLisos = 0;
        var eraVisivel = _carga is { Visible: true };
        if (_carga is null)
        {
            _carga = new CanvasLayer { Layer = 110 };
            AddChild(_carga);
            _cargaRaiz = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
            _cargaRaiz.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _carga.AddChild(_cargaRaiz);
            // 13/11 ("tira aquela imagem do carregamento; usa prints do jogo, sem texto"): fundo escuro + uma foto do
            // próprio jogo (FotosDeCarga), com um zoom bem lento
            var escuro = new ColorRect { Color = new Color(0.04f, 0.04f, 0.05f), MouseFilter = Control.MouseFilterEnum.Ignore };
            escuro.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _cargaRaiz.AddChild(escuro);
            _cargaFoto = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore };
            _cargaFoto.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _cargaRaiz.AddChild(_cargaFoto);
            var veu = new ColorRect { Color = new Color(0, 0, 0, 0.35f), MouseFilter = Control.MouseFilterEnum.Ignore };
            veu.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _cargaRaiz.AddChild(veu);
            _cargaTexto = HudView.MakeLabel("", 26, new Color(0.98f, 0.86f, 0.55f), 6);
            if (HudView.TitleFont is not null) _cargaTexto.AddThemeFontOverride("font", HudView.TitleFont);
            _cargaTexto.HorizontalAlignment = HorizontalAlignment.Center;
            _cargaTexto.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            _cargaTexto.Position = new Vector2(-400, -120);
            _cargaTexto.Size = new Vector2(800, 40);
            _cargaRaiz.AddChild(_cargaTexto);
        }
        _cargaTexto!.Text = texto;
        if (_cargaFoto is not null && !eraVisivel)
        {
            _cargaFoto.Texture = FotosDeCarga.Sortear();
            _cargaDesde = Time.GetTicksMsec();
        }
        _cargaRaiz!.Modulate = Colors.White;
        _carga.Visible = true;
        _cargaAte = ulong.MaxValue;
    }

    private void AtualizarCarga()
    {
        if (_carga is not { Visible: true } || _cargaRaiz is null) return;
        var agora = Time.GetTicksMsec();
        if (_cargaFoto is not null)
        {
            // 16/11 (dono: "as imagens dão leves zooms engasgando, comendo frame; deixa a imagem estática e usa só uma — quando
            // troca parece que carregou e ainda não"): uma foto só, parada, do começo ao fim do carregamento
            _cargaFoto.Scale = Vector2.One;
        }
        if (_mapView is null || agora < _cargaAte) { _quadrosLisos = 0; return; }
        // 18/11: só sai quando o jogo parou de engasgar (sombras/shaders do mapa novo compilam nos primeiros quadros):
        // 12 quadros seguidos abaixo de 50 ms, ou no máximo 5 s
        if (_cargaSaida == 0)
        {
            _quadrosLisos = Engine.GetFramesPerSecond() > 0 && _ultimoDelta < 0.05 ? _quadrosLisos + 1 : 0;
            if (_quadrosLisos < 12 && agora - _cargaAte < 5000) return;
            _cargaSaida = agora;
        }
        var a = 1f - (agora - _cargaSaida) / 500f;   // meio segundo de saída suave
        if (a <= 0f) { _carga.Visible = false; _cargaSaida = 0; _quadrosLisos = 0; return; }
        _cargaRaiz.Modulate = new Color(1, 1, 1, a);
    }

    private bool _avisoGrafico;

    private void OnLoggedIn()
    {
        // 15/11 noite (pente-fino 13: o dono jogou numa RTX 4090 no gráfico BAIXO sem perceber — casas sem textura): placa
        // forte no baixo ganha um aviso, uma vez por sessão, de como voltar ao alto
        if (!_avisoGrafico && GraphicsQuality.Current == GraphicsQuality.Level.Baixo && GraphicsQuality.PlacaForte())
        {
            _avisoGrafico = true;
            GetTree().CreateTimer(12).Timeout += () => _hud?.ShowNotice("Seu gráfico está no BAIXO (texturas simples). Sua placa aguenta o ALTO: aperte F10 ou ESC → Gráficos.");
        }
        MostrarCarga("Carregando o mundo…");
        _loginAt = Time.GetTicksMsec();
        _loginPanel.Visible = false;
        _loginBackground.Visible = false;
        _loginDim.Visible = false;
        _lobby.Hide();
        _hud.Root.Visible = true;
        _statusText = "";
        _moveSender.Reset();
        _faceThrottle.Reset();
        _cooldowns.Reset();
        _lastLevel = 0;
        _cam.Snap();
        CaptureMouse(true);
        GetWindow().Title = $"Cinzas da Maré — {_model.Name}";
    }

    private void DropClient()
    {
        Interlocked.Increment(ref _generation);
        _client?.Dispose();
        _client = null;
        _model.Reset();
        _cooldowns.Reset();
        ClearWorld();
        _loginPanel.Visible = true;
        _loginBackground.Visible = true;
        _loginDim.Visible = true;
        _lobby.Hide();
        _hud.Root.Visible = false;
        _inventory.Visible = false;
        _pausePanel.Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetWindow().Title = "Cinzas da Maré";
    }

    /// <summary>Volta à seleção de personagem (reconecta com o mesmo token).</summary>
    private void BackToLobby()
    {
        DropClient();
        _loginPanel.Visible = false;
        _ = ConnectAsync();
    }

    private void Send(NetMessage message) => _client?.Send(message);

    // ------------------------------------------------------------ eventos do servidor -> efeitos

    private void OnVisualEvent(NetMessage message)
    {
        var now = (long)Time.GetTicksMsec();
        switch (message)
        {
            case CatalogMessage catalog:
                foreach (var skill in catalog.Skills) _cooldowns.Register(skill.Id, skill.CooldownMs);
                break;
            case CastResultMessage cast when cast.Success:
                _cooldowns.StartSkill(cast.SkillId, now);
                if (MyState() is { } caster)
                    Sfx.PlayAt(SkillSound(cast.SkillId), caster.Pos + Vector3.Up, -4f);
                break;
            case CastResultMessage { Success: false } failedCast when failedCast.Failure is not ("Busy" or "OnCooldown"):
                Sfx.Play("ui_erro", -10f);
                break;
            case ActionResultMessage action when action.Action == "use_item" && action.Success:
                _cooldowns.Start(_lastUseMana ? CooldownTracker.Mana : CooldownTracker.Potion, now, _lastUseMana ? 6000 : CooldownTracker.PotionMs);
                PulseMine(_lastUseMana ? new Color(0.35f, 0.55f, 1.0f, 0.7f) : new Color(0.35f, 1.0f, 0.55f, 0.7f), 1.5f);
                Sfx.Play(_lastUseMana ? "mana" : "pocao");
                break;
            case ActionResultMessage { Action: "interact", Success: true } interact:
                PulseMine(new Color(1.0f, 0.85f, 0.35f, 0.8f), 2.2f);
                Sfx.Play(interact.Reason switch { "chest_opened" => "bau", "gate_opened" => "alavanca", "npc" or "station" or "storage" => "ui_abrir", "gather" => "minerar", _ => "ui_aviso" });
                break;
            case ActionResultMessage { Action: "equip" or "unequip", Success: true }:
                PulseMine(new Color(0.8f, 0.85f, 1f, 0.6f), 1.3f);
                Sfx.Play("equipar");
                break;
            case ActionResultMessage { Action: "buy", Success: true }:
                Sfx.Play("comprar");
                if (_lastDialog is { } shop) NpcSay(shop.NpcId, "feliz", "npc_aceno", 1.4f);
                break;
            case ActionResultMessage { Action: "sell", Success: true }:
                Sfx.Play("vender");
                break;
            case ActionResultMessage { Action: "travel" or "paid_travel", Success: true }:
                Sfx.Play("portal");
                break;
            case ActionResultMessage { Action: "spend_stat", Success: true }:
                Sfx.Play("ui_clique");
                break;
            case ActionResultMessage { Action: "quest", Success: true } questTalk when _lastDialog is { } questNpc:
                NpcSay(questNpc.NpcId, questTalk.Reason == "complete" ? "feliz" : "concorda", questTalk.Reason == "complete" ? "npc_aceno" : "npc_conversa", 2f);
                Sfx.Play(questTalk.Reason == "complete" ? "missao_completa" : "missao_aceita");
                break;
            case ActionResultMessage { Action: "buy" or "sell" or "quest", Success: false } grumble when _lastDialog is { } grumpy:
                NpcSay(grumpy.NpcId, grumble.Reason is "not_enough_coins" or "no_coins" ? "bravo" : "duvida", "npc_conversa", 1.5f);
                Sfx.Play("ui_erro", -6f);
                break;
            case ActionResultMessage { Action: "quest", Success: true } quest:
                Sfx.Play(quest.Reason == "complete" ? "missao_completa" : "missao_aceita");
                break;
            case ActionResultMessage { Success: false } refused when refused.Action is not ("dodge" or "create_character" or "move"):
                Sfx.Play("ui_erro", -6f);
                break;
            case CoinsMessage coins when coins.Gained != 0:
                if (coins.Gained > 0) _hud?.FeedCoins(coins.Gained);
                else _hud?.Feed($"{coins.Gained} moedas", new Color(0.85f, 0.6f, 0.5f));
                Sfx.Play(coins.Gained > 0 ? "moeda" : "gastar", coins.Gained > 0 ? -2f : -4f, 0.05f, 120);
                break;
            case NpcDialogMessage dialog:
                Sfx.Play("ui_abrir");
                NpcSay(dialog.NpcId, "ola", "npc_conversa", 3f);
                break;
            case StorageMessage box:
                // baú estilo MU: o baú abre à esquerda e a mochila junto, à direita (arrasta entre os dois)
                _inventory.Visible = true;
                _inventory.StorageOpen = box.Container;
                CaptureMouse(false);
                break;
            case StationMessage:
                CaptureMouse(false);
                break;
            case ActionResultMessage { Action: "craft", Success: true } crafted:
                Sfx.Play(crafted.Reason is "forja" ? "forja" : crafted.Reason is "caldeirao" or "panela" ? "caldeirao" : "equipar");
                PulseMine(new Color(1f, 0.7f, 0.3f, 0.7f), 1.6f);
                break;
            case ActionResultMessage { Action: "gather", Success: true }:
                Sfx.Play("pegar");
                break;
            case ActionResultMessage { Action: "plant" or "water", Success: true }:
                Sfx.Play("plantar");
                break;
            case ActionResultMessage { Action: "harvest", Success: true }:
                Sfx.Play("colher");
                break;
            case ActionResultMessage { Action: "wing_dash", Success: true }:
                Sfx.Play("asas");
                if (MyState() is { } winged) _effects.Pulse(winged.Pos, new Color(0.56f, 0.41f, 0.75f, 0.6f), 2.2f);
                break;
            case ActionResultMessage { Action: "storage", Success: true }:
                Sfx.Play("ui_clique");
                break;
            case UpdateManifestMessage or UpdateChunkMessage:
                _updater.Handle(message);
                break;
            case MapMessage map:
                // 11/11: atualizador — no 1º mapa, se o servidor é de outro PC, confere se tem versão nova do jogo
                var srvHost = _hostEdit.Text.Trim();
                if (srvHost is not ("127.0.0.1" or "localhost" or "") && !_updaterOn && !AmigoBuild) { _updaterOn = true; _updater.Send += Send; _updater.Start(); }
                Sfx.Play("portal", -6f);
                Sfx.Ambience(Sfx.AmbienceFor(map.Map.Id == "fornalha" ? "lava" : map.Map.Theme, map.Map.Instanced, map.Map.Id.StartsWith("porto", StringComparison.Ordinal)));
                break;
            case BestiaryMessage bm:
                _bestiario.Handle(bm);
                break;
            case BookMessage book:
                _livro.Show(book.Title, book.Text, book.Discovery);
                CaptureMouse(false);
                break;
            case VinteUmStateMessage vu:
                _vinteUm.Handle(vu);
                if (vu.Open) { _model.Dialog = null; _panels.CloseAll(); CaptureMouse(false); }
                else CaptureMouse(!GodOpen && !_inventory.Visible && !_panels.AnyOpen);
                break;
            case TradeStateMessage trade:
                _troca.Handle(trade);
                if (trade.State == "aberta")
                {
                    // 12/11: a mochila abre junto e o clique direito põe o item na mesa
                    if (!_inventory.Visible) { _inventory.Visible = true; Sfx.Play("ui_abrir"); }
                    _inventory.TradePut = id => Send(new TradeIntent("item", ItemId: id));
                    _troca.Put = id => Send(new TradeIntent("item", ItemId: id));
                    _inventory.OverTrade = () => _troca.Root.Visible && _troca.Root.GetGlobalRect().HasPoint(_troca.Root.GetGlobalMousePosition());
                }
                else if (trade.State == "fechada" && _inventory.TradePut is not null)
                {
                    _inventory.TradePut = null;
                    _inventory.Visible = false;
                }
                if (trade.State != "fechada") CaptureMouse(false);
                else CaptureMouse(!GodOpen && !_inventory.Visible && !_panels.AnyOpen);
                break;
            case NoticeMessage notice:
                if (notice.Text.Contains("enfurece")) Sfx.Play("rugido");
                else if (notice.Kind == "legend") { Sfx.Play("nivel"); PulseMine(new Color(0.3f, 1f, 0.6f, 0.6f), 1.6f); }
                else if (notice.Kind == "regiao") { Sfx.Play("nivel"); PulseMine(new Color(1f, 0.75f, 0.35f, 0.6f), 2.2f); }   // 13/11: região nova   // 11/11: drop/refino grande
                else if (notice.Kind == "reward") Sfx.Play("ui_aviso", -4f);
                else if (notice.Kind == "nivel") { Sfx.Play("nivel"); _hud.MostrarNivel(notice.Text); }   // 18/11: cartão de nível
                else if (notice.Kind == "pesquisa_abrir") { _pesquisa.Show(global: true); CaptureMouse(false); }   // 17/11: Catálogo (18/11: busca em tudo)
                else if (notice.Kind == "mesclar_abrir") { _mesclar.Show(_model); CaptureMouse(false); }   // 18/11: Máquina de Mesclar Visual
                else if (notice.Kind == "pedra" && notice.Text.Split('|') is { Length: 3 } pp
                         && double.TryParse(pp[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var px)
                         && double.TryParse(pp[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var py))
                {   // 17/11: cada golpe na rocha oca dá uma tremidinha nela
                    _mapView?.TremerPedra(px, py);
                    Sfx.PlayAt("minerar", new Vector3((float)px, 1f, (float)py), 0f, 0.15f);
                }
                if (notice.Text.StartsWith('+')) _hud?.Feed("Obteve: " + notice.Text[1..].Trim());
                break;
            case ClaimResultMessage claim when claim.Success:
                PulseMine(new Color(1.0f, 0.85f, 0.35f, 0.7f), 1.4f);
                _hud?.Feed("Obteve: " + (_lastClaimLabel ?? "item"));
                Sfx.Play("pegar");
                break;
            case ProgressMessage progress:
                if (_lastLevel > 0 && progress.Level > _lastLevel)
                {
                    Sfx.Play("nivel");
                    PulseMine(new Color(1.0f, 0.85f, 0.35f, 0.8f), 2.6f);
                    ImpactMine(new Color(1.0f, 0.85f, 0.35f));
                    _hud?.ShowNotice($"Nível {progress.Level}!  Vida e poder aumentaram.");
                }
                _lastLevel = progress.Level;
                break;
            case CombatEvent combat:
                ShowCombat(combat);
                break;
        }
    }

    private EntityState? FindState(string id)
        => _players.TryGetValue(id, out var p) ? p : _monsters.TryGetValue(id, out var m) ? m : null;

    private Node3D? _pip;
    private float _pipT;

    /// <summary>O vagalume-guia voando em volta do próprio personagem (só visual; as dicas vêm do GuideHints).</summary>
    private AmbientParticles? _air;

    private void UpdatePip(double delta)
    {
        var st = MyState();
        if (st is null || _model.Map is null) { if (_pip is not null) _pip.Visible = false; return; }
        if (_pip is null)
        {
            _pip = new Node3D { Name = "Pip" };
            _world.AddChild(_pip);
            VisualKit.Part(_pip, VisualKit.Sphere(0.06f), VisualKit.Glow(new Color(0.75f, 1f, 0.55f), 5f), Vector3.Zero);
            foreach (var side in new[] { -1f, 1f })
                VisualKit.Part(_pip, VisualKit.Sphere(0.05f), VisualKit.Unshaded(new Color(0.85f, 0.95f, 1f, 0.35f), true),
                    new Vector3(side * 0.06f, 0.03f, 0), null, new Vector3(1f, 0.25f, 0.6f));
            _pip.AddChild(new OmniLight3D { LightColor = new Color(0.7f, 1f, 0.5f), LightEnergy = 0.7f, OmniRange = 2.2f });
        }
        _pipT += (float)delta;
        _pip.Visible = true;
        var target = st.Pos + new Vector3(Mathf.Cos(_pipT * 0.9f) * 0.75f, 2.0f + 0.15f * Mathf.Sin(_pipT * 2.3f), Mathf.Sin(_pipT * 0.9f) * 0.75f);
        _pip.Position = _pip.Position.DistanceTo(target) > 6f ? target : _pip.Position.Lerp(target, 1f - Mathf.Exp(-4f * (float)delta));
    }

    private const float PlayerDustAt = 1.3f, MonsterDustAt = 1.0f;

    /// <summary>
    /// Efeitos dos golpes da família de caranguejos (lote 2): o barril do Ermitão voa em arco até a marca e explode;
    /// o monte de terra do Escavador corre por baixo do chão até o alvo e estoura quando ele sai; espinhos do de Pedra.
    /// Só visual — quem decide onde cai e quem acerta é o servidor (a marca no chão é a mesma).
    /// </summary>
    private void CrabFx(EntityState st, MonsterView m)
    {
        var act = m.Alive ? m.Action : "";
        var changed = act != st.FxAction;
        st.FxAction = act;
        var aim = new Vector3((float)m.AimX, 0f, (float)m.AimY);

        // barril explosivo: aparece na metade da preparação e cai na marca no fim
        if (act == "windup:barril_explosivo" && m.ActionT > 0.45)
        {
            if (st.Barrel is null)
            {
                // lote 3: barril próprio (0,6 m, pavio aceso, origem no centro); senão o barril do cenário reduzido
                if (GlbCache.Instance("projeteis/proj_barril_explosivo") is { } keg) st.Barrel = keg;
                else { st.Barrel = GlbCache.Instance("proj_barril_explosivo") ?? new Node3D(); st.Barrel.Scale = Vector3.One * 0.62f; }
                var fuse = new OmniLight3D { LightColor = new Color(1f, 0.55f, 0.15f), LightEnergy = 2.2f, OmniRange = 2.5f, Position = new Vector3(0, 0.45f, 0) };
                st.Barrel.AddChild(fuse);
                _world.AddChild(st.Barrel);
            }
            var k = Mathf.Clamp((float)(m.ActionT - 0.45) / 0.55f, 0f, 1f);
            var from = st.Pos + new Vector3(0, 1.2f, 0);
            var to = aim + new Vector3(0, 0.2f, 0);
            st.Barrel.Visible = true;
            st.Barrel.Position = from.Lerp(to, k) + Vector3.Up * (4f * k * (1f - k) * 3.2f);
            st.Barrel.Rotation = new Vector3(k * 8f, k * 3f, 0);
        }
        else if (st.Barrel is not null) st.Barrel.Visible = false;
        if (changed && act == "active:barril_explosivo")
        {
            _effects.Impact(aim + Vector3.Up * 0.5f, new Color(1f, 0.55f, 0.15f), 30, 2.6f);
            _effects.Pulse(aim, new Color(1f, 0.45f, 0.12f, 0.75f), (float)m.AimRadius + 0.4f, 0.55);
            _effects.Death(aim + Vector3.Up * 0.3f, new Color(1f, 0.6f, 0.2f));
            _effects.Dust(aim, new Color(0.4f, 0.35f, 0.3f), 1.6f);
            Sfx.PlayAt("explosao", aim + Vector3.Up, 0f, 0.1f);
            if (IsNear(aim, 12f)) _cam.AddShake(0.4f);
        }

        // Escavador: monte de terra andando por baixo do chão (aviso de onde ele vai sair)
        if (act == "windup:emergir")
        {
            if (st.Mound is null)
            {
                st.Mound = GlbCache.Instance("prop_monte_terra_escavador") ?? new Node3D();
                _world.AddChild(st.Mound);
            }
            st.Mound.Visible = true;
            st.Mound.Position = st.Pos;
            st.FxDust -= (float)GetProcessDeltaTime();
            if (st.FxDust <= 0f) { st.FxDust = 0.14f; _effects.Dust(st.Pos, new Color(0.62f, 0.52f, 0.38f), 1.2f); }
        }
        else if (st.Mound is not null) st.Mound.Visible = false;
        if (changed && act == "active:emergir")
        {
            _effects.Impact(st.Pos + Vector3.Up * 0.4f, new Color(0.7f, 0.58f, 0.4f), 26, 2.2f);
            _effects.Pulse(st.Pos, new Color(0.75f, 0.6f, 0.4f, 0.7f), (float)m.AimRadius + 0.3f, 0.5);
            for (var i = 0; i < 6; i++)
                _effects.Dust(st.Pos + new Vector3(Mathf.Cos(i * 1.05f), 0, Mathf.Sin(i * 1.05f)) * 1.4f, new Color(0.6f, 0.5f, 0.36f), 1.8f);
            Sfx.PlayAt("hab_impacto", st.Pos + Vector3.Up, -2f, 0.1f);
            if (IsNear(st.Pos, 12f)) _cam.AddShake(0.3f);
        }

        // Pedra: ao soltar os espinhos, um tremor curto (a carapaça abre)
        if (changed && act == "active:chuva_de_espinhos")
        {
            _effects.Pulse(st.Pos, new Color(0.7f, 0.68f, 0.6f, 0.6f), 2.2f, 0.35);
            Sfx.PlayAt("hab_espinhos", st.Pos + Vector3.Up, -3f, 0.1f);
        }
        // Tirano: guarda de pinça e maré furiosa estouram em volta
        if (changed && act is "active:guarda_de_pinca" or "active:mare_furiosa" or "active:esmagar")
        {
            _effects.Impact(aim + Vector3.Up * 0.3f, new Color(0.55f, 0.75f, 0.95f), 26, 2.6f);
            _effects.Pulse(aim, new Color(0.5f, 0.75f, 1f, 0.7f), (float)m.AimRadius + 0.4f, 0.5);
            Sfx.PlayAt("hab_impacto", aim + Vector3.Up, 1f, 0.1f);
            if (IsNear(aim, 16f)) _cam.AddShake(act == "active:mare_furiosa" ? 0.6f : 0.35f);
        }
        if (changed && act == "windup:mare_furiosa") Sfx.PlayAt("rugido", st.Pos + Vector3.Up, 3f, 0.05f);
    }

    /// <summary>Vento das cinzas: leve, de lado em relação à câmera (lê bem na tela).</summary>
    private Vector3 WindDir()
    {
        var r = _cam.Camera.GlobalBasis.X;
        return new Vector3(r.X, 0, r.Z).Normalized() * 1.1f;
    }

    /// <summary>Some aos poucos (0 = inteiro, 1 = invisível) — usado ao virar cinza; volta a 0 ao renascer.</summary>
    private static void SetFade(EntityState st, float f)
    {
        if (Mathf.IsEqualApprox(st.Fade, f)) return;
        st.Fade = f;
        ApplyFade(st.Visual.Root, f);
    }

    private static void ApplyFade(Node n, float f)
    {
        if (n is GeometryInstance3D g) g.Transparency = f;
        foreach (var c in n.GetChildren()) ApplyFade(c, f);
    }

    /// <summary>Retrato do herói para a troca de controlador (o laboratório mede a velocidade por conta própria).</summary>
    private PlayerHandoff HeroHandoff()
    {
        var st = MyState();
        return new PlayerHandoff(st?.Pos ?? Vector3.Zero, Vector3.Zero, st?.Visual.Root.Rotation.Y ?? 0f, st?.Visual, _cam,
            _model.BasicSkill?.Id);
    }

    private EntityState? MyState()
        => _model.CharacterId is not null && _players.TryGetValue(_model.CharacterId, out var s) ? s : null;

    private void PulseMine(Color color, float radius)
    {
        if (MyState() is { } s) _effects.Pulse(s.Pos, color, radius);
    }

    private void ImpactMine(Color color)
    {
        if (MyState() is { } s) _effects.Impact(s.Pos + new Vector3(0f, 1.2f, 0f), color, 14, 1.2f);
    }

    /// <summary>Um golpe aconteceu (decidido e transmitido pelo servidor).</summary>
    private void ShowCombat(CombatEvent e)
    {
        var target = FindState(e.TargetId);
        if (target is null) return;
        var attacker = FindState(e.AttackerId);
        var mine = e.AttackerId == _model.CharacterId;
        var taken = e.TargetId == _model.CharacterId;
        var fromMonster = _monsters.ContainsKey(e.AttackerId);
        var head = target.Pos + new Vector3(0f, target.Visual.Height * 0.9f, 0f);
        if (!e.Evaded) Efeitos.PoderesVfx.Acerto(_effects.Root, e.SkillId, target.Pos, target.Visual.Height, attacker?.Pos, e.Heal, mine, _cam.AddShake);   // gancho VFX 14/11 (Missão Terça)

        if (e.Heal)
        {
            // cura (número verde) ou barreira (escudo azul-esverdeado): sem "golpe"
            // 17/11 noite (dono: "o escudo do mago diz que recuperou vida e não recupera"): a Esfera de Proteção é ESCUDO
            // (absorve golpes), não cura — mostra "◈ escudo N" azul, nunca "+N" verde
            var barrier = e.Shield || e.SkillId.Contains("barreira") || e.SkillId.Contains("protecao") || e.SkillId.Contains("escudo")
                || _model.Catalog?.Skills.Any(sk => sk.Id == e.SkillId && sk.TargetMode == "barrier") == true;
            var c = barrier ? new Color(0.45f, 0.85f, 1f) : new Color(0.45f, 1f, 0.5f);
            _hud.SpawnFloater(head, barrier ? $"◈ escudo {e.Damage}" : $"+{e.Damage}", c, 24);
            _effects.Pulse(target.Pos, new Color(c.R, c.G, c.B, 0.55f), 1.3f, 0.6);
            _effects.Impact(target.Pos + new Vector3(0, 1.1f, 0), new Color(1f, 0.85f, 0.4f), 6, 0.7f);
            if (attacker is not null && !ReferenceEquals(attacker, target))
                _effects.Slash(attacker.Pos + new Vector3(0, 1.2f, 0), target.Pos + new Vector3(0, 1.1f, 0), new Color(0.4f, 1f, 0.6f, 0.6f), 0.05f);
            Sfx.PlayAt("cura", target.Pos + Vector3.Up, -3f);
            return;
        }
        if (e.Absorbed > 0) _hud.SpawnFloater(head + new Vector3(0, 0.4f, 0), $"absorveu {e.Absorbed}", new Color(0.45f, 0.95f, 0.85f), 16);
        if (e.Evaded && e.SkillId == "parry")
        {
            // aparou na hora certa: faísca grande, som de metal, tranco curto na tela
            _hud.SpawnFloater(head, "APAROU!", new Color(1f, 0.85f, 0.3f), 30);
            _effects.Impact(target.Pos + new Vector3(0, 1.2f, 0), new Color(1f, 0.9f, 0.5f), 14, 1.4f);
            Sfx.PlayAt("acerto_lamina", target.Pos + Vector3.Up, 2f);
            Sfx.PlayAt("esquiva", target.Pos + Vector3.Up, -2f);
            HitStop(0.12);
            return;
        }
        if (e.Evaded)
        {
            _hud.SpawnFloater(head, "esquivou", new Color(0.6f, 0.85f, 1f), 18);
            Sfx.PlayAt("esquiva", target.Pos + Vector3.Up, -4f);
            return;
        }
        if (taken)
        {
            Sfx.Play("dano", e.Crit ? 2f : -1f, 0.1f, 60);
            _hud.FlashDamage(e.Damage);
        }
        else Sfx.PlayAt(e.SkillId.Contains("explosao") || e.SkillId.Contains("estilhaco") ? "explosao" : mine ? MyHitSound() : "acerto", target.Pos + Vector3.Up, mine ? 0f : -6f);
        if (e.TargetDied) Sfx.PlayAt(taken ? "morte" : "monstro_morre", target.Pos + Vector3.Up, taken ? 2f : -2f);
        var heavy = e.SkillId is ClientModel.SkillGolpe or "monster_pancada" or "skill_mago_explosao_de_cinzas";
        var numberColor = taken ? new Color(1.0f, 0.35f, 0.30f) : (mine ? new Color(1.0f, 0.90f, 0.45f) : new Color(1.0f, 0.65f, 0.40f));
        _hud.SpawnFloater(head, e.Crit ? $"{e.Damage}!" : e.Damage.ToString(), numberColor, e.Crit || e.Exposed ? 30 : 22);
        if (e.Exposed && mine) _hud.SpawnFloater(head + new Vector3(0, 0.45f, 0), "EXPOSTO", new Color(1f, 0.8f, 0.25f), 16);
        if (e.Staggered && mine) _hud.SpawnFloater(head + new Vector3(0, 0.45f, 0), "QUEBROU!", new Color(1f, 0.55f, 0.2f), 18);

        var hitFrom = attacker is not null ? (target.Pos - attacker.Pos) : Vector3.Back;
        target.Visual.Hit(hitFrom.Normalized());
        var chest = target.Pos + new Vector3(0f, target.Visual.Height * 0.55f, 0f);
        var streak = fromMonster ? new Color(1.0f, 0.25f, 0.20f) : (heavy ? new Color(1.0f, 0.65f, 0.20f) : new Color(1.0f, 0.95f, 0.80f));
        var ranged = e.SkillId is "skill_mago_seta_de_brasa" or "monster_bola_de_cinza" or "skill_mago_explosao_de_cinzas"
            || e.SkillId.Contains("bruxa") || e.SkillId.Contains("arqueiro") || e.SkillId.Contains("batedor") || e.SkillId.Contains("arcanista");
        if (attacker is not null && !ranged && !Efeitos.PoderesVfx.Conhece(e.SkillId))   // gancho VFX 14/11: o poder tem rastro próprio
            _effects.Slash(attacker.Pos + new Vector3(0f, attacker.Visual.Height * 0.55f, 0f), chest, streak, heavy ? 0.12f : 0.07f);
        _effects.Impact(chest, ranged ? new Color(1f, 0.55f, 0.2f) : streak, e.Crit ? 14 : 8, e.Crit || heavy ? 1.4f : 1.0f);
        // 13/11 ("mini respingos de sangue quando bato no monstro e quando ele me bate — tipo o sangramento, só que menor")
        if (Sangra(target)) RespingoSangue(target, chest, hitFrom, e.Crit || heavy ? 9 : 5);
        if (e.TargetDied)
            _effects.Death(target.Pos, fromMonster ? new Color(0.85f, 0.2f, 0.2f) : new Color(0.75f, 0.6f, 0.4f));
        if (taken) _cam.AddShake(e.SkillId == "monster_pancada" ? 0.6f : e.Crit ? 0.25f : 0.14f);
        if (mine && heavy) _cam.AddShake(0.08f);
        // peso no golpe: o alvo é empurrado um pouco (o servidor corrige a posição suavemente) e, em crítico, golpe
        // pesado ou abate, o tempo "trava" por um instante (hit-stop)
        if (mine && !fromMonster && hitFrom.LengthSquared() > 1e-4f)
            target.Pos += new Vector3(hitFrom.X, 0, hitFrom.Z).Normalized() * (heavy || e.Crit ? 0.35f : 0.15f);
        if (mine && (e.Crit || heavy || e.TargetDied)) HitStop(e.TargetDied ? 0.09 : 0.06);
    }

    /// <summary>13/11: quebra um texto em linhas de até n letras (sem cortar palavra).</summary>
    internal static string Quebrar(string texto, int n)
    {
        var linhas = new List<string>(); var atual = "";
        foreach (var w in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (atual.Length > 0 && atual.Length + 1 + w.Length > n) { linhas.Add(atual); atual = w; }
            else atual = atual.Length == 0 ? w : atual + " " + w;
        }
        if (atual.Length > 0) linhas.Add(atual);
        return string.Join("\n", linhas);
    }

    private ulong _hitStopUntil;

    /// <summary>Pausa curtíssima no impacto (tempo do jogo quase parado por alguns centésimos de segundo).</summary>
    private void HitStop(double seconds)
    {
        Engine.TimeScale = 0.08;
        _hitStopUntil = Time.GetTicksMsec() + (ulong)(seconds * 1000);
    }

    // ------------------------------------------------------------ interface

    private void BuildUi()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var backgroundTexture = VisualAssets.Load("fundo_login.jpg");
        if (backgroundTexture is not null)
            _loginBackground = new TextureRect
            {
                Texture = backgroundTexture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
        else
            _loginBackground = new ColorRect { Color = new Color(0.05f, 0.05f, 0.07f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _loginBackground.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_loginBackground);

        _loginDim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.5f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _loginDim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_loginDim);

        _loginPanel = new PanelContainer { Position = new Vector2(48, 48) };
        _loginPanel.AddThemeStyleboxOverride("panel", HudView.PanelStyle(14f, 0.9f));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        _loginPanel.AddChild(box);
        box.AddChild(HudView.MakeLabel("Cinzas da Maré", 26, new Color(0.95f, 0.78f, 0.30f)));
        box.AddChild(HudView.MakeLabel("Cinzas da Maré · as terras de KRYMOR", 14));   // 17/11: sem "protótipo" (análise, acabamento)
        _hostEdit = AddField(box, "Servidor", false);
        _portEdit = AddField(box, "Porta", false);
        _tokenEdit = AddField(box, "Token", true);
        var connect = new Button { Text = "Conectar" };
        connect.Pressed += () => _ = ConnectAsync();
        box.AddChild(connect);
        _statusLabel = HudView.MakeLabel("", 14);
        box.AddChild(_statusLabel);
        layer.AddChild(_loginPanel);

        _lobby = new LobbyView();
        _lobby.EnterRequested += id => Send(new EnterWorldIntent(id, CinzasDaMare.Shared.VersaoJogo.Atual));
        _lobby.CreateRequested += (name, cls) => Send(new CreateCharacterIntent(name, cls));
        _lobby.DeleteRequested += (id, confirm) => Send(new DeleteCharacterIntent(id, confirm));
        _lobby.BackRequested += () => { DropClient(); };
        layer.AddChild(_lobby.Root);

        _hud = new HudView();
        layer.AddChild(_hud.Root);
        SlotPocaoDrop.Escolheu += EscolherPocao1;
        SlotTecla2Drop.Escolheu += EscolherTecla2;
        // 13/11: fechou a caixa "Parabéns" (OK ou X) — volta a prender o mouse se não houver outra janela aberta
        // 13/11 22:26: o "Parabéns" de cada coisa aparece uma vez só por personagem (lista guardada no PC)
        _model.JaParabenizado = chave => Parabenizados().Contains(chave);
        _model.MarcarParabenizado = chave =>
        {
            Parabenizados().Add(chave);
            try { using var f = Godot.FileAccess.Open(ArqParabens(), Godot.FileAccess.ModeFlags.Write); f?.StoreString(string.Join("\n", Parabenizados())); } catch { }
        };
        _hud.Desafiar += alvo => Send(new PartyIntent("duelo", alvo));
        _hud.ConvidarGrupo += alvo => Send(new PartyIntent("invite", alvo));   // 14/11: x1 pela lista do grupo (embaixo do minimapa)
        _hud.GanhouFechou += () => CaptureMouse(!_inventory.Visible && !_panels.AnyOpen && !GodOpen && !_troca.Open && !_livro.Open && !_bestiario.Open && !_vinteUm.Open);

        // telas do mundo antes da mochila: a mochila (e suas janelas) fica por cima dos avisos
        AddChild(new Arrastador());   // 12/11: janelas arrastáveis pela faixa do título
        _panels = new WorldPanels();
        _panels.Send += Send;
        _god = new GodPanel();
        _god.Send += Send;
        _official = new NetworkPlayerController(
            HandleContinuousInput,
            e => { HandleInput(e); return true; },
            HeroHandoff,
            (dir, sprint) => Send(new MoveIntent(dir.X, dir.Y, sprint, Walk: !sprint)),
            Queue,
            _ => Dodge());
        AddChild(_official);
        if (!AmigoBuild)
        {
        _lab = new PlayerSandboxManager(_official, () => MyState() is null ? null : HeroHandoff(), () => _model.MapId, () => _cam.Camera);
        AddChild(_lab);
        _god.AttachLab(_lab);
        }
        _editor = new EditorView { Name = "Editor" };
        AddChild(_editor);
        _editor.Send += Send;
        _editor.Applied += () => _forceMapRebuild = true;
        _editor.Exited += () => { _cam.Camera.Current = true; CaptureMouse(true); };
        _editor.CurrentMap = () => _mapView;
        _editor._onde = () => _model.Me is { } eu ? new Vector3((float)eu.X, 0, (float)eu.Y) : null;
        _editor.BackToEdit += () =>
        {
            var me = _model.Me;
            _editor.Enter(me is null ? Vector3.Zero : new Vector3((float)me.X, 0, (float)me.Y), Mathf.RadToDeg(_cam.Yaw));
        };
        _god.Lab += acao =>
        {
            switch (acao)
            {
                case "foto_carga": _god.Hide(); CaptureMouse(true); GetTree().CreateTimer(0.3).Timeout += () => { FotosDeCarga.Tirar(this, _cam.Camera); _hud.ShowNotice("Foto tirada para a tela de carregamento."); }; break;
                case "ver_carga": MostrarCarga("Carregando o mundo…"); _cargaAte = Time.GetTicksMsec() + 3000; break;
                case "sacudir": OrbeHud.Chacoalhar(4f); GetTree().CreateTimer(0.45).Timeout += () => OrbeHud.Chacoalhar(-4.5f); break;
            }
        };
        _god.EditRequested += () =>
        {
            _god.Hide();
            var me = _model.Me;
            _editor.Enter(me is null ? Vector3.Zero : new Vector3((float)me.X, 0, (float)me.Y), Mathf.RadToDeg(_cam.Yaw));
        };
        _panels.BindRequested += (key, skillId) =>
        {
            // um poder fica numa tecla só; "" = deixar a tecla vazia
            foreach (var k in _model.Binds.Where(kv => kv.Value == skillId && skillId.Length > 0).Select(kv => kv.Key).ToList()) _model.Binds[k] = "";
            _model.Binds[key] = skillId;
            SaveBinds();
        };
        layer.AddChild(_panels.Root);
        _troca.Send += Send;
        _troca.InventarioEsquerda = () => _inventory.Visible ? _inventory.Root.GlobalPosition.X : null;
        layer.AddChild(_troca.Root);
        Arrastador.Ligar(_troca.Root, "troca", 46f, porDesvio: true, fechar: () => Send(new TradeIntent("cancelar")));
        layer.AddChild(_livro.Root);
        Arrastador.Ligar(_livro.Root, "livro", 46f, porDesvio: true, fechar: () => _livro.Close());
        layer.AddChild(_pesquisa.Root);
        Arrastador.Ligar(_pesquisa.Root, "pesquisa", 46f, porDesvio: true, fechar: () => _pesquisa.Close());
        layer.AddChild(_mesclar.Root);
        Arrastador.Ligar(_mesclar.Root, "mesclar", 46f, porDesvio: true, fechar: () => { _mesclar.Close(); CaptureMouse(!_inventory.Visible && !_panels.AnyOpen); });
        _mesclar.Send = Send;
        _pesquisa.Pesquisar = texto => Send(new PesquisaIntent(texto) { Global = _pesquisa.Global });

        layer.AddChild(_trilha);
        _trilha.Escolher = (n, id) => { Send(new TrilhaEscolhaIntent(n, id)); Sfx.Play("moeda"); };   // 18/11 17h: prêmio 1 de 3
        layer.AddChild(_amigos);
        _amigos.Send += Send;
        layer.AddChild(_menuTab);
        _menuTab.Send += Send;
        // 14/11 tarde (dono: "no TAB não é pra abrir coisas por cima, é pra trocar de aba"): o TAB pega emprestado o
        // conteúdo das janelas próprias (Trilha, Mapa, Opções, Teclas) e devolve ao trocar de aba ou fechar
        _menuTab.Corpo = id => id switch
        {
            "trilha" => _trilha.Corpo, "mapa" => _panels.MapaCorpo, "opcoes" => _opcoesBox, "teclas" => _teclasCorpo, _ => null,
        };
        _menuTab.Embutido += (id, on) =>
        {
            if (id == "trilha" && on) { _trilha.Visible = false; Send(new TrilhaIntent()); _trilha.Focar(); }
            if (id == "mapa") { _panels.MapaNoMenu = on; if (on && _panels.IsOpen("map")) _panels.Toggle("map"); }
            if (id == "teclas") { if (on) { _teclasPanel.Visible = false; MontarTeclas(); } else _teclaEsperando = null; }
            if (id == "opcoes" && on) { _pausePedido = false; _pausePanel.Visible = false; }
        };
        layer.AddChild(_bestiario.Root);
        Arrastador.Ligar(_bestiario.Root, "livro_monstros", 46f, porDesvio: true, fechar: () => _bestiario.Hide());
        _livro.Closed += () => CaptureMouse(!GodOpen && !_inventory.Visible && !_panels.AnyOpen);
        _vinteUm.Send += Send;
        layer.AddChild(_vinteUm.Root);
        Arrastador.Ligar(_vinteUm.Root, "vinteum", 46f, porDesvio: true, fechar: () => _vinteUm.Leave());
        Arrastador.Fechou += () =>
        {
            if (_panels.DialogOpen is false) _model.Dialog = null;
            CaptureMouse(!GodOpen && !_inventory.Visible && !_panels.AnyOpen && !_troca.Open && !_livro.Open && !_bestiario.Open && !_vinteUm.Open);
        };
        _chat.Send += Send;
        _chat.Closed += () => CaptureMouse(!GodOpen && !_inventory.Visible && !_panels.AnyOpen && !_troca.Open);
        layer.AddChild(_chat.Root);
        layer.AddChild(_god.Root);

        _inventory = new InventoryPanel();
        _panels.InventarioEsquerda = () => _inventory.Visible ? _inventory.Root.GlobalPosition.X : null;
        _inventory.OverShop = () => _panels.MouseOverDialog;
        _inventory.Send += Send;
        _inventory.OpenTravel += () => { _inventory.Visible = false; if (!_panels.IsOpen("map")) TogglePanel("map"); };
        layer.AddChild(_inventory.Root);

        _pausePanel = new PanelContainer { Visible = false };
        _pausePanel.AddThemeStyleboxOverride("panel", HudView.PanelStyle(12f, 0.85f));
        _pausePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        _pausePanel.Position = new Vector2(-190, -160);
        var pauseBox = new VBoxContainer();
        pauseBox.AddChild(HudView.MakeLabel("Mouse livre", 18, new Color(0.95f, 0.78f, 0.30f)));
        pauseBox.AddChild(HudView.MakeLabel("Clique no jogo para voltar a controlar a câmera.", 13));
        pauseBox.AddChild(HudView.MakeLabel("Versão " + CinzasDaMare.Shared.VersaoJogo.Atual, 12, new Color(0.6f, 0.75f, 0.9f)));   // 12/11: para conferir com o amigo
        // 14/11 tarde: o miolo das opções fica numa caixa própria (o TAB a pega emprestada na aba Opções)
        var pauseHost = pauseBox;
        pauseBox = _opcoesBox = new VBoxContainer();
        pauseHost.AddChild(_opcoesBox);
        // 11/11: configurações de som (o jogo estava alto demais e não tinha onde baixar)
        pauseBox.AddChild(new HSeparator());
        pauseBox.AddChild(HudView.MakeLabel("Configurações de som", 16, new Color(0.95f, 0.78f, 0.30f)));
        HSlider Volume(string nome, float valor)
        {
            var row = new HBoxContainer();
            var label = HudView.MakeLabel(nome, 13);
            label.CustomMinimumSize = new Vector2(110, 0);
            row.AddChild(label);
            var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, Value = valor * 100, CustomMinimumSize = new Vector2(180, 24),
                FocusMode = Control.FocusModeEnum.None };
            row.AddChild(slider);
            var pct = HudView.MakeLabel($"{valor * 100:0}%", 13);
            pct.CustomMinimumSize = new Vector2(44, 0);
            slider.ValueChanged += v => pct.Text = $"{v:0}%";
            row.AddChild(pct);
            pauseBox.AddChild(row);
            return slider;
        }
        var geral = Volume("Volume geral", Sfx.Geral);
        var efeitos = Volume("Efeitos", Sfx.Volume);
        var ambiente = Volume("Ambiente", Sfx.Ambiente);
        void Aplicar(double _) => Sfx.SetVolumes((float)geral.Value / 100f, (float)efeitos.Value / 100f, (float)ambiente.Value / 100f);
        geral.ValueChanged += Aplicar; efeitos.ValueChanged += Aplicar; ambiente.ValueChanged += Aplicar;
        var mute = new CheckBox { Text = "Sem som", ButtonPressed = Sfx.Geral <= 0.001f, FocusMode = Control.FocusModeEnum.None };
        var lastGeral = Sfx.Geral > 0.001f ? Sfx.Geral : 0.7f;
        mute.Toggled += on =>
        {
            if (on) { lastGeral = Math.Max(0.05f, (float)geral.Value / 100f); geral.Value = 0; }
            else geral.Value = lastGeral * 100;
        };
        pauseBox.AddChild(mute);
        // 13/11 (pedido do Barto: "opções padrões de jogo — alterar teclas"; "histórico numa tela maior")
        pauseBox.AddChild(new HSeparator());
        pauseBox.AddChild(HudView.MakeLabel("Opções", 16, new Color(0.95f, 0.78f, 0.30f)));
        var opcoes = new HBoxContainer();
        var bTeclas = new Button { Text = "Trocar teclas" };
        bTeclas.Pressed += () => { if (_menuTab.Visible) _menuTab.Ir("teclas"); else { MontarTeclas(); _teclasPanel.Visible = true; } };
        var bHist = new Button { Text = "Histórico (O)" };
        bHist.Pressed += () => AbrirHistorico(true);
        var bGraf = new Button { Text = "Gráficos: " + GraphicsQuality.Label(GraphicsQuality.Current) };
        bGraf.Pressed += () =>
        {
            var lv = GraphicsQuality.Cycle();
            GraphicsQuality.ApplyViewport(GetViewport());
            _mapView?.ApplyQuality();
            bGraf.Text = "Gráficos: " + GraphicsQuality.Label(lv);
        };
        opcoes.AddChild(bTeclas); opcoes.AddChild(bHist); opcoes.AddChild(bGraf);
        pauseBox.AddChild(opcoes);
        // 15/11 noite (dono: "coloca nas configs pra escolher o FPS"): limite de quadros + sincronia vertical
        var fpsRow = new HBoxContainer();
        fpsRow.AddThemeConstantOverride("separation", 10);
        fpsRow.AddChild(HudView.MakeLabel("Limite de FPS", 14));
        var fpsOpt = new OptionButton { CustomMinimumSize = new Vector2(130, 0), FocusMode = Control.FocusModeEnum.None };
        for (var i = 0; i < GraphicsQuality.FpsOpcoes.Length; i++)
        {
            fpsOpt.AddItem(GraphicsQuality.FpsLabel(GraphicsQuality.FpsOpcoes[i]), i);
            if (GraphicsQuality.FpsOpcoes[i] == GraphicsQuality.FpsLimite) fpsOpt.Selected = i;
        }
        var vs = new CheckBox { Text = "V-Sync (sem rasgos na tela)", ButtonPressed = GraphicsQuality.VSync, FocusMode = Control.FocusModeEnum.None };
        fpsOpt.ItemSelected += idx => GraphicsQuality.MudarFps(GraphicsQuality.FpsOpcoes[(int)idx], vs.ButtonPressed);
        vs.Toggled += on => GraphicsQuality.MudarFps(GraphicsQuality.FpsOpcoes[Math.Max(0, fpsOpt.Selected)], on);
        fpsRow.AddChild(fpsOpt); fpsRow.AddChild(vs);
        pauseBox.AddChild(fpsRow);
        pauseBox.AddChild(new HSeparator());
        var back = new Button { Text = "Trocar de personagem" };
        back.Pressed += BackToLobby;
        pauseBox.AddChild(back);
        _pausePanel.AddChild(pauseHost);
        layer.AddChild(_pausePanel);
        CriarOpcoes(layer);
        Arrastador.Ligar(_pausePanel, "menu_esc", 40f, fechar: () => { _pausePedido = false; _pausePanel.Visible = false; CaptureMouse(true); });
    }

    private static LineEdit AddField(VBoxContainer box, string title, bool secret)
    {
        box.AddChild(HudView.MakeLabel(title, 14));
        var edit = new LineEdit { CustomMinimumSize = new Vector2(340, 0), Secret = secret };
        box.AddChild(edit);
        return edit;
    }

    private void UpdateHud(long now, double delta)
    {
        var me = _model.Me;
        if (_model.Notice is { } notice)
        {
            _hud.ShowNotice(notice);
            _model.Notice = null;
        }
        if (me is not null)
        {
            var zone = _model.ZoneAt(me.X, me.Y);
            if (zone is not null && zone.Id != _zoneId)
            {
                _hud.ShowZone(zone.Name);
                _zoneId = zone.Id;
            }
        }

        string? prompt = null;
        _hud.PromptAncora = null;
        if (_decorColocando is not null)
            prompt = $"Colocando: {ClientModel.DisplayName(_decorColocando)} ({CasaDecor.Categoria(_decorColocando)})\nclique: colocar · Q/E ou rodinha: girar · Esc/botão direito: " + (_decorNova ? "guardar na mochila" : "deixar onde estava");
        else if (me is not null && me.Alive && DecorPerto() is { } dperto)
            prompt = "F — Mover: " + ClientModel.DisplayName(dperto.Id) + "   (editando · Ctrl+F sai)";
        else if (me is not null && me.Alive && _model.NearestInteractable() is { } near)
        {
            prompt = "F — " + near.Label;
            if (near.Kind == "item" && _model.Snapshot?.Items.FirstOrDefault(i => i.ItemId == near.Id) is { } noChao)
            {
                var w3 = new Vector3((float)noChao.X, Visuals.EscadaCasa.Altura(noChao.X, noChao.Y) + 0.4f, (float)noChao.Y);
                if (!_cam.Camera.IsPositionBehind(w3)) _hud.PromptAncora = _cam.Camera.UnprojectPosition(w3);
            }
            if (near.Kind == "item" && _model.Snapshot?.Items.FirstOrDefault(i => i.ItemId == near.Id) is { } item)
            {
                // 13/11 (2ª vez: "a prévia dos itens no chão ainda é um quadradão feio"): plaquinha no estilo MU — o nome na cor
                // da raridade, a tecla F ao lado, e no máximo 2 linhas curtas do que muda contra o equipado (sem o resumo inteiro)
                prompt = ClientModel.DisplayName(item.DefinitionId);
                var vqi = CinzasDaMare.Shared.Content.Variacao.Of(item.Quality);
                _hud.PromptCor = vqi > 0 ? ItemTooltip.VarColor(item.Quality)!.Value with { A = 1f } : item.Quality switch
                {
                    "excelente" => new Color(0.35f, 1f, 0.6f), "incomum" => new Color(0.5f, 0.95f, 0.45f), "raro" => new Color(0.45f, 0.72f, 1f),
                    "chefe" => new Color(1f, 0.6f, 0.25f), "lendario" => new Color(1f, 0.84f, 0.3f), _ => new Color(0.93f, 0.9f, 0.82f),
                };
                if (_model.ItemInfoFor(item.DefinitionId) is { Slot: not "consumable" } info)
                {
                    var forMe = info.Classes.Count == 0 || info.Classes.Contains(_model.ClassId);
                    if (!forMe) prompt += "\n(outra classe)";
                    else if (ItemTooltip.Compare(_model, info) is { Count: > 0 } diff) prompt += "\n" + string.Join("\n", diff.Take(2).Select(d => d.Text));
                }
            }
        }

        MonsterView? boss = null;
        if (me is not null && _model.Snapshot is not null)
            boss = _model.Snapshot.Monsters.FirstOrDefault(m => m.Boss && m.Alive && ClientModel.Distance(me.X, me.Y, m.X, m.Y) < 20);
        _bossPerto = boss is not null;
        var locked = LockedMonster();
        MonsterView? target = locked;
        if (target is null && me is not null)
        {
            var (fx, fy) = CameraForward();
            target = _model.BestMonsterInDirection(fx, fy, 14, 40);
        }
        _aimedId = target is { Alive: true } ? target.Id : null;
        // Pip, o vagalume-guia: agora é companheiro (slot na janela do personagem); só dá dicas quando está com você
        if (me is not null && me.Alive && _model.Companions?.ActivePet == "vagalume")
        {
            var fighting = _model.Snapshot?.Monsters.Any(mv => mv.Alive && ClientModel.Distance(me.X, me.Y, mv.X, mv.Y) < 12) ?? false;
            if (_guide.Tick(_model, _sessionTime, fighting) is { } hint)
            {
                _hud.ShowNotice(hint);
                _hud.Feed(hint, new Color(0.55f, 0.9f, 1f));
                Sfx.Play("ui_aviso", -12f);
            }
        }
        _hud.CameraYaw = _cam.Yaw;
        SonsDoLugar(_model.Me, (float)delta);
        BolinhaDoDescanso((float)delta);
        AuraDeCura((float)delta);   // 18/11: brilho verde enquanto a vida enche no círculo seguro
        DecorLoop();   // 18/11: escolher onde fica a decoração
        if (_updaterOn)
        {
            _updater.Process(delta);
            _updaterNoticeIn -= delta;
            if (_updater.Status.Length > 0 && (_updater.Status != _updaterShown && _updaterNoticeIn <= 0 || _updater.Done && _updaterShown != _updater.Status))
            {
                _updaterShown = _updater.Status;
                _updaterNoticeIn = 2.5;
                _hud.ShowNotice(_updater.Status);
                if (_updater.Done) _hud.Feed(_updater.Status);
            }
        }
        _hud.MiniMap.Model = _model;
        _hud.Update(_model, _cooldowns, now, target, prompt, boss, Input.MouseMode == Input.MouseModeEnum.Captured, delta,
            locked is not null, Math.Max(0, _dodgeReadyAt - now));
        _hud.UpdateFloaters(_cam.Camera, delta);

        if (locked is not null && _monsters.TryGetValue(locked.Id, out var ls))
        {
            _lockMarker.Visible = true;
            _lockMarker.Position = ls.Pos + new Vector3(0, ls.Visual.Height + 0.55f + 0.08f * Mathf.Sin((float)_time * 5f), 0);
            _lockMarker.Rotation = new Vector3(0, (float)_time * 3f, 0);
        }
        else _lockMarker.Visible = false;
    }

    // ------------------------------------------------------------ mundo

    private void EnsureWorld()
    {
        if (_model.Map is { } map)
        {
            // editor no jogo: o mesmo mapa mudou (árvore, monstro...) — monta de novo com a versão nova
            if (_mapView is not null && _mapView.Map.Id == map.Id && !(_forceMapRebuild && !ReferenceEquals(_mapView.Map, map))) return;
            // 16/11 (otimização item 3): atrás da tela de carregamento o mapa é montado em etapas (~40 ms por quadro), com
            // barra de progresso; a tela e a rede continuam vivas. Sem tela de carga (editor) monta de uma vez, como antes
            if (_mapMontando is { } mm0)
            {
                // mesmo mapa (o servidor pode reenviar o mesmo mapa): continua; se for versão nova do editor, o teste lá em
                // cima remonta quando terminar
                if (mm0.Map.Id == map.Id)
                {
                    if (!mm0.Avancar(40)) { MostrarProgressoCarga(mm0.Progresso); return; }
                    MostrarProgressoCarga(1f);
                    _mapMontando = null;
                    if (ReferenceEquals(mm0.Map, map)) _forceMapRebuild = false;
                    _mapView?.Root.QueueFree();
                    _arena?.Root.QueueFree();
                    _arena = null;
                    _mapView = mm0; _postalEm = 12;
                    _cargaAte = Time.GetTicksMsec() + 700;
                    GameLog.Info("cliente.mapa", $"mapa {map.Id} montado em {Time.GetTicksMsec() - _mapMontandoDesde} ms (em etapas, a tela não travou)");
                    LigarMapaNovo(map);
                    return;
                }
                mm0.Root.Free();   // o mapa mudou no meio da montagem: descarta e começa o novo
                _mapMontando = null;
            }
            // troca de mapa (portal, viagem) também ganha a tela de carga com barra — antes o jogo congelava uns segundos;
            // o editor (mesmo mapa montado de novo) continua instantâneo
            var trocaDeMapa = _mapView is null || _mapView.Map.Id != map.Id;
            if ((_carga is { Visible: true } || trocaDeMapa) && OS.GetEnvironment("KRYMOR_MAPA_DE_UMA_VEZ") != "1")
            {
                if (_carga is not { Visible: true }) MostrarCarga(map.DisplayName);
                _cargaTexto!.Text = map.DisplayName;
                _mapView?.Root.QueueFree();
                _mapView = null;
                _mapMontando = new MapView(map, aosPoucos: true);
                _mapMontandoDesde = Time.GetTicksMsec();
                MostrarProgressoCarga(0f);
                return;
            }
            _forceMapRebuild = false;
            _mapView?.Root.QueueFree();
            _arena?.Root.QueueFree();
            _arena = null;
            var buildStart = Time.GetTicksMsec();
            if (_carga is { Visible: true }) _cargaTexto!.Text = map.DisplayName;
            _mapView = new MapView(map); _postalEm = 12;
            _cargaAte = Time.GetTicksMsec() + 700;   // espera o primeiro quadro do mapa montado antes de tirar a tela
            // 11/11 (log para achar travadas): quanto custou montar o mapa
            GameLog.Info("cliente.mapa", $"mapa {map.Id} montado em {Time.GetTicksMsec() - buildStart} ms");
            LigarMapaNovo(map);
            return;
        }
        if (_mapMontando is { } mx) { mx.Root.Free(); _mapMontando = null; }   // saiu (sem mapa) no meio da montagem
        // servidor antigo (arena, sem mapa): cenário da E7 — só se o mapa não chegar (era o "3D mal feito" no começo)
        if (_carga is { Visible: true }) return;
        var half = (float)_model.ArenaHalfSize;
        if (_arena is not null && Math.Abs(_arenaHalf - half) < 0.001f) return;
        _arena?.Root.QueueFree();
        _arena = ArenaBuilder.Build(half);
        _arenaHalf = half;
        _world.AddChild(_arena.Root);
    }

    private MapView? _mapMontando;
    private ulong _mapMontandoDesde;
    private ProgressBar? _cargaBarra;

    /// <summary>16/11: barrinha dourada embaixo do nome do mapa enquanto ele é montado.</summary>
    private void MostrarProgressoCarga(float v)
    {
        if (_cargaRaiz is null) return;
        if (_cargaBarra is null)
        {
            _cargaBarra = new ProgressBar { ShowPercentage = false, MinValue = 0, MaxValue = 1, Step = 0.001, MouseFilter = Control.MouseFilterEnum.Ignore };
            _cargaBarra.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
            _cargaBarra.Position = new Vector2(-220, -70);
            _cargaBarra.Size = new Vector2(440, 6);
            var fundo = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.55f) };
            fundo.SetCornerRadiusAll(3);
            var cheio = new StyleBoxFlat { BgColor = new Color(0.98f, 0.8f, 0.42f) };
            cheio.SetCornerRadiusAll(3);
            _cargaBarra.AddThemeStyleboxOverride("background", fundo);
            _cargaBarra.AddThemeStyleboxOverride("fill", cheio);
            _cargaRaiz.AddChild(_cargaBarra);
        }
        _cargaBarra.Value = v;
        _cargaBarra.Visible = v < 1f;
    }

    private void LigarMapaNovo(CinzasDaMare.Shared.World.MapLayout map)
    {
        if (_mapView is null) return;
        {
            // 17/11 (dono: "todos os NPCs falam o mesmo quando chega perto — dá uma variada, uns 5 sons"): sorteia a saudação
            _mapView.OnGreet = (npc, at) => Sfx.Voice(SaudacoesNpc[Random.Shared.Next(SaudacoesNpc.Length)], NpcFemale(npc), NpcPitch(npc), at);
            _world.AddChild(_mapView.Root);
            if (_air is null) { _air = new AmbientParticles(); _world.AddChild(_air); }
            _air.SetTheme(map.Id == "fundo_triangulo" ? "submerso" : map.Theme, GraphicsQuality.Current != GraphicsQuality.Level.Baixo);
            _itemsQuietUntil = Time.GetTicksMsec() + 1500;   // itens que já estavam no chão não "caem" de novo
        }
    }

    private void AnimateArenaFlames()
    {
        var t = (float)_time;
        foreach (var (light, phase) in _arena!.Flames)
            light.LightEnergy = 2.6f + 0.45f * Mathf.Sin(t * 9f + phase) + 0.2f * Mathf.Sin(t * 23f + phase * 2f);
    }

    private string ModelOf(string? definitionId)
        => definitionId is null ? "" : _model.ItemInfoFor(definitionId)?.Model ?? definitionId;

    private readonly Dictionary<string, CartVisual> _carts = new();
    private Node3D? _xTesouro;

    /// <summary>Carroça da escolta: aparece/anda/some conforme os marcadores de missão vindos do servidor.</summary>
    private void SyncQuestMarkers()
    {
        // 17/11: X pintado no chão do mapa do tesouro
        if (_model.QuestMarkers.TryGetValue("tesouro", out var tz) && tz.Kind == "tesouro")
        {
            if (_xTesouro is null || !GodotObject.IsInstanceValid(_xTesouro))
            {
                _xTesouro = new Node3D { Name = "XTesouro" };
                var tinta = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.08f, 0.05f), Roughness = 1f };
                foreach (var ang in new[] { 45f, -45f })
                    _xTesouro.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2.6f, 0.04f, 0.45f), Material = tinta },
                        RotationDegrees = new Vector3(0, ang, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                _world.AddChild(_xTesouro);
            }
            _xTesouro.Position = new Vector3((float)tz.X, Relevo.Altura((float)tz.X, (float)tz.Y) + 0.05f, (float)tz.Y);
        }
        else if (_xTesouro is not null) { if (GodotObject.IsInstanceValid(_xTesouro)) _xTesouro.QueueFree(); _xTesouro = null; }
        foreach (var (qid, mk) in _model.QuestMarkers)
        {
            if (mk.Kind != "carroca") continue;
            if (!_carts.TryGetValue(qid, out var cart) || !GodotObject.IsInstanceValid(cart))
            {
                cart = new CartVisual();
                _world.AddChild(cart);
                _carts[qid] = cart;
            }
            cart.SetTarget(new Vector3((float)mk.X, 0, (float)mk.Y));
        }
        foreach (var qid in _carts.Keys.ToList())
            if (!_model.QuestMarkers.ContainsKey(qid))
            {
                if (GodotObject.IsInstanceValid(_carts[qid])) _carts[qid].QueueFree();
                _carts.Remove(qid);
            }
    }

    private void SyncWorld(float dt, long now)
    {
        if (_air is not null && MyState() is { } meAir) _air.Follow(meAir.Pos);
        SyncQuestMarkers();
        var snapshot = _model.Snapshot;
        if (snapshot is null) return;
        _curSnapTick = snapshot.Tick;
        _hud.BeginPlates();

        // ---- jogadores ----
        var seen = new HashSet<string>();
        foreach (var p in snapshot.Players)
        {
            seen.Add(p.CharacterId);
            var isMe = p.CharacterId == _model.CharacterId;
            var formKey = p.Form ?? "";
            if (!_players.TryGetValue(p.CharacterId, out var st) || st.ClassId != p.ClassId || st.FormKey != formKey)
            {
                var hadForm = st is not null && st.FormKey != formKey;
                st?.Visual.Dispose();
                // 12/11: transformado, o boneco vira a fera do bestiário (Pandemônio: o próprio boneco, verde e de máscara)
                // 13/11: as 4 formas agora são roupas 3D sobre o NOSSO boneco (pacote do trabalhador); sem o arquivo, a fera antiga
                IEntityVisual? forma3d = null;
                if (formKey is "forma_lobisomem" or "forma_vampiro" or "forma_ceifador" or "forma_anjo"
                    && VisualFactory.Player(p.ClassId, isMe) is MannequinVisual fm)
                {
                    if (fm.SetForma(formKey)) forma3d = fm; else fm.Dispose();
                }
                var visual = forma3d ?? formKey switch
                {
                    "forma_lobisomem" => VisualFactory.Monster("monster_bst_lobisomem", false, false, "werewolf"),
                    "forma_vampiro" => VisualFactory.Monster("monster_bst_vampiro", false, false, "vampire"),
                    "forma_ceifador" => VisualFactory.Monster("monster_bst_a_morte", false, false, "death"),
                    "forma_anjo" => VisualFactory.Monster("monster_bst_anjo", false, false, "angel"),
                    _ => VisualFactory.Player(p.ClassId, isMe),
                };
                if (formKey == "forma_pandemonio" && visual is MannequinVisual mv) mv.SetPandemonio(true);
                st = new EntityState { Visual = visual, ClassId = p.ClassId, FormKey = formKey };
                _world.AddChild(st.Visual.Root);
                _players[p.CharacterId] = st;
                if (hadForm && formKey == "forma_pandemonio")
                {
                    // 12/11: animação da máscara — redemoinho verde subindo, clarão e o corpo "estourando" para o tamanho da fera
                    StartWhirl(st, 0.9f, 1.6f, new Color(0.45f, 1f, 0.35f));
                    _effects.Pulse(new Vector3((float)p.X, 0.1f, (float)p.Y), new Color(0.4f, 1f, 0.3f, 0.9f), 3.4f);
                    _effects.Impact(new Vector3((float)p.X, 1.4f, (float)p.Y), new Color(0.5f, 1f, 0.3f), 36, 2f);
                    st.Visual.Root.Scale = Vector3.One * 0.55f;
                    var tw = st.Visual.Root.CreateTween();
                    tw.TweenProperty(st.Visual.Root, "scale", Vector3.One * 1.12f, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                    tw.TweenProperty(st.Visual.Root, "scale", Vector3.One, 0.25);
                    Sfx.Play("rugido", -2f);
                    if (isMe) _cam.AddShake(0.35f);
                }
                else if (hadForm)
                {
                    _effects.Pulse(new Vector3((float)p.X, 0.1f, (float)p.Y), formKey.Length > 0 ? new Color(0.6f, 0.2f, 1f, 0.8f) : new Color(1f, 1f, 1f, 0.6f), 2.6f);
                    _effects.Impact(new Vector3((float)p.X, 1.2f, (float)p.Y), new Color(0.7f, 0.4f, 1f), 24, 1.6f);
                    Sfx.Play(formKey.Length > 0 ? "rugido" : "ui_aviso", -4f);
                }
            }
            var gear = $"{p.Weapon}|{p.Offhand}|{p.Body}|{p.Head}|{p.Gloves}|{p.Boots}|{p.Pants}|{p.GearVar}|{p.Skin}";
            if (gear != st.Gear)
            {
                st.Gear = gear;
                // 13/11: com skin no bracelete, a aparência inteira é a da skin (a armadura continua valendo, mas não aparece)
                var semSkin = string.IsNullOrEmpty(p.Skin);
                string? Peca(string? id) => semSkin && id is not null ? ModelOf(id) : null;
                st.Visual.SetGear(p.Weapon is null ? null : ModelOf(p.Weapon), p.Offhand is null ? null : ModelOf(p.Offhand), Peca(p.Body));
                if (st.Visual is IPlayerVisual headed)
                {
                    headed.SetHead(Peca(p.Head));
                    headed.SetLimbs(Peca(p.Gloves), Peca(p.Boots));
                    if (st.Visual is MannequinVisual pantsVisual) pantsVisual.SetPants(Peca(p.Pants));
                    if (st.Visual is MannequinVisual glowVisual) glowVisual.SetArmorGlow(p.GearVar);   // 12/11: mais dourado quanto melhor a variação
                }
            }
            var faceX = p.FacingX;
            var faceY = p.FacingY;
            // 13/11 ("sentar tem que vir de costas pro banco, de qualquer lado que eu chegue"): sentado vale a direção do banco
            if (isMe && _desiredFace is { } d && !p.Action.StartsWith("windup", StringComparison.Ordinal) && p.Action != "dodge" && p.Action != "sit" && p.Action != "deitado")
            {
                faceX = d.X;
                faceY = d.Y;
            }
            if (isMe && LabOwnsHero)
            {
                // o protótipo move e anima o próprio boneco; aqui só acompanha a posição (câmera, mapa, sons)
                _labOwnedLast = true;
                st.PrevPos = st.Pos;
                st.Pos = new Vector3(_lab!.HeroPosition.X, 0, _lab.HeroPosition.Z);
                continue;
            }
            if (isMe && _labOwnedLast)
            {
                // desligou: volta NA HORA para onde o servidor diz (sem deslizar)
                _labOwnedLast = false;
                st.Pos = st.PrevPos = new Vector3((float)p.X, 0, (float)p.Y);
            }
            Advance(st, p.X, p.Y, faceX, faceY, dt, isMe ? 22f : 14f);
            // morreu: cai de joelhos e, ~1,3 s depois, vira cinza que o vento leva ("estalo do Thanos"); não afunda no chão
            var prevDead = st.DeadFor;
            st.DeadFor = p.Alive ? 0f : st.DeadFor + dt;
            // renasceu: a câmera vira para onde o personagem olha (para dentro do mapa), não para o muro do portão
            if (isMe && p.Alive && prevDead > 0f) _cam.Yaw = Mathf.Atan2(-(float)p.FacingX, -(float)p.FacingY);
            if (!p.Alive && prevDead < PlayerDustAt && st.DeadFor >= PlayerDustAt)
                _effects.Disintegrate(st.Pos, 1.7f, 0.7f, WindDir(), true);
            var dustP = p.Alive ? 0f : Mathf.Clamp((st.DeadFor - PlayerDustAt) / 1.1f, 0f, 1f);
            SetFade(st, dustP);
            if (!p.Alive) st.Visual.Root.Visible = dustP < 1f;
            else if (!isMe) st.Visual.Root.Visible = true;
            if (p.Action != st.LastAction || p.ActionSeq != st.LastSeq) OnPlayerActionChanged(st, p);
            var frame = new VisualFrame
            {
                Speed = st.Speed, Action = p.Action, ActionT = (float)p.ActionT, ActionSeq = p.ActionSeq,
                Alive = p.Alive, Guarding = p.Guarding,
            };
            if (st.Visual is IPlayerVisual hv)
            {
                var vel = st.Pos - st.PrevPos;
                hv.MoveDirLocal = vel.LengthSquared() > 1e-6f ? (st.Visual.Root.Basis.Inverse() * vel).Normalized() : Vector3.Back;
            }
            if (st.Visual is IPlayerVisual hum)
            {
                // montaria: corcel sob o boneco (o boneco sobe para a sela); asas nas costas; brilho do lendário
                if (p.Mount != st.MountKind)
                {
                    st.MountKind = p.Mount;
                    st.Mount?.Dispose();
                    st.Mount = null;
                    if (p.Mount is not null)
                    {
                        st.Mount = VisualFactory.Mount(p.Mount);
                        st.MountRise = VisualFactory.AlturaSela(p.Mount);
                        hum.Root.AddChild(st.Mount.Root);
                        st.Mount.Root.Position = new Vector3(0, -st.MountRise, -0.1f);
                        if (isMe) Sfx.Play("montar");
                    }
                    hum.SetMounted(p.Mount is not null);
                }
                if (st.Mount is not null)
                {
                    hum.Root.Position += new Vector3(0, st.MountRise, 0);
                    // 13/11 ("esquiva montado: eu saio do bicho e escorrego — faz a criatura dar o bote para a frente"): na
                    // esquiva o bicho galopa mais rápido e se inclina num salto curto; o cavaleiro acompanha
                    var bote = p.Action == "dodge" ? Mathf.Sin(Mathf.Clamp((float)p.ActionT, 0f, 1f) * Mathf.Pi) : 0f;
                    st.Mount.Root.Rotation = new Vector3(-0.18f * bote, 0, 0);
                    hum.Root.Position += new Vector3(0, 0.35f * bote, 0);   // o bicho é filho do boneco: sobe junto
                    st.Mount.Update(new VisualFrame { Speed = st.Speed * (1f + bote), Alive = true, Action = "" }, dt);
                }
                hum.SetWings(p.Wings);
                if (hum is MannequinVisual flyer) flyer.Flying = p.Flying;
                SetCarry(st, p.Carry);
                Espadas(st, p.Status);   // 18/11 19h: Espadas Voadoras girando em volta
                hum.SetLegendary(p.Legendary);
                if (hum is MannequinVisual glowing) { glowing.SetWeaponGlow(p.WeaponPlus, p.WeaponExcellent); glowing.SetAura(p.SetCompleto); glowing.SetSkin(p.Skin); }
                if (hum is MannequinVisual evoMan) { evoMan.SetEvolved(p.Evolved); evoMan.SetClasse3(p.Class3); }   // troca de classe: cara de leão/corvo/serpente/gato; 3ª: leão
            }
            if (st.LastLevel > 0 && p.Level > st.LastLevel) LevelUpAura(st, p.Level, isMe);
            st.LastLevel = p.Level;
            if (st.Visual is MannequinVisual man) man.SetStealth(p.Stealth, isMe);
            if (p.Stealth != st.Stealth)
            {
                st.Stealth = p.Stealth;
                _effects.Pulse(st.Pos, new Color(0.25f, 0.3f, 0.25f, 0.6f), 1.4f, 0.5);
                Sfx.PlayAt("furtivo", st.Pos + Vector3.Up, isMe ? 0f : -6f);
            }
            StatusFx(st, p.Shield > 0 && p.Alive, false, false, dt);
            if (p.Alive && IsNear(st.Pos, 35f)) StatusGotas(st, p.Status ?? "", dt);
            st.Visual.Update(frame, dt);
            // interação com o chão: poeira/areia/neve levantando a cada passo (mais forte correndo)
            if (p.Alive && st.Speed > 1.2f && IsNear(st.Pos, 30f))
            {
                st.DustT -= dt;
                if (st.DustT <= 0f)
                {
                    st.DustT = st.Speed > 3.4f ? 0.16f : 0.42f;
                    _effects.Dust(BehindFeet(st), GroundDust(), st.Speed > 3.4f ? 1.6f : 0.6f);
                    if (isMe)
                    {
                        Sfx.PlayAt(st.Ground > 0.2f && Visuals.EscadaCasa.NoAssoalho(st.Pos.X, st.Pos.Z) ? "passo_madeira" : StepSound(), st.Pos, st.Speed > 3.4f ? -10f : -14f, 0.12f);
                        // armadura de metal tilinta a cada passo (couro e pano, não)
                        if (_model.Equipment.Any(e => e.Slot == "body" && (e.DefinitionId.Contains("cota") || e.DefinitionId.Contains("placa") || e.DefinitionId.Contains("malha") || e.DefinitionId.Contains("guerreiro_"))))
                            Sfx.PlayAt("passo_metal", st.Pos + Vector3.Up, -16f, 0.15f);
                    }
                }
            }
            if (!st.WasAlive && p.Alive) _effects.Pulse(st.Pos, new Color(1f, 1f, 1f, 0.5f), 1.6f);
            st.WasAlive = p.Alive;

            // marca azul da área que o Mago está preparando (aliados também veem)
            if (p.Action.StartsWith("windup:skill_mago_explosao", StringComparison.Ordinal)
                || p.Action.StartsWith("windup:skill_batedor_chuva", StringComparison.Ordinal))
            {
                st.Telegraph ??= CreateTelegraph();
                st.Telegraph.Show(new Vector3((float)p.AimX, 0, (float)p.AimY), 0, (float)(p.AimRadius > 0 ? p.AimRadius : 2.8), 0, (float)p.ActionT, false);
            }
            else st.Telegraph?.Hide();

            if (!isMe)
                _hud.UpdatePlate("p:" + p.CharacterId, (_model.InParty(p.CharacterId) ? "◆ " : "") + $"{p.Name}  Nv {p.Level}" + (p.Title is { Length: > 0 } t ? $"\n«{t}»" : ""),
                    p.MaxHealth > 0 ? p.Health / p.MaxHealth : 0, true,
                    _model.InParty(p.CharacterId) ? new Color(0.35f, 0.60f, 1f) : new Color(0.30f, 0.80f, 0.40f),
                    st.Pos + new Vector3(0, st.Visual.Height + 0.25f, 0), _cam.Camera, p.Alive);
        }
        Prune(_players, seen);

        // ---- monstros ----
        seen.Clear();
        var meStream = MyState()?.Pos;
        var novosMonstros = 0;
        foreach (var m in snapshot.Monsters)
        {
            // streaming (técnica de jogo grande): só existe na cena o monstro a até ~75 m; longe, o boneco é desmontado
            // (o servidor manda o mapa inteiro — 140+ monstros e ~20 mil nós deixavam o jogo a 55 FPS numa RTX 4090)
            if (!m.Boss && meStream is { } msp
                && msp.DistanceTo(new Vector3((float)m.X, 0, (float)m.Y)) > (_monsters.ContainsKey(m.Id) ? 95f : 75f))
            {
                _hud.UpdatePlate("m:" + m.Id, "", 0, false, new Color(0.78f, 0.15f, 0.12f), Vector3.Zero, _cam.Camera, false);
                continue;   // fora de "seen": Prune desmonta
            }
            // 16/11 madrugada (lista de otimização, item 2 — "pico ao nascer/chegar no mapa"): no máximo 6 bonecos NOVOS de
            // monstro por retrato; os de perto (25 m) e chefes entram na hora, o resto aparece nos retratos seguintes
            // (~meio segundo) em vez de montar 100+ bonecos no mesmo quadro
            if (!_monsters.ContainsKey(m.Id) && !m.Boss && novosMonstros >= 6 && meStream is { } msn
                && msn.DistanceTo(new Vector3((float)m.X, 0, (float)m.Y)) > 25f)
            {
                _hud.UpdatePlate("m:" + m.Id, "", 0, false, new Color(0.78f, 0.15f, 0.12f), Vector3.Zero, _cam.Camera, false);
                continue;
            }
            seen.Add(m.Id);
            if (!_monsters.TryGetValue(m.Id, out var st))
            {
                novosMonstros++;
                // 14/11: um visual que quebra não pode travar o mundo inteiro (u81: personagem e monstros sumiram) — vira o genérico
                IEntityVisual vis;
                try { vis = VisualFactory.Monster(m.DefinitionId, m.Boss, m.Rare, m.Visual, m.Id); }
                catch (Exception e) { GameLog.Warn("cliente.visual", $"visual de {m.DefinitionId} falhou ({e.Message}); usando o genérico"); vis = VisualFactory.Monster(m.DefinitionId, m.Boss, m.Rare, "", m.Id); }
                st = new EntityState { Visual = vis, Telegraph = CreateTelegraph(), Def = m.DefinitionId + "|" + m.Visual };
                if (m.DefinitionId.EndsWith("_dourado", StringComparison.Ordinal)) Dourar(st.Visual.Root);   // 13/11: versão dourada (rara)
                st.Rim = MonsterStandOut(st.Visual.Root, m.Boss || m.Elite || m.Rare,
                    rimOn: !(m.Visual.Contains("rat") || m.Visual.Contains("rato") || m.DefinitionId.Contains("rato")));
                _world.AddChild(st.Visual.Root);
                _monsters[m.Id] = st;
            }
            if (!st.WasAlive && m.Alive) { st.Placed = false; _effects.Spawn(new Vector3((float)m.X, 0, (float)m.Y), m.Boss ? new Color(1f, 0.45f, 0.2f) : new Color(0.75f, 0.85f, 1f)); }
            SleepMark(st, m.Alive && m.Action == "sleep" && !m.DefinitionId.Contains("boneco"));   // boneco não dorme (nem desenhado)
            st.WasAlive = m.Alive;
            Advance(st, m.X, m.Y, m.FacingX, m.FacingY, dt, 14f);
            // 18/11: no golpe (pulo) o corpo segue o arco do servidor sem atraso
            st.Lift = m.Action == "dive" || m.Action.StartsWith("active:", StringComparison.Ordinal) ? (float)m.Lift : Mathf.Lerp(st.Lift, (float)m.Lift, 1f - Mathf.Exp(-10f * dt));
            if (st.Lift > 0.002f) st.Visual.Root.Position += new Vector3(0, st.Lift, 0);
            // 11/11 ("o fantasma de cal anda voando, tipo quando a gente está de asa"): flutua acima do chão, balançando
            if (st.Visual is MannequinVisual ghostMv && (m.Visual == "fantasma_cal" || m.DefinitionId.Contains("fantasma"))) ghostMv.Hover = m.Alive;
            if (m.Alive && (m.Visual == "fantasma_cal" || m.DefinitionId.Contains("fantasma")))
                st.Visual.Root.Position += new Vector3(0, 0.55f + 0.18f * Mathf.Sin((float)_time * 1.6f + (m.Id.GetHashCode() & 31)), 0);
            // 11/11 noite ("dormindo eles só ficam parados"): dormindo, o corpo sobe e desce devagar (respiração)
            var breath = m.Alive && m.Action == "sleep" ? 1f + 0.035f * Mathf.Sin((float)_time * 2.1f + (m.Id.GetHashCode() & 63)) : 1f;
            var wantScale = new Vector3((float)m.Size, (float)m.Size * breath, (float)m.Size);
            if ((st.Visual.Root.Scale - wantScale).LengthSquared() > 1e-7f) st.Visual.Root.Scale = wantScale;   // variante maior pelo nível
            // longe do jogador (fora da névoa): não desenha nem anima — o servidor manda o mapa inteiro (80+ monstros)
            // e animar todos derrubava o FPS para 60 numa RTX 4090 ("gameplay travada")
            var far = !m.Boss && MyState() is { } meNow && meNow.Pos.DistanceTo(st.Pos) > 60f;
            MarkTarget(st, m.Alive && m.Id == _aimedId);
            // morto: cai e, 1 s depois, se desfaz em cinza como o jogador (pedido: "todos os monstros devem sumir")
            var prevDeadM = st.DeadFor;
            st.DeadFor = m.Alive ? 0f : st.DeadFor + dt;
            if (!m.Alive && !far && prevDeadM < MonsterDustAt && st.DeadFor >= MonsterDustAt)
                _effects.Disintegrate(st.Pos, st.Visual.Height * (float)m.Size * 0.8f, Mathf.Clamp(st.Visual.Height * (float)m.Size, 0.5f, 3f), WindDir(), m.Boss || st.Visual.Height * m.Size > 1.6);
            var sink = m.Alive ? 0f : Mathf.Clamp((st.DeadFor - MonsterDustAt) / 0.9f, 0f, 1f);
            SetFade(st, sink);
            st.Visual.Root.Visible = !far && sink < 1f;
            if (far)
            {
                st.Telegraph!.Hide();
                _hud.UpdatePlate("m:" + m.Id, "", 0, false, new Color(0.78f, 0.15f, 0.12f), st.Pos, _cam.Camera, false);
                continue;
            }
            // 13/11 (desempenho): sombra só dos monstros perto — longe, cada um era desenhado mais 4 vezes (faixas da sombra)
            var sombra = m.Boss || (MyState() is { } meS && meS.Pos.DistanceTo(st.Pos) < 26f);
            if (st.Sombra != sombra) { st.Sombra = sombra; SetShadows(st.Visual.Root, sombra); }
            var windup = m.Action.StartsWith("windup", StringComparison.Ordinal);
            var active = m.Action.StartsWith("active", StringComparison.Ordinal);
            // 16/11: tática de bando (rugido, guarda, conjurar cura) — pose de preparo + voz, sem marca de golpe no chão
            var tatica = m.Action.StartsWith("tatica:", StringComparison.Ordinal);
            if (m.Action != st.LastAction)
            {
                if ((windup || tatica) && m.Alive && IsNear(st.Pos, 22f) && CreatureVoice(m) is { } voice)
                    Sfx.PlayAt(voice, st.Pos + Vector3.Up, m.Boss ? 2f : -5f, 0.12f);
                if (m.Boss && m.Action == "active:pancada" && MyState() is { } mine && mine.Pos.DistanceTo(st.Pos) < 14f)
                {
                    _cam.AddShake(0.45f);
                    _effects.Impact(new Vector3((float)m.AimX, 0.3f, (float)m.AimY), new Color(1f, 0.5f, 0.2f), 24, 2.4f);
                    _effects.Pulse(new Vector3((float)m.AimX, 0, (float)m.AimY), new Color(1f, 0.5f, 0.2f, 0.7f), (float)m.AimRadius + 0.5f, 0.5);
                }
                st.LastAction = m.Action;
            }
            StatusFx(st, false, m.Weakened && m.Alive, m.Marked && m.Alive, dt);
            // 12/11: lento pela Estaca de Gelo — cristaizinhos de gelo saindo do corpo
            if (m.Chilled && m.Alive && IsNear(st.Pos, 30f))
            {
                st.ChillT -= dt;
                if (st.ChillT <= 0f) { st.ChillT = 0.3f; _effects.Impact(st.Pos + new Vector3(0, st.Visual.Height * 0.5f, 0), new Color(0.6f, 0.88f, 1f), 4, 0.7f); }
            }
            st.Visual.Update(new VisualFrame
            {
                Speed = st.Speed, Action = tatica ? "windup:" + m.Action[7..] : m.Action, ActionT = (float)m.ActionT, ActionSeq = m.ActionSeq, Alive = m.Alive,
                WindupGlow = windup ? 0.15f + 0.85f * (float)m.ActionT : active ? 1f : 0f,
            }, dt);
            // 10/11: monstro avançando também levanta poeira (os grandes, mais)
            if (m.Alive && st.Speed > 1.5f && IsNear(st.Pos, 26f))
            {
                st.DustT -= dt;
                if (st.DustT <= 0f)
                {
                    st.DustT = st.Speed > 3.2f ? 0.18f : 0.4f;
                    _effects.Dust(BehindFeet(st), GroundDust(), (st.Speed > 3.2f ? 1.5f : 0.6f) * (m.Boss ? 1.4f : 1f));
                }
            }
            if (m.Alive && (windup || active) && m.AimRadius > 0)
                st.Telegraph!.Show(new Vector3((float)m.AimX, 0, (float)m.AimY), Mathf.Atan2((float)m.FacingX, (float)m.FacingY),
                    (float)m.AimRadius, (float)m.AimArc, (float)m.ActionT, active, m.AimKind);
            else st.Telegraph!.Hide();
            // 18/11: unhada em X — uma garra de luz por risco (o primeiro "\", o segundo "/")
            if (m.Alive && active && m.AimKind == "xis")
            {
                var fase = m.ActionT < 0.45 ? 1 : 2;
                if (st.XisFase < fase)
                {
                    st.XisFase = fase;
                    var centro = new Vector3((float)m.AimX, st.Pos.Y, (float)m.AimY);
                    _effects.Garras(centro, Mathf.Atan2((float)m.FacingX, (float)m.FacingY), (float)m.AimRadius, fase == 1 ? 1 : -1, new Color(0.75f, 0.45f, 1f, 0.9f));
                    Sfx.PlayAt("golpe_pesado", centro, -6f);
                }
            }
            else st.XisFase = 0;
            UpdateBeam(st, m, windup, active);
            UpdateFire(st, m, windup, active);
            CrabFx(st, m);

            // 2ª fase do chefe: luz vermelha pulsando no corpo e um estouro ao mudar
            if (m.Enraged && !st.WasEnraged)
            {
                _effects.Pulse(new Vector3((float)m.X, 0, (float)m.Y), new Color(1f, 0.2f, 0.1f, 0.7f), 4.5f, 0.6);
                if (IsNear(st.Pos, 18f)) _cam.AddShake(0.35f);
                st.Rage ??= new OmniLight3D { LightColor = new Color(1f, 0.22f, 0.12f), OmniRange = 5f, Position = new Vector3(0, 1.2f, 0) };
                if (st.Rage.GetParent() is null) st.Visual.Root.AddChild(st.Rage);
            }
            st.WasEnraged = m.Enraged;
            // 14/11 tarde (dono: "INVASÃO DOURADA — monstros dourados fortes invadem a cidade"): corpo de ouro + brilho
            if (m.Affix == "dourado" && !st.Dourado)
            {
                st.Dourado = true;
                var ouro = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.78f, 0.25f, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    Metallic = 1f, Roughness = 0.25f, EmissionEnabled = true, Emission = new Color(0.9f, 0.6f, 0.12f), EmissionEnergyMultiplier = 0.45f,
                    BlendMode = BaseMaterial3D.BlendModeEnum.Add };
                void Pintar(Node n) { if (n is GeometryInstance3D g) g.MaterialOverlay = ouro; foreach (var c in n.GetChildren()) Pintar(c); }
                Pintar(st.Visual.Root);
                st.Visual.Root.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.35f), OmniRange = 4f, LightEnergy = 1.4f, Position = new Vector3(0, 1.2f, 0) });
            }
            if (st.Rage is not null)
            {
                st.Rage.Visible = m.Enraged && m.Alive;
                st.Rage.LightEnergy = 1.6f + 0.9f * Mathf.Sin((float)_time * 7f);
            }

            var exposed = m.Alive && m.Action.StartsWith("recover", StringComparison.Ordinal);
            var affix = m.Affix switch { "rapido" => "Rápido · ", "vampiro" => "Vampírico · ", "blindado" => "Blindado · ", "furioso" => "Feroz · ", "dourado" => "✦ Dourado · ", "explosivo" => "Explosivo · ", _ => "" };
            // 13/11 (pedido do Barto: "áreas de risco — caveira nos monstros fortes demais pra você"): 6+ níveis acima = ☠
            var meuNivel = _model.Progress?.Level ?? _model.Me?.Level ?? 1;
            var caveira = m.Level >= meuNivel + 6;
            // 16/11: temperamento em ação (táticas de bando)
            var humor = m.Humor switch { "frenesi" => "FRENESI · ", "fugindo" => "Fugindo · ", "guarda" => "De guarda · ", "furia" => "Furioso · ", "cura" => "Curando · ", _ => "" };
            var label = (caveira ? "☠ " : "") + (humor.Length > 0 ? humor : m.Enraged ? "Furioso · " : "") + (m.Rare ? "★ " : "") + (m.Elite ? "Elite · " : "") + affix + m.SpeciesName + (m.Level > 0 ? $"  Nv {m.Level}" : "") + (exposed ? "  · brecha!" : "");
            // 13/11 ("o boneco de treino não precisa mostrar a vida, só o dano que tira"): nome e nível, sem barra
            var boneco = m.DefinitionId == "monster_boneco_treino";
            if (boneco) label = $"Boneco de treino  Nv {m.Level}";
            if (!m.Boss)
                _hud.UpdatePlate("m:" + m.Id, label, m.MaxHealth > 0 ? m.Health / m.MaxHealth : 0, !boneco,
                    exposed ? new Color(0.95f, 0.75f, 0.2f) : caveira ? new Color(0.45f, 0.1f, 0.55f) : new Color(0.78f, 0.15f, 0.12f),
                    st.Pos + new Vector3(0, st.Visual.Height + 0.25f, 0), _cam.Camera,
                    // tela limpa (feedback "mundo confuso"): nome só do alvo, de quem já apanhou, de elite/raro ou bem perto
                    m.Alive && IsNear(st.Pos, 24f) && (!_bossPerto || m.Id == _lockedId) && (m.Id == _lockedId || m.Health < m.MaxHealth - 0.5 || m.Elite || m.Rare
                                                        || m.Affix.Length > 0 || IsNear(st.Pos, 8f) || caveira && IsNear(st.Pos, 18f)));
        }
        Prune(_monsters, seen);

        // ---- itens no chão ----
        var seenItems = new HashSet<string>();
        foreach (var item in snapshot.Items)
        {
            seenItems.Add(item.ItemId);
            if (!_items.TryGetValue(item.ItemId, out var st))
            {
                st = CreateItem(item);
                _items[item.ItemId] = st;
                // ritual do drop: item novo salta do chão com som pela raridade (só o que caiu agora, perto)
                if (Time.GetTicksMsec() > _itemsQuietUntil && IsNear(st.Root.Position, 30f))
                {
                    st.Pop = 0f;
                    Sfx.PlayAt(item.Quality switch { "lendario" => "drop_lendario", "raro" or "chefe" => "drop_raro", _ => "drop_comum" },
                        st.Root.Position + Vector3.Up * 0.5f, item.Quality == "comum" ? -6f : 0f);
                }
            }
            st.Phase += dt;
            st.Pop = Mathf.Min(1f, st.Pop + dt / 0.55f);
            var hop = st.Pop < 1f ? Mathf.Sin(st.Pop * Mathf.Pi) * 1.3f : 0f;
            st.Model.Position = new Vector3(0, 0.25f + hop + 0.1f * Mathf.Sin(st.Phase * 2.2f), 0);
            st.Model.Rotation = new Vector3(st.Pop < 1f ? st.Pop * Mathf.Tau * 1.5f : 0f, st.Phase * 1.3f, 0);
            var color = ItemTooltip.VarColor(item.Quality) ?? item.Quality switch { "excelente" => new Color(0.25f, 1f, 0.6f), "incomum" => new Color(0.45f, 0.92f, 0.45f), "raro" => new Color(0.45f, 0.75f, 1f), "chefe" => new Color(1f, 0.6f, 0.25f), "lendario" => new Color(1f, 0.85f, 0.25f), _ => new Color(0.86f, 0.84f, 0.78f) };
            _hud.UpdatePlate("i:" + item.ItemId, ClientModel.DisplayName(item.DefinitionId), 1.0, false, color,
                st.Root.Position + new Vector3(0, 1.2f, 0), _cam.Camera,
                IsNear(st.Root.Position, item.Quality is "comum" or "incomum" ? 6f : 14f));
        }
        foreach (var id in _items.Keys.Where(k => !seenItems.Contains(k)).ToList())
        {
            _items[id].Root.QueueFree();
            _items.Remove(id);
        }

        // ---- projéteis (posição extrapolada entre snapshots) ----
        var seenProjectiles = new HashSet<string>();
        foreach (var p in snapshot.Projectiles ?? Array.Empty<ProjectileView>())
        {
            seenProjectiles.Add(p.Id);
            if (!_projectiles.TryGetValue(p.Id, out var ps))
            {
                ps = new ProjectileState { Root = CreateProjectile(p) };
                _world.AddChild(ps.Root);
                _projectiles[p.Id] = ps;
            }
            if (!ReferenceEquals(ps.View, p)) { ps.View = p; ps.ReceivedAt = _time; }
            var ahead = (float)((_time - ps.ReceivedAt) * p.Speed);
            // 13/11 ("os arremessos entram no chão — tem que acompanhar o solo como os personagens e a linha de ataque")
            var px = p.X + p.DirX * ahead; var py = p.Y + p.DirY * ahead;
            var chao = Visuals.EscadaCasa.ElevBase(px, py, 0.8);
            ps.Ground = ps.Ground == 0f ? chao : Mathf.Lerp(ps.Ground, chao, 1f - Mathf.Exp(-12f * dt));
            ps.Root.Position = new Vector3((float)px, 1.2f + ps.Ground, (float)py);
        }
        foreach (var id in _projectiles.Keys.Where(k => !seenProjectiles.Contains(k)).ToList())
        {
            var gone = _projectiles[id];
            _effects.Impact(gone.Root.Position, gone.View.Hostile ? new Color(1f, 0.45f, 0.15f) : new Color(1f, 0.7f, 0.3f), 6, 0.8f);
            gone.Root.QueueFree();
            _projectiles.Remove(id);
        }

        // ---- companheiros (pequeno draco, fênix) ----
        var seenPets = new HashSet<string>();
        foreach (var pet in snapshot.Pets ?? Array.Empty<PetView>())
        {
            seenPets.Add(pet.OwnerId);
            if (!_pets.TryGetValue(pet.OwnerId, out var ps) || ps.Kind != pet.Kind)
            {
                ps?.Visual.Dispose();
                ps = new PetState { Visual = VisualFactory.Pet(pet.Kind, pet.OwnerId), Kind = pet.Kind, Pos = new Vector3((float)pet.X, 0, (float)pet.Y) };
                _world.AddChild(ps.Visual.Root);
                _pets[pet.OwnerId] = ps;
            }
            var target = new Vector3((float)pet.X, 0, (float)pet.Y);
            // 15/11 (dono: "a movimentação dos pets"): a velocidade era a DISTÂNCIA até o alvo ÷ dt — o atraso normal do
            // seguir virava 10–20 m/s, então o pet ficava "correndo" parado e patinava. Agora: deslocamento real do quadro,
            // suavizado; andando, olha para onde anda (o virar do servidor só vale parado ou atacando).
            var antes = ps.Pos;
            if (ps.Pos.DistanceTo(target) > 8f) ps.Pos = target;
            else ps.Pos = ps.Pos.Lerp(target, 1f - Mathf.Exp(-9f * dt));
            var desl = ps.Pos - antes;
            ps.Speed = Mathf.Lerp(ps.Speed, desl.Length() / Mathf.Max(dt, 0.001f), 1f - Mathf.Exp(-8f * dt));
            var atacando = !string.IsNullOrEmpty(pet.Action) && pet.Action != "chase" && pet.Action != "idle";
            var yawAlvo = ps.Speed > 0.5f && !atacando && desl.LengthSquared() > 1e-8f
                ? Mathf.Atan2(desl.X, desl.Z) : Mathf.Atan2((float)pet.FacingX, (float)pet.FacingY);
            ps.Yaw = Mathf.LerpAngle(ps.Yaw, yawAlvo, 1f - Mathf.Exp(-8f * dt));
            ps.Visual.Root.Position = ps.Pos;
            ps.Visual.Root.Rotation = new Vector3(0, ps.Yaw, 0);
            ps.Visual.Root.Visible = !pet.Down;
            ps.Visual.Update(new VisualFrame { Speed = ps.Speed < 0.15f ? 0f : ps.Speed, Alive = true, Action = pet.Action, ActionT = (float)pet.ActionT }, dt);
        }
        foreach (var id in _pets.Keys.Where(k => !seenPets.Contains(k)).ToList())
        {
            _pets[id].Visual.Dispose();
            _pets.Remove(id);
        }

        // ---- efeitos no chão (chamas) ----
        var seenEffects = new HashSet<string>();
        foreach (var e in snapshot.Effects ?? Array.Empty<GroundEffectView>())
        {
            seenEffects.Add(e.Id);
            if (!_groundEffects.TryGetValue(e.Id, out var gs))
            {
                gs = CreateGroundEffect(e);
                _world.AddChild(gs.Root);
                _groundEffects[e.Id] = gs;
            }
            gs.Phase += dt;
            gs.Root.Position = new Vector3((float)e.X, 0.05f, (float)e.Y);
            if (gs.Kind.Length > 0)
            {
                // pavio: anel vermelho mostra o raio da explosão e pisca cada vez mais rápido; a faísca treme
                if (gs.Kind is "barril_aceso" or "estouro_aceso")
                {
                    var t = (float)e.T;
                    var blink = 0.5f + 0.5f * Mathf.Sin(gs.Phase * (6f + 30f * t));
                    ((StandardMaterial3D)gs.Disc.MaterialOverride).AlbedoColor = new Color(1f, 0.25f, 0.1f, 0.12f + 0.3f * blink);
                    if (gs.Root.GetNodeOrNull<Node3D>("Faisca") is { } f) f.Position = new Vector3(0.12f + 0.03f * Mathf.Sin(gs.Phase * 40f), 1.1f - 0.25f * t, 0);
                    if (gs.Root.GetNodeOrNull<OmniLight3D>("Luz") is { } l) l.LightEnergy = 0.6f + 1.2f * blink;
                }
                if (gs.Kind is "poca" or "poca_fogo")   // T = segundos que faltam: some no último 1,5 s
                    gs.Root.Scale = Vector3.One * Mathf.Clamp((float)e.T / 1.5f, 0.05f, 1f) * (0.97f + 0.03f * Mathf.Sin(gs.Phase * 3f));
                continue;
            }
            // some aos poucos no fim (T: 0 = começou, 1 = acabou)
            var fade = 1f - Mathf.Clamp((float)e.T, 0f, 1f) * 0.6f;
            gs.Disc.Scale = new Vector3(1f, 1f, 1f) * (0.96f + 0.04f * Mathf.Sin(gs.Phase * 9f));
            ((StandardMaterial3D)gs.Disc.MaterialOverride).AlbedoColor = new Color(1f, 0.42f, 0.1f, 0.42f * fade);
        }
        foreach (var id in _groundEffects.Keys.Where(k => !seenEffects.Contains(k)).ToList())
        {
            _groundEffects[id].Root.QueueFree();
            _groundEffects.Remove(id);
        }

        _hud.EndPlates();
    }

    /// <summary>Um barril de pólvora (modelo do cenário; sem ele, cilindro de madeira com aros).</summary>
    internal const float BarrilAltura = 1.12f;   // 15/11: o fundo do barril em cima das mãos

    internal static Node3D KegNode()
    {
        var n = new Node3D();
        if (!MapKit.Glb(n, "cenario_barril", Vector3.Zero, 0f, 0.62f))
        {
            VisualKit.Part(n, VisualKit.Cylinder(0.26f, 0.26f, 0.8f, 10), VisualKit.Mat(new Color(0.45f, 0.30f, 0.18f), 0.85f), new Vector3(0, 0.4f, 0));
            foreach (var y in new[] { 0.15f, 0.65f })
                VisualKit.Part(n, VisualKit.Cylinder(0.28f, 0.28f, 0.05f, 10), VisualKit.Mat(new Color(0.25f, 0.25f, 0.27f), 0.4f, 0.8f), new Vector3(0, y, 0));
        }
        // marca de pólvora: um "X" vermelho e o pavio saindo da tampa
        VisualKit.Part(n, VisualKit.Cylinder(0.015f, 0.015f, 0.35f, 4), VisualKit.Mat(new Color(0.15f, 0.12f, 0.1f), 0.9f), new Vector3(0.05f, 0.95f, 0), new Vector3(0, 0, 25));
        return n;
    }

    /// <summary>18/11 19h: Espadas Voadoras — 1 a 3 lâminas de luz girando em volta do Campeão (status "espadasN").</summary>
    private static void Espadas(EntityState st, string? status)
    {
        var n = 0;
        foreach (var t in (status ?? "").Split(','))
            if (t.StartsWith("espadas", StringComparison.Ordinal) && t.Length == 8 && char.IsDigit(t[7])) n = t[7] - '0';
        if (n == 0 && (status ?? "").Split(',').Contains("espadas")) n = 3;
        if (n == 0) { if (st.Espadas is not null && GodotObject.IsInstanceValid(st.Espadas)) st.Espadas.QueueFree(); st.Espadas = null; return; }
        if (st.Espadas is null || !GodotObject.IsInstanceValid(st.Espadas) || st.Espadas.GetChildCount() != n)
        {
            st.Espadas?.QueueFree();
            st.Espadas = new Node3D { Name = "EspadasVoadoras" };
            var lamina = VisualKit.Glow(new Color(0.75f, 0.9f, 1f), 2.2f);
            var cabo = VisualKit.Mat(new Color(0.85f, 0.65f, 0.3f), 0.3f, 0.9f);
            for (var i = 0; i < n; i++)
            {
                var piv = new Node3D { RotationDegrees = new Vector3(0, i * 360f / n, 0) };
                st.Espadas.AddChild(piv);
                var e = new Node3D { Position = new Vector3(0, 1.3f, 1.1f), RotationDegrees = new Vector3(0, 90, 70) };
                piv.AddChild(e);
                VisualKit.Part(e, VisualKit.Box(0.06f, 0.7f, 0.02f), lamina, new Vector3(0, 0.35f, 0));
                VisualKit.Part(e, VisualKit.Box(0.22f, 0.04f, 0.05f), cabo, Vector3.Zero);
                VisualKit.Part(e, VisualKit.Box(0.04f, 0.18f, 0.04f), cabo, new Vector3(0, -0.1f, 0));
            }
            st.Visual.Root.AddChild(st.Espadas);
        }
        st.Espadas.RotationDegrees = new Vector3(0, (float)(Time.GetTicksMsec() * 0.18 % 360), 0);
    }

    /// <summary>Barril nas mãos (11/11): fica na frente do peito, abraçado.</summary>
    private static void SetCarry(EntityState st, string? carry)
    {
        if (st.Visual is Visuals.MannequinVisual mv) mv.CarregandoBarril = carry is not null;
        if (carry is null) { st.Carry?.QueueFree(); st.Carry = null; return; }
        if (st.Carry is not null) return;
        st.Carry = KegNode();
        // 15/11: um pouco mais alto e colado na barriga (os braços abraçam por baixo); levemente inclinado para trás
        st.Carry.Position = new Vector3(0, BarrilAltura, 0.34f);
        st.Carry.RotationDegrees = new Vector3(-8f, 0f, 0f);
        st.Carry.Scale = Vector3.One * 0.62f;
        st.Visual.Root.AddChild(st.Carry);
    }

    private GroundEffectState CreateGroundEffect(GroundEffectView e)
    {
        // 11/11: armadilha da dungeon — anel vermelho que enche (clique!) e a rajada de dardos
        if (e.Kind is "dardos_aviso" or "dardos")
        {
            var troot = new Node3D();
            var tring = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = (float)e.Radius, BottomRadius = (float)e.Radius, Height = 0.03f, RadialSegments = 32 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1f, 0.2f, 0.1f, e.Kind == "dardos" ? 0f : 0.28f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                },
            };
            troot.AddChild(tring);
            var tpos = new Vector3((float)e.X, 1.0f, (float)e.Y);
            if (e.Kind == "dardos_aviso") Sfx.PlayAt("alavanca", tpos, -2f);
            else
            {
                _effects.Impact(tpos, new Color(0.75f, 0.7f, 0.6f), 26, 2.2f);
                Sfx.PlayAt("flecha", tpos, 0f);
                Sfx.PlayAt("acerto_flecha", tpos, -4f);
                if (IsNear(tpos, 6f)) _cam.AddShake(0.25f);
            }
            return new GroundEffectState { Root = troot, Disc = tring, Kind = e.Kind };
        }
        // 18/11: poça de lodo do sapo (deixa lento) e de brasa do elemental (queima) — some aos poucos no fim
        if (e.Kind is "poca" or "poca_fogo")
        {
            var fogo = e.Kind == "poca_fogo";
            var proot = new Node3D();
            var cor = fogo ? new Color(1f, 0.38f, 0.08f, 0.55f) : new Color(0.16f, 0.26f, 0.06f, 0.78f);
            var pdisc = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = (float)e.Radius, BottomRadius = (float)e.Radius, Height = 0.03f, RadialSegments = 28 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = cor, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, Metallic = 0.1f,
                    EmissionEnabled = true, Emission = fogo ? new Color(1f, 0.35f, 0.05f) : new Color(0.2f, 0.45f, 0.05f),
                    EmissionEnergyMultiplier = fogo ? 1.6f : 0.35f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                },
            };
            proot.AddChild(pdisc);
            var prng = new Random(e.Id.GetHashCode());
            for (var i = 0; i < 6; i++)   // bolhas (lodo) / brasas (fogo) espalhadas
            {
                var a = prng.NextDouble() * Math.PI * 2; var d = Math.Sqrt(prng.NextDouble()) * e.Radius * 0.8;
                VisualKit.Part(proot, VisualKit.Sphere(fogo ? 0.07f : 0.09f), fogo ? VisualKit.Glow(new Color(1f, 0.6f, 0.2f), 4f)
                    : VisualKit.Mat(new Color(0.25f, 0.38f, 0.1f), 0.1f), new Vector3((float)(Math.Cos(a) * d), 0.04f, (float)(Math.Sin(a) * d)));
            }
            if (fogo) proot.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.5f, 0.2f), LightEnergy = 1.2f, OmniRange = (float)e.Radius + 2f, Position = new Vector3(0, 0.6f, 0) });
            Sfx.PlayAt(fogo ? "hab_impacto" : "plantar", new Vector3((float)e.X, 0.3f, (float)e.Y), -6f);
            return new GroundEffectState { Root = proot, Disc = pdisc, Kind = e.Kind };
        }
        // 11/11: barril da parede rachada — no chão, com o pavio aceso, e a explosão
        if (e.Kind is "barril" or "barril_aceso" or "explosao" or "estouro_aceso")   // 16/11: + corpo do elite explosivo
        {
            var kroot = new Node3D();
            var ring = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = (float)e.Radius, BottomRadius = (float)e.Radius, Height = 0.03f, RadialSegments = 32 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1f, 0.3f, 0.1f, 0f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                },
            };
            kroot.AddChild(ring);
            if (e.Kind == "explosao")
            {
                var pos = new Vector3((float)e.X, 0.6f, (float)e.Y);
                _effects.Impact(pos, new Color(1f, 0.55f, 0.15f), 40, 3.5f);
                _effects.Pulse(pos, new Color(1f, 0.45f, 0.1f, 0.85f), (float)e.Radius * 1.4f, 0.6);
                _effects.Impact(pos + new Vector3(0, 0.8f, 0), new Color(0.35f, 0.32f, 0.3f), 30, 2.5f);   // poeira e pedras
                Sfx.Play("explosao", 0f);
                if (IsNear(pos, 14f)) _cam.AddShake(0.7f);
                kroot.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.55f, 0.2f), LightEnergy = 6f, OmniRange = 12f, Position = new Vector3(0, 1.5f, 0) });
                return new GroundEffectState { Root = kroot, Disc = ring, Kind = e.Kind };
            }
            if (e.Kind != "estouro_aceso") kroot.AddChild(KegNode());
            if (e.Kind == "barril_aceso")
            {
                var spark = VisualKit.Part(kroot, VisualKit.Sphere(0.07f), VisualKit.Glow(new Color(1f, 0.75f, 0.3f), 6f), new Vector3(0.12f, 1.1f, 0));
                spark.Name = "Faisca";
                kroot.AddChild(new OmniLight3D { Name = "Luz", LightColor = new Color(1f, 0.6f, 0.2f), LightEnergy = 1.2f, OmniRange = 3f, Position = new Vector3(0.12f, 1.2f, 0) });
            }
            return new GroundEffectState { Root = kroot, Disc = ring, Kind = e.Kind };
        }
        var root = new Node3D();
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.42f, 0.1f, 0.42f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = new Color(1f, 0.35f, 0.05f),
            EmissionEnergyMultiplier = 1.5f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var r = (float)e.Radius;
        var disc = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = 0.04f, RadialSegments = 32 }, MaterialOverride = mat };
        root.AddChild(disc);
        // labaredas pequenas espalhadas na área
        var rng = new Random(e.Id.GetHashCode());
        for (var i = 0; i < 7; i++)
        {
            var a = rng.NextDouble() * Math.PI * 2;
            var d = Math.Sqrt(rng.NextDouble()) * r * 0.85;
            VisualKit.Part(root, VisualKit.Cylinder(0f, 0.14f, 0.5f, 6), VisualKit.Glow(new Color(1f, 0.55f, 0.15f), 4f),
                new Vector3((float)(Math.Cos(a) * d), 0.25f, (float)(Math.Sin(a) * d)));
        }
        root.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.5f, 0.2f), LightEnergy = 1.4f, OmniRange = r + 2.5f, Position = new Vector3(0, 0.8f, 0) });
        return new GroundEffectState { Root = root, Disc = disc };
    }

    private bool IsNear(Vector3 position, float range)
        => MyState() is { } me && me.Pos.DistanceTo(position) <= range;

    private TelegraphView CreateTelegraph()
    {
        var t = new TelegraphView();
        _world.AddChild(t.Root);
        return t;
    }

    private void OnPlayerActionChanged(EntityState st, PlayerView p)
    {
        if (Efeitos.PoderesVfx.Fase(_effects.Root, st.Visual.Root, st.Pos, p, p.CharacterId == _model.CharacterId, _cam.AddShake)) { st.LastAction = p.Action; st.LastSeq = p.ActionSeq; return; }   // gancho VFX 14/11 (Client/Efeitos/PoderesVfx.cs)
        if (p.Action.StartsWith("recover:skill_mago_passo", StringComparison.Ordinal))
        {
            _effects.Pulse(st.Pos, new Color(0.55f, 0.75f, 1f, 0.7f), 1.6f, 0.4);
            _effects.Impact(st.Pos + new Vector3(0, 1f, 0), new Color(0.55f, 0.75f, 1f), 10, 1f);
        }
        if (p.Action.StartsWith("recover:skill_mago_explosao", StringComparison.Ordinal))
        {
            var at = new Vector3((float)p.AimX, 0.2f, (float)p.AimY);
            _effects.Impact(at, new Color(1f, 0.5f, 0.15f), 22, 2.2f);
            _effects.Pulse(at, new Color(1f, 0.55f, 0.2f, 0.8f), 3.2f, 0.45);
            if (p.CharacterId == _model.CharacterId) _cam.AddShake(0.12f);
        }
        if (p.Action == "dodge") _effects.Pulse(st.Pos, new Color(0.8f, 0.8f, 0.8f, 0.35f), 0.9f, 0.3);
        PowerEffects(st, p);
        st.LastAction = p.Action;
        st.LastSeq = p.ActionSeq;
    }

    // 12/11 (Giro de Lâmina "tipo Garen"): o boneco gira no lugar e três arcos de lâmina giram em volta dele
    private readonly List<(EntityState St, Node3D Node, float Until, float Dur)> _whirls = new();

    private void StartWhirl(EntityState st, float seconds, float radius, Color? tint = null)
    {
        var node = new Node3D { Name = "Redemoinho" };
        var tc = tint ?? new Color(0.55f, 0.75f, 1f);
        var blade = VisualKit.Unshaded(tint is { } t1 ? new Color(t1.R, t1.G, t1.B, 0.75f) : new Color(0.85f, 0.92f, 1f, 0.75f), true);
        var trail = VisualKit.Unshaded(new Color(tc.R, tc.G, tc.B, 0.35f), true);
        var dust = VisualKit.Unshaded(new Color(0.9f, 0.82f, 0.65f, 0.28f), true);
        for (var i = 0; i < 3; i++)
        {
            var arc = new Node3D { RotationDegrees = new Vector3(i == 1 ? 8f : -6f, i * 120f, 0), Position = new Vector3(0, 0.75f + 0.18f * i, 0) };
            node.AddChild(arc);
            VisualKit.Part(arc, VisualKit.Sector(radius * 0.92f, 70f, 16, radius * 0.78f), blade, Vector3.Zero);
            VisualKit.Part(arc, VisualKit.Sector(radius * 0.95f, 140f, 24, radius * 0.55f), trail, Vector3.Zero);
        }
        VisualKit.Part(node, VisualKit.Sector(radius, 360f, 40, radius * 0.7f), dust, new Vector3(0, 0.06f, 0));
        node.AddChild(new OmniLight3D { LightColor = tint ?? new Color(0.7f, 0.85f, 1f), LightEnergy = 1.2f, OmniRange = radius + 2f, Position = new Vector3(0, 1f, 0), ShadowEnabled = false });
        _world.AddChild(node);
        _whirls.Add((st, node, (float)_time + seconds, seconds));
        Sfx.PlayAt("golpe_pesado", st.Pos + Vector3.Up, -4f);
    }

    private void UpdateWhirls(float dt)
    {
        // 17/11: Furacão sustentado — enquanto o servidor diz "active:skill_mascara_furacao", há sempre um redemoinho no boneco
        foreach (var pst in _players.Values)
            if (pst.LastAction.StartsWith("active:skill_mascara_furacao", StringComparison.Ordinal) && !_whirls.Any(w => ReferenceEquals(w.St, pst)))
                StartWhirl(pst, 0.5f, 4.2f, new Color(0.45f, 1f, 0.35f));
        for (var i = _whirls.Count - 1; i >= 0; i--)
        {
            var (st, node, until, dur) = _whirls[i];
            // 17/11 (dono: "a animação do furacão termina, mas o ataque continua até a fúria acabar"): enquanto o servidor
            // mantém o boneco girando ("active:skill_mascara_furacao"), o redemoinho não some
            if (st.LastAction.Contains("furacao") && st.LastAction.StartsWith("active", StringComparison.Ordinal) && until - (float)_time < 0.35f)
            {
                until = (float)_time + 0.35f;
                _whirls[i] = (st, node, until, Mathf.Max(dur, 0.5f));
            }
            var left = until - (float)_time;
            if (left <= 0f || !GodotObject.IsInstanceValid(node)) { if (GodotObject.IsInstanceValid(node)) node.QueueFree(); _whirls.RemoveAt(i); continue; }
            node.Position = st.Pos + new Vector3(0, st.Ground, 0);
            node.RotateY(-dt * 15f);
            var k = Mathf.Clamp(left / 0.25f, 0f, 1f) * Mathf.Clamp((dur - left) / 0.08f, 0f, 1f);
            node.Scale = new Vector3(1f, 1f, 1f) * (0.85f + 0.15f * k);
            foreach (var c in node.GetChildren()) if (c is Node3D n3) n3.Visible = k > 0.05f;
            st.Visual.Root.RotateY(-(dur - left) * 15f);   // o corpo gira junto (o Advance repõe o giro normal a cada quadro)
            if ((int)(left * 4f) != (int)((left + dt) * 4f)) _effects.Dust(st.Pos + new Vector3(0, st.Ground, 0), GroundDust(), 1.2f);
        }
    }

    /// <summary>Efeitos dos poderes dos tomos (área no chão, giros, finais), no momento do golpe.</summary>
    private void PowerEffects(EntityState st, PlayerView p)
    {
        var colon = p.Action.IndexOf(':');
        if (colon < 0) return;
        var phase = p.Action[..colon];
        var skill = p.Action[(colon + 1)..];
        var mine = p.CharacterId == _model.CharacterId;
        var at = new Vector3((float)p.AimX, 0.2f, (float)p.AimY);
        var fwd = new Vector3((float)p.FacingX, 0, (float)p.FacingY);
        if (phase == "windup" && skill is "skill_guerreiro_chuva_espadas" or "skill_arqueiro_tiro_certeiro")
        {
            // 12/11: espada jogada para o alto / mira longa: marca no chão e brilho de carga
            if (skill.EndsWith("chuva_espadas")) _effects.Pulse(at, new Color(0.85f, 0.9f, 1f, 0.55f), 4.2f, 0.9);
            else _effects.Pulse(st.Pos, new Color(1f, 0.95f, 0.6f, 0.5f), 1.4f, 1.1);
            return;
        }
        if (phase == "windup" && (skill.EndsWith("fenix") || skill.EndsWith("serpente") || skill.EndsWith("cometa") || skill.EndsWith("furia_colosso") || skill.EndsWith("floresta_viva")))
        {
            // final: carga visível em volta de quem usa
            var c = skill.EndsWith("serpente") ? new Color(0.2f, 0.6f, 1f, 0.7f) : skill.EndsWith("floresta_viva") ? new Color(0.3f, 1f, 0.5f, 0.7f) : new Color(1f, 0.5f, 0.15f, 0.7f);
            _effects.Pulse(st.Pos, c, 2.2f, 0.9);
            // 18/11 15h: golpe em volta de você — a marca no chão tem o raio de verdade do poder (Fúria: 5,5 m)
            // 18/11 19h (dono: "a marcação vermelha da Fúria ainda está pequena"): sempre o raio de verdade + o corpo do
            // monstro (o golpe pega quem encosta na borda) — sem depender do catálogo trazer o poder
            var raio = _model.SkillById(skill) is { Radius: > 0 } info ? (float)info.Radius : skill.EndsWith("furia_colosso") ? 5.5f : 4f;
            _effects.Area(st.Pos, c, raio + 0.6f, 0.75);
            Sfx.PlayAt("preparo", st.Pos + Vector3.Up, mine ? 0f : -6f);
            return;
        }
        if (phase != "recover") return;
        switch (skill)
        {
            case "skill_arqueiro_chuva" or "skill_batedor_chuva_de_flechas":
                for (var i = 0; i < 10; i++)
                {
                    var off = new Vector3((float)GD.RandRange(-3.2, 3.2), 0, (float)GD.RandRange(-3.2, 3.2));
                    _effects.Slash(at + off + new Vector3(0.3f, 5f, 0.3f), at + off, new Color(0.9f, 0.9f, 0.8f, 0.8f), 0.03f);
                }
                _effects.Pulse(at, new Color(0.9f, 0.85f, 0.6f, 0.6f), 3.6f, 0.4);
                break;
            case "skill_mago_chuva_meteoros" or "skill_mago_cometa":
            {
                var big = skill.EndsWith("cometa");
                for (var i = 0; i < (big ? 1 : 6); i++)
                {
                    var off = big ? Vector3.Zero : new Vector3((float)GD.RandRange(-3.5, 3.5), 0, (float)GD.RandRange(-3.5, 3.5));
                    _effects.Slash(at + off + new Vector3(-2f, 8f, -1f), at + off, new Color(1f, 0.55f, 0.15f), big ? 0.5f : 0.15f);
                    _effects.Impact(at + off, new Color(1f, 0.45f, 0.1f), big ? 40 : 12, big ? 3.5f : 1.4f);
                }
                _effects.Pulse(at, new Color(1f, 0.5f, 0.15f, 0.8f), big ? 6f : 4.2f, 0.6);
                Sfx.PlayAt("explosao", at, mine ? 2f : -4f);
                if (mine) _cam.AddShake(big ? 0.5f : 0.2f);
                break;
            }
            case "skill_bruxa_espinhos":
                for (var i = 0; i < 9; i++)
                {
                    var off = new Vector3((float)GD.RandRange(-2.6, 2.6), 0, (float)GD.RandRange(-2.6, 2.6));
                    _effects.Slash(at + off, at + off + new Vector3(0, 1.4f, 0), new Color(0.35f, 0.8f, 0.3f), 0.08f);
                }
                _effects.Pulse(at, new Color(0.3f, 0.9f, 0.4f, 0.7f), 3.2f, 0.4);
                break;
            case "skill_mascara_furacao":
                _effects.Pulse(st.Pos, new Color(0.45f, 1f, 0.35f, 0.75f), 4f, 0.4);
                StartWhirl(st, 1.6f, 4.2f, new Color(0.45f, 1f, 0.35f));   // 12/11: furacão verde da máscara
                if (mine) _cam.AddShake(0.15f);
                break;
            case "skill_guerreiro_giro":
                _effects.Pulse(st.Pos, new Color(1f, 0.9f, 0.7f, 0.7f), 3.2f, 0.35);
                StartWhirl(st, 1.45f, 3.6f);   // 12/11: redemoinho de lâminas (5 golpes no servidor)
                if (mine) _cam.AddShake(0.12f);
                break;
            case "skill_guerreiro_furia_colosso":
                _effects.Impact(st.Pos + new Vector3(0, 0.3f, 0), new Color(1f, 0.55f, 0.2f), 40, 3.5f);
                _effects.Pulse(st.Pos, new Color(1f, 0.5f, 0.2f, 0.85f), 6f, 0.7);
                Sfx.PlayAt("explosao", st.Pos, mine ? 2f : -4f);
                if (mine) _cam.AddShake(0.5f);
                break;
            case "skill_arqueiro_serpente":
                // tsunami: ondas em sequência à frente
                for (var i = 1; i <= 5; i++)
                    _effects.Pulse(st.Pos + fwd * (i * 2.1f), new Color(0.2f, 0.55f, 1f, 0.75f), 1.6f + i * 0.35f, 0.4 + i * 0.12);
                _effects.Impact(st.Pos + fwd * 5f + new Vector3(0, 1.5f, 0), new Color(0.6f, 0.85f, 1f), 30, 3f);
                if (mine) _cam.AddShake(0.4f);
                break;
            case "skill_bruxa_floresta_viva":
                _effects.Pulse(st.Pos, new Color(0.3f, 1f, 0.5f, 0.7f), 10f, 0.9);
                break;
            // ---- 12/11: poderes novos da 1ª classe
            case "skill_guerreiro_estocada":
            {
                // lança de energia reta: um traço longo e fino à frente
                var de = st.Pos + new Vector3(0, 1.1f, 0) + fwd * 0.6f;
                _effects.Slash(de, de + fwd * 7f, new Color(0.85f, 0.95f, 1f, 0.9f), 0.12f);
                _effects.Slash(de + new Vector3(0, 0.05f, 0), de + fwd * 6.5f, new Color(0.5f, 0.75f, 1f, 0.6f), 0.28f);
                _effects.Impact(st.Pos + fwd * 6.8f + new Vector3(0, 1.1f, 0), new Color(0.7f, 0.85f, 1f), 10, 1f);
                if (mine) _cam.AddShake(0.12f);
                break;
            }
            case "skill_guerreiro_laminas_sangue":
            {
                // duas lâminas de sangue cruzadas em X à frente
                var side = new Vector3(fwd.Z, 0, -fwd.X);
                var c0 = st.Pos + new Vector3(0, 1.1f, 0) + fwd * 2.6f;
                var sangue = new Color(0.85f, 0.05f, 0.08f, 0.9f);
                _effects.Slash(c0 - side * 2f + new Vector3(0, 1.1f, 0), c0 + side * 2f - new Vector3(0, 0.9f, 0), sangue, 0.2f);
                _effects.Slash(c0 + side * 2f + new Vector3(0, 1.1f, 0), c0 - side * 2f - new Vector3(0, 0.9f, 0), sangue, 0.2f);
                _effects.Impact(c0, new Color(0.7f, 0.02f, 0.05f), 24, 1.8f);
                if (mine) _cam.AddShake(0.2f);
                break;
            }
            case "skill_guerreiro_chuva_espadas":
                for (var i = 0; i < 9; i++)
                {
                    var off = new Vector3((float)GD.RandRange(-3.4, 3.4), 0, (float)GD.RandRange(-3.4, 3.4));
                    _effects.Slash(at + off + new Vector3(0, 7f, 0), at + off, new Color(0.9f, 0.92f, 1f, 0.95f), 0.09f);
                    _effects.Impact(at + off, new Color(0.8f, 0.85f, 1f), 6, 0.8f);
                }
                _effects.Pulse(at, new Color(0.8f, 0.85f, 1f, 0.7f), 4.2f, 0.5);
                Sfx.PlayAt("golpe_pesado", at, mine ? 0f : -6f);
                if (mine) _cam.AddShake(0.3f);
                break;
            case "skill_mago_esfera_protecao":
                _effects.Pulse(st.Pos + new Vector3(0, 1f, 0), new Color(0.5f, 0.7f, 1f, 0.6f), 1.8f, 0.8);
                _effects.Pulse(st.Pos, new Color(0.6f, 0.8f, 1f, 0.5f), 2.4f, 0.6);
                break;
            case "skill_mago_prisao_gelo":
                for (var i = 0; i < 10; i++)
                {
                    var off = new Vector3((float)GD.RandRange(-3.0, 3.0), 0, (float)GD.RandRange(-3.0, 3.0));
                    _effects.Slash(at + off, at + off + new Vector3(0, 1.2f, 0), new Color(0.7f, 0.92f, 1f), 0.12f);
                }
                _effects.Pulse(at, new Color(0.6f, 0.9f, 1f, 0.8f), 3.6f, 0.9);
                break;
            case "skill_mago_anel_chamas":
                _effects.Pulse(st.Pos, new Color(1f, 0.45f, 0.1f, 0.85f), 4.6f, 0.5);
                _effects.Impact(st.Pos + new Vector3(0, 0.4f, 0), new Color(1f, 0.5f, 0.15f), 30, 2.5f);
                Sfx.PlayAt("explosao", st.Pos, mine ? 0f : -6f);
                if (mine) _cam.AddShake(0.2f);
                break;
            case "skill_bruxa_raizes":
                for (var i = 0; i < 12; i++)
                {
                    var off = new Vector3((float)GD.RandRange(-2.8, 2.8), 0, (float)GD.RandRange(-2.8, 2.8));
                    _effects.Slash(at + off, at + off + new Vector3((float)GD.RandRange(-0.3, 0.3), 1.1f, (float)GD.RandRange(-0.3, 0.3)), new Color(0.4f, 0.28f, 0.12f), 0.1f);
                }
                _effects.Pulse(at, new Color(0.45f, 0.8f, 0.3f, 0.7f), 3.4f, 0.9);
                break;
            case "skill_bruxa_tronco":
                _effects.Slash(at + new Vector3(0, -0.5f, 0), at + new Vector3(0, 3.2f, 0), new Color(0.42f, 0.27f, 0.12f), 0.55f);
                _effects.Impact(at, new Color(0.5f, 0.35f, 0.15f), 26, 2.2f);
                _effects.Pulse(at, new Color(0.5f, 0.75f, 0.3f, 0.7f), 2.6f, 0.4);
                Sfx.PlayAt("golpe_pesado", at, mine ? 0f : -6f);
                if (mine) _cam.AddShake(0.3f);
                break;
            case "skill_bruxa_vinhas":
                for (var i = -2; i <= 2; i++)
                {
                    var dir = fwd.Rotated(Vector3.Up, i * 0.28f);
                    _effects.Slash(st.Pos + new Vector3(0, 0.5f, 0), st.Pos + dir * 6.5f + new Vector3(0, 0.3f, 0), new Color(0.3f, 0.75f, 0.25f, 0.9f), 0.07f);
                }
                break;
            case "skill_arqueiro_tiro_certeiro":
                _effects.Impact(st.Pos + fwd * 1.2f + new Vector3(0, 1.3f, 0), new Color(1f, 0.95f, 0.7f), 10, 0.8f);
                if (mine) _cam.AddShake(0.15f);
                break;
        }
    }

    /// <summary>Suaviza posição e giro; mede a velocidade para o ciclo de passos.</summary>
    /// <summary>Cor da poeira do passo pelo chão do mapa (areia, terra, neve, cinza, lama).</summary>
    /// <summary>Som próprio de cada habilidade (família do poder pelo id); o resto cai no som da classe.</summary>
    private string SkillSound(string id)
    {
        bool Has(params string[] k) => k.Any(id.Contains);
        if (Has("gelo")) return "hab_gelo";
        if (Has("brasa", "cinzas", "meteoro", "cometa", "fenix", "estilhaco")) return "hab_fogo";
        if (Has("faisca", "lampejo", "centelha", "lanca", "perfurante")) return "hab_raio";
        if (Has("toque", "renovo", "floresta", "cura")) return "hab_cura";
        if (Has("guarda", "barreira")) return "hab_escudo";
        if (Has("giro", "onda", "arco", "serpente")) return "hab_giro";
        if (Has("furia", "salto", "golpe_de_cunha")) return "hab_impacto";
        if (Has("investida", "avancante", "passo_etereo")) return "hab_investida";
        if (Has("passos_fantasma", "olhar", "aura")) return "hab_sombra";
        if (Has("espinhos", "raizes", "vinhas", "tronco")) return "hab_espinhos";
        if (Has("estocada", "laminas", "espadas")) return "hab_impacto";
        if (Has("esfera_protecao")) return "hab_escudo";
        if (Has("anel_chamas")) return "hab_fogo";
        if (Has("tiro_certeiro")) return "hab_arco_tenso";
        if (Has("chuva")) return "hab_chuva_flechas";
        if (Has("emboscada", "rajada")) return "hab_arco_tenso";
        return _model.ClassId is "mago" or "bruxa" ? "magia" : _model.ClassId == "arqueiro" ? "flecha" : "golpe";
    }

    /// <summary>Som do acerto pelo tipo da arma (lâmina corta, porrete afunda, flecha crava, magia estala).</summary>
    private string MyHitSound()
    {
        var w = _model.Equipment.FirstOrDefault(e => e.Slot == ClientModel.WeaponSlot)?.DefinitionId ?? "";
        if (w.Length == 0) return "acerto";
        if (_model.ClassId == "arqueiro" || w.Contains("arco") || w.Contains("besta")) return "acerto_flecha";
        if (_model.ClassId is "mago" or "bruxa") return "acerto_magia";
        if (w.Contains("cajado") || w.Contains("varinha") || w.Contains("talisma") || w.Contains("grimorio") || w.Contains("orbe")) return "acerto_magia";
        if (w.Contains("porrete") || w.Contains("clava") || w.Contains("martelo") || w.Contains("maca") || w.Contains("malho")) return "acerto_contundente";
        return "acerto_lamina";
    }

    /// <summary>Passo pelo chão do mapa (areia abafa, pedra estala, neve range, lama chapinha).</summary>
    private string StepSound() => _model.Map?.Theme switch
    {
        "praia" or "canion" or "acropole" => "passo_areia",
        "neve" => "passo_neve",
        "brejo" => "passo_lama",
        "casarao" or "biblioteca" => "passo_madeira",   // 11/11: assoalho de tábuas
        "cidade" or "lava" or "masmorra" or "cripta" => "passo_pedra",
        _ => "passo_terra",
    };

    /// <summary>Voz da criatura ao preparar um ataque (família pelo id/visual).</summary>
    /// <summary>17/11: as 5 saudações que um NPC pode soltar quando você chega perto (voz_m_* / voz_f_*).</summary>
    private static readonly string[] SaudacoesNpc = { "ola", "hum", "oh", "aham", "haha" };

    private static string? CreatureVoice(MonsterView m)
    {
        var k = (m.DefinitionId + " " + m.Visual).ToLowerInvariant();
        if (k.Contains("dragao") || k.Contains("dragon")) return "rugido";
        if (k.Contains("caranguejo") || k.Contains("crab") || k.Contains("siri")) return "voz_casco";
        if (k.Contains("zumbi") || k.Contains("mumia") || k.Contains("demonio") || k.Contains("wendigo")) return "voz_bruto";
        if (k.Contains("vampiro") || k.Contains("a_morte") || k.Contains("anjo") || k.Contains("genio") || k.Contains("visitante")) return "voz_espirito";
        if (k.Contains("chupa") || k.Contains("cerberus") || k.Contains("tres_cabecas")) return "voz_fera";
        if (k.Contains("meleca") || k.Contains("slime") || k.Contains("planta")) return "passo_lama";
        if (k.Contains("esquelet") || k.Contains("skeleton") || k.Contains("ossad")) return "voz_ossos";
        if (k.Contains("goblin") || k.Contains("kobold") || k.Contains("saci")) return "voz_goblin";
        if (k.Contains("aranha") || k.Contains("spider") || k.Contains("inseto") || k.Contains("vespa") || k.Contains("besouro") || k.Contains("verme")) return "voz_inseto";
        if (k.Contains("fantasma") || k.Contains("espectro") || k.Contains("espirito") || k.Contains("ghost") || k.Contains("alma")) return "voz_espirito";
        if (k.Contains("lobo") || k.Contains("wolf") || k.Contains("urso") || k.Contains("javali") || k.Contains("cao") || k.Contains("fera")) return "voz_fera";
        if (k.Contains("troll") || k.Contains("gigante") || k.Contains("ogro") || k.Contains("orc") || k.Contains("bruto")) return "voz_bruto";
        if (k.Contains("morcego") || k.Contains("bat") || k.Contains("rato")) return "voz_guincho";
        if (k.Contains("mago") || k.Contains("bruxo") || k.Contains("cultista") || k.Contains("xama")) return "voz_feitico";
        return null;
    }

    /// <summary>Ponto um pouco atrás de quem anda (a poeira sobe dos calcanhares, não do meio do corpo).</summary>
    private static Vector3 BehindFeet(EntityState st)
    {
        var v = st.Pos - st.PrevPos;
        v.Y = 0;
        return v.LengthSquared() > 1e-6f ? st.Pos - v.Normalized() * 0.3f : st.Pos;
    }

    private Color GroundDust() => _model.Map?.Theme switch
    {
        "praia" or "canion" or "acropole" => new Color(0.82f, 0.68f, 0.48f, 0.55f),
        "neve" => new Color(0.95f, 0.97f, 1f, 0.7f),
        "lava" => new Color(0.35f, 0.30f, 0.28f, 0.6f),
        "brejo" => new Color(0.32f, 0.36f, 0.26f, 0.5f),
        "cidade" => new Color(0.60f, 0.57f, 0.52f, 0.5f),
        _ => new Color(0.55f, 0.47f, 0.35f, 0.5f),
    };

    private static long _curSnapTick;
    private static Shader? _rimShader;
    private static Texture2D? _blobTex;

    /// <summary>
    /// Destaque do monstro (pedido: "no MU os monstros são destacados, no nosso mal dá para ver"): contorno de luz nas
    /// bordas (fresnel — separa o bicho do chão parecido com ele) e sombra escura e macia embaixo (ele "pisa" no chão).
    /// Elite/raro/chefe: contorno mais forte e avermelhado.
    /// </summary>
    private string? _aimedId;

    private static ShaderMaterial MonsterStandOut(Node3D root, bool special, bool rimOn = true)
    {
        _rimShader ??= new Shader
        {
            Code = @"shader_type spatial;
render_mode blend_add, unshaded, cull_back, depth_draw_never;
uniform vec4 rim : source_color = vec4(1.0, 0.9, 0.7, 1.0);
uniform float strength = 0.6;
void fragment() {
    float f = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
    ALBEDO = rim.rgb * f * strength;
}",
        };
        var mat = new ShaderMaterial { Shader = _rimShader };
        mat.SetShaderParameter("rim", special ? new Color(1f, 0.45f, 0.3f) : new Color(1f, 0.92f, 0.75f));
        mat.SetShaderParameter("strength", special ? 1.1f : 0.65f);
        void Walk(Node n)
        {
            foreach (var c in n.GetChildren())
            {
                // 11/11 noite ("contorno dos ratos piscando, como se fosse o corpo 3d"): pelo/folha de dois lados
                // (sem cull) brigava com a camada de brilho — nesses não põe
                if (c is GeometryInstance3D g && c is not CpuParticles3D && c is not GpuParticles3D && !DoubleSided(g)) g.MaterialOverlay = mat;
                Walk(c);
            }
        }
        if (rimOn) Walk(root);
        if (_blobTex is null)
        {
            var g = new Gradient();
            g.SetColor(0, new Color(0, 0, 0, 0.6f));
            g.AddPoint(0.6f, new Color(0, 0, 0, 0.35f));
            g.SetColor(g.GetPointCount() - 1, new Color(0, 0, 0, 0f));
            _blobTex = new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 64, Height = 64 };
        }
        var aabb = new Aabb();
        var first = true;
        void Bounds(Node n, Transform3D t)
        {
            foreach (var c in n.GetChildren())
            {
                if (c is not Node3D c3) continue;
                var ct = t * c3.Transform;
                if (c3 is VisualInstance3D vi && c3 is not CpuParticles3D) { var b = ct * vi.GetAabb(); aabb = first ? b : aabb.Merge(b); first = false; }
                Bounds(c3, ct);
            }
        }
        Bounds(root, Transform3D.Identity);
        var r = first ? 0.6f : Mathf.Clamp(Mathf.Max(aabb.Size.X, aabb.Size.Z) * 0.65f, 0.4f, 4f);
        var blob = new MeshInstance3D
        {
            Name = "SombraDoMonstro", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new PlaneMesh { Size = new Vector2(r * 2f, r * 2f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = _blobTex, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Colors.White,
            },
            Position = new Vector3(0, 0.03f, 0),
        };
        root.AddChild(blob);
        mat.SetMeta("base_rim", special ? new Color(1f, 0.45f, 0.3f) : new Color(1f, 0.92f, 0.75f));
        mat.SetMeta("base_strength", special ? 1.1f : 0.65f);
        return mat;
    }

    /// <summary>Alvo da mira/trava ganha um contorno vermelho leve (como no MU ao passar o mouse no inimigo).</summary>
    private static bool DoubleSided(GeometryInstance3D g)
    {
        if (g.MaterialOverride is BaseMaterial3D o) return o.CullMode == BaseMaterial3D.CullModeEnum.Disabled;
        if (g is MeshInstance3D mi && mi.Mesh is { } mesh)
            for (var i = 0; i < mesh.GetSurfaceCount(); i++)
                if (mi.GetActiveMaterial(i) is BaseMaterial3D bm && (bm.CullMode == BaseMaterial3D.CullModeEnum.Disabled || bm.Transparency != BaseMaterial3D.TransparencyEnum.Disabled)) return true;
        return false;
    }

    private static void MarkTarget(EntityState st, bool on)
    {
        if (st.Rim is null || st.RimTarget == on) return;
        st.RimTarget = on;
        st.Rim.SetShaderParameter("rim", on ? new Color(1f, 0.18f, 0.12f) : (Color)st.Rim.GetMeta("base_rim"));
        st.Rim.SetShaderParameter("strength", on ? 1.25f : (float)st.Rim.GetMeta("base_strength"));
    }

    /// <summary>
    /// Movimento liso entre fotos do servidor (feedback: "correndo fica muito ruim"). Antes o boneco perseguia a última
    /// posição recebida (10 por segundo): andava aos trancos — corre, quase para, corre. Agora estima a velocidade entre
    /// as duas últimas fotos e continua andando nela até a próxima chegar (no máximo 0,15 s), corrigindo o erro aos poucos.
    /// Parou no servidor → velocidade zera na foto seguinte (sem passar do ponto).
    /// </summary>
    private static void Advance(EntityState st, double x, double y, double fx, double fy, float dt, float rate)
    {
        var target = new Vector3((float)x, 0f, (float)y);
        var now = Time.GetTicksMsec() / 1000.0;
        st.PrevPos = st.Pos;
        if (!st.Placed || st.Pos.DistanceTo(target) > 6f)
        {
            st.Pos = target;
            st.PrevPos = target;
            st.Placed = true;
            st.Yaw = Mathf.Atan2((float)fx, (float)fy);
            st.SnapPos = target; st.SnapVel = Vector3.Zero; st.SnapTime = now; st.SnapTick = _curSnapTick;
        }
        else
        {
            if (st.SnapTick != _curSnapTick)
            {
                var gap = (float)(now - st.SnapTime);
                st.SnapVel = gap is > 0.02f and < 0.4f ? (target - st.SnapPos) / gap : Vector3.Zero;
                if (st.SnapVel.Length() > 12f) st.SnapVel = Vector3.Zero;   // salto/teleporte: não extrapola
                st.SnapPos = target; st.SnapTime = now; st.SnapTick = _curSnapTick;
            }
            var age = Mathf.Min((float)(now - st.SnapTime), 0.15f);
            var predicted = st.SnapPos + st.SnapVel * age;
            st.Pos = st.Pos.Lerp(predicted, 1f - Mathf.Exp(-rate * 0.7f * dt));
        }
        var speed = dt > 0 ? st.Pos.DistanceTo(st.PrevPos) / dt : 0f;
        var before = st.Speed;
        st.Speed = Mathf.Lerp(st.Speed, speed, 1f - Mathf.Exp(-12f * dt));
        var yawBefore = st.Yaw;
        // virar com peso: rápido, mas não instantâneo (antes parecia patinar)
        if (Math.Abs(fx) + Math.Abs(fy) > 1e-6)
            st.Yaw = Mathf.LerpAngle(st.Yaw, Mathf.Atan2((float)fx, (float)fy), 1f - Mathf.Exp(-14f * dt));
        if (dt > 0)
        {
            // leve: arrancar inclina um pouco para a frente, frear para trás, curva para dentro
            var accel = (st.Speed - before) / dt;
            var yawRate = Mathf.AngleDifference(yawBefore, st.Yaw) / dt;
            st.Lean = Mathf.Lerp(st.Lean, Mathf.Clamp(accel * 0.006f, -0.08f, 0.06f), 1f - Mathf.Exp(-6f * dt));
            st.Bank = Mathf.Lerp(st.Bank, Mathf.Clamp(-yawRate * st.Speed * 0.004f, -0.06f, 0.06f), 1f - Mathf.Exp(-6f * dt));
        }
        // 11/11 (escada do casarão): o servidor decide X/Y; o cliente põe os pés na altura do assoalho/degrau
        var ground = Visuals.EscadaCasa.Altura(st.Pos.X, st.Pos.Z) + Relevo.Altura(st.Pos.X, st.Pos.Z);
        st.Ground = Mathf.Abs(st.Ground - ground) > 3f ? ground : Mathf.Lerp(st.Ground, ground, 1f - Mathf.Exp(-18f * dt));
        st.Visual.Root.Position = st.Pos + new Vector3(0, st.Ground, 0);
        st.Visual.Root.Rotation = new Vector3(st.Lean, st.Yaw, st.Bank);
    }

    private ItemState CreateItem(WorldItemView item)
    {
        var root = new Node3D { Position = new Vector3((float)item.X, Visuals.EscadaCasa.Altura(item.X, item.Y), (float)item.Y) };
        _world.AddChild(root);
        var model = GearFactory.GroundItem(_model.ItemInfoFor(item.DefinitionId)?.Model, item.DefinitionId);
        root.AddChild(model);
        // item "vivo" no chão (estilo MU): feixe e anel na cor da raridade, luz própria e brilhos subindo nos raros+
        var vq = CinzasDaMare.Shared.Content.Variacao.Of(item.Quality);
        var color = vq > 0 ? ItemTooltip.VarColor(item.Quality)!.Value with { A = 0.2f + 0.035f * vq } : item.Quality switch
        {
            "excelente" => new Color(0.2f, 1f, 0.55f, 0.65f),
            "incomum" => new Color(0.35f, 0.9f, 0.35f, 0.35f), "raro" => new Color(0.35f, 0.65f, 1f, 0.5f), "epico" => new Color(0.75f, 0.4f, 1f, 0.55f), "chefe" => new Color(1f, 0.5f, 0.15f, 0.55f), "lendario" => new Color(1f, 0.8f, 0.2f, 0.65f),
            _ => new Color(1f, 0.88f, 0.55f, 0.22f),
        };
        // 11/11: joia de refino também ganha feixe (dourado) — é o drop que todo mundo quer
        var jewel = item.DefinitionId.StartsWith("joia_");
        if (jewel) color = new Color(1f, 0.85f, 0.35f, 0.55f);
        var special = item.Quality is "raro" or "epico" or "chefe" or "lendario" or "excelente" || jewel || vq >= 7;
        // 14/11 ("mostrar no chão"): nome do item bom flutuando, com a raridade e as opções estilo MU (★ Excelente, Sorte, Opção +N)
        if (item.Quality is not ("comum" or "") || jewel || item.DefinitionId.StartsWith("caixa_pandora"))
        {
            var info = _model.ItemInfoFor(item.DefinitionId);
            var nome = info?.DisplayName ?? item.DefinitionId;
            var rar = vq > 0 ? $"V{vq} " : item.Quality switch { "excelente" => "Excelente ", "epico" => "Épico ", "raro" => "Raro ", "incomum" => "Incomum ", "lendario" => "Lendário ", "chefe" => "de Chefe ", _ => "" };
            var mu = CinzasDaMare.Shared.Content.OpcoesMu.Rolar(item.ItemId, item.Quality, info?.Category, info?.MinLevel ?? 1);
            var extras = new List<string>();
            if (mu.Linhas.Count(l => l.Text.StartsWith("★")) is > 0 and var n) extras.Add(new string('★', n));
            if (mu.Sorte) extras.Add("Sorte");
            if (mu.Opcao > 0) extras.Add($"Opção +{mu.Opcao}");
            root.AddChild(new Label3D
            {
                Text = rar + nome + (extras.Count > 0 ? "\n" + string.Join(" · ", extras) : ""), FontSize = 30, OutlineSize = 8, PixelSize = 0.006f,
                Modulate = new Color(Mathf.Min(1f, color.R * 1.15f), Mathf.Min(1f, color.G * 1.15f), Mathf.Min(1f, color.B * 1.15f)), OutlineModulate = new Color(0, 0, 0, 0.85f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = false, Position = new Vector3(0, 1.0f, 0),
                VisibilityRangeEnd = 22f,
            });
        }
        // feixe só para o que vale a pena (antes todo item comum tinha um pilar de 3 m: tela cheia de colunas)
        // 13/11 ("o item bom no chão está com uma luz muito forte, quase sólida — transforma num brilho transparente"):
        // o feixe virou um véu de luz — sem tampa, mais forte embaixo e sumindo para cima, transparente nas bordas
        // 14/11 (dono: "ao sair a pessoa fala OLOCO, olha o que eu dropei"): o drop de topo ganha feixe alto que se vê de
        // longe, estouro de brilho e som próprio; o bom (raro, épico, V7+, joia) ganha um sininho
        var oloco = item.Quality is "excelente" or "lendario" || vq >= 10 || item.DefinitionId is "caixa_pandora_4" or "caixa_pandora_5";
        var bom = !oloco && (item.Quality is "raro" or "epico" or "chefe" || vq >= 7 || jewel);
        if (oloco || bom) Sfx.PlayAt(oloco ? "drop_lendario" : "drop_raro", root.Position, oloco ? 2f : -4f, 0.02f, 300);
        if (oloco)
        {
            root.AddChild(new CpuParticles3D
            {
                OneShot = true, Explosiveness = 0.95f, Amount = 48, Lifetime = 1.4f, Position = new Vector3(0, 0.5f, 0),
                Direction = Vector3.Up, Spread = 70f, Gravity = new Vector3(0, -2.5f, 0), InitialVelocityMin = 2.5f, InitialVelocityMax = 5f,
                ScaleAmountMin = 0.7f, ScaleAmountMax = 1.6f,
                Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 4, Rings = 2, Material = VisualKit.Glow(new Color(color.R, color.G, color.B), 6f) },
            });
        }
        if (special || item.Quality == "incomum" || vq >= 4)
        {
            var alto = oloco ? 14f : special ? 4.5f : 1.6f;
            var feixe = new ShaderMaterial { Shader = _feixeShader ??= new Shader { Code = FeixeShader } };
            feixe.SetShaderParameter("cor", new Color(color.R, color.G, color.B, Mathf.Min(0.55f, color.A + 0.1f)));
            feixe.SetShaderParameter("altura", alto);
            var tubo = VisualKit.Part(root, new CylinderMesh { TopRadius = oloco ? 0.5f : special ? 0.32f : 0.18f, BottomRadius = oloco ? 0.28f : special ? 0.2f : 0.12f, Height = alto, RadialSegments = 20, Rings = 1, CapTop = false, CapBottom = false },
                feixe, new Vector3(0, alto / 2f, 0));
            tubo.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        VisualKit.Part(root, VisualKit.Ring(0.55f, 0.04f), VisualKit.Unshaded(new Color(color.R, color.G, color.B, 0.45f), true), new Vector3(0, 0.04f, 0));
        if (special)
        {
            root.AddChild(new OmniLight3D { LightColor = new Color(color.R, color.G, color.B), LightEnergy = 0.8f, OmniRange = 3f, Position = new Vector3(0, 0.6f, 0) });
            var sparks = new CpuParticles3D
            {
                Amount = 16, Lifetime = 1.6f, EmissionShape = CpuParticles3D.EmissionShapeEnum.Ring, EmissionRingRadius = 0.45f, EmissionRingInnerRadius = 0.3f,
                EmissionRingHeight = 0.05f, EmissionRingAxis = Vector3.Up, Direction = Vector3.Up, Spread = 10f, Gravity = new Vector3(0, 0.6f, 0),
                InitialVelocityMin = 0.4f, InitialVelocityMax = 1.0f, ScaleAmountMin = 0.6f, ScaleAmountMax = 1.3f,
                Mesh = new SphereMesh { Radius = 0.025f, Height = 0.05f, RadialSegments = 4, Rings = 2, Material = VisualKit.Glow(new Color(color.R, color.G, color.B), 4f) },
            };
            root.AddChild(sparks);
        }
        return new ItemState { Root = root, Model = model, Phase = (float)GD.RandRange(0, 6) };
    }

    private static Shader? _bubbleShader, _auraShader, _feixeShader;
    private const string FeixeShader = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
uniform vec4 cor : source_color = vec4(1.0, 0.8, 0.3, 0.4);
uniform float altura = 4.0;
varying float h;
void vertex() { h = clamp(VERTEX.y / altura + 0.5, 0.0, 1.0); }
void fragment() {
    float frente = pow(abs(dot(NORMAL, VIEW)), 2.0);          // meio do feixe mais forte, bordas somem
    float sobe = pow(1.0 - h, 1.6);                            // forte embaixo, some em cima
    float pulso = 0.8 + 0.2 * sin(TIME * 2.6 - h * 7.0);
    ALBEDO = cor.rgb;
    ALPHA = cor.a * frente * sobe * pulso;
}";

    /// <summary>
    /// Subiu de nível: aura de poder (estilo da referência do usuário) — chamas de energia amarelas com pontas serrilhadas
    /// nascendo do chão e subindo em volta do corpo, borda brilhante, línguas internas tremulando; depois some.
    /// </summary>
    private void LevelUpAura(EntityState st, int level, bool isMe)
    {
        var aura = new Node3D { Name = "AuraNivel" };
        st.Visual.Root.AddChild(aura);
        var mat = new ShaderMaterial { Shader = _auraShader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;
uniform float rise = 0.0;   // 0 = ainda no chão, 1 = altura toda
uniform float life = 1.0;
uniform float seed = 0.0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0; float a = 0.5; for (int k = 0; k < 4; k++) { v += a * noise(p); p *= 2.03; a *= 0.5; } return v; }
void vertex() {
    // de frente para a câmera, em pé (gira só em Y)
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(normalize(cross(vec3(0.0, 1.0, 0.0), INV_VIEW_MATRIX[2].xyz)), 0.0),
        vec4(0.0, 1.0, 0.0, 0.0), vec4(normalize(cross(INV_VIEW_MATRIX[0].xyz, vec3(0.0, 1.0, 0.0))), 0.0), MODEL_MATRIX[3]);
}
void fragment() {
    vec2 uv = vec2(UV.x, 1.0 - UV.y);
    float t = TIME + seed;
    float x = uv.x - 0.5;
    float n = fbm(vec2(uv.x * 5.0, uv.y * 2.2 - t * 2.6));
    float n2 = fbm(vec2(uv.x * 12.0 + 7.0, uv.y * 4.0 - t * 4.2));
    // silhueta: larga embaixo, afunila em cima; pontas serrilhadas que sobem
    float width = 0.34 * (1.0 - 0.42 * uv.y) + 0.07 * (n - 0.5);
    float top = rise * (0.74 + 0.2 * n2 + 0.03 * sin(uv.x * 22.0 + t * 7.0));
    float body = 1.0 - smoothstep(width - 0.09, width, abs(x));
    float vert = 1.0 - smoothstep(top - 0.16, top, uv.y);
    float outer = body * vert;
    float iw = width * 0.84;
    float inner = (1.0 - smoothstep(iw - 0.1, iw, abs(x))) * (1.0 - smoothstep(top * 0.9 - 0.18, top * 0.9, uv.y));
    float rim = clamp(outer - inner, 0.0, 1.0);
    // línguas de fogo por dentro (sobem rápido)
    float tongue = smoothstep(0.52, 0.72, fbm(vec2(uv.x * 9.0, uv.y * 3.2 - t * 5.5))) * inner;
    vec3 gold = vec3(1.0, 0.68, 0.06);
    vec3 hot = vec3(1.0, 0.93, 0.45);
    ALBEDO = mix(gold, hot, clamp(rim * 1.2 + tongue * 0.5, 0.0, 1.0)) * 1.05;
    float ground = smoothstep(0.0, 0.12, uv.y);   // nasce do chão sem corte reto
    ALPHA = clamp(rim * 0.5 + inner * 0.07 + tongue * 0.16, 0.0, 1.0) * life * ground;
}" } };
        mat.SetShaderParameter("seed", (float)GD.RandRange(0.0, 50.0));
        var flame = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(2.0f, 3.0f) }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        flame.Position = new Vector3(0, 1.5f, 0);
        aura.AddChild(flame);
        // segunda camada um pouco atrás e maior (volume)
        var mat2 = (ShaderMaterial)mat.Duplicate();
        mat2.SetShaderParameter("seed", (float)GD.RandRange(50.0, 100.0));
        var back = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(2.4f, 3.5f) }, MaterialOverride = mat2, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, 1.75f, -0.05f) };
        aura.AddChild(back);
        var light = new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.3f), LightEnergy = 0f, OmniRange = 7f, Position = new Vector3(0, 1.2f, 0) };
        aura.AddChild(light);
        _effects.Pulse(st.Pos, new Color(1f, 0.85f, 0.3f, 0.9f), 3.4f, 0.7);
        _effects.Impact(st.Pos + new Vector3(0, 0.3f, 0), new Color(1f, 0.9f, 0.45f), 36, 2.2f);
        _hud.SpawnFloater(st.Pos + new Vector3(0, 3.2f, 0), $"NÍVEL {level}!", new Color(1f, 0.85f, 0.3f), 34);
        Sfx.PlayAt("nivel", st.Pos + Vector3.Up, isMe ? 2f : -6f);
        if (isMe) _cam.AddShake(0.25f);
        void Set(float rise, float life)
        {
            foreach (var m in new[] { mat, mat2 }) { m.SetShaderParameter("rise", rise); m.SetShaderParameter("life", life); }
            light.LightEnergy = 5f * life * rise;
        }
        Set(0f, 1f);
        var tween = aura.CreateTween();
        tween.TweenMethod(Callable.From<float>(v => Set(v, 1f)), 0f, 1f, 0.35f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);  // sai do chão
        tween.TweenInterval(1.7f);
        tween.TweenMethod(Callable.From<float>(v => Set(0.6f + 0.4f * v, v)), 1f, 0f, 0.9f);   // apaga baixando
        tween.TweenCallback(Callable.From(aura.QueueFree));
    }

    /// <summary>Sinais das classes novas: bolha da Barreira de Éter, névoa da Aura Primal e marca do Olhar do Caçador.</summary>
    /// <summary>
    /// 13/11 ("o que causa o sangramento? coloca alguma animação ou ícone pra reforçar o que tá acontecendo — sangue saindo,
    /// veneno algo roxo..."): gotas saindo do corpo enquanto o status dura — sangue (pinga até o chão e fica uma manchinha),
    /// veneno (bolhas verde-arroxeadas subindo), maldição (fumaça roxa), queimadura (faíscas), lentidão (cristais azuis).
    /// </summary>
    private static float R(float a, float b) => a + (float)Random.Shared.NextDouble() * (b - a);

    private static StandardMaterial3D[]? _sangue;

    /// <summary>
    /// 13/11 ("o sangramento parece tinta; deixa mais parecido com sangue"): vermelho-escuro quase vinho, molhado
    /// (pouca rugosidade, brilho de verniz), três tons para não sair tudo igual.
    /// </summary>
    private static StandardMaterial3D SangueMat()
    {
        _sangue ??= new[] { new Color(0.32f, 0.01f, 0.015f), new Color(0.42f, 0.02f, 0.02f), new Color(0.24f, 0.005f, 0.01f) }
            .Select(c => new StandardMaterial3D
            {
                AlbedoColor = c, Roughness = 0.12f, Metallic = 0.05f, MetallicSpecular = 0.8f,
                ClearcoatEnabled = true, Clearcoat = 0.9f, ClearcoatRoughness = 0.08f,
                RimEnabled = true, Rim = 0.25f, RimTint = 0.8f,
            }).ToArray();
        return _sangue[Random.Shared.Next(_sangue.Length)];
    }

    private static readonly string[] SemSangue = { "golem", "esqueleto", "skeleton", "automato", "autômato", "fantasma", "ghost", "boneco",
        "espectro", "elemental", "cristal", "gelo", "slime", "gosma", "estatua", "gargul", "gárgul", "armadura", "sombra", "espirito", "wisp", "vagalume" };

    /// <summary>Quem sangra no golpe: jogador e bicho de carne (pedra, osso, gosma e fantasma não).</summary>
    private bool Sangra(EntityState st)
    {
        if (st.Def.Length == 0) return true;   // jogadores e NPCs
        var d = st.Def.ToLowerInvariant();
        return !SemSangue.Any(d.Contains);
    }

    /// <summary>Respingo curto no golpe: gotinhas saem do lado oposto ao golpe, caem em arco e viram manchinhas que somem.</summary>
    private void RespingoSangue(EntityState st, Vector3 de, Vector3 dir, int n)
    {
        if (!st.Visual.Root.IsInsideTree() || !IsNear(st.Pos, 30f)) return;
        var d = new Vector3(dir.X, 0, dir.Z);
        d = d.LengthSquared() > 1e-4f ? d.Normalized() : Vector3.Forward;
        var chao = st.Visual.Root.GlobalPosition.Y + 0.015f;
        for (var i = 0; i < n; i++)
        {
            var tam = R(0.014f, 0.032f);
            var g = VisualKit.Part(_world, VisualKit.Sphere(tam), SangueMat(), de, null, new Vector3(0.85f, 1.5f, 0.85f));
            g.TopLevel = true;
            g.GlobalPosition = de + new Vector3(R(-0.08f, 0.08f), R(-0.1f, 0.12f), R(-0.08f, 0.08f));
            g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            var lado = new Vector3(-d.Z, 0, d.X) * R(-0.9f, 0.9f);
            var v = (d * R(1.2f, 2.6f) + lado) + Vector3.Up * R(0.6f, 2.0f);
            var p0 = g.GlobalPosition;
            // tempo até o chão (parábola)
            var h = p0.Y - chao;
            var tf = (v.Y + Mathf.Sqrt(v.Y * v.Y + 2f * 9.8f * Mathf.Max(0.05f, h))) / 9.8f;
            var tw = g.CreateTween();
            tw.TweenMethod(Callable.From<float>(t =>
            {
                if (!GodotObject.IsInstanceValid(g)) return;
                g.GlobalPosition = p0 + new Vector3(v.X * t, v.Y * t - 4.9f * t * t, v.Z * t);
            }), 0f, tf, tf);
            var mancha = R(1.6f, 3.2f);
            tw.TweenProperty(g, "scale", new Vector3(mancha, 0.08f, mancha * R(0.7f, 1.4f)), 0.06f);
            tw.TweenInterval(R(1.2f, 2.2f));
            tw.TweenProperty(g, "scale", new Vector3(mancha * 0.4f, 0.04f, mancha * 0.4f), 0.8f);
            tw.TweenCallback(Callable.From(g.QueueFree));
        }
    }

    private static StandardMaterial3D? _fumacaMat;
    /// <summary>Fumacinha de veneno: um sopro verde-musgo, macio (textura radial), que sobe, cresce e some.</summary>
    private void Fumacinha(Node3D root)
    {
        if (_fumacaMat is null)
        {
            var grad = new Gradient();
            grad.SetColor(0, new Color(1, 1, 1, 0.9f)); grad.SetColor(1, new Color(1, 1, 1, 0f));
            var tex = new GradientTexture2D { Gradient = grad, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 64, Height = 64 };
            _fumacaMat = new StandardMaterial3D
            {
                AlbedoTexture = tex, AlbedoColor = new Color(0.45f, 0.7f, 0.25f, 0.42f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
                VertexColorUseAsAlbedo = false,
            };
        }
        var mat = (StandardMaterial3D)_fumacaMat.Duplicate();
        mat.AlbedoColor = new Color(R(0.35f, 0.5f), R(0.62f, 0.78f), R(0.18f, 0.3f), 0.38f);
        var inicio = root.GlobalPosition + new Vector3(R(-0.3f, 0.3f), R(0.4f, 1.3f), R(-0.3f, 0.3f));
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * R(0.25f, 0.4f) }, MaterialOverride = mat, TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _world.AddChild(q);
        q.GlobalPosition = inicio;
        var tw = q.CreateTween().SetParallel();
        var dur = R(1.0f, 1.5f);
        tw.TweenProperty(q, "global_position", inicio + new Vector3(R(-0.25f, 0.25f), R(0.5f, 0.9f), R(-0.25f, 0.25f)), dur).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(q, "scale", Vector3.One * R(2.0f, 2.8f), dur);
        tw.TweenProperty(mat, "albedo_color:a", 0f, dur).SetEase(Tween.EaseType.In);
        tw.Chain().TweenCallback(Callable.From(q.QueueFree));
    }

    /// <summary>Caveirinha da maldição: crânio pequeno roxo (testa, olhos fundos, dentes) que sobe balançando e some.</summary>
    private void Caveirinha(Node3D root)
    {
        var g = new Node3D { TopLevel = true };
        _world.AddChild(g);
        var osso = VisualKit.Glow(new Color(0.72f, 0.45f, 1f), 1.6f);
        osso.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        var olho = VisualKit.Unshaded(new Color(0.08f, 0.02f, 0.12f), false);
        VisualKit.Part(g, VisualKit.Sphere(0.075f), osso, new Vector3(0, 0.02f, 0), null, new Vector3(1f, 0.95f, 0.95f));
        VisualKit.Part(g, VisualKit.Box(0.085f, 0.045f, 0.07f), osso, new Vector3(0, -0.055f, 0.01f));
        foreach (var lado in new[] { -1f, 1f })
            VisualKit.Part(g, VisualKit.Sphere(0.022f), olho, new Vector3(lado * 0.03f, 0.015f, 0.065f));
        VisualKit.Part(g, VisualKit.Box(0.012f, 0.02f, 0.01f), olho, new Vector3(0, -0.02f, 0.072f));
        foreach (var n in g.GetChildren()) if (n is GeometryInstance3D gi) gi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var inicio = root.GlobalPosition + new Vector3(R(-0.35f, 0.35f), R(1.0f, 1.7f), R(-0.35f, 0.35f));
        g.GlobalPosition = inicio;
        g.Scale = Vector3.One * 0.4f;
        if (GetViewport().GetCamera3D() is { } cam) g.LookAt(cam.GlobalPosition with { Y = inicio.Y }, Vector3.Up, true);
        var tw = g.CreateTween().SetParallel();
        tw.TweenProperty(g, "scale", Vector3.One * R(0.9f, 1.2f), 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(g, "global_position", inicio + new Vector3(R(-0.15f, 0.15f), R(0.6f, 0.9f), R(-0.15f, 0.15f)), 1.3f).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(osso, "albedo_color:a", 0f, 0.6f).SetDelay(0.7f);
        tw.TweenProperty(g, "scale", Vector3.One * 0.2f, 0.4f).SetDelay(0.9f);
        tw.Chain().TweenCallback(Callable.From(g.QueueFree));
    }

    private void StatusGotas(EntityState st, string status, float dt)
    {
        if (status.Length == 0) return;
        st.GotaT -= dt;
        if (st.GotaT > 0f) return;
        st.GotaT = 0.12f;
        var root = st.Visual.Root;
        if (!root.IsInsideTree()) return;
        foreach (var kind in status.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var (cor, sobe, tam, brilho) = kind switch
            {
                "sangramento" => (new Color(0.65f, 0.02f, 0.03f), false, 0.045f, 0.4f),
                "veneno" => (new Color(0.55f, 0.85f, 0.2f), true, 0.06f, 1.2f),
                "maldicao" => (new Color(0.55f, 0.2f, 0.85f), true, 0.09f, 1.4f),
                "queimadura" => (new Color(1f, 0.5f, 0.12f), true, 0.035f, 3f),
                "lentidao" => (new Color(0.55f, 0.85f, 1f), false, 0.04f, 1.2f),
                _ => (Colors.Transparent, false, 0f, 0f),
            };
            if (tam <= 0f) continue;
            // 17/11 noite (dono: "o verde do veneno e o roxo do fantasma estão parecendo chiclete — efeitos sutis e bem
            // feitos: fumacinha pro veneno e caveirinha pro roxo")
            if (kind == "veneno") { if (R(0f, 1f) < 0.7f) Fumacinha(root); continue; }
            if (kind == "maldicao") { if (R(0f, 1f) < 0.3f) Caveirinha(root); continue; }
            var inicio = root.GlobalPosition + new Vector3(R(-0.25f, 0.25f), R(0.7f, 1.4f), R(-0.25f, 0.25f));
            var mat = kind == "sangramento" ? SangueMat() : brilho > 1f ? VisualKit.Glow(cor, brilho) : VisualKit.Mat(cor, 0.2f);
            if (kind == "maldicao" || kind == "veneno") { mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha; mat.AlbedoColor = cor with { A = 0.7f }; }
            var g = VisualKit.Part(_world, VisualKit.Sphere(tam), mat, inicio, null, kind == "sangramento" ? new Vector3(0.8f, 1.4f, 0.8f) : null);
            g.TopLevel = true;
            g.GlobalPosition = inicio;
            g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            var tw = g.CreateTween();
            if (sobe)
            {
                tw.SetParallel();
                tw.TweenProperty(g, "global_position", inicio + new Vector3(R(-0.2f, 0.2f), R(0.6f, 1.1f), R(-0.2f, 0.2f)), R(0.7f, 1.1f));
                tw.TweenProperty(g, "scale", Vector3.One * (kind == "maldicao" ? 2.2f : 0.3f), 0.9f);
                tw.Chain().TweenCallback(Callable.From(g.QueueFree));
            }
            else
            {
                var chao = root.GlobalPosition.Y + 0.02f;
                tw.TweenProperty(g, "global_position", new Vector3(inicio.X, chao, inicio.Z), Mathf.Sqrt(2f * (inicio.Y - chao) / 9.8f)).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
                if (kind == "sangramento")
                {
                    // 13/11: mancha irregular (oval torta, tamanho variado) que escorre um pouco e seca devagar
                    var a = R(1.8f, 3.4f);
                    tw.TweenProperty(g, "scale", new Vector3(a, 0.06f, a * R(0.6f, 1.5f)), 0.07f);
                    tw.TweenProperty(g, "scale", new Vector3(a * 1.25f, 0.05f, a * R(0.9f, 1.8f)), 0.6f);
                    tw.TweenInterval(R(2.2f, 3.5f));
                    tw.TweenProperty(g, "scale", new Vector3(a * 0.5f, 0.03f, a * 0.5f), 1.2f);
                }
                tw.TweenCallback(Callable.From(g.QueueFree));
            }
        }
    }

    private void StatusFx(EntityState st, bool shield, bool weakened, bool marked, float dt)
    {
        var root = st.Visual.Root;
        if (shield && st.Bubble is null)
        {
            st.Bubble = new Node3D { Name = "Barreira" };
            root.AddChild(st.Bubble);
            var mat = new ShaderMaterial { Shader = _bubbleShader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;
uniform vec4 tint : source_color = vec4(0.3, 1.0, 0.75, 1.0);
void fragment() {
    float rim = pow(1.0 - abs(dot(NORMAL, VIEW)), 2.5);
    float bands = 0.5 + 0.5 * sin(UV.y * 60.0 + TIME * 3.0);
    ALBEDO = tint.rgb;
    ALPHA = clamp(rim * 0.8 + bands * 0.04, 0.0, 1.0);
}" } };
            var sphere = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.85f, Height = 2.1f, RadialSegments = 24, Rings = 12 }, MaterialOverride = mat };
            sphere.Position = new Vector3(0, 0.95f, 0);
            st.Bubble.AddChild(sphere);
            if (IsNear(st.Pos, 30f)) Sfx.PlayAt("cura", st.Pos + Vector3.Up, -6f);
        }
        else if (!shield && st.Bubble is not null)
        {
            _effects.Impact(st.Pos + new Vector3(0, 1f, 0), new Color(0.45f, 0.95f, 0.85f), 10, 1f);
            st.Bubble.QueueFree();
            st.Bubble = null;
        }
        if (st.Bubble is not null) st.Bubble.Scale = Vector3.One * (1f + Mathf.Sin((float)_time * 4f) * 0.02f);

        if (weakened && st.Mist is null)
        {
            st.Mist = new Node3D { Name = "Nevoa" };
            root.AddChild(st.Mist);
            VisualKit.Part(st.Mist, VisualKit.Ring(0.9f, 0.25f), VisualKit.Unshaded(new Color(0.3f, 0.95f, 0.5f, 0.45f), true), new Vector3(0, 0.05f, 0));
            VisualKit.Part(st.Mist, VisualKit.Ring(0.6f, 0.08f), VisualKit.Unshaded(new Color(1f, 0.8f, 0.35f, 0.5f), true), new Vector3(0, 0.07f, 0));
            for (var i = 0; i < 6; i++)
            {
                var a = i * Mathf.Tau / 6f;
                VisualKit.Part(st.Mist, VisualKit.Sphere(0.06f), VisualKit.Glow(i % 2 == 0 ? new Color(0.3f, 1f, 0.55f) : new Color(1f, 0.8f, 0.35f), 1.6f),
                    new Vector3(Mathf.Cos(a) * 0.75f, 0.4f + 0.3f * (i % 3), Mathf.Sin(a) * 0.75f));
            }
        }
        else if (!weakened && st.Mist is not null) { st.Mist.QueueFree(); st.Mist = null; }
        if (st.Mist is not null) st.Mist.Rotation = new Vector3(0, (float)_time * 1.3f, 0);

        if (marked && st.Mark is null)
        {
            st.Mark = new Node3D { Name = "Marca", Position = new Vector3(0, st.Visual.Height + 0.6f, 0) };
            root.AddChild(st.Mark);
            var red = VisualKit.Glow(new Color(1f, 0.25f, 0.2f), 2.2f);
            VisualKit.Part(st.Mark, VisualKit.Cylinder(0f, 0.13f, 0.2f, 4), red, new Vector3(0, -0.1f, 0), new Vector3(180, 0, 0));
            VisualKit.Part(st.Mark, VisualKit.Ring(0.22f, 0.03f), red, Vector3.Zero, new Vector3(90, 0, 0));
        }
        else if (!marked && st.Mark is not null) { st.Mark.QueueFree(); st.Mark = null; }
        if (st.Mark is not null) st.Mark.Rotation = new Vector3(0, (float)_time * 2.5f, 0);
    }

    /// <summary>Monstro dormindo: "z z" flutuando devagar acima da cabeça.</summary>
    private static StandardMaterial3D? _lidMat;

    /// <summary>11/11 noite (quadro: "dormindo precisam fechar os olhos"): as superfícies de olho/pupila (pelo nome do
    /// material ou do nó: olho, eye, pupila) viram "pálpebra" escura e sem brilho; voltam ao acordar.</summary>
    private static void CloseEyes(EntityState st, bool closed)
    {
        if (closed == (st.ClosedEyes is not null)) return;
        if (!closed)
        {
            foreach (var (mesh, surface, before) in st.ClosedEyes!)
                if (GodotObject.IsInstanceValid(mesh)) mesh.SetSurfaceOverrideMaterial(surface, before);
            st.ClosedEyes = null;
            return;
        }
        _lidMat ??= new StandardMaterial3D { AlbedoColor = new Color(0.09f, 0.075f, 0.065f), Roughness = 1f };
        st.ClosedEyes = new();
        void Walk(Node n, bool eyeNode)
        {
            var nm = n.Name.ToString().ToLowerInvariant();
            var isEyeNode = eyeNode || nm.StartsWith("eye_") || nm.Contains("olho") || nm.Contains("pupila");
            if (n is MeshInstance3D mi && mi.Mesh is { } mesh)
                for (var i = 0; i < mesh.GetSurfaceCount(); i++)
                {
                    var mat = mi.GetActiveMaterial(i);
                    var mn = (mat?.ResourceName ?? "").ToLowerInvariant();
                    if (!isEyeNode && !mn.Contains("olho") && !mn.Contains("eye") && !mn.Contains("pupila")) continue;
                    st.ClosedEyes.Add((mi, i, mi.GetSurfaceOverrideMaterial(i)));
                    mi.SetSurfaceOverrideMaterial(i, _lidMat);
                }
            foreach (var c in n.GetChildren()) Walk(c, isEyeNode);
        }
        Walk(st.Visual.Root, false);
    }

    private void SleepMark(EntityState st, bool asleep)
    {
        CloseEyes(st, asleep);
        if (!asleep) { if (st.Zz is not null) st.Zz.Visible = false; return; }
        st.Zz ??= new Label3D { Text = "z z", FontSize = 46, OutlineSize = 8, Modulate = new Color(0.8f, 0.9f, 1f, 0.85f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.01f, NoDepthTest = false };
        if (st.Zz.GetParent() is null) st.Visual.Root.AddChild(st.Zz);
        st.Zz.Visible = true;
        var k = (float)(_time * 0.6 % 1.0);
        st.Zz.Position = new Vector3(0.2f, st.Visual.Height + 0.2f + k * 0.4f, 0);
        st.Zz.Modulate = new Color(0.8f, 0.9f, 1f, 0.85f * (1f - k));
    }

    /// <summary>
    /// 12/11: sopro de fogo (gárgula). Preparação: brasa na boca e luz crescendo; disparo: jato de chamas do tamanho do
    /// cone que o servidor usa (o alcance da marca = o alcance do fogo), com fumaça no fim.
    /// </summary>
    private void UpdateFire(EntityState st, MonsterView m, bool windup, bool active)
    {
        var on = m.Alive && m.AimKind == "fogo" && (windup || active);
        if (!on)
        {
            if (st.Fire is not null) st.Fire.Emitting = false;
            if (st.FireLight is not null) st.FireLight.LightEnergy = Mathf.MoveToward(st.FireLight.LightEnergy, 0f, 0.2f);
            return;
        }
        var reach = Mathf.Max(1.5f, (float)m.AimRadius);
        if (st.Fire is null)
        {
            st.Fire = new CpuParticles3D
            {
                Name = "SoproDeFogo", Amount = 70, Lifetime = 0.55f, LocalCoords = false, Emitting = false,
                EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.08f,
                Direction = new Vector3(0, 0, 1), Gravity = new Vector3(0, 1.4f, 0), DampingMin = 1f, DampingMax = 2f,
                ScaleAmountMin = 0.25f, ScaleAmountMax = 0.5f, ScaleAmountCurve = VidaAmbiente.Crescer(0.35f, 1.6f),
                ColorRamp = VidaAmbiente.Rampa((0f, new Color(1f, 0.95f, 0.6f, 1f)), (0.35f, new Color(1f, 0.55f, 0.12f, 0.9f)),
                    (0.75f, new Color(0.85f, 0.2f, 0.05f, 0.55f)), (1f, new Color(0.15f, 0.1f, 0.1f, 0f))),
                Mesh = new QuadMesh { Size = Vector2.One, Material = VidaAmbiente.Particula("fx_fumaca.png", Colors.White, true) },
            };
            st.Visual.Root.AddChild(st.Fire);
            st.FireLight = new OmniLight3D { LightColor = new Color(1f, 0.55f, 0.2f), LightEnergy = 0f, OmniRange = 5f, ShadowEnabled = false };
            st.Visual.Root.AddChild(st.FireLight);
        }
        // boca: na frente da cabeça (o visual já está virado para a frente do monstro = +Z local)
        var mouth = new Vector3(0, Mathf.Max(0.5f, st.Visual.Height * 0.78f), 0.3f);
        st.Fire.Position = mouth;
        st.Fire.Spread = Mathf.Clamp((float)m.AimArc * 0.45f, 8f, 40f);
        st.Fire.InitialVelocityMin = reach / 0.55f * 0.75f;
        st.Fire.InitialVelocityMax = reach / 0.55f * 1.05f;
        st.Fire.Direction = new Vector3(0, -0.12f, 1f);
        st.Fire.Emitting = active;
        st.FireLight!.Position = mouth + new Vector3(0, 0, active ? reach * 0.4f : 0.1f);
        st.FireLight.LightEnergy = active ? 3.2f + 0.8f * Mathf.Sin((float)_time * 40f) : 1.6f * (float)m.ActionT;
        if (active && IsNear(st.Pos, 10f)) _cam.AddShake(0.015f);
    }

    /// <summary>Raio (olho alado, chefes): fio fino de mira na preparação; feixe grosso e brilhante no disparo.</summary>
    private void UpdateBeam(EntityState st, MonsterView m, bool windup, bool active)
    {
        UpdateChicote(st, m, windup, active);
        var on = m.Alive && m.AimKind == "beam" && (windup || active);
        if (!on) { if (st.Beam is not null) st.Beam.Visible = false; return; }
        if (st.Beam is null)
        {
            st.Beam = new Node3D { Name = "Raio" };
            // 18/11: língua do sapo (rosa, sem brilho) e ferrão do escorpião (verde de veneno) usam o mesmo traço
            var lingua = m.Action.Contains("linguada"); var ferrao = m.Action.Contains("ferrao");
            var cor = lingua ? new Color(0.95f, 0.45f, 0.55f) : ferrao ? new Color(0.5f, 1f, 0.25f) : new Color(1f, 0.25f, 0.15f);
            var core = VisualKit.Part(st.Beam, VisualKit.Cylinder(1f, 1f, 1f, 10), lingua ? VisualKit.Mat(cor, 0.3f) : VisualKit.Glow(cor, 6f), Vector3.Zero, new Vector3(90, 0, 0));
            core.Name = "Nucleo";
            var halo = VisualKit.Part(st.Beam, VisualKit.Cylinder(1f, 1f, 1f, 10), VisualKit.Unshaded(new Color(cor.R, cor.G, cor.B, lingua ? 0f : 0.35f), true), Vector3.Zero, new Vector3(90, 0, 0));
            halo.Name = "Halo";
            st.Beam.AddChild(new OmniLight3D { Name = "Luz", LightColor = cor, LightEnergy = lingua ? 0f : 3f, OmniRange = 5f });
            _world.AddChild(st.Beam);
        }
        var len = (float)m.AimRadius;
        var width = Mathf.Max(0.15f, -(float)m.AimArc * 0.5f);
        var h = st.Visual.Height > 1.6f ? Mathf.Min(st.Visual.Height - 0.6f, 2.2f) : 1.0f;
        st.Beam.Visible = true;
        st.Beam.Position = new Vector3(st.Pos.X, h, st.Pos.Z);
        st.Beam.Rotation = new Vector3(0, Mathf.Atan2((float)m.FacingX, (float)m.FacingY), 0);
        var thick = active ? width * (0.8f + 0.2f * Mathf.Sin((float)_time * 60f)) : 0.03f + 0.04f * (float)m.ActionT;
        foreach (var name in new[] { "Nucleo", "Halo" })
        {
            var mesh = st.Beam.GetNode<Node3D>(name);
            var r = name == "Halo" ? thick * 1.8f : thick * 0.6f;
            mesh.Scale = new Vector3(r, len, r);
            mesh.Position = new Vector3(0, 0, len / 2f);
            mesh.Visible = active || name == "Nucleo";
        }
        var light = st.Beam.GetNode<OmniLight3D>("Luz");
        light.Position = new Vector3(0, 0, Mathf.Min(len, 3f));
        light.LightEnergy = active ? 4f : 0.8f * (float)m.ActionT;
        if (active && IsNear(st.Pos, 12f)) _cam.AddShake(0.04f);
    }

    /// <summary>18/11: chicotada (corda, corrente, cipó, língua, arpão): na preparação o chicote enrola atrás; no golpe estica
    /// até a ponta da marca, ondulando — sem brilho (não é magia).</summary>
    private void UpdateChicote(EntityState st, MonsterView m, bool windup, bool active)
    {
        var on = m.Alive && m.AimKind == "chicote" && (windup || active);
        if (!on) { if (st.Chicote is not null) st.Chicote.Visible = false; return; }
        const int Gomos = 14;
        if (st.Chicote is null)
        {
            st.Chicote = new Node3D { Name = "Chicote" };
            var cor = m.DefinitionId.Contains("raiz") || m.DefinitionId.Contains("planta") || m.DefinitionId.Contains("urtiga") || m.DefinitionId.Contains("lodo") || m.DefinitionId.Contains("carvalho")
                ? new Color(0.28f, 0.36f, 0.14f)
                : m.DefinitionId.Contains("cavaleiro") || m.DefinitionId.Contains("capitao") || m.DefinitionId.Contains("cobrador") || m.DefinitionId.Contains("sepulcral")
                    ? new Color(0.45f, 0.45f, 0.48f) : new Color(0.42f, 0.27f, 0.15f);
            var mat = VisualKit.Mat(cor, 0.7f, cor.R < 0.5f && cor.B > 0.4f ? 0.8f : 0f);
            for (var i = 0; i < Gomos; i++)
                VisualKit.Part(st.Chicote, VisualKit.Cylinder(0.05f - i * 0.0025f, 0.05f - i * 0.0025f, 1f, 6), mat, Vector3.Zero, new Vector3(90, 0, 0)).Name = "g" + i;
            _world.AddChild(st.Chicote);
        }
        var len = active ? (float)m.AimRadius : 1.2f + 0.6f * (float)m.ActionT;
        var h = st.Visual.Height > 1.6f ? Mathf.Min(st.Visual.Height - 0.5f, 2.0f) : 0.9f;
        st.Chicote.Visible = true;
        st.Chicote.Position = new Vector3(st.Pos.X, h, st.Pos.Z);
        st.Chicote.Rotation = new Vector3(0, Mathf.Atan2((float)m.FacingX, (float)m.FacingY) + (active ? 0f : Mathf.Pi * 0.85f), 0);
        var passo = len / Gomos;
        for (var i = 0; i < Gomos; i++)
        {
            var g = st.Chicote.GetNode<Node3D>("g" + i);
            var k = i / (float)Gomos;
            var onda = (active ? 0.25f : 0.5f) * Mathf.Sin(k * 9f - (float)_time * (active ? 30f : 8f)) * k;
            g.Position = new Vector3(onda, -k * k * (active ? 0.6f : 0.4f) * h, (i + 0.5f) * passo);
            g.Scale = new Vector3(1f, passo * 1.15f, 1f);
        }
        if (active && IsNear(st.Pos, 10f)) _cam.AddShake(0.012f);
    }

    private static Node3D? FabProjetil(string kind)
    {
        var k = kind.StartsWith("monster_", StringComparison.Ordinal) ? kind[8..] : kind;
        var n = new Node3D();
        void Rastro(Color c, float r = 0.15f, float l = 0.8f)
            => VisualKit.Part(n, VisualKit.Cylinder(r, 0.01f, l, 8), VisualKit.Unshaded(c, true), new Vector3(0, 0, -l * 0.55f), new Vector3(90, 0, 0));
        switch (k)
        {
            case "coco" or "chuva_de_coco":
            {
                var casca = VisualKit.Mat(new Color(0.38f, 0.24f, 0.12f), 0.95f);
                VisualKit.Part(n, VisualKit.Sphere(0.2f), casca, Vector3.Zero, new Vector3(30, 40, 0), new Vector3(1f, 1.15f, 1f));
                foreach (var (x, y) in new[] { (-0.05f, 0.09f), (0.05f, 0.09f), (0f, 0.15f) })
                    VisualKit.Part(n, VisualKit.Sphere(0.025f), VisualKit.Mat(new Color(0.12f, 0.07f, 0.04f)), new Vector3(x, y, 0.15f));
                Rastro(new Color(0.7f, 0.55f, 0.35f, 0.25f));
                return n;
            }
            case "estilingue" or "rajada_estilingue":
                VisualKit.Part(n, VisualKit.Sphere(0.08f), VisualKit.Mat(new Color(0.5f, 0.48f, 0.44f), 0.9f), Vector3.Zero);
                Rastro(new Color(0.85f, 0.8f, 0.7f, 0.25f), 0.06f, 0.6f);
                return n;
            case "nota_desafinada":
            {
                // nota musical roxa brilhante (cabeça + haste + bandeirinha)
                var g = VisualKit.Glow(new Color(0.8f, 0.35f, 1f), 3f);
                VisualKit.Part(n, VisualKit.Sphere(0.13f), g, Vector3.Zero, null, new Vector3(1.3f, 1f, 0.6f));
                VisualKit.Part(n, VisualKit.Box(0.03f, 0.42f, 0.03f), g, new Vector3(0.12f, 0.2f, 0));
                VisualKit.Part(n, VisualKit.Box(0.16f, 0.05f, 0.03f), g, new Vector3(0.18f, 0.38f, 0), new Vector3(0, 0, -25));
                n.AddChild(new OmniLight3D { LightColor = new Color(0.8f, 0.4f, 1f), LightEnergy = 1.2f, OmniRange = 3f, ShadowEnabled = false });
                return n;
            }
            case "chocalho":
                VisualKit.Part(n, VisualKit.Sphere(0.11f), VisualKit.Mat(new Color(0.4f, 0.75f, 0.95f), 0.4f), Vector3.Zero);
                VisualKit.Part(n, VisualKit.Cylinder(0.02f, 0.02f, 0.2f, 6), VisualKit.Mat(new Color(0.95f, 0.85f, 0.4f), 0.4f), new Vector3(0, 0, -0.15f), new Vector3(90, 0, 0));
                return n;
            case "esporos" or "esporo_grande" or "chuva_de_esporos":
            {
                var g = VisualKit.Glow(new Color(0.75f, 0.35f, 1f), 2.4f);
                g.Transparency = BaseMaterial3D.TransparencyEnum.Alpha; g.AlbedoColor = new Color(0.75f, 0.35f, 1f, 0.75f);
                var r = k == "esporo_grande" ? 0.28f : 0.17f;
                VisualKit.Part(n, VisualKit.Sphere(r), g, Vector3.Zero);
                for (var i = 0; i < 4; i++) VisualKit.Part(n, VisualKit.Sphere(r * 0.35f), g, new Vector3(Mathf.Sin(i * 1.6f) * r * 1.4f, Mathf.Cos(i * 1.6f) * r * 1.4f, -0.1f * i));
                Rastro(new Color(0.7f, 0.3f, 1f, 0.3f), r, 1f);
                return n;
            }
            case "malabares_faca":
                VisualKit.Part(n, VisualKit.Box(0.04f, 0.01f, 0.45f), VisualKit.Mat(new Color(0.8f, 0.82f, 0.86f), 0.15f, 0.95f), Vector3.Zero);
                VisualKit.Part(n, VisualKit.Box(0.05f, 0.03f, 0.12f), VisualKit.Mat(new Color(0.5f, 0.08f, 0.1f), 0.6f), new Vector3(0, 0, -0.26f));
                Rastro(new Color(1f, 1f, 1f, 0.25f), 0.05f, 0.9f);
                return n;
            case "torta_na_cara" or "tortas_em_leque":
                VisualKit.Part(n, VisualKit.Cylinder(0.24f, 0.2f, 0.1f, 16), VisualKit.Mat(new Color(0.8f, 0.6f, 0.3f), 0.8f), Vector3.Zero);
                VisualKit.Part(n, VisualKit.Sphere(0.22f), VisualKit.Mat(new Color(0.98f, 0.96f, 0.92f), 0.5f), new Vector3(0, 0.06f, 0), null, new Vector3(1f, 0.45f, 1f));
                VisualKit.Part(n, VisualKit.Sphere(0.05f), VisualKit.Mat(new Color(0.85f, 0.1f, 0.15f), 0.3f), new Vector3(0, 0.15f, 0));
                return n;
            case "espinho_venenoso":
                VisualKit.Part(n, VisualKit.Cylinder(0f, 0.06f, 0.5f, 6), VisualKit.Mat(new Color(0.3f, 0.45f, 0.15f), 0.6f), Vector3.Zero, new Vector3(90, 0, 0));
                VisualKit.Part(n, VisualKit.Sphere(0.05f), VisualKit.Glow(new Color(0.6f, 1f, 0.2f), 2f), new Vector3(0, 0, 0.22f));
                Rastro(new Color(0.55f, 0.95f, 0.25f, 0.3f), 0.08f, 0.9f);
                return n;
            case "bolhas_toxicas":
            {
                var lodo = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.18f, 0.05f, 0.8f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f,
                    EmissionEnabled = true, Emission = new Color(0.25f, 0.7f, 0.1f), EmissionEnergyMultiplier = 0.9f };
                VisualKit.Part(n, VisualKit.Sphere(0.26f), lodo, Vector3.Zero);
                VisualKit.Part(n, VisualKit.Sphere(0.08f), VisualKit.Glow(new Color(0.6f, 1f, 0.3f), 2f), new Vector3(0.08f, 0.1f, 0.12f));
                return n;
            }
            case "corvos":
            {
                // 18/11: corvo do espantalho — corpo preto, bico amarelo, asas abertas (faz curva atrás do jogador)
                var pena = VisualKit.Mat(new Color(0.06f, 0.06f, 0.08f), 0.7f);
                VisualKit.Part(n, VisualKit.Sphere(0.13f), pena, Vector3.Zero, null, new Vector3(0.8f, 0.75f, 1.5f));
                VisualKit.Part(n, VisualKit.Sphere(0.08f), pena, new Vector3(0, 0.05f, 0.18f));
                VisualKit.Part(n, VisualKit.Cylinder(0f, 0.025f, 0.09f, 4), VisualKit.Mat(new Color(0.85f, 0.65f, 0.15f), 0.5f), new Vector3(0, 0.04f, 0.29f), new Vector3(90, 0, 0));
                foreach (var lado in new[] { -1f, 1f })
                    VisualKit.Part(n, VisualKit.Box(0.42f, 0.02f, 0.18f), pena, new Vector3(lado * 0.24f, 0.05f, -0.02f), new Vector3(0, 0, lado * -22f));
                VisualKit.Part(n, VisualKit.Box(0.12f, 0.015f, 0.16f), pena, new Vector3(0, 0, -0.22f));
                VisualKit.Part(n, VisualKit.Sphere(0.018f), VisualKit.Glow(new Color(1f, 0.25f, 0.1f), 3f), new Vector3(0.04f, 0.08f, 0.22f));
                return n;
            }
            case "corrente_espectral":
            {
                // 18/11: corrente do fantasma — elos de luz fria e um gancho na ponta (puxa quem acerta)
                var luz = VisualKit.Glow(new Color(0.55f, 0.95f, 1f), 3f);
                for (var i = 0; i < 4; i++)
                    VisualKit.Part(n, new TorusMesh { InnerRadius = 0.05f, OuterRadius = 0.09f, Rings = 12, RingSegments = 6 }, luz, new Vector3(0, 0, -0.18f * i), new Vector3(0, 90 * (i % 2), 90));
                VisualKit.Part(n, VisualKit.Cylinder(0f, 0.09f, 0.22f, 5), luz, new Vector3(0, 0, 0.16f), new Vector3(90, 0, 0));
                Rastro(new Color(0.5f, 0.9f, 1f, 0.3f), 0.1f, 1.2f);
                return n;
            }
        }
        n.Free();
        return null;
    }

    private static Node3D CreateProjectile(ProjectileView p)
    {
        // raiz girada para a direção do voo (frente = +Z); a cauda fica atrás, deitada no eixo Z
        var root = new Node3D { Rotation = new Vector3(0, Mathf.Atan2((float)p.DirX, (float)p.DirY), 0) };
        if (!p.Hostile && Efeitos.PoderesVfx.Projetil(p.Kind) is { } vfx) { root.AddChild(vfx); return root; }   // gancho VFX 14/11
        if (!p.Hostile && (p.Kind.Contains("arqueiro") || p.Kind.Contains("batedor")))
        {
            // flecha: haste, ponta de ferro e penas; a de emboscada deixa um rastro claro
            var big = p.Kind.Contains("emboscada");
            VisualKit.Part(root, VisualKit.Cylinder(0.012f, 0.012f, 0.8f, 5), VisualKit.Mat(new Color(0.45f, 0.32f, 0.18f), 0.8f), Vector3.Zero, new Vector3(90, 0, 0));
            VisualKit.Part(root, VisualKit.Cylinder(0f, 0.03f, 0.1f, 4), VisualKit.Mat(new Color(0.6f, 0.62f, 0.66f), 0.3f, 0.8f), new Vector3(0, 0, 0.44f), new Vector3(90, 0, 0));
            VisualKit.Part(root, VisualKit.Box(0.005f, 0.06f, 0.14f), VisualKit.Mat(new Color(0.85f, 0.82f, 0.75f), 0.9f), new Vector3(0, 0, -0.34f));
            if (big)
                VisualKit.Part(root, VisualKit.Cylinder(0.05f, 0.005f, 1.4f, 6), VisualKit.Unshaded(new Color(0.9f, 0.95f, 1f, 0.35f), true), new Vector3(0, 0, -0.8f), new Vector3(90, 0, 0));
            return root;
        }
        if (!p.Hostile && p.Kind.Contains("lanca_gelo"))
        {
            // 12/11: estaca de gelo — cristal comprido de ponta afiada, núcleo claro e rastro de névoa fria
            var gelo = VisualKit.Glow(new Color(0.55f, 0.85f, 1f), 2.6f);
            VisualKit.Part(root, VisualKit.Cylinder(0f, 0.16f, 0.55f, 6), gelo, new Vector3(0, 0, 0.55f), new Vector3(90, 0, 0));
            VisualKit.Part(root, VisualKit.Cylinder(0.16f, 0.11f, 0.9f, 6), gelo, new Vector3(0, 0, -0.17f), new Vector3(90, 0, 0));
            VisualKit.Part(root, VisualKit.Cylinder(0.06f, 0.06f, 1.3f, 6), VisualKit.Glow(new Color(0.9f, 0.97f, 1f), 4f), new Vector3(0, 0, 0.05f), new Vector3(90, 0, 0));
            foreach (var (x, y) in new[] { (0.13f, 0.06f), (-0.12f, -0.05f), (0.02f, 0.14f) })
                VisualKit.Part(root, VisualKit.Cylinder(0f, 0.05f, 0.35f, 4), gelo, new Vector3(x, y, -0.2f), new Vector3(80, x * 120f, 0));
            VisualKit.Part(root, VisualKit.Cylinder(0.25f, 0.02f, 1.6f, 8), VisualKit.Unshaded(new Color(0.75f, 0.9f, 1f, 0.35f), true), new Vector3(0, 0, -1.3f), new Vector3(90, 0, 0));
            root.AddChild(new OmniLight3D { LightColor = new Color(0.55f, 0.8f, 1f), LightEnergy = 1.6f, OmniRange = 4f, ShadowEnabled = false });
            return root;
        }
        if (!p.Hostile && p.Kind.EndsWith("fenix"))
        {
            // fênix: corpo de fogo com asas abertas e rastro
            var fire = VisualKit.Glow(new Color(1f, 0.5f, 0.12f), 4f);
            VisualKit.Part(root, VisualKit.Sphere(0.35f), fire, Vector3.Zero);
            VisualKit.Part(root, VisualKit.Sphere(0.18f), VisualKit.Glow(new Color(1f, 0.85f, 0.4f), 5f), new Vector3(0, 0.15f, 0.35f));
            foreach (var side in new[] { -1f, 1f })
                VisualKit.Part(root, VisualKit.Box(1.3f, 0.05f, 0.5f), fire, new Vector3(side * 0.75f, 0.1f, -0.1f), new Vector3(0, side * -15f, side * 18f));
            VisualKit.Part(root, VisualKit.Cylinder(0.35f, 0.02f, 2.2f, 8), VisualKit.Unshaded(new Color(1f, 0.45f, 0.1f, 0.5f), true), new Vector3(0, 0, -1.3f), new Vector3(90, 0, 0));
            root.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.5f, 0.15f), LightEnergy = 3f, OmniRange = 6f });
            return root;
        }
        if (p.Hostile && p.Kind.Contains("pedregulho"))
        {
            // 13/11 ("o arremesso do Urso é legal, mas não dá para entender"): pedregulho grande, girando, com rastro de poeira
            var rocha = VisualKit.Mat(new Color(0.45f, 0.42f, 0.38f), 0.95f);
            var pedra = VisualKit.Part(root, VisualKit.Sphere(0.45f), rocha, Vector3.Zero, new Vector3(20, 30, 0), new Vector3(1.1f, 0.85f, 1f));
            VisualKit.Part(pedra, VisualKit.Sphere(0.22f), rocha, new Vector3(0.3f, 0.15f, 0.1f));
            VisualKit.Part(pedra, VisualKit.Sphere(0.18f), rocha, new Vector3(-0.25f, -0.1f, 0.2f));
            VisualKit.Part(root, VisualKit.Cylinder(0.4f, 0.03f, 1.6f, 8), VisualKit.Unshaded(new Color(0.7f, 0.62f, 0.5f, 0.35f), true), new Vector3(0, 0, -0.9f), new Vector3(90, 0, 0));
            return root;
        }
        // 13/11: projéteis do conteúdo novo (coco, pedrinha de estilingue, nota de rabeca, chocalho, esporos, facas, torta,
        // espinho da Dama Urtiga, bolhas de lodo do tritão) — cada um com a sua cara, para dar para ler o que vem
        if (p.Hostile && FabProjetil(p.Kind) is { } fab) { root.AddChild(fab); return root; }
        if (p.Hostile && p.Kind.Contains("espinho") && GlbCache.Instance("proj_espinho_pedra") is { } spike)
        {
            // espinho do Caranguejo de Pedra (lote 2): ponta para a frente (+Z do projétil)
            root.AddChild(spike);
            VisualKit.Part(root, VisualKit.Cylinder(0.08f, 0.01f, 0.7f, 6), VisualKit.Unshaded(new Color(0.7f, 0.68f, 0.6f, 0.3f)), new Vector3(0, 0, -0.45f), new Vector3(90, 0, 0));
            return root;
        }
        if (p.Hostile && p.Kind.EndsWith("areia") && GlbCache.Instance("proj_bola_areia") is { } sand)
        {
            root.AddChild(sand);
            VisualKit.Part(root, VisualKit.Cylinder(0.2f, 0.02f, 0.9f, 8), VisualKit.Unshaded(new Color(0.75f, 0.65f, 0.48f, 0.3f)), new Vector3(0, 0, -0.5f), new Vector3(90, 0, 0));
            return root;
        }
        if (p.Hostile && p.Kind.Contains("flech"))
        {
            // flecha de goblin: haste escura e ponta de pedra
            VisualKit.Part(root, VisualKit.Cylinder(0.014f, 0.014f, 0.75f, 5), VisualKit.Mat(new Color(0.3f, 0.22f, 0.14f), 0.8f), Vector3.Zero, new Vector3(90, 0, 0));
            VisualKit.Part(root, VisualKit.Cylinder(0f, 0.035f, 0.12f, 4), VisualKit.Mat(new Color(0.35f, 0.35f, 0.33f), 0.6f), new Vector3(0, 0, 0.42f), new Vector3(90, 0, 0));
            VisualKit.Part(root, VisualKit.Box(0.005f, 0.06f, 0.12f), VisualKit.Mat(new Color(0.5f, 0.15f, 0.12f), 0.9f), new Vector3(0, 0, -0.32f));
            return root;
        }
        if (p.Hostile && (p.Kind.EndsWith("areia") || p.Kind.EndsWith("terra") || p.Kind.Contains("rochas") || p.Kind.EndsWith("escoria")))
        {
            // torrão de areia/terra: pedrinhas e poeira atrás, sem brilho
            var dirt = VisualKit.Mat(p.Kind.EndsWith("areia") ? new Color(0.78f, 0.68f, 0.48f) : new Color(0.42f, 0.33f, 0.24f), 1f);
            VisualKit.Part(root, VisualKit.Sphere(0.16f), dirt, Vector3.Zero);
            VisualKit.Part(root, VisualKit.Sphere(0.09f), dirt, new Vector3(0.12f, 0.05f, -0.15f));
            VisualKit.Part(root, VisualKit.Sphere(0.07f), dirt, new Vector3(-0.1f, -0.04f, -0.25f));
            VisualKit.Part(root, VisualKit.Cylinder(0.2f, 0.02f, 0.9f, 8), VisualKit.Unshaded(new Color(0.75f, 0.65f, 0.48f, 0.3f)), new Vector3(0, 0, -0.5f), new Vector3(90, 0, 0));
            return root;
        }
        if (p.Hostile && (p.Kind.EndsWith("teia") || p.Kind.Contains("gelo")))
        {
            var ice = p.Kind.Contains("gelo");
            var c = ice ? new Color(0.6f, 0.85f, 1f) : new Color(0.92f, 0.92f, 0.88f);
            VisualKit.Part(root, VisualKit.Sphere(0.22f), ice ? VisualKit.Glow(c, 2f) : VisualKit.Mat(c, 0.9f), Vector3.Zero).Scale = new Vector3(1f, 0.6f, 1.3f);
            VisualKit.Part(root, VisualKit.Cylinder(0.18f, 0.02f, 0.9f, 8), VisualKit.Unshaded(new Color(c.R, c.G, c.B, 0.35f), true), new Vector3(0, 0, -0.5f), new Vector3(90, 0, 0));
            return root;
        }
        var color = p.Hostile ? (p.Kind.EndsWith("olhar") ? new Color(1f, 0.1f, 0.15f) : new Color(1f, 0.35f, 0.1f))
            : p.Kind.Contains("bruxa") ? new Color(0.35f, 1f, 0.55f) : new Color(1f, 0.7f, 0.25f);
        VisualKit.Part(root, VisualKit.Sphere(p.Hostile ? 0.32f : 0.2f), VisualKit.Glow(color, 5f), Vector3.Zero);
        VisualKit.Part(root, VisualKit.Cylinder(p.Hostile ? 0.25f : 0.15f, 0.02f, 1.1f, 8), VisualKit.Unshaded(new Color(color.R, color.G, color.B, 0.45f), true),
            new Vector3(0, 0, -0.6f), new Vector3(90, 0, 0));
        root.AddChild(new OmniLight3D { LightColor = color, LightEnergy = 1.5f, OmniRange = 3.5f });
        return root;
    }

    private void Prune(Dictionary<string, EntityState> states, HashSet<string> seen)
    {
        foreach (var id in states.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            states[id].Visual.Dispose();
            states[id].Telegraph?.Root.QueueFree();
            states[id].Beam?.QueueFree();
            states[id].Chicote?.QueueFree();
            states[id].Barrel?.QueueFree();
            states[id].Mound?.QueueFree();
            states.Remove(id);
        }
    }

    private void ClearWorld()
    {
        foreach (var s in _players.Values.Concat(_monsters.Values))
        {
            s.Visual.Dispose();
            s.Telegraph?.Root.QueueFree();
            s.Barrel?.QueueFree();
            s.Mound?.QueueFree();
        }
        _players.Clear();
        _monsters.Clear();
        foreach (var i in _items.Values) i.Root.QueueFree();
        _items.Clear();
        foreach (var p in _projectiles.Values) p.Root.QueueFree();
        _projectiles.Clear();
        foreach (var pet in _pets.Values) pet.Visual.Dispose();
        _pets.Clear();
        _hud.ClearTransient();
        _effects.Clear();
        _mapView?.Root.QueueFree();
        _mapView = null;
        _arena?.Root.QueueFree();
        _arena = null;
        _lastLevel = 0;
        _lockedId = null;
        _queued = null;
        _zoneId = null;
        _aimMarker.Visible = false;
        _lockMarker.Visible = false;
    }
}
