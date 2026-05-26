using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using AntennaReader.Infrastructure;
using OpenCvSharp;
using Point = System.Windows.Point;

namespace AntennaReader.Services
{
    public static class DiagramDetectionServiceDP
    {
        public static Dictionary<int, double> ExtractCurve(DrawingCanvas canvas, bool enableDebugOutput = false)
        {
            // get preprcessed data
            CostMapData data = ImageProcessingPipeline.GenerateCostMap(canvas, enableDebugOutput);

            // Dynamic Programming Pathfinding (Sun & Pallottino algorithm)
            double[,] cheapestPathCosts = new double[data.Angles, data.Rads];
            int[,] parent = new int[data.Angles, data.Rads];
            int[,] ancestor = new int[data.Angles, data.Rads];

            for (int rad = 0; rad < data.Rads; rad++)
            {
                cheapestPathCosts[0, rad] = data.CostMapValues[rad];
                ancestor[0, rad] = rad;
            }

            int maxShift = canvas.Setting.DpMaxShift; // Max pixel shift tracking constraint per step angle

            for (int angle = 1; angle < data.Angles; angle++)
            {
                int rowStartIndex = angle * data.Rads;
                for (int rad = 0; rad < data.Rads; rad++)
                {
                    double bestPreviousCost = double.PositiveInfinity;
                    int bestPreviousRad = rad;

                    for (int shift = -maxShift; shift <= maxShift; shift++)
                    {
                        int prevRad = rad + shift;
                        if (prevRad < 0 || prevRad >= data.Rads) continue;

                        if (cheapestPathCosts[angle - 1, prevRad] < bestPreviousCost)
                        {
                            bestPreviousCost = cheapestPathCosts[angle - 1, prevRad];
                            bestPreviousRad = prevRad;
                        }
                    }

                    cheapestPathCosts[angle, rad] = data.CostMapValues[rowStartIndex + rad] + bestPreviousCost;
                    parent[angle, rad] = bestPreviousRad;
                    ancestor[angle, rad] = ancestor[angle - 1, bestPreviousRad];
                }
            }

            // find winning path
            double winningPathCost = double.PositiveInfinity;
            int winningEndRad = -1;

            for (int rad = 0; rad < data.Rads; rad++)
            {
                int pathAncestorRad = ancestor[data.Angles - 1, rad];
                if (Math.Abs(pathAncestorRad - rad) <= maxShift * 2)
                {
                    if (cheapestPathCosts[data.Angles - 1, rad] < winningPathCost)
                    {
                        winningPathCost = cheapestPathCosts[data.Angles - 1, rad];
                        winningEndRad = rad;
                    }
                }
            }
            // fall back if no path meets criteria 
            if (winningEndRad == -1)
            {
                double cheapestEnd = double.PositiveInfinity;
                for (int r = 0; r < data.Rads; r++)
                {
                    if (cheapestPathCosts[data.Angles - 1, r] < cheapestEnd)
                    {
                        cheapestEnd = cheapestPathCosts[data.Angles - 1, r];
                        winningEndRad = r;
                    }
                }
            }
            // Reconstruct the 360-degree path
            int[] optimalPath = new int[data.Angles];
            int currentR = winningEndRad;
            for (int a = data.Angles - 1; a >= 0; a--)
            {
                optimalPath[a] = currentR;
                currentR = parent[a, currentR];
            }

            if (enableDebugOutput && data.PolarImage != null && data.OriginalImage != null)
            {
                using Mat polarWithPath = data.PolarImage.Clone();
                for (int a = 0; a < data.Angles; a++)
                {
                    Cv2.Circle(polarWithPath, new OpenCvSharp.Point(optimalPath[a], a), 1, Scalar.Red, -1);
                }
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "detected_path.png"), polarWithPath);
                System.Diagnostics.Debug.WriteLine($"DP done. Cost = {winningPathCost}, End radius = {winningEndRad}");

                using Mat sourceWithPath = data.OriginalImage.Clone();
                for (int a = 0; a < data.Angles; a++)
                {
                    double radAngle = a * Math.PI / 180.0;
                    double pixelRadius = optimalPath[a];
                    int px = (int)(data.DiagramCenterX + pixelRadius * Math.Cos(radAngle));
                    int py = (int)(data.DiagramCenterY + pixelRadius * Math.Sin(radAngle));
                    Cv2.Circle(sourceWithPath, new OpenCvSharp.Point(px, py), 2, Scalar.Red, -1);
                }
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "original_with_detected_path.png"), sourceWithPath);
            }

            // clean resources from memory
            data.PolarImage?.Dispose();
            data.OriginalImage?.Dispose();

            // Ramer-Douglas-Peucker for geometric simplification -> less measurement points -> user friendly.
            List<Point> cartesianPoints = new List<Point>();
            for (int a = 0; a < data.Angles; a++)
            {
                double radAngle = (a * Math.PI) / 180.0;
                double x = optimalPath[a] * Math.Cos(radAngle);
                double y = optimalPath[a] * Math.Sin(radAngle);
                cartesianPoints.Add(new Point(x, y));
            }

            List<bool> keepFlags = new List<bool>(new bool[data.Angles]);
            keepFlags[0] = true;
            keepFlags[data.Angles - 1] = true;

            double epsilon = canvas.Setting.DpEpsilon;
            DouglasPeuckerSimplify(cartesianPoints, 0, data.Angles - 1, epsilon, keepFlags);

            // create a measurment every 5 degrees
            Dictionary<int, double> finalMeasurements = new Dictionary<int, double>();
            System.Diagnostics.Debug.WriteLine($"BackgroundRotation = {canvas.BackgroundRotation}");

            for (int a = 0; a < data.Angles; a++)
            {
                bool forcePoint = (a % canvas.Setting.DpSyncInterval == 0); // Safety sync checkpoint every 30 degrees
                if (keepFlags[a] || forcePoint)
                {
                    double normalizedDistance = (double)optimalPath[a] / data.DiagramMaxRadius;
                    int canvasAngle = (a + 90 + 360) % 360;

                    int snappedAngle = (int)(Math.Round(canvasAngle / 5.0) * 5) % 360;
                    if (snappedAngle < 0) snappedAngle += 360;

                    finalMeasurements[snappedAngle] = canvas.DbFromDistance(normalizedDistance);
                }
            }
            return finalMeasurements;
        }

        #region RDP Simplification Helpers
        private static void DouglasPeuckerSimplify(List<Point> points, int start, int end, double epsilon, List<bool> keepFlags)
        {
            if (end - start <= 1) return;

            double maxDistance = 0;
            int indexFarthest = start;

            for (int i = start + 1; i < end; i++)
            {
                double distance = GetPerpendicularDistance(points[i], points[start], points[end]);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    indexFarthest = i;
                }
            }

            if (maxDistance > epsilon)
            {
                keepFlags[indexFarthest] = true;
                DouglasPeuckerSimplify(points, start, indexFarthest, epsilon, keepFlags);
                DouglasPeuckerSimplify(points, indexFarthest, end, epsilon, keepFlags);
            }
        }

        private static double GetPerpendicularDistance(Point p, Point start, Point end)
        {
            double lineLength = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
            if (lineLength == 0)
            {
                return Math.Sqrt(Math.Pow(p.X - start.X, 2) + Math.Pow(p.Y - start.Y, 2));
            }

            double numerator = Math.Abs((end.Y - start.Y) * p.X - (end.X - start.X) * p.Y + end.X * start.Y - end.Y * start.X);
            return numerator / lineLength;
        }
        #endregion
    }
}