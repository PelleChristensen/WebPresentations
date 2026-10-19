using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace DenmarkMap
{
    [Serializable]
    public class MunicipalityMarker
    {
        public string name;
        [Tooltip("Danish municipality code (kommunekode), e.g. 101 = København")]
        public int municipalityCode;
        [Tooltip("Where to place the marker (usually the main town)")]
        public double latitude, longitude;

        [NonSerialized] public float consumptionKWh;
        [NonSerialized] public Transform marker;
        [NonSerialized] public WindParkLabel label;
        [NonSerialized] public float currentHeight = 1f, targetHeight = 1f;

        public MunicipalityMarker() { }
        public MunicipalityMarker(string n, int code, double lat, double lon) { name = n; municipalityCode = code; latitude = lat; longitude = lon; }
    }

    /// <summary>
    /// Household electricity consumption per municipality from Energi Data Service (Energinet),
    /// dataset PrivateConsumptionHeatingHour: measured, hourly, per municipality, published with a 9–15 day delay.
    /// Live mode shows the newest published hour; Historical mode (see MapTimeline) shows the selected hour.
    /// Each municipality gets a marker (your city prefab) whose height scales with consumption, plus a label.
    /// </summary>
    public class MunicipalityConsumptionVisualizer : MonoBehaviour
    {
        public Transform mapRoot;
        public GameObject markerPrefab;

        public List<MunicipalityMarker> municipalities = new List<MunicipalityMarker>
        {
            new MunicipalityMarker("København", 101, 55.6761, 12.5683),
            new MunicipalityMarker("Aarhus", 751, 56.1572, 10.2107),
            new MunicipalityMarker("Aalborg", 851, 57.0488, 9.9217),
            new MunicipalityMarker("Odense", 461, 55.4038, 10.4024),
            new MunicipalityMarker("Vejle", 630, 55.7113, 9.5357),
            new MunicipalityMarker("Esbjerg", 561, 55.4765, 8.4594),
            new MunicipalityMarker("Frederiksberg", 147, 55.6786, 12.5332),
            new MunicipalityMarker("Randers", 730, 56.4607, 10.0364),
            new MunicipalityMarker("Silkeborg", 740, 56.1697, 9.5451),
            new MunicipalityMarker("Horsens", 615, 55.8607, 9.8503),
        };

        [Header("Data")]
        [Tooltip("How often to check for newly published data in Live mode (the source updates about once a day).")]
        public float liveRefreshMinutes = 30f;

        [Header("Markers")]
        [Tooltip("Vertical scale of the marker for the largest consumer (prefab height x this).")]
        public float maxHeightScale = 6f;
        public float minHeightScale = 0.6f;
        public bool snapToTerrain = true;
        public float labelGap = 0.015f;
        public float labelScale = 0.0005f;
        public TMP_FontAsset font;

        [Header("Status (read only)")]
        public string status = "not started";
        public string shownHourDK;

        const string Api = "https://api.energidataservice.dk/dataset/PrivateConsumptionHeatingHour";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        MapTimeline _timeline;
        Coroutine _loop;

        [Serializable] class Rec { public string TimeDK; public int MunicipalityCode; public string Municipality; public float ConsumptionkWh; }
        [Serializable] class Resp { public Rec[] records; }

        void Start()
        {
            if (mapRoot == null) { var m = GetComponentInParent<MapLODDistance>(); mapRoot = m ? m.transform : transform.parent; }
            BuildMarkers();
            _timeline = MapTimeline.Instance;
            if (_timeline != null) _timeline.Changed += Restart;
            Restart();
        }

        void OnDestroy() { if (_timeline != null) _timeline.Changed -= Restart; }

        void Restart()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = StartCoroutine(Loop());
        }

        IEnumerator Loop()
        {
            while (true)
            {
                bool live = _timeline == null || _timeline.IsLive;
                yield return live ? LoadLatest() : LoadHour(_timeline.SelectedDK);
                if (!live) yield break;
                yield return new WaitForSeconds(liveRefreshMinutes * 60f);
            }
        }

        void Update()
        {
            foreach (var m in municipalities)
            {
                if (m.marker == null) continue;
                m.currentHeight = Mathf.Lerp(m.currentHeight, m.targetHeight, 1f - Mathf.Exp(-3f * Time.deltaTime));
                var s = m.marker.localScale; s.y = m.currentHeight; m.marker.localScale = s;
                if (m.label != null)
                    m.label.transform.position = m.marker.position + mapRoot.up * (MarkerTop(m) + labelGap);
            }
        }

        float _prefabHeight = 0.01f, _prefabTop = 0.01f;
        float MarkerTop(MunicipalityMarker m) => _prefabTop * m.currentHeight;

        // ---------------------------------------------------------------- markers
        void BuildMarkers()
        {
            if (markerPrefab != null)
            {
                var rs = markerPrefab.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    // measure in prefab-local space (prefab root may carry an arbitrary saved position)
                    var tmp = Instantiate(markerPrefab); tmp.transform.position = Vector3.zero; tmp.transform.rotation = Quaternion.identity;
                    var b = tmp.GetComponentsInChildren<Renderer>()[0].bounds;
                    foreach (var r in tmp.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                    _prefabHeight = b.size.y; _prefabTop = b.max.y;
                    Destroy(tmp);
                }
            }
            if (snapToTerrain) MapPinPlacerColliders(mapRoot);
            Physics.SyncTransforms();

            foreach (var m in municipalities)
            {
                Vector3 local = DenmarkMapProjection.LatLonToLocal(m.latitude, m.longitude);
                Vector3 world = mapRoot.TransformPoint(local);
                if (snapToTerrain && Ground(mapRoot.TransformPoint(new Vector3(local.x, 0.5f, local.z)), out var hit)) world = hit;
                var go = markerPrefab ? Instantiate(markerPrefab, transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "City_" + m.name;
                go.transform.SetParent(transform, true);
                go.transform.SetPositionAndRotation(world, Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, mapRoot.up), mapRoot.up));
                m.marker = go.transform;
                m.label = WindParkLabel.Create(go.transform.parent, world + mapRoot.up * (_prefabTop + labelGap), labelScale, font, m.name);
                m.label.SetLoading();
            }
        }

        bool Ground(Vector3 from, out Vector3 point)
        {
            point = default; float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(from, -mapRoot.up, 2f, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = h.collider.transform;
                if (!t.IsChildOf(mapRoot) || t.IsChildOf(transform) || !t.name.Contains("_LOD")) continue;
                if (h.distance < best) { best = h.distance; point = h.point; }
            }
            return best < float.MaxValue;
        }

        static void MapPinPlacerColliders(Transform root)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name.EndsWith("_LOD2") && mf.name.StartsWith("Tile_") && mf.sharedMesh != null && mf.GetComponent<MeshCollider>() == null)
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        // ---------------------------------------------------------------- data
        IEnumerator Get(string url, Action<string> done)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                using (var r = UnityWebRequest.Get(url))
                {
                    r.timeout = 30;
                    yield return r.SendWebRequest();
                    if (r.result == UnityWebRequest.Result.Success) { done(r.downloadHandler.text); yield break; }
                    if (r.responseCode == 429) { status = "rate limited – retrying in 2 min"; yield return new WaitForSeconds(125f); continue; }
                    status = "error: " + r.error; done(null); yield break;
                }
            }
            done(null);
        }

        string CodeFilter() => UnityWebRequest.EscapeURL("{\"MunicipalityCode\":[" + string.Join(",", municipalities.Select(m => "\"" + m.municipalityCode + "\"")) + "]}");

        IEnumerator LoadLatest()
        {
            status = "finding newest published hour…";
            string latest = null;
            yield return Get($"{Api}?limit=1&sort=TimeUTC%20DESC", txt =>
            {
                var r = txt != null ? JsonUtility.FromJson<Resp>(txt) : null;
                if (r?.records != null && r.records.Length > 0) latest = r.records[0].TimeDK;
            });
            if (latest == null) yield break;
            DateTime t = DateTime.Parse(latest, Inv);
            yield return LoadHour(t);
        }

        IEnumerator LoadHour(DateTime hourDK)
        {
            hourDK = new DateTime(hourDK.Year, hourDK.Month, hourDK.Day, hourDK.Hour, 0, 0);
            string start = hourDK.ToString("yyyy-MM-dd'T'HH:mm", Inv), end = hourDK.AddHours(1).ToString("yyyy-MM-dd'T'HH:mm", Inv);
            status = "loading " + start + "…";
            Resp resp = null;
            yield return Get($"{Api}?start={start}&end={end}&timezone=dk&limit=0&filter={CodeFilter()}", txt =>
            {
                if (txt != null) resp = JsonUtility.FromJson<Resp>(txt);
            });
            if (resp?.records == null) yield break;
            if (resp.records.Length == 0) { status = "no data for " + start + " (data is published 9–15 days late)"; SetNoData(); yield break; }

            foreach (var m in municipalities) m.consumptionKWh = 0f;
            foreach (var r in resp.records)
            {
                var m = municipalities.FirstOrDefault(x => x.municipalityCode == r.MunicipalityCode);
                if (m != null) m.consumptionKWh += r.ConsumptionkWh;
            }
            float max = Mathf.Max(1f, municipalities.Max(m => m.consumptionKWh));
            string when = hourDK.ToString("d MMM HH:00", Inv);
            foreach (var m in municipalities)
            {
                m.targetHeight = Mathf.Lerp(minHeightScale, maxHeightScale, m.consumptionKWh / max);
                float mwh = m.consumptionKWh / 1000f;
                m.label?.SetCustom(mwh >= 100f ? $"{mwh:0} MWh" : $"{mwh:0.0} MWh", $"households · {when}–{hourDK.AddHours(1):HH}:00");
            }
            shownHourDK = start;
            status = $"ok – {resp.records.Length} records for {start} (DK time)";
        }

        void SetNoData()
        {
            foreach (var m in municipalities) { m.targetHeight = minHeightScale; m.label?.SetCustom("–", "no data for this hour"); }
        }
    }
}
