using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// HUD de groupe (Couche C). Reutilise TEL QUEL le cadre/fond/glisser-
// deposer/redimensionnement de StatusWindow (memes classes CSS, meme
// sequence de cablage en C# dans BuildWindow ci-dessous - PartyWindow.uxml
// reference directement StatusWindow.uss) plutot que de les reimplementer
// a cote : demande explicite de l'utilisateur apres plusieurs tentatives
// d'adaptation qui n'avaient pas exactement le meme comportement (surtout
// le redimensionnement horizontal). Seul le contenu (liste de membres,
// generee en C#) est propre a cette fenetre. Toujours present, pas une
// fenetre qu'on ouvre/ferme (meme categorie que StatusWindow). Pas de
// bouton "Quitter le groupe" : /leave et l'icone d'action Party font deja
// ce travail. Le mode de butin est affiche en LECTURE SEULE : aucun
// paquet de changement de mode en cours de groupe n'existe cote serveur
// (seul RequestJoinParty porte un lootRuleId, utilise uniquement a la
// creation).
public class PartyWindow : L2Window
{
    private struct MemberRowRefs
    {
        public int ObjectId;
        public int ClassId;
        public VisualElement Block;
        public Label NameLabel;
        public VisualElement CpBar;
        public VisualElement CpBarBg;
        public VisualElement HpBar;
        public VisualElement HpBarBg;
        public VisualElement MpBar;
        public VisualElement MpBarBg;
        public int SnapshotCp;
        public int SnapshotMaxCp;
        public int SnapshotHp;
        public int SnapshotMaxHp;
        public int SnapshotMp;
        public int SnapshotMaxMp;
    }

    private VisualTreeAsset _barTemplate;

    [SerializeField] private float _partyWindowMinWidth = 175f;
    [SerializeField] private float _partyWindowMaxWidth = 400f;

    private VisualElement _membersContainer;
    private VisualElement _dragArea;
    private Label _lootRuleLabel;

    private readonly List<MemberRowRefs> _rows = new List<MemberRowRefs>();
    private float _lastVitalsUpdateTime;

    private static readonly string[] LootRuleLabels =
    {
        "Chacun pour soi",
        "Aléatoire",
        "Aléatoire incluant spoil",
        "Par ordre de tour",
        "Par ordre de tour incluant spoil",
    };

    private static PartyWindow _instance;
    public static PartyWindow Instance { get { return _instance; } }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        if (PartyManager.Instance != null)
        {
            PartyManager.Instance.OnPartyChanged -= RefreshMembers;
        }

        _classTooltip?.RemoveFromHierarchy();

