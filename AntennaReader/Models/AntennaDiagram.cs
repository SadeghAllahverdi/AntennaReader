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

        // 1. station name
        public string StationName { get; set; } = string.Empty;
        // 2. State 
        public string State { get; set; } = string.Empty;

        // 3. City
        public string City { get; set; } = string.Empty;
        // 2. creation at
        public DateTime CreateDate { get; set; }
        // 3. list of measurements
        public List<AntennaMeasurement> Measurements { get; set; } = new List<AntennaMeasurement>();

    }
}
