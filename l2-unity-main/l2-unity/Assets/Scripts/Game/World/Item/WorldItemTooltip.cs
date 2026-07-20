using UnityEngine;
using UnityEngine.UIElements;

// Info-bulle simple affichee au survol d'un objet au sol (nom + quantite),
// suivant le meme principe de positionnement world->screen que les
// nameplates (NameplatesManagerBase.UpdateNameplatePosition), mais
// independant du systeme Entity/Nameplate existant puisque WorldItem n'est
// pas un Entity.
public class WorldItemTooltip
{
    private static WorldItemTooltip _instance;
    public static WorldItemTooltip Instance => _instance ??= new WorldItemTooltip();

    private Label _label;

    private void EnsureLabel()
    {
        if (_label != null)
        {
            return;
        }

        if (L2GameUI.Instance == null || L2GameUI.Instance.RootElement == null)
        {
            return;
        }

        _label = new Label();
        _label.style.position = Position.Absolute;
        _label.style.color = Color.white;
        _label.style.unityTextAlign = TextAnchor.MiddleCenter;
        _label.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        _label.style.paddingLeft = 6;
        _label.style.paddingRight = 6;
        _label.style.paddingTop = 2;
        _label.style.paddingBottom = 2;
        _label.style.display = DisplayStyle.None;
        _label.pickingMode = PickingMode.Ignore;
        L2GameUI.Instance.RootElement.Add(_label);
    }

    public void Show(WorldItem item, Camera camera)
    {
        EnsureLabel();
        if (_label == null || item == null || camera == null)
        {
            return;
        }

        string name = ItemTable.Instance.GetItem(item.ItemTemplateId)?.ItemName?.Name ?? $"Item {item.ItemTemplateId}";
        _label.text = item.Count > 1 ? $"{name} (x{item.Count})" : name;

        Vector3 worldPosition = item.transform.position + Vector3.up * 0.5f;
        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);

        _label.style.display = DisplayStyle.Flex;
        _label.style.left = screenPosition.x - _label.resolvedStyle.width / 2f;
        _label.style.top = Screen.height - screenPosition.y - _label.resolvedStyle.height;
    }

    public void Hide()
    {
        if (_label != null)
        {
            _label.style.display = DisplayStyle.None;
        }
    }
}
