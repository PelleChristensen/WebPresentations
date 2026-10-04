using UnityEngine;

namespace DenmarkMap
{
    /// <summary>
    /// Converts metric LOD distances (e.g. LOD0 &lt; 0.8 m, LOD1 &lt; 1.4 m, LOD2 beyond) into
    /// screen-relative LODGroup transitions for every map tile under this object.
    /// Unity LODGroups switch on screen size, so the thresholds depend on the camera FOV and
    /// QualitySettings.lodBias. This component recomputes them when either changes (e.g. when an
    /// XR headset sets its own FOV), and when the map is scaled.
    /// </summary>
    [ExecuteAlways]
    public class MapLODDistance : MonoBehaviour
    {
        [Tooltip("Use LOD0 closer than this distance (metres, measured to tile centre).")]
        public float lod0Distance = 0.8f;
        [Tooltip("Use LOD1 closer than this distance; LOD2 beyond.")]
        public float lod1Distance = 1.4f;
        [Tooltip("Never cull tiles (LOD2 stays visible at any distance).")]
        public bool neverCull = true;
        [Tooltip("Camera used to read FOV. Defaults to Camera.main.")]
        public Camera targetCamera;
        [Tooltip("FOV used when no camera is found (degrees, vertical).")]
        public float fallbackFov = 90f;

        LODGroup[] _groups;
        float _lastFov = -1f, _lastBias = -1f;
        Vector3 _lastScale;

        void OnEnable()
        {
            _groups = GetComponentsInChildren<LODGroup>(true);
            Apply(true);
        }

        void LateUpdate() => Apply(false);

        float CurrentFov()
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            return cam != null ? cam.fieldOfView : fallbackFov;
        }

        public void Apply(bool force)
        {
            if (_groups == null || _groups.Length == 0) _groups = GetComponentsInChildren<LODGroup>(true);
            float fov = CurrentFov();
            float bias = QualitySettings.lodBias;
            if (!force && Mathf.Approximately(fov, _lastFov) && Mathf.Approximately(bias, _lastBias) && transform.lossyScale == _lastScale)
                return;
            _lastFov = fov; _lastBias = bias; _lastScale = transform.lossyScale;

            float halfTan = Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
            foreach (var g in _groups)
            {
                if (g == null) continue;
                var lods = g.GetLODs();
                if (lods.Length == 0) continue;
                // world-space size Unity uses for the group
                Vector3 s = g.transform.lossyScale;
                float size = g.size * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                float[] d = { lod0Distance, lod1Distance };
                for (int i = 0; i < lods.Length; i++)
                {
                    float h;
                    if (i < d.Length) h = size * bias / (2f * d[i] * halfTan);
                    else h = neverCull ? 0.0001f : size * bias / (2f * 6f * halfTan);
                    // transitions must be strictly decreasing
                    if (i > 0) h = Mathf.Min(h, lods[i - 1].screenRelativeTransitionHeight - 0.0001f);
                    lods[i].screenRelativeTransitionHeight = Mathf.Clamp(h, 0.00005f, 0.9999f);
                }
                g.SetLODs(lods);
            }
        }
    }
}
