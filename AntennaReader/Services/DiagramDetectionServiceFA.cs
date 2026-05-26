using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using AntennaReader.Infrastructure;
using OpenCvSharp;
using Point = System.Windows.Point;
namespace AntennaReader.Services
{
    public static class DiagramDetectionServiceFA
    {
        public static Dictionary<int, double> ExtractCurve(DrawingCanvas canvas, bool enableDebugOutput = false)
        {
            // 1. Get the pre-processed cost map data from the master pipeline
            CostMapData data = ImageProcessingPipeline.GenerateCostMap(canvas, enableDebugOutput);

            int ignoredInnerRadiusOfDiagram = (int)(data.DiagramMaxRadius * canvas.Setting.CenterDeadzonePercent);

            // 4. Iteratively Reweighted Least Squares (IRLS) Fourier Fit
            int[] fittedPath = new int[data.Angles];
            int numHarmonics = canvas.Setting.FourierHarmonics;
            int irlsIterations = 3;

            for (int iter = 0; iter < irlsIterations; iter++)
            {
                double[] candidateRadii = new double[data.Angles];
                double[] candidateWeights = new double[data.Angles];

                // iterate over each angle and compute weighted average.
                for (int angle = 0; angle < data.Angles; angle++)
                {
                    int rowStartIndex = angle * data.Rads;
                    double weightedRadiusSum = 0;   // for each angle, the sum over (how dark the pixels are x thier radius)
                    double weightSum = 0;           // for each angle the sum over how dark pixels are.

                    for (int rad = ignoredInnerRadiusOfDiagram; rad < data.Rads; rad++)
                    {
                        double weight = 255 - data.CostMapValues[rowStartIndex + rad];

                        if (iter > 0)
                        {
                            double dist = Math.Abs(rad - fittedPath[angle]);
                            double variance = canvas.Setting.FaVariance;
                            weight *= Math.Exp(-(dist * dist) / (2.0 * variance * variance));
                        }

                        weightedRadiusSum += weight * rad;
                        weightSum += weight;
                    }

                    if (weightSum > 0)
                    {
                        candidateRadii[angle] = weightedRadiusSum / weightSum; // for each angle candidate pixel radius
                        candidateWeights[angle] = weightSum;                   // for each angle candidate pixel darkness
                    }
                    else
                    {
                        candidateRadii[angle] = iter > 0 ? fittedPath[angle] : data.DiagramMaxRadius / 2.0;
                    }
                }

                double[] cosineCoeffs = new double[numHarmonics + 1];
                double[] sineCoeffs = new double[numHarmonics + 1];

                double radiusSum = 0;
                for (int a = 0; a < data.Angles; a++) radiusSum += candidateRadii[a];
                cosineCoeffs[0] = radiusSum / data.Angles;

                for (int n = 1; n <= numHarmonics; n++)
                {
                    double cosineSum = 0;
                    double sineSum = 0;
                    for (int a = 0; a < data.Angles; a++)
                    {
                        double theta = a * Math.PI / 180.0;
                        cosineSum += candidateRadii[a] * Math.Cos(n * theta);
                        sineSum += candidateRadii[a] * Math.Sin(n * theta);
                    }
                    cosineCoeffs[n] = (2.0 / data.Angles) * cosineSum;
                    sineCoeffs[n] = (2.0 / data.Angles) * sineSum;
                }

                // evaluate the Fourier series for the next pass
                for (int a = 0; a < data.Angles; a++)
                {
                    double theta = a * Math.PI / 180.0;
                    double radius = cosineCoeffs[0];
                    for (int n = 1; n <= numHarmonics; n++)
                    {
                        radius += cosineCoeffs[n] * Math.Cos(n * theta) + sineCoeffs[n] * Math.Sin(n * theta);
                    }
                    fittedPath[a] = (int)Math.Round(radius);

                    if (fittedPath[a] < 0) fittedPath[a] = 0;
                    if (fittedPath[a] >= data.Rads) fittedPath[a] = data.Rads - 1;
                }
            }

            if (enableDebugOutput && data.PolarImage != null && data.OriginalImage != null)
            {
                using Mat polarWithFit = data.PolarImage.Clone();
                for (int a = 0; a < data.Angles; a++)
                {
                    Cv2.Circle(polarWithFit, new OpenCvSharp.Point(fittedPath[a], a), 1, Scalar.Red, -1);
                }
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "fourier_fit.png"), polarWithFit);

                using Mat sourceWithFit = data.OriginalImage.Clone();
                for (int a = 0; a < data.Angles; a++)
                {
                    double radAngle = a * Math.PI / 180.0;
                    double pixelRadius = fittedPath[a];
                    int px = (int)(data.DiagramCenterX + pixelRadius * Math.Cos(radAngle));
                    int py = (int)(data.DiagramCenterY + pixelRadius * Math.Sin(radAngle));
                    Cv2.Circle(sourceWithFit, new OpenCvSharp.Point(px, py), 2, Scalar.Red, -1);
                }
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "original_with_fourier_fit.png"), sourceWithFit);
            }

            // clean up resources from memory
            data.PolarImage?.Dispose();
            data.OriginalImage?.Dispose();

            // convert smooth fitted path to measurements with strict 5 deg snap
            Dictionary<int, double> finalMeasurements = new Dictionary<int, double>();

            for (int a = 0; a < data.Angles; a++)
            {
                if (a % 5 == 0)
                {
                    double normalizedDistance = (double)fittedPath[a] / data.DiagramMaxRadius;
                    int canvasAngle = (a + 90) % 360;

                    finalMeasurements[canvasAngle] = canvas.DbFromDistance(normalizedDistance);
                }
            }

            return finalMeasurements;
        }
    }
}