        _instance = null;
    }

    protected override void LoadAssets()
    {
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/PartyWindow/PartyWindow");
        _barTemplate = LoadAsset("Data/UI/_Elements/Components/L2Bar/L2Bar");
    }

    protected override IEnumerator BuildWindow(VisualElement root)
    {
        InitWindow(root);

        yield return new WaitForEndOfFrame();

        // Position par defaut forcee en dur (la largeur/le cadre/le fond
        // viennent tels quels de StatusWindow.uss, cf. PartyWindow.uxml).
        _windowEle.style.position = Position.Absolute;
        _windowEle.style.left = 8;
        _windowEle.style.top = 92;

        // Mecanique identique a StatusWindow.cs (meme sequence, memes cibles) :
        // glisser-deposer sur la zone de classe "drag-area" (le cadre
        // Frame_DF_Ver), redimensionnement horizontal sur le wrapper
        // "SizeControl", recalcul immediat au changement de geometrie
        // (ici les jauges, via UpdateVitals, plutot que juste les valeurs).
        _dragArea = GetElementByClass("drag-area");
        DragManipulator drag = new DragManipulator(_dragArea, _windowEle, this);
        _dragArea.AddManipulator(drag);

        VisualElement horizontalResizeHandle = GetElementById("SizeControl");
        HorizontalResizeManipulator horizontalResize = new HorizontalResizeManipulator(
            horizontalResizeHandle, _windowEle, _partyWindowMinWidth, _partyWindowMaxWidth);
        horizontalResizeHandle.AddManipulator(horizontalResize);

        _windowEle.RegisterCallback<GeometryChangedEvent>(evt => UpdateVitals());

        _membersContainer = GetElementById("MembersContainer");
        _membersContainer.style.flexDirection = FlexDirection.Column;

        _lootRuleLabel = (Label)GetElementById("LootRuleLabel");
        _lootRuleLabel.style.fontSize = 8;

        // RefreshMembers() decide elle-meme si le HUD doit etre visible
        // (montre si deja en groupe au chargement, ex. reconnexion - cache
        // sinon).
        PartyManager.Instance.OnPartyChanged += RefreshMembers;
        RefreshMembers();

        L2GameUI.Instance.WindowLoadComplete();
    }

    private void FixedUpdate()
    {
        if (_membersContainer == null || _isWindowHidden) return;

        if (Time.time - _lastVitalsUpdateTime < 0.5f) return;
        _lastVitalsUpdateTime = Time.time;

        UpdateVitals();
    }

    private void RefreshMembers()
    {
        if (_membersContainer == null) return;

        if (!PartyManager.Instance.IsInParty)
        {
            HideWindow(true);
            return;
        }

        if (_isWindowHidden)
        {
            ShowWindow();
        }

        _lootRuleLabel.text = "Butin : " + LootRuleLabels[Mathf.Clamp((int)PartyManager.Instance.LootRule, 0, LootRuleLabels.Length - 1)];

        _membersContainer.Clear();
        _rows.Clear();

        // Le joueur local n'est PAS liste ici : ses propres CP/HP/MP sont
        // deja visibles dans StatusWindow, inutile de les dupliquer (retire
        // a la demande de l'utilisateur).
        foreach (PartyMemberInfo member in PartyManager.Instance.OtherMembers.Values)
        {
            AddMemberBlock(member.ObjectId, member.Name, member.Level, member.ClassId, member.Cp, member.MaxCp, member.Hp, member.MaxHp, member.Mp, member.MaxMp);
        }

        UpdateVitals();
    }

    // objectId etc. designent toujours un AUTRE membre : le joueur local
    // n'est jamais ajoute (cf. RefreshMembers).
    private void AddMemberBlock(int objectId, string name, int level, int classId, int cp, int maxCp, int hp, int maxHp, int mp, int maxMp)
    {
        bool isLeader = PartyManager.Instance.LeaderObjectId == objectId;

        VisualElement block = new VisualElement();
        block.AddToClassList("party-member-block");
        block.style.flexDirection = FlexDirection.Column;

        VisualElement header = new VisualElement();
        header.AddToClassList("party-member-header");
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;

        Label nameLabel = new Label((isLeader ? "★ " : "") + name);
        nameLabel.AddToClassList("party-member-name");
        nameLabel.AddToClassList(isLeader ? "l2-color-2" : "l2-color-4");
        nameLabel.style.fontSize = 11;
        nameLabel.style.overflow = Overflow.Hidden;
        nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
        header.Add(nameLabel);

        bool canKick = PartyManager.Instance.IsLeader;
        if (canKick)
        {
            Button kickButton = new Button();
            kickButton.AddToClassList("party-member-kick-btn");
            kickButton.style.marginLeft = new StyleLength(StyleKeyword.Auto);
            // Meme icone que sur la barre d'actions ("Exclure", action013)
            // plutot qu'un "X" texte, en miniature.
            Texture2D kickIcon = IconTable.Instance.LoadTextureByName("action013");
            if (kickIcon != null) kickButton.style.backgroundImage = kickIcon;
            kickButton.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            kickButton.style.width = 14;
            kickButton.style.height = 14;
            kickButton.RegisterCallback<ClickEvent>(evt =>
            {
                GameClient.Instance.ClientPacketHandler.SendRequestOustPartyMember(name);
                evt.StopPropagation();
            });
            header.Add(kickButton);
        }

        block.Add(header);

        VisualElement cpBarInstance = BuildMemberBar("CP");
        block.Add(cpBarInstance);

        VisualElement hpBarInstance = BuildMemberBar("HP");
        block.Add(hpBarInstance);

        VisualElement mpBarInstance = BuildMemberBar("MP");
        block.Add(mpBarInstance);

        block.RegisterCallback<PointerEnterEvent>(evt => ShowClassTooltip(nameLabel, classId, level));
        block.RegisterCallback<PointerLeaveEvent>(evt => HideClassTooltip());

        // Cible le membre en cliquant son bloc (comme le client L2
        // d'origine) - seulement possible s'il est a portee de rendu (une
        // Entity client existe), rien a faire sinon.
        block.RegisterCallback<ClickEvent>(evt =>
        {
            if (WorldSpawner.Instance.TryGetEntity(objectId, out Entity entity))
            {
                TargetManager.Instance.SetTarget(new ObjectData(entity.gameObject));
            }
        });

        _membersContainer.Add(block);

        _rows.Add(new MemberRowRefs
        {
            ObjectId = objectId,
            ClassId = classId,
            Block = block,
            NameLabel = nameLabel,
            CpBar = cpBarInstance.Q<VisualElement>("Bar"),
            CpBarBg = cpBarInstance.Q<VisualElement>("BarBg"),
            HpBar = hpBarInstance.Q<VisualElement>("Bar"),
            HpBarBg = hpBarInstance.Q<VisualElement>("BarBg"),
            MpBar = mpBarInstance.Q<VisualElement>("Bar"),
            MpBarBg = mpBarInstance.Q<VisualElement>("BarBg"),
            SnapshotCp = cp,
            SnapshotMaxCp = Mathf.Max(1, maxCp),
            SnapshotHp = hp,
            SnapshotMaxHp = Mathf.Max(1, maxHp),
            SnapshotMp = mp,
            SnapshotMaxMp = Mathf.Max(1, maxMp),
        });
    }

    // Instancie une jauge L2Bar avec le meme gabarit que StatusWindow. Ni
    // texte valeur/max ni lettre CP/HP/MP en filigrane (retires a la
    // demande de l'utilisateur - juste la jauge). La couleur (jaune=cp/
    // rouge=hp/bleu=mp) n'est PAS confiee aux classes CSS "cp"/"mp" de
    // L2Bar.uss (censees suffire, mais les 3 jauges rendaient toutes en
    // rouge en jeu malgre des classes correctement posees - cause exacte
    // non identifiee) : les 6 images (Bar+BarBg x Left/Center/Right) sont
    // affectees directement ici depuis Data/UI/Assets/Status/Gauge/
    // Gauge_DF_Large_{CP,HP,MP}, garanti correct quel que soit ce mystere.
    private VisualElement BuildMemberBar(string gaugeType)
    {
        VisualElement instance = _barTemplate.CloneTree();

        Label nameLabel = instance.Q<Label>("Label");
        if (nameLabel != null) nameLabel.style.display = DisplayStyle.None;

        Label statusLabel = instance.Q<Label>("StatusLabel");
        if (statusLabel != null) statusLabel.style.display = DisplayStyle.None;

        Label innerText = instance.Q<Label>("Text");
        if (innerText != null) innerText.style.display = DisplayStyle.None;

        VisualElement bar = instance.Q<VisualElement>("Bar");
        VisualElement barBg = instance.Q<VisualElement>("BarBg");

        ApplyGaugeTextures(bar, gaugeType, false);
        ApplyGaugeTextures(barBg, gaugeType, true);

        foreach (VisualElement ve in new[] { bar, barBg })
        {
            if (ve == null) continue;
            ve.style.minHeight = 9;
            ve.style.maxHeight = 9;
        }

        // Pas de marge entre les jauges : collees les unes aux autres
        // (demande explicite, il y avait un espace vertical visible entre
        // CP/HP/MP).
        instance.style.minHeight = 9;
        instance.style.maxHeight = 9;
        instance.style.marginBottom = 0;

        return instance;
    }

    private static readonly Dictionary<string, Texture2D> _gaugeTextureCache = new Dictionary<string, Texture2D>();

    private void ApplyGaugeTextures(VisualElement container, string gaugeType, bool background)
    {
        if (container == null) return;

        string folder = "Gauge_DF_Large_" + gaugeType;
        string suffix = background ? "_bg_" : "_";

        SetGaugeImage(container.Q<VisualElement>("BGLeft"), folder, suffix + "Left");
        SetGaugeImage(container.Q<VisualElement>("BGCenter"), folder, suffix + "Center");
        SetGaugeImage(container.Q<VisualElement>("BGRight"), folder, suffix + "Right");
    }

    private void SetGaugeImage(VisualElement element, string folder, string suffix)
    {
        if (element == null) return;

        string key = folder + suffix;
        if (!_gaugeTextureCache.TryGetValue(key, out Texture2D texture))
        {
            string path = $"Data/UI/Assets/Status/Gauge/{folder}/{folder}{suffix}";
            texture = Resources.Load<Texture2D>(path);
            // Meme repli que ps_levelback (import "Sprite", pas toujours
            // chargeable directement en Texture2D) - trouve en reponse au
            // retour "CP correct mais HP/MP vides".
            if (texture == null)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite != null) texture = sprite.texture;
            }

            if (texture == null)
            {
                Debug.LogWarning($"[PartyWindow] Gauge texture introuvable : {path}");
            }

            _gaugeTextureCache[key] = texture;
        }

        if (texture != null)
        {
            element.style.backgroundImage = texture;
        }
    }

    // Info-bulle de classe : element flottant ajoute directement a la
    // racine de l'UI (comme WorldItemTooltip.cs), PAS un enfant de cette
    // fenetre - un essai precedent en enfant de _windowEle (position
    // absolue) faisait pourtant decaler toute la fenetre au survol
    // (mecanisme exact non identifie, un enfant absolu qui deborde a
    // visiblement une influence sur la mesure de layout du parent dans ce
    // cas). En dehors de l'arbre de la fenetre, aucun risque que ça
    // revienne. Style pose en dur en C# (pas de classe USS - cet element
    // n'est plus un descendant de PartyWindow.uxml, sa feuille de style ne
    // s'appliquerait pas ici).
    private Label _classTooltip;

    private void EnsureClassTooltip()
    {
        if (_classTooltip != null) return;
        if (L2GameUI.Instance == null || L2GameUI.Instance.RootElement == null) return;

        _classTooltip = new Label();
        _classTooltip.style.position = Position.Absolute;
        _classTooltip.style.display = DisplayStyle.None;
        _classTooltip.style.color = Color.white;
        _classTooltip.style.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
        _classTooltip.style.borderTopWidth = 1;
        _classTooltip.style.borderBottomWidth = 1;
        _classTooltip.style.borderLeftWidth = 1;
        _classTooltip.style.borderRightWidth = 1;
        _classTooltip.style.borderTopColor = new Color(0.259f, 0.231f, 0.161f);
        _classTooltip.style.borderBottomColor = new Color(0.259f, 0.231f, 0.161f);
        _classTooltip.style.borderLeftColor = new Color(0.259f, 0.231f, 0.161f);
        _classTooltip.style.borderRightColor = new Color(0.259f, 0.231f, 0.161f);
        _classTooltip.style.paddingLeft = 5;
        _classTooltip.style.paddingRight = 5;
        _classTooltip.style.paddingTop = 2;
        _classTooltip.style.paddingBottom = 2;
        _classTooltip.style.fontSize = 10;
        _classTooltip.style.whiteSpace = WhiteSpace.NoWrap;
        _classTooltip.pickingMode = PickingMode.Ignore;
        L2GameUI.Instance.RootElement.Add(_classTooltip);
    }

    private void ShowClassTooltip(VisualElement nameLabel, int classId, int level)
    {
        EnsureClassTooltip();
        if (_classTooltip == null) return;

        _classTooltip.text = $"{(CharacterClass)classId} - Lv {level}";

        Vector2 localPos = L2GameUI.Instance.RootElement.WorldToLocal(nameLabel.worldBound.position);
        _classTooltip.style.left = localPos.x + nameLabel.resolvedStyle.width + 6;
        _classTooltip.style.top = localPos.y;
        _classTooltip.style.display = DisplayStyle.Flex;
    }

    private void HideClassTooltip()
    {
        if (_classTooltip != null) _classTooltip.style.display = DisplayStyle.None;
    }

    // Source des jauges : PartyManager.OtherMembers, tenu a jour en temps
    // reel par PartySmallWindowUpdate (paquet serveur dedie envoye a chaque
    // changement de CP/HP/MP/niveau d'un membre, cf.
    // PlayerStatus.java#broadcastStatusUpdate). C'est la SEULE source fiable
    // quelle que soit la portee de rendu : un membre hors champ n'a pas
    // d'Entity locale, donc s'appuyer sur WorldSpawner/Entity.Status (essaye
    // avant la decouverte de ce paquet, cf. historique) ne se mettait a jour
    // que si le membre etait physiquement visible - d'ou les jauges figees
    // constatees en jeu. Les champs SnapshotXxx de MemberRowRefs ne servent
    // plus qu'a l'affichage initial (AddMemberBlock).
    private void UpdateVitals()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            MemberRowRefs row = _rows[i];

            int cp = row.SnapshotCp, maxCp = row.SnapshotMaxCp;
            int hp = row.SnapshotHp, maxHp = row.SnapshotMaxHp;
            int mp = row.SnapshotMp, maxMp = row.SnapshotMaxMp;

            if (PartyManager.Instance.OtherMembers.TryGetValue(row.ObjectId, out PartyMemberInfo member))
            {
                cp = member.Cp;
                maxCp = Mathf.Max(1, member.MaxCp);
                hp = member.Hp;
                maxHp = Mathf.Max(1, member.MaxHp);
                mp = member.Mp;
                maxMp = Mathf.Max(1, member.MaxMp);
            }

            SetBarRatio(row.CpBar, row.CpBarBg, cp, maxCp);
            SetBarRatio(row.HpBar, row.HpBarBg, hp, maxHp);
            SetBarRatio(row.MpBar, row.MpBarBg, mp, maxMp);
        }
    }

    private void SetBarRatio(VisualElement bar, VisualElement barBg, int current, int max)
    {
        if (bar == null || barBg == null) return;

        float ratio = Mathf.Clamp01((float)current / max);
        float bgWidth = barBg.resolvedStyle.width;
        bar.style.width = bgWidth * ratio;
    }

    public override void ShowWindow()
    {
        base.ShowWindow();
        AudioManager.Instance.PlayUISound("window_open");
    }

    public override void HideWindow(bool silent)
    {
        base.HideWindow(silent);

        if (!silent)
            AudioManager.Instance.PlayUISound("window_close");
    }
}
