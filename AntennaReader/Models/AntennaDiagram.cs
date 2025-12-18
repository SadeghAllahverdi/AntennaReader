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

        // 1. Antenna name -> antannaCode_stationName
        public string AntennaName { get; set; } = string.Empty;
        
        // 2. Antenna Owner
        public string AntennaOwner { get; set; } = string.Empty;
        // 3. State 
        public string State { get; set; } = string.Empty;

        // 4. City
        public string City { get; set; } = string.Empty;
        // 5. creation at
        public DateTime CreateDate { get; set; }
        // 6. list of measurements
        public List<AntennaMeasurement> Measurements { get; set; } = new List<AntennaMeasurement>();

    }
}
