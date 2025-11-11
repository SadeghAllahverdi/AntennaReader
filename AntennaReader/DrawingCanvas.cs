using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AntennaReader
{
    public class DrawingCanvas : Canvas
    {
        #region States
        private bool _isDrawing = false;    // draw state
        private bool _isMoving = false;     // move state
        private bool _isResizing = false;   // resize state
        private bool _isLocked = false;     // locked state
        public bool IsLocked { get=> _isLocked; set=> _isLocked = value; } // property
        #endregion

        #region Attributes
        private BitmapImage? _backgroundImage = null;   // background image
        private double _backgroundRotation = 0.0;       // background rotation

        private Point? _startPoint = null;              // start point for draw
        private Point? _endPoint = null;                // end point for draw

        private Point? _moveStartPoint = null;          // start point for move
        private Point? _moveEndPoint = null;            // end point for move

        private string _resizeDirection = "";           // resize direction
        private Point _origin = new Point(0.0, 0.0);    // origne of the diagram
        private double _zoomFactor = 1.0;               // zoom factor

        private List<int> _contours = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 10, 15, 20, 25, 30 };    // contour levels in dB

        public Dictionary<int, (double, Point)> measurements = new Dictionary<int, (double, Point)>(); // dictionary to store points
        #endregion

        #region Constructor 
        /// <summary>
        /// Initializes the canvas Object, sets default background and subscribes to mouse and keyboard events
        /// </summary>
        public DrawingCanvas()
        {
            this.Focusable = true;                 // enable keyboard focus
            this.Background = Brushes.DarkGray;    // set default background color
            // subscribe to mouse and key event handlers
            this.MouseLeftButtonDown += DrawingCanvas_MouseLeftButtonDown; // left click
            this.MouseLeftButtonUp += DrawingCanvas_MouseLeftButtonUp;     // left release
            this.MouseMove += DrawingCanvas_MouseMove;   // move
            this.MouseWheel += DrawingCanvas_MouseWheel; // wheel
            this.KeyDown += DrawingCanvas_KeyDown;       // keyboard button push
        }
        #endregion

        #region Event Handler (Mouse Move)
        /// <summary>
        /// handles mouse move events for draw, move , resize states.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point pos = this._GetPositions(e.GetPosition(this)); // get position of the event
            // determine the current state
            // 1. if not move or draw or resize -> set cursor icon
            if (!this._isMoving && !this._isDrawing && !this._isResizing) 
            {
                this._SetCursorIcon(pos);
                return;
            }
            // 1. resize -> call helper function for resize -> update visuals
            if (this._isResizing)
            {
                this.Resize(pos);
                this.InvalidateVisual();
            }
            // 2. if move -> call helper function for resize -> update visuals
            if (this._isMoving)
            {
                this.Move(pos);
                this.InvalidateVisual();
            }
            // 1. if draw -> call helper function for draw -> update visuals
            if (this._isDrawing)
            {
                this.Draw(pos);
                this.InvalidateVisual();
            }
        }
        #endregion

        #region Event Handler (Mouse Left Release)
        /// <summary>
        /// handles when mouse left button is released -> reset draw, move, resize so that mouse move does not affect anything.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DrawingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this._isDrawing = false;    // reset draw state
            this._isMoving = false;     // reset move state
            this._isResizing = false;   // reset resize state
            this._resizeDirection = ""; // reset resize direction
        }
        #endregion

        #region Event Handler (Mouse Left Click)
        /// <summary>
        /// handels when mouse left button is clicked -> determine operation (measure, resize, move, draw) and set states so 
        /// that mouse move can perform the correct operation.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point pos = this._GetPositions(e.GetPosition(this)); // get position of the event
            // determine the correct operation
            // 1. if diagram is locked -> measure angle, dbValue and position
            if (this._isLocked)
            {
                (int closestAngle, double dbValue, Point point)? result = this.MeasurePoint(pos);
                if (result != null) // if measurement is valid -> store it and update visuals
                {
                    int angle = result.Value.closestAngle;
                    double dbValue = result.Value.dbValue;
                    Point point = result.Value.point;
                    this.measurements[angle] = (dbValue, point);
                    this.InvalidateVisual();
                    return;
                }
            }
            // 2. if position of the event is near an edge-> set state: resize
            string rd = this._GetResizeDirection(pos);
            if (rd != "")
            {
                this._isDrawing = false;
                this._isMoving = false;
                this._isResizing = true;
                this._resizeDirection = rd;
            }
            // 2. if position of the event is inside the ellipse -> set state: move
            if (this._IsInsideEllipse(pos))
            {
                this._isDrawing = false;
                this._isResizing = false;
                this._isMoving = true;
                this._moveStartPoint = pos;
            }
            // 3. if there is no start point -> set state: draw and set start point to the event position
            else if (this._startPoint == null)
            {
                this._isMoving = false;
                this._isResizing = false;
                this._isDrawing = true;
                this._startPoint = pos;
            }
        }
        #endregion

        #region Event Handler (Mouse Wheel)
        /// <summary>
        /// handels when mouse wheel is scrolled -> zoom in or out based on the scroll direction
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DrawingCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // check if the diagram is defined
            if (this._startPoint == null || this._endPoint == null)
            {
                return;
            }

            Point pos = e.GetPosition(this); // get the position of the event

            double delta = e.Delta > 0 ? 1.1 : 0.9; // determine zoom direction
            double zf = this._zoomFactor; // save current zoom factor

            // calculate the position based on the current zoom factor and origin
            double x = (pos.X - this._origin.X) / zf;
            double y = (pos.Y - this._origin.Y) / zf;

            // update zoom factor
            this._zoomFactor *= delta; 
            this._zoomFactor = Math.Clamp(this._zoomFactor, 0.1, 12.0); // clamp zoom factor to a range

            // update origin
            double ox = pos.X - x * this._zoomFactor; 
            double oy = pos.Y - y * this._zoomFactor;
            this._origin = new Point(ox, oy);

            this.InvalidateVisual();
        }
        #endregion

        #region Event Handler (Key Down)
        private void DrawingCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            // check if diagram is locked
            if (!this._isLocked)
            {
                return;
            }
            // rotate background counter clockwise 
            if (e.Key == Key.Q)
            {
                this._backgroundRotation -= 1.0;
                InvalidateVisual();
            }
            // rotate background clockwise
            else if (e.Key == Key.E)
            {
                this._backgroundRotation += 1.0;
                InvalidateVisual();
            }
        }
        #endregion

        #region Render
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            dc.PushTransform(new TranslateTransform(this._origin.X, this._origin.Y));
            dc.PushTransform(new ScaleTransform(this._zoomFactor, this._zoomFactor));

            // draw background image if available
            if (this._backgroundImage != null)
            {
                double bgWidth = this._backgroundImage.PixelWidth;
                double bgHeight = this._backgroundImage.PixelHeight;

                dc.PushTransform(new TranslateTransform(bgWidth / 2, bgHeight / 2));
                dc.PushTransform(new RotateTransform(this._backgroundRotation));
                dc.PushTransform(new TranslateTransform(-bgWidth / 2, -bgHeight / 2));

                dc.DrawImage(this._backgroundImage, new Rect(0, 0, bgWidth, bgHeight));

                dc.Pop();
                dc.Pop();
                dc.Pop();
            }

            if (this._startPoint == null || this._endPoint == null)
            {
                dc.Pop();
                dc.Pop();
                return;
            }

            Rect rect = new Rect(this._startPoint.Value, this._endPoint.Value);
            Point center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            EllipseGeometry ellipse = new EllipseGeometry(center, rect.Width / 2, rect.Height / 2);

            dc.DrawRectangle(null, new Pen(Brushes.Red, 2), rect);
            dc.DrawGeometry(null, new Pen(Brushes.Blue, 2), ellipse);

            for (int deg = 0; deg < 360; deg += 10)
            {
                double lx = center.X + (rect.Width / 2) * Math.Cos((deg - 90) * Math.PI / 180.0);
                double ly = center.Y + (rect.Height / 2) * Math.Sin((deg - 90) * Math.PI / 180.0);
                dc.DrawLine(new Pen(Brushes.Gray, 1), center, new Point(lx, ly));

                FormattedText text = new FormattedText(
                    $"{deg}°",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    6,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip
                );

                double tx = center.X + (rect.Width / 2) * 1.1 * Math.Cos((deg - 90) * Math.PI / 180.0);
                double ty = center.Y + (rect.Height / 2) * 1.1 * Math.Sin((deg - 90) * Math.PI / 180.0);
                dc.DrawText(text, new Point(tx - text.Width / 2, ty - text.Height / 2));
            }

            foreach (int cr in this._contours)
            {
                double rx = (rect.Width / 2) * Math.Pow(10, -cr / 20.0);
                double ry = (rect.Height / 2) * Math.Pow(10, -cr / 20.0);

                EllipseGeometry contourEllipse = new EllipseGeometry(center, rx, ry);
                dc.DrawGeometry(null, new Pen(Brushes.LightGray, 1), contourEllipse);

                FormattedText text = new FormattedText(
                    $"{cr}°",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    3,
                    Brushes.Red,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip
                );

                double tx = center.X + rx;
                double ty = center.Y;
                dc.DrawText(text, new Point(tx - text.Width / 2, ty - text.Height / 2));
            }

            foreach (KeyValuePair<int, (double, Point)> entry in this.measurements)
            {
                int angle = entry.Key;
                double dbValue = entry.Value.Item1;
                Point point = entry.Value.Item2;
                dc.DrawEllipse(Brushes.Yellow, new Pen(Brushes.Orange, 1), point, 2, 2);
            }

            if (this.measurements.Count > 1)
            {
                List<int> angles = this.measurements.Keys.OrderBy(a => a).ToList();
                for (int i = 0; i < angles.Count - 1; i++)
                {
                    int angle1 = angles[i];
                    Point p1 = this.measurements[angle1].Item2;

                    int angle2 = angles[i + 1];
                    Point p2 = this.measurements[angle2].Item2;

                    dc.DrawLine(new Pen(Brushes.Orange, 1), p1, p2);
                }
                if (angles.Count == 36)
                {
                    Point firstPoint = this.measurements[angles[0]].Item2;
                    Point lastPoint = this.measurements[angles[angles.Count - 1]].Item2;
                    dc.DrawLine(new Pen(Brushes.Orange, 1), lastPoint, firstPoint);
                }
            }

            dc.Pop();
            dc.Pop();
        }
        #endregion

        #region Helper Function (Get Positions)
        private Point _GetPositions(Point pos)
        {
            double x = (pos.X - this._origin.X) / this._zoomFactor;
            double y = (pos.Y - this._origin.Y) / this._zoomFactor;
            return new Point(x, y);
        }
        #endregion

        #region Helper Function (is Mouse Inside Ellipse)
        private bool _IsInsideEllipse(Point pos)
        {
            if (this._startPoint == null || this._endPoint == null)
            {
                return false;
            }
            Rect rect = new Rect(this._startPoint.Value, this._endPoint.Value);
            Point center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            EllipseGeometry ellipse = new EllipseGeometry(center, rect.Width / 2, rect.Height / 2);
            return ellipse.FillContains(pos);
        }
        #endregion

        #region Helper Function (Get Resize Direction)
        private string _GetResizeDirection(Point pos)
        {
            if (this._startPoint == null || this._endPoint == null)
            {
                return "";
            }
            Rect rect = new Rect(this._startPoint.Value, this._endPoint.Value);
            const double threshold = 10.0;

            if (pos.Y >= rect.Top && pos.Y <= rect.Bottom)
            {
                if (Math.Abs(pos.X - rect.Left) <= threshold)
                {
                    return "Left";
                }
                if (Math.Abs(pos.X - rect.Right) <= threshold)
                {
                    return "Right";
                }
            }
            if (pos.X >= rect.Left && pos.X <= rect.Right)
            {
                if (Math.Abs(pos.Y - rect.Bottom) <= threshold)
                {
                    return "Bottom";
                }
                if (Math.Abs(pos.Y - rect.Top) <= threshold)
                {
                    return "Top";
                }
            }
            return "";
        }
        #endregion

        #region Helper Function (Set Cursor Icon)
        private void _SetCursorIcon(Point pos)
        {
            if (this._isLocked)
            {
                this.Cursor = Cursors.Arrow;
                return;
            }
            string edge = this._GetResizeDirection(pos);
            if (edge == "Left" || edge == "Right")
            {
                this.Cursor = Cursors.SizeWE;
            }
            else if (edge == "Bottom" || edge == "Top")
            {
                this.Cursor = Cursors.SizeNS;
            }
            else if (this._IsInsideEllipse(pos))
            {
                this.Cursor = Cursors.SizeAll;
            }
            else
            {
                this.Cursor = Cursors.Arrow;
            }
        }
        #endregion

        #region Helper Function (Set Background Image)
        public void SetBackgroundImage(string filePath)
        {
            this._backgroundImage = new BitmapImage(new Uri(filePath, UriKind.Absolute));
            InvalidateVisual();
        }
        #endregion

        #region Helper Function (Draw)
        private void Draw(Point pos)
        {
            if (this._startPoint == null)
            {
                return;
            }
            this._endPoint = pos;
        }
        #endregion

        #region Helper Function (Move)
        private void Move(Point pos)
        {
            if (this._moveStartPoint == null || this._startPoint == null || this._endPoint == null)
            {
                return;
            }
            double deltaX = pos.X - this._moveStartPoint.Value.X;
            double deltaY = pos.Y - this._moveStartPoint.Value.Y;

            this._startPoint = new Point(this._startPoint.Value.X + deltaX, this._startPoint.Value.Y + deltaY);
            this._endPoint = new Point(this._endPoint.Value.X + deltaX, this._endPoint.Value.Y + deltaY);

            this._moveStartPoint = pos;
            this._UpdateMeasurements();
        }
        #endregion

        #region Helper Function (Resize)
        private void Resize(Point pos)
        {
            if (this._startPoint == null || this._endPoint == null || string.IsNullOrEmpty(this._resizeDirection))
            {
                return;
            }
            Point start = this._startPoint.Value;
            Point end = this._endPoint.Value;

            if (this._resizeDirection == "Left")
            {
                start = new Point(pos.X, start.Y);
            }
            else if (this._resizeDirection == "Right")
            {
                end = new Point(pos.X, end.Y);
            }
            else if (this._resizeDirection == "Top")
            {
                start = new Point(start.X, pos.Y);
            }
            else if (this._resizeDirection == "Bottom")
            {
                end = new Point(end.X, pos.Y);
            }

            this._startPoint = start;
            this._endPoint = end;
            this._UpdateMeasurements();
        }
        #endregion

        #region Helper Function (Delete Diagram)
        public void DeleteDiagram()
        {
            this._isLocked = false;
            this._isDrawing = false;
            this._isMoving = false;
            this._isResizing = false;

            this._startPoint = null;
            this._endPoint = null;
            this._moveStartPoint = null;
            this._moveEndPoint = null;
            this.measurements.Clear();

            this._resizeDirection = "";

            this.InvalidateVisual();
        }
        #endregion

        #region Helper Function (Delete Background Image)
        public void DeleteBackgroundImage()
        {
            this._backgroundImage = null;
            this._backgroundRotation = 0.0;
            InvalidateVisual();
        }
        #endregion

        #region Helper Function (Delete Measurments)
        public void DeleteMeasurements()
        {
            this.measurements.Clear();
            InvalidateVisual();
        }
        #endregion

        #region Helper Function (Measure Point)
        private (int closestAngle, double dbValue, Point point)? MeasurePoint(Point pos)
        {
            if (this._startPoint == null || this._endPoint == null)
            {
                return null;
            }
            Rect rect = new Rect(this._startPoint.Value, this._endPoint.Value);
            Point center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

            double x = (pos.X - center.X) / (rect.Width / 2);
            double y = (pos.Y - center.Y) / (rect.Height / 2);
            double r = Math.Sqrt(x * x + y * y);

            double lowestDiviation = double.PositiveInfinity;
            int closestAngle = 0;
            if (r != 0)
            {
                for (int a = 0; a <= 360; a += 10)
                {
                    int angle = a;
                    double angleRad = (angle - 90) * Math.PI / 180.0;
                    double dotProduct = x * Math.Cos(angleRad) + y * Math.Sin(angleRad);

                    if (dotProduct < 0)
                    {
                        angle = (angle + 180) % 360;
                        dotProduct = -dotProduct;
                    }

                    double deviation = 1 - Math.Abs(dotProduct / r);
                    if (deviation < lowestDiviation)
                    {
                        lowestDiviation = deviation;
                        closestAngle = angle;
                    }
                }
            }

            double dbValue = 30.0;
            if (r > 0)
            {
                dbValue = Math.Max(0, Math.Min(-20 * Math.Log10(r), 30));
            }

            double rad = (closestAngle - 90) * Math.PI / 180.0;
            double linear = Math.Pow(10, -dbValue / 20);
            double px = center.X + (rect.Width / 2) * linear * Math.Cos(rad);
            double py = center.Y + (rect.Height / 2) * linear * Math.Sin(rad);
            Point point = new Point(px, py);

            return (closestAngle, dbValue, point);
        }
        #endregion

        #region Helper Function (Update Measurments)
        private void _UpdateMeasurements()
        {
            if (this._startPoint == null || this._endPoint == null)
            {
                return;
            }

            Dictionary<int, (double, Point)> updatedMeasurements = new Dictionary<int, (double, Point)>();
            Rect rect = new Rect(this._startPoint.Value, this._endPoint.Value);
            Point center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

            foreach (KeyValuePair<int, (double, Point)> entry in this.measurements)
            {
                int angle = entry.Key;
                double dbValue = entry.Value.Item1;

                double rad = (angle - 90) * Math.PI / 180.0;
                double linear = Math.Pow(10, -dbValue / 20);
                double px = center.X + (rect.Width / 2) * linear * Math.Cos(rad);
                double py = center.Y + (rect.Height / 2) * linear * Math.Sin(rad);

                Point point = new Point(px, py);

                updatedMeasurements[angle] = (dbValue, point);
            }
            this.measurements = updatedMeasurements;
        }
        #endregion
    }
}