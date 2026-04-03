using System;
using System.Collections.Generic;
using System.Linq;

namespace AntennaReader
{
    public enum InterpolationMode
    {
        Linear,
        CatmullRom,
        Monotone,
        Lagrange,
    }

    public static class Interpolator
    {
        #region interpolate
        /// <summary>
        /// Takes raw clicked points and fills in the gaps between them with
        /// interpolated values, one per integer degree across the full 360°.
        /// Connects the last point back to the first to close the loop.
        /// Needs at least 2 points to do anything.
        /// </summary>
        public static Dictionary<int, double> Interpolate(
    Dictionary<int, double> rawClickedPoints,
    InterpolationMode mode)
        {
            if (rawClickedPoints == null || rawClickedPoints.Count < 2)
                return new Dictionary<int, double>();

            // 1. sort the angles
            List<int> sortedAngles = rawClickedPoints.Keys.OrderBy(k => k).ToList();
            int n = sortedAngles.Count;

            // 2. find the biggest gap
            int biggestGapIndex = 0;
            int biggestGapSize = 0;
            for (int i = 0; i < n; i++)
            {
                int current = sortedAngles[i];
                int next = sortedAngles[(i + 1) % n];
                int gap = (next - current + 360) % 360;
                if (gap > biggestGapSize)
                {
                    biggestGapSize = gap;
                    biggestGapIndex = i;
                }
            }

            // 3. reorder: start from the point AFTER the biggest gap
            //    and add 360 to any angle that would go below the starting angle
            int startIdx = (biggestGapIndex + 1) % n;
            List<(double t, double v)> pts = new List<(double t, double v)>();
            int baseAngle = sortedAngles[startIdx];

            for (int i = 0; i < n; i++)
            {
                int idx = (startIdx + i) % n;
                int angle = sortedAngles[idx];
                // if angle is less than base, it wrapped around, so add 360
                if (angle < baseAngle)
                    angle += 360;
                pts.Add((angle, rawClickedPoints[sortedAngles[idx]]));
            }

            // 4. close the loop: add first point again at +360
            pts.Add((pts[0].t + 360, pts[0].v));

            // 5. interpolate
            var result = mode switch
            {
                InterpolationMode.Linear => InterpolateLinear(pts),
                InterpolationMode.CatmullRom => InterpolateCatmullRom(pts),
                InterpolationMode.Monotone => InterpolateMonotone(pts),
                InterpolationMode.Lagrange => InterpolateLagrange(pts),
                _ => InterpolateLinear(pts)
            };

            // 6. map everything back to 0-359
            Dictionary<int, double> final = new Dictionary<int, double>();
            foreach (var kv in result)
            {
                int deg = ((kv.Key % 360) + 360) % 360;
                final[deg] = Math.Clamp(kv.Value, 0.0, 30.0);
            }

            return final;
        }
        #endregion

        #region Linear

        /// <summary>
        /// Draws a straight line between each pair of points.
        /// Simple as it gets — just connect the dots.
        /// </summary>
        private static Dictionary<int, double> InterpolateLinear(List<(double t, double v)> pts)
        {
            var result = new Dictionary<int, double>();
            int n = pts.Count;

            for (int i = 0; i < n - 1; i++)
            {
                double t0 = pts[i].t;
                double t1 = pts[i + 1].t;
                double v0 = pts[i].v;
                double v1 = pts[i + 1].v;
                double span = t1 - t0;

                for (int deg = (int)t0; deg <= (int)t1; deg++)
                {
                    double u = span < 1e-9 ? 0.0 : (deg - t0) / span;
                    result[deg] = v0 + u * (v1 - v0);
                }
            }

            return result;
        }

        #endregion

        #region Catmull-Rom

        /// <summary>
        /// Smooth curve that passes through every point.
        /// Uses the slope between the previous and next point as
        /// the tangent at each point, so the curve flows nicely.
        /// Endpoints get a flat tangent (slope = 0) since they
        /// have no neighbor on one side.
        ///
        /// The math uses four blending functions (h00, h10, h01, h11)
        /// to mix the two endpoint values and their tangents together.
        /// </summary>
        private static Dictionary<int, double> InterpolateCatmullRom(List<(double t, double v)> pts)
        {
            var result = new Dictionary<int, double>();
            int n = pts.Count;

            double[] m = new double[n];
            for (int i = 0; i < n; i++)
            {
                if (i == 0 || i == n - 1)
                {
                    m[i] = 0.0;
                }
                else
                {
                    m[i] = (pts[i + 1].v - pts[i - 1].v) / 2.0;
                }
            }

            for (int i = 0; i < n - 1; i++)
            {
                double t0 = pts[i].t;
                double t1 = pts[i + 1].t;
                double p0 = pts[i].v;
                double p1 = pts[i + 1].v;
                double m0 = m[i];
                double m1 = m[i + 1];
                double span = t1 - t0;

                for (int deg = (int)t0; deg <= (int)t1; deg++)
                {
                    double u = span < 1e-9 ? 0.0 : (deg - t0) / span;
                    double u2 = u * u;
                    double u3 = u2 * u;

                    double h00 = 2 * u3 - 3 * u2 + 1;
                    double h10 = u3 - 2 * u2 + u;
                    double h01 = -2 * u3 + 3 * u2;
                    double h11 = u3 - u2;

                    result[deg] = h00 * p0 + h10 * (m0 * span) + h01 * p1 + h11 * (m1 * span);
                }
            }

            return result;
        }

