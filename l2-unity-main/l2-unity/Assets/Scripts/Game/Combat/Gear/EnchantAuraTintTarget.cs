using UnityEngine;

// Pose sur chaque ParticleSystem d'une aura d'enchant au moment ou elle est
// montee. Memorise les couleurs D'ORIGINE du materiau du prefab et fournit
// une methode de re-teinte qui repart toujours de ces valeurs.
//
// Pourquoi c'est necessaire : sur les prefabs du Particle Pack, la couleur
// visible ne vient pas de la couleur des particules mais de proprietes du
// MATERIAU - typiquement un `_EmissionColor` HDR fixe (ElectricalSparks a
// meme un `_BaseColor` totalement noir, tout son rendu passe par l'emission).
// Regler `main.startColor` seul n'avait donc quasiment aucun effet visible.
//
// La re-teinte conserve la LUMINOSITE authoree par l'artiste et n'en change
// que la teinte : un effet concu comme un eclair tres lumineux le reste, il
// devient simplement bleu ou rouge selon le niveau d'enchant.
[DisallowMultipleComponent]
public class EnchantAuraTintTarget : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private ParticleSystem _particleSystem;
    private Material _materialInstance;

    private bool _hasBaseColor;
    private bool _hasColor;
    private bool _hasTintColor;
    private bool _hasEmissionColor;

    private float _baseLuminance;
    private float _colorLuminance;
    private float _tintLuminance;
    private float _emissionLuminance;
    private float _originalAlpha = 1f;

    public void Capture(ParticleSystem particleSystem)
    {
        _particleSystem = particleSystem;

        ParticleSystemRenderer renderer = particleSystem != null
            ? particleSystem.GetComponent<ParticleSystemRenderer>()
            : null;
        if (renderer == null) return;

        // .material (et non .sharedMaterial) : cree une instance propre a ce
        // renderer, sinon on modifierait l'asset partage sur disque et la
        // couleur du dernier perso enchante deteindrait sur tous les autres.
        _materialInstance = renderer.material;
        if (_materialInstance == null) return;

        _hasBaseColor = _materialInstance.HasProperty(BaseColorId);
        _hasColor = _materialInstance.HasProperty(ColorId);
        _hasTintColor = _materialInstance.HasProperty(TintColorId);
        _hasEmissionColor = _materialInstance.HasProperty(EmissionColorId);

        if (_hasBaseColor)
        {
            Color c = _materialInstance.GetColor(BaseColorId);
            _baseLuminance = Luminance(c);
            _originalAlpha = c.a;
        }
        if (_hasColor) _colorLuminance = Luminance(_materialInstance.GetColor(ColorId));
        if (_hasTintColor) _tintLuminance = Luminance(_materialInstance.GetColor(TintColorId));
        if (_hasEmissionColor) _emissionLuminance = Luminance(_materialInstance.GetColor(EmissionColorId));
    }

    // hue = couleur d'enchant (normalisee en teinte), intensity = facteur
    // multiplicatif applique par-dessus la luminosite d'origine.
    public void ApplyTint(Color hue, float intensity)
    {
        if (_particleSystem != null)
        {
            ParticleSystem.MainModule main = _particleSystem.main;
            main.startColor = hue;
        }

        if (_materialInstance == null) return;

        Color normalized = Normalize(hue);
        // hue.a porte l'opacite du calque : on l'applique par-dessus l'alpha
        // d'origine du materiau plutot que de l'ecraser, pour ne pas perdre
        // une transparence deja voulue par l'artiste.
        float alpha = _originalAlpha * Mathf.Clamp01(hue.a);

        if (_hasBaseColor && _baseLuminance > 0.0001f)
        {
            _materialInstance.SetColor(BaseColorId, Scale(normalized, _baseLuminance * intensity, alpha));
        }
        if (_hasColor && _colorLuminance > 0.0001f)
        {
            _materialInstance.SetColor(ColorId, Scale(normalized, _colorLuminance * intensity, alpha));
        }
        if (_hasTintColor && _tintLuminance > 0.0001f)
        {
            _materialInstance.SetColor(TintColorId, Scale(normalized, _tintLuminance * intensity, alpha));
        }
        if (_hasEmissionColor && _emissionLuminance > 0.0001f)
        {
            // L'emission est ce qui porte reellement le rendu de la plupart
            // de ces effets - c'est elle qui donne le glow intense.
            _materialInstance.SetColor(EmissionColorId, Scale(normalized, _emissionLuminance * intensity, 1f));
        }
    }

    private void OnDestroy()
    {
        // L'instance de materiau creee par renderer.material n'est pas
        // liberee automatiquement : sans ca, chaque arme enchantee equipee
        // laisserait un materiau orphelin en memoire.
        if (_materialInstance != null) Destroy(_materialInstance);
    }

    private static float Luminance(Color c)
    {
        return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }

    // Ramene la couleur a une teinte pure de luminance 1, pour pouvoir lui
    // reappliquer ensuite la luminosite voulue sans la doubler.
    private static Color Normalize(Color c)
    {
        float l = Luminance(c);
        if (l <= 0.0001f) return Color.white;
        return new Color(c.r / l, c.g / l, c.b / l, c.a);
    }

    private static Color Scale(Color normalized, float luminance, float alpha)
    {
        return new Color(normalized.r * luminance, normalized.g * luminance, normalized.b * luminance, alpha);
    }
}
