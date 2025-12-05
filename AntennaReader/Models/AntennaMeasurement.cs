using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AntennaReader.Models
{
    /// <summary>
    /// Represents a single measurement from an antenna diagram.
    /// </summary>
    public class AntennaMeasurement
    {
        
       
        public int Id { get; set; }  // primary key
        public int AntennaDiagramId { get; set; } // foreign key
        public AntennaDiagram? Diagram { get; set; }

        //properties
        // 1. angle
        public int Angle { get; set; }
        // 2. db value
        public double DbValue { get; set; }
        // 3. position
        public double PosX { get; set; }
        public double PosY { get; set; }
    }
}
