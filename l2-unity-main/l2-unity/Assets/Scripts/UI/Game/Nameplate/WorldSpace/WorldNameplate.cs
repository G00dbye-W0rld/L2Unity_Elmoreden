using UnityEngine;
using TMPro;

// Mirroir world-space de Nameplate.cs (Assets/Scripts/UI/Game/Nameplate/Nameplate/Nameplate.cs) :
// meme logique de couleurs (karma/PvP flag/clignotement) et de bulle de ciblage,
// mais ecrit dans un TMP_Text/MeshRenderer plutot qu'un VisualElement UI Toolkit.
// L'icone est un Quad+MeshRenderer, pas un SpriteRenderer (constate
// invisible en jeu dans ce projet malgre plusieurs materiaux essayes -
// cf. WorldNameplatePrefabGenerator.BuildTransparentQuadMaterial).
//
// Une seule icone (etoile+joyau+branche separatrice, cf.
// NameplateBubbleIconGenerator) remplace les deux bulles gauche/droite
// d'origine. Un MATERIAU/TEXTURE PAR ETAT (Hover/Target/Attack, prepares a
// la main) : changer d'etat = echanger sharedMaterial, meme principe que
// l'ancien systeme a deux bulles - pas une teinte runtime.
public class WorldNameplate
{
    public enum BubbleState { None, Target, Attack, Hover }

    private readonly GameObject _root;
    private readonly TMP_Text _nameText;
    private readonly TMP_Text _titleText;
    private readonly MeshRenderer _bubbleIcon;

    private Material _hoverMaterial;
    private Material _targetMaterial;
    private Material _attackMaterial;

    private int _previousServerTitleColor = -1;
    private int _previousServerNameColor = -1;
    private Color _previousServerNameColorValue;
    private int _lastFlag = 0;
    private int _previousKarmaAmount = 0;
    private bool _blink;
    private float _lastBlinkTime;
    private BubbleState _currentBubbleState = BubbleState.None;

    // Fondu par distance : l'alpha de l'icone est pousse via MaterialPropertyBlock
    // en blanc (le materiau-asset porte deja sa propre couleur/texture d'etat,
    // partagee entre toutes les nameplates - le muter changerait l'icone de
    // tout le monde a la fois). Le texte utilise TMP_Text.alpha, un simple
    // multiplicateur par instance.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock _mpb;
    private float _currentAlpha = 1f;

    public GameObject Root => _root;
    public Transform RootTransform => _root.transform;
    public Transform Target { get; private set; }
    public Entity Entity { get; private set; }
    public float OffsetHeight { get; set; }

    public WorldNameplate(GameObject root)
    {
        _root = root;
        _nameText = root.transform.Find("Name").GetComponent<TMP_Text>();
        _titleText = root.transform.Find("Title").GetComponent<TMP_Text>();
        _bubbleIcon = root.transform.Find("BubbleIcon").GetComponent<MeshRenderer>();
        _mpb = new MaterialPropertyBlock();
        _defaultTitlePosition = _titleText.transform.localPosition;
        _storeTitlePosition = _defaultTitlePosition + new Vector3(0f, StoreTitleLift, 0f);

        // Reinitialise l'alpha : cette instance enveloppe peut-etre un
        // GameObject recycle du pool, dont le texte/MPB gardait un alpha de
        // fondu residuel. Le sentinel force l'application (pas d'early-out).
        _currentAlpha = -1f;
        SetAlpha(1f);
    }

    // Fondu global de la nameplate (1 = opaque, 0 = invisible). Applique au
    // texte (nom + titre) et a l'icone. La comparaison evite de reecrire les
    // proprietes chaque frame quand l'alpha n'a pas bouge de facon perceptible.
    public virtual void SetAlpha(float alpha)
    {
        if (Mathf.Abs(alpha - _currentAlpha) < 0.004f) return;
        _currentAlpha = alpha;

        _nameText.alpha = alpha;
        _titleText.alpha = alpha;
        ApplyIconAlpha();
    }

