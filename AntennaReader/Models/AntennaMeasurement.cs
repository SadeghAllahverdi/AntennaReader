using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AntennaReader.Models
{
    /// <summary>
    /// Represents a single user measurement from an antenna diagram.
    /// </summary>
    public class AntennaMeasurement
    {
        public int Id { get; set; }  // primary key
        public int AntennaDiagramId { get; set; } // foreign key
        public AntennaDiagram Diagram { get; set; } = null!;
        public int Angle { get; set; }
        public double DbValue { get; set; }
        public double PosX { get; set; }
        public double PosY { get; set; }
    }
}
