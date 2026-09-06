#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Rendering;

[ExecuteInEditMode]
[RequireComponent(typeof(Light))]
public class LightLOD : MonoBehaviour
{
    private Light _light;

    [SerializeField]
    [Range(0, 15)]
    private float _updateDelay = 1f;
    [SerializeField] private float _squareDistanceFromCamera;
    [SerializeField] private float _lastUpdate;
    private Camera _mainCamera;

    [SerializeField]
    private List<LODAdjustment> LODLevels = new();

    private bool _ready = false;
    private bool _inRange = true;
    private LightSchedule _schedule;

    private void Awake()
    {
        _light = GetComponent<Light>();
    }

    private void Start()
    {
        _ready = false;
        _lastUpdate = 0;
    }

    // Les lumieres d'une region se chargent avant la camera du joueur : lire
    // CameraController.Instance dans Start levait une NullReferenceException,
    // et le LOD ne s'appliquait alors jamais.
    private bool ResolveCamera()
    {
        if (_mainCamera != null)
        {
            return true;
        }

        if (CameraController.Instance == null)
        {
            return false;
        }

        _mainCamera = CameraController.Instance.GetComponent<Camera>();

        return _mainCamera != null;
    }


    private void Update()
    {
        if (_light == null)
        {
            _light = GetComponent<Light>();
            if (_light == null)
            {
                return;
            }
        }

#if (UNITY_EDITOR) 
        if (!EditorApplication.isPlaying)
        {
            _ready = true;
        }
#endif

        if (!_ready)
        {
            if (CameraController.Instance == null || CameraController.Instance.Target == null)
            {
                return;
            }

            if (CameraController.Instance.CurrentDistance > CameraController.Instance.MaxDistance)
            {
                return;
            }

            _ready = true;
        }

        if (Time.time - _lastUpdate >= _updateDelay)
        {
            _lastUpdate = Time.time;
#if (UNITY_EDITOR)
            if (EditorApplication.isPlaying)
            {
                if (ResolveCamera())
                {
                    AdjustLODQuality(_mainCamera);
                }
            }
            else
            {
                if (Camera.current != null)
                {
                    AdjustLODQuality(Camera.current);
                }
            }
#else
            if (ResolveCamera())
            {
                AdjustLODQuality(_mainCamera);
            }
#endif
        }
    }


    private void AdjustLODQuality(Camera camera)
    {
        _squareDistanceFromCamera = Vector3.SqrMagnitude(
            camera.transform.position - transform.position
        );

        for (int i = 0; i < LODLevels.Count; i++)
        {
            if (_squareDistanceFromCamera > LODLevels[i].MinSquareDistance
                && _squareDistanceFromCamera <= LODLevels[i].MaxSquareDistance
            )
            {
                _inRange = true;
                RefreshEnabled();
                _light.shadows = LODLevels[i].LightShadows;
                if (QualitySettings.shadowResolution <= LODLevels[i].ShadowResolution)
                {
                    _light.shadowResolution = (LightShadowResolution)QualitySettings.shadowResolution;
                }
                else
                {
                    _light.shadowResolution = (LightShadowResolution)LODLevels[i].ShadowResolution;
                }

                return;
            }
        }

        _inRange = false;
        RefreshEnabled();
    }

    // Seul ecrivain de Light.enabled : la distance vient d'ici, l'heure de
    // LightSchedule. Sans cet arbitrage les deux se battraient a chaque image.
    public void RefreshEnabled()
    {
        if (_light == null)
        {
            return;
        }

        if (_schedule == null)
        {
            _schedule = GetComponent<LightSchedule>();
        }

        // Hors mode jeu, LightSchedule.Awake n'a pas tourne et son IsOn vaut
        // false : le consulter eteindrait les lumieres dans la vue Scene.
        bool scheduleAllows = _schedule == null || !Application.isPlaying || _schedule.IsOn;

        _light.enabled = _inRange && scheduleAllows;
    }
}