using UnityEngine;

public class CursorManager : MonoBehaviour
{
    [SerializeField] private CursorType _lastCursorType = CursorType.Default;
    public enum CursorType
    {
        Default,
        Attack,
        Talk,
        Pickup
    }

    [SerializeField] private Texture2D _defaultCursorTexture;
    [SerializeField] private Texture2D _attackCursorTexture;
    [SerializeField] private Texture2D _talkCursorTexture;
    [SerializeField] private Texture2D _pickupCursorTexture;
    private string _cursorFolder = "Data/UI/Assets/Cursor/";
    private bool _graphicCursorEnabled = true;

    private static CursorManager _instance;
    public static CursorManager Instance { get { return _instance; } }

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

        _defaultCursorTexture = Resources.Load<Texture2D>(_cursorFolder + "Default");
        _attackCursorTexture = Resources.Load<Texture2D>(_cursorFolder + "Attack");
        _talkCursorTexture = Resources.Load<Texture2D>(_cursorFolder + "Talk");
        _pickupCursorTexture = Resources.Load<Texture2D>(_cursorFolder + "Pickup");
    }

    public void ChangeCursor(CursorType cursorType)
    {
        if (_lastCursorType == cursorType)
        {
            return;
        }

        _lastCursorType = cursorType;
        ApplyCursor(cursorType);
    }

    // Bascule entre le curseur graphique (textures ci-dessus) et le curseur OS
    // natif - reglage "Curseur graphique" de SettingsWindow. Reapplique
    // immediatement (contourne le garde-fou _lastCursorType de ChangeCursor,
    // qui sinon ignorerait un changement de reglage a type de curseur inchange).
    public void SetGraphicCursorEnabled(bool enabled)
    {
        _graphicCursorEnabled = enabled;
        ApplyCursor(_lastCursorType);
    }

    private void ApplyCursor(CursorType cursorType)
    {
        if (!_graphicCursorEnabled)
        {
            UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }

        switch (cursorType)
        {
            case CursorType.Default:
                UnityEngine.Cursor.SetCursor(_defaultCursorTexture, Vector2.zero, CursorMode.Auto);
                break;
            case CursorType.Attack:
                UnityEngine.Cursor.SetCursor(_attackCursorTexture, Vector2.zero, CursorMode.Auto);
                break;
            case CursorType.Talk:
                UnityEngine.Cursor.SetCursor(_talkCursorTexture, Vector2.zero, CursorMode.Auto);
                break;
            case CursorType.Pickup:
                UnityEngine.Cursor.SetCursor(_pickupCursorTexture, Vector2.zero, CursorMode.Auto);
                break;
        }
    }
}
