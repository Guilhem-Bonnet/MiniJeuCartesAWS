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

    // Défausse (visuel): pile qui grossit au fur et à mesure.
    [Export] public bool EnableDiscardPile { get; set; } = true;

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
    
    // Audio: on utilise uniquement les AudioStreamPlayer3D placés dans la scène (Main3D.tscn / Audio3D).
    private Node3D? _audio3dRoot;
    private AudioStreamPlayer3D? _sfxFlip;
    private AudioStreamPlayer3D? _sfxDraw;
    private AudioStreamPlayer3D? _sfxShuffle;
    private AudioStreamPlayer3D? _sfxCorrect;
    private AudioStreamPlayer3D? _sfxWrong;
    private AudioStreamPlayer3D? _ambience;

    private Node3D _cardRig = null!;
    private GpuParticles3D? _sparkles;
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

    private bool _cardMotionAnchorValid;
    private Transform3D _cardMotionAnchorGlobal;
    private Vector2 _mouseMotion;

    private Node3D? _deckRig;
    private CsgBox3D? _deckStack;
    private Marker3D? _deckSpawn;
    private Marker3D? _cardFocus;
    private Marker3D? _discardTarget;

    private CsgBox3D? _discardStack;
    private int _visualDiscardCount;
    private bool _discardAnchorsCaptured;
    private float _discardStackBottomY;

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

    // Animations (AnimationPlayer)
    private const string AnimCardDraw = "card_draw";
    private const string AnimCardSendAway = "card_send_away";
    private const string AnimCardFlipFallbackLeft = "card_flip_fallback_left";
    private const string AnimDeckShuffle = "deck_shuffle";

    private AnimationPlayer? _cardAnim;
    private AnimationPlayer? _deckAnim;
    private Node3D? _cardRigOffset;
    private Node3D? _deckRigOffset;

    private enum PendingAnimAction
    {
        None = 0,
        CardSendAwayAndMaybeShuffle = 1,
    }

    private PendingAnimAction _pendingAnimAction;
    private string _pendingAnimName = "";
    private int _pendingRunToken;
    private int _pendingQuestionToken;

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

        // Menu (Paramètres / Quitter)
        InitMenuAndSettingsUI();

        // 3D refs
        _cardRig = GetNode<Node3D>("../../CardRig");
        _camera = GetNode<Camera3D>("../../Camera");
        _cardAnim = GetNodeOrNull<AnimationPlayer>("../../CardRig/CardAnim");
        if (IsInstanceValid(_cardAnim))
        {
            // Evite les doublons en hot-reload, sans générer d'erreur si aucune connexion n'existe.
            SafeReconnect(_cardAnim, SignalAnimationFinished, Callable.From<StringName>(OnCardAnimationFinished));
        }
        _sparkles = GetNodeOrNull<GpuParticles3D>("../../CardRig/Sparkles");
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

        EnsureDiscardPileNodes();

        _deckAnim = GetNodeOrNull<AnimationPlayer>("../../Set/DeckRig/DeckAnim");
        if (IsInstanceValid(_deckAnim))
        {
            SafeReconnect(_deckAnim, SignalAnimationFinished, Callable.From<StringName>(OnDeckAnimationFinished));
        }

        // Le reparenting (offset parents) pendant _Ready peut échouer si le parent est en train
        // de construire ses enfants. On le décale à la fin du frame.
        CallDeferred(nameof(DeferredWorldRigSetup));

        _tableMesh = GetNode<MeshInstance3D>("../../Set/Table");
        _wallMesh = GetNode<MeshInstance3D>("../../Set/BackWall");

        BuildProceduralTextures();
        ApplySceneMaterials();

        ApplyViewportToCardMaterials();

        if (AutoFrameCameraOnReady)
            FrameCameraForReadability();

        _visualDeckRemaining = _visualDeckCapacity;
        UpdateDeckVisual();

        _visualDiscardCount = 0;
        UpdateDiscardVisual();

        LoadDeck();
        ShowReadyScreen();
    }

    private void DeferredWorldRigSetup()
    {
        if (IsInstanceValid(_cardRig))
            _cardRigOffset = EnsureOffsetParent(_cardRig!, "CardRigOffset");

        if (IsInstanceValid(_deckRig))
            _deckRigOffset = EnsureOffsetParent(_deckRig!, "DeckRigOffset");
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
        // Base de micro-motion: par défaut, on part de la pose réelle en scène.
        _cardMotionAnchorValid = true;
        _cardMotionAnchorGlobal = _cardReferenceGlobal;
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

    private void EnsureDiscardPileNodes()
    {
        if (!EnableDiscardPile)
            return;

        if (!IsInstanceValid(_discardTarget))
            return;

        // Préfère un node existant dans la scène: DiscardTarget/DiscardStack.
        _discardStack = _discardTarget!.GetNodeOrNull<CsgBox3D>("DiscardStack");
        if (!IsInstanceValid(_discardStack))
        {
            var size = new Vector3(0.1f, 0.001f, 0.14f);
            if (IsInstanceValid(_deckStack))
                size = _deckStack!.Size;

            _discardStack = new CsgBox3D
            {
                Name = "DiscardStack",
                Size = new Vector3(size.X, 0.001f, size.Z),
                Visible = false,
            };

            // Matériau cohérent avec le deck si possible.
            if (IsInstanceValid(_deckStack) && _deckStack!.Material != null)
                _discardStack.Material = _deckStack.Material;

            _discardTarget.AddChild(_discardStack);
        }

        _discardAnchorsCaptured = false;
    }

    private void CaptureDiscardAnchorsIfNeeded()
    {
        if (_discardAnchorsCaptured)
            return;
        if (!IsInstanceValid(_discardStack))
            return;

        var size = _discardStack!.Size;
        var pos = _discardStack.Position;
        _discardStackBottomY = pos.Y - (size.Y * 0.5f);
        _discardAnchorsCaptured = true;
    }

    private void UpdateDiscardVisual()
    {
        if (!EnableDiscardPile)
            return;
        if (!IsInstanceValid(_discardStack))
            return;

        CaptureDiscardAnchorsIfNeeded();

        if (_visualDiscardCount <= 0)
        {
            _discardStack!.Visible = false;
            return;
        }

        _discardStack!.Visible = true;

        // Même logique que le deck: on augmente la hauteur sans déplacer la base.
        var h = Math.Max(DeckCardThickness * 2.0f, _visualDiscardCount * DeckCardThickness);
        var prevSize = _discardStack.Size;
        var prevPos = _discardStack.Position;
        _discardStack.Size = new Vector3(prevSize.X, h, prevSize.Z);
        _discardStack.Position = new Vector3(prevPos.X, _discardStackBottomY + (h * 0.5f), prevPos.Z);
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

    private Transform3D GetCardMotionBaseTransformGlobal()
    {
        if (_cardMotionAnchorValid)
            return _cardMotionAnchorGlobal;
        return GetFocusTransformGlobal();
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

    private void ApplyCardMicroMotion(double delta)
    {
        // Ne pas lutter contre les animations (tirage, flip, discard, etc.)
        if (IsCardAnimationPlaying())
            return;

        // Capture une base stable (pose fin d'anim) avant d'appliquer la micro-motion.
        // Sans ça, on peut avoir un snap si `GetFocusTransformGlobal()` diffère de la pose
        // réelle de fin du clip (typiquement après `card_draw`).
        if (!_cardMotionAnchorValid && !_cardIsBackSide)
        {
            _cardMotionAnchorValid = true;
            _cardMotionAnchorGlobal = _cardRig.GlobalTransform;
        }

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

            var baseT = GetCardMotionBaseTransformGlobal();
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

    private bool IsCardAnimationPlaying()
    {
        return IsInstanceValid(_cardAnim) && _cardAnim!.IsPlaying();
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

        _visualDeckRemaining = _visualDeckCapacity;
        UpdateDeckVisual();

        _visualDiscardCount = 0;
        UpdateDiscardVisual();

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

        _visualDiscardCount = 0;
        UpdateDiscardVisual();

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

}
