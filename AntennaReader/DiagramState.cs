using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AntennaReader
{
    /// <summary>
    /// Saves the state of the diagram 
    /// </summary>
    public class DiagramState
    {
        public Point? StartPoint { get; set; }
        public Point? EndPoint { get; set; }
        public Dictionary<int, (double, Point)> Measurements { get; set; }
        public bool IsLocked { get; set; }

        public bool IsEqoDistanceMode { get; set; }
        public int EqoDistanceMaxDb { get; set; }

        public DiagramState(Point? startPoint, Point? endPoint, Dictionary<int, (double, Point)> measurements, bool isLocked, bool IsEqoDistanceMode, int EqoDistanceMaxDb) 
        {
            this.StartPoint = startPoint;
            this.EndPoint = endPoint;
            this.Measurements = new Dictionary<int, (double, Point)>(measurements);
            this.IsLocked = isLocked;
            this.IsEqoDistanceMode = IsEqoDistanceMode;
            this.EqoDistanceMaxDb = Math.Max(1, EqoDistanceMaxDb);
        }
        /// <summary>
        /// Defualt constructor
        /// </summary>
        public DiagramState ()
        {             
            this.StartPoint = null;
            this.EndPoint = null;
            this.Measurements = new Dictionary<int, (double, Point)>();
            this.IsLocked = false;
            this.IsEqoDistanceMode = false;
            this.EqoDistanceMaxDb = 30;
        }
    }
}
