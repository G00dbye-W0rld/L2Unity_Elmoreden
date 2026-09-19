using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

// Crochetage d'une porte de salle de clan : le serveur tire le de, la fenetre
// l'anime puis affiche le resultat. Construite comme la fiche de clan.
public class LockpickWindow : L2PopupWindow
{
    private const int StateSuccess = 2;
    private const int ResultPin = 1;
    private const int ResultFail = 2;
    private const int ResultAlarm = 3;
    private const float RollDelay = 2f;

    private Label _hallName;
    private VisualElement _pins;
    private Label _die;
    private Label _rollDetail;
    private Label _result;
    private Label _difficulty;
    private Label _bonus;
    private Label _lockpicks;
    private Button _rollButton;

    private int _doorObjectId;
    private int _lockpickCount;
    private float _nextRoll;
    private Coroutine _animation;

    private static LockpickWindow _instance;
    public static LockpickWindow Instance { get { return _instance; } }

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
        if (_instance == this)
        {
            _instance = null;
        }
    }

    protected override void LoadAssets()
    {
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/LockpickWindow/LockpickWindow");
    }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);

        VisualElement dragArea = GetElementByClass("drag-area");
        dragArea.AddManipulator(new DragManipulator(dragArea, _windowEle, this));

        RegisterCloseWindowEvent("btn-close-frame");
        RegisterClickWindowEvent(_windowEle, dragArea);

        MoveContentIntoFrame();

        VisualElement page = GetElementById("LockpickContent");
        _hallName = page.Q<Label>("HallName");
        _pins = page.Q<VisualElement>("Pins");
        _die = page.Q<Label>("Die");
        _rollDetail = page.Q<Label>("RollDetail");
        _result = page.Q<Label>("Result");
        _difficulty = page.Q<Label>("Difficulty");
        _bonus = page.Q<Label>("Bonus");
        _lockpicks = page.Q<Label>("Lockpicks");

        _rollButton = page.Q<Button>("RollBtn");
        _rollButton.AddManipulator(new ButtonClickSoundManipulator(_rollButton));
        _rollButton.RegisterCallback<MouseUpEvent>(evt => Roll(), TrickleDown.TrickleDown);

        Button close = page.Q<Button>("CloseBtn");
        close.AddManipulator(new ButtonClickSoundManipulator(close));
        close.RegisterCallback<MouseUpEvent>(evt => HideWindow(false), TrickleDown.TrickleDown);
    }

    // Q() teste aussi l'element de depart : on prend l'enfant direct "Content".
    private void MoveContentIntoFrame()
    {
        VisualElement content = GetElementById("LockpickContent");
        VisualElement outer = GetElementById("Content");
        VisualElement slot = null;

        if (outer != null)
        {
            foreach (VisualElement child in outer.Children())
            {
                if (child.name == "Content")
                {
                    slot = child;
                    break;
                }
            }
        }

        if (content == null || slot == null || content.parent == slot)
        {
            return;
        }

        content.RemoveFromHierarchy();
        slot.Add(content);
    }

    protected override IEnumerator BuildWindow(VisualElement root)
    {
        InitWindow(root);

        yield return new WaitForEndOfFrame();

        CenterWindow();
        HideWindow(true);
    }

    public void OnState(ExLockpickPacket state)
    {
        bool first = _isWindowHidden || state.DoorObjectId != _doorObjectId;
        _doorObjectId = state.DoorObjectId;
        _lockpickCount = state.Lockpicks;

        if (first)
        {
            ShowWindow();
            BringToFront();
            _die.text = "d20";
            _rollDetail.text = string.Empty;
            _result.text = string.Empty;
        }

        _hallName.text = state.HallName;
        _difficulty.text = state.Difficulty.ToString();
        _bonus.text = "+" + state.Bonus;
        _lockpicks.text = state.Lockpicks.ToString();

        if (state.Die > 0)
        {
            if (_animation != null)
            {
                StopCoroutine(_animation);
            }
            _animation = StartCoroutine(Animate(state));
        }
        else
        {
            ShowPins(state.Pins, state.PinsTotal);
            RefreshButton();
        }
    }

    private IEnumerator Animate(ExLockpickPacket state)
    {
        _result.text = string.Empty;
        _rollDetail.text = string.Empty;

        for (float t = 0f; t < 0.9f; t += 0.07f)
        {
            _die.text = Random.Range(1, 21).ToString();
            yield return new WaitForSeconds(0.07f);
        }

        _die.text = state.Die.ToString();
        _rollDetail.text = $"{state.Die} + {state.Bonus} = {state.Die + state.Bonus}   contre {state.Difficulty}";
        ShowPins(state.Pins, state.PinsTotal);

        switch (state.Result)
        {
            case ResultPin:
                SetResult(state.State == StateSuccess ? "La serrure cède ! La porte s'ouvre." : "Une goupille cède.", new Color(0.55f, 0.9f, 0.55f));
                break;
            case ResultFail:
                SetResult("Le crochet se brise.", new Color(0.95f, 0.75f, 0.4f));
                break;
            case ResultAlarm:
                SetResult("Le crochet se brise dans un grincement : le clan est alerté !", new Color(1f, 0.45f, 0.4f));
                break;
        }

        // Le serveur refuse un jet avant deux secondes : on reactive le bouton a ce moment.
        yield return new WaitForSeconds(Mathf.Max(0f, _nextRoll - Time.time));
        _animation = null;
        RefreshButton();

        if (state.State == StateSuccess)
        {
            yield return new WaitForSeconds(2f);
            HideWindow(false);
        }
    }

    private void SetResult(string text, Color color)
    {
        _result.text = text;
        _result.style.color = color;
    }

    private void ShowPins(int done, int total)
    {
        _pins.Clear();
        for (int i = 0; i < total; i++)
        {
            VisualElement pin = new VisualElement();
            pin.style.width = pin.style.height = 18;
            pin.style.marginLeft = pin.style.marginRight = 6;
            pin.style.borderTopLeftRadius = pin.style.borderTopRightRadius = 9;
            pin.style.borderBottomLeftRadius = pin.style.borderBottomRightRadius = 9;
            pin.style.borderLeftWidth = pin.style.borderRightWidth = 2;
            pin.style.borderTopWidth = pin.style.borderBottomWidth = 2;
            Color border = new Color(0.59f, 0.49f, 0.27f);
            pin.style.borderLeftColor = pin.style.borderRightColor = border;
            pin.style.borderTopColor = pin.style.borderBottomColor = border;
            pin.style.backgroundColor = i < done ? new Color(1f, 0.84f, 0.5f) : new Color(0f, 0f, 0f, 0.5f);
            _pins.Add(pin);
        }
    }

    private void RefreshButton()
    {
        _rollButton.SetEnabled(_animation == null && _lockpickCount > 0 && Time.time >= _nextRoll);
        if (_lockpickCount <= 0 && string.IsNullOrEmpty(_result.text))
        {
            SetResult("Plus aucun crochet.", new Color(0.95f, 0.75f, 0.4f));
        }
    }

    private void Roll()
    {
        if (!_rollButton.enabledSelf || Time.time < _nextRoll)
        {
            return;
        }

        _nextRoll = Time.time + RollDelay;
        _rollButton.SetEnabled(false);
        GameClient.Instance.ClientPacketHandler.SendRequestLockpick(_doorObjectId, 1);
    }

    public override void HideWindow(bool silent)
    {
        if (!_isWindowHidden && _doorObjectId != 0 && GameClient.Instance != null)
        {
            GameClient.Instance.ClientPacketHandler.SendRequestLockpick(_doorObjectId, 0);
        }
        _doorObjectId = 0;
        base.HideWindow(silent);
    }
}
