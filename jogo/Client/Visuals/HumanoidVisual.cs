using System;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>Aparência de um humanoide articulado (só visual).</summary>
public sealed class HumanoidStyle
{
    public float Scale = 1f;
    public Color Skin = new(0.25f, 0.19f, 0.16f);     // madeira escura do boneco (pacote visual v1)
    /// <summary>Juntas e cabeça cor de osso (#C8BBA2), como nas pranchas. null = derivado da pele.</summary>
    public Color? JointColor = new Color(0.62f, 0.47f, 0.30f); // juntas de bronze gasto (prancha 07)
    public Color? HeadColor = new Color(0.74f, 0.67f, 0.55f);
    /// <summary>Membros finos (jogadores e NPCs); monstros humanoides grandes usam 1.</summary>
    public float LimbThickness = 0.78f;
    public bool Horns;          // minotauro, draugr rei
    public bool Crown;          // reis e regentes
    public Color? BodyGlow;     // elemental, espectro: corpo que brilha
    public Color Cloth = new(0.30f, 0.38f, 0.52f);
    public Color Accent = new(0.85f, 0.70f, 0.30f);
    public Color Eyes = new(0.08f, 0.08f, 0.09f);
    public bool EyesGlow;
    public bool Robe;          // saia longa (Acólito, Mago)
    public bool Hood;
    public bool Bulky;         // ombreiras e tronco largo (Capataz)
    public float Hunch;        // inclinação permanente para a frente (Saqueador)
    public string? FixedWeapon; // arma própria do monstro (não vem de equipamento)
    public bool CasterHands;   // mãos brilhando (Acólito)
    public bool Cape;          // capa bordô desfiada nas costas (Saqueador, prancha 04)
    /// <summary>E9 (jogadores): sem armadura no corpo, aparece só de cueca opaca; a roupa vem dos equipamentos.</summary>
    public bool Underwear;
    // traços do inimigo feito no manequim (MannequinVisual.Monster)
    public bool Ears;          // orelha pontuda (goblin)
    public bool Nose;          // nariz comprido (goblin)
    public bool Tusks;         // presas (troll, orc)
    public bool Beard;         // barba (necromante)
    public float Girth;        // largura do corpo (0 = automático)
    public float HeadScale;    // cabeça maior (goblin); 0 = normal
    public bool Bone;          // esqueleto: corpo cor de osso liso (sem a madeira do manequim) e costelas
    // 13/11 ("mobs femininas: harpias, vampiras, esfinge, medusa"): corpo feminino do manequim + traços
    public bool Female;
    public Color? HairColor;   // cabelo solto (o manequim feminino é careca)
    public bool SnakeHair;     // medusa: serpentes no lugar do cabelo
    public bool Nemes;         // esfinge: toucado listrado de faraó
    public string? Wings;      // asas nas costas ("aguia" = penas, "abismo" = morcego)
    public bool Talons;        // harpia: garras nos pés e penas nos braços
    public Color UnderwearColor = new(0.22f, 0.16f, 0.12f);
}

/// <summary>
/// Boneco humanoide de juntas (cabeça, tronco, braços e pernas articulados) feito de cápsulas, esferas e caixas.
/// Animação procedural: passos pela velocidade; poses de preparação/golpe/recuperação pela ação do servidor;
/// recuo ao apanhar; rolamento na esquiva; queda na morte. Frente = +Z.
/// </summary>
public sealed class HumanoidVisual : IPlayerVisual
{
    private struct Pose
    {
        public float BodyY, TorsoX, TorsoY, HeadX, Crouch;
        public Vector3 ShoulderR, ShoulderL;
        public float ElbowR, ElbowL, HipR, HipL, KneeR, KneeL;
        public float StaffPitch;

        public static Pose Lerp(Pose a, Pose b, float t)
        {
            t = Mathf.Clamp(t, 0f, 1f);
            return new Pose
            {
                BodyY = Mathf.Lerp(a.BodyY, b.BodyY, t), TorsoX = Mathf.Lerp(a.TorsoX, b.TorsoX, t),
                TorsoY = Mathf.Lerp(a.TorsoY, b.TorsoY, t), HeadX = Mathf.Lerp(a.HeadX, b.HeadX, t),
                Crouch = Mathf.Lerp(a.Crouch, b.Crouch, t),
                ShoulderR = a.ShoulderR.Lerp(b.ShoulderR, t), ShoulderL = a.ShoulderL.Lerp(b.ShoulderL, t),
                ElbowR = Mathf.Lerp(a.ElbowR, b.ElbowR, t), ElbowL = Mathf.Lerp(a.ElbowL, b.ElbowL, t),
                HipR = Mathf.Lerp(a.HipR, b.HipR, t), HipL = Mathf.Lerp(a.HipL, b.HipL, t),
                KneeR = Mathf.Lerp(a.KneeR, b.KneeR, t), KneeL = Mathf.Lerp(a.KneeL, b.KneeL, t),
                StaffPitch = Mathf.Lerp(a.StaffPitch, b.StaffPitch, t),
            };
        }
    }

    private readonly HumanoidStyle _style;
    private readonly VisualKit.FlashGroup _flash = new();
    private readonly Node3D _scaleRoot, _body, _hips, _torso, _head, _shoulderR, _shoulderL, _elbowR, _elbowL,
        _handR, _handL, _hipR, _hipL, _kneeR, _kneeL, _weaponSocket, _shieldSocket, _armorRoot, _orb;
    private Node3D? _weapon, _shield, _helmet;
    private readonly Node3D _headSocket;
    private string? _weaponModel, _offhandModel, _bodyModel, _headModel;
    private Pose _pose;
    private float _phase, _time, _hitT = 1f, _deathT, _fade, _blinkFlash;
    private Vector3 _hitDir = Vector3.Back;
    private bool _wasAlive = true;
    private string _lastAction = "";

    public Node3D Root { get; } = new() { Name = "Humanoide" };
    public float Height { get; }
    /// <summary>Direção local do movimento (para o rolamento da esquiva), definida pelo Main.</summary>
    public Vector3 MoveDirLocal { get; set; } = Vector3.Back;

