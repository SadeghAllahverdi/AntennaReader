using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using AntennaReader.Infrastructure;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Point = System.Windows.Point;

namespace AntennaReader.Services
{
    public static class DiagramDetectionService
    {
        public static Dictionary<int, double> ExtractCurve(DrawingCanvas canvas, bool enableDebugOutput = false)
        {
            if (!canvas.HasBackgroundImage || !canvas.HasDiagram || !canvas.IsLocked)
            {
                throw new InvalidOperationException(
                    "Canvas needs: a loaded image, a drawn diagram rectangle, and the diagram must be locked.");
            }

            // 1. Read image and handle background rotation transformations
            using Mat sourceImage = BitmapSourceConverter.ToMat(canvas.BackgroundImage!);
            using Mat workingImage = new Mat();

            if (Math.Abs(canvas.BackgroundRotation) > 0.1)
            {
                Point2f centerPt = new Point2f(sourceImage.Width / 2f, sourceImage.Height / 2f);
                using Mat rotMatrix = Cv2.GetRotationMatrix2D(centerPt, canvas.BackgroundRotation, 1.0);
                Cv2.WarpAffine(sourceImage, workingImage, rotMatrix, sourceImage.Size());
            }
            else
            {
                sourceImage.CopyTo(workingImage);
            }

            if (enableDebugOutput) Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "01_image_read_success.png"), workingImage);

            // 2. Polar transform: polar diagram to rectangular image
            double diagramCenterX = canvas.DiagramCenter!.Value.X - canvas.BackgroundDrawX;
            double diagramCenterY = canvas.DiagramCenter!.Value.Y - canvas.BackgroundDrawY;
            double diagramMaxRadius = Math.Max(canvas.DiagramRadiusX, canvas.DiagramRadiusY);
            Point2f diagramCenter = new Point2f((float)diagramCenterX, (float)diagramCenterY);

