using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DenmarkMap
{
    [Serializable]
    public class MapLocation
    {
        public string name = "New location";
        [Tooltip("Latitude in decimal degrees (WGS84 / GPS), e.g. 55.6761")]
        public double latitude;
        [Tooltip("Longitude in decimal degrees (WGS84 / GPS), e.g. 12.5683")]
        public double longitude;

        public MapLocation() { }
        public MapLocation(string name, double latitude, double longitude)
        { this.name = name; this.latitude = latitude; this.longitude = longitude; }
    }

    /// <summary>
    /// Holds a list of named lat/lon locations and places a pin prefab at each one on the Denmark map.
    /// Put this on a GameObject that is a child of the map root so the pins follow the map.
    /// Pins are spawned as children of this GameObject (Clear removes all its children).
    /// Right-click the component header for "Populate Pins" / "Clear Pins" in edit mode.
    /// </summary>
    public class MapPinPlacer : MonoBehaviour
    {
        [Tooltip("Root of the DenmarkMap prefab.")]
        public Transform mapRoot;
        public GameObject pinPrefab;
        public List<MapLocation> locations = new List<MapLocation>
        {
            new MapLocation("Skagen", 57.7209, 10.5839),
            new MapLocation("Aarhus", 56.1572, 10.2107),
            new MapLocation("Copenhagen", 55.6761, 12.5683),
        };

        [Header("Placement")]
        [Tooltip("Raycast down onto the map so pins sit on the terrain. Falls back to sea-level height if nothing is hit.")]
        public bool snapToSurface = true;
        [Tooltip("Adds MeshColliders to the LOD2 tile meshes (needed for snapping and for VR interaction with the map).")]
        public bool addCollidersToMap = true;
        [Tooltip("Move each pin so the bottom of its renderers touches the surface (use if the pin pivot is not at its tip).")]
        public bool alignPinBottom = true;
        [Tooltip("Re-create the pins when entering Play mode if none exist.")]
        public bool populateOnStart = true;

        void Start()
        {
            if (populateOnStart && Application.isPlaying && transform.childCount == 0) Populate();
        }

        /// <summary>Adds a location and places its pin. Returns the pin instance.</summary>
        public GameObject AddLocation(string name, double latitude, double longitude)
        {
            var loc = new MapLocation(name, latitude, longitude);
            locations.Add(loc);
            if (addCollidersToMap) EnsureMapColliders();
            return PlacePin(loc);
        }

        [ContextMenu("Populate Pins")]
        public void Populate()
        {
            if (mapRoot == null || pinPrefab == null) { Debug.LogWarning("[MapPinPlacer] Assign mapRoot and pinPrefab.", this); return; }
            Clear();
            if (addCollidersToMap) EnsureMapColliders();
            Physics.SyncTransforms();
            foreach (var loc in locations) PlacePin(loc);
        }

        [ContextMenu("Clear Pins")]
        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying) { Undo.DestroyObjectImmediate(c); continue; }
#endif
                Destroy(c);
            }
        }

        GameObject PlacePin(MapLocation loc)
        {
            Vector3 local = DenmarkMapProjection.LatLonToLocal(loc.latitude, loc.longitude);
            Vector3 world = mapRoot.TransformPoint(local);
            Vector3 up = mapRoot.up;

            if (snapToSurface && TryGetSurface(mapRoot.TransformPoint(new Vector3(local.x, 0.5f, local.z)), -up, out var hit))
                world = hit;

            GameObject pin;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                pin = (GameObject)PrefabUtility.InstantiatePrefab(pinPrefab, transform);
                Undo.RegisterCreatedObjectUndo(pin, "Place map pin");
            }
            else
#endif
                pin = Instantiate(pinPrefab, transform);

            pin.name = "Pin_" + loc.name;
            pin.transform.SetPositionAndRotation(world, Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, up).sqrMagnitude > 1e-6f ? Vector3.ProjectOnPlane(Vector3.forward, up) : mapRoot.forward, up));

            if (alignPinBottom)
            {
                var rs = pin.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    float bottom = Vector3.Dot(b.min - world, up); // assumes the map is roughly level
                    pin.transform.position -= up * bottom;
                }
            }
            return pin;
        }

        bool TryGetSurface(Vector3 from, Vector3 dir, out Vector3 point)
        {
            point = default;
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(from, dir, 2f, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = h.collider.transform;
                if (!t.IsChildOf(mapRoot) || t.IsChildOf(transform)) continue; // only the map, never pins
                if (h.distance < best) { best = h.distance; point = h.point; }
            }
            return best < float.MaxValue;
        }

        /// <summary>Adds MeshColliders to the LOD2 mesh of every tile (low-poly, enough for pins and grabbing).</summary>
        public void EnsureMapColliders()
        {
            foreach (var mf in mapRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.name.EndsWith("_LOD2") || mf.sharedMesh == null) continue;
                if (mf.GetComponent<MeshCollider>() != null) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) { Undo.AddComponent<MeshCollider>(mf.gameObject).sharedMesh = mf.sharedMesh; continue; }
#endif
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
        }
    }
}
