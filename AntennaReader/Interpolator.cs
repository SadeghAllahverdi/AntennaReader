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
        BSpline
    }

    public static class SplineInterpolator
    {
        // ─────────────────────────────────────────────────────────────
        // PUBLIC ENTRY POINT
        //
        // Takes only the clicked points (any number, any angles).
        // Returns a dense dictionary with one dB entry per integer degree,
        // BUT only for degrees that fall BETWEEN consecutive clicked points.
        // Gaps (ranges with no clicked points on both sides) are left empty.
        //
        // Example: clicked 0, 10, 100, 120
        //   → fills degrees 0..10, 10..100, 100..120
        //   → degrees 120..359 are absent (gap)
        // ─────────────────────────────────────────────────────────────

        public static Dictionary<int, double> Interpolate(
            Dictionary<int, double> measurements,
            InterpolationMode mode)
        {
            if (measurements == null || measurements.Count == 0)
                return new Dictionary<int, double>();

            // need at least 2 points to draw anything
            if (measurements.Count < 2)
                return new Dictionary<int, double>();

            List<(double t, double v)> pts = measurements
                .OrderBy(kv => kv.Key)
                .Select(kv => ((double)kv.Key, kv.Value))
                .ToList();

            return mode switch
            {
                InterpolationMode.Linear => InterpolateLinear(pts),
                InterpolationMode.CatmullRom => InterpolateCatmullRom(pts),
                InterpolationMode.Monotone => InterpolateMonotone(pts),
                InterpolationMode.Lagrange => InterpolateLagrange(pts),
                InterpolationMode.BSpline => InterpolateBSpline(pts),


                _ => InterpolateLinear(pts)
            };
        }

        /// <summary>
        /// Same as Interpolate but treats the data as a closed loop —
        /// connects the last point back to the first to fill the gap.
        /// Used only for baking the final result.
        /// </summary>
        public static Dictionary<int, double> InterpolateClosedLoop(
            Dictionary<int, double> measurements,
            InterpolationMode mode)
        {
            if (measurements == null || measurements.Count < 2)
                return new Dictionary<int, double>();

            // append the first point again at angle + 360 to close the loop
            Dictionary<int, double> closed = new Dictionary<int, double>(measurements);
            int firstAngle = measurements.Keys.Min();
            int lastAngle = measurements.Keys.Max();

            // add wrap-around point only if not already a full 360
            if (firstAngle != 0 || lastAngle != 350 || measurements.Count != 36)
            {
                closed[360] = measurements[firstAngle];
            }

            List<(double t, double v)> pts = closed
                .OrderBy(kv => kv.Key)
                .Select(kv => ((double)kv.Key, kv.Value))
                .ToList();

            // run chosen method — result will cover 0..359 fully
            var result = mode switch
            {
                InterpolationMode.Linear => InterpolateLinear(pts),
                InterpolationMode.CatmullRom => InterpolateCatmullRom(pts),
                InterpolationMode.Monotone => InterpolateMonotone(pts),
                InterpolationMode.Lagrange => InterpolateLagrange(pts),
                _ => InterpolateLinear(pts)
            };

            // clamp all results to valid degree range 0..359
            return result
                .Where(kv => kv.Key >= 0 && kv.Key <= 359)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        // ─────────────────────────────────────────────────────────────
        // LINEAR
        // Straight line between each pair of consecutive clicked points.
        // ─────────────────────────────────────────────────────────────

        private static Dictionary<int, double> InterpolateLinear(List<(double t, double v)> pts)
        {
            var result = new Dictionary<int, double>();
            int n = pts.Count;

            // only iterate over the n-1 segments between consecutive clicked points
            // do NOT wrap around from last to first (that would fill the gap)
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

        // ─────────────────────────────────────────────────────────────
        // CATMULL-ROM  (Cubic Hermite with centripetal tangents)
        //
        // Tangent at point i:
        //   m[i] = (v[i+1] - v[i-1]) / 2
        //
        // For endpoints (no neighbor on one side) tangent = 0
        // → mathematically produces a straight line for that segment.
        //
        // Cubic Hermite basis on local u in [0, 1]:
        //   h00 =  2u^3 - 3u^2 + 1
        //   h10 =   u^3 - 2u^2 + u
        //   h01 = -2u^3 + 3u^2
        //   h11 =   u^3 -  u^2
        //
        //   p(u) = h00*p0 + h10*(m0*span) + h01*p1 + h11*(m1*span)
        // ─────────────────────────────────────────────────────────────

        private static Dictionary<int, double> InterpolateCatmullRom(List<(double t, double v)> pts)
        {
            var result = new Dictionary<int, double>();
            int n = pts.Count;

            // compute tangents — endpoints get 0 (no neighbor = straight line)
            double[] m = new double[n];
            for (int i = 0; i < n; i++)
            {
                if (i == 0 || i == n - 1)
                {
                    m[i] = 0.0; // endpoint: tangent = 0 → straight line for that segment
                }
                else
                {
                    m[i] = (pts[i + 1].v - pts[i - 1].v) / 2.0; // interior tangent
                }
            }

            // evaluate each segment between consecutive clicked points only
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

        // ─────────────────────────────────────────────────────────────
        // MONOTONE CUBIC  (Fritsch-Carlson)
        //
        // Same as Catmull-Rom but tangents are constrained so the curve
        // never overshoots between two adjacent points.
        // Perfect for antenna patterns: nulls stay nulls, lobes don't spike.
        //
        // Step 1: secant slopes  d[i] = (v[i+1] - v[i]) / span[i]
        // Step 2: initial tangents same as Catmull-Rom (0 at endpoints)
        // Step 3: Fritsch-Carlson constraint
        //   if d[i] == 0 → m[i] = m[i+1] = 0  (flat segment stays flat)
        //   else:
        //     alpha = m[i]   / d[i]
        //     beta  = m[i+1] / d[i]
        //     if alpha^2 + beta^2 > 9 → scale down both tangents
        // ─────────────────────────────────────────────────────────────

        private static Dictionary<int, double> InterpolateMonotone(List<(double t, double v)> pts)
        {
            var result = new Dictionary<int, double>();
            int n = pts.Count;

            // step 1: secant slopes and spans
            double[] d = new double[n - 1];
            double[] span = new double[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                span[i] = pts[i + 1].t - pts[i].t;
                d[i] = span[i] < 1e-9 ? 0.0 : (pts[i + 1].v - pts[i].v) / span[i];
            }

            // step 2: initial tangents (0 at endpoints)
            double[] m = new double[n];
            m[0] = 0.0;
            m[n - 1] = 0.0;
            for (int i = 1; i < n - 1; i++)
                m[i] = (pts[i + 1].v - pts[i - 1].v) / 2.0;

            // step 3: Fritsch-Carlson monotonicity constraint per segment
            for (int i = 0; i < n - 1; i++)
            {
                if (Math.Abs(d[i]) < 1e-9)
                {
                    // flat segment → force both endpoint tangents to zero
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

            // evaluate segments — identical to Catmull-Rom from here
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

        // ─────────────────────────────────────────────────────────────
        // LAGRANGE
        //
        // Ln,i(t) = prod_{j=0, j!=i}^{n}  (t - t_j) / (t_i - t_j)
        // f(t)    = sum_{i=0}^{n}  v_i * Ln,i(t)
        //
        // Only evaluated within the range of clicked points (no extrapolation).
        // ─────────────────────────────────────────────────────────────

        private static Dictionary<int, double> InterpolateLagrange(List<(double t, double v)> pts)
        {
            var result = new Dictionary<int, double>();
            int n = pts.Count;

            double tMin = pts[0].t;
            double tMax = pts[n - 1].t;

            // only fill degrees within the clicked range
            for (int deg = (int)tMin; deg <= (int)tMax; deg++)
            {
                double tQuery = deg;
                double sum = 0.0;

                for (int i = 0; i < n; i++)
                {
                    // compute L_{n,i}(t)
                    double Li = 1.0;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i) continue;
                        double num = tQuery - pts[j].t; // (t   - t_j)
                        double den = pts[i].t - pts[j].t; // (t_i - t_j)
                        if (Math.Abs(den) < 1e-9) { Li = 0.0; break; }
                        Li *= num / den;
                    }
                    sum += pts[i].v * Li;
                }

                result[deg] = sum;
            }
            return result;

        }

        // ─────────────────────────────────────────────────────────────
        // B-SPLINE (uniform cubic, Cox-de Boor)
        //
        // Approximating — does NOT pass through clicked points.
        // Smoother than Bezier, global influence of control points.
        // ─────────────────────────────────────────────────────────────

        private static Dictionary<int, double> InterpolateBSpline(List<(double t, double v)> pts)
        {
            var result = new Dictionary<int, double>();
            int n = pts.Count;

            if (n < 2) return result;

            // for each segment between consecutive clicked points
            for (int i = 0; i < n - 1; i++)
            {
                double t0 = pts[i].t;
                double t1 = pts[i + 1].t;
                double span = t1 - t0;

                // gather 4 control points (clamp at boundaries)
                double p0 = pts[Math.Max(i - 1, 0)].v;
                double p1 = pts[i].v;
                double p2 = pts[i + 1].v;
                double p3 = pts[Math.Min(i + 2, n - 1)].v;

                for (int deg = (int)t0; deg <= (int)t1; deg++)
                {
                    double u = span < 1e-9 ? 0.0 : (deg - t0) / span;
                    double u2 = u * u;
                    double u3 = u2 * u;

                    // uniform cubic B-spline basis (Cox-de Boor)
                    double b0 = (-u3 + 3 * u2 - 3 * u + 1) / 6.0;
                    double b1 = (3 * u3 - 6 * u2 + 4) / 6.0;
                    double b2 = (-3 * u3 + 3 * u2 + 3 * u + 1) / 6.0;
                    double b3 = u3 / 6.0;

                    result[deg] = b0 * p0 + b1 * p1 + b2 * p2 + b3 * p3;
                }
            }

            return result;
        }
    }
}