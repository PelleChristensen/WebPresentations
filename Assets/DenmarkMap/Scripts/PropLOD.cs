using UnityEngine;

namespace DenmarkMap
{
    /// <summary>
    /// Distance-based LOD for map props (windmills, landmarks...). Uses the same distances as the
    /// MapLODDistance on the map (found in parents) so props switch together with the terrain tiles;
    /// falls back to its own values when the prop is not on a map.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(LODGroup))]
    public class PropLOD : MonoBehaviour
    {
        public float lod0Distance = 0.8f;
        public float lod1Distance = 1.4f;
        public bool neverCull = true;
        public float fallbackFov = 90f;

        LODGroup _group;
        MapLODDistance _map;
        float _lastFov = -1f, _lastBias = -1f, _lastD0 = -1f, _lastD1 = -1f;
        Vector3 _lastScale;

        void OnEnable()
        {
            _group = GetComponent<LODGroup>();
            _map = GetComponentInParent<MapLODDistance>();
            _lastFov = -1f;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (_group == null) return;
            float d0 = _map ? _map.lod0Distance : lod0Distance;
            float d1 = _map ? _map.lod1Distance : lod1Distance;
            bool keep = _map ? _map.neverCull : neverCull;
            var cam = (_map && _map.targetCamera) ? _map.targetCamera : Camera.main;
            float fov = cam ? cam.fieldOfView : fallbackFov;
            float bias = QualitySettings.lodBias;
            if (Mathf.Approximately(fov, _lastFov) && Mathf.Approximately(bias, _lastBias) && d0 == _lastD0 && d1 == _lastD1
                && transform.lossyScale == _lastScale) return;
            _lastFov = fov; _lastBias = bias; _lastD0 = d0; _lastD1 = d1; _lastScale = transform.lossyScale;
            MapLODDistance.ApplyDistances(_group, d0, d1, keep, fov);
        }
    }
}
