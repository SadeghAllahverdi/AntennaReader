using System;
using System.IO;
using System.Windows;
using AntennaReader.Infrastructure;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
namespace AntennaReader.Services
{
    public class CostMapData
    {
        public byte[] CostMapValues { get; set; } = Array.Empty<byte>();
        public int Angles { get; set; }
        public int Rads { get; set; }
        public double DiagramMaxRadius { get; set; }
        public double DiagramCenterX { get; set; }
        public double DiagramCenterY { get; set; }
        public Mat? OriginalImage { get; set; }
        public Mat? PolarImage { get; set; }
    }

    public static class ImageProcessingPipeline
    {
        public static CostMapData GenerateCostMap(DrawingCanvas canvas, bool enableDebugOutput = false)
        {
            // preconditions
            if (!canvas.HasBackgroundImage || !canvas.HasDiagram || !canvas.IsLocked)
            {
                throw new InvalidOperationException("Canvas needs: a loaded image, a drawn diagram rectangle, and the diagram must be locked.");
            }

            // read image and apply background rotation if needed
            using Mat sourceImage = BitmapSourceConverter.ToMat(canvas.BackgroundImage!);
            System.Diagnostics.Debug.WriteLine($"source image: channels: {sourceImage.Channels()}, type: {sourceImage.Type()}, depth: {sourceImage.Depth()}");
            Mat workingImage = new Mat();

            if (Math.Abs(canvas.BackgroundRotation) > 0.1)
            {
                Point2f centerPt = new Point2f(sourceImage.Width / 2f, sourceImage.Height / 2f);
                using Mat rotMatrix = Cv2.GetRotationMatrix2D(centerPt, -canvas.BackgroundRotation, 1.0);
                Cv2.WarpAffine(sourceImage, workingImage, rotMatrix, sourceImage.Size());
            }
            else
            {
                sourceImage.CopyTo(workingImage);
            }
            // debug output
            if (enableDebugOutput)
            {
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "read_image.png"), workingImage);
            }
            // polar transform
            double diagramCenterX = canvas.DiagramCenter!.Value.X - canvas.BackgroundDrawX;
            double diagramCenterY = canvas.DiagramCenter!.Value.Y - canvas.BackgroundDrawY;
            double diagramMaxRadius = Math.Max(canvas.DiagramRadiusX, canvas.DiagramRadiusY);
            Point2f diagramCenter = new Point2f((float)diagramCenterX, (float)diagramCenterY);

            int angles = 360;
            int rads = (int)diagramMaxRadius;
            Mat polarImage = new Mat();

            Cv2.WarpPolar(
                src: workingImage,
                dst: polarImage,
                dsize: new OpenCvSharp.Size(rads, angles),
                center: diagramCenter,
                maxRadius: diagramMaxRadius,
                interpolationFlags: InterpolationFlags.Linear,
                warpPolarMode: WarpPolarMode.Linear
            );
            
            System.Diagnostics.Debug.WriteLine($"polar image: channels: {polarImage.Channels()}, type: {polarImage.Type()}, depth: {polarImage.Depth()}");

            // debug output
            if (enableDebugOutput)
            {
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "applied_polar_transform.png"), polarImage);
            }

            // change image to HSV
            using Mat hsvImage = new Mat();
            Cv2.CvtColor(polarImage, hsvImage, ColorConversionCodes.BGR2HSV);
            System.Diagnostics.Debug.WriteLine($"polar image: channels: {polarImage.Channels()}, type: {polarImage.Type()}, depth: {polarImage.Depth()}");
            Mat[] hsvChannels = Cv2.Split(hsvImage);
            using Mat s = hsvChannels[1];
            using Mat v = hsvChannels[2];

            using Mat saturatedMask = new Mat(); // high saturated pixels
            Cv2.Threshold(s, saturatedMask, canvas.Setting.ImageSaturationThreshold, 255, ThresholdTypes.Binary);

            using Mat darkMask = new Mat(); // low value pixels
            Cv2.Threshold(v, darkMask, canvas.Setting.ImageDarkThreshold, 255, ThresholdTypes.BinaryInv);

            using Mat combinedMask = new Mat();
            Cv2.BitwiseOr(saturatedMask, darkMask, combinedMask);
            // cost map
            using Mat costMap = new Mat();
            Cv2.BitwiseNot(combinedMask, costMap);
            Cv2.GaussianBlur(costMap, costMap, new OpenCvSharp.Size(5, 5), 0);
            if (enableDebugOutput) Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "cost_map.png"), costMap);

            byte[] costMapValues = new byte[angles * rads];
            System.Runtime.InteropServices.Marshal.Copy(costMap.Data, costMapValues, 0, costMapValues.Length);

            // Ignoring inner 10% 
            int ignoredInnerRadius = (int)(diagramMaxRadius * 0.1);
            for (int angle = 0; angle < angles; angle++)
            {
                int rowStartIndex = angle * rads;
                for (int rad = 0; rad < ignoredInnerRadius; rad++)
                {
                    costMapValues[rowStartIndex + rad] = 255;
                }
            }

            if (enableDebugOutput)
            {
                using Mat costMapWithIgnoredCenter = new Mat(angles, rads, MatType.CV_8UC1);
                System.Runtime.InteropServices.Marshal.Copy(costMapValues, 0, costMapWithIgnoredCenter.Data, costMapValues.Length);
                Cv2.ImWrite(Path.Combine(AppPaths.DebugFolder, "cost_map_with_dead_zone.png"), costMapWithIgnoredCenter);
            }

            foreach (Mat channel in hsvChannels)
            {
                channel.Dispose();
            }


            return new CostMapData
            {
                CostMapValues = costMapValues,
                Angles = angles,
                Rads = rads,
                DiagramMaxRadius = diagramMaxRadius,
                DiagramCenterX = diagramCenterX,
                DiagramCenterY = diagramCenterY,
                OriginalImage = workingImage,
                PolarImage = polarImage
            };
        }
    }
}