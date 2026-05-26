using System;
using System.Collections.Generic;
using System.Linq;

namespace AntennaReader.Models
{
    /// <summary>
    /// A diagram setting. Stored in DB, also used as runtime config by DrawingCanvas.
    /// </summary>
    public class DrawingCanvasSetting
    {
        #region Attributes
        public static readonly IReadOnlyList<double> DefaultLogContours =
            new double[] { 1, 2, 3, 4, 5, 6, 8, 10, 15, 20, 25, 30 };

        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsLogScale { get; set; } = true;
        public double lowerBound { get; set; } = 0.0;
        public double upperBound { get; set; } = 30.0;
        public double ContourStep { get; set; } = 2.0;
        public int CsvExportPrecision { get; set; } = 3;
        public int PATExportPrecision { get; set; } = 3;
        public DateTime LastModified { get; set; } = DateTime.Now;

        // --- Advanced Tuning (Danger Zone) ---

        // 1. BOTH (Image Pipeline)
        private int _imageSaturationThreshold = 40; // Color Ignorer: Filters out colored background grids. (Both)
        public int ImageSaturationThreshold
        {
            get => _imageSaturationThreshold;
            set => _imageSaturationThreshold = Math.Max(0, Math.Min(255, value));
        }
        private int _imageDarkThreshold = 100; // Ink Filter: How dark the drawn line must be. (Both)
        public int ImageDarkThreshold
        {
            get => _imageDarkThreshold;
            set => _imageDarkThreshold = Math.Max(0, Math.Min(255, value));
        }
        private double _centerDeadzonePercent = 0.10; // Center Blindspot: Ignores the center crosshairs (10%). (Both)
        public double CenterDeadzonePercent
        {
            get => _centerDeadzonePercent;
            set => _centerDeadzonePercent = Math.Max(0.0, Math.Min(0.90, value));
        }
        private int _preBlurKernelSize = 5; // Grid Eraser: Melts away thin background grid lines. (Both)
        public int PreBlurKernelSize
        {
            get => _preBlurKernelSize;
            set
            {
                int safeValue = Math.Max(1, Math.Min(31, value));
                // If the number is even (like 4), add 1 to make it odd (5)
                if (safeValue % 2 == 0) safeValue += 1;
                _preBlurKernelSize = safeValue;
            }
        }

        // 2. DP (Dynamic Programming)
        private double _dpEpsilon = 1.8; //shape Precision: lower = more points/smoother. Higher = fewer points/blockier. (DP)
        public double DpEpsilon
        {
            get => _dpEpsilon;
            set => _dpEpsilon = Math.Max(0.1, Math.Min(20.0, value));
        }
        private int _dpSyncInterval = 30; //shape Integrity: A safety checkpoint that forces a coordinate lock every X degrees. (DP)
        public int DpSyncInterval
        {
            get => _dpSyncInterval;
            set => _dpSyncInterval = Math.Max(1, Math.Min(90, value));
        }
        private int _dpMaxShift = 4; // how many of the previous pixels have influence (DP)
        public int DpMaxShift
        {
            get => _dpMaxShift;
            set => _dpMaxShift = Math.Max(1, Math.Min(20, value));
        }

        // 3. FA (Fourier Algorithm)
        private int _fourierHarmonics = 10; // how easy the line turns. Too low = jagged. Too high = overfitting and noise. (FA)
        public int FourierHarmonics
        {
            get => _fourierHarmonics;
            set => _fourierHarmonics = Math.Max(2, Math.Min(30, value));
        }
        private double _faVariance = 20.0; // line Focus: How tightly it follows dark ink vs. blurring it out. (FA)
        public double FaVariance
        {
            get => _faVariance;
            set => _faVariance = Math.Max(1.0, Math.Min(100.0, value));
        }
        #endregion

        #region Helper -> Get Contours
        /// <summary>
        /// returns the contour ring values to draw
        /// </summary>
        public List<double> GetContours()
        {
            if (IsLogScale)
            {
                return DefaultLogContours
                    .Where(contour => contour >= lowerBound && contour <= upperBound)
                    .ToList();
            }

            List<double> contours = new List<double>();
            if (ContourStep <= 0) return contours;

            double firstContour = lowerBound + ContourStep;
            for (double contour = firstContour; contour < upperBound; contour += ContourStep)
            {
                contours.Add(Math.Round(contour, 2));
            }
            contours.Add(upperBound);
            return contours;
        }
        #endregion

        #region Helper -> Valid Contour Steps
        /// <summary>
        /// returns the list of valid contour step values for the current dB range
        /// </summary>
        public List<double> GetValidContourSteps()
        {
            double minContour = IsLogScale ? 0.0 : lowerBound;
            double range = upperBound - minContour;
            double[] candidates = { 0.5, 1, 2, 2.5, 5, 10, 15, 20, 25 };
            return candidates
                .Where(s =>
                {
                    double n = range / s;
                    return n >= 3 && n <= 20;
                })
                .ToList();
        }
        #endregion

        #region Helper -> Clone
        /// <summary>
        /// independent copy used for cancel-revert and undo/redo
        /// </summary>
        public DrawingCanvasSetting Clone()
        {
            return new DrawingCanvasSetting
            {
                Id = this.Id,
                Name = this.Name,
                IsLogScale = this.IsLogScale,
                lowerBound = this.lowerBound,
                upperBound = this.upperBound,
                ContourStep = this.ContourStep,
                CsvExportPrecision = this.CsvExportPrecision,
                PATExportPrecision = this.PATExportPrecision,
                LastModified = this.LastModified,

                ImageSaturationThreshold = this.ImageSaturationThreshold,
                ImageDarkThreshold = this.ImageDarkThreshold,
                CenterDeadzonePercent = this.CenterDeadzonePercent,
                PreBlurKernelSize = this.PreBlurKernelSize,
                DpEpsilon = this.DpEpsilon,
                DpSyncInterval = this.DpSyncInterval,
                DpMaxShift = this.DpMaxShift,
                FourierHarmonics = this.FourierHarmonics,
                FaVariance = this.FaVariance
            };
        }
        #endregion
    }
}