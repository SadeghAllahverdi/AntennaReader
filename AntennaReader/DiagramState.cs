using AntennaReader.Models;
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
        #region Attributes
        public Point? StartPoint { get; set; }
        public Point? EndPoint { get; set; }
        public Dictionary<int, (double, Point)> Measurements { get; set; }
        public bool IsLocked { get; set; }
        public DrawingCanvasSetting Setting { get; set; }
        #endregion

        #region Constructor 1
        /// <summary>
        /// Default constructor
        /// </summary>
        public DiagramState()
        {
            this.StartPoint = null;
            this.EndPoint = null;
            this.Measurements = new Dictionary<int, (double, Point)>();
            this.IsLocked = false;
            this.Setting = new DrawingCanvasSetting();
        }
        #endregion

        #region Constructor 2
        /// <summary>
        /// Constructor with measurements and settings as copy, used for undo/redo and saving/loading
        /// </summary>
        public DiagramState(Point? startPoint, Point? endPoint, Dictionary<int, (double, Point)> measurements, bool isLocked, DrawingCanvasSetting setting)
        {
            this.StartPoint = startPoint;
            this.EndPoint = endPoint;
            this.Measurements = new Dictionary<int, (double, Point)>(measurements);
            this.IsLocked = isLocked;
            this.Setting = setting.Clone();
        }
        #endregion
    }
}