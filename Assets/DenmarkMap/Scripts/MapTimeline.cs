using System;
using System.Globalization;
using UnityEngine;

namespace DenmarkMap
{
    /// <summary>
    /// Shared "what time are we showing" for all data layers on the map.
    /// Live = newest data each source has. Historical = a chosen Danish local date/time.
    /// Data visualisers listen to <see cref="Changed"/> and reload. (A VR date/time picker can later just set
    /// <see cref="SetHistorical"/> / <see cref="SetLive"/>.)
    /// </summary>
    public class MapTimeline : MonoBehaviour
    {
        public enum Mode { Live, Historical }

        public Mode mode = Mode.Live;
        [Tooltip("Danish local time, format yyyy-MM-dd HH:mm. Used when Mode = Historical.")]
        public string historicalTimeDK = "2026-09-28 18:00";

        public event Action Changed;

        static MapTimeline _instance;
        public static MapTimeline Instance => _instance != null ? _instance : (_instance = FindFirstObjectByType<MapTimeline>());

        public bool IsLive => mode == Mode.Live;

        /// <summary>Selected time as Danish local time (only meaningful when not live).</summary>
        public DateTime SelectedDK
        {
            get
            {
                if (DateTime.TryParseExact(historicalTimeDK, new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd" },
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return t;
                return DateTime.Now;
            }
        }

        public void SetLive() { mode = Mode.Live; Changed?.Invoke(); }

        public void SetHistorical(DateTime danishLocalTime)
        {
            mode = Mode.Historical;
            historicalTimeDK = danishLocalTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            Changed?.Invoke();
        }

        void OnEnable() => _instance = this;

        void OnValidate()
        {
            if (Application.isPlaying) Changed?.Invoke();
        }
    }
}
