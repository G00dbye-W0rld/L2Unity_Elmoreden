using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class TargetWindow : L2PopupWindow
{
    private Label _nameLabel;
    private VisualElement _HPBarContainer;
    private VisualElement _HPBar;
    private VisualElement _HPBarBG;
    private Button _partyInviteButton;
    [SerializeField] private float _targetWindowMinWidth = 175.0f;
    [SerializeField] private float _targetWindowMaxWidth = 300.0f;

    private static TargetWindow _instance;
    public static TargetWindow Instance { get { return _instance; } }

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
        _instance = null;
    }

    protected override void LoadAssets()
    {
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/TargetWindow/TargetWindow");
    }

    protected override IEnumerator BuildWindow(VisualElement root)
    {
        InitWindow(root);

        yield return new WaitForEndOfFrame();

        var statusWindowDragArea = GetElementByClass("drag-area");
        DragManipulator drag = new DragManipulator(statusWindowDragArea, _windowEle, this);
        statusWindowDragArea.AddManipulator(drag);

        var horizontalResizeHandle = GetElementById("SizeControl");
        HorizontalResizeManipulator horizontalResize = new HorizontalResizeManipulator(
            horizontalResizeHandle, _windowEle, _targetWindowMinWidth, _targetWindowMaxWidth);
        horizontalResizeHandle.AddManipulator(horizontalResize);

        var closeBtnHandle = (Button)GetElementById("CloseBtn");
        closeBtnHandle.AddManipulator(new ButtonClickSoundManipulator(closeBtnHandle));
        closeBtnHandle.RegisterCallback<MouseUpEvent>(evt =>
        {
            TargetManager.Instance.ClearTarget();
        });

        horizontalResizeHandle.AddManipulator(horizontalResize);

        _nameLabel = (Label)GetElementById("TargetName");
        if (_nameLabel == null)
        {
            Debug.LogError("Target window target name label is null.");
        }

        _HPBarContainer = GetElementById("HPBar");

        if (_HPBarContainer == null)
        {
            Debug.LogError("Target window _HPBarContainer is null");
        }

        _HPBar = _HPBarContainer.Q<VisualElement>("Bar");
        if (_HPBar == null)
        {
            Debug.LogError("Target window HPBar is null");
        }

        _HPBarBG = _HPBarContainer.Q<VisualElement>("BarBg");
        if (_HPBarBG == null)
        {
            Debug.LogError("Target window _HPBarBG is null");
        }

        _partyInviteButton = (Button)GetElementById("PartyInviteButton");
        _partyInviteButton.style.backgroundImage = IconTable.Instance.LoadTextureByName("action011");
        _partyInviteButton.AddManipulator(new ButtonClickSoundManipulator(_partyInviteButton));
        _partyInviteButton.RegisterCallback<ClickEvent>(evt =>
        {
            Entity target = TargetManager.Instance.Target;
            if (target == null) return;

            int lootRuleId = PartyManager.Instance.IsInParty ? (int)PartyManager.Instance.LootRule : GameSettings.PreferredPartyLootRule;
            GameClient.Instance.ClientPacketHandler.SendRequestJoinParty(target.Identity.Name, lootRuleId);
        });

        _windowEle.style.position = Position.Absolute;
        _windowEle.style.left = Screen.width / 2f - _windowEle.resolvedStyle.width / 2f;
        _windowEle.style.top = 0;

        L2GameUI.Instance.WindowLoadComplete();
    }

    private void FixedUpdate()
    {
        if (_windowEle == null)
        {
            return;
        }

        if (TargetManager.Instance.HasTarget())
        {
            if (_isWindowHidden)
            {
                ShowWindow();
            }

            Entity targetData = TargetManager.Instance.Target;
            _nameLabel.text = targetData.Identity.Name;

            // "+" d'invitation : uniquement pour un AUTRE joueur, et pas s'il
            // est deja dans le groupe actuel (rien a inviter dans ce cas).
            // BUG CORRIGE : ce code ne testait QUE EntityType.Player, or dans
            // cette architecture EntityType.Player designe EXCLUSIVEMENT le
            // personnage LOCAL (tag pose uniquement par PlayerSpawner.cs, qui
            // ne sert qu'a se spawner soi-meme - PlayerEntity.Instance est
            // d'ailleurs le raccourci partout dans le code pour "l'entite
            // dont l'Id == CurrentPlayerId", cf. WorldSpawner.GetEntityAsync/
            // ExecuteWithEntityAsync). Un AUTRE joueur reel recoit toujours
            // EntityType.User (tag pose par UserSpawner.cs, qui active
            // NetworkTransformReceive - donc bien un personnage pilote a
            // distance). Consequence concrete : l'icone d'invitation ne
            // pouvait quasiment jamais s'afficher pour un vrai joueur cible,
            // exactement le symptome "icone d'invitation pas toujours
            // affichee" remonte. PartyManager.IsMember(Entity) accepte deja
            // les deux valeurs pour cette raison - meme logique ici.
            // IsMember(Entity) (pas IsMember(int)) par coherence avec le
            // reste du code : verifie l'EntityType en plus de l'Id, cf.
            // PartyManager.IsMember pour le risque de collision d'ObjectId.
            bool canInvite = (targetData.Identity.EntityType == EntityType.Player || targetData.Identity.EntityType == EntityType.User) &&
                targetData != PlayerEntity.Instance &&
                !PartyManager.Instance.IsMember(targetData);
            _partyInviteButton.EnableInClassList("hidden", !canInvite);

            if (targetData.Identity.IsHpShowable)
            {
                SetTargetColor(targetData.Stats.Level - PlayerEntity.Instance.Stats.Level);

                if (!_HPBarContainer.ClassListContains("visible"))
                {
                    _HPBarContainer.AddToClassList("visible");
                }

                if (_HPBarBG != null && _HPBar != null)
                {
                    float hpRatio = (float)targetData.Status.Hp / targetData.Stats.MaxHp;
                    float bgWidth = _HPBarBG.resolvedStyle.width;
                    float barWidth = bgWidth * hpRatio;
                    _HPBar.style.width = barWidth;
                }
            }
            else
            {
                SetTargetColor(0);
                if (_HPBarContainer.ClassListContains("visible"))
                {
                    _HPBarContainer.RemoveFromClassList("visible");
                }
            }
        }
        else if (!_isWindowHidden)
        {
            HideWindow(false);
        }
    }

    public override void ShowWindow()
    {
        base.ShowWindow();
        L2GameUI.Instance.WindowOpened(this);
    }

    public override void HideWindow(bool silent)
    {
        base.HideWindow(silent);

        TargetManager.Instance.ClearTarget();

        if (!silent)
            AudioManager.Instance.PlayUISound("window_close");

        L2GameUI.Instance.WindowClosed(this);
    }

    public void SetTargetColor(int color)
    {
        if (color > -3 && color < 3)
        {
            _nameLabel.style.color = Color.white;
        }
        else if (color <= -3 && color > -6)
        {
            _nameLabel.style.color = new Color(0.6039f, 0.9490f, 0.6392f);
        }
        else if (color <= -6 && color > -9)
        {
            _nameLabel.style.color = new Color(0.4901f, 0.466f, 1f);
        }
        else if (color < -9)
        {
            _nameLabel.style.color = new Color(0, 0, 0.945f);
        }
        else if (color >= 3 && color < 6)
        {
            _nameLabel.style.color = new Color(0.9294f, 0.9412f, 0.5412f);
        }
        else if (color >= 6 && color < 9)
        {
            _nameLabel.style.color = new Color(0.8588f, 0.4980f, 0.4980f);
        }
        else if (color > 9)
        {
            _nameLabel.style.color = new Color(0.945f, 0, 0);
        }
    }
}