    // Alpha de fondu seul (blanc, ne touche pas la couleur/texture propre du
    // materiau d'etat courant) via MaterialPropertyBlock.
    private void ApplyIconAlpha()
    {
        _bubbleIcon.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, new Color(1f, 1f, 1f, _currentAlpha));
        _bubbleIcon.SetPropertyBlock(_mpb);
    }

    // Un materiau-asset PAR ETAT (genere par WorldNameplatePrefabGenerator a
    // partir de 3 PNG prepares a la main) : changer d'etat = echanger
    // sharedMaterial, jamais modifier la texture d'un materiau en place (un
    // materiau est partage entre toutes les nameplates - le muter
    // changerait l'icone de tout le monde a la fois).
    public void SetBubbleMaterials(Material hover, Material target, Material attack)
    {
        _hoverMaterial = hover;
        _targetMaterial = target;
        _attackMaterial = attack;
    }

    // Rebind pour la reutilisation depuis le pool - reinitialise tout l'etat
    // de comparaison pour qu'une instance recyclee n'herite pas des valeurs
    // (couleur, bulle) de l'entite precedente qui l'occupait.
    public void Bind(Entity entity)
    {
        _iconsMeasuredName = null;
        AdoptRecycledParts();
        Target = entity.transform;
        Entity = entity;
        _nameText.text = entity.Identity.Name;
        _titleText.text = entity.Identity.Title;
        // Position de l'icone : fixe, celle du prefab (le placement
        // dynamique au bord du nom mesure via TMP donnait des marges
        // asymetriques - abandonne).

        _previousServerTitleColor = -1;
        _previousServerNameColor = -1;
        _previousServerNameColorValue = default;
        _lastFlag = 0;
        _previousKarmaAmount = 0;
        _blink = false;
        _lastBlinkTime = 0f;
        // Hover sert desormais d'etat par defaut TOUJOURS affiche (plus une
        // reaction au survol souris) - cf. NameplatesManagerGame.UpdateWorldBubbleStates.
        SetBubbleState(BubbleState.Hover);

        _shownOperateType = (OperateType)255;

        if (_chatBubble != null)
        {
            _chatBubble.SetActive(false);
        }
    }

    // Le GameObject recycle garde les elements crees pour l'entite precedente :
    // on les reprend et on les eteint, au lieu d'en creer des doublons.
    private void AdoptRecycledParts()
    {
        _crestIcon = null;
        _allyIcon = null;
        _warIcon = null;
        HideIcon(FindIcon("CrestIcon"));
        HideIcon(FindIcon("AllyCrestIcon"));
        HideIcon(FindIcon("WarIcon"));

        if (_chatBubble == null)
        {
            Transform bubble = _root.transform.Find(ChatBubbleName);
            if (bubble != null)
            {
                _chatBubble = bubble.gameObject;
                Transform text = bubble.Find("Text");
                _chatText = text != null ? text.GetComponent<TMP_Text>() : null;
            }
        }
    }

    private NameIcon FindIcon(string name)
    {
        MeshRenderer renderer = FindNameIcon(name);
        return renderer != null ? new NameIcon { Renderer = renderer } : null;
    }

    private OperateType _shownOperateType = (OperateType)255;
    private string _shownStoreMessage;
    private string _shownTitle;

    // Fond du nom de magasin : un quad noir dimensionne sur le texte, plus
    // precis que la balise <mark> de TMP. Cree a la demande, garde par le pool.
    private const string StoreBackgroundName = "StoreBackground";
    private const float StoreBackgroundAlpha = 0.9f;
    private static readonly Vector2 StoreBackgroundPadding = new Vector2(0.14f, 0.07f);
    private static Material _storeBackgroundMaterial;
    private const float StoreTitleLift = 0.08f;
    private Vector3 _defaultTitlePosition;
    private Vector3 _storeTitlePosition;
    private MeshRenderer _storeBackground;
    private Mesh _storeBackgroundMesh;
    private MaterialPropertyBlock _storeBackgroundMpb;
    private float _storeBackgroundShownAlpha = -1f;

    // Un marchand affiche le nom de son magasin a la place du titre, sur fond
    // noir : violet pour la vente, rose pour l'achat, orange pour l'atelier.
    private void UpdateStoreTitle()
    {
        OperateType type = Entity.OperateType;
        string message = Entity.StoreMessage;
        string title = Entity.Identity.Title;

        if (type != _shownOperateType || message != _shownStoreMessage || title != _shownTitle)
        {
            _shownOperateType = type;
            _shownStoreMessage = message;
            _shownTitle = title;

            string color = StoreColor(type);
            // L'encart apparait des que le personnage tient boutique, meme
            // sans nom saisi : l'en-tete suffit a l'annoncer.
            bool isStore = color != null;
            string header = StoreHeader(type);
            string body = string.IsNullOrEmpty(message) ? header : header + "\n" + message;

            _titleText.text = isStore ? $"<color={color}><noparse>{body}</noparse></color>" : title;
            _titleText.transform.localPosition = isStore ? _storeTitlePosition : _defaultTitlePosition;
            RefreshStoreBackground(isStore);
        }

        if (_storeBackground != null && _storeBackground.enabled && !Mathf.Approximately(_storeBackgroundShownAlpha, _currentAlpha))
        {
            ApplyStoreBackgroundAlpha();
        }
    }

    private void RefreshStoreBackground(bool visible)
    {
        if (_storeBackground == null)
        {
            _storeBackground = FindStoreBackground();
        }

        if (!visible)
        {
            if (_storeBackground != null)
            {
                _storeBackground.enabled = false;
            }
            return;
        }

        if (_storeBackground == null)
        {
            _storeBackground = CreateStoreBackground();
        }

        _titleText.ForceMeshUpdate();
        Bounds bounds = _titleText.textBounds;

        Transform t = _storeBackground.transform;
        t.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0.01f);
        WorldBubbleVisual.UpdateSlicedMesh(_storeBackgroundMesh, bounds.size.x + StoreBackgroundPadding.x, bounds.size.y + StoreBackgroundPadding.y);
        _storeBackground.enabled = true;
        ApplyStoreBackgroundAlpha();
    }

    private MeshRenderer FindStoreBackground()
    {
        Transform existing = _titleText.transform.Find(StoreBackgroundName);
        if (existing == null)
        {
            return null;
        }

        _storeBackgroundMesh = existing.GetComponent<MeshFilter>().sharedMesh;
        return existing.GetComponent<MeshRenderer>();
    }

    private MeshRenderer CreateStoreBackground()
    {
        if (_storeBackgroundMaterial == null)
        {
            _storeBackgroundMaterial = WorldBubbleVisual.CreateMaterial(_bubbleIcon.sharedMaterial, _titleText.fontSharedMaterial.renderQueue - 1);
        }

        GameObject go = new GameObject(StoreBackgroundName);
        go.transform.SetParent(_titleText.transform, false);
        _storeBackgroundMesh = new Mesh { name = "StoreBackground" };
        go.AddComponent<MeshFilter>().sharedMesh = _storeBackgroundMesh;

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _storeBackgroundMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    // Suit le fondu par distance de la nameplate, comme le texte.
    private void ApplyStoreBackgroundAlpha()
    {
        _storeBackgroundMpb ??= new MaterialPropertyBlock();
        _storeBackground.GetPropertyBlock(_storeBackgroundMpb);
        _storeBackgroundMpb.SetColor(BaseColorId, new Color(0f, 0f, 0f, StoreBackgroundAlpha * Mathf.Max(0f, _currentAlpha)));
        _storeBackground.SetPropertyBlock(_storeBackgroundMpb);
        _storeBackgroundShownAlpha = _currentAlpha;
    }

    // Bulle de chat au-dessus de la tete : meme habillage que l'encart de
    // magasin, avec une pointe dessous et une disparition en fondu.
    private const string ChatBubbleName = "ChatBubble";
    private const float ChatBubbleDuration = 5f;
    private const float ChatBubbleFadeOut = 0.6f;
    private const float ChatBubbleWidth = 1.9f;
    private const float ChatBubbleBaseHeight = 0.33f;
    private const float ChatBubbleAlpha = 0.82f;
    private static readonly Vector2 ChatBubblePadding = new Vector2(0.16f, 0.09f);
    private static Material _chatBubbleMaterial;

    private GameObject _chatBubble;
    private TMP_Text _chatText;
    private MeshRenderer _chatBackground;
    private MeshRenderer _chatTail;
    private Mesh _chatBackgroundMesh;
    private MaterialPropertyBlock _chatBubbleMpb;
    private float _chatBubbleHideTime;

    public void ShowChatBubble(string text, Color textColor)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_chatBubble == null)
        {
            CreateChatBubble();
        }

        // Activer AVANT de mesurer : TMP ne recalcule pas ses bornes sur un
        // objet desactive, et le fond restait alors invisible.
        _chatBubble.SetActive(true);

        _chatText.color = textColor;
        _chatText.text = text;
        _chatText.ForceMeshUpdate();

        Bounds bounds = _chatText.textBounds;
        float width = bounds.size.x + ChatBubblePadding.x;
        float height = bounds.size.y + ChatBubblePadding.y;

        WorldBubbleVisual.UpdateSlicedMesh(_chatBackgroundMesh, width, height);
        _chatBackground.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0.01f);
        _chatTail.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y - height * 0.5f, 0.01f);

        // Posee au-dessus du titre : le bas de la bulle reste a la meme
        // hauteur quel que soit le nombre de lignes.
        _chatBubble.transform.localPosition = new Vector3(0f, ChatBubbleBaseHeight + height * 0.5f, 0f);

        _chatBubbleHideTime = Time.time + ChatBubbleDuration;
        ApplyChatBubbleAlpha(1f);
    }

    private void CreateChatBubble()
    {
        _chatBubble = new GameObject(ChatBubbleName);
        _chatBubble.transform.SetParent(_root.transform, false);
        _chatBubble.layer = _root.layer;

        // Copie du titre : elle apporte la police, le materiau de texte et la
        // couche de rendu deja valides dans ce projet.
        GameObject textGo = Object.Instantiate(_titleText.gameObject, _chatBubble.transform, false);
        textGo.name = "Text";
        _chatText = textGo.GetComponent<TMP_Text>();
        _chatText.text = "";
        _chatText.color = Color.white;
        _chatText.alignment = TextAlignmentOptions.Center;
        _chatText.textWrappingMode = TextWrappingModes.Normal;
        _chatText.rectTransform.localPosition = Vector3.zero;
        _chatText.rectTransform.sizeDelta = new Vector2(ChatBubbleWidth, 1f);

        Transform copiedStoreBackground = textGo.transform.Find(StoreBackgroundName);
        if (copiedStoreBackground != null)
        {
            Object.Destroy(copiedStoreBackground.gameObject);
        }

        if (_chatBubbleMaterial == null)
        {
            _chatBubbleMaterial = WorldBubbleVisual.CreateMaterial(_bubbleIcon.sharedMaterial, _chatText.fontSharedMaterial.renderQueue - 1);
        }

        _chatBackgroundMesh = new Mesh { name = "ChatBubbleBackground" };
        _chatBackground = CreateChatBubblePiece("Background", _chatBackgroundMesh);

        Mesh tailMesh = new Mesh { name = "ChatBubbleTail" };
        WorldBubbleVisual.UpdateTailMesh(tailMesh);
        _chatTail = CreateChatBubblePiece("Tail", tailMesh);

        _chatBubble.SetActive(false);
    }

    private MeshRenderer CreateChatBubblePiece(string pieceName, Mesh mesh)
    {
        GameObject go = new GameObject(pieceName);
        go.transform.SetParent(_chatBubble.transform, false);
        go.layer = _root.layer;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _chatBubbleMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private void TickChatBubble()
    {
        float remaining = _chatBubbleHideTime - Time.time;
        if (remaining <= 0f)
        {
            _chatBubble.SetActive(false);
            return;
        }

        ApplyChatBubbleAlpha(Mathf.Clamp01(remaining / ChatBubbleFadeOut));
    }

    // Suit le fondu par distance de la nameplate, comme le texte.
    private void ApplyChatBubbleAlpha(float fade)
    {
        float alpha = fade * Mathf.Max(0f, _currentAlpha);
        _chatText.alpha = alpha;

        _chatBubbleMpb ??= new MaterialPropertyBlock();
        _chatBubbleMpb.SetColor(BaseColorId, new Color(0f, 0f, 0f, ChatBubbleAlpha * alpha));
        _chatBackground.SetPropertyBlock(_chatBubbleMpb);
        _chatTail.SetPropertyBlock(_chatBubbleMpb);
    }

    // En-tete affichee au-dessus du message, comme dans le client d'origine.
    private static string StoreHeader(OperateType type)
    {
        switch (type)
        {
            case OperateType.Sell:
            case OperateType.PackageSell:
                return "<Magasin priv\u00e9 - Vente>";
            case OperateType.Buy:
                return "<Magasin priv\u00e9 - Achat>";
            case OperateType.Manufacture:
                return "<Atelier priv\u00e9>";
            default:
                return "";
        }
    }

    private static string StoreColor(OperateType type)
    {
        switch (type)
        {
            case OperateType.Sell:
            case OperateType.PackageSell:
                return "#C08CFF";
            case OperateType.Buy:
                return "#FF8CD2";
            case OperateType.Manufacture:
                return "#FFA040";
            default:
                return null;
        }
    }

    // Port quasi verbatim de Nameplate.ManageColors() - meme ordre de
    // priorite (karma > PvP flag fixe > PvP flag clignotant > couleur
    // serveur par defaut) et memes champs de detection de changement pour
    // eviter de reecrire la couleur a chaque frame.
    // Icones autour du nom : blasons d'alliance et de clan a gauche, symbole de
    // guerre a droite. Des quads comme l'icone de la plaque : texture posee par
    // MaterialPropertyBlock, materiau partage entre toutes les plaques.
    private const float IconGap = 0.35f;
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly Color MutualWarNameColor = new Color(1f, 0.45f, 0.35f);
    private static readonly Color OneSidedWarNameColor = new Color(1f, 0.7f, 0.35f);
    private static Material _iconMaterial;
    private static Mesh _iconMesh;
    private static Texture2D _warSingle;
    private static Texture2D _warDouble;

    private sealed class NameIcon
    {
        public MeshRenderer Renderer;
        public MaterialPropertyBlock Mpb;
        public Texture2D Shown;
        public float Alpha = -1f;
    }

    private NameIcon _crestIcon;
    private NameIcon _allyIcon;
    private NameIcon _warIcon;
    private string _iconsMeasuredName;
    private Bounds _nameBounds;

    private void UpdateNameIcons()
    {
        NetworkIdentity identity = Entity.Identity;
        bool isPlayer = identity.EntityType == EntityType.Player || identity.EntityType == EntityType.User;

        Texture2D crest = null;
        Texture2D ally = null;
        Texture2D war = null;

        if (isPlayer)
        {
            ClanCrests.Request(identity.ClanCrestId);
            ClanCrests.RequestAlly(identity.AllyCrestId);
            crest = identity.ClanCrestId != 0 ? ClanCrests.Get(identity.ClanCrestId) : null;
            ally = identity.AllyCrestId != 0 ? ClanCrests.GetAlly(identity.AllyCrestId) : null;

            if (identity.EntityType == EntityType.User && (identity.IsMutualWar || identity.IsOneSidedWar))
            {
                LoadWarIcons();
                war = identity.IsMutualWar ? _warDouble : _warSingle;
            }
        }

        if (crest == null && ally == null && war == null)
        {
            HideIcon(_crestIcon);
            HideIcon(_allyIcon);
            HideIcon(_warIcon);
            _iconsMeasuredName = null;
            return;
        }

        // Mesure faite seulement quand le nom change, et apres ForceMeshUpdate :
        // sans lui TMP n'a pas encore de dimensions. fontSize n'est pas en unites locales.
        if (_nameText.text != _iconsMeasuredName)
        {
            _nameText.ForceMeshUpdate();
            Bounds bounds = _nameText.textBounds;
            if (bounds.size.y <= 0f)
            {
                return;
            }

            _nameBounds = bounds;
            _iconsMeasuredName = _nameText.text;
        }

        float size = _nameBounds.size.y * 1.3f;
        float left = _nameBounds.min.x - IconGap * size;

        if (crest != null)
        {
            ShowIcon(ref _crestIcon, "CrestIcon", crest, left - size * 0.5f, size);
            left -= size * 1.1f;
        }
        else
        {
            HideIcon(_crestIcon);
        }

        if (ally != null)
        {
            ShowIcon(ref _allyIcon, "AllyCrestIcon", ally, left - size * 0.5f, size);
        }
        else
        {
            HideIcon(_allyIcon);
        }

        if (war != null)
        {
            ShowIcon(ref _warIcon, "WarIcon", war, _nameBounds.max.x + IconGap * size + size * 0.5f, size);
        }
        else
        {
            HideIcon(_warIcon);
        }
    }

    private static void LoadWarIcons()
    {
        if (_warSingle == null)
        {
            _warSingle = Resources.Load<Texture2D>("Data/UI/Assets/Clan/WarSingle");
            _warDouble = Resources.Load<Texture2D>("Data/UI/Assets/Clan/WarDouble");
        }
    }

    private void ShowIcon(ref NameIcon icon, string name, Texture2D texture, float x, float size)
    {
        if (icon == null)
        {
            icon = new NameIcon { Renderer = FindNameIcon(name) ?? CreateNameIcon(name), Mpb = new MaterialPropertyBlock() };
        }

        if (!icon.Renderer.gameObject.activeSelf)
        {
            icon.Renderer.gameObject.SetActive(true);
        }

        Transform t = icon.Renderer.transform;
        t.localScale = new Vector3(size, size, 1f);
        t.localPosition = new Vector3(x, _nameBounds.center.y, 0f);

        // Appele a chaque image : on ne reecrit le bloc que si l'image ou le fondu change.
        float alpha = Mathf.Max(0f, _currentAlpha);
        if (texture == icon.Shown && Mathf.Abs(alpha - icon.Alpha) < 0.004f)
        {
            return;
        }

        icon.Shown = texture;
        icon.Alpha = alpha;
        icon.Renderer.GetPropertyBlock(icon.Mpb);
        icon.Mpb.SetTexture(BaseMapId, texture);
        icon.Mpb.SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha));
        icon.Renderer.SetPropertyBlock(icon.Mpb);
    }

    private static void HideIcon(NameIcon icon)
    {
        if (icon != null && icon.Renderer.gameObject.activeSelf)
        {
            icon.Renderer.gameObject.SetActive(false);
            icon.Shown = null;
        }
    }

    // La plaque vient d'un pool : ses icones survivent au recyclage. Sans cette
    // reprise, un PNJ heritait du blason du joueur precedent.
    private MeshRenderer FindNameIcon(string name)
    {
        Transform existing = _nameText.transform.Find(name);
        return existing != null ? existing.GetComponent<MeshRenderer>() : null;
    }

    private MeshRenderer CreateNameIcon(string name)
    {
        if (_iconMaterial == null)
        {
            _iconMaterial = WorldBubbleVisual.CreateMaterial(_bubbleIcon.sharedMaterial, _nameText.fontSharedMaterial.renderQueue);
            _iconMaterial.name = "WorldNameIcon";
        }

        if (_iconMesh == null)
        {
            _iconMesh = BuildQuad();
        }

        GameObject go = new GameObject(name);
        go.transform.SetParent(_nameText.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = _iconMesh;

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _iconMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static Mesh BuildQuad()
    {
        Mesh mesh = new Mesh { name = "CrestQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    public void ManageColors()
    {
        UpdateStoreTitle();
        UpdateNameIcons();

        if (_chatBubble != null && _chatBubble.activeSelf)
        {
            TickChatBubble();
        }

        if (_previousServerTitleColor != Entity.Appearance.ServerTitleColor)
        {
            _previousServerTitleColor = Entity.Appearance.ServerTitleColor;
            // Meme bug que le nom (cf. plus bas) : toujours ecrire une
            // couleur ici, meme la couleur par defaut, sinon un titre recycle
            // depuis le pool garderait la couleur speciale du titre precedent.
            _titleText.color = _previousServerTitleColor != 0
                ? ColorUtils.IntegerToColor(_previousServerTitleColor)
                : Nameplate.DEFAULT_TITLE_COLOR;
        }

        if (Entity.Stats.Karma > 0)
        {
            if (Entity.Stats.Karma != _previousKarmaAmount)
            {
                _previousKarmaAmount = Entity.Stats.Karma;

                float lerpRatio = Mathf.Clamp(Entity.Stats.Karma / 300f, 0.25f, 1f);
                _nameText.color = Color.Lerp(Nameplate.DEFAULT_NAME_COLOR, Nameplate.FINAL_KARMA_COLOR, lerpRatio);
            }
        }
        else if (Entity.Identity.PvpFlag == 1)
        {
            _lastFlag = Entity.Identity.PvpFlag;
            _nameText.color = Nameplate.FLAG_COLOR;
        }
        else if (Entity.Identity.PvpFlag == 2)
        {
            _lastFlag = Entity.Identity.PvpFlag;
            if (Time.time - _lastBlinkTime > 0.5f)
            {
                _blink = !_blink;

                _nameText.color = _blink ? Nameplate.FLAG_COLOR : _previousServerNameColorValue;
                _lastBlinkTime = Time.time;
            }
        }
        else if (Entity.Identity.EntityType == EntityType.User && (Entity.Identity.IsMutualWar || Entity.Identity.IsOneSidedWar))
        {
            // Ennemi de guerre : rouge si la guerre est mutuelle, orange sinon.
            _lastFlag = 0;
            _previousKarmaAmount = 0;
            _previousServerNameColor = -1;
            _nameText.color = Entity.Identity.IsMutualWar ? MutualWarNameColor : OneSidedWarNameColor;
        }
        else if (PartyManager.Instance.IsMember(Entity) && Entity.Identity.Id != PlayerEntity.Instance.Identity.Id)
        {
            // Couleur des membres du groupe : priorite plus basse que
            // karma/PvP flag (un membre PK reste visible comme tel), mais
            // remplace la couleur serveur par defaut tant qu'aucun etat plus
            // prioritaire n'est actif. Pas de soi-meme (deja le joueur local).
            _lastFlag = 0;
            _previousKarmaAmount = 0;
            _previousServerNameColor = -1;
            _nameText.color = Nameplate.PARTY_MEMBER_COLOR;
        }
        else
        {
            if (_previousServerNameColor != Entity.Appearance.ServerNameColor || Entity.Stats.Karma != _previousKarmaAmount || Entity.Identity.PvpFlag != _lastFlag)
            {
                _lastFlag = 0;
                _previousKarmaAmount = 0;

                _previousServerNameColor = Entity.Appearance.ServerNameColor;
                // Toujours ecrire une couleur ici (meme la couleur par
                // defaut) : ce nameplate peut etre une instance recyclee du
                // pool qui affichait encore la couleur speciale (party/PK/
                // flag) de l'entite precedente. Sans ce else, ServerNameColor
                // == 0 (cas courant, aucune couleur serveur) ne touchait
                // jamais _nameText.color et la teinte residuelle restait
                // affichee jusqu'au prochain changement detecte.
                _previousServerNameColorValue = _previousServerNameColor != 0
                    ? ColorUtils.IntegerToColor(_previousServerNameColor)
                    : Nameplate.DEFAULT_NAME_COLOR;
                _nameText.color = _previousServerNameColorValue;
            }
        }
    }

    // Meme ordre de priorite que l'existant : l'appelant doit calculer
    // target/attack d'abord, puis appeler a nouveau avec Hover si survole -
    // le dernier appel gagne, reproduisant le fait que le survol s'applique
    // apres la cible dans NameplatesManagerGame.UpdateNameplateStyle.
    public void SetBubbleState(BubbleState state)
    {
        if (_currentBubbleState == state) return;
        _currentBubbleState = state;

        bool visible = state != BubbleState.None;
        _bubbleIcon.enabled = visible;

        Material material = state switch
        {
            BubbleState.Attack => _attackMaterial,
            BubbleState.Target => _targetMaterial,
            BubbleState.Hover => _hoverMaterial,
            _ => null
        };

        if (material != null)
        {
            _bubbleIcon.sharedMaterial = material;
        }
    }
}
