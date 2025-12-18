using AntennaReader.Models;
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
        public bool IsLocked { get => _isLocked; set => _isLocked = value; } // property -> is diagram locked?
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

        private bool _isPerformingUndoRedo = false; // are we doing undo-redo?
        public bool HasDiagram => this._startPoint != null && this._endPoint != null;

        public Stack<DiagramState> UndoStack = new Stack<DiagramState>(); // undo stack
        public Stack<DiagramState> RedoStack = new Stack<DiagramState>(); // redo stack
        private const int maxUndoRedoSteps = 30; // maximum undo-redo steps
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
            Point p = e.GetPosition(this);     // get absolute position of the mouse
            Point pos = this._GetPositions(p); // recalculate position based on current origin and zoom factor
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
            }
            // 2. if move -> call helper function for resize -> update visuals
            if (this._isMoving)
            {
                this.Move(pos);
            }
            // 1. if draw -> call helper function for draw -> update visuals
            if (this._isDrawing)
            {
                this.Draw(pos);
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
            Point p = e.GetPosition(this); // get absolute position of the mouse
            Point pos = this._GetPositions(p); // recalculate position based on current origin and zoom factor
            // determine the correct operation
            // 1. if diagram is locked -> measure angle, dbValue and position
            if (this._isLocked)
            {
                (int closestAngle, double dbValue, Point point)? result = this.MeasurePoint(pos);
                if (result != null) // if measurement is valid -> store it and update visuals
                {
                    this._SaveState(); // save state

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
                this._SaveState(); // save state

                this._isDrawing = false;
                this._isMoving = false;
                this._isResizing = true;
                this._resizeDirection = rd;
                return;
            }
            // 2. if position of the event is inside the ellipse -> set state: move
            if (this._IsInsideEllipse(pos))
            {
                this._SaveState(); // save state

                this._isDrawing = false;
                this._isResizing = false;
                this._isMoving = true;
                this._moveStartPoint = pos; // set move start point
                return;
            }
            // 3. if there is no start point -> set state: draw and set start point to the event position
            else if (this._startPoint == null)
            {
                this._SaveState(); // save state

                this._isMoving = false;
                this._isResizing = false;
                this._isDrawing = true;
                this._startPoint = pos; // set start point of diagram
                return;
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

            Point p = e.GetPosition(this); // get absolute position of the mouse

            double delta = e.Delta > 0 ? 1.1 : 0.9; // determine zoom direction
            double zf = this._zoomFactor;           // save current zoom factor

            // recalculate position based on current zoom factor and origin
            Point pos = this._GetPositions(p);

            // update zoom factor
            this._zoomFactor *= delta;
            this._zoomFactor = Math.Clamp(this._zoomFactor, 0.1, 12.0); // clamp zoom factor to a range

            // update origin
            double ox = p.X - pos.X * this._zoomFactor;
            double oy = p.Y - pos.Y * this._zoomFactor;
            this._origin = new Point(ox, oy);

            this.InvalidateVisual();
        }
        #endregion

        #region Event Handler (Key Down rotation)
        /// <summary>
        /// handles when keyboard E or Q is pressed -> rotates background image if diagram is locked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DrawingCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            // check if diagram is defined
            if (this._startPoint == null || this._endPoint == null)
            {
                return;
            }
            // check if diagram is locked
            if (!this._isLocked)
            {
                return;
            }

            bool eventHandled = false;

            // rotate background counter clockwise 
            if (e.Key == Key.Q)
            {
                this._backgroundRotation -= 1.0;
                eventHandled = true;
            }
            // rotate background clockwise
            else if (e.Key == Key.E)
            {
                this._backgroundRotation += 1.0;
                eventHandled = true;
            }

            else if (e.Key == Key.Up)
            {
                this._startPoint = new Point(this._startPoint.Value.X, this._startPoint.Value.Y - 10);
                this._endPoint = new Point(this._endPoint.Value.X, this._endPoint.Value.Y - 10);
                eventHandled = true;
            }

            else if (e.Key == Key.Right)
            {
                this._startPoint = new Point(this._startPoint.Value.X + 10, this._startPoint.Value.Y);
                this._endPoint = new Point(this._endPoint.Value.X + 10, this._endPoint.Value.Y);
                eventHandled = true;
            }

            else if (e.Key == Key.Left)
            {
                this._startPoint = new Point(this._startPoint.Value.X - 10, this._startPoint.Value.Y);
                this._endPoint = new Point(this._endPoint.Value.X - 10, this._endPoint.Value.Y);
                eventHandled = true;
            }

            else if (e.Key == Key.Down)
            {
                this._startPoint = new Point(this._startPoint.Value.X, this._startPoint.Value.Y + 10);
                this._endPoint = new Point(this._endPoint.Value.X, this._endPoint.Value.Y + 10);
                eventHandled = true;
            }

            if (eventHandled)
            {
                e.Handled = true; 
                this._UpdateMeasurements(); 
                this.InvalidateVisual();
            }

        }
        #endregion

        #region Render
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            dc.PushTransform(new TranslateTransform(this._origin.X, this._origin.Y)); // shift coordinate system to origin
            dc.PushTransform(new ScaleTransform(this._zoomFactor, this._zoomFactor)); // multiply coordinate system with zoom factor

            // draw background image if available
            if (this._backgroundImage != null)
            {
                // image width and height
                double bgWidth = this._backgroundImage.PixelWidth;
                double bgHeight = this._backgroundImage.PixelHeight;
                // start point to draw bg image
                double bgDrawX = (this.ActualWidth - bgWidth) / 2;
                double bgDrawY = (this.ActualHeight - bgHeight) / 2;
                // center of bg image
                double bgCenterX = bgDrawX + bgWidth / 2;
                double bgCenterY = bgDrawY + bgHeight / 2;

                dc.PushTransform(new TranslateTransform(bgCenterX, bgCenterY));    // shift coordinate system to image center
                dc.PushTransform(new RotateTransform(this._backgroundRotation));   // rotate coordinate system
                dc.PushTransform(new TranslateTransform(-bgCenterX, -bgCenterY));  // reset coordinate system
                // draw image
                dc.DrawImage(this._backgroundImage, new Rect(bgDrawX, bgDrawY, bgWidth, bgHeight));
                // remove all transforms
                dc.Pop();
                dc.Pop();
                dc.Pop();
            }
            // check if there is a diagram defined
            if (this._startPoint == null || this._endPoint == null)
            {
                // remove all transforms and return
                dc.Pop();
                dc.Pop();
                return;
            }

            // define rectangle based on start and end points
            Rect rect = new Rect(this._startPoint.Value, this._endPoint.Value);
            // calculate center point
            Point center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            // define ellipse based
            EllipseGeometry ellipse = new EllipseGeometry(center, rect.Width / 2, rect.Height / 2);

            dc.DrawRectangle(null, new Pen(Brushes.Red, 2), rect); // draw rectangle
            dc.DrawGeometry(null, new Pen(Brushes.Blue, 2), ellipse); // draw ellipse

            // draw radial lines and labels
            for (int deg = 0; deg < 360; deg += 10) // every 10 degrees
            {
                // calculate x and y components
                double lx = center.X + (rect.Width / 2) * Math.Cos((deg - 90) * Math.PI / 180.0); // x componet of the edge point
                double ly = center.Y + (rect.Height / 2) * Math.Sin((deg - 90) * Math.PI / 180.0); // y component of the edge point
                // draw line from center to edge
                dc.DrawLine(new Pen(Brushes.Gray, 1), center, new Point(lx, ly));
                // label
                FormattedText text = new FormattedText(
                    $"{deg}°",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    6,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip
                );
                double tx = center.X + (rect.Width / 2) * 1.1 * Math.Cos((deg - 90) * Math.PI / 180.0); // x component of label 
                double ty = center.Y + (rect.Height / 2) * 1.1 * Math.Sin((deg - 90) * Math.PI / 180.0); // y component of label
                // draw label
                dc.DrawText(text, new Point(tx - text.Width / 2, ty - text.Height / 2));
            }
            // draw contour lines and labels
            foreach (int cr in this._contours)
            {
                // calculate rx and ry based on contour value
                double rx = (rect.Width / 2) * Math.Pow(10, -cr / 20.0);  // rx
                double ry = (rect.Height / 2) * Math.Pow(10, -cr / 20.0); // ry
                // define ellipse 
                EllipseGeometry contourEllipse = new EllipseGeometry(center, rx, ry);
                // draw contour ellipse
                dc.DrawGeometry(null, new Pen(Brushes.LightGray, 1), contourEllipse);
                // label
                FormattedText text = new FormattedText(
                    $"{cr}°",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    3,
                    Brushes.Red,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip
                );

                double tx = center.X + rx; // x component of label
                double ty = center.Y;      // y component of label
                // draw label
                dc.DrawText(text, new Point(tx - text.Width / 2, ty - text.Height / 2));
            }

            // draw measured points
            foreach (KeyValuePair<int, (double, Point)> entry in this.measurements)
            {
                int angle = entry.Key;           // angle
                Point point = entry.Value.Item2; // position
                // draw point
                dc.DrawEllipse(Brushes.Yellow, new Pen(Brushes.Orange, 1), point, 2, 2);
            }
            // if more than one point -> connect with lines
            if (this.measurements.Count > 1)
            {
                List<int> sortedAngles = this.measurements.Keys.OrderBy(a => a).ToList(); // angles (sorted)
                for (int i = 0; i < sortedAngles.Count - 1; i++)
                {
                    Point p1 = this.measurements[sortedAngles[i]].Item2; // position 1
                    Point p2 = this.measurements[sortedAngles[i + 1]].Item2; // position 2
                    // draw line from p1 to p2
                    dc.DrawLine(new Pen(Brushes.Orange, 1), p1, p2);
                }
                if (sortedAngles.Count == 36) // if all points are measured -> connect last to fist
                {
                    Point first = this.measurements[sortedAngles[0]].Item2; //first point
                    Point last = this.measurements[sortedAngles[35]].Item2; // last point
                    // draw line from last to first
                    dc.DrawLine(new Pen(Brushes.Orange, 1), last, first);
                }
            }
            // remove all transforms
            dc.Pop();
            dc.Pop();
        }
        #endregion

        #region Helper Function (Trim State Stack)
        /// <summary>
        /// keeps the undo and redo stack withing a certain limit
        /// </summary>
        private void TrimStateStack(Stack<DiagramState> stateStack)
        {
            // check if stack exeeds maximum limit
            if (stateStack.Count <= maxUndoRedoSteps)
            {
                return;
            }

            DiagramState[] statesArray = stateStack.ToArray(); // convert stack to array -> so i can trim and reverse it ...
            stateStack.Clear(); // clear stack
            foreach (DiagramState state in statesArray.Take(maxUndoRedoSteps).Reverse())
            {
                stateStack.Push(state); // rebuild stack
            }
        }
        #endregion

        #region Helper Function (Save Diagram State)
        /// <summary>
        /// saves the current states of the diagram
        /// </summary>
        private void _SaveState()
        {
            // check if we are performing undo-redo -> do not save state
            if (this._isPerformingUndoRedo)
            {
                return;
            }
            // create a new diagram state
            DiagramState currentState = new DiagramState(
                this._startPoint,
                this._endPoint,
                this.measurements,
                this._isLocked
            );

            this.UndoStack.Push(currentState); // push current state to undo stack
            this.RedoStack.Clear(); // clear redo stack
            this.TrimStateStack(this.UndoStack); // trim undo stack just in case
        }
        #endregion

        #region Helper function (Undo)
        /// <summary>
        /// Undo the last action
        /// </summary>
        public void Undo()
        {
            // check if undo stack is empty
            if (this.UndoStack.Count == 0)
            {
                return;
            }
            // set flag -> performing undo-redo
            this._isPerformingUndoRedo = true;
            // save current state to redo stack
            DiagramState currentState = new DiagramState(
               this._startPoint,
               this._endPoint,
               this.measurements,
               this._isLocked
            );
            this.RedoStack.Push(currentState);
            this.TrimStateStack(this.RedoStack); 
            // pop undo stack -> previous state
            DiagramState prevState = this.UndoStack.Pop();
            // reset attributes to previous state
            this._startPoint = prevState.StartPoint;
            this._endPoint = prevState.EndPoint;
            this.measurements = new Dictionary<int, (double, Point)>(prevState.Measurements);
            this._isLocked = prevState.IsLocked;

            this._isPerformingUndoRedo = false; // reset flag -> not performing undo-redo
            this.InvalidateVisual(); // update visuals
        }
        #endregion

        #region Helper function (Redo)
        /// <summary>
        /// Redo the last action
        /// </summary>
        public void Redo()
        {
            // check if redo stack is empty
            if (this.RedoStack.Count == 0)
            {
                return;
            }
            // set flag -> performing undo-redo
            this._isPerformingUndoRedo = true;
            // save current state to redo stack
            DiagramState currentState = new DiagramState(
               this._startPoint,
               this._endPoint,
               this.measurements,
               this._isLocked
            );
            this.UndoStack.Push(currentState);
            this.TrimStateStack(this.UndoStack);
            DiagramState nextState = this.RedoStack.Pop();
            // reset attributes to previous state
            this._startPoint = nextState.StartPoint;
            this._endPoint = nextState.EndPoint;
            this.measurements = new Dictionary<int, (double, Point)>(nextState.Measurements);
            this._isLocked = nextState.IsLocked;

            this._isPerformingUndoRedo = false; // reset flag -> not performing undo-redo
            this.InvalidateVisual(); // update visuals
        }
        #endregion

        #region Helper Function (Get Positions)
        /// <summary>
        /// converts absolute position of the mouse to a position on the diagram based on origin and zoom factor
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
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
            // check if start and end points are defined
            if (this._startPoint == null || this._endPoint == null)
            {
                return false; // if diagram is not defined -> return false
            }
            // define a rectangle using start and end points
            Rect rect = new Rect(this._startPoint.Value, this._endPoint.Value);
            // calculate center 
            Point center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            // define ellipse
            EllipseGeometry ellipse = new EllipseGeometry(center, rect.Width / 2, rect.Height / 2);
            // does ellipse contain the position?
            return ellipse.FillContains(pos);
        }
        #endregion

        #region Helper Function (Get Resize Direction)
        /// <summary>
        /// determines the correct resize edge based on the position 
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        private string _GetResizeDirection(Point pos)
        {
            // check if start and end points are defined
            if (this._startPoint == null || this._endPoint == null)
            {
                return ""; // if diagram is not defined -> return empty string
            }
            // define a rectangle using start and end points
            Rect rect = new Rect(this._startPoint.Value, this._endPoint.Value);
            // define threshold for edge detection (10 pixels)
            const double threshold = 10.0;

            // Note : coordinate system is : top-left (0,0), bottom-right (width, height)
            // pos between top and bottom edge
            if (pos.Y >= rect.Top && pos.Y <= rect.Bottom)
            {
                // and near left edge?
                if (Math.Abs(pos.X - rect.Left) <= threshold)
                {
                    return "Left";
                }
                // and near right edge?
                if (Math.Abs(pos.X - rect.Right) <= threshold)
                {
                    return "Right";
                }
            }
            // pos between left and right edge
            if (pos.X >= rect.Left && pos.X <= rect.Right)
            {
                // and near bottom edge?
                if (Math.Abs(pos.Y - rect.Bottom) <= threshold)
                {
                    return "Bottom";
                }
                // and near top edge?
                if (Math.Abs(pos.Y - rect.Top) <= threshold)
                {
                    return "Top";
                }
            }
            return "";
        }
        #endregion

        #region Helper Function (Set Cursor Icon)
        /// <summary>
        /// determines the correct cursor icon based on the position
        /// </summary>
        /// <param name="pos"></param>
        private void _SetCursorIcon(Point pos)
        {
            // if diagram is locked -> set arrow cursor and return
            if (this._isLocked)
            {
                this.Cursor = Cursors.Arrow;
                return;
            }
            // if mouse if near an edge -> set resize cursor
            string edge = this._GetResizeDirection(pos);
            if (edge == "Left" || edge == "Right")
            {
                this.Cursor = Cursors.SizeWE;
            }
            else if (edge == "Bottom" || edge == "Top")
            {
                this.Cursor = Cursors.SizeNS;
            }
            // if mouse is inside ellipse -> set move cursor
            else if (this._IsInsideEllipse(pos))
            {
                this.Cursor = Cursors.SizeAll;
            }
            // default -> set arrow cursor
            else
            {
                this.Cursor = Cursors.Arrow;
            }
        }
        #endregion

        #region Helper Function (Set Background Image)
        /// <summary>
        /// sets the background image
        /// </summary>
        /// <param name="filePath"></param>
        public void SetBackgroundImage(string filePath)
        {
            this._backgroundImage = new BitmapImage(new Uri(filePath, UriKind.Absolute));
            InvalidateVisual();
        }
        #endregion

        #region Helper Function (Draw)
        /// <summary>
        /// draws the diagram 
        /// </summary>
        /// <param name="pos"></param>
        private void Draw(Point pos)
        {
            // check if start point is defined
            if (this._startPoint == null)
            {
                return;
            }
            // update the endpoint to the current position
            this._endPoint = pos;
            // update visuals
            this.InvalidateVisual();
        }
        #endregion

        #region Helper Function (Move)
        /// <summary>
        /// Moves the diagram
        /// </summary>
        /// <param name="pos"></param>
        private void Move(Point pos)
        {
            // check if diagram is defined or move start point exists
            if (this._moveStartPoint == null || this._startPoint == null || this._endPoint == null)
            {
                return;
            }
            // calculate distance in x and y
            double dx = pos.X - this._moveStartPoint.Value.X;
            double dy = pos.Y - this._moveStartPoint.Value.Y;

            // update start and end point of diagram
            this._startPoint = new Point(this._startPoint.Value.X + dx, this._startPoint.Value.Y + dy);
            this._endPoint = new Point(this._endPoint.Value.X + dx, this._endPoint.Value.Y + dy);
            // update the move start point
            this._moveStartPoint = pos;
            // update point measurements
            this._UpdateMeasurements();
            // update visuals
            this.InvalidateVisual();
        }
        #endregion

        #region Helper Function (Resize)
        /// <summary>
        /// handles resize
        /// </summary>
        /// <param name="pos"></param>
        private void Resize(Point pos)
        {
            // check if diagram is defined and resize direction exists
            if (this._startPoint == null || this._endPoint == null || string.IsNullOrEmpty(this._resizeDirection))
            {
                return;
            }
            // store current start and end points
            Point start = this._startPoint.Value;
            Point end = this._endPoint.Value;

            // update start or end point based on resize direction
            if (this._resizeDirection == "Left")
            {
                start = new Point(pos.X, start.Y); // left -> change x component of start point
            }
            else if (this._resizeDirection == "Right")
            {
                end = new Point(pos.X, end.Y); // right -> change x component of end point
            }
            else if (this._resizeDirection == "Top")
            {
                start = new Point(start.X, pos.Y); // top -> change y component of start point
            }
            else if (this._resizeDirection == "Bottom")
            {
                end = new Point(end.X, pos.Y); // bottom -> change y component of end point
            }
            //update start and end points
            this._startPoint = start;
            this._endPoint = end;
            // update point measurements
            this._UpdateMeasurements();
            // update visuals
            this.InvalidateVisual();
        }
        #endregion

        #region Helper Function (Delete Diagram)
        /// <summary>
        /// deletes the diagram
        /// </summary>
        public void DeleteDiagram()
        {
            this._SaveState(); // save state
            // reset all attributes of diagram
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
            // update visuals
            this.InvalidateVisual();
        }
        #endregion

        #region Helper Function (Delete Background Image)
        /// <summary>
        /// deletes the background image
        /// </summary>
        public void DeleteBackgroundImage()
        {
            // reset background image and rotation
            this._backgroundImage = null;
            this._backgroundRotation = 0.0;
            // update visuals
            InvalidateVisual();
        }
        #endregion

        #region Helper Function (Delete Measurments)
        /// <summary>
        /// deletes the measured points
        /// </summary>
        public void DeleteMeasurements()
        {
            this._SaveState(); // save state
            // reset measurements
            this.measurements.Clear();
            // update visuals
            InvalidateVisual();
        }
        #endregion

        #region Helper Function (Measure Point)
        /// <summary>
        /// measures the closest angle, db value and descrete position based on the given position
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        private (int closestAngle, double dbValue, Point point)? MeasurePoint(Point pos)
        {
            // check if diagram is defined
            if (this._startPoint == null || this._endPoint == null)
            {
                return null;
            }
            // define a rectangle using start and end points
            Rect rect = new Rect(this._startPoint.Value, this._endPoint.Value);
            // calculate center
            Point center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

            // normalized x and y components
            double x = (pos.X - center.X) / (rect.Width / 2);
            double y = (pos.Y - center.Y) / (rect.Height / 2);
            // normalized distance from center
            double distance = Math.Sqrt(x * x + y * y);

            // 1. calculate db value based on distance
            double dbValue = 30.0;
            if (distance > 0)
            {
                dbValue = Math.Max(0, Math.Min(-20 * Math.Log10(distance), 30));
            }

            // 2. find closest angle to the recorded position
            double lowestDiviation = double.PositiveInfinity;
            int closestAngle = 0;
            // check distance is not zero
            if (distance != 0)
            {
                for (int a = 0; a < 360; a += 10)
                {
                    int angle = a; // current angle
                    double angleRad = (angle - 90) * Math.PI / 180.0; // current angle in radian
                    double dotProduct = x * Math.Cos(angleRad) + y * Math.Sin(angleRad); // dot product
                    // check if dot product is negative
                    if (dotProduct < 0)
                    {
                        angle = (angle + 180) % 360; // reverse angle
                        dotProduct = -dotProduct;
                    }

                    double deviation = 1 - Math.Abs(dotProduct / distance); // calculate deviation
                    if (deviation < lowestDiviation) // is it the lowest deviation so far?
                    {
                        lowestDiviation = deviation; // update lowest deviation
                        closestAngle = angle; // update closest angle
                    }
                }
            }

            // 3. calculate the descrete position based on closest angle and db value
            double rad = (closestAngle - 90) * Math.PI / 180.0;
            double linear = Math.Pow(10, -dbValue / 20);
            double px = center.X + (rect.Width / 2) * linear * Math.Cos(rad);
            double py = center.Y + (rect.Height / 2) * linear * Math.Sin(rad);

            Point descPoint = new Point(px, py);

            return (closestAngle, dbValue, descPoint);
        }
        #endregion

        #region Helper Function (Update Measurments)
        private void _UpdateMeasurements()
        {
            // check if diagram is defined
            if (this._startPoint == null || this._endPoint == null)
            {
                return;
            }
            // define rectangle using start and end points
            Rect rect = new Rect(this._startPoint.Value, this._endPoint.Value);
            // calculate center
            Point center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

            // define a new dictionary to store measurements
            Dictionary<int, (double, Point)> updatedMeasurements = new Dictionary<int, (double, Point)>();
            foreach (KeyValuePair<int, (double, Point)> entry in this.measurements)
            {
                // angle and db value stay the same
                int angle = entry.Key;
                double dbValue = entry.Value.Item1;
                // recalculate positions based on new diagram dimentions
                double rad = (angle - 90) * Math.PI / 180.0;
                double linear = Math.Pow(10, -dbValue / 20);
                double px = center.X + (rect.Width / 2) * linear * Math.Cos(rad);
                double py = center.Y + (rect.Height / 2) * linear * Math.Sin(rad);
                Point point = new Point(px, py);

                updatedMeasurements[angle] = (dbValue, point);
            }
            // update measurements
            this.measurements = updatedMeasurements;
        }
        #endregion

        #region Helper Function (Interpolate Measurments)
        /// <summary>
        /// Uses Linear Interpolation to fill in missing measurement points
        /// </summary>
        public void InterpolateMeasurements()
        {
            if (this.measurements.Count == 0 || this._startPoint == null || this._endPoint == null)
            {
                return;
            }
            this._SaveState(); 
            // calculate center point
            Rect rect = new Rect(this._startPoint.Value, this._endPoint.Value);
            Point center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            // list measured angles
            var result = new Dictionary<int, (double, Point)>(this.measurements);
            var measuredAngles = result.Keys.OrderBy(a => a).ToList();
            // iterate through all angles
            for (int currentAngle = 0; currentAngle < 360; currentAngle += 10)
            {
                // if current angle was measured -> continue
                if (measuredAngles.Contains(currentAngle))
                {
                    continue;
                }
                // initialize lower and upper bounds
                int lowerAngle = -1;
                int upperAngle = -1;
                // set lower and upper bounds
                foreach (var angle in measuredAngles)
                {
                    if (angle < currentAngle)
                    {
                        lowerAngle = angle;
                    }
                    if (angle > currentAngle && upperAngle == -1)
                    {
                        upperAngle = angle;
                    }
                }
                // no lower bound
                if (lowerAngle == -1)
                {

                    double db = result[upperAngle].Item1;
                    // recalculate position based on angle and interpolated db value
                    double rad = (currentAngle - 90) * Math.PI / 180.0;
                    double linear = Math.Pow(10, -db / 20);
                    double px = center.X + (rect.Width / 2) * linear * Math.Cos(rad);
                    double py = center.Y + (rect.Height / 2) * linear * Math.Sin(rad);
                    Point point = new Point(px, py);

                    result[currentAngle] = (db, point);
                }
                // no upper bound
                else if (upperAngle == -1)
                {
                    double db = result[lowerAngle].Item1;

                    double rad = (currentAngle - 90) * Math.PI / 180.0;
                    double linear = Math.Pow(10, -db / 20);
                    double px = center.X + (rect.Width / 2) * linear * Math.Cos(rad);
                    double py = center.Y + (rect.Height / 2) * linear * Math.Sin(rad);
                    Point point = new Point(px, py);

                    result[currentAngle] = (db, point);
                }
                // interpolation possible
                else
                {
                    // calculate interpolation factor
                    double alpha = (double)(currentAngle - lowerAngle) / (upperAngle - lowerAngle);
                    // interpolate db values
                    double lowerDb = result[lowerAngle].Item1;
                    double upperDb = result[upperAngle].Item1;
                    double interpolatedDb = lowerDb + alpha * (upperDb - lowerDb);
                    // recalculate position based on angle and interpolated db value
                    double rad = (currentAngle - 90) * Math.PI / 180.0;
                    double linear = Math.Pow(10, -interpolatedDb / 20);
                    double px = center.X + (rect.Width / 2) * linear * Math.Cos(rad);
                    double py = center.Y + (rect.Height / 2) * linear * Math.Sin(rad);
                    Point point = new Point(px, py);
                    // store interpolated value
                    result[currentAngle] = (interpolatedDb, point);
                }
            }
            this.measurements = result;
            this.InvalidateVisual();
        }
        #endregion

        #region Helper Function (Set Meaurments From Database)
        /// <summary>
        /// sets measurements based on a dictionary of angle and db values
        /// </summary>
        public bool SetMeasurements(Dictionary<int, double> angleDB)
        {
            // check if diagram is defined or there is available data
            if(this._startPoint == null || this._endPoint == null || angleDB == null)
            {
                return false;
            }
            this._SaveState(); // save state
            Dictionary<int, (double, Point)> newMeasurements = new Dictionary<int, (double, Point)>();
            foreach (KeyValuePair<int, double> kvp in angleDB)
            {
                double dbValue = kvp.Value;
                newMeasurements[kvp.Key] = (dbValue, new Point(0, 0)); // initialze all positions with (0,0)
            }
            this.measurements = newMeasurements;
            this._UpdateMeasurements(); // update positions
            this.InvalidateVisual();
            return true;
        }
        #endregion
    }
}