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
    }

    private OperateType _shownOperateType = (OperateType)255;
    private string _shownStoreMessage;
    private string _shownTitle;

    // Fond du nom de magasin : un quad noir dimensionne sur le texte, plus
    // precis que la balise <mark> de TMP. Cree a la demande, garde par le pool.
    private const string StoreBackgroundName = "StoreBackground";
    private const float StoreBackgroundAlpha = 0.9f;
    private static readonly Vector2 StoreBackgroundPadding = new Vector2(0.3f, 0.1f);
    private static Material _storeBackgroundMaterial;
    private MeshRenderer _storeBackground;
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
            bool isStore = color != null && !string.IsNullOrEmpty(message);

            _titleText.text = isStore ? $"<color={color}><noparse>{message}</noparse></color>" : title;
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
        t.localScale = new Vector3(bounds.size.x + StoreBackgroundPadding.x, bounds.size.y + StoreBackgroundPadding.y, 1f);
        _storeBackground.enabled = true;
        ApplyStoreBackgroundAlpha();
    }

    private MeshRenderer FindStoreBackground()
    {
        Transform existing = _titleText.transform.Find(StoreBackgroundName);
        return existing != null ? existing.GetComponent<MeshRenderer>() : null;
    }

    private MeshRenderer CreateStoreBackground()
    {
        if (_storeBackgroundMaterial == null)
        {
            // Copie du materiau de la bulle : deja transparent et visible en jeu.
            Material source = _bubbleIcon.sharedMaterial;
            _storeBackgroundMaterial = source != null
                ? new Material(source)
                : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _storeBackgroundMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
            _storeBackgroundMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
            _storeBackgroundMaterial.renderQueue = _titleText.fontSharedMaterial.renderQueue - 1;
        }

        GameObject go = new GameObject(StoreBackgroundName);
        go.transform.SetParent(_titleText.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

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
    public void ManageColors()
    {
        UpdateStoreTitle();

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