        #endregion

        #region Monotone (Fritsch-Carlson)

        /// <summary>
        /// Like Catmull-Rom but prevents overshooting.
        /// If the data goes up then down, this method won't let the
        /// curve shoot past the peak. It does this by checking each
        /// segment — if the tangents would cause the curve to wiggle
        /// or overshoot, it scales them down to keep things tame.
        ///
        /// Flat segments (same value on both ends) get zero tangents
        /// so the curve stays perfectly flat there.
        /// </summary>
        private static Dictionary<int, double> InterpolateMonotone(List<(double t, double v)> pts)
        {
            var result = new Dictionary<int, double>();
            int n = pts.Count;

            double[] d = new double[n - 1];
            double[] span = new double[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                span[i] = pts[i + 1].t - pts[i].t;
                d[i] = span[i] < 1e-9 ? 0.0 : (pts[i + 1].v - pts[i].v) / span[i];
            }

            double[] m = new double[n];
            m[0] = 0.0;
            m[n - 1] = 0.0;
            for (int i = 1; i < n - 1; i++)
                m[i] = (pts[i + 1].v - pts[i - 1].v) / 2.0;

            for (int i = 0; i < n - 1; i++)
            {
                if (Math.Abs(d[i]) < 1e-9)
                {
                    m[i] = 0.0;
                    m[i + 1] = 0.0;
                    continue;
                }

                double alpha = m[i] / d[i];
                double beta = m[i + 1] / d[i];
                double h = alpha * alpha + beta * beta;

                if (h > 9.0)
                {
                    double scale = 3.0 / Math.Sqrt(h);
                    m[i] = scale * alpha * d[i];
                    m[i + 1] = scale * beta * d[i];
                }
            }

            for (int i = 0; i < n - 1; i++)
            {
                double t0 = pts[i].t;
                double t1 = pts[i + 1].t;
                double p0 = pts[i].v;
                double p1 = pts[i + 1].v;
                double m0 = m[i];
                double m1 = m[i + 1];
                double s = span[i];

                for (int deg = (int)t0; deg <= (int)t1; deg++)
                {
                    double u = s < 1e-9 ? 0.0 : (deg - t0) / s;
                    double u2 = u * u;
                    double u3 = u2 * u;

                    double h00 = 2 * u3 - 3 * u2 + 1;
                    double h10 = u3 - 2 * u2 + u;
                    double h01 = -2 * u3 + 3 * u2;
                    double h11 = u3 - u2;

                    result[deg] = h00 * p0 + h10 * (m0 * s) + h01 * p1 + h11 * (m1 * s);
                }
            }

            return result;
        }

        #endregion

        #region Lagrange

        /// <summary>
        /// Uses ALL points at once to build one big polynomial that
        /// passes through every single point. Smooth but can go crazy
        /// (huge spikes) if you have lots of points — that's just how
        /// high-degree polynomials work. Best with a small number of points.
        ///
        /// For each degree, it calculates a weighted blend of all point
        /// values, where the weights (basis polynomials) are set up so
        /// the curve hits each point exactly.
        /// </summary>
        private static Dictionary<int, double> InterpolateLagrange(List<(double t, double v)> pts)
        {
            var result = new Dictionary<int, double>();
            int n = pts.Count;

            double tMin = pts[0].t;
            double tMax = pts[n - 1].t;

            for (int deg = (int)tMin; deg <= (int)tMax; deg++)
            {
                double tQuery = deg;
                double sum = 0.0;

                for (int i = 0; i < n; i++)
                {
                    double Li = 1.0;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i) continue;
                        double num = tQuery - pts[j].t;
                        double den = pts[i].t - pts[j].t;
                        if (Math.Abs(den) < 1e-9) { Li = 0.0; break; }
                        Li *= num / den;
                    }
                    sum += pts[i].v * Li;
                }

                result[deg] = sum;
            }
            return result;
        }

        #endregion
    }
}