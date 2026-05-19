using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntennaReader.Models
{
    /// <summary>
    /// Represents the measuremetns of an antenna diagram.
    /// </summary>
    public class AntennaDiagram
    {
        public int Id { get; set; } // primary key
        public string AntennaName { get; set; } = string.Empty; 
        public string AntennaOwner { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public DateTime CreateDate { get; set; }
        public List<AntennaMeasurement> Measurements { get; set; } = new(); // original user clicked values
        public List<AntennaInterpolatedMeasurement> InterpolatedMeasurements { get; set; } = new(); // interpolated values
    }
}
