#nullable enable

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class TimedRunUI : Control
{
    private const string BuildMarker = "V3";
    private static readonly Color NeutralText = new(0.92f, 0.94f, 0.98f, 1f);
    private static readonly Color CorrectAccent = new(0.15f, 0.85f, 0.55f, 1f);
    private static readonly Color WrongAccent = new(1f, 0.3f, 0.35f, 1f);
    private static readonly Color DarkCardGold = new(0.88f, 0.73f, 0.36f, 1f);
    private static readonly Color LightCardBlack = new(0.08f, 0.09f, 0.12f, 1f);
    private static readonly Color DarkCardAnswerText = new(0.92f, 0.94f, 0.98f, 1f);

    // Lisibilité / confort
    [Export] public int CardViewportWidth { get; set; } = 2867;
    [Export] public int CardViewportHeight { get; set; } = 2048;
    [Export] public uint CardClickCollisionMask { get; set; } = 1;
    
    // Audio
    [Export] public bool EnableAudio { get; set; } = true;
    [Export] public bool EnableAmbience { get; set; } = true;
    [Export(PropertyHint.Range, "-40,6,0.5")] public float SfxVolumeDb { get; set; } = -7.0f;
    [Export(PropertyHint.Range, "-40,6,0.5")] public float AmbienceVolumeDb { get; set; } = -18.0f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float ClickAnswerRegionVTop { get; set; } = 0.58f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float ClickAnswerRegionVBottom { get; set; } = 0.93f;
    [Export(PropertyHint.Range, "0,0.45,0.01")] public float ClickAnswerRegionUMargin { get; set; } = 0.08f;

    // Mouvement: privilégier une micro-parallaxe souris plutôt qu'un bobbing continu.
    [Export] public bool EnableMouseGuidedCardMotion { get; set; } = true;
    [Export] public float MouseMotionMaxOffset { get; set; } = 0.026f;
    [Export] public float MouseMotionMaxYawDeg { get; set; } = 7.0f;
    [Export] public float MouseMotionMaxPitchDeg { get; set; } = 9.0f;
    [Export] public float MouseMotionSmoothing { get; set; } = 10.0f;
    [Export] public bool EnableIdleBobbing { get; set; } = false;

    // “Icônes” générées (mapping) — affichage style carte à collectionner
    private static readonly Dictionary<string, string> DomainIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CloudConcepts"] = "☁️",
        ["Security"] = "🔐",
        ["Technology"] = "🧠",
        ["Billing"] = "💳",
    };

    // Pondérations inspirées des domaines CCP (simple, stable)
    private static readonly (string Domain, float Weight)[] DomainWeights =
    {
        ("CloudConcepts", 0.24f),
        ("Security", 0.30f),
        ("Technology", 0.34f),
        ("Billing", 0.12f),
    };

    private const string DeckPath = "res://Data/questions_practitioner.json";

    [Export] public int TimeLimitSeconds { get; set; } = 120;

    // Par défaut on respecte les placements faits dans la scène (caméra/table).
    [Export] public bool AutoFrameCameraOnReady { get; set; } = false;

    [Export] public float DeckCardThickness { get; set; } = 0.0015f;

    private Control _panelRoot = null!;
    private Label _domainLabel = null!;
    private Label _timerLabel = null!;
    private Label _scoreLabel = null!;
    private Label _questionLabel = null!;
    private Label _feedbackLabel = null!;
    private Button _restartButton = null!;

    private Label _topDomainLabel = null!;
    private Label _topTimerLabel = null!;
    private Label _topScoreLabel = null!;

    private readonly List<Button> _answerButtons = new();
    private readonly Random _rng = new();
    
    private Node? _audioRoot;
    private readonly List<AudioStreamPlayer> _sfxPlayers = new();
    private int _sfxPlayerIdx;
    private AudioStreamPlayer? _ambiencePlayer;
    
    private AudioStream? _sfxFlip;
    private AudioStream? _sfxDraw;
    private AudioStream? _sfxShuffle;
    private AudioStream? _sfxCorrect;
    private AudioStream? _sfxWrong;
    private AudioStream? _ambienceLoop;

    private Node3D _cardRig = null!;
    private GpuParticles3D _sparkles = null!;
    private MeshInstance3D _cardFrontMesh = null!;
    private MeshInstance3D _cardBackMesh = null!;
    private SubViewport _cardFrontViewport = null!;
    private SubViewport _cardBackViewport = null!;
    private Label _cardDomainLabel = null!;
    private Label _cardTimerFaceLabel = null!;
    private Label _cardScoreFaceLabel = null!;
    private Label _cardQuestionLabel = null!;
    private readonly List<Control> _cardAnswerRows = new();
    private readonly List<Label> _cardAnswerLabels = new();
    private readonly List<PanelContainer> _cardAnswerPanels = new();
    private readonly List<Label> _cardAnswerResultLabels = new();
    private RichTextLabel _cardBackContent = null!;
    private OmniLight3D _warmLamp = null!;

    private MeshInstance3D _tableMesh = null!;
    private MeshInstance3D _wallMesh = null!;

    private Camera3D _camera = null!;

    private bool _cardReferenceCaptured;
    private Transform3D _cardReferenceGlobal;

    private bool _cardIsBackSide;
    private bool _flipLeftNext;
    private float _lastFlipSign = 1f;
    private StaticBody3D? _cardClickArea;

    private Vector2 _mouseMotion;

    private Node3D? _deckRig;
    private CsgBox3D? _deckStack;
    private Marker3D? _deckSpawn;
    private Marker3D? _cardFocus;
    private Marker3D? _discardTarget;

    private int _visualDeckCapacity = 24;
    private int _visualDeckRemaining;

    private bool _deckAnchorsCaptured;
    private float _deckStackBottomY;
    private float _deckSpawnAboveTopOffsetY;

    private Texture2D _paperGrain = null!;
    private Texture2D _woodGrain = null!;
    private Texture2D _wallGrain = null!;

    private ShaderMaterial _cardFrontMat = null!;
    private ShaderMaterial _cardBackMat = null!;

    private double _idleResumeAt;
    private Vector3 _idleAnchorPos;
    private Vector3 _idleAnchorRot;
    private bool _idleAnchorValid;

    private int _runToken;
    private int _questionToken;

    private Tween? _cardTween;
    private Tween? _deckTween;

    private bool _runActive;
    private bool _answeredCurrent;
    private double _timeRemaining;

    private bool _awaitingContinueClick;
    private int _continueRunToken;
    private int _continueQuestionToken;

    private int _hoveredAnswerIndex = -1;
    private StyleBoxFlat? _answerBarStyleBase;
    private StyleBoxFlat? _answerBarHoverStyleBase;
    private readonly List<StyleBoxFlat> _answerBarStyles = new();
    private readonly List<StyleBoxFlat> _answerBarHoverStyles = new();
    private readonly List<StyleBoxFlat> _answerBarStylesNoBorder = new();
    private readonly List<StyleBoxFlat> _answerBarHoverStylesNoBorder = new();

    private bool _isDarkCardTheme;

    private int _chosenAnswerIndex = -1;
    private int _correctAnswerIndex = -1;

    private readonly List<Question> _allQuestions = new();
    private readonly Dictionary<string, List<int>> _indicesByDomain = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<int>> _usedByDomain = new(StringComparer.OrdinalIgnoreCase);

    private Question _currentQuestion = null!;
    private bool _hasCurrentQuestion;
    private AnswerOption[] _currentOptions = Array.Empty<AnswerOption>();

    private int _answered;
    private int _correct;

    private readonly Dictionary<string, (int asked, int correct)> _domainStats = new(StringComparer.OrdinalIgnoreCase);

    public override void _Ready()
    {
        GD.Print($"[MiniJeuCartesAWS] Build marker: {BuildMarker}");
        // Le titre peut ne pas se refléter selon le WM; on a aussi un marqueur UI.
        DisplayServer.WindowSetTitle($"MiniJeuCartesAWS — {BuildMarker}");

        // Sécurise l'input clavier + le tick (certains nodes peuvent avoir ces flags désactivés en scène).
        SetProcess(true);
        SetProcessInput(true);
        
        EnsureAudio();
        SetProcessUnhandledInput(true);

        // UI refs
        _panelRoot = GetNode<Control>("Margin/Center/CardPanel");
        _domainLabel = GetNode<Label>("Margin/Center/CardPanel/CardMargin/VBox/TopRow/Domain");
        _timerLabel = GetNode<Label>("Margin/Center/CardPanel/CardMargin/VBox/TopRow/Timer");
        _scoreLabel = GetNode<Label>("Margin/Center/CardPanel/CardMargin/VBox/TopRow/Score");
        _questionLabel = GetNode<Label>("Margin/Center/CardPanel/CardMargin/VBox/Question");
        _feedbackLabel = GetNode<Label>("Margin/Center/CardPanel/CardMargin/VBox/Feedback");
        _restartButton = GetNode<Button>("Margin/Center/CardPanel/CardMargin/VBox/Restart");

        _topDomainLabel = GetNode<Label>("TopHUD/TopRow/Domain");
        _topTimerLabel = GetNode<Label>("TopHUD/TopRow/Timer");
        _topScoreLabel = GetNode<Label>("TopHUD/TopRow/Score");

        _answerButtons.Add(GetNode<Button>("Margin/Center/CardPanel/CardMargin/VBox/Answers/A"));
        _answerButtons.Add(GetNode<Button>("Margin/Center/CardPanel/CardMargin/VBox/Answers/B"));
        _answerButtons.Add(GetNode<Button>("Margin/Center/CardPanel/CardMargin/VBox/Answers/C"));
        _answerButtons.Add(GetNode<Button>("Margin/Center/CardPanel/CardMargin/VBox/Answers/D"));

        for (var i = 0; i < _answerButtons.Count; i++)
        {
            var idx = i;
            _answerButtons[i].Pressed += () => Choose(idx);
        }

        _restartButton.Pressed += StartRun;

        // 3D refs
        _cardRig = GetNode<Node3D>("../../CardRig");
        _camera = GetNode<Camera3D>("../../Camera");
        _sparkles = GetNode<GpuParticles3D>("../../CardRig/Sparkles");
        _cardFrontMesh = GetNode<MeshInstance3D>("../../CardRig/CardFace");
        _cardBackMesh = GetNode<MeshInstance3D>("../../CardRig/CardBack");
        _cardClickArea = GetNodeOrNull<StaticBody3D>("../../CardRig/CardClickArea");
        _cardFrontViewport = GetNode<SubViewport>("../../CardRig/CardFrontViewport");
        _cardBackViewport = GetNode<SubViewport>("../../CardRig/CardBackViewport");

        // UI de carte (SubViewports): on utilise les Unique Names (%) pour rester robuste
        // même si on réorganise la mise en page dans les sous-scènes.
        var faceUi = GetNode<Control>("../../CardRig/CardFrontViewport/Face");
        _cardDomainLabel = faceUi.GetNode<Label>("%FaceDomain");
        _cardTimerFaceLabel = faceUi.GetNodeOrNull<Label>("%FaceTimer") ?? _cardTimerFaceLabel;
        _cardScoreFaceLabel = faceUi.GetNodeOrNull<Label>("%FaceScore") ?? _cardScoreFaceLabel;
        _cardQuestionLabel = faceUi.GetNode<Label>("%FaceQuestion");
        _cardAnswerRows.Clear();
        _cardAnswerLabels.Clear();
        _cardAnswerPanels.Clear();
        _cardAnswerResultLabels.Clear();

        var answerA = faceUi.GetNode<PanelContainer>("%AnswerA");
        var answerB = faceUi.GetNode<PanelContainer>("%AnswerB");
        var answerC = faceUi.GetNode<PanelContainer>("%AnswerC");
        var answerD = faceUi.GetNode<PanelContainer>("%AnswerD");

        _cardAnswerPanels.Add(answerA);
        _cardAnswerPanels.Add(answerB);
        _cardAnswerPanels.Add(answerC);
        _cardAnswerPanels.Add(answerD);

        _cardAnswerRows.Add(answerA);
        _cardAnswerRows.Add(answerB);
        _cardAnswerRows.Add(answerC);
        _cardAnswerRows.Add(answerD);

        _cardAnswerLabels.Add(faceUi.GetNode<Label>("%FaceA"));
        _cardAnswerLabels.Add(faceUi.GetNode<Label>("%FaceB"));
        _cardAnswerLabels.Add(faceUi.GetNode<Label>("%FaceC"));
        _cardAnswerLabels.Add(faceUi.GetNode<Label>("%FaceD"));

        _cardAnswerResultLabels.Add(faceUi.GetNode<Label>("%FaceAResult"));
        _cardAnswerResultLabels.Add(faceUi.GetNode<Label>("%FaceBResult"));
        _cardAnswerResultLabels.Add(faceUi.GetNode<Label>("%FaceCResult"));
        _cardAnswerResultLabels.Add(faceUi.GetNode<Label>("%FaceDResult"));

        var backUi = GetNode<Control>("../../CardRig/CardBackViewport/Back");
        _cardBackContent = backUi.GetNode<RichTextLabel>("%BackContent");
        _warmLamp = GetNode<OmniLight3D>("../../Set/WarmLamp");

        _answerBarStyleBase = GD.Load<StyleBoxFlat>("res://Resources/CardUI/StyleBoxFlat_AnswerBar.tres");
        _answerBarHoverStyleBase = GD.Load<StyleBoxFlat>("res://Resources/CardUI/StyleBoxFlat_AnswerBar_Hover.tres");
        BuildAnswerBarStyles();

        // Pose de référence: la CardRig telle que placée dans la scène (face caméra).
        // On l’utilise comme cible pour toutes les animations afin d’éviter les dérives.
        _cardReferenceGlobal = _cardRig.GlobalTransform;
        _cardReferenceCaptured = true;

        // Lisibilité: rendu 2D plus net sur la carte.
        ApplyCardViewportSizing();

        // Nodes "monde" (siblings du HUD) — robuste: GetNodeOrNull + fallback FindChild.
        ResolveWorldNodesForDeck();

        _tableMesh = GetNode<MeshInstance3D>("../../Set/Table");
        _wallMesh = GetNode<MeshInstance3D>("../../Set/BackWall");

        BuildProceduralTextures();
        ApplySceneMaterials();

        ApplyViewportToCardMaterials();

        if (AutoFrameCameraOnReady)
            FrameCameraForReadability();

        _visualDeckRemaining = _visualDeckCapacity;
        UpdateDeckVisual();

        LoadDeck();
        ShowReadyScreen();
    }

    private void BuildAnswerBarStyles()
    {
        _answerBarStyles.Clear();
        _answerBarHoverStyles.Clear();
        _answerBarStylesNoBorder.Clear();
        _answerBarHoverStylesNoBorder.Clear();

        if (_answerBarStyleBase == null || _answerBarHoverStyleBase == null)
            return;

        // Réponses sans couleurs: on utilise les styleboxes neutres (définies en .tres).
        // On prépare aussi une variante sans bordure (utile sur cartes sombres pour éviter
        // un contour blanc trop présent et sujet à aliasing).
        for (var i = 0; i < 4; i++)
        {
            var normal = (StyleBoxFlat)_answerBarStyleBase.Duplicate();
            var hover = (StyleBoxFlat)_answerBarHoverStyleBase.Duplicate();
            _answerBarStyles.Add(normal);
            _answerBarHoverStyles.Add(hover);

            var normalNoBorder = (StyleBoxFlat)normal.Duplicate();
            normalNoBorder.BorderWidthLeft = 0;
            normalNoBorder.BorderWidthTop = 0;
            normalNoBorder.BorderWidthRight = 0;
            normalNoBorder.BorderWidthBottom = 0;
            normalNoBorder.BorderColor = new Color(normalNoBorder.BorderColor.R, normalNoBorder.BorderColor.G, normalNoBorder.BorderColor.B, 0f);
            _answerBarStylesNoBorder.Add(normalNoBorder);

            var hoverNoBorder = (StyleBoxFlat)hover.Duplicate();
            hoverNoBorder.BorderWidthLeft = 0;
            hoverNoBorder.BorderWidthTop = 0;
            hoverNoBorder.BorderWidthRight = 0;
            hoverNoBorder.BorderWidthBottom = 0;
            hoverNoBorder.BorderColor = new Color(hoverNoBorder.BorderColor.R, hoverNoBorder.BorderColor.G, hoverNoBorder.BorderColor.B, 0f);
            _answerBarHoverStylesNoBorder.Add(hoverNoBorder);
        }
    }

    private void ApplyCardViewportSizing()
    {
        if (IsInstanceValid(_cardFrontViewport))
            _cardFrontViewport.Size = new Vector2I(Math.Max(256, CardViewportWidth), Math.Max(256, CardViewportHeight));
        if (IsInstanceValid(_cardBackViewport))
            _cardBackViewport.Size = new Vector2I(Math.Max(256, CardViewportWidth), Math.Max(256, CardViewportHeight));

        // Anti-aliasing: efficace pour du texte/UI rendu en SubViewport texture.
        // - MSAA2D lisse les bords dans la texture.
        // - FXAA sur le viewport principal aide les silhouettes en 3D.
        try
        {
            var main = GetViewport();
            main.ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Fxaa;
            main.Msaa3D = Viewport.Msaa.Msaa4X;

            if (IsInstanceValid(_cardFrontViewport))
                _cardFrontViewport.Msaa2D = Viewport.Msaa.Msaa4X;
            if (IsInstanceValid(_cardBackViewport))
                _cardBackViewport.Msaa2D = Viewport.Msaa.Msaa4X;
        }
        catch
        {
        }

        // Note: la taille de texte du verso se règle dans la scène CardBackUI (Theme Overrides).
        // On n'impose pas de taille ici, sinon l'édition dans l'UI devient impossible.
    }

    private void CaptureDeckAnchorsIfNeeded()
    {
        if (_deckAnchorsCaptured)
            return;
        if (!IsInstanceValid(_deckStack))
            return;

        // On prend un repère “bas de pile” pour pouvoir changer la hauteur sans déplacer le bas.
        var size = _deckStack!.Size;
        var pos = _deckStack.Position;
        _deckStackBottomY = pos.Y - (size.Y * 0.5f);

        // On garde l’offset du marker de spawn au-dessus du deck (si défini dans la scène).
        if (IsInstanceValid(_deckSpawn))
        {
            var topY = _deckStackBottomY + size.Y;
            _deckSpawnAboveTopOffsetY = _deckSpawn!.Position.Y - topY;
        }
        else
        {
            _deckSpawnAboveTopOffsetY = 0.0f;
        }

        _deckAnchorsCaptured = true;
    }

    private void ShowReadyScreen()
    {
        _runActive = false;
        _answeredCurrent = true;
        _hasCurrentQuestion = false;

        CancelInFlightAnimations();

        foreach (var b in _answerButtons)
            b.Visible = false;

        _panelRoot.Visible = true;

        _domainLabel.Text = "Domaine: —";
        _timerLabel.Text = $"{BuildMarker} ⏱️ {FormatTime(TimeLimitSeconds)}";
        _scoreLabel.Text = "Score: 0/0";

        _questionLabel.Text =
            "Prêt ?\n\n" +
            "Timed run: réponds au maximum de questions avant la fin du chrono.\n\n" +
            "Clavier: 1–4";

        _feedbackLabel.Text = "";
        _feedbackLabel.SelfModulate = NeutralText;

        _restartButton.Text = "Commencer";
        _restartButton.Visible = true;
        _restartButton.Disabled = false;
        _restartButton.GrabFocus();
    }

    private void ResolveWorldNodesForDeck()
    {
        var scene = GetTree().CurrentScene;
        if (scene == null)
        {
            GD.PushWarning("[MiniJeuCartesAWS] CurrentScene est null; résolution deck via chemins relatifs.");
            _deckRig = GetNodeOrNull<Node3D>("../../Set/DeckRig");
            _deckStack = GetNodeOrNull<CsgBox3D>("../../Set/DeckRig/DeckStack");
            _deckSpawn = GetNodeOrNull<Marker3D>("../../Set/DeckRig/DeckSpawn");
            _cardFocus = GetNodeOrNull<Marker3D>("../../CardFocus");
            _discardTarget = GetNodeOrNull<Marker3D>("../../DiscardTarget");
            return;
        }

        // Chemins stricts (conformes à IA_CONTEXT.md / Main3D.tscn).
        _deckRig = scene.GetNodeOrNull<Node3D>("Set/DeckRig");
        _deckStack = scene.GetNodeOrNull<CsgBox3D>("Set/DeckRig/DeckStack");
        _deckSpawn = scene.GetNodeOrNull<Marker3D>("Set/DeckRig/DeckSpawn");
        _cardFocus = scene.GetNodeOrNull<Marker3D>("CardFocus");
        _discardTarget = scene.GetNodeOrNull<Marker3D>("DiscardTarget");

        if (!IsInstanceValid(_deckRig) || !IsInstanceValid(_deckStack) || !IsInstanceValid(_deckSpawn) || !IsInstanceValid(_cardFocus) || !IsInstanceValid(_discardTarget))
        {
            var missing = new List<string>();
            if (!IsInstanceValid(_deckRig)) missing.Add("Set/DeckRig");
            if (!IsInstanceValid(_deckStack)) missing.Add("Set/DeckRig/DeckStack");
            if (!IsInstanceValid(_deckSpawn)) missing.Add("Set/DeckRig/DeckSpawn");
            if (!IsInstanceValid(_cardFocus)) missing.Add("CardFocus");
            if (!IsInstanceValid(_discardTarget)) missing.Add("DiscardTarget");

            GD.PushWarning($"[MiniJeuCartesAWS] Deck nodes manquants: {string.Join(", ", missing)}");
            DumpSceneForDeckDebug(scene);
            return;
        }

        GD.Print("[MiniJeuCartesAWS] Deck nodes OK: DeckRig/DeckStack/DeckSpawn/CardFocus/DiscardTarget.");
    }

    private static void DumpSceneForDeckDebug(Node scene)
    {
        try
        {
            GD.Print($"[MiniJeuCartesAWS] CurrentScene: {scene.Name} ({scene.GetPath()})");
            var children = scene.GetChildren();
            GD.Print($"[MiniJeuCartesAWS] CurrentScene children ({children.Count}): {string.Join(", ", children.Select(c => c.Name))}");

            var set = scene.GetNodeOrNull<Node>("Set") as Node;
            if (set != null)
            {
                var setChildren = set.GetChildren();
                GD.Print($"[MiniJeuCartesAWS] Set children ({setChildren.Count}): {string.Join(", ", setChildren.Select(c => c.Name))}");
            }
            else
            {
                GD.Print("[MiniJeuCartesAWS] Node 'Set' introuvable sous CurrentScene.");
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[MiniJeuCartesAWS] DumpSceneForDeckDebug a échoué: {ex.Message}");
        }
    }

    private void FrameCameraForReadability()
    {
        if (!IsInstanceValid(_camera) || !IsInstanceValid(_cardRig))
            return;

        // Cadrage "tabletop": lisible + on voit deck et décor.
        _camera.Fov = 64.0f;

        var focus = IsInstanceValid(_cardFocus) ? _cardFocus!.GlobalPosition : (_cardRig.GlobalPosition + new Vector3(0, 0.08f, 0));
        var deck = IsInstanceValid(_deckRig) ? _deckRig!.GlobalPosition : focus;
        var target = (focus * 0.7f) + (deck * 0.3f);

        _camera.GlobalPosition = target + new Vector3(0.0f, 1.10f, 2.95f);
        _camera.LookAt(target, Vector3.Up);
    }

    private Vector3 GetFrontRotationDegrees()
    {
        if (IsInstanceValid(_cardFocus))
            return _cardFocus!.RotationDegrees;

        return _cardRig.RotationDegrees;
    }

    private Transform3D GetFocusTransformGlobal()
    {
        if (_cardReferenceCaptured)
            return _cardReferenceGlobal;

        if (IsInstanceValid(_cardFocus))
            return _cardFocus!.GlobalTransform;

        return _cardRig.GlobalTransform;
    }

    private Basis GetFrontBasisGlobal()
    {
        return GetFocusTransformGlobal().Basis;
    }

    private static Transform3D WithBasisAtOrigin(Basis basis, Vector3 origin)
    {
        var t = new Transform3D(basis, origin);
        return t;
    }

    private void UpdateDeckVisual()
    {
        if (!IsInstanceValid(_deckStack))
            return;

        CaptureDeckAnchorsIfNeeded();

        var h = Mathf.Max(0.02f, _visualDeckRemaining * DeckCardThickness);
        var prevSize = _deckStack!.Size;
        var prevPos = _deckStack.Position;
        _deckStack.Size = new Vector3(prevSize.X, h, prevSize.Z);
        // Garder le bas du deck stable: y = bottom + h/2.
        _deckStack.Position = new Vector3(prevPos.X, _deckStackBottomY + (h * 0.5f), prevPos.Z);

        // Garder le point de tirage au-dessus du deck.
        if (IsInstanceValid(_deckSpawn))
        {
            var sp = _deckSpawn!.Position;
            var minMargin = Math.Max(DeckCardThickness * 2.0f, 0.004f);
            var margin = Math.Max(_deckSpawnAboveTopOffsetY, minMargin);
            _deckSpawn.Position = new Vector3(sp.X, _deckStackBottomY + h + margin, sp.Z);
        }
    }

    public override void _Process(double delta)
    {
        if (!_runActive)
            return;

        _timeRemaining -= delta;
        if (_timeRemaining <= 0)
        {
            _timeRemaining = 0;
            EndRun();
            return;
        }

        UpdateTopRow();

        UpdateCardHover();

        if (IsIdleAllowed())
            ApplyCardMicroMotion(delta);
        AnimateAmbientLight();
    }

    private void UpdateCardHover()
    {
        if (_answeredCurrent || _cardIsBackSide)
        {
            ClearHover();
            return;
        }

        if (!TryGetHoverAnswerIndex(out var idx))
        {
            ClearHover();
            return;
        }

        if (idx == _hoveredAnswerIndex)
            return;

        _hoveredAnswerIndex = idx;
        ApplyAnswerRowVisualState();
    }

    private void ClearHover()
    {
        if (_hoveredAnswerIndex == -1)
            return;

        _hoveredAnswerIndex = -1;
        ApplyAnswerRowVisualState();
    }

    private bool TryGetHoverAnswerIndex(out int answerIndex)
    {
        answerIndex = -1;

        if (!IsInstanceValid(_camera) || !IsInstanceValid(_cardFrontMesh) || !IsInstanceValid(_cardClickArea))
            return false;
        if (!IsInstanceValid(_cardFrontViewport) || _cardAnswerRows.Count != 4 || !_cardAnswerRows.All(IsInstanceValid))
            return false;

        var vp = GetViewport();
        var mouse = vp.GetMousePosition();
        var from = _camera.ProjectRayOrigin(mouse);
        var dir = _camera.ProjectRayNormal(mouse);
        var to = from + dir * 10.0f;

        var world = _camera.GetWorld3D();
        if (world == null)
            return false;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = CardClickCollisionMask;
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var hit = world.DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
            return false;

        if (!hit.TryGetValue("collider", out var colliderObj))
            return false;
        var colliderGo = colliderObj.AsGodotObject();
        if (colliderGo == null || colliderGo != _cardClickArea)
            return false;

        if (!hit.TryGetValue("position", out var posObj))
            return false;
        var hitWorld = (Vector3)posObj;

        // Convertit la position hit en coordonnées viewport.
        var p = _cardFrontMesh.ToLocal(hitWorld);
        var plane = GetCardPlaneSize();
        var w = Math.Max(0.0001f, plane.X);
        var h = Math.Max(0.0001f, plane.Y);
        var u = (p.X / w) + 0.5f;
        var v = (p.Z / h) + 0.5f;

        var vpSize = _cardFrontViewport.Size;
        var pt = new Vector2(u * vpSize.X, v * vpSize.Y);
        for (var i = 0; i < _cardAnswerRows.Count; i++)
        {
            var rect = _cardAnswerRows[i].GetGlobalRect();
            if (rect.HasPoint(pt))
            {
                answerIndex = i;
                return true;
            }
        }

        return false;
    }

    private void ApplyAnswerRowVisualState()
    {
        if (_cardAnswerPanels.Count != 4)
            return;

        if (_answerBarStyles.Count != 4 || _answerBarHoverStyles.Count != 4)
            return;
        if (_answerBarStylesNoBorder.Count != 4 || _answerBarHoverStylesNoBorder.Count != 4)
            return;

        var styles = _isDarkCardTheme ? _answerBarStylesNoBorder : _answerBarStyles;
        var hoverStyles = _isDarkCardTheme ? _answerBarHoverStylesNoBorder : _answerBarHoverStyles;

        var effectiveHoverIndex = (_answeredCurrent || _cardIsBackSide) ? -1 : _hoveredAnswerIndex;

        for (var i = 0; i < _cardAnswerPanels.Count; i++)
        {
            var panel = _cardAnswerPanels[i];
            if (!IsInstanceValid(panel))
                continue;

            var isHover = i == effectiveHoverIndex;

            // Important: ne pas utiliser SelfModulate pour teinter le PanelContainer,
            // sinon ça multiplie aussi la couleur du texte et peut le rendre illisible.
            panel.SelfModulate = Colors.White;
            panel.AddThemeStyleboxOverride("panel", isHover ? hoverStyles[i] : styles[i]);
        }
    }

    private void ApplyCardMicroMotion(double delta)
    {
        // Ne pas lutter contre les tweens (tirage, flip, discard, etc.)
        if (_cardTween != null && GodotObject.IsInstanceValid(_cardTween))
            return;

        // Pendant la lecture du verso, la lisibilité prime.
        if (_cardIsBackSide)
            return;

        if (EnableMouseGuidedCardMotion)
        {
            var vp = GetViewport();
            var rect = vp.GetVisibleRect();
            var size = rect.Size;
            if (size.X <= 1 || size.Y <= 1)
                return;

            var mouse = vp.GetMousePosition();
            var nx = Mathf.Clamp((float)((mouse.X / size.X) - 0.5) * 2.0f, -1.0f, 1.0f);
            var ny = Mathf.Clamp((float)(0.5 - (mouse.Y / size.Y)) * 2.0f, -1.0f, 1.0f);

            var target = new Vector2(nx, ny);
            var alpha = 1.0f - Mathf.Exp(-Mathf.Max(1.0f, MouseMotionSmoothing) * (float)delta);
            _mouseMotion = _mouseMotion.Lerp(target, alpha);

            var baseT = GetFocusTransformGlobal();
            var b = baseT.Basis;

            // Déplacement léger DANS le plan de la carte.
            // La PlaneMesh de la face a une normale locale ~Y, donc le plan est (X,Z).
            var offset = (b.X * (_mouseMotion.X * MouseMotionMaxOffset)) + (b.Z * (_mouseMotion.Y * MouseMotionMaxOffset));

            // Rotation: yaw avec la souris; pitch arrière surtout quand la souris est en haut.
            var yaw = Mathf.DegToRad(MouseMotionMaxYawDeg) * -_mouseMotion.X;
            var pitchFactor = Mathf.Clamp((_mouseMotion.Y - 0.25f) / 0.75f, 0.0f, 1.0f);
            var pitch = Mathf.DegToRad(MouseMotionMaxPitchDeg) * -pitchFactor;

            // yaw autour de l'axe vertical dans le plan (Z), pitch autour de l'axe horizontal (X)
            b = b.Rotated(baseT.Basis.Z, yaw);
            b = b.Rotated(baseT.Basis.X, pitch);

            _cardRig.GlobalTransform = WithBasisAtOrigin(b, baseT.Origin + offset);
            _idleAnchorValid = false;
            return;
        }

        if (EnableIdleBobbing)
            AnimateCardIdle();
    }

    private void SuppressIdle(double seconds)
    {
        var now = Time.GetTicksMsec() / 1000.0;
        _idleResumeAt = Math.Max(_idleResumeAt, now + Math.Max(0, seconds));
        _idleAnchorValid = false;
    }

    private static void KillTween(ref Tween? tween)
    {
        if (tween == null)
            return;

        if (GodotObject.IsInstanceValid(tween))
            tween.Kill();

        tween = null;
    }

    private void CancelInFlightAnimations()
    {
        KillTween(ref _cardTween);
        KillTween(ref _deckTween);
        _idleAnchorValid = false;
    }

    private bool IsIdleAllowed()
    {
        var now = Time.GetTicksMsec() / 1000.0;
        return now >= _idleResumeAt;
    }

    private void ApplyViewportToCardMaterials()
    {
        // Matériaux "collector" (face/verso) pilotés par paramètres.
        var shader = new Shader
        {
            Code = @"shader_type spatial;
render_mode specular_schlick_ggx;

uniform sampler2D face_tex : source_color, filter_linear_mipmap_anisotropic;
uniform sampler2D grain_tex : source_color, filter_linear_mipmap_anisotropic;

uniform vec3 bg_color = vec3(0.12, 0.13, 0.18);
uniform vec3 base_tint = vec3(1.0, 1.0, 1.0);
uniform float grain_strength = 0.10;
uniform float grain_scale = 10.0;
uniform float contrast = 1.0;
uniform bool flip_uv = false;
uniform float bend = 0.0; // [-1..1] effet de souplesse pendant le flip

void vertex() {
    // PlaneMesh: surface dans XZ, normale ~+Y. On ajoute une légère courbure vers la normale.
    float x = UV.x - 0.5;
    float profile = clamp((0.25 - x * x) * 4.0, 0.0, 1.0); // 0 bords, 1 centre
    float amp = clamp(abs(bend), 0.0, 1.0);
    float lift = amp * profile * 0.020; // discret mais visible (un peu souple)
    VERTEX += NORMAL * lift;
}

void fragment() {
    vec2 uv = UV;
    if (flip_uv) {
        uv = vec2(1.0 - uv.x, 1.0 - uv.y);
    }

    vec4 ft = texture(face_tex, uv);
    vec3 face = ft.rgb;
    float a = ft.a;
    face = (face - 0.5) * contrast + 0.5;
    float g = texture(grain_tex, uv * grain_scale).r;

    // Si le SubViewport est transparent, on le mélange avec un fond.
    vec3 col = mix(bg_color, face, a);
    col *= base_tint;
    col *= mix(1.0, 0.90 + 0.20 * g, grain_strength);
    // Pas de vignette/border/frame: rendu plus propre, lisibilité prioritaire.

    ALBEDO = col;
    ROUGHNESS = 0.93;
    METALLIC = 0.0;
    SPECULAR = 0.0;
}
"
        };

        _cardFrontMat = new ShaderMaterial { Shader = shader };
        _cardFrontMat.SetShaderParameter("face_tex", _cardFrontViewport.GetTexture());
        _cardFrontMat.SetShaderParameter("grain_tex", _paperGrain);
        _cardFrontMat.SetShaderParameter("bg_color", new Color(0.12f, 0.13f, 0.18f, 1f));
        _cardFrontMat.SetShaderParameter("flip_uv", false);
        _cardFrontMat.SetShaderParameter("bend", 0.0f);

        _cardBackMat = new ShaderMaterial { Shader = shader };
        _cardBackMat.SetShaderParameter("face_tex", _cardBackViewport.GetTexture());
        _cardBackMat.SetShaderParameter("grain_tex", _paperGrain);
        _cardBackMat.SetShaderParameter("bg_color", new Color(0.10f, 0.11f, 0.14f, 1f));
        _cardBackMat.SetShaderParameter("flip_uv", true);
        _cardBackMat.SetShaderParameter("bend", 0.0f);

        _cardFrontMesh.MaterialOverride = _cardFrontMat;
        _cardBackMesh.MaterialOverride = _cardBackMat;
    }

    private void BuildProceduralTextures()
    {
        _paperGrain = CreateNoiseTexture(256, 256, 2.2f, FastNoiseLite.NoiseTypeEnum.Simplex);
        _woodGrain = CreateNoiseTexture(512, 512, 1.2f, FastNoiseLite.NoiseTypeEnum.Perlin);
        _wallGrain = CreateNoiseTexture(512, 512, 0.9f, FastNoiseLite.NoiseTypeEnum.SimplexSmooth);
    }

    private Texture2D CreateNoiseTexture(int w, int h, float frequency, FastNoiseLite.NoiseTypeEnum type)
    {
        var noise = new FastNoiseLite
        {
            NoiseType = type,
            Frequency = frequency,
        };

        var tex = new NoiseTexture2D
        {
            Width = w,
            Height = h,
            Noise = noise,
            Seamless = true,
        };

        return tex;
    }

    private void EnsureAudio()
    {
        if (!EnableAudio)
            return;
        
        // Script attaché à HUD/Root: on crée tout sous ce node.
        _audioRoot = GetNodeOrNull<Node>("Audio");
        if (!IsInstanceValid(_audioRoot))
        {
            _audioRoot = new Node { Name = "Audio" };
            AddChild(_audioRoot);
        }
        
        _sfxFlip = TryLoadStream("res://Assets/Audio/SFX/card_flip.wav");
        _sfxDraw = TryLoadStream("res://Assets/Audio/SFX/card_draw.wav");
        _sfxShuffle = TryLoadStream("res://Assets/Audio/SFX/card_shuffle.wav");
        _sfxCorrect = TryLoadStream("res://Assets/Audio/SFX/correct.wav");
        _sfxWrong = TryLoadStream("res://Assets/Audio/SFX/wrong.wav");
        _ambienceLoop = TryLoadStream("res://Assets/Audio/Ambience/ambience_loop.wav");
        
        // Players SFX (petit pool pour éviter de couper un son si un autre part).
        if (_sfxPlayers.Count == 0)
        {
            for (var i = 0; i < 4; i++)
            {
                var p = new AudioStreamPlayer
                {
                    Name = $"Sfx{i}",
                    Bus = "SFX",
                    VolumeDb = SfxVolumeDb,
                };
                _audioRoot.AddChild(p);
                _sfxPlayers.Add(p);
            }
        }
        else
        {
            foreach (var p in _sfxPlayers)
            {
                if (IsInstanceValid(p))
                {
                    p.Bus = "SFX";
                    p.VolumeDb = SfxVolumeDb;
                }
            }
        }
        
        // Ambience
        if (!IsInstanceValid(_ambiencePlayer))
        {
            _ambiencePlayer = new AudioStreamPlayer
            {
                Name = "Ambience",
                Bus = "Ambience",
                VolumeDb = AmbienceVolumeDb,
            };
            _audioRoot.AddChild(_ambiencePlayer);
        }
        else
        {
            _ambiencePlayer!.Bus = "Ambience";
            _ambiencePlayer!.VolumeDb = AmbienceVolumeDb;
        }
        
        if (EnableAmbience && IsInstanceValid(_ambiencePlayer) && _ambiencePlayer!.Stream == null)
        {
            _ambiencePlayer.Stream = _ambienceLoop;
            if (_ambiencePlayer.Stream is AudioStreamWav wav)
            {
                // loop simple (le WAV est "loopable" par construction)
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                wav.LoopBegin = 0;
                wav.LoopEnd = wav.Data != null ? wav.Data.Length : 0;
            }
            if (_ambiencePlayer.Stream != null)
                _ambiencePlayer.Play();
        }
    }
    
    private static AudioStream? TryLoadStream(string path)
    {
        if (!ResourceLoader.Exists(path))
            return null;
        return GD.Load<AudioStream>(path);
    }
    
    private void PlaySfx(AudioStream? stream, float pitch = 1.0f)
    {
        if (!EnableAudio)
            return;
        if (stream == null)
            return;
        if (_sfxPlayers.Count == 0)
            return;
        
        var p = _sfxPlayers[_sfxPlayerIdx % _sfxPlayers.Count];
        _sfxPlayerIdx++;
        
        if (!IsInstanceValid(p))
            return;
        
        p.VolumeDb = SfxVolumeDb;
        p.Stream = stream;
        p.PitchScale = pitch;
        p.Play();
    }

    private void ApplySceneMaterials()
    {
        // Table: bois sombre (simple mais texturé)
        if (IsInstanceValid(_tableMesh))
        {
            var tableMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.17f, 0.14f, 0.12f, 1f),
                Roughness = 0.72f,
                Metallic = 0.02f,
                AlbedoTexture = _woodGrain,
            };

            _tableMesh.MaterialOverride = tableMat;
        }

        // Mur: grain léger (style plâtre/peinture)
        if (IsInstanceValid(_wallMesh))
        {
            var wallMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.10f, 0.11f, 0.15f, 1f),
                Roughness = 0.86f,
                Metallic = 0.0f,
                AlbedoTexture = _wallGrain,
            };

            _wallMesh.MaterialOverride = wallMat;
        }
    }

    private void AnimateAmbientLight()
    {
        // Légère pulsation (chill) sur la lampe chaude
        var t = (float)Time.GetTicksMsec() / 1000f;
        _warmLamp.LightEnergy = 2.25f + Mathf.Sin(t * 0.65f) * 0.22f;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed)
            return;

        if (!_runActive)
        {
            if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter || key.Keycode == Key.Space)
            {
                if (IsInstanceValid(_restartButton) && _restartButton.Visible && !_restartButton.Disabled)
                    StartRun();
            }
            return;
        }

        if (key.Keycode == Key.Key1) Choose(0);
        else if (key.Keycode == Key.Key2) Choose(1);
        else if (key.Keycode == Key.Key3) Choose(2);
        else if (key.Keycode == Key.Key4) Choose(3);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_runActive)
            return;

        // Clic gauche sur la carte => choisir une réponse (sans UI overlay).
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            if (_answeredCurrent && _cardIsBackSide && _awaitingContinueClick)
                TryContinueByCardClick();
            else if (!_answeredCurrent && !_cardIsBackSide)
                TryChooseByCardClick();
            return;
        }

        // Clavier 1–4 (plus "tôt" que _UnhandledInput)
        if (@event is InputEventKey key && key.Pressed)
        {
            if (key.Keycode == Key.Key1) Choose(0);
            else if (key.Keycode == Key.Key2) Choose(1);
            else if (key.Keycode == Key.Key3) Choose(2);
            else if (key.Keycode == Key.Key4) Choose(3);
        }
    }

    private void TryChooseByCardClick()
    {
        if (!_runActive || _answeredCurrent || _cardIsBackSide)
            return;
        if (!IsInstanceValid(_camera) || !IsInstanceValid(_cardFrontMesh) || !IsInstanceValid(_cardClickArea))
            return;

        var vp = GetViewport();
        var mouse = vp.GetMousePosition();
        var from = _camera.ProjectRayOrigin(mouse);
        var dir = _camera.ProjectRayNormal(mouse);
        var to = from + dir * 10.0f;

        var world = _camera.GetWorld3D();
        if (world == null)
            return;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = CardClickCollisionMask;
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var hit = world.DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
            return;

        if (!hit.TryGetValue("collider", out var colliderObj))
            return;
        var colliderGo = colliderObj.AsGodotObject();
        if (colliderGo == null || colliderGo != _cardClickArea)
            return;

        if (!hit.TryGetValue("position", out var posObj))
            return;
        var hitWorld = (Vector3)posObj;

        if (!TryMapHitToAnswerIndex(hitWorld, out var idx))
            return;

        Choose(idx);
    }

    private void TryContinueByCardClick()
    {
        if (!_runActive || !_answeredCurrent || !_cardIsBackSide)
            return;
        if (!_awaitingContinueClick)
            return;
        if (!IsInstanceValid(_camera) || !IsInstanceValid(_cardClickArea))
            return;

        var vp = GetViewport();
        var mouse = vp.GetMousePosition();
        var from = _camera.ProjectRayOrigin(mouse);
        var dir = _camera.ProjectRayNormal(mouse);
        var to = from + dir * 10.0f;

        var world = _camera.GetWorld3D();
        if (world == null)
            return;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = CardClickCollisionMask;
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var hit = world.DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
            return;

        if (!hit.TryGetValue("collider", out var colliderObj))
            return;
        var colliderGo = colliderObj.AsGodotObject();
        if (colliderGo == null || colliderGo != _cardClickArea)
            return;

        _awaitingContinueClick = false;
        AnimateCardSendAwayAndMaybeShuffle(_continueRunToken, _continueQuestionToken);
    }

    private bool TryMapHitToAnswerIndex(Vector3 hitWorld, out int answerIndex)
    {
        answerIndex = -1;

        // Convertit la position hit en coordonnées locales de la face (PlaneMesh en XZ).
        var p = _cardFrontMesh.ToLocal(hitWorld);
        var plane = GetCardPlaneSize();
        var w = Math.Max(0.0001f, plane.X);
        var h = Math.Max(0.0001f, plane.Y);

        var u = (p.X / w) + 0.5f;
        var v = (p.Z / h) + 0.5f; // 0 en haut (Z=-h/2), 1 en bas (Z=+h/2)

        // Mapping précis: on utilise la mise en page réelle (rectangles des 4 réponses dans le SubViewport).
        if (IsInstanceValid(_cardFrontViewport) && _cardAnswerRows.Count == 4 && _cardAnswerRows.All(IsInstanceValid))
        {
            var vpSize = _cardFrontViewport.Size;
            var pt = new Vector2(u * vpSize.X, v * vpSize.Y);

            for (var i = 0; i < _cardAnswerRows.Count; i++)
            {
                var rect = _cardAnswerRows[i].GetGlobalRect();
                if (rect.HasPoint(pt))
                {
                    answerIndex = i;
                    return true;
                }
            }
        }

        // Fallback (ancienne logique par bandes)
        if (u < ClickAnswerRegionUMargin || u > 1.0f - ClickAnswerRegionUMargin)
            return false;

        var top = Mathf.Min(ClickAnswerRegionVTop, ClickAnswerRegionVBottom);
        var bottom = Mathf.Max(ClickAnswerRegionVTop, ClickAnswerRegionVBottom);
        if (v < top || v > bottom)
            return false;

        var t = (v - top) / Mathf.Max(0.0001f, bottom - top);
        var idx = Mathf.Clamp((int)(t * 4.0f), 0, 3);
        answerIndex = idx;
        return true;
    }

    private Vector2 GetCardPlaneSize()
    {
        try
        {
            if (IsInstanceValid(_cardFrontMesh) && _cardFrontMesh.Mesh is PlaneMesh pm)
                return pm.Size;
        }
        catch
        {
        }

        // Fallback (paysage)
        return new Vector2(1.344f, 0.96f);
    }

    private Vector3 GetCardVisibleCenterWorld()
    {
        if (_cardIsBackSide)
        {
            if (IsInstanceValid(_cardBackMesh))
                return _cardBackMesh.GlobalTransform.Origin;
        }

        if (IsInstanceValid(_cardFrontMesh))
            return _cardFrontMesh.GlobalTransform.Origin;

        return _cardRig.GlobalTransform.Origin;
    }

    private static Transform3D WithBasisKeepingCardCenter(Basis basis, Vector3 cardCenterWorld, Vector3 localOffset)
    {
        var origin = cardCenterWorld - (basis * localOffset);
        return new Transform3D(basis, origin);
    }

    private Transform3D WithBasisKeepingCardCenter(Basis basis, Vector3 cardCenterWorld)
    {
        // Le mesh visible n'a pas exactement le même offset local en recto vs verso.
        var localOffset = Vector3.Zero;
        if (_cardIsBackSide && IsInstanceValid(_cardBackMesh))
            localOffset = _cardBackMesh.Position;
        else if (IsInstanceValid(_cardFrontMesh))
            localOffset = _cardFrontMesh.Position;

        return WithBasisKeepingCardCenter(basis, cardCenterWorld, localOffset);
    }

    private void StartRun()
    {
        _runToken++;
        _questionToken = 0;
        CancelInFlightAnimations();

        _awaitingContinueClick = false;
        _continueRunToken = 0;
        _continueQuestionToken = 0;

        _runActive = true;
        _answeredCurrent = false;
        _hasCurrentQuestion = false;
        _timeRemaining = Math.Max(10, TimeLimitSeconds);

        _answered = 0;
        _correct = 0;
        _domainStats.Clear();

        foreach (var d in _usedByDomain.Keys.ToList())
            _usedByDomain[d].Clear();

        _restartButton.Visible = false;

        // Immersion: pendant le run, tout se passe sur la carte 3D.
        _panelRoot.Visible = false;
        _feedbackLabel.Text = "";
        _feedbackLabel.SelfModulate = NeutralText;

        foreach (var b in _answerButtons)
        {
            b.Visible = false;
            b.Disabled = false;
        }

        NextQuestion(first: true);
    }

    private void EndRun()
    {
        CancelInFlightAnimations();

        _awaitingContinueClick = false;

        _runActive = false;
        _answeredCurrent = true;
        _hasCurrentQuestion = false;

        foreach (var b in _answerButtons)
            b.Visible = false;

        // On ré-affiche le panneau uniquement pour le résumé / rejouer.
        _panelRoot.Visible = true;

        _domainLabel.Text = "Domaine: —";
        _timerLabel.Text = "⏱️ 00:00";
        _scoreLabel.Text = $"Score: {_correct}/{_answered}";

        var accuracy = _answered <= 0 ? 0 : (int)Math.Round(100.0 * _correct / _answered);
        _questionLabel.Text = $"⏳ Temps écoulé !\n\nScore: {_correct}/{_answered} ({accuracy}%)\n\nRejouer pour un nouveau tirage aléatoire.";

        _feedbackLabel.Text = BuildDomainBreakdown();
        _feedbackLabel.SelfModulate = NeutralText;

        _restartButton.Text = "Rejouer";
        _restartButton.Visible = true;
        _restartButton.Disabled = false;
        _restartButton.GrabFocus();
    }

    private string BuildDomainBreakdown()
    {
        if (_domainStats.Count == 0)
            return "";

        var ordered = DomainWeights.Select(d => d.Domain).ToList();
        var parts = new List<string>();

        foreach (var domain in ordered)
        {
            if (!_domainStats.TryGetValue(domain, out var s))
                continue;

            var icon = DomainIcons.TryGetValue(domain, out var i) ? i : "🧩";
            var acc = s.asked <= 0 ? 0 : (int)Math.Round(100.0 * s.correct / s.asked);
            parts.Add($"{icon} {domain}: {s.correct}/{s.asked} ({acc}%)");
        }

        return "Répartition (run):\n" + string.Join("\n", parts);
    }

    private void NextQuestion(bool first = false)
    {
        _questionToken++;

        _cardIsBackSide = false;
        _awaitingContinueClick = false;

        _answeredCurrent = false;
        _chosenAnswerIndex = -1;
        _correctAnswerIndex = -1;
        _feedbackLabel.Text = "";
        _feedbackLabel.SelfModulate = NeutralText;

        foreach (var b in _answerButtons)
        {
            b.Disabled = false;
        }

        if (!TryDrawQuestionWeighted(out var q))
        {
            // Mode infini: si on a épuisé les questions (ou la politique anti-répétition), on recycle.
            foreach (var d in _usedByDomain.Keys.ToList())
                _usedByDomain[d].Clear();

            if (!TryDrawQuestionWeighted(out q))
            {
                _questionLabel.Text = "Aucune question disponible.";
                EndRun();
                return;
            }
        }

        _hasCurrentQuestion = true;
        _currentQuestion = q;

        ApplyCardStyleForDifficulty(_currentQuestion.Difficulty);
        SetBackSidePending();

        _currentOptions = BuildShuffledOptions(_currentQuestion);

        // Texte UI (overlay) + face de carte
        _questionLabel.Text = _currentQuestion.Prompt;
        _cardQuestionLabel.Text = _currentQuestion.Prompt;
        for (var i = 0; i < _answerButtons.Count; i++)
            _answerButtons[i].Text = _currentOptions[i].Text;

        // Réponses visibles directement sur la carte (effet "jeu de cartes")
        for (var i = 0; i < _cardAnswerLabels.Count && i < _currentOptions.Length; i++)
            _cardAnswerLabels[i].Text = $"{i + 1}) {_currentOptions[i].Text}";

        // Résultat sur le recto: reset (Bonne/Mauvaise à droite).
        foreach (var r in _cardAnswerResultLabels)
        {
            if (IsInstanceValid(r))
                r.Text = "";
        }

        // Reset du look "boutons" (neutre) à chaque question.
        _hoveredAnswerIndex = -1;
        ApplyAnswerRowVisualState();

        _domainLabel.Text = FormatDomain(_currentQuestion.Domain);
        _cardDomainLabel.Text = FormatDomainFace(_currentQuestion.Domain);
        UpdateTopRow();

        // Pas de focus forcé: on veut éviter l'UI overlay et jouer au clavier.

        // Tirage depuis le deck vers la caméra
        AnimateDrawFromDeck(first);
    }

    private void AnimateDrawFromDeck(bool first)
    {
        SuppressIdle(0.60);
        
        // Petit son de pioche/pose.
        PlaySfx(_sfxDraw, pitch: 0.98f + (float)_rng.NextDouble() * 0.06f);

        if (!IsInstanceValid(_deckSpawn) || !IsInstanceValid(_cardFocus))
        {
            if (first) AnimateCardEnter(); else AnimateCardFlip();
            return;
        }

        // La carte "sort" du deck.
        var startT = _deckSpawn.GlobalTransform;
        startT.Basis = startT.Basis.Rotated(Vector3.Up, Mathf.DegToRad(-18f));

        var liftT = startT;
        liftT.Origin = liftT.Origin + new Vector3(0, 0.05f, 0);

        var focusT = GetFocusTransformGlobal();

        _cardRig.GlobalTransform = startT;

        KillTween(ref _cardTween);
        var tween = CreateTween();
        _cardTween = tween;
        tween.TweenProperty(_cardRig, "global_transform", liftT, 0.12)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_cardRig, "global_transform", focusT, 0.22)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        tween.TweenCallback(Callable.From(() => _cardTween = null));
    }

    private void ApplyCardStyleForDifficulty(int difficulty)
    {
        // Facile: papier clair; Moyen: neutre; Difficile: sombre collector
        var d = Math.Clamp(difficulty, 1, 3);
        _isDarkCardTheme = d >= 3;

        if (_cardFrontMat != null)
        {
            if (d == 1)
            {
                _cardFrontMat.SetShaderParameter("bg_color", new Color(0.98f, 0.97f, 0.94f, 1f));
                _cardFrontMat.SetShaderParameter("base_tint", new Color(1.00f, 0.98f, 0.95f, 1f));
                _cardFrontMat.SetShaderParameter("grain_strength", 0.06f);
                _cardFrontMat.SetShaderParameter("grain_scale", 12.0f);
                _cardFrontMat.SetShaderParameter("contrast", 1.25f);
            }
            else if (d == 2)
            {
                _cardFrontMat.SetShaderParameter("bg_color", new Color(0.92f, 0.94f, 1.00f, 1f));
                _cardFrontMat.SetShaderParameter("base_tint", new Color(0.95f, 0.96f, 1.00f, 1f));
                _cardFrontMat.SetShaderParameter("grain_strength", 0.07f);
                _cardFrontMat.SetShaderParameter("grain_scale", 11.0f);
                _cardFrontMat.SetShaderParameter("contrast", 1.28f);
            }
            else
            {
                // Thème sombre, mais lisible: on garde un fond foncé tout en évitant de "tuer" l'UI.
                _cardFrontMat.SetShaderParameter("bg_color", new Color(0.10f, 0.105f, 0.13f, 1f));
                _cardFrontMat.SetShaderParameter("base_tint", new Color(0.95f, 0.96f, 1.00f, 1f));
                _cardFrontMat.SetShaderParameter("grain_strength", 0.08f);
                _cardFrontMat.SetShaderParameter("grain_scale", 10.0f);
                _cardFrontMat.SetShaderParameter("contrast", 1.10f);
            }
        }

        if (_cardBackMat != null)
        {
            // IMPORTANT: le shader multiplie TOUTE la couleur finale par base_tint.
            // Si on met un tint sombre ici, on "tue" aussi le texte du SubViewport.
            // On garde un verso sombre via bg_color (défini au setup), mais on laisse
            // la texture UI (texte) rester lisible.
            _cardBackMat.SetShaderParameter("base_tint", new Color(1f, 1f, 1f, 1f));
            _cardBackMat.SetShaderParameter("grain_strength", 0.07f);
            _cardBackMat.SetShaderParameter("grain_scale", 10.0f);
            _cardBackMat.SetShaderParameter("contrast", 1.05f);
        }

        // Lisibilité: carte sombre => titres dorés, réponses en blanc cassé.
        // Carte claire => texte noir.
        var faceText = d >= 3 ? DarkCardGold : LightCardBlack;
        var answersText = d >= 3 ? DarkCardAnswerText : faceText;
        if (IsInstanceValid(_cardDomainLabel))
            _cardDomainLabel.SelfModulate = faceText;
        if (IsInstanceValid(_cardQuestionLabel))
            _cardQuestionLabel.SelfModulate = faceText;
        foreach (var l in _cardAnswerLabels)
        {
            if (IsInstanceValid(l))
                l.SelfModulate = answersText;
        }

        // Le verso est sur fond sombre: on force un texte clair stable.
        if (IsInstanceValid(_cardBackContent))
            _cardBackContent.AddThemeColorOverride("default_color", NeutralText);

        // S'assure que le style des réponses suit le thème (bordure ou non).
        ApplyAnswerRowVisualState();
    }

    private void SetBackSidePending()
    {
        if (!IsInstanceValid(_cardBackContent))
            return;

        _cardBackContent.Text = "Réponds d'abord, puis retourne la carte.";
    }

    private void Choose(int chosenIndex)
    {
        if (!_runActive)
            return;
        if (_answeredCurrent)
            return;
        if (!_hasCurrentQuestion)
            return;
        if (chosenIndex < 0 || chosenIndex >= _currentOptions.Length)
            return;

        _answeredCurrent = true;

        var ok = _currentOptions[chosenIndex].IsCorrect;
        _answered++;
        if (ok) _correct++;

        var domain = _currentQuestion.Domain;
        if (!_domainStats.TryGetValue(domain, out var stat))
            stat = (0, 0);
        stat.asked += 1;
        if (ok) stat.correct += 1;
        _domainStats[domain] = stat;

        _feedbackLabel.Text = ok
            ? $"✅ Correct ! {_currentQuestion.Explanation}"
            : $"❌ Faux. Réponse: {_currentQuestion.CorrectAnswer}. {_currentQuestion.Explanation}";
        _feedbackLabel.SelfModulate = ok ? CorrectAccent : WrongAccent;

        HighlightAnswers(chosenIndex);

        foreach (var b in _answerButtons)
            b.Disabled = true;

        UpdateTopRow();

        if (ok)
            FxCorrect();
        else
            FxWrong();

        SetBackSideResult(chosenIndex, ok);
        AnimateCardRevealBack();

        // Laisser lire le verso: l'utilisateur doit cliquer sur la carte pour continuer.
        _awaitingContinueClick = true;
        _continueRunToken = _runToken;
        _continueQuestionToken = _questionToken;
    }

    private void AnimateCardSendAwayAndMaybeShuffle(int runTokenAtStart, int questionTokenAtStart)
    {
        if (!_runActive)
            return;
        if (_runToken != runTokenAtStart)
            return;
        if (_questionToken != questionTokenAtStart)
            return;

        SuppressIdle(0.80);

        if (!IsInstanceValid(_discardTarget))
        {
            // Fallback: juste enchaîner
            if (_runActive) NextQuestion();
            return;
        }

        KillTween(ref _cardTween);
        var tween = CreateTween();
        _cardTween = tween;
        var startT = _cardRig.GlobalTransform;
        var liftT = startT;
        liftT.Origin = liftT.Origin + new Vector3(0, 0.02f, 0);

        var discardT = _discardTarget.GlobalTransform;
        discardT.Basis = discardT.Basis.Rotated(Vector3.Up, Mathf.DegToRad(25f));

        tween.TweenProperty(_cardRig, "global_transform", liftT, 0.10)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_cardRig, "global_transform", discardT, 0.20)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);

        tween.TweenCallback(Callable.From(() =>
        {
            if (!_runActive)
                return;
            if (_runToken != runTokenAtStart)
                return;
            if (_questionToken != questionTokenAtStart)
                return;

            _visualDeckRemaining = Math.Max(0, _visualDeckRemaining - 1);
            UpdateDeckVisual();
        }));

        tween.TweenInterval(0.05);

        tween.TweenCallback(Callable.From(() =>
        {
            if (!_runActive)
                return;
            if (_runToken != runTokenAtStart)
                return;
            if (_questionToken != questionTokenAtStart)
                return;

            if (_visualDeckRemaining <= 0)
            {
                AnimateDeckShuffle();
                _visualDeckRemaining = _visualDeckCapacity;
                UpdateDeckVisual();
            }

            if (_runActive)
                NextQuestion();
        }));

        tween.TweenCallback(Callable.From(() => _cardTween = null));
    }

    private void AnimateDeckShuffle()
    {
        if (!IsInstanceValid(_deckRig))
            return;

        PlaySfx(_sfxShuffle, pitch: 0.96f + (float)_rng.NextDouble() * 0.08f);

        SuppressIdle(0.75);
        KillTween(ref _deckTween);
        var tween = CreateTween();
        _deckTween = tween;
        var startRot = _deckRig!.RotationDegrees;
        var startPos = _deckRig!.Position;

        // Petit "mélange" visible: lift + secousses + retour
        tween.TweenProperty(_deckRig, "position", startPos + new Vector3(0, 0.03f, 0), 0.10)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(_deckRig, "rotation_degrees", startRot + new Vector3(0, 0, 6f), 0.10)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        tween.TweenProperty(_deckRig, "rotation_degrees", startRot + new Vector3(0, 0, -6f), 0.10)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_deckRig, "rotation_degrees", startRot + new Vector3(0, 0, 4f), 0.08)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_deckRig, "rotation_degrees", startRot, 0.08)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);

        tween.Parallel().TweenProperty(_deckRig, "position", startPos, 0.16)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
    }

    private void SetBackSideResult(int chosenIndex, bool ok)
    {
        if (!IsInstanceValid(_cardBackContent))
            return;

        var chosenText = _currentOptions[chosenIndex].Text;
        var correctIndex = Array.FindIndex(_currentOptions, o => o.IsCorrect);
        var correctText = correctIndex >= 0 && correctIndex < _currentOptions.Length
            ? _currentOptions[correctIndex].Text
            : _currentQuestion.CorrectAnswer;

        var chosenColor = ok ? "#2EE59C" : "#FF4D5A";
        var icon = ok ? "✅" : "❌";

        var lines = new List<string>();
        for (var i = 0; i < _currentOptions.Length; i++)
        {
            var letter = (char)('A' + i);
            var t = EscapeBbcode(_currentOptions[i].Text);

            var tag = "";
            if (i == correctIndex)
                tag += " [b](bonne réponse)[/b]";
            if (i == chosenIndex)
                tag += " [b](ta réponse)[/b]";

            if (i == correctIndex)
                lines.Add($"[color=#2EE59C]{letter}. {t}{tag}[/color]");
            else if (i == chosenIndex)
                lines.Add($"[color=#FF4D5A]{letter}. {t}{tag}[/color]");
            else
                lines.Add($"{letter}. {t}{tag}");
        }

        // Note: on ne “fabrique” pas l’explication des autres choix; on affiche l’explication fournie.
        _cardBackContent.Text =
            $"{icon} [b]Ta réponse:[/b] [color={chosenColor}]{EscapeBbcode(chosenText)}[/color]\n" +
            $"[b]Bonne réponse:[/b] [color=#2EE59C]{EscapeBbcode(correctText)}[/color]\n\n" +
            $"[b]Choix proposés:[/b]\n{string.Join("\n", lines)}\n\n" +
            $"[b]Pourquoi ?[/b]\n{EscapeBbcode(_currentQuestion.Explanation)}\n\n" +
            $"[center][b]Clique sur la carte pour continuer[/b][/center]";
    }

    private static string EscapeBbcode(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        return s
            .Replace("[", "\\[")
            .Replace("]", "\\]");
    }

    private void UpdateTopRow()
    {
        var score = $"Score: {_correct}/{_answered}";
        var timer = $"{BuildMarker} ⏱️ {FormatTime(_timeRemaining)}";
        if (IsInstanceValid(_scoreLabel))
            _scoreLabel.Text = score;
        if (IsInstanceValid(_timerLabel))
            _timerLabel.Text = timer;

        // Défensif: si la scène HUD a été modifiée (ex: suppression du post-it),
        // on évite un crash et on re-résout les refs si besoin.
        if (!IsInstanceValid(_topScoreLabel))
            _topScoreLabel = GetNodeOrNull<Label>("TopHUD/TopRow/Score") ?? _topScoreLabel;
        if (!IsInstanceValid(_topTimerLabel))
            _topTimerLabel = GetNodeOrNull<Label>("TopHUD/TopRow/Timer") ?? _topTimerLabel;

        if (IsInstanceValid(_topScoreLabel))
            _topScoreLabel.Text = score;
        if (IsInstanceValid(_topTimerLabel))
            _topTimerLabel.Text = timer;

        // Défensif: les UI de SubViewport peuvent être recréées / rechargées.
        if (!IsInstanceValid(_cardScoreFaceLabel) || !IsInstanceValid(_cardTimerFaceLabel))
        {
            var faceUi = GetNodeOrNull<Control>("../../CardRig/CardFrontViewport/Face");
            if (IsInstanceValid(faceUi))
            {
                if (!IsInstanceValid(_cardScoreFaceLabel))
                    _cardScoreFaceLabel = faceUi.GetNodeOrNull<Label>("%FaceScore") ?? _cardScoreFaceLabel;
                if (!IsInstanceValid(_cardTimerFaceLabel))
                    _cardTimerFaceLabel = faceUi.GetNodeOrNull<Label>("%FaceTimer") ?? _cardTimerFaceLabel;
            }
        }

        if (IsInstanceValid(_cardScoreFaceLabel))
            _cardScoreFaceLabel.Text = score;
        if (IsInstanceValid(_cardTimerFaceLabel))
            _cardTimerFaceLabel.Text = $"⏱️ {FormatTime(_timeRemaining)}";
    }

    private static string FormatTime(double seconds)
    {
        var s = Math.Max(0, (int)Math.Ceiling(seconds));
        var m = s / 60;
        var r = s % 60;
        return $"{m:00}:{r:00}";
    }

    private string FormatDomain(string domain)
    {
        var icon = DomainIcons.TryGetValue(domain, out var i) ? i : "🧩";
        return $"Domaine: {icon} {domain}";
    }

    private string FormatDomainFace(string domain)
    {
        var icon = DomainIcons.TryGetValue(domain, out var i) ? i : "🧩";
        return $"{icon} {domain}";
    }

    private void HighlightAnswers(int chosenIndex)
    {
        var correctIndex = Array.FindIndex(_currentOptions, o => o.IsCorrect);
        _chosenAnswerIndex = chosenIndex;
        _correctAnswerIndex = correctIndex;

        // Recto (sur la carte): indicateur explicite à droite, sans changer les teintes de fond.
        for (var i = 0; i < _cardAnswerResultLabels.Count; i++)
        {
            var label = _cardAnswerResultLabels[i];
            if (!IsInstanceValid(label))
                continue;

            if (i == correctIndex)
            {
                label.Text = "Bonne";
                label.SelfModulate = NeutralText;
            }
            else if (i == chosenIndex)
            {
                label.Text = "Mauvaise";
                label.SelfModulate = NeutralText;
            }
            else
            {
                label.Text = "";
                label.SelfModulate = NeutralText;
            }
        }

        // Ne pas recolorer les boutons UI par code: on laisse le thème gérer l'apparence.
    }

    private void FxCorrect()
    {
        SuppressIdle(0.30);
        PlaySfx(_sfxCorrect, pitch: 0.98f + (float)_rng.NextDouble() * 0.05f);
        if (IsInstanceValid(_sparkles))
        {
            _sparkles.Emitting = false;
            _sparkles.Restart();
            _sparkles.Emitting = true;
        }

        var tween = CreateTween();
        var baseRot = _cardRig.Rotation;
        tween.TweenProperty(_cardRig, "rotation", new Vector3(baseRot.X - 0.08f, baseRot.Y, baseRot.Z), 0.08);
        tween.TweenProperty(_cardRig, "rotation", baseRot, 0.14);
    }

    private void FxWrong()
    {
        SuppressIdle(0.20);
        PlaySfx(_sfxWrong, pitch: 0.98f + (float)_rng.NextDouble() * 0.05f);
        // Petit shake
        var tween = CreateTween();
        var p = _cardRig.Position;
        tween.TweenProperty(_cardRig, "position", p + new Vector3(0.03f, 0, 0), 0.04);
        tween.TweenProperty(_cardRig, "position", p - new Vector3(0.03f, 0, 0), 0.04);
        tween.TweenProperty(_cardRig, "position", p, 0.04);
    }

    private void AnimateCardEnter()
    {
        SuppressIdle(0.35);
        var tween = CreateTween();
        var basePos = _cardRig.Position;
        var baseRot = _cardRig.Rotation;
        _cardRig.Position = basePos + new Vector3(0, -0.05f, 0);
        _cardRig.Rotation = baseRot;
        tween.TweenProperty(_cardRig, "position", basePos, 0.18);
        tween.TweenProperty(_cardRig, "rotation", baseRot + new Vector3(0.02f, 0, 0), 0.18);
        tween.TweenProperty(_cardRig, "rotation", baseRot, 0.12);
    }

    private void AnimateCardFlip()
    {
        // Flip court utilisé uniquement en fallback (sans deck): pivot au centre (style croupier), sans changer la position finale.
        SuppressIdle(0.35);

        var tween = CreateTween();
        var startPos = _cardRig.Position;

        var liftPos = startPos + new Vector3(0, 0.03f, 0);

        tween.TweenProperty(_cardRig, "position", liftPos, 0.12)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        var sign = _flipLeftNext ? -1f : 1f;
        _flipLeftNext = !_flipLeftNext;

        // IMPORTANT: flip recto/verso => rotation autour d'un axe DANS le plan.
        // Style "croupier": bascule avant/arrière autour de l'axe largeur (X).
        tween.Parallel().TweenProperty(_cardRig, "rotation_degrees", new Vector3(105f * sign, 0f, 0f), 0.12)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);

        tween.TweenCallback(Callable.From(() =>
        {
            _cardRig.RotationDegrees = new Vector3(-105f * sign, 0f, 0f);
        }));

        tween.TweenProperty(_cardRig, "position", startPos, 0.12)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(_cardRig, "rotation_degrees", Vector3.Zero, 0.12)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private Transform3D GetBackTransformGlobal()
    {
        var focusT = GetFocusTransformGlobal();
        var frontBasis = focusT.Basis;
        // La face de carte est une PlaneMesh (normale locale ~Y). Un vrai recto/verso nécessite
        // une rotation autour d'un axe dans le plan (X ou Z).
        // Flip "sur le côté" => rotation autour de l'axe hauteur (Z), pour garder le texte lisible.
        var axis = frontBasis.Z;
        var backBasis = frontBasis.Rotated(axis, Mathf.Pi);
        return WithBasisAtOrigin(backBasis, focusT.Origin);
    }

    private void AnimateCardRevealBack()
    {
        // Retourne la carte (verso) recto/verso avec pivot au centre (flip latéral).
        // Important: la position finale reste exactement celle d'avant le flip, sinon illisible.
        SuppressIdle(0.70);
        
        PlaySfx(_sfxFlip, pitch: 0.97f + (float)_rng.NextDouble() * 0.06f);

        var tween = CreateTween();
        _cardTween = tween;
        var startT = _cardRig.GlobalTransform;
        var baseCenter = IsInstanceValid(_cardFrontMesh) ? _cardFrontMesh.GlobalTransform.Origin : GetCardVisibleCenterWorld();
        var liftCenter = baseCenter + new Vector3(0, 0.025f, 0);

        // On calcule les transforms en utilisant l'offset local du recto (avant de basculer l'état).
        var frontLocalOffset = IsInstanceValid(_cardFrontMesh) ? _cardFrontMesh.Position : Vector3.Zero;

        _cardIsBackSide = true;

        // IMPORTANT: on part de la base actuelle (sinon la carte "glisse" car le pivot n'est pas au centre du Node).
        var frontBasis = startT.Basis;
        var axis = frontBasis.Z; // axe dans le plan (flip sur le côté)

        // La direction (droite/gauche) se voit dans l'intermédiaire, pas dans l'état final (π == -π).
        var sign = _flipLeftNext ? -1f : 1f;
        _flipLeftNext = !_flipLeftNext;
        _lastFlipSign = sign;

        var midBasis = frontBasis.Rotated(axis, sign * (Mathf.Pi * 0.5f));
        var backBasis = frontBasis.Rotated(axis, sign * Mathf.Pi);

        var liftT = WithBasisKeepingCardCenter(frontBasis, liftCenter, frontLocalOffset);
        var midLiftT = WithBasisKeepingCardCenter(midBasis, liftCenter, frontLocalOffset);
        var backLiftT = WithBasisKeepingCardCenter(backBasis, liftCenter, frontLocalOffset);
        // Position finale identique (centre de carte conservé).
        var backT = WithBasisKeepingCardCenter(backBasis, baseCenter, frontLocalOffset);

        tween.TweenProperty(_cardRig, "global_transform", liftT, 0.10)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        // Souplesse: courbure au milieu du flip.
        if (_cardFrontMat != null)
            tween.Parallel().TweenProperty(_cardFrontMat, "shader_parameter/bend", sign * 1.0f, 0.20)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        if (_cardBackMat != null)
            tween.Parallel().TweenProperty(_cardBackMat, "shader_parameter/bend", sign * 1.0f, 0.20)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);

        tween.TweenProperty(_cardRig, "global_transform", midLiftT, 0.20)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_cardRig, "global_transform", backLiftT, 0.18)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);

        // Relâche la courbure en fin de flip.
        if (_cardFrontMat != null)
            tween.Parallel().TweenProperty(_cardFrontMat, "shader_parameter/bend", 0.0f, 0.18)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);
        if (_cardBackMat != null)
            tween.Parallel().TweenProperty(_cardBackMat, "shader_parameter/bend", 0.0f, 0.18)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);

        tween.TweenProperty(_cardRig, "global_transform", backT, 0.10)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);

        tween.TweenCallback(Callable.From(() => _cardTween = null));
    }

    private void AnimateCardReturnFront()
    {
        SuppressIdle(0.65);
        
        PlaySfx(_sfxFlip, pitch: 0.97f + (float)_rng.NextDouble() * 0.06f);
        var tween = CreateTween();
        _cardTween = tween;

        var startT = _cardRig.GlobalTransform;
        var startBasis = startT.Basis;
        var baseCenter = GetCardVisibleCenterWorld();
        var liftCenter = baseCenter + new Vector3(0, 0.02f, 0);

        // Offset local correspondant au verso (état courant).
        var backLocalOffset = IsInstanceValid(_cardBackMesh) ? _cardBackMesh.Position : (IsInstanceValid(_cardFrontMesh) ? _cardFrontMesh.Position : Vector3.Zero);

        var focusT = GetFocusTransformGlobal();
        var frontBasis = focusT.Basis;
        var axis = frontBasis.Z; // flip sur le côté

        // On revient par le même côté que l'aller, pour éviter les inversions étranges.
        var sign = _lastFlipSign;
        var midBasis = startBasis.Rotated(axis, -sign * (Mathf.Pi * 0.5f));

        var liftT = WithBasisKeepingCardCenter(startBasis, liftCenter, backLocalOffset);
        var midLiftT = WithBasisKeepingCardCenter(midBasis, liftCenter, backLocalOffset);
        var frontLiftT = WithBasisKeepingCardCenter(frontBasis, liftCenter, backLocalOffset);
        var frontT = WithBasisKeepingCardCenter(frontBasis, baseCenter, backLocalOffset);

        _cardIsBackSide = false;

        tween.TweenProperty(_cardRig, "global_transform", liftT, 0.10)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        // Ré-applique une petite courbure au milieu du retour.
        if (_cardFrontMat != null)
            tween.Parallel().TweenProperty(_cardFrontMat, "shader_parameter/bend", -sign * 1.0f, 0.20)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        if (_cardBackMat != null)
            tween.Parallel().TweenProperty(_cardBackMat, "shader_parameter/bend", -sign * 1.0f, 0.20)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);

        tween.TweenProperty(_cardRig, "global_transform", midLiftT, 0.20)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_cardRig, "global_transform", frontLiftT, 0.18)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);

        if (_cardFrontMat != null)
            tween.Parallel().TweenProperty(_cardFrontMat, "shader_parameter/bend", 0.0f, 0.18)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);
        if (_cardBackMat != null)
            tween.Parallel().TweenProperty(_cardBackMat, "shader_parameter/bend", 0.0f, 0.18)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);

        tween.TweenProperty(_cardRig, "global_transform", frontT, 0.10)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);

        tween.TweenCallback(Callable.From(() => _cardTween = null));
    }

    private void AnimateCardIdle()
    {
        // Bobbing ultra léger, “chill”
        if (!_idleAnchorValid)
        {
            _idleAnchorPos = _cardRig.Position;
            _idleAnchorRot = _cardRig.Rotation;
            _idleAnchorValid = true;
        }

        var t = (float)Time.GetTicksMsec() / 1000f;
        _cardRig.Position = _idleAnchorPos + new Vector3(0, Mathf.Sin(t * 0.8f) * 0.01f, 0);
        _cardRig.Rotation = _idleAnchorRot + new Vector3(Mathf.Sin(t * 0.6f) * 0.01f, Mathf.Sin(t * 0.4f) * 0.015f, 0);
    }

    private void LoadDeck()
    {
        _allQuestions.Clear();
        _indicesByDomain.Clear();
        _usedByDomain.Clear();

        try
        {
            if (!ResourceLoader.Exists(DeckPath))
            {
                GD.PrintErr($"[MiniJeuCartesAWS] Deck missing: {DeckPath}");
                BuildFallbackDeck();
                IndexDeck();
                return;
            }

            using var f = FileAccess.Open(DeckPath, FileAccess.ModeFlags.Read);
            var json = f.GetAsText();

            var deck = JsonSerializer.Deserialize<QuestionDeck>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (deck?.Questions == null || deck.Questions.Length == 0)
            {
                BuildFallbackDeck();
                IndexDeck();
                return;
            }

            foreach (var q in deck.Questions)
            {
                if (q.Answers == null || q.Answers.Length != 4)
                    continue;
                if (q.CorrectIndex < 0 || q.CorrectIndex > 3)
                    continue;
                if (string.IsNullOrWhiteSpace(q.Prompt))
                    continue;
                if (q.Answers.Any(string.IsNullOrWhiteSpace))
                    continue;

                var domain = NormalizeDomain(q.Domain, q.Category);
                var difficulty = Math.Clamp(q.Difficulty <= 0 ? 1 : q.Difficulty, 1, 3);
                var correctAnswer = q.Answers[q.CorrectIndex];

                _allQuestions.Add(new Question(
                    domain,
                    difficulty,
                    q.Prompt,
                    q.Answers,
                    correctAnswer,
                    q.Explanation ?? ""));
            }

            if (_allQuestions.Count == 0)
                BuildFallbackDeck();

            IndexDeck();
        }
        catch (Exception e)
        {
            GD.PrintErr($"[MiniJeuCartesAWS] Deck load error: {e.Message}");
            BuildFallbackDeck();
            IndexDeck();
        }
    }

    private bool TryDrawQuestionWeighted(out Question q)
    {
        q = null!;

        // Si un domaine est épuisé (toutes questions marquées utilisées), on le recycle.
        foreach (var kv in _indicesByDomain)
        {
            var domain = kv.Key;
            var indices = kv.Value;
            if (!_usedByDomain.TryGetValue(domain, out var used))
                continue;
            if (indices.Count > 0 && used.Count >= indices.Count)
                used.Clear();
        }

        // Si tout est vide (deck minuscule), on recycle tout.
        if (_indicesByDomain.Count > 0 && _usedByDomain.Values.All(u => u.Count == 0) == false)
        {
            // ok
        }

        // Tirage pondéré par domaine + biais difficulté
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var domain = ChooseDomainWeighted();
            if (!_indicesByDomain.TryGetValue(domain, out var list) || list.Count == 0)
                continue;

            if (!_usedByDomain.TryGetValue(domain, out var used))
            {
                used = new HashSet<int>();
                _usedByDomain[domain] = used;
            }

            var candidates = list.Where(i => !used.Contains(i)).ToList();
            if (candidates.Count == 0)
            {
                used.Clear();
                candidates = list.ToList();
            }

            // Pondération difficulté 1/2/3
            var picked = PickByDifficulty(candidates);
            used.Add(picked);
            q = _allQuestions[picked];
            return true;
        }

        return false;
    }

    private string ChooseDomainWeighted()
    {
        var total = 0f;
        foreach (var d in DomainWeights)
            total += d.Weight;

        var r = (float)(_rng.NextDouble() * total);
        foreach (var d in DomainWeights)
        {
            r -= d.Weight;
            if (r <= 0)
                return d.Domain;
        }

        return DomainWeights[0].Domain;
    }

    private int PickByDifficulty(List<int> candidates)
    {
        // Bias vers difficile (comme avant)
        float WeightFor(int idx)
        {
            var d = _allQuestions[idx].Difficulty;
            return d == 1 ? 1.0f : (d == 2 ? 1.3f : 1.6f);
        }

        var total = 0f;
        foreach (var c in candidates)
            total += WeightFor(c);

        var r = (float)(_rng.NextDouble() * total);
        foreach (var c in candidates)
        {
            r -= WeightFor(c);
            if (r <= 0)
                return c;
        }

        return candidates[0];
    }

    private void IndexDeck()
    {
        for (var i = 0; i < _allQuestions.Count; i++)
        {
            var d = _allQuestions[i].Domain;
            if (!_indicesByDomain.TryGetValue(d, out var list))
            {
                list = new List<int>();
                _indicesByDomain[d] = list;
                _usedByDomain[d] = new HashSet<int>();
            }

            list.Add(i);
        }
    }

    private void BuildFallbackDeck()
    {
        _allQuestions.Clear();
        _allQuestions.Add(new Question(
            "Technology",
            1,
            "Quel service AWS est un stockage d'objets ?",
            new[] { "Amazon S3", "Amazon RDS", "AWS Lambda", "Amazon EC2" },
            "Amazon S3",
            "Amazon S3 est un service de stockage d'objets (buckets/objets)."));

        _allQuestions.Add(new Question(
            "Security",
            1,
            "Quel service gère des rôles et des politiques d'accès ?",
            new[] { "AWS IAM", "Amazon VPC", "Amazon Route 53", "Amazon CloudFront" },
            "AWS IAM",
            "IAM (Identity and Access Management) permet de gérer utilisateurs, rôles et politiques."));

        _allQuestions.Add(new Question(
            "Billing",
            1,
            "Quel outil aide à optimiser les coûts AWS ?",
            new[] { "AWS Cost Explorer", "AWS Shield", "AWS Snowball", "AWS WAF" },
            "AWS Cost Explorer",
            "Cost Explorer sert à analyser l'usage et les coûts pour détecter des optimisations."));

    }

    private static string NormalizeDomain(string domain, string category)
    {
        var d = (domain ?? string.Empty).Trim();
        if (d.Length > 0)
            return d;

        // Compat: si l'ancien deck n'a que `category`
        var c = (category ?? string.Empty).Trim();
        if (c.Equals("Security", StringComparison.OrdinalIgnoreCase) || c.Equals("Compliance", StringComparison.OrdinalIgnoreCase))
            return "Security";
        if (c.Equals("Billing", StringComparison.OrdinalIgnoreCase))
            return "Billing";

        // Tout le reste: services/tech
        return "Technology";
    }

    private AnswerOption[] BuildShuffledOptions(Question q)
    {
        var opts = q.Answers
            .Select(a => new AnswerOption(a, a == q.CorrectAnswer))
            .ToList();

        // Shuffle
        for (var i = opts.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (opts[i], opts[j]) = (opts[j], opts[i]);
        }

        return opts.ToArray();
    }

    private readonly record struct AnswerOption(string Text, bool IsCorrect);

    private sealed record Question(
        string Domain,
        int Difficulty,
        string Prompt,
        string[] Answers,
        string CorrectAnswer,
        string Explanation);

    private sealed class QuestionDeck
    {
        public string DeckId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public QuestionItem[] Questions { get; set; } = Array.Empty<QuestionItem>();
    }

    private sealed class QuestionItem
    {
        public string Category { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public int Difficulty { get; set; } = 1;

        public string Prompt { get; set; } = string.Empty;
        public string[] Answers { get; set; } = Array.Empty<string>();
        public int CorrectIndex { get; set; }
        public string Explanation { get; set; } = string.Empty;
    }
}
