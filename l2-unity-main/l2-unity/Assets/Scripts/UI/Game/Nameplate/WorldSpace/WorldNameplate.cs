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
    }

    // Port quasi verbatim de Nameplate.ManageColors() - meme ordre de
    // priorite (karma > PvP flag fixe > PvP flag clignotant > couleur
    // serveur par defaut) et memes champs de detection de changement pour
    // eviter de reecrire la couleur a chaque frame.
    public void ManageColors()
    {
        if (_previousServerTitleColor != Entity.Appearance.ServerTitleColor)
        {
            _previousServerTitleColor = Entity.Appearance.ServerTitleColor;

            if (_previousServerTitleColor != 0)
            {
                _titleText.color = ColorUtils.IntegerToColor(_previousServerTitleColor);
            }
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
        else
        {
            if (_previousServerNameColor != Entity.Appearance.ServerNameColor || Entity.Stats.Karma != _previousKarmaAmount || Entity.Identity.PvpFlag != _lastFlag)
            {
                _lastFlag = 0;
                _previousKarmaAmount = 0;

                _previousServerNameColor = Entity.Appearance.ServerNameColor;

                if (_previousServerNameColor != 0)
                {
                    _previousServerNameColorValue = ColorUtils.IntegerToColor(_previousServerNameColor);
                    _nameText.color = _previousServerNameColorValue;
                }
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