    public HumanoidVisual(HumanoidStyle style)
    {
        _style = style;
        Height = 1.85f * style.Scale;
        _scaleRoot = VisualKit.Joint(Root, Vector3.Zero, "Escala");
        _scaleRoot.Scale = Vector3.One * style.Scale;
        _body = VisualKit.Joint(_scaleRoot, new Vector3(0, 0.55f, 0), "Corpo");
        _hips = VisualKit.Joint(_body, new Vector3(0, 0.40f, 0), "Quadril");

        var skin = _flash.Add(style.BodyGlow is { } bg ? VisualKit.Glow(bg, 1.1f) : VisualKit.Mat(style.Skin, 0.75f));
        // de cueca: o "pano" do corpo vira pele, e a pelve vira a cueca (opaca, cobre bem)
        var cloth = style.Underwear ? skin : _flash.Add(VisualKit.Mat(style.Cloth, 0.85f));
        var under = style.Underwear ? _flash.Add(VisualKit.Mat(style.UnderwearColor, 0.9f)) : cloth;
        var accent = _flash.Add(VisualKit.Mat(style.Accent, 0.5f, 0.4f));
        var joints = _flash.Add(VisualKit.Mat(style.JointColor ?? style.Skin.Darkened(0.25f), 0.6f, 0.15f));
        var headMat = style.HeadColor is { } hc ? _flash.Add(VisualKit.Mat(hc, 0.55f)) : skin;
        var lt = style.LimbThickness;
        var widthMul = style.Bulky ? 1.35f : 1f;

        // pelve e tronco
        VisualKit.Part(_hips, VisualKit.Box(0.34f * widthMul, 0.2f, 0.23f), under, new Vector3(0, -0.02f, 0));
        _torso = VisualKit.Joint(_hips, new Vector3(0, 0.08f, 0), "Tronco");
        VisualKit.Part(_torso, VisualKit.Capsule(0.15f, 0.5f), skin, new Vector3(0, 0.24f, 0), null, new Vector3((0.85f + 0.4f * lt) * widthMul * 0.95f, 1f, 0.70f));
        // peito anguloso (dá leitura de "boneco de juntas" de frente) e junta da cintura
        VisualKit.Part(_torso, VisualKit.Box(0.30f * widthMul, 0.16f, 0.18f), skin, new Vector3(0, 0.40f, 0.01f), new Vector3(8, 0, 0));
        VisualKit.Part(_torso, VisualKit.Sphere(0.07f), joints, new Vector3(0, 0.04f, 0), null, new Vector3(1.5f, 0.8f, 1.2f));
        if (!style.Underwear)
        {
            VisualKit.Part(_torso, VisualKit.Box(0.40f * widthMul, 0.2f, 0.24f), cloth, new Vector3(0, 0.38f, 0)); // peito/túnica
            VisualKit.Part(_torso, VisualKit.Box(0.30f * widthMul, 0.05f, 0.22f), accent, new Vector3(0, 0.04f, 0)); // cinto
        }
        if (style.Bulky)
            foreach (var side in new[] { -1f, 1f })
                VisualKit.Part(_torso, VisualKit.Sphere(0.15f), accent, new Vector3(side * 0.30f, 0.50f, 0), null, new Vector3(1.2f, 0.7f, 1.1f));

        // cabeça (com viseira/olhos para mostrar a frente)
        var neck = VisualKit.Joint(_torso, new Vector3(0, 0.56f, 0), "Pescoco");
        VisualKit.Part(neck, VisualKit.Cylinder(0.045f, 0.055f, 0.1f), joints, new Vector3(0, 0.03f, 0));
        _head = VisualKit.Joint(neck, new Vector3(0, 0.08f, 0), "Cabeca");
        VisualKit.Part(_head, VisualKit.Sphere(0.13f), headMat, new Vector3(0, 0.12f, 0), null, new Vector3(0.82f, 1.18f, 0.95f));
        // rosto sem traços (cabeça oval): só uma fenda discreta mostra a frente; olhos acesos em quem tem magia/monstros
        var eyeMat = style.EyesGlow ? VisualKit.Glow(style.Eyes, 3f) : VisualKit.Mat(style.HeadColor is { } hcc ? hcc.Darkened(0.35f) : style.Eyes, 0.5f);
        VisualKit.Part(_head, VisualKit.Box(style.EyesGlow ? 0.15f : 0.09f, style.EyesGlow ? 0.03f : 0.015f, 0.04f), eyeMat, new Vector3(0, 0.15f, 0.108f));
        if (style.Horns)
            foreach (var side in new[] { -1f, 1f })
                VisualKit.Part(_head, VisualKit.Cylinder(0f, 0.05f, 0.3f, 5), joints, new Vector3(side * 0.12f, 0.24f, 0.02f), new Vector3(-20, 0, side * -55));
        if (style.Crown)
            for (var i = 0; i < 5; i++)
                VisualKit.Part(_head, VisualKit.Cylinder(0f, 0.035f, 0.12f, 4), _flash.Add(VisualKit.Mat(new Color(0.62f, 0.45f, 0.24f), 0.4f, 0.7f)),
                    new Vector3(Mathf.Cos(i * 1.256f) * 0.1f, 0.27f, Mathf.Sin(i * 1.256f) * 0.1f));
        if (style.Hood)
        {
            VisualKit.Part(_head, VisualKit.Cylinder(0.02f, 0.18f, 0.36f), cloth, new Vector3(0, 0.2f, -0.02f), new Vector3(-12, 0, 0));
        }

        // braços
        (_shoulderR, _elbowR, _handR) = Arm(-1f, skin, joints, style.Underwear ? joints : cloth, widthMul * lt);
        (_shoulderL, _elbowL, _handL) = Arm(1f, skin, joints, style.Underwear ? joints : cloth, widthMul * lt);
        _weaponSocket = VisualKit.Joint(_handR, Vector3.Zero, "Arma");
        _shieldSocket = VisualKit.Joint(_elbowL, new Vector3(0.08f, -0.14f, 0.02f), "Escudo");
        _shieldSocket.RotationDegrees = new Vector3(0, 90, 0);
        _orb = VisualKit.Joint(_torso, new Vector3(0, 0.3f, 0.45f), "Orbe");
        VisualKit.Part(_orb, VisualKit.Sphere(0.12f), VisualKit.Glow(new Color(1f, 0.5f, 0.15f), 4f), Vector3.Zero);
        _orb.Visible = false;
        if (style.CasterHands)
            foreach (var hand in new[] { _handR, _handL })
                VisualKit.Part(hand, VisualKit.Sphere(0.06f), VisualKit.Glow(new Color(1f, 0.45f, 0.1f), 3f), Vector3.Zero);

        // pernas
        (_hipR, _kneeR) = Leg(-1f, skin, joints, cloth, lt);
        (_hipL, _kneeL) = Leg(1f, skin, joints, cloth, lt);
        _headSocket = VisualKit.Joint(_head, Vector3.Zero, "ElmoSocket");

        _armorRoot = VisualKit.Joint(_hips, Vector3.Zero, "Armadura");
        if (style.Robe) BuildRobe(_armorRoot, cloth, accent, 1.0f);
        if (style.Cape)
        {
            var capeMat = _flash.Add(VisualKit.Mat(style.Cloth, 0.92f));
            VisualKit.Part(_torso, VisualKit.Box(0.42f, 0.70f, 0.03f), capeMat, new Vector3(0, 0.18f, -0.16f), new Vector3(-10, 0, 0));
            VisualKit.Part(_torso, VisualKit.Box(0.46f, 0.10f, 0.24f), capeMat, new Vector3(0, 0.52f, -0.04f)); // gola sobre os ombros
            for (var i = 0; i < 5; i++)
                VisualKit.Part(_torso, VisualKit.Cylinder(0.05f, 0f, 0.14f + (i % 2) * 0.08f, 3), capeMat, new Vector3(-0.17f + i * 0.085f, -0.24f - (i % 2) * 0.04f, -0.22f));
        }
        if (style.Hunch > 0) _torso.Rotation = new Vector3(style.Hunch, 0, 0);
        if (style.FixedWeapon is not null) SetWeapon(style.FixedWeapon);
        _pose = IdlePose();
    }

