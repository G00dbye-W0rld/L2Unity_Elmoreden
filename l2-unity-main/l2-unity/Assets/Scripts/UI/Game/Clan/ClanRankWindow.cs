using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

// Privileges des rangs, ouverte depuis la fenetre de clan. Construite comme
// les options du chat, dont le montage est eprouve :
// contenu glisse dans l'emplacement du cadre, fenetre centree.
public class ClanRankWindow : L2PopupWindow
{
    private ClanRankPanel _panel;

    private static ClanRankWindow _instance;
    public static ClanRankWindow Instance { get { return _instance; } }

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

        ClanRanks.Changed -= Refresh;
    }

    protected override void LoadAssets()
    {
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/ClanWindow/ClanRankWindow");
    }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);

        VisualElement dragArea = GetElementByClass("drag-area");
        dragArea.AddManipulator(new DragManipulator(dragArea, _windowEle, this));

        RegisterCloseWindowEvent("btn-close-frame");
        RegisterClickWindowEvent(_windowEle, dragArea);

        MoveContentIntoFrame();

        _panel = new ClanRankPanel(GetElementById("RankContent"));
        ClanRanks.Changed += Refresh;
    }

    // Q() teste aussi l'element de depart : il renvoyait le conteneur externe au
    // lieu de l'emplacement, et laissait un cadre vide en haut de la fenetre.
    private static VisualElement InnerSlot(VisualElement outer)
    {
        if (outer == null)
        {
            return null;
        }

        foreach (VisualElement child in outer.Children())
        {
            if (child.name == "Content")
            {
                return child;
            }
        }

        return null;
    }

    // Meme deplacement que les options du chat : le contenu vit DANS le cadre.
    private void MoveContentIntoFrame()
    {
        VisualElement content = GetElementById("RankContent");
        VisualElement outer = GetElementById("Content");
        VisualElement slot = InnerSlot(outer);

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

        // Pas de WindowLoadComplete : fenetre ajoutee par code dans L2GameUI.
    }

    /// Le bouton qui l'ouvre ramene la fenetre de clan au premier plan des
    /// l'appui : on repasse devant a l'ouverture, sinon on s'ouvre dessous.
    public void Open()
    {
        ShowWindow();
        BringToFront();
        _panel.Show();
    }

    private void Refresh()
    {
        if (_panel != null && !_isWindowHidden)
        {
            _panel.Refresh();
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
        L2GameUI.Instance.WindowClosed(this);
    }
}
