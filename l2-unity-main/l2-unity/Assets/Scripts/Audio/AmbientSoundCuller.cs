using System.Collections.Generic;
using UnityEngine;

/// N'active que les emetteurs d'ambiance proches du joueur.
///
/// POURQUOI
/// Les level designers de 2006 ont disperse des sources ponctuelles a la main,
/// et il y en a beaucoup : mesure du 2026-08-14, **179 690 emetteurs** sur les
/// 152 regions, soit ~1 180 par region. Avec la fenetre de streaming a quatre
/// regions, ce sont environ 4 700 GameObjects actifs en permanence, chacun
/// portant un collider de declenchement que la physique teste contre le joueur.
///
/// Or un emetteur a plus de cent metres est inaudible : son evenement FMOD est
/// attenue bien avant. Le garder actif ne produit rien d'audible et coute a
/// chaque image.
///
/// COMMENT
/// Les emetteurs s'inscrivent a leur reveil et le culler les active ou les
/// desactive par lots, a intervalle regulier. Desactiver le MonoBehaviour
/// suffit : Unity cesse alors de lui envoyer OnTriggerEnter, qui est le seul
/// declencheur de lecture.
///
/// CE QU'IL NE FAUT PAS FAIRE
/// Mettre un Update() dans chaque emetteur pour qu'il se teste lui-meme : ce
/// serait 4 700 appels par image la ou une seule boucle centralisee suffit,
/// et Unity paie un cout fixe par MonoBehaviour actif.
public class AmbientSoundCuller : MonoBehaviour
{
    [Tooltip("Rayon d'activation, en unites Unity. Au-dela, l'emetteur est "
             + "desactive. A garder superieur a la portee d'attenuation la plus "
             + "longue, sinon on coupera des sons encore audibles.")]
    [SerializeField] private float _radius = 150f;

    [Tooltip("Marge d'hysteresis. Un emetteur ne se rallume qu'a _radius et ne "
             + "s'eteint qu'a _radius + cette marge : sans elle, un joueur "
             + "immobile pile a la frontiere ferait clignoter le son.")]
    [SerializeField] private float _hysteresis = 20f;

    [Tooltip("Secondes entre deux passes. Inutile de le faire chaque image : "
             + "parcourir 150 unites demande plusieurs secondes.")]
    [SerializeField] private float _interval = 0.5f;

    [SerializeField] private bool _verbose = false;

    private static readonly List<AmbientSoundEmitter> _emitters = new List<AmbientSoundEmitter>();

    private float _next;

    /// Inscrit un emetteur. Appele par l'emetteur lui-meme.
    ///
    /// Les emetteurs dont l'evenement est absent des banques ne s'inscrivent
    /// pas : ils sont deja desactives definitivement et les reveiller ne
    /// produirait qu'une exception de plus.
    public static void Register(AmbientSoundEmitter emitter)
    {
        if (emitter != null)
        {
            _emitters.Add(emitter);
        }
    }

    public static void Unregister(AmbientSoundEmitter emitter)
    {
        _emitters.Remove(emitter);
    }

    private static bool IsInsideTrigger(AmbientSoundEmitter emitter, Vector3 point)
    {
        var sphere = emitter.GetComponent<SphereCollider>();

        if (sphere == null)
        {
            return false;
        }

        Vector3 centre = emitter.transform.TransformPoint(sphere.center);
        Vector3 scale = emitter.transform.lossyScale;
        float radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));

        return (centre - point).sqrMagnitude <= radius * radius;
    }

    private void Update()
    {
        if (Time.time < _next)
        {
            return;
        }

        _next = Time.time + _interval;

        // Pas Camera.main : la LoadingCamera porte le meme tag et reste au
        // point du menu, a des milliers d'unites. Tous les emetteurs seraient
        // alors juges trop loin et desactives.
        Vector3 listener;

        if (CameraController.Instance != null)
        {
            listener = CameraController.Instance.transform.position;
        }
        else if (Camera.main != null)
        {
            listener = Camera.main.transform.position;
        }
        else
        {
            return;
        }
        float onSqr = _radius * _radius;
        float offSqr = (_radius + _hysteresis) * (_radius + _hysteresis);

        int active = 0;
        int removed = 0;

        // Parcours a l'envers : les regions dechargees laissent des references
        // detruites, qu'on retire au passage sans invalider l'index courant.
        for (int i = _emitters.Count - 1; i >= 0; i--)
        {
            AmbientSoundEmitter emitter = _emitters[i];

            if (emitter == null)
            {
                _emitters.RemoveAt(i);
                removed++;
                continue;
            }

            float sqr = (emitter.transform.position - listener).sqrMagnitude;

            if (emitter.enabled)
            {
                if (sqr > offSqr)
                {
                    emitter.Stop();
                    emitter.enabled = false;
                }
                else
                {
                    active++;
                }
            }
            else if (sqr <= onSqr)
            {
                emitter.enabled = true;
                active++;

                // Un composant desactive ne recoit pas OnTriggerEnter. Si le
                // joueur se trouve deja dans la sphere au moment ou on rallume
                // l'emetteur - au spawn, ou parce qu'il y est entre pendant que
                // le composant dormait - aucun declenchement n'arrivera jamais.
                if (IsInsideTrigger(emitter, listener) && !emitter.IsPlaying())
                {
                    emitter.Play();
                }
            }
        }

        if (_verbose)
        {
            int playing = 0;
            int inside = 0;

            foreach (AmbientSoundEmitter e in _emitters)
            {
                if (e == null || !e.enabled)
                {
                    continue;
                }

                if (e.IsPlaying())
                {
                    playing++;
                }

                if (IsInsideTrigger(e, listener))
                {
                    inside++;
                }
            }

            Debug.Log($"[AmbientCull] {active} actifs sur {_emitters.Count} inscrits | "
                      + $"{inside} sphere(s) contenant l'auditeur | {playing} en lecture"
                      + (removed > 0 ? $" ({removed} detruits retires)" : ""));
        }
    }
}
