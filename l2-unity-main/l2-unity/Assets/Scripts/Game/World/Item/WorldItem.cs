using UnityEngine;

// Represente un objet visible au sol dans le monde (drop de monstre, objet
// lache par un joueur...). Volontairement PAS un Entity : pas de HP, combat,
// controleur d'animation - juste un objet ramassable. Voir ItemSpawner pour
// la creation/suppression et WorldSpawner.RemoveObject pour le despawn
// (reutilise DeleteObject, deja gere pour les NPC).
public class WorldItem : MonoBehaviour
{
    [SerializeField] private Transform _iconBillboard;
    [SerializeField] private MeshRenderer _iconRenderer;

    private static Transform _cameraTransform;

    public int ObjectId { get; private set; }
    public int ItemTemplateId { get; private set; }
    public int Count { get; private set; }
    public bool IsStackable { get; private set; }

    private const float BobAmplitude = 0.08f;
    private const float BobSpeed = 1.5f;
    private const float SpinSpeed = 45f;
    private const float HighlightScale = 1.15f;
    private const float HighlightLerpSpeed = 10f;

    private Vector3 _basePosition;
    private Vector3 _baseScale;
    private float _bobOffset;
    private bool _isHighlighted;

    public void Initialize(int objectId, int itemTemplateId, int count, bool isStackable)
    {
        ObjectId = objectId;
        ItemTemplateId = itemTemplateId;
        Count = count;
        IsStackable = isStackable;

        _basePosition = transform.position;
        _baseScale = transform.localScale;
        _bobOffset = Random.Range(0f, Mathf.PI * 2f);

        if (_iconRenderer != null)
        {
            Texture2D icon = IconTable.Instance.GetIcon(itemTemplateId);
            if (icon != null)
            {
                _iconRenderer.material.mainTexture = icon;
            }
        }
    }

    // Survol souris (cf. ClickManager) - simple pulse d'echelle, fonctionne
    // pareil pour le placeholder (billboard) et les vrais meshes importes,
    // sans avoir besoin d'un shader d'outline dedie.
    public void SetHighlighted(bool highlighted)
    {
        _isHighlighted = highlighted;
    }

    private void Update()
    {
        if (_cameraTransform == null && Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }

        // Remappe le sinus de [-1,1] vers [0,1] : le rebond ne doit se
        // produire que VERS LE HAUT depuis la position de repos (deja calee
        // sur le sol), jamais en-dessous (sinon l'objet s'enfonce visuellement
        // dans le sol au point bas de l'oscillation).
        float bobT = (Mathf.Sin(Time.time * BobSpeed + _bobOffset) + 1f) * 0.5f;
        transform.position = _basePosition + Vector3.up * (bobT * BobAmplitude);

        Vector3 targetScale = _baseScale * (_isHighlighted ? HighlightScale : 1f);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * HighlightLerpSpeed);

        if (_iconBillboard != null && _cameraTransform != null)
        {
            _iconBillboard.rotation = Quaternion.LookRotation(_iconBillboard.position - _cameraTransform.position);
        }
        else if (_iconBillboard == null)
        {
            transform.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);
        }
    }
}
