using System;
using System.Collections.Generic;
using System.ComponentModel;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using WindowsPreview.Kinect;

namespace KinectFaces
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {

        readonly string NoSensorStatusText = "No ready Kinect found!";
        readonly string RunningStatusText = "Running";
        readonly string SensorNotAvailableStatusText = "Kinect not available!";


        readonly double HighConfidenceHandSize = 40;
        readonly double LowConfidenceHandSize = 20;

        readonly double TrackedBoneThickness = 4.0;
        readonly double InferredBoneThickness = 1.0;
        readonly float InferredZPositionClamp = 0.1f;

        KinectSensor kinectSensor = null;

        CoordinateMapper coordinateMapper = null;
        BodyFrameReader bodyFrameReader = null;

        WindowsPreview.Kinect.Body[] bodies = null;

        Canvas drawingCanvas;

        Body[] BodyInfos;

        List<Color> BodyColors;

        private int BodyCount
        {
            set
            {
                if (value == 0)
                {
                    this.BodyInfos = null;
                    return;
                }

                // creates instances of BodyInfo objects for potential number of bodies
                if (this.BodyInfos == null || this.BodyInfos.Length != value)
                {
                    this.BodyInfos = new Body[value];

                    for (int bodyIndex = 0; bodyIndex < this.bodies.Length; bodyIndex++)
                    {
                        this.BodyInfos[bodyIndex] = new Body(this.BodyColors[bodyIndex]);
                    }
                }
            }

            get { return this.BodyInfos == null ? 0 : this.BodyInfos.Length; }
        }

        private float JointSpaceWidth { get; set; }

        private float JointSpaceHeight { get; set; }

        public MainPage()
        {
            this.kinectSensor = KinectSensor.GetDefault();
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.JointSpaceWidth = frameDescription.Width;
            this.JointSpaceHeight = frameDescription.Height;

            // get total number of bodies from BodyFrameSource
            this.bodies = new WindowsPreview.Kinect.Body[this.kinectSensor.BodyFrameSource.BodyCount];

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // wire handler for frame arrival
            this.bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // populate body colors, one for each BodyIndex
            this.BodyColors = new List<Color>
            {
                Colors.Red,
                Colors.Orange,
                Colors.Green,
                Colors.Blue,
                Colors.Indigo,
                Colors.Violet
            };

            // sets total number of possible tracked bodies
            // create ellipses and lines for drawing bodies
            this.BodyCount = this.kinectSensor.BodyFrameSource.BodyCount;

            this.drawingCanvas = new Canvas();
            this.kinectSensor.Open();

            this.StatusText = this.kinectSensor.IsAvailable ? RunningStatusText
                                                            : NoSensorStatusText;

            this.DataContext = this;
            this.InitializeComponent();

            // set the clip rectangle to prevent rendering outside the canvas
            this.drawingCanvas.Clip = new RectangleGeometry();
            this.drawingCanvas.Clip.Rect = new Rect(0.0, 0.0, this.DisplayGrid.Width, this.DisplayGrid.Height);

            // create visual objects for drawing joints, bone lines, and clipped edges
            this.PopulateVisualObjects();

            // add canvas to DisplayGrid
            this.DisplayGrid.Children.Add(this.drawingCanvas);
        }


        public event PropertyChangedEventHandler PropertyChanged;

        private string _StatusText = null;
        public string StatusText
        {
            get
            {
                return this._StatusText;
            }

            set
            {
                if (this._StatusText != value)
                {
                    this._StatusText = value;
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.bodies != null)
            {
                foreach (WindowsPreview.Kinect.Body body in this.bodies)
                {
                    if (body != null)
                    {
                        body.Dispose();
                    }
                }
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        // Handles the body frame data arriving from the sensor
        private void Reader_BodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                this.BeginBodiesUpdate();

                // iterate through each body
                for (int bodyIndex = 0; bodyIndex < this.bodies.Length; bodyIndex++)
                {
                    WindowsPreview.Kinect.Body body = this.bodies[bodyIndex];

                    if (body.IsTracked)
                    {
                        this.UpdateBody(body, bodyIndex);
                    }
                    else
                    {
                        // collapse this body from canvas as it goes out of view
                        this.ClearBody(bodyIndex);
                    }
                }
            }
        }

        /// <summary>
        /// Clear update status of all bodies
        /// </summary>
        internal void BeginBodiesUpdate()
        {
            if (this.BodyInfos != null)
            {
                foreach (var bodyInfo in this.BodyInfos)
                {
                    bodyInfo.Updated = false;
                }
            }
        }

        /// <summary>
        /// Update body data for each body that is tracked.
        /// </summary>
        /// <param name="body">body for getting joint info</param>
        /// <param name="bodyIndex">index for body we are currently updating</param>
        internal void UpdateBody(WindowsPreview.Kinect.Body body, int bodyIndex)
        {
            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
            var jointPointsInDepthSpace = new Dictionary<JointType, Point>();

            var bodyInfo = this.BodyInfos[bodyIndex];

            CoordinateMapper coordinateMapper = this.kinectSensor.CoordinateMapper;

            // update all joints
            foreach (var jointType in body.Joints.Keys)
            {
                // sometimes the depth(Z) of an inferred joint may show as negative
                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                CameraSpacePoint position = body.Joints[jointType].Position;
                if (position.Z < 0)
                {
                    position.Z = InferredZPositionClamp;
                }

                // map joint position to depth space
                DepthSpacePoint depthSpacePoint = coordinateMapper.MapCameraPointToDepthSpace(position);
                jointPointsInDepthSpace[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);

                // modify hand ellipse colors based on hand states
                // modity hand ellipse sizes based on tracking confidences
                if (jointType == JointType.HandRight)
                {
                    this.UpdateHand(bodyInfo.HandRightEllipse, body.HandRightState, body.HandRightConfidence, jointPointsInDepthSpace[jointType]);
                }

                if (jointType == JointType.HandLeft)
                {
                    this.UpdateHand(bodyInfo.HandLeftEllipse, body.HandLeftState, body.HandLeftConfidence, jointPointsInDepthSpace[jointType]);
                }
            }

            // update all bones
            foreach (var bone in bodyInfo.Bones)
            {
                this.UpdateBone(bodyInfo.BoneLines[bone], joints[bone.Item1], joints[bone.Item2],
                                jointPointsInDepthSpace[bone.Item1],
                                jointPointsInDepthSpace[bone.Item2]);
            }
        }

        /// <summary>
        /// Collapse the body from the canvas.
        /// </summary>
        /// <param name="bodyIndex"></param>
        private void ClearBody(int bodyIndex)
        {
            var bodyInfo = this.BodyInfos[bodyIndex];

            // collapse all bone lines
            foreach (var bone in bodyInfo.Bones)
            {
                bodyInfo.BoneLines[bone].Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            // collapse handstate ellipses
            bodyInfo.HandLeftEllipse.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            bodyInfo.HandRightEllipse.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        /// <summary>
        /// Updates hand state ellipses depending on tracking state and it's confidence.
        /// </summary>
        /// <param name="ellipse">ellipse representing handstate</param>
        /// <param name="handState">open, closed, or lasso</param>
        /// <param name="trackingConfidence">confidence of handstate</param>
        /// <param name="point">location of handjoint</param>
        private void UpdateHand(Ellipse ellipse, HandState handState, TrackingConfidence trackingConfidence, Point point)
        {
            ellipse.Fill = new SolidColorBrush(this.HandStateToColor(handState));

            // draw handstate ellipse based on tracking confidence
            ellipse.Width = ellipse.Height = (trackingConfidence == TrackingConfidence.Low) ? LowConfidenceHandSize : HighConfidenceHandSize;

            ellipse.Visibility = Windows.UI.Xaml.Visibility.Visible;

            // don't draw handstate if hand joints are not tracked
            if (!Double.IsInfinity(point.X) && !Double.IsInfinity(point.Y))
            {
                Canvas.SetLeft(ellipse, point.X - ellipse.Width / 2);
                Canvas.SetTop(ellipse, point.Y - ellipse.Width / 2);
            }
        }

        /// <summary>
        /// Update a bone line.
        /// </summary>
        /// <param name="line">line representing a bone line</param>
        /// <param name="startJoint">start joint of bone line</param>
        /// <param name="endJoint">end joint of bone line</param>
        /// <param name="startPoint">location of start joint</param>
        /// <param name="endPoint">location of end joint</param>
        private void UpdateBone(Line line, Joint startJoint, Joint endJoint, Point startPoint, Point endPoint)
        {
            // don't draw if neither joints are tracked
            if (startJoint.TrackingState == TrackingState.NotTracked || endJoint.TrackingState == TrackingState.NotTracked)
            {
                line.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                return;
            }

            // all lines are inferred thickness unless both joints are tracked
            line.StrokeThickness = InferredBoneThickness;

            if (startJoint.TrackingState == TrackingState.Tracked &&
                endJoint.TrackingState == TrackingState.Tracked)
            {
                line.StrokeThickness = TrackedBoneThickness;
            }

            line.Visibility = Windows.UI.Xaml.Visibility.Visible;

            line.X1 = startPoint.X;
            line.Y1 = startPoint.Y;
            line.X2 = endPoint.X;
            line.Y2 = endPoint.Y;
        }

        // Select color of hand state
        private Color HandStateToColor(HandState handState)
        {
            switch (handState)
            {
                case HandState.Open:
                    return Colors.Green;

                case HandState.Closed:
                    return Colors.Red;

                case HandState.Lasso:
                    return Colors.Blue;
            }

            return Colors.Transparent;
        }

        // Instantiate new objects for joints and bone lines
        private void PopulateVisualObjects()
        {
            foreach (var bodyInfo in this.BodyInfos)
            {
                // add left and right hand ellipses of all bodies to canvas
                this.drawingCanvas.Children.Add(bodyInfo.HandLeftEllipse);
                this.drawingCanvas.Children.Add(bodyInfo.HandRightEllipse);

                // add bone lines of all bodies to canvas
                foreach (var bone in bodyInfo.Bones)
                {
                    this.drawingCanvas.Children.Add(bodyInfo.BoneLines[bone]);
                }
            }
        }

        // BodyInfo class that contains handstate ellipses and lines for bones between two joints.
        private class Body
        {
            public bool Updated { get; set; }

            public Color BodyColor { get; set; }

            // ellipse representing left handstate
            public Ellipse HandLeftEllipse { get; set; }

            // ellipse representing right handstate
            public Ellipse HandRightEllipse { get; set; }

            // definition of bones
            public TupleList<JointType, JointType> Bones { get; private set; }

            // collection of bones associated with the line object
            public Dictionary<Tuple<JointType, JointType>, Line> BoneLines { get; private set; }

            public Body(Color bodyColor)
            {
                this.BodyColor = bodyColor;

                // create hand state ellipses
                this.HandLeftEllipse = new Ellipse()
                {
                    Visibility = Windows.UI.Xaml.Visibility.Collapsed
                };

                this.HandRightEllipse = new Ellipse()
                {
                    Visibility = Windows.UI.Xaml.Visibility.Collapsed
                };

                // collection of bones
                this.BoneLines = new Dictionary<Tuple<JointType, JointType>, Line>();

                // a bone defined as a line between two joints
                this.Bones = new TupleList<JointType, JointType>
                {
                    // Torso
                    { JointType.Head, JointType.Neck },
                    { JointType.Neck, JointType.SpineShoulder },
                    { JointType.SpineShoulder, JointType.SpineMid },
                    { JointType.SpineMid, JointType.SpineBase },
                    { JointType.SpineShoulder, JointType.ShoulderRight },
                    { JointType.SpineShoulder, JointType.ShoulderLeft },
                    { JointType.SpineBase, JointType.HipRight },
                    { JointType.SpineBase, JointType.HipLeft },

                    // Right Arm
                    { JointType.ShoulderRight, JointType.ElbowRight },
                    { JointType.ElbowRight, JointType.WristRight },
                    { JointType.WristRight, JointType.HandRight },
                    { JointType.HandRight, JointType.HandTipRight },
                    { JointType.WristRight, JointType.ThumbRight },

                    // Left Arm
                    { JointType.ShoulderLeft, JointType.ElbowLeft },
                    { JointType.ElbowLeft, JointType.WristLeft },
                    { JointType.WristLeft, JointType.HandLeft },
                    { JointType.HandLeft, JointType.HandTipLeft },
                    { JointType.WristLeft, JointType.ThumbLeft },

                    // Right Leg
                    { JointType.HipRight, JointType.KneeRight },
                    { JointType.KneeRight, JointType.AnkleRight },
                    { JointType.AnkleRight, JointType.FootRight },
                
                    // Left Leg
                    { JointType.HipLeft, JointType.KneeLeft },
                    { JointType.KneeLeft, JointType.AnkleLeft },
                    { JointType.AnkleLeft, JointType.FootLeft },
                };

                // pre-populate list of bones that are non-visible initially
                foreach (var bone in this.Bones)
                {
                    this.BoneLines.Add(bone, new Line()
                    {
                        Stroke = new SolidColorBrush(BodyColor),
                        Visibility = Visibility.Collapsed
                    });
                }
            }
        }

        private class TupleList<T1, T2> : List<Tuple<T1, T2>>
        {
            public void Add(T1 item, T2 item2)
            {
                this.Add(new Tuple<T1, T2>(item, item2));
            }
        }

        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            if (!this.kinectSensor.IsAvailable)
            {
                this.StatusText = SensorNotAvailableStatusText;
            }
            else
            {
                this.StatusText = RunningStatusText;
            }
        }
    }
}