    private (Node3D Shoulder, Node3D Elbow, Node3D Hand) Arm(float side, Material skin, Material joints, Material cloth, float widthMul)
    {
        var shoulder = VisualKit.Joint(_torso, new Vector3(side * 0.24f * widthMul, 0.47f, 0), side < 0 ? "OmbroD" : "OmbroE");
        VisualKit.Part(shoulder, VisualKit.Sphere(0.07f), cloth, Vector3.Zero);
        VisualKit.Part(shoulder, VisualKit.Capsule(0.055f * widthMul, 0.32f), skin, new Vector3(0, -0.15f, 0));
        var elbow = VisualKit.Joint(shoulder, new Vector3(0, -0.30f, 0), side < 0 ? "CotoveloD" : "CotoveloE");
        VisualKit.Part(elbow, VisualKit.Sphere(0.05f), joints, Vector3.Zero);
        VisualKit.Part(elbow, VisualKit.Capsule(0.05f * widthMul, 0.28f), skin, new Vector3(0, -0.13f, 0));
        var hand = VisualKit.Joint(elbow, new Vector3(0, -0.28f, 0), side < 0 ? "MaoD" : "MaoE");
        VisualKit.Part(hand, VisualKit.Sphere(0.055f), joints, Vector3.Zero, null, new Vector3(1f, 1.2f, 0.8f));
        return (shoulder, elbow, hand);
    }

    private (Node3D Hip, Node3D Knee) Leg(float side, Material skin, Material joints, Material cloth, float lt = 1f)
    {
        var hip = VisualKit.Joint(_hips, new Vector3(side * 0.1f, -0.05f, 0), side < 0 ? "QuadrilD" : "QuadrilE");
        VisualKit.Part(hip, VisualKit.Sphere(0.06f * lt), joints, Vector3.Zero);
        VisualKit.Part(hip, VisualKit.Capsule(0.075f * lt, 0.44f), _style.Underwear ? skin : cloth, new Vector3(0, -0.21f, 0));
        var knee = VisualKit.Joint(hip, new Vector3(0, -0.43f, 0), side < 0 ? "JoelhoD" : "JoelhoE");
        VisualKit.Part(knee, VisualKit.Sphere(0.062f * Math.Max(lt, 0.85f)), joints, Vector3.Zero);
        VisualKit.Part(knee, VisualKit.Capsule(0.06f * lt, 0.42f), skin, new Vector3(0, -0.21f, 0));
        // pé nu simplificado (cor de junta); botas vêm de equipamento no futuro
        VisualKit.Part(knee, VisualKit.Sphere(0.045f), joints, new Vector3(0, -0.41f, 0));
        VisualKit.Part(knee, VisualKit.Box(0.09f, 0.05f, 0.2f), skin, new Vector3(0, -0.445f, 0.05f));
        return (hip, knee);
    }

    private static void BuildRobe(Node3D parent, Material cloth, Material accent, float length)
    {
        VisualKit.Part(parent, VisualKit.Cylinder(0.19f, 0.34f, 0.85f * length, 10), cloth, new Vector3(0, -0.42f * length, 0));
        VisualKit.Part(parent, VisualKit.Cylinder(0.345f, 0.345f, 0.04f, 10), accent, new Vector3(0, -0.84f * length, 0));
    }

    // ------------------------------------------------------------ equipamentos

    public void SetGear(string? weapon, string? offhand, string? body)
    {
        if (_style.FixedWeapon is null && weapon != _weaponModel) SetWeapon(weapon);
        if (offhand != _offhandModel)
        {
            _offhandModel = offhand;
            _shield?.QueueFree();
            _shield = GearFactory.Shield(offhand);
            if (_shield is not null) _shieldSocket.AddChild(_shield);
        }
        if (body != _bodyModel)
        {
            _bodyModel = body;
            foreach (var child in _armorRoot.GetChildren()) child.QueueFree();
            ClearTorsoArmor();
            BuildArmor(body);
        }
    }

    /// <summary>Peça de cabeça (E9). null = cabeça à mostra.</summary>
    public void SetHead(string? model)
    {
        if (model == _headModel) return;
        _headModel = model;
        _helmet?.QueueFree();
        _helmet = GearFactory.Helmet(model);
        if (_helmet is not null) _headSocket.AddChild(_helmet);
    }

    private void SetWeapon(string? model)
    {
        _weaponModel = model;
        _weapon?.QueueFree();
        _weapon = GearFactory.Weapon(model);
        if (_weapon is null) return;
        _weaponSocket.AddChild(_weapon);
        // lâmina para a frente e para cima (aparece de qualquer ângulo; nas poses de golpe acompanha o braço)
        if (!GearFactory.IsStaff(model)) _weaponSocket.RotationDegrees = new Vector3(GearFactory.IsTwoHanded(model) ? 30 : 50, 0, 0);
        _weapon.Position = GearFactory.IsStaff(model) ? new Vector3(0, -0.05f, 0.06f) : Vector3.Zero;
    }

    // Paleta do pacote visual v1: ferro #59616B, bordô #633B47, bronze/âmbar, couro, violeta do mago.
    private static readonly Color Iron = new(0.50f, 0.53f, 0.58f), IronDark = new(0.30f, 0.31f, 0.34f), Bordo = new(0.39f, 0.23f, 0.28f),
        Bronze = new(0.66f, 0.48f, 0.26f), LeatherC = new(0.30f, 0.19f, 0.12f), LeatherDark = new(0.18f, 0.12f, 0.08f),
        Violet = new(0.31f, 0.18f, 0.38f), VioletDark = new(0.20f, 0.12f, 0.25f);

    /// <summary>
    /// Conjuntos de armadura (prancha 07). Cada conjunto veste tronco, braços e pernas com peças presas às juntas certas —
    /// as articulações continuam à mostra entre as peças. Ombreiras/braçadeiras/botas seguem os membros na animação.
    /// </summary>
    private void BuildArmor(string? model)
    {
        if (_style.Robe) BuildRobe(_armorRoot, _flash.Add(VisualKit.Mat(_style.Cloth, 0.85f)), _flash.Add(VisualKit.Mat(_style.Accent, 0.5f, 0.4f)), 1f);
        Material M(Color c, float rough = 0.85f, float metal = 0f) => _flash.Add(VisualKit.Mat(c, rough, metal));
        switch (model)
        {
            case "body_gambeson":
            {
                // "Primeiras peças": colete de couro, alça diagonal, cinto com bolsa, braçadeiras e botas
                var leather = M(LeatherC, 0.9f);
                var dark = M(LeatherDark, 0.8f);
                var buckle = M(Bronze, 0.4f, 0.7f);
                AttachToTorso(VisualKit.Box(0.38f, 0.34f, 0.26f), leather, new Vector3(0, 0.30f, 0));
                AttachToTorso(VisualKit.Box(0.07f, 0.62f, 0.28f), dark, new Vector3(0.02f, 0.30f, 0.005f));
                _armorParts[^1].RotationDegrees = new Vector3(0, 0, 32);
                AttachToTorso(VisualKit.Box(0.36f, 0.06f, 0.27f), dark, new Vector3(0, 0.08f, 0));
                AttachToTorso(VisualKit.Box(0.06f, 0.06f, 0.03f), buckle, new Vector3(0, 0.08f, 0.14f));
                Wear(_hips, VisualKit.Box(0.10f, 0.12f, 0.07f), dark, new Vector3(0.15f, -0.03f, 0.10f));
                Bracers(leather, buckle);
                Boots(dark, 0.17f);
                break;
            }
            case "body_rags":
            {
                // trapos (primeiras peças do mago): túnica curta desfiada e corda na cintura
                var cloth = M(new Color(0.33f, 0.22f, 0.32f), 0.95f);
                var rope = M(new Color(0.55f, 0.45f, 0.32f), 0.95f);
                AttachToTorso(VisualKit.Box(0.36f, 0.40f, 0.25f), cloth, new Vector3(0, 0.30f, 0));
                AttachToTorso(VisualKit.Cylinder(0.17f, 0.17f, 0.04f, 10), rope, new Vector3(0, 0.08f, 0));
                Wear(_armorRoot, VisualKit.Cylinder(0.20f, 0.25f, 0.20f, 10), cloth, new Vector3(0, -0.06f, 0));
                Fringe(_armorRoot, cloth, 0.25f, -0.16f, 9, 0.16f);
                foreach (var e in new[] { _elbowR, _elbowL })
                    Wear(e, VisualKit.Cylinder(0.062f, 0.058f, 0.08f, 8), rope, new Vector3(0, -0.22f, 0));
                break;
            }
            case "body_mail":
            {
                // "Ferro de Vigília": peitoral anguloso, ombreiras em lâminas, tabardo bordô com tridente, braçadeiras, grevas e botas
                var iron = M(Iron, 0.45f, 0.55f);
                var ironD = M(IronDark, 0.5f, 0.5f);
                var tabard = M(Bordo, 0.9f);
                var bronze = M(Bronze, 0.4f, 0.7f);
                AttachToTorso(VisualKit.Box(0.42f, 0.30f, 0.29f), iron, new Vector3(0, 0.36f, 0));
                AttachToTorso(VisualKit.Box(0.34f, 0.18f, 0.26f), ironD, new Vector3(0, 0.15f, 0));
                AttachToTorso(VisualKit.Box(0.30f, 0.04f, 0.30f), bronze, new Vector3(0, 0.06f, 0));
                AttachToTorso(VisualKit.Box(0.03f, 0.26f, 0.02f), ironD, new Vector3(0, 0.37f, 0.15f)); // quilha do peitoral
                Trident(_torso, new Vector3(0, 0.36f, 0.152f), 0.55f, bronze);
                Tabard(tabard, bronze, 0.42f);
                Pauldron(_shoulderR, iron, bronze, 1f, false);
                Pauldron(_shoulderL, iron, bronze, 1f, false);
                Bracers(iron, bronze);
                Gloves(M(LeatherDark, 0.8f));
                Boots(ironD, 0.22f, iron);
                break;
            }
            case "body_plate":
            {
                // conjunto raro: placas escuras, ombreiras com espinho, capa bordô, sino de âmbar no peito
                var plate = M(new Color(0.30f, 0.31f, 0.35f), 0.4f, 0.6f);
                var trim = M(Bronze, 0.4f, 0.7f);
                var cape = M(Bordo.Darkened(0.1f), 0.9f);
                AttachToTorso(VisualKit.Box(0.46f, 0.32f, 0.31f), plate, new Vector3(0, 0.36f, 0));
                AttachToTorso(VisualKit.Box(0.38f, 0.20f, 0.28f), plate, new Vector3(0, 0.14f, 0));
                AttachToTorso(VisualKit.Box(0.47f, 0.035f, 0.32f), trim, new Vector3(0, 0.25f, 0));
                AttachToTorso(VisualKit.Cylinder(0.035f, 0.06f, 0.09f, 8), _flash.Add(VisualKit.Glow(new Color(1f, 0.62f, 0.2f), 1.4f)), new Vector3(0, 0.36f, 0.16f));
                AttachToTorso(VisualKit.Box(0.40f, 0.62f, 0.03f), cape, new Vector3(0, 0.12f, -0.17f));
                _armorParts[^1].RotationDegrees = new Vector3(-8, 0, 0);
                Fringe(_torso, cape, 0.0f, -0.20f, 5, 0.12f, back: true);
                Tabard(cape, trim, 0.46f);
                Pauldron(_shoulderR, plate, trim, 1.2f, true);
                Pauldron(_shoulderL, plate, trim, 1.2f, true);
                Bracers(plate, trim);
                Gloves(plate);
                Boots(plate, 0.26f, trim);
                foreach (var k in new[] { _kneeR, _kneeL })
                    Wear(k, VisualKit.Sphere(0.075f), plate, new Vector3(0, 0.0f, 0.03f), null, new Vector3(1f, 1f, 0.8f));
                break;
            }
            case "body_robe":
            case "body_robe_vein":
            {
                // manto do mago: painéis abertos na frente (as pernas aparecem ao andar), gola alta, faixa e emblema
                var rare = model == "body_robe_vein";
                var robe = M(rare ? VioletDark : Violet, 0.9f);
                var trim = rare ? _flash.Add(VisualKit.Glow(new Color(0.56f, 0.41f, 0.75f), 1.2f)) : M(Bronze, 0.5f, 0.5f);
                AttachToTorso(VisualKit.Box(0.40f, 0.44f, 0.27f), robe, new Vector3(0, 0.29f, 0));
                AttachToTorso(VisualKit.Box(0.05f, 0.44f, 0.28f), trim, new Vector3(0, 0.29f, 0.004f));
                AttachToTorso(VisualKit.Box(0.34f, 0.05f, 0.28f), M(LeatherDark, 0.8f), new Vector3(0, 0.08f, 0.002f));
                AttachToTorso(VisualKit.Cylinder(0.17f, 0.13f, 0.12f, 10), robe, new Vector3(0, 0.56f, -0.02f)); // gola alta
                RobePanels(robe, trim, 1.0f);
                foreach (var sh in new[] { _shoulderR, _shoulderL })
                {
                    Wear(sh, VisualKit.Sphere(0.11f), robe, new Vector3(0, 0.02f, 0), null, new Vector3(1.1f, 0.7f, 1.1f));
                    if (rare) Wear(sh, VisualKit.Cylinder(0f, 0.07f, 0.18f, 4), trim, new Vector3(0, 0.09f, 0), new Vector3(0, 0, 0));
                }
                foreach (var e in new[] { _elbowR, _elbowL })
                    Wear(e, VisualKit.Cylinder(0.06f, 0.11f, 0.22f, 8), robe, new Vector3(0, -0.14f, 0)); // manga larga
                Boots(M(LeatherDark, 0.8f), 0.14f);
                break;
            }
        }
    }

    private void Wear(Node3D at, Mesh mesh, Material mat, Vector3 pos, Vector3? rot = null, Vector3? scale = null)
        => _armorParts.Add(VisualKit.Part(at, mesh, mat, pos, rot, scale));

    private void Bracers(Material m, Material? band = null)
    {
        foreach (var e in new[] { _elbowR, _elbowL })
        {
            Wear(e, VisualKit.Cylinder(0.072f, 0.062f, 0.16f, 8), m, new Vector3(0, -0.17f, 0));
            if (band is not null) Wear(e, VisualKit.Cylinder(0.075f, 0.075f, 0.025f, 8), band, new Vector3(0, -0.10f, 0));
        }
    }

    private void Gloves(Material m)
    {
        foreach (var h in new[] { _handR, _handL })
            Wear(h, VisualKit.Sphere(0.065f), m, Vector3.Zero, null, new Vector3(1f, 1.25f, 0.9f));
    }

    /// <summary>Botas até a canela (o joelho fica à mostra); cano opcional com outra cor.</summary>
    private void Boots(Material m, float height, Material? cuff = null)
    {
        foreach (var k in new[] { _kneeR, _kneeL })
        {
            Wear(k, VisualKit.Cylinder(0.075f, 0.07f, height, 8), m, new Vector3(0, -0.44f + height / 2f, 0));
            Wear(k, VisualKit.Box(0.115f, 0.08f, 0.25f), m, new Vector3(0, -0.445f, 0.05f));
            if (cuff is not null) Wear(k, VisualKit.Cylinder(0.082f, 0.082f, 0.03f, 8), cuff, new Vector3(0, -0.44f + height, 0));
        }
    }

    /// <summary>Ombreira em duas lâminas sobrepostas (opcional: espinho) presa à junta do ombro.</summary>
    private void Pauldron(Node3D shoulder, Material plate, Material edge, float size, bool spike)
    {
        var side = shoulder == _shoulderR ? -1f : 1f;
        Wear(shoulder, VisualKit.Sphere(0.12f * size), plate, new Vector3(side * 0.03f, 0.04f, 0), null, new Vector3(1.25f, 0.62f, 1.15f));
        Wear(shoulder, VisualKit.Box(0.19f * size, 0.04f, 0.22f * size), plate, new Vector3(side * 0.06f, -0.04f, 0), new Vector3(0, 0, side * 28));
        Wear(shoulder, VisualKit.Box(0.20f * size, 0.02f, 0.225f * size), edge, new Vector3(side * 0.07f, -0.07f, 0), new Vector3(0, 0, side * 32));
        if (spike) Wear(shoulder, VisualKit.Cylinder(0f, 0.045f, 0.2f, 4), edge, new Vector3(side * 0.08f, 0.14f, 0), new Vector3(0, 0, side * 25));
    }

    /// <summary>Tabardo na frente e atrás, preso ao quadril (balança com o corpo), com barra de outra cor.</summary>
    private void Tabard(Material cloth, Material trim, float length)
    {
        foreach (var z in new[] { 0.135f, -0.135f })
        {
            Wear(_hips, VisualKit.Box(0.17f, length, 0.03f), cloth, new Vector3(0, -length / 2f + 0.02f, z));
            Wear(_hips, VisualKit.Box(0.175f, 0.03f, 0.035f), trim, new Vector3(0, -length + 0.03f, z));
        }
        Trident(_hips, new Vector3(0, -length * 0.45f, 0.152f), 0.4f, trim);
    }

    /// <summary>Saia de manto em painéis: dois na frente (abertos), um atrás e dois nos lados.</summary>
    private void RobePanels(Material robe, Material trim, float length)
    {
        var h = 0.80f * length;
        foreach (var side in new[] { -1f, 1f })
        {
            Wear(_armorRoot, VisualKit.Box(0.17f, h, 0.03f), robe, new Vector3(side * 0.09f, -h / 2f, 0.15f), new Vector3(-6, side * -8, side * 4));
            Wear(_armorRoot, VisualKit.Box(0.03f, h, 0.035f), trim, new Vector3(side * 0.012f, -h / 2f, 0.16f), new Vector3(-6, 0, side * 4));
            Wear(_armorRoot, VisualKit.Box(0.03f, h * 0.95f, 0.26f), robe, new Vector3(side * 0.19f, -h / 2f, 0), new Vector3(0, 0, side * -6));
        }
        Wear(_armorRoot, VisualKit.Box(0.38f, h, 0.03f), robe, new Vector3(0, -h / 2f, -0.15f), new Vector3(6, 0, 0));
        Wear(_armorRoot, VisualKit.Box(0.39f, 0.035f, 0.035f), trim, new Vector3(0, -h + 0.02f, -0.155f), new Vector3(6, 0, 0));
        Fringe(_armorRoot, robe, 0.0f, -h - 0.02f, 4, 0.10f, back: true);
    }

