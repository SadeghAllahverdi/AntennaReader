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
        // canonical default log contours — used when ContourMode is DefaultLog
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

        #region Helper -> Get Contours
        /// <summary>
        /// returns the contour ring values to draw
        /// </summary>
        public List<double> GetContours()
        {
            // for log scale, ignore ContourStep
            if (IsLogScale)
            {
                return DefaultLogContours
                    .Where(contour => contour >= lowerBound && contour <= upperBound)
                    .ToList();
            }
            // for linear
            List<double> contours = new List<double>();
            if (ContourStep <= 0)
            {
                return contours;
            }
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
                LastModified = this.LastModified
            };
        }
        #endregion
    }
}