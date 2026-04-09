using System;
using System.Collections.Generic;
using System.Linq;

namespace AntennaReader
{
    public enum InterpolationMode
    {
        PeriodicCubicSpline,
        Linear,
        Monotone,
        CatmullRom,
        Lagrange,
    }

    public static class Interpolator
    {
        #region Interpolator
        /// <summary>
        /// Fills in 360° of interpolated values from the clicked points.
        /// Handles wrap-around by finding the biggest gap and skipping it.
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
                InterpolationMode.PeriodicCubicSpline => InterpolatePeriodicCubicSpline(pts),
                InterpolationMode.Linear => InterpolateLinear(pts),
                InterpolationMode.Monotone => InterpolateMonotone(pts),
                InterpolationMode.CatmullRom => InterpolateCatmullRom(pts),
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

        #region Periodic Cubic Spline Interpolation
        /// <summary>
        /// Solves a system of equations for smooth second derivatives at each point.
        /// Industry standard for antenna patterns. Stable and smooth.
        /// </summary>
        private static Dictionary<int, double> InterpolatePeriodicCubicSpline(List<(double t, double v)> pts)
        {
            var result = new Dictionary<int, double>();
            int n = pts.Count;

            if (n < 3)
                return InterpolateLinear(pts);

            int segments = n - 1;

            // step 1: compute h (spacing) and delta (slope) for each segment
            double[] h = new double[segments];
            double[] delta = new double[segments];
            for (int i = 0; i < segments; i++)
            {
                h[i] = pts[i + 1].t - pts[i].t;
                if (h[i] < 1e-9) h[i] = 1e-9;
                delta[i] = (pts[i + 1].v - pts[i].v) / h[i];
            }

            // step 2: solve for second derivatives M[i]
            double[] M = new double[n];
            M[0] = 0.0;
            M[n - 1] = 0.0;

            if (segments >= 2)
            {
                int size = n - 2;
                double[] sub = new double[size];
                double[] diag = new double[size];
                double[] sup = new double[size];
                double[] rhs = new double[size];

                for (int i = 0; i < size; i++)
                {
                    int pi = i + 1;
                    sub[i] = h[pi - 1];
                    diag[i] = 2.0 * (h[pi - 1] + h[pi]);
                    sup[i] = h[pi];
                    rhs[i] = 6.0 * (delta[pi] - delta[pi - 1]);
                }

                double[] solution = SolveTridiagonal(sub, diag, sup, rhs);
                for (int i = 0; i < size; i++)
                {
                    M[i + 1] = solution[i];
                }
            }

            // step 3: evaluate the spline on each segment
            for (int i = 0; i < segments; i++)
            {
                double t0 = pts[i].t;
                double t1 = pts[i + 1].t;
                double y0 = pts[i].v;
                double y1 = pts[i + 1].v;
                double hi = h[i];
                double M0 = M[i];
                double M1 = M[i + 1];

                for (int deg = (int)t0; deg <= (int)t1; deg++)
                {
                    double x = deg;
                    double a_coeff = (t1 - x) / hi;
                    double b_coeff = (x - t0) / hi;

                    double val = a_coeff * y0
                               + b_coeff * y1
                               + ((a_coeff * a_coeff * a_coeff - a_coeff) * M0
                                + (b_coeff * b_coeff * b_coeff - b_coeff) * M1)
                               * (hi * hi) / 6.0;

                    result[deg] = val;
                }
            }

            return result;
        }

        /// <summary>
        /// Thomas algorithm — solves the tridiagonal system in O(n). Fast and stable.
        /// </summary>
        private static double[] SolveTridiagonal(double[] a, double[] b, double[] c, double[] rhs)
        {
            int n = b.Length;
            if (n == 0) return Array.Empty<double>();
            if (n == 1) return new double[] { rhs[0] / b[0] };

            double[] c_prime = new double[n];
            double[] rhs_prime = new double[n];

            c_prime[0] = c[0] / b[0];
            rhs_prime[0] = rhs[0] / b[0];

            for (int i = 1; i < n; i++)
            {
                double denom = b[i] - a[i] * c_prime[i - 1];
                c_prime[i] = (i < n - 1) ? c[i] / denom : 0.0;
                rhs_prime[i] = (rhs[i] - a[i] * rhs_prime[i - 1]) / denom;
            }

            double[] x = new double[n];
            x[n - 1] = rhs_prime[n - 1];
            for (int i = n - 2; i >= 0; i--)
            {
                x[i] = rhs_prime[i] - c_prime[i] * x[i + 1];
            }

            return x;
        }
        #endregion

        #region Linear Interpolation
        /// <summary>
        /// Straight lines between each pair of points. Simple.
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

        #region Monotone Interpolation (Fritsch-Carlson)
        /// <summary>
        /// Like Catmull-Rom but won't overshoot — keeps the curve tame.
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

        #region Extra Interpolations (Lag, CatMull)
        /// <summary>
        /// Smooth curve through every point using neighbor slopes as tangents.
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
        /// <summary>
        /// One big polynomial through all points. Smooth but can go crazy with many points.
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