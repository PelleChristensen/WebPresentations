using UnityEngine;

namespace DenmarkMap
{
    /// <summary>
    /// Animates a wind turbine prop: spins the rotor (blades + hub) around its local Z axis every frame,
    /// and yaws the hood (nacelle) around the tower's Y axis so the rotor faces into the wind.
    /// Hierarchy: Windmill (this) > Tower > Hood > Rotor.
    /// </summary>
    [ExecuteAlways]
    public class WindTurbine : MonoBehaviour
    {
        [Tooltip("Rotor (blades + hub). Spins around its local Z axis.")]
        public Transform rotor;
        [Tooltip("Hood / nacelle. Yaws around the tower's Y axis.")]
        public Transform hood;

        [Header("Rotation")]
        [Tooltip("Blade speed in degrees per second. Positive = clockwise seen from the front, like real turbines. 72 °/s ≈ 12 rpm.")]
        public float rotorSpeed = 72f;
        [Tooltip("Spin the rotor in edit mode too (handy for previewing).")]
        public bool spinInEditMode = false;

        [Header("Wind direction")]
        [Tooltip("Direction the wind comes FROM, in compass degrees: 0 = north, 90 = east, 180 = south, 270 = west. The rotor turns to face it.")]
        [Range(0f, 360f)] public float windDirection = 270f;
        [Tooltip("Optional: transform whose forward (+Z) is north. Leave empty to use the DenmarkMap root above this object, or world +Z.")]
        public Transform northReference;

        Transform _mapRoot;

        void OnEnable()
        {
            if (rotor == null) rotor = FindDeep(transform, "Rotor");
            if (hood == null) hood = FindDeep(transform, "Hood");
            var map = GetComponentInParent<MapLODDistance>();
            _mapRoot = map ? map.transform : null;
            ApplyWindDirection();
        }

        void OnValidate() => ApplyWindDirection();

        void Update()
        {
            if (rotor != null && (Application.isPlaying || spinInEditMode))
                rotor.Rotate(0f, 0f, rotorSpeed * Time.deltaTime, Space.Self);
        }

        /// <summary>Sets the wind direction (degrees, wind coming from; 0 = north) and turns the hood.</summary>
        public void SetWindDirection(float degreesFrom)
        {
            windDirection = Mathf.Repeat(degreesFrom, 360f);
            ApplyWindDirection();
        }

        public void ApplyWindDirection()
        {
            if (hood == null) return;
            Transform towerT = hood.parent != null ? hood.parent : transform;
            Vector3 up = towerT.up;
            Vector3 north = NorthDirection(up);
            // Unity rotates clockwise (seen from above) for positive angles around +Y: north -> east.
            Vector3 towardWind = Quaternion.AngleAxis(windDirection, up) * north;
            hood.rotation = Quaternion.LookRotation(towardWind, up);   // hood +Z (rotor front) faces into the wind
        }

        Vector3 NorthDirection(Vector3 up)
        {
            Vector3 n;
            if (northReference != null) n = northReference.forward;
            else if (_mapRoot != null) n = -_mapRoot.forward;           // the map's tiles have north along local -Z
            else n = Vector3.forward;
            n = Vector3.ProjectOnPlane(n, up);
            return n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
        }

        static Transform FindDeep(Transform t, string name)
        {
            foreach (Transform c in t)
            {
                if (c.name == name) return c;
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
