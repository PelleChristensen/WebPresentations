using UnityEngine;

namespace DenmarkMap
{
    /// <summary>
    /// Converts WGS84 latitude/longitude (GPS) to positions on the Denmark table map.
    /// Matches the projection the map was built with: spherical Lambert azimuthal equal-area,
    /// scale 1:155,000, 20x vertical exaggeration. Bornholm/Ertholmene (lon &gt; 14°E) go to the inset.
    /// Board space: x = east, y = north (metres, origin at board centre). Use LatLonToLocal for positions
    /// relative to the DenmarkMap root (Y = up, water surface at Y = 0).
    /// </summary>
    public static class DenmarkMapProjection
    {
        public const double EarthRadius = 6371000.0;
        public const double Scale = 155000.0;
        public const float Exaggeration = 20f;
        public const float LandBase = 0.004f;      // sea level on land sits 4 mm above the water

        // main map
        const double Lon0 = 10.4, Lat0 = 56.1, OffX = 2050.0, OffY = 6825.0;
        // Bornholm inset
        const double BLon0 = 14.92, BLat0 = 55.13, BOffX = 1000.0, BOffY = 2850.0;
        const double InsetX = -0.82, InsetY = 1.07;

        public static readonly Rect Board = new Rect(-1.0f, -1.25f, 2.0f, 2.5f);

        /// <summary>True if the coordinate is drawn in the Bornholm inset.</summary>
        public static bool IsInInset(double lat, double lon) => lon > 14.0;

        /// <summary>Lat/lon (degrees) to board coordinates in metres (x = east, y = north).</summary>
        public static Vector2 LatLonToBoard(double lat, double lon)
        {
            double bx, by;
            if (IsInInset(lat, lon))
            {
                Laea(lat, lon, BLat0, BLon0, out var x, out var y);
                bx = (x - BOffX) / Scale + InsetX;
                by = (y - BOffY) / Scale + InsetY;
            }
            else
            {
                Laea(lat, lon, Lat0, Lon0, out var x, out var y);
                bx = (x - OffX) / Scale;
                by = (y - OffY) / Scale;
            }
            return new Vector2((float)bx, (float)by);
        }

        /// <summary>
        /// The tile FBXs import with north along local -Z and east along local -X (the builder then
        /// rotates the DenmarkMap root 180° so north is world +Z). This maps board coordinates into
        /// that imported local frame, so results are always relative to the DenmarkMap root.
        /// </summary>
        public static Vector3 BoardToLocal(Vector2 board, float y) => new Vector3(-board.x, y, -board.y);

        /// <summary>Lat/lon to a local position on the DenmarkMap root. Height from a real elevation in metres.</summary>
        public static Vector3 LatLonToLocal(double lat, double lon, float elevationMetres = 0f)
            => BoardToLocal(LatLonToBoard(lat, lon), ElevationToLocalY(elevationMetres));

        /// <summary>Real-world elevation (m) to local map height on land.</summary>
        public static float ElevationToLocalY(float elevationMetres)
            => LandBase + Mathf.Max(elevationMetres, 0.3f) * Exaggeration / (float)Scale;

        /// <summary>UV0 of the map at a coordinate (for painting data textures).</summary>
        public static Vector2 LatLonToUV(double lat, double lon)
        {
            var b = LatLonToBoard(lat, lon);
            return new Vector2((b.x - Board.xMin) / Board.width, (b.y - Board.yMin) / Board.height);
        }

        static void Laea(double lat, double lon, double lat0, double lon0, out double x, out double y)
        {
            const double d2r = System.Math.PI / 180.0;
            double p = lat * d2r, l = (lon - lon0) * d2r, p0 = lat0 * d2r;
            double sp = System.Math.Sin(p), cp = System.Math.Cos(p), sp0 = System.Math.Sin(p0), cp0 = System.Math.Cos(p0), cl = System.Math.Cos(l);
            double k = System.Math.Sqrt(2.0 / (1.0 + sp0 * sp + cp0 * cp * cl));
            x = EarthRadius * k * cp * System.Math.Sin(l);
            y = EarthRadius * k * (cp0 * sp - sp0 * cp * cl);
        }
    }
}
