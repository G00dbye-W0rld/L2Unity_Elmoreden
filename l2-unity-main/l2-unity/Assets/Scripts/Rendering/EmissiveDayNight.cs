using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Attenue le jour l'emission des materiaux auto-illumines importes du client
// (fenetres, runes, lampes). Ces materiaux etant partages, on ne peut pas agir
// par objet : on remplace donc chacun par UNE copie d'execution, pilotee ici.
//
// La copie n'est pas une precaution de style : ecrire directement dans un
// materiau modifie l'ASSET, et la valeur reste apres l'arret du mode jeu.
public class EmissiveDayNight : MonoBehaviour
{
    [Tooltip("Part de l'emission conservee en plein jour. 0 = eteint.")]
    [SerializeField] [Range(0f, 1f)] private float _dayFactor = 0.15f;

    [Tooltip("Duree de la transition, en secondes.")]
    [SerializeField] private float _fadeSeconds = 6f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly Dictionary<Material, Material> _copies = new Dictionary<Material, Material>();
    private readonly HashSet<Material> _isCopy = new HashSet<Material>();
    private readonly List<Material> _live = new List<Material>();
    private readonly List<Color> _authored = new List<Color>();

    private float _factor = -1f;
    private float _applied = -1f;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Collect();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Collect();
    }

    // Rappelee a chaque region chargee : le streaming amene sans cesse de
    // nouveaux renderers, y compris sur des materiaux deja connus.
    private void Collect()
    {
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];

                if (m == null || _isCopy.Contains(m))
                {
                    continue;
                }

                if (!_copies.TryGetValue(m, out Material copy))
                {
                    if (!IsEmissive(m))
                    {
                        continue;
                    }

                    copy = new Material(m) { name = m.name + " (jour/nuit)" };

                    _copies[m] = copy;
                    _isCopy.Add(copy);
                    _live.Add(copy);
                    _authored.Add(m.GetColor(EmissionColorId));
                }

                mats[i] = copy;
                changed = true;
            }

            if (changed)
            {
                r.sharedMaterials = mats;
            }
        }

        _applied = -1f;
    }

    private static bool IsEmissive(Material m)
    {
        return m.HasProperty(EmissionColorId)
               && m.IsKeywordEnabled("_EMISSION")
               && m.GetColor(EmissionColorId).maxColorComponent > 0.001f;
    }

    private void Update()
    {
        if (WorldClock.Instance == null || _live.Count == 0)
        {
            return;
        }

        float target = WorldClock.Instance.IsNightTime() ? 1f : _dayFactor;

        if (_factor < 0f)
        {
            _factor = target;
        }
        else if (_fadeSeconds > 0f)
        {
            float span = Mathf.Max(0.001f, 1f - _dayFactor);
            _factor = Mathf.MoveTowards(_factor, target, span * Time.deltaTime / _fadeSeconds);
        }
        else
        {
            _factor = target;
        }

        if (Mathf.Approximately(_factor, _applied))
        {
            return;
        }

        _applied = _factor;

        for (int i = 0; i < _live.Count; i++)
        {
            _live[i].SetColor(EmissionColorId, _authored[i] * _factor);
        }
    }
}
