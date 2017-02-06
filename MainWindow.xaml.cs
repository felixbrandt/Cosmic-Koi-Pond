using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace CosmicKoiPond
{
    /// <summary>
    /// Interaction logic for MainWindow
    /// (Most of this is from Microsofts Kinect examples)
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor _kinectSensor;

        /// <summary>
        /// Reader for color frames
        /// </summary>
        private ColorFrameReader _colorFrameReader;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private readonly WriteableBitmap _colorBitmap;

        /// <summary>
        /// Window for rendering the visuals
        /// </summary>
        private readonly VideoOutput _videoWindow;

        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        /// <summary>
        /// Constant for clapming Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        private readonly Brush _handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        private readonly Brush _handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        private readonly Brush _handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>
        private readonly Pen _inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private readonly DrawingGroup _bodyDrawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private readonly DrawingImage _bodyImageSource;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private readonly CoordinateMapper _coordinateMapper;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader _bodyFrameReader;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] _bodies;

        private readonly List<Body> _bodiesPreviousFrame;

        /// <summary>
        /// Definition of bones
        /// </summary>
        private readonly List<Tuple<JointType, JointType>> _bones;

        /// <summary>
        /// Width of display (depth space)
        /// </summary>
        private readonly int _displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private readonly int _displayHeight;

        /// <summary>
        /// List of colors for each body tracked
        /// </summary>
        private readonly List<Pen> _bodyColors;
        
        /// <summary>
        /// List of gesture detectors, there will be one detector created for each potential body (max 6)
        /// </summary>
        private List<GestureDetector> _gestureDetectorList;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // get the kinectSensor object
            _kinectSensor = KinectSensor.GetDefault();

            // open the reader for the color frames
            _colorFrameReader = _kinectSensor.ColorFrameSource.OpenReader();

            // wire handler for frame arrival
            _colorFrameReader.FrameArrived += Reader_ColorFrameArrived;

            // create the colorFrameDescription from the ColorFrameSource using Bgra format
            FrameDescription colorFrameDescription = _kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            // create the bitmap to display
            _colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            // Get the coordinate mapper
            _coordinateMapper = _kinectSensor.CoordinateMapper;

            // Get the depth (display) extents
            FrameDescription depthFrameDescription = _kinectSensor.DepthFrameSource.FrameDescription;

            // Get size of joint space
            _displayWidth = depthFrameDescription.Width;
            _displayHeight = depthFrameDescription.Height;

            // initialize the gesture detection objects for our gestures
            _gestureDetectorList = new List<GestureDetector>();

            // create video window
            _videoWindow = new VideoOutput();

            // create a gesture detector for each body (6)
            int maxBodies = _kinectSensor.BodyFrameSource.BodyCount;

            _bodiesPreviousFrame = new List<Body>();

            for (int i = 0; i < maxBodies; ++i)
            {
                GestureResultView result = new GestureResultView(i, false, false, 0.0f, _videoWindow);
                GestureDetector detector = new GestureDetector(_kinectSensor, result);
                _gestureDetectorList.Add(detector);
            }

            // open the reader for the body frames
            _bodyFrameReader = _kinectSensor.BodyFrameSource.OpenReader();

            // A bone defined as a line  between two joints
            _bones = new List<Tuple<JointType, JointType>>
            {
                // Torso
                new Tuple<JointType, JointType>(JointType.Head, JointType.Neck),
                new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder),
                new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight),
                new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft),
                // Right Arm
                new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight),
                new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight),
                new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight),
                new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight),
                // Left Arm
                new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft),
                new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft),
                new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft),
                new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft)
            };

            // populate  body colors, one for each BodyIndex
            _bodyColors = new List<Pen>
            {
                new Pen(Brushes.Red, 6),
                new Pen(Brushes.Orange, 6),
                new Pen(Brushes.Green, 6),
                new Pen(Brushes.Blue, 6),
                new Pen(Brushes.Indigo, 6),
                new Pen(Brushes.Violet, 6)
            };


            // open the sensor
            _kinectSensor.Open();

            // Create the drawing group we'll use for drawing
            _bodyDrawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            _bodyImageSource = new DrawingImage(_bodyDrawingGroup);

            // use the window object as the view model in this simple example
            DataContext = this;

            // initialize the components (controls) of the window
            InitializeComponent();
        }

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource => _colorBitmap;

        /// <summary>
        /// Gets the bone bitmap to display
        /// </summary>
        public ImageSource BodyImageSource => _bodyImageSource;

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // Close the Video window
            _videoWindow?.Close();

            if (_colorFrameReader != null)
            {
                // ColorFrameReder is IDisposable
                _colorFrameReader.Dispose();
                _colorFrameReader = null;
            }

            if (_bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable too :)
                _bodyFrameReader.Dispose();
                _bodyFrameReader = null;
            }

            if (_gestureDetectorList != null)
            {
                foreach (GestureDetector detector in _gestureDetectorList)
                {
                    // GestureDetector is ... you guessed it ... IDisposable
                    detector.Dispose();
                }

                _gestureDetectorList.Clear();
                _gestureDetectorList = null;
            }

            if (_kinectSensor != null)
            {
                _kinectSensor.Close();
                _kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the color frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            // ColorFrame is IDisposable
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                    using (colorFrame.LockRawImageBuffer())
                    {
                        _colorBitmap.Lock();

                        // verify data and write the new color frame data to the display bitmap
                        if ((colorFrameDescription.Width == _colorBitmap.PixelWidth) && (colorFrameDescription.Height == _colorBitmap.PixelHeight))
                        {
                            colorFrame.CopyConvertedFrameDataToIntPtr(
                                _colorBitmap.BackBuffer,
                                (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                                ColorImageFormat.Bgra);

                            _colorBitmap.AddDirtyRect(new Int32Rect(0, 0, _colorBitmap.PixelWidth, _colorBitmap.PixelHeight));
                        }

                        _colorBitmap.Unlock();
                    }
                }
            }
        }

        /// <summary>
        /// Load the Video window after the debug window finishes loading
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_bodyFrameReader != null)
            {
                _bodyFrameReader.FrameArrived += Reader_BodyFrameArrived;
            }

            // Display the VideoWindow once the debug window is loaded
            _videoWindow.Show();
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Reader_BodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (_bodies == null)
                    {
                        _bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(_bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                // Draw the bodies
                using (DrawingContext dc = _bodyDrawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, _displayWidth, _displayHeight));

                    int penIndex = 0;

                    foreach(Body body in _bodiesPreviousFrame.ToList())
                    {
                        if (!body.IsTracked)
                        {
                            // A player left the kinect view
                            Console.WriteLine(@"CheckFishRemoval() ({0} spawningCreature(es))", body.TrackingId);
                            _bodiesPreviousFrame.Remove(body);
                            _videoWindow.OnPlayerLeave(body);
                        }
                    }

                    foreach (Body body in _bodies)
                    {
                        Pen drawPen = _bodyColors[penIndex++];

                        if (body.IsTracked)
                        {
                            if (!_bodiesPreviousFrame.Contains(body)) {
                                // A player moved into the kinect view
                                Console.WriteLine(@"CheckFishRemoval() ({0} spawningCreature(es))", body.TrackingId);
                                _bodiesPreviousFrame.Add(body);
                                _videoWindow.OnPlayerEnter(body);
                            }
                            
                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                            // convert the joint points to depth (display) space
                            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                            foreach (JointType jointType in joints.Keys)
                            {
                                // Sometimes the depth (Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                CameraSpacePoint position = joints[jointType].Position;
                                if (position.Z < 0)
                                {
                                    position.Z = InferredZPositionClamp;
                                }

                                DepthSpacePoint depthSpacePoint = _coordinateMapper.MapCameraPointToDepthSpace(position);
                                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            }

                            DrawBody(joints, jointPoints, dc, drawPen);

                            DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                            DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);
                        }
                    }

                    // prevent drawing outside of our render area
                    _bodyDrawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, _displayWidth, _displayHeight));
                }

                // we may have lost/acquired bodies, so update the corresponding gesture detectors
                if (_bodies != null)
                {
                    // loop through all bodies to see if any of the gesture detectors need to be updated
                    int maxBodies = _kinectSensor.BodyFrameSource.BodyCount;
                    for (int i = 0; i < maxBodies; ++i)
                    {
                        Body body = _bodies[i];
                        ulong trackingId = body.TrackingId;

                        // if the current body TrackingId changed, update the corresponding gesture detector with the new value
                        if (trackingId != _gestureDetectorList[i].TrackingId)
                        {
                            _gestureDetectorList[i].TrackingId = trackingId;

                            // if the current body is tracked, unpause its detector to get VisualGestureBuilderFrameArrived events
                            // if the current body is not tracked, pause its detector so we don't waste resources trying to get invalid gesture results
                            _gestureDetectorList[i].IsPaused = trackingId == 0;
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="dc">drawing context to draw to</param>
        /// <param name="drawPen">specifies color to draw a specific body</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, Dictionary<JointType, Point> jointPoints, DrawingContext dc, Pen drawPen)
        {
            // draw the bones
            foreach (var bone in _bones)
            {
                DrawBone(joints, jointPoints, bone.Item1, bone.Item2, dc, drawPen);
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="drawingPen">specifies color to draw a specific bone</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = _inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="dc">drawing context to draw to</param>
        private void DrawHand(HandState handState, Point handPosition, DrawingContext dc)
        {
            switch (handState)
            {
                case HandState.Closed:
                    dc.DrawEllipse(_handClosedBrush, null, handPosition, HandSize, HandSize);
                    break;
                case HandState.Open:
                    dc.DrawEllipse(_handOpenBrush, null, handPosition, HandSize, HandSize);
                    break;
                default:
                    dc.DrawEllipse(_handLassoBrush, null, handPosition, HandSize, HandSize);
                    break;
            }
        }

        
    }
}