            int polarWidth = (int)diagramMaxRadius;
            int polarHeight = 360;
            using Mat polarImage = new Mat();
            Cv2.WarpPolar(
                src: workingImage,
                dst: polarImage,
                dsize: new OpenCvSharp.Size(polarWidth, polarHeight),
                center: diagramCenter,
                maxRadius: diagramMaxRadius,
                interpolationFlags: InterpolationFlags.Linear,
                warpPolarMode: WarpPolarMode.Linear
            );
            if (enableDebugOutput) Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "02_polar_transform_success.png"), polarImage);

            // 3. Cost map generation
            using Mat hsvImage = new Mat();
            Cv2.CvtColor(polarImage, hsvImage, ColorConversionCodes.BGR2HSV);
            Mat[] hsvChannels = Cv2.Split(hsvImage);
            using Mat h = hsvChannels[0];
            using Mat s = hsvChannels[1];
            using Mat v = hsvChannels[2];

            if (enableDebugOutput)
            {
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "03a_hue_channel.png"), h);
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "03b_saturation_channel.png"), s);
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "03c_value_channel.png"), v);
            }

            using Mat saturatedMask = new Mat(); // high saturated pixels
            Cv2.Threshold(s, saturatedMask, 40, 255, ThresholdTypes.Binary);

            using Mat darkMask = new Mat(); // low value pixels
            Cv2.Threshold(v, darkMask, 100, 255, ThresholdTypes.BinaryInv);

            if (enableDebugOutput)
            {
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "03d_color_mask.png"), saturatedMask);
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "03e_dark_mask.png"), darkMask);
            }

            // Combine masks
            using Mat combinedMask = new Mat();
            Cv2.BitwiseOr(saturatedMask, darkMask, combinedMask);
            if (enableDebugOutput) Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "03f_combined_mask.png"), combinedMask);

            using Mat costMap = new Mat();
            Cv2.BitwiseNot(combinedMask, costMap);
            Cv2.GaussianBlur(costMap, costMap, new OpenCvSharp.Size(5, 5), 0);
            if (enableDebugOutput) Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "03g_cost_map.png"), costMap);

            int angles = costMap.Height;
            int rads = costMap.Width;
            byte[] costMapValues = new byte[angles * rads];
            System.Runtime.InteropServices.Marshal.Copy(costMap.Data, costMapValues, 0, costMapValues.Length);

            // Ignoring inner 10% of diagram to avoid noise near center crosshairs
            int ignoredInnerRadiusOfDiagram = (int)(diagramMaxRadius * 0.1);
            for (int angle = 0; angle < angles; angle++)
            {
                int rowStartIndex = angle * rads;
                for (int rad = 0; rad < ignoredInnerRadiusOfDiagram; rad++)
                {
                    costMapValues[rowStartIndex + rad] = 255;
                }
            }

            if (enableDebugOutput)
            {
                using Mat costMapWithIgnoredCenter = new Mat(angles, rads, MatType.CV_8UC1);
                System.Runtime.InteropServices.Marshal.Copy(costMapValues, 0, costMapWithIgnoredCenter.Data, costMapValues.Length);
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "04_cost_map_with_dead_zone.png"), costMapWithIgnoredCenter);
            }

            foreach (Mat channel in hsvChannels)
            {
                channel.Dispose();
            }

            // 4. Dynamic Programming Pathfinding (Sun & Pallottino algorithm)
            double[,] cheapestPathCosts = new double[angles, rads];
            int[,] parent = new int[angles, rads];
            int[,] ancestor = new int[angles, rads];

            for (int rad = 0; rad < rads; rad++)
            {
                cheapestPathCosts[0, rad] = costMapValues[rad];
                ancestor[0, rad] = rad;
            }

            int maxShift = 4; // Max pixel shift tracking constraint per step angle

            for (int angle = 1; angle < angles; angle++)
            {
                int rowStartIndex = angle * rads;
                for (int rad = 0; rad < rads; rad++)
                {
                    double bestPreviousCost = double.PositiveInfinity;
                    int bestPreviousRad = rad;

                    for (int shift = -maxShift; shift <= maxShift; shift++)
                    {
                        int prevRad = rad + shift;
                        if (prevRad < 0 || prevRad >= rads) continue;

                        if (cheapestPathCosts[angle - 1, prevRad] < bestPreviousCost)
                        {
                            bestPreviousCost = cheapestPathCosts[angle - 1, prevRad];
                            bestPreviousRad = prevRad;
                        }
                    }

                    cheapestPathCosts[angle, rad] = costMapValues[rowStartIndex + rad] + bestPreviousCost;
                    parent[angle, rad] = bestPreviousRad;
                    ancestor[angle, rad] = ancestor[angle - 1, bestPreviousRad];
                }
            }

            // Find winning path
            double winningPathCost = double.PositiveInfinity;
            int winningEndRad = -1;

            for (int rad = 0; rad < rads; rad++)
            {
                int pathAncestorRad = ancestor[angles - 1, rad];
                if (Math.Abs(pathAncestorRad - rad) <= maxShift * 2)
                {
                    if (cheapestPathCosts[angles - 1, rad] < winningPathCost)
                    {
                        winningPathCost = cheapestPathCosts[angles - 1, rad];
                        winningEndRad = rad;
                    }
                }
            }

            if (winningEndRad == -1)
            {
                double cheapestEnd = double.PositiveInfinity;
                for (int r = 0; r < rads; r++)
                {
                    if (cheapestPathCosts[angles - 1, r] < cheapestEnd)
                    {
                        cheapestEnd = cheapestPathCosts[angles - 1, r];
                        winningEndRad = r;
                    }
                }
            }

            // Reconstruct the 360-degree path
            int[] optimalPath = new int[angles];
            int currentR = winningEndRad;
            for (int a = angles - 1; a >= 0; a--)
            {
                optimalPath[a] = currentR;
                currentR = parent[a, currentR];
            }

            if (enableDebugOutput)
            {
                using Mat polarWithPath = polarImage.Clone();
                for (int a = 0; a < angles; a++)
                {
                    Cv2.Circle(polarWithPath, new OpenCvSharp.Point(optimalPath[a], a), 1, Scalar.Red, -1);
                }
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "05_polar_with_detected_path.png"), polarWithPath);
                System.Diagnostics.Debug.WriteLine($"DP done. Cost = {winningPathCost}, End radius = {winningEndRad}");

                using Mat sourceWithPath = workingImage.Clone(); // Uses the properly rotated workingImage map
                for (int a = 0; a < angles; a++)
                {
                    double radAngle = a * Math.PI / 180.0;
                    double pixelRadius = optimalPath[a];
                    int px = (int)(diagramCenterX + pixelRadius * Math.Cos(radAngle));
                    int py = (int)(diagramCenterY + pixelRadius * Math.Sin(radAngle));
                    Cv2.Circle(sourceWithPath, new OpenCvSharp.Point(px, py), 2, Scalar.Red, -1);
                }
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "06_original_with_detected_path.png"), sourceWithPath);
            }

            // 5. Adaptive geometric simplification (Ramer-Douglas-Peucker)
            List<Point> cartesianPoints = new List<Point>();
            for (int a = 0; a < angles; a++)
            {
                double radAngle = (a * Math.PI) / 180.0;
                double x = optimalPath[a] * Math.Cos(radAngle);
                double y = optimalPath[a] * Math.Sin(radAngle);
                cartesianPoints.Add(new Point(x, y));
            }

            List<bool> keepFlags = new List<bool>(new bool[angles]);
            keepFlags[0] = true;
            keepFlags[angles - 1] = true;

            double epsilon = 1.8;
            DouglasPeuckerSimplify(cartesianPoints, 0, angles - 1, epsilon, keepFlags);

            // 6. Convert simplified key-points to measurements with strict 5 deg snap
            Dictionary<int, double> finalMeasurements = new Dictionary<int, double>();

            for (int a = 0; a < angles; a++)
            {
                bool forcePoint = (a % 30 == 0); // Safety sync checkpoint every 30 degrees

                if (keepFlags[a] || forcePoint)
                {
                    double normalizedDistance = (double)optimalPath[a] / diagramMaxRadius;
                    int canvasAngle = (a + 90) % 360;

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