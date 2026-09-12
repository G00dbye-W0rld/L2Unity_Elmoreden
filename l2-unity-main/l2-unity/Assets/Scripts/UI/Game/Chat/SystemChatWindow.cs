using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

// Fenetre des messages systeme, posee juste au-dessus du chat. Sa largeur et
// sa position suivent celles du chat ; seule sa hauteur se regle, par sa
// bordure du haut. Activable depuis les options du chat.
public class SystemChatWindow : L2Window
{
    private const int MaximumMessageCount = 60;
    private const float MinHeight = 60f;
    private const float MaxHeight = 320f;
    private const float DefaultHeight = 110f;
    private const float GapAboveChat = 4f;

    private Label _content;
    private int _messageCount;

    private static SystemChatWindow _instance;
    public static SystemChatWindow Instance { get { return _instance; } }

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
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/ChatWindow/SystemChatWindow");
    }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);

        _content = GetLabelById("Content");
        _content.text = "";

        _windowEle.style.height = DefaultHeight;

        VisualElement topEdge = GetElementById("SystemResizeTop");
        topEdge.AddManipulator(new EdgeResizeManipulator(topEdge, _windowEle, EdgeResizeManipulator.Edge.Top, MinHeight, MaxHeight, 0f, 0f));
    }

    protected override IEnumerator BuildWindow(VisualElement root)
    {
        InitWindow(root);

        yield return new WaitForEndOfFrame();

        SetEnabled(GameSettings.ChatSystemWindow);
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            ShowWindow();
        }
        else
        {
            HideWindow(true);
        }
    }

    public void AddMessage(string message)
    {
        if (_content == null)
        {
            return;
        }

        if (_content.text.Length > 0)
        {
            _content.text += "\r\n";
        }

        _content.text += message;

        if (_messageCount++ >= MaximumMessageCount)
        {
            int firstLineBreak = _content.text.IndexOf("\r\n");
            if (firstLineBreak >= 0)
            {
                _content.text = _content.text[(firstLineBreak + 2)..];
            }

            _messageCount = MaximumMessageCount;
        }
    }

    private void LateUpdate()
    {
        if (_isWindowHidden || _windowEle == null || ChatWindow.Instance == null)
        {
            return;
        }

        VisualElement chat = ChatWindow.Instance.WindowElement;
        if (chat == null)
        {
            return;
        }

        // Le chat est place par le flux de son conteneur : on lit donc ses
        // coordonnees ecran reelles plutot que ses styles left/top.
        Rect bound = chat.worldBound;
        if (bound.width <= 0f)
        {
            return;
        }

        _windowEle.style.position = Position.Absolute;
        _windowEle.style.width = bound.width;
        _windowEle.style.left = bound.xMin;
        _windowEle.style.top = bound.yMin - _windowEle.resolvedStyle.height - GapAboveChat;
    }
}