    /// <summary>Barra desfiada: pontas triangulares penduradas (em volta, ou só atrás).</summary>
    private void Fringe(Node3D parent, Material cloth, float radius, float y, int count, float len, bool back = false)
    {
        for (var i = 0; i < count; i++)
        {
            Vector3 pos;
            if (back) pos = new Vector3(-0.15f + i * (0.30f / Math.Max(1, count - 1)), y, -0.16f - radius);
            else
            {
                var a = i * Mathf.Tau / count;
                pos = new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius);
            }
            Wear(parent, VisualKit.Cylinder(0.05f, 0f, len * (0.8f + 0.4f * ((i * 7) % 3) / 2f), 3), cloth, pos);
        }
    }

    /// <summary>Tridente do Porto (emblema): haste, travessa e três pontas, em bronze.</summary>
    private void Trident(Node3D parent, Vector3 at, float s, Material m)
    {
        Wear(parent, VisualKit.Box(0.018f * s * 2, 0.26f * s, 0.01f), m, at + new Vector3(0, -0.04f * s, 0));
        Wear(parent, VisualKit.Box(0.16f * s, 0.018f * s * 2, 0.01f), m, at + new Vector3(0, 0.06f * s, 0));
        foreach (var dx in new[] { -0.07f, 0f, 0.07f })
            Wear(parent, VisualKit.Cylinder(0f, 0.016f * s * 2, 0.10f * s, 4), m, at + new Vector3(dx * s, 0.12f * s, 0));
    }

    private void AttachToTorso(Mesh mesh, Material material, Vector3 position, Vector3? scale = null)
    {
        // Peça presa ao tronco (acompanha a torção); some ao trocar de armadura (ClearTorsoArmor).
        _armorParts.Add(VisualKit.Part(_torso, mesh, material, position, null, scale));
    }

    private readonly System.Collections.Generic.List<Node3D> _armorParts = new();

    // ------------------------------------------------------------ animação

    public void Hit(Vector3 fromDirection)
    {
        _hitT = 0f;
        _hitDir = fromDirection;
    }

    public void Update(in VisualFrame f, float delta)
    {
        _time += delta;
        if (f.Action != _lastAction && f.Action.StartsWith("recover:skill_mago_passo")) _blinkFlash = 1f;
        _lastAction = f.Action;

        // ---- morte ----
        if (!f.Alive)
        {
            if (_wasAlive) { _deathT = 0f; _fade = 0f; }
            _wasAlive = false;
            _deathT = Mathf.Min(1f, _deathT + delta / 0.6f);
            var fall = Mathf.SmoothStep(0f, 1f, _deathT);
            _body.Rotation = new Vector3(-1.45f * fall, 0, 0.15f * fall);
            _body.Position = new Vector3(0, 0.55f - 0.38f * fall, -0.3f * fall);
            if (_deathT >= 1f) _fade = Mathf.Min(1f, _fade + delta / 1.6f);
            _flash.Apply(0f, 0f, Colors.Red, _fade);
            Root.Visible = _fade < 0.99f;
            return;
        }
        if (!_wasAlive)
        {
            _wasAlive = true;
            _deathT = 0f;
            _fade = 0f;
            Root.Visible = true;
            _body.Rotation = Vector3.Zero;
            _body.Position = new Vector3(0, 0.55f, 0);
        }

        // ---- passos ----
        var moving = Mathf.Clamp(f.Speed / 4.5f, 0f, 1.3f);
        _phase += f.Speed * delta * 2.6f / Mathf.Max(0.6f, _style.Scale);
        var target = WalkPose(moving);

        // ---- ação (preparação / golpe / recuperação) ----
        var actionPose = ActionPose(f, target, out var actionWeight);
        target = Pose.Lerp(target, actionPose, actionWeight);
        if (f.Guarding) target = Pose.Lerp(target, GuardPose(target), 1f);

        // ---- recuo ao apanhar ----
        if (_hitT < 1f)
        {
            _hitT = Mathf.Min(1f, _hitT + delta / 0.28f);
            var k = VisualKit.Snap(_hitT, 0.2f);
            target.TorsoX -= 0.35f * k;
            target.HeadX -= 0.3f * k;
        }

        var rate = f.Action.StartsWith("active") || f.Action.StartsWith("recover") ? 30f : 16f;
        _pose = Pose.Lerp(_pose, target, 1f - Mathf.Exp(-rate * delta));
        ApplyPose(_pose);

        // ---- esquiva: rolamento ----
        if (f.Action == "dodge")
        {
            var axis = new Vector3(MoveDirLocal.Z, 0, -MoveDirLocal.X);
            if (axis.LengthSquared() < 0.01f) axis = Vector3.Right;
            var angle = Mathf.Tau * Mathf.SmoothStep(0f, 1f, f.ActionT);
            _body.Basis = new Basis(axis.Normalized(), angle);
            _body.Position = new Vector3(0, 0.55f - 0.32f * Mathf.Sin(Mathf.Pi * f.ActionT), 0);
        }
        else
        {
            _body.Basis = Basis.Identity;
            _body.Position = new Vector3(0, 0.55f + _pose.BodyY - _pose.Crouch, 0);
        }

        // ---- brilhos ----
        _blinkFlash = Mathf.Max(0f, _blinkFlash - delta * 3f);
        var flash = Mathf.Max(_hitT < 1f ? (1f - _hitT) : 0f, _blinkFlash);
        _flash.Apply(flash, f.WindupGlow, new Color(1f, 0.25f, 0.1f));
    }

    private void ClearTorsoArmor()
    {
        foreach (var p in _armorParts) p.QueueFree();
        _armorParts.Clear();
    }

    private Pose IdlePose()
    {
        var staff = GearFactory.IsStaff(_weaponModel ?? _style.FixedWeapon);
        return new Pose
        {
            ShoulderR = new Vector3(staff ? -0.35f : -0.1f, 0, -0.12f),
            ShoulderL = new Vector3(-0.05f, 0, 0.12f),
            ElbowR = staff ? -0.55f : -0.25f,
            ElbowL = -0.2f,
            StaffPitch = 0.1f,
        };
    }

    private Pose WalkPose(float amp)
    {
        var p = IdlePose();
        var s = Mathf.Sin(_phase);
        var c = Mathf.Cos(_phase);
        var breathe = 0.02f * Mathf.Sin(_time * 2.2f);
        p.TorsoX = breathe + 0.10f * amp;
        p.HipR = 0.6f * s * amp;
        p.HipL = -0.6f * s * amp;
        p.KneeR = amp * (0.15f + 0.7f * Mathf.Max(0f, c));
        p.KneeL = amp * (0.15f + 0.7f * Mathf.Max(0f, -c));
        p.ShoulderR.X += -0.45f * s * amp;
        p.ShoulderL.X += 0.45f * s * amp;
        p.BodyY = 0.04f * Mathf.Abs(c) * amp;
        p.TorsoY = 0.08f * s * amp;
        return p;
    }

    private Pose GuardPose(Pose basePose)
    {
        var p = basePose;
        p.ShoulderL = new Vector3(-1.35f, -1.1f, 0.1f);
        p.ElbowL = -1.25f;
        p.ShoulderR = new Vector3(-0.5f, 0, -0.3f);
        p.ElbowR = -0.9f;
        p.TorsoX = 0.12f;
        p.Crouch = 0.08f;
        p.KneeR = Mathf.Max(p.KneeR, 0.35f);
        p.KneeL = Mathf.Max(p.KneeL, 0.35f);
        return p;
    }

    private float _strikeAt;
    private int _strikeSeq = -1;
    private string _strikePhase = "";

    /// <summary>Ataques de monstro sem pose própria herdam a da família (estocada, talho largo, pancada, conjuração).</summary>
    private static string PoseFamily(string id)
    {
        if (id.StartsWith("skill_")) return id;
        bool Has(params string[] k) { foreach (var x in k) if (id.Contains(x)) return true; return false; }
        if (Has("estoc", "punhal", "bote", "botes", "mordid", "picada", "presas", "avanco", "investida", "salto")) return "estocada";
        if (Has("varr", "ceif", "chicote", "giro", "coice", "garras", "pinca", "recuo")) return "varrida";
        if (Has("pancad", "esmag", "martel", "pis", "terremoto", "punhos", "tremor", "clava", "machad", "picaret", "atrasado", "pesado", "golpe")) return "pancada";
        if (Has("grito", "repuls", "pulso", "badalad", "espinhos", "onda", "brasa", "chuva", "correntes", "rochas", "assombro", "enraizar", "circulo")) return "repulsao";
        if (Has("bola", "cinza", "flech", "lament", "areia", "terra", "raio", "lanca", "rajada", "olhar", "teia", "cuspe", "almas", "gelo", "escoria", "sopro", "labareda")) return "bola_de_cinza";
        return id;
    }

    /// <summary>Pose da ação atual. Nomes vêm do servidor: "windup:skill", "recover:skill", "active:ataque".</summary>
    private Pose ActionPose(in VisualFrame f, Pose walk, out float weight)
    {
        {
            var ph = f.Action.IndexOf(':') is var c && c > 0 ? f.Action[..c] : f.Action;
            if (f.ActionSeq != _strikeSeq || (ph == "active" && _strikePhase != "active")) _strikeAt = _time;
            _strikeSeq = f.ActionSeq;
            _strikePhase = ph;
        }
        weight = 0f;
        var colon = f.Action.IndexOf(':');
        if (f.Action == "sleep")
        {
            // dormindo sentado: agachado, cabeça caída, braços soltos; respiração lenta
            _orb.Visible = false;
            weight = 1f;
            var p0 = walk;
            p0.Crouch = 0.42f; p0.KneeR = 1.9f; p0.KneeL = 1.9f; p0.HipR = -1.5f; p0.HipL = -1.5f;
            p0.TorsoX = 0.35f + 0.03f * Mathf.Sin(_time * 1.5f); p0.HeadX = 0.6f;
            p0.ShoulderR = new Vector3(-0.3f, 0, -0.1f); p0.ShoulderL = new Vector3(-0.3f, 0, 0.1f);
            return p0;
        }
        if (colon < 0) { _orb.Visible = false; return walk; }
        var phase = f.Action[..colon];
        var id = f.Action[(colon + 1)..];
        var t = f.ActionT;
        var p = walk;
        weight = 1f;
        var twoHanded = GearFactory.IsTwoHanded(_weaponModel ?? _style.FixedWeapon);
        _orb.Visible = false;

        Pose Windup, Strike;
        if (_style.FixedWeapon is not null || _style.CasterHands) id = PoseFamily(id);
        switch (id)
        {
            case "skill_guerreiro_corte":
            case "talho":
            case "varrida":
            {
                var wide = id == "varrida";
                Windup = walk;
                Windup.ShoulderR = new Vector3(-2.3f, wide ? -0.6f : 0f, -0.7f);
                Windup.ElbowR = -1.1f;
                Windup.TorsoY = wide ? -1.0f : -0.55f;
                Windup.TorsoX = id == "talho" ? -0.25f : -0.05f;
                Windup.ShoulderL = new Vector3(-0.6f, 0, 0.5f);
                Strike = Windup;
                Strike.ShoulderR = new Vector3(-0.7f, wide ? 0.5f : 0.1f, 0.5f);
                Strike.ElbowR = -0.15f;
                Strike.TorsoY = wide ? 1.0f : 0.6f;
                Strike.TorsoX = id == "talho" ? 0.55f : 0.2f;
                Strike.ShoulderL = new Vector3(0.3f, 0, 0.3f);
                Strike.KneeR = 0.4f; Strike.KneeL = 0.2f; Strike.HipR = -0.4f; Strike.HipL = 0.2f;
                break;
            }
            case "skill_guerreiro_golpe_de_cunha":
            case "pancada":
            {
                Windup = walk;
                Windup.ShoulderR = new Vector3(-2.95f, 0, -0.15f);
                Windup.ShoulderL = new Vector3(-2.8f, 0, 0.2f);
                Windup.ElbowR = -0.45f; Windup.ElbowL = -0.5f;
                Windup.TorsoX = -0.32f;
                Windup.Crouch = 0.04f;
                Windup.KneeR = 0.3f; Windup.KneeL = 0.3f;
                Strike = walk;
                Strike.ShoulderR = new Vector3(-0.55f, 0, -0.1f);
                Strike.ShoulderL = new Vector3(-0.6f, 0, 0.15f);
                Strike.ElbowR = -0.1f; Strike.ElbowL = -0.2f;
                Strike.TorsoX = 0.75f;
                Strike.Crouch = 0.16f;
                Strike.KneeR = 0.9f; Strike.KneeL = 0.5f; Strike.HipR = -0.7f; Strike.HipL = 0.1f;
                Strike.HeadX = 0.2f;
                break;
            }
            case "estocada":
            case "bote":
            {
                Windup = walk;
                Windup.ShoulderR = new Vector3(0.7f, 0, -0.2f);
                Windup.ElbowR = -1.4f;
                Windup.TorsoX = -0.15f;
                Windup.TorsoY = -0.4f;
                Windup.Crouch = 0.15f;
                Windup.KneeR = 0.8f; Windup.KneeL = 0.8f; Windup.HipR = -0.5f; Windup.HipL = 0.3f;
                Strike = walk;
                Strike.ShoulderR = new Vector3(-1.6f, 0, 0f);
                Strike.ElbowR = 0f;
                Strike.TorsoX = 0.45f;
                Strike.TorsoY = 0.3f;
                Strike.HipR = -0.9f; Strike.HipL = 0.6f; Strike.KneeR = 0.5f;
                break;
            }
            case "skill_mago_seta_de_brasa":
            case "bola_de_cinza":
            {
                Windup = walk;
                Windup.ShoulderR = new Vector3(-1.1f, 0, 0.35f);
                Windup.ShoulderL = new Vector3(-1.1f, 0, -0.35f);
                Windup.ElbowR = -0.9f; Windup.ElbowL = -0.9f;
                Windup.TorsoY = -0.15f;
                Windup.StaffPitch = 0.5f;
                Strike = walk;
                Strike.ShoulderR = new Vector3(-1.55f, 0, 0.1f);
                Strike.ShoulderL = new Vector3(-1.4f, 0, -0.1f);
                Strike.ElbowR = 0f; Strike.ElbowL = -0.1f;
                Strike.TorsoX = 0.2f;
                Strike.StaffPitch = 1.35f;
                if (phase == "windup" && id == "bola_de_cinza")
                {
                    _orb.Visible = true;
                    _orb.Scale = Vector3.One * (0.3f + 0.9f * t);
                }
                break;
            }
            case "skill_mago_explosao_de_cinzas":
            case "repulsao":
            {
                var spread = id == "repulsao";
                Windup = walk;
                Windup.ShoulderR = spread ? new Vector3(-1.4f, 0, -1.3f) : new Vector3(-2.9f, 0, -0.1f);
                Windup.ShoulderL = spread ? new Vector3(-1.4f, 0, 1.3f) : new Vector3(-2.7f, 0, 0.25f);
                Windup.ElbowR = -0.3f; Windup.ElbowL = -0.3f;
                Windup.TorsoX = -0.25f;
                Windup.StaffPitch = -0.1f;
                Strike = walk;
                Strike.ShoulderR = new Vector3(-0.9f, 0, -0.1f);
                Strike.ShoulderL = new Vector3(-0.9f, 0, 0.1f);
                Strike.ElbowR = -0.1f; Strike.ElbowL = -0.1f;
                Strike.TorsoX = 0.45f;
                Strike.Crouch = 0.14f;
                Strike.KneeR = 0.8f; Strike.KneeL = 0.8f; Strike.HipR = -0.5f; Strike.HipL = -0.5f;
                Strike.StaffPitch = 0.8f;
                break;
            }
            case "skill_mago_passo_etereo":
            {
                Windup = walk;
                Strike = walk;
                Strike.Crouch = 0.1f;
                Strike.TorsoX = 0.3f;
                Strike.KneeR = 0.6f; Strike.KneeL = 0.6f;
                break;
            }
            default:
                weight = 0f;
                return walk;
        }

        if (twoHanded)
        {
            // as duas mãos no cabo
            Windup.ShoulderL = new Vector3(Windup.ShoulderR.X, -Windup.ShoulderR.Y, -Windup.ShoulderR.Z + 0.6f);
            Windup.ElbowL = Windup.ElbowR - 0.2f;
            Strike.ShoulderL = new Vector3(Strike.ShoulderR.X, -Strike.ShoulderR.Y, -Strike.ShoulderR.Z + 0.6f);
            Strike.ElbowL = Strike.ElbowR - 0.2f;
        }

        switch (phase)
        {
            case "windup":
                // monstros tremem no fim da preparação: o golpe vem aí
                var shake = t > 0.7f && _style.FixedWeapon is not null ? 0.04f * Mathf.Sin(_time * 60f) : 0f;
                p = Pose.Lerp(walk, Windup, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.6f)));
                p.TorsoY += shake;
                return p;
            case "active":
                if (_style.FixedWeapon is not null || _style.CasterHands)
                {
                    // monstros: cada golpe da sequência (estocadas) volta um pouco e bate de novo
                    // 10/11 ("golpe de vários ataques: a animação não condiz"): golpe longo (sequência de estocadas)
                    // repete a estocada a cada 0,27 s — o mesmo ritmo dos acertos no servidor; golpe curto não muda
                    var k = Mathf.Min(1f, ((_time - _strikeAt) % 0.27f) / 0.14f);
                    return Pose.Lerp(Pose.Lerp(Windup, Strike, 0.35f), Strike, Mathf.SmoothStep(0f, 1f, k));
                }
                return Pose.Lerp(Windup, Strike, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 2.5f)));
            case "recover":
                // golpe sai no começo da recuperação (para habilidades do jogador) e volta devagar.
                // Monstros: ficam "presos" no fim do golpe a maior parte do tempo (janela de contra-ataque).
                var isMonster = _style.FixedWeapon is not null || _style.CasterHands;
                var swing = isMonster ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t / 0.22f));
                p = Pose.Lerp(Windup, Strike, swing);
                if (isMonster) { p.HeadX += 0.25f; p.TorsoX += 0.1f + 0.03f * Mathf.Sin(_time * 7f); }
                var back = Mathf.SmoothStep(0f, 1f, (t - (isMonster ? 0.7f : 0.45f)) / (isMonster ? 0.3f : 0.55f));
                return Pose.Lerp(p, walk, back);
        }
        weight = 0f;
        return walk;
    }

    private void ApplyPose(Pose p)
    {
        _torso.Rotation = new Vector3(_style.Hunch + p.TorsoX, p.TorsoY, 0);
        _head.Rotation = new Vector3(p.HeadX - _style.Hunch * 0.6f, -p.TorsoY * 0.4f, 0);
        _shoulderR.Rotation = p.ShoulderR;
        _shoulderL.Rotation = p.ShoulderL;
        _elbowR.Rotation = new Vector3(p.ElbowR, 0, 0);
        _elbowL.Rotation = new Vector3(p.ElbowL, 0, 0);
        if (_mounted)
        {
            // montado: pernas abertas para a frente, joelhos dobrados (sela)
            _hipR.Rotation = new Vector3(-1.35f, 0, -0.35f);
            _hipL.Rotation = new Vector3(-1.35f, 0, 0.35f);
            _kneeR.Rotation = new Vector3(1.3f, 0, 0);
            _kneeL.Rotation = new Vector3(1.3f, 0, 0);
        }
        else
        {
            _hipR.Rotation = new Vector3(-p.HipR, 0, 0);
            _hipL.Rotation = new Vector3(-p.HipL, 0, 0);
            _kneeR.Rotation = new Vector3(p.KneeR, 0, 0);
            _kneeL.Rotation = new Vector3(p.KneeL, 0, 0);
        }
        if (_wings is not null)
        {
            var spread = _lastAction == "wing_dash" ? 1.1f : 0.25f + 0.06f * Mathf.Sin(_time * 2.2f);
            _wingL!.Rotation = new Vector3(0, -spread, 0.15f);
            _wingR!.Rotation = new Vector3(0, spread, -0.15f);
        }
        if (GearFactory.IsStaff(_weaponModel ?? _style.FixedWeapon))
            _weaponSocket.Rotation = new Vector3(p.StaffPitch - p.ShoulderR.X - p.ElbowR - _style.Hunch - p.TorsoX, 0, -p.ShoulderR.Z);
    }

    public void Dispose()
    {
        Root.QueueFree();
    }

    // ------------------------------------------------------------ montaria, asas, brilho lendário

    private bool _mounted;
    private Node3D? _wings, _wingL, _wingR;
    private string? _wingsKind;
    private OmniLight3D? _legendLight;

    public void SetMounted(bool mounted) => _mounted = mounted;

    /// <summary>Asas nas costas (presas ao tronco): estrutura de ferro escuro e membrana discreta; "neve" = plumas brancas.</summary>
    public void SetWings(string? kind)
    {
        if (kind == _wingsKind) return;
        _wingsKind = kind;
        _wings?.QueueFree();
        _wings = null;
        if (kind is null) return;
        _wings = VisualKit.Joint(_torso, new Vector3(0, 0.42f, -0.16f), "Asas");
        var frame = VisualKit.Mat(new Color(0.18f, 0.18f, 0.20f), 0.4f, 0.85f);
        var membrane = kind == "neve" ? VisualKit.Mat(new Color(0.88f, 0.90f, 0.95f), 0.8f) : VisualKit.Mat(new Color(0.30f, 0.15f, 0.20f), 0.85f);
        var rune = VisualKit.Glow(new Color(0.56f, 0.41f, 0.75f), 1.6f);
        _wingL = VisualKit.Joint(_wings, new Vector3(0.06f, 0, 0));
        _wingR = VisualKit.Joint(_wings, new Vector3(-0.06f, 0, 0));
        foreach (var (w, side) in new[] { (_wingL, 1f), (_wingR, -1f) })
        {
            VisualKit.Part(w, VisualKit.Box(0.04f, 0.04f, 0.7f), frame, new Vector3(side * 0.05f, 0.18f, -0.33f), new Vector3(-25, side * 10, 0));
            VisualKit.Part(w, VisualKit.Box(0.03f, 0.55f, 0.5f), membrane, new Vector3(side * 0.05f, -0.02f, -0.36f), new Vector3(-8, 0, 0));
            VisualKit.Part(w, VisualKit.Box(0.035f, 0.035f, 0.5f), frame, new Vector3(side * 0.05f, -0.22f, -0.3f), new Vector3(15, 0, 0));
            VisualKit.Part(w, VisualKit.Box(0.04f, 0.12f, 0.04f), rune, new Vector3(side * 0.06f, 0.05f, -0.5f));
        }
    }

    /// <summary>Lendário equipado: brilho âmbar discreto na arma (não esconde ataques).</summary>
    public void SetLegendary(bool on)
    {
        if (on && _legendLight is null)
        {
            _legendLight = new OmniLight3D { LightColor = new Color(1f, 0.72f, 0.32f), LightEnergy = 0.9f, OmniRange = 2.2f };
            _weaponSocket.AddChild(_legendLight);
        }
        if (_legendLight is not null) _legendLight.Visible = on;
    }
}
