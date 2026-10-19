using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace DenmarkMap
{
    public enum PriceArea { DK1, DK2 }

    [Serializable]
    public class WindPark
    {
        public string name;
        public double latitude, longitude;
        public float capacityMW;
        public PriceArea area;
        [Tooltip("Draw turbines + label. Unticked parks are only used to calibrate the estimates.")]
        public bool show = true;

        [NonSerialized] public float windSpeed, windDirection, estimateMW, outputMW;
        [NonSerialized] public float currentSpin, currentDir = -1f;
        [NonSerialized] public readonly List<WindTurbine> turbines = new List<WindTurbine>();
        [NonSerialized] public WindParkLabel label;

        public WindPark() { }
        public WindPark(string n, double lat, double lon, float mw, PriceArea a, bool s = true)
        { name = n; latitude = lat; longitude = lon; capacityMW = mw; area = a; show = s; }
    }

    /// <summary>
    /// Experiment: live wind-park visualisation on the Denmark map.
    /// - Wind speed/direction per park: Open-Meteo (no key), 100 m above sea level.
    /// - Real measured offshore wind production: Energi Data Service (Energinet, no key):
    ///   1-minute national total (PowerSystemRightNow) split by price area (ElectricityProdex5MinRealtime).
    /// - Per-park output is ESTIMATED: each park's live wind through a generic power curve, then scaled so the parks in
    ///   each price area add up to the measured offshore production (no open per-park real-time data exists).
    /// Place this on a child of the DenmarkMap root. Works in Play mode.
    /// </summary>
    public class WindParkVisualizer : MonoBehaviour
    {
        public Transform mapRoot;
        public GameObject turbinePrefab;
        [Tooltip("Scale of each turbine relative to the prefab (prefab rotor is ~4 cm).")]
        public float turbineScale = 0.6f;
        [Tooltip("Distance between turbines in a park (metres on the table).")]
        public float turbineSpacing = 0.03f;

        [Header("Data")]
        public float refreshSeconds = 60f;
        public List<WindPark> parks = DefaultParks();

        [Header("Animation")]
        [Tooltip("Rotor speed (deg/s) at 100 % of capacity. Speed scales linearly with the park's output.")]
        public float maxRotorSpeed = 120f;
        [Tooltip("How fast rotor speed changes (deg/s per second).")]
        public float spinAcceleration = 40f;
        [Tooltip("How fast the hoods turn to a new wind direction (deg/s).")]
        public float yawSpeed = 15f;

        [Header("Labels")]
        public float labelHeight = 0.06f;
        public float labelScale = 0.0005f;
        public TMP_FontAsset font;

        [Header("Status (read only)")]
        public string status = "not started";
        public string lastUpdateLocal;
        public float dk1OffshoreMW, dk2OffshoreMW, dkOffshoreTotalMW;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static List<WindPark> DefaultParks() => new List<WindPark>
        {
            // 10 largest Danish wind parks (all offshore). Capacity in MW.
            new WindPark("Kriegers Flak", 55.0167, 12.9333, 604.8f, PriceArea.DK2),
            new WindPark("Horns Rev 3", 55.7000, 7.6889, 406.7f, PriceArea.DK1),
            new WindPark("Anholt", 56.6000, 11.2100, 400.0f, PriceArea.DK1),
            new WindPark("Vesterhav Syd & Nord", 56.3800, 8.0000, 344.0f, PriceArea.DK1),
            new WindPark("Horns Rev 2", 55.6000, 7.5900, 209.3f, PriceArea.DK1),
            new WindPark("Rødsand 2", 54.5600, 11.5500, 207.0f, PriceArea.DK2),
            new WindPark("Nysted", 54.5500, 11.7100, 165.6f, PriceArea.DK2),
            new WindPark("Horns Rev 1", 55.5297, 7.9061, 160.0f, PriceArea.DK1),
            new WindPark("Middelgrunden", 55.6908, 12.6703, 40.0f, PriceArea.DK2),
            new WindPark("Nissum Bredning", 56.6700, 8.2439, 28.0f, PriceArea.DK1),
            // smaller offshore parks: only used so the estimates add up to the measured totals
            new WindPark("Samsø", 55.7200, 10.5800, 23.0f, PriceArea.DK1, false),
            new WindPark("Sprogø", 55.3400, 10.9600, 21.0f, PriceArea.DK2, false),
            new WindPark("Rønland", 56.6628, 8.2194, 17.2f, PriceArea.DK1, false),
            new WindPark("Avedøre Holme", 55.6000, 12.4583, 10.8f, PriceArea.DK2, false),
            new WindPark("Frederikshavn", 57.4444, 10.5611, 7.6f, PriceArea.DK1, false),
            new WindPark("Tunø Knob", 55.9694, 10.3556, 5.0f, PriceArea.DK1, false),
        };

        // ---------------------------------------------------------------- lifecycle
        void Start()
        {
            if (mapRoot == null)
            {
                var map = GetComponentInParent<MapLODDistance>();
                mapRoot = map ? map.transform : transform.parent;
            }
            BuildVisuals();
            StartCoroutine(UpdateLoop());
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var p in parks)
            {
                if (!p.show) continue;
                float target = p.capacityMW > 0 ? maxRotorSpeed * Mathf.Clamp01(p.outputMW / p.capacityMW) : 0f;
                p.currentSpin = Mathf.MoveTowards(p.currentSpin, target, spinAcceleration * dt);
                if (p.currentDir < 0f) p.currentDir = p.windDirection;
                p.currentDir = Mathf.MoveTowardsAngle(p.currentDir, p.windDirection, yawSpeed * dt);
                foreach (var t in p.turbines)
                {
                    if (t == null) continue;
                    t.rotorSpeed = p.currentSpin;
                    t.SetWindDirection(p.currentDir);
                }
            }
        }

        IEnumerator UpdateLoop()
        {
            while (true)
            {
                yield return Refresh();
                yield return new WaitForSeconds(refreshSeconds);
            }
        }

        // ---------------------------------------------------------------- visuals
        void BuildVisuals()
        {
            var placedLabels = new List<Vector3>();
            foreach (var p in parks)
            {
                if (!p.show) continue;
                int n = p.capacityMW >= 300f ? 3 : p.capacityMW >= 150f ? 2 : 1;
                Vector2 c = DenmarkMapProjection.LatLonToBoard(p.latitude, p.longitude);
                // keep the whole cluster on the board (Horns Rev and Kriegers Flak lie just off its edges)
                float m = 0.02f + turbineSpacing;
                var b = DenmarkMapProjection.Board;
                bool clamped = c.x < b.xMin + m || c.x > b.xMax - m || c.y < b.yMin + m || c.y > b.yMax - m;
                c.x = Mathf.Clamp(c.x, b.xMin + m, b.xMax - m);
                c.y = Mathf.Clamp(c.y, b.yMin + m, b.yMax - m);

                var parkRoot = new GameObject("Park_" + p.name).transform;
                parkRoot.SetParent(transform, false);
                parkRoot.position = mapRoot.TransformPoint(DenmarkMapProjection.BoardToLocal(c, 0f));

                Vector2[] offs = n == 1 ? new[] { Vector2.zero }
                    : n == 2 ? new[] { new Vector2(0, 0.5f), new Vector2(0, -0.5f) }
                    : new[] { new Vector2(0, 0.58f), new Vector2(-0.5f, -0.29f), new Vector2(0.5f, -0.29f) };
                foreach (var o in offs)
                {
                    Vector3 local = DenmarkMapProjection.BoardToLocal(c + o * turbineSpacing, 0f);   // water surface
                    var go = Instantiate(turbinePrefab, parkRoot);
                    go.transform.position = mapRoot.TransformPoint(local);
                    go.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(mapRoot.forward, mapRoot.up), mapRoot.up);
                    go.transform.localScale = Vector3.one * turbineScale;
                    var wt = go.GetComponent<WindTurbine>();
                    if (wt != null) { wt.rotorSpeed = 0f; p.turbines.Add(wt); }
                }

                // label above the park, raised if it would overlap an earlier one
                Vector3 lp = parkRoot.position + mapRoot.up * labelHeight;
                foreach (var other in placedLabels)
                    if (Vector3.ProjectOnPlane(lp - other, mapRoot.up).magnitude < 0.09f && Mathf.Abs(Vector3.Dot(lp - other, mapRoot.up)) < 0.03f)
                        lp += mapRoot.up * 0.032f;
                placedLabels.Add(lp);
                p.label = WindParkLabel.Create(parkRoot, lp, labelScale, font, p.name + (clamped ? "  ›" : ""));
                p.label.SetLoading();
            }
        }

        // ---------------------------------------------------------------- data
        [Serializable] class OMCurrent { public float wind_speed_100m; public float wind_direction_100m; }
        [Serializable] class OMItem { public OMCurrent current; }
        [Serializable] class OMList { public OMItem[] items; }
        [Serializable] class ProdexRec { public string Minutes5UTC; public string PriceArea; public float OffshoreWindPower; }
        [Serializable] class ProdexResp { public ProdexRec[] records; }
        [Serializable] class PsrnRec { public string Minutes1UTC; public string Minutes1DK; public float OffshoreWindPower; }
        [Serializable] class PsrnResp { public PsrnRec[] records; }

        IEnumerator Get(string url, Action<string> onText)
        {
            using (var r = UnityWebRequest.Get(url))
            {
                r.timeout = 20;
                yield return r.SendWebRequest();
                if (r.result == UnityWebRequest.Result.Success) onText(r.downloadHandler.text);
                else { status = "error: " + r.error + " (" + url.Split('?')[0] + ")"; onText(null); }
            }
        }

        IEnumerator Refresh()
        {
            bool windOk = false, prodOk = false;
            string lats = string.Join(",", parks.Select(p => p.latitude.ToString("F4", Inv)));
            string lons = string.Join(",", parks.Select(p => p.longitude.ToString("F4", Inv)));
            yield return Get($"https://api.open-meteo.com/v1/forecast?latitude={lats}&longitude={lons}&current=wind_speed_100m,wind_direction_100m&wind_speed_unit=ms",
                txt =>
                {
                    if (txt == null) return;
                    if (txt.TrimStart().StartsWith("{")) txt = "[" + txt + "]";
                    var list = JsonUtility.FromJson<OMList>("{\"items\":" + txt + "}");
                    if (list?.items == null || list.items.Length != parks.Count) return;
                    for (int i = 0; i < parks.Count; i++)
                    {
                        parks[i].windSpeed = list.items[i].current.wind_speed_100m;
                        parks[i].windDirection = list.items[i].current.wind_direction_100m;
                    }
                    windOk = true;
                });

            float dk1 = -1, dk2 = -1, total = -1; string stamp = null;
            yield return Get("https://api.energidataservice.dk/dataset/ElectricityProdex5MinRealtime?limit=6&sort=Minutes5UTC%20DESC", txt =>
            {
                if (txt == null) return;
                var resp = JsonUtility.FromJson<ProdexResp>(txt);
                foreach (var rec in resp.records)
                {
                    if (rec.PriceArea == "DK1" && dk1 < 0) dk1 = rec.OffshoreWindPower;
                    if (rec.PriceArea == "DK2" && dk2 < 0) dk2 = rec.OffshoreWindPower;
                }
            });
            yield return Get("https://api.energidataservice.dk/dataset/PowerSystemRightNow?limit=1&sort=Minutes1UTC%20DESC", txt =>
            {
                if (txt == null) return;
                var resp = JsonUtility.FromJson<PsrnResp>(txt);
                if (resp.records != null && resp.records.Length > 0) { total = resp.records[0].OffshoreWindPower; stamp = resp.records[0].Minutes1DK; }
            });
            if (dk1 >= 0 && dk2 >= 0)
            {
                // use the 1-minute national total, split by the latest 5-minute price-area shares
                if (total > 0 && dk1 + dk2 > 1f) { float k = total / (dk1 + dk2); dk1 *= k; dk2 *= k; }
                dk1OffshoreMW = dk1; dk2OffshoreMW = dk2; dkOffshoreTotalMW = dk1 + dk2;
                prodOk = true;
            }

            if (windOk)
            {
                foreach (var p in parks) p.estimateMW = p.capacityMW * PowerCurve(p.windSpeed);
                if (prodOk)
                {
                    Calibrate(PriceArea.DK1, dk1OffshoreMW);
                    Calibrate(PriceArea.DK2, dk2OffshoreMW);
                }
                else foreach (var p in parks) p.outputMW = p.estimateMW;
                foreach (var p in parks)
                    if (p.show && p.label != null) p.label.SetValues(p.outputMW, p.capacityMW, p.windSpeed, p.windDirection);
                lastUpdateLocal = stamp ?? DateTime.Now.ToString("HH:mm");
                status = prodOk ? "ok – calibrated to Energinet offshore totals" : "ok – wind only (production feed unavailable)";
            }
            else if (!status.StartsWith("error")) status = "no wind data";
        }

        void Calibrate(PriceArea area, float measuredMW)
        {
            var inArea = parks.Where(p => p.area == area).ToList();
            float est = inArea.Sum(p => p.estimateMW);
            float cap = inArea.Sum(p => p.capacityMW);
            foreach (var p in inArea)
            {
                float v = est > 1f ? p.estimateMW * measuredMW / est : (cap > 0 ? p.capacityMW * measuredMW / cap : 0f);
                p.outputMW = Mathf.Clamp(v, 0f, p.capacityMW);
            }
        }

        /// <summary>Generic normalised power curve for a modern offshore turbine (hub-height wind in m/s -> 0..1).</summary>
        public static float PowerCurve(float v)
        {
            const float cutIn = 3.5f, rated = 12.5f, cutOut = 25f;
            if (v < cutIn || v >= cutOut) return 0f;
            if (v >= rated) return 1f;
            float x = (v - cutIn) / (rated - cutIn);
            return x * x * (3f - 2f * x);
        }
    }

    /// <summary>Small world-space panel above a wind park that always faces the camera.</summary>
    public class WindParkLabel : MonoBehaviour
    {
        TextMeshProUGUI _title, _value, _sub;

        public static WindParkLabel Create(Transform parent, Vector3 worldPos, float scale, TMP_FontAsset font, string title)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * scale / Mathf.Max(parent.lossyScale.x, 1e-6f);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(150, 62);
            rt.pivot = new Vector2(0.5f, 0f);

            var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            Stretch((RectTransform)bg.transform);
            bg.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.16f, 0.82f);

            var l = go.AddComponent<WindParkLabel>();
            l._title = MakeText(go.transform, font, 12, FontStyles.Normal, new Vector2(0, 44), 16, new Color(0.85f, 0.92f, 1f));
            l._value = MakeText(go.transform, font, 24, FontStyles.Bold, new Vector2(0, 16), 28, Color.white);
            l._sub = MakeText(go.transform, font, 10, FontStyles.Normal, new Vector2(0, 3), 14, new Color(0.75f, 0.85f, 0.9f));
            l._title.text = title;
            return l;
        }

        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        static TextMeshProUGUI MakeText(Transform parent, TMP_FontAsset font, float size, FontStyles style, Vector2 pos, float h, Color c)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0);
            r.anchoredPosition = pos; r.sizeDelta = new Vector2(-8, h);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.fontStyle = style; t.color = c;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        public void SetLoading() { _value.text = "…"; _sub.text = "loading live data"; }

        /// <summary>Free text for other data layers (e.g. municipality consumption).</summary>
        public void SetCustom(string value, string sub) { _value.text = value; _sub.text = sub; }

        public void SetValues(float mw, float capacity, float windSpeed, float windDir)
        {
            _value.text = mw >= 100f ? $"{mw:0} MW" : $"{mw:0.0} MW";
            int pct = capacity > 0 ? Mathf.RoundToInt(100f * mw / capacity) : 0;
            _sub.text = $"{pct}% of {capacity:0} MW · {windSpeed:0} m/s {Compass(windDir)} · est.";
        }

        static string Compass(float deg)
        {
            string[] d = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return d[Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / 45f) % 8];
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 up = transform.parent ? transform.parent.up : Vector3.up;
            Vector3 dir = Vector3.ProjectOnPlane(transform.position - cam.transform.position, up);
            if (dir.sqrMagnitude > 1e-8f) transform.rotation = Quaternion.LookRotation(dir, up);
        }
    }
}
