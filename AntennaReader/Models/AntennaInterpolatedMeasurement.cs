using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace AntennaReader.Models
{
    /// <summary>
    /// Represents an original user-clicked point (before baking/interpolation).
    /// </summary>
    public class AntennaInterpolatedMeasurement
    {
        public int Id { get; set; }
        public int AntennaDiagramId { get; set; }
        public AntennaDiagram Diagram { get; set; } = null!;

        public int Angle { get; set; }
        public double DbValue { get; set; }
    }
}