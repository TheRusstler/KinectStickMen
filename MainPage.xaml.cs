using Microsoft.Kinect.Face;
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

        #region Constants

        readonly string NoSensorStatusText = "No ready Kinect found!";
        readonly string RunningStatusText = "Running";
        readonly string SensorNotAvailableStatusText = "Kinect not available!";

        readonly double HighConfidenceHandSize = 40;
        readonly double LowConfidenceHandSize = 20;

        readonly double TrackedBoneThickness = 4.0;
        readonly double InferredBoneThickness = 1.0;
        readonly float InferredZPositionClamp = 0.1f;

        #endregion


        #region Properties

        KinectSensor kinectSensor = null;

        CoordinateMapper coordinateMapper = null;
        BodyFrameReader bodyFrameReader = null;

        WindowsPreview.Kinect.Body[] bodies = null;

        Canvas drawingCanvas;

        Body[] BodyInfos;

        FaceFrameSource[] faceFrameSources = null;
        FaceFrameReader[] faceFrameReaders = null;
        FaceFrameResult[] faceFrameResults = null;

        int displayWidth, displayHeight;

        List<Color> BodyColors;

        public int BodyCount
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

        #endregion


        #region Dependency Properties

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

        #endregion


        public MainPage()
        {
            this.kinectSensor = KinectSensor.GetDefault();
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

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

            this.BodyCount = this.kinectSensor.BodyFrameSource.BodyCount;


            // Face tracking
            // specify the required face frame results
            FaceFrameFeatures faceFrameFeatures =
                FaceFrameFeatures.BoundingBoxInColorSpace
                | FaceFrameFeatures.PointsInColorSpace
                | FaceFrameFeatures.RotationOrientation
                | FaceFrameFeatures.FaceEngagement
                | FaceFrameFeatures.Glasses
                | FaceFrameFeatures.Happy
                | FaceFrameFeatures.LeftEyeClosed
                | FaceFrameFeatures.RightEyeClosed
                | FaceFrameFeatures.LookingAway
                | FaceFrameFeatures.MouthMoved
                | FaceFrameFeatures.MouthOpen;

            // create a face frame source + reader to track each face in the FOV
            this.faceFrameSources = new FaceFrameSource[this.BodyCount];
            this.faceFrameReaders = new FaceFrameReader[this.BodyCount];
            for (int i = 0; i < this.BodyCount; i++)
            {
                this.faceFrameSources[i] = new FaceFrameSource(this.kinectSensor, 0, faceFrameFeatures);
                this.faceFrameReaders[i] = this.faceFrameSources[i].OpenReader();
            }

            this.faceFrameResults = new FaceFrameResult[this.BodyCount];



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


        #region Events

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < this.BodyCount; i++)
            {
                if (this.faceFrameReaders[i] != null)
                {
                    this.faceFrameReaders[i].FrameArrived += this.Reader_FaceFrameArrived;
                }
            }

            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;
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
                for (int i = 0; i < BodyCount; i++)
                {
                    if (bodies[i] != null)
                    {
                        bodies[i].Dispose();
                    }


                    if (this.faceFrameReaders[i] != null)
                    {
                        this.faceFrameReaders[i].Dispose();
                        this.faceFrameReaders[i] = null;
                    }

                    if (this.faceFrameSources[i] != null)
                    {
                        this.faceFrameSources[i] = null;
                    }
                }
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
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
                for (int bodyIndex = 0; bodyIndex < this.bodies.Length; bodyIndex++)
                {
                    WindowsPreview.Kinect.Body body = this.bodies[bodyIndex];

                    if (body.IsTracked)
                    {
                        this.UpdateBody(body, bodyIndex);
                    }
                    else
                    {
                        this.ClearBody(bodyIndex);
                    }
                }
            }
        }

        private void Reader_FaceFrameArrived(object sender, FaceFrameArrivedEventArgs e)
        {
            using (FaceFrame faceFrame = e.FrameReference.AcquireFrame())
            {
                if (faceFrame != null)
                {
                    // get the index of the face source from the face source array
                    int index = this.GetFaceSourceIndex(faceFrame.FaceFrameSource);

                    // check if this face frame has valid face frame results
                    if (this.ValidateFaceBoxAndPoints(faceFrame.FaceFrameResult))
                    {
                        // store this face frame result to draw later
                        this.faceFrameResults[index] = faceFrame.FaceFrameResult;
                    }
                    else
                    {
                        // indicates that the latest face frame result from this reader is invalid
                        this.faceFrameResults[index] = null;
                    }
                }
            }
        }

        #endregion

        //private void DrawFace(FrameworkElement face, )
        //{
        //    CoordinateMapper coordinateMapper = this.kinectSensor.CoordinateMapper;

        //    Brush drawingBrush = new SolidColorBrush(this.BodyColors[0]);

        //    //var facePoints = faceResult.;
        //    //;
        //    //CameraSpacePoint point = facePoints[FacePointType.Nose].
        //    //faceResult.FacePointsInColorSpace

        //    //CameraSpacePoint cameraPoint = new CameraSpacePoint();
        //    //cameraPoint.X = faceBox.Right - faceBox.Left;
        //    //cameraPoint.Y = faceBox.Top - faceBox.Bottom;
        //    //faceBox.

        //    //DepthSpacePoint depthSpacePoint = coordinateMapper.MapCameraPointToDepthSpace(.;


        //    //var rect = new Rectangle();
        //    //rect.Fill = drawingBrush;
        //    //rect.Width = faceBox.Right - faceBox.Left;
        //    //rect.Height = faceBox.Bottom - faceBox.Top;
        //    //rect.Margin = new Thickness(faceBox.Left, faceBox.Top, 0, 0);
        //    //this.drawingCanvas.Children.Add(rect);
        //}

        private void DrawFace(FrameworkElement face, FaceFrameResult faceFrameResult, Point point)
        {
            face.Visibility = Visibility.Visible;
        }

        private void UpdateBody(WindowsPreview.Kinect.Body body, int bodyIndex)
        {
            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
            var jointPointsInDepthSpace = new Dictionary<JointType, Point>();

            var bodyInfo = this.BodyInfos[bodyIndex];

            CoordinateMapper coordinateMapper = this.kinectSensor.CoordinateMapper;

            foreach (var jointType in body.Joints.Keys)
            {
                CameraSpacePoint position = body.Joints[jointType].Position;
                if (position.Z < 0)
                {
                    position.Z = InferredZPositionClamp;
                }

                DepthSpacePoint depthSpacePoint = coordinateMapper.MapCameraPointToDepthSpace(position);
                jointPointsInDepthSpace[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);

                if (jointType == JointType.Head)
                {
                    // Face
                    if (this.faceFrameSources[bodyIndex].IsTrackingIdValid)
                    {
                        if (this.faceFrameResults[bodyIndex] != null)
                        {
                            this.UpdateHead(bodyInfo.Face, bodyInfo, this.faceFrameResults[bodyIndex], jointPointsInDepthSpace[jointType]);
                        }
                        else
                        {
                            this.UpdateHead(bodyInfo.Face, bodyInfo, null, jointPointsInDepthSpace[jointType]);
                        }
                    }
                    else
                    {
                        if (this.bodies[bodyIndex].IsTracked)
                        {
                            this.faceFrameSources[bodyIndex].TrackingId = this.bodies[bodyIndex].TrackingId;
                        }
                        this.UpdateHead(bodyInfo.Face, bodyInfo, null, jointPointsInDepthSpace[jointType]);
                    }
                }

                if (jointType == JointType.HandRight)
                {
                    this.UpdateHand(bodyInfo.HandLeftThumbsUp, body.HandRightState, body.HandRightConfidence, jointPointsInDepthSpace[jointType]);
                }

                if (jointType == JointType.HandLeft)
                {
                    this.UpdateHand(bodyInfo.HandRightThumbsUp, body.HandLeftState, body.HandLeftConfidence, jointPointsInDepthSpace[jointType]);
                }
            }

            foreach (var bone in bodyInfo.Bones)
            {
                this.UpdateBone(bodyInfo.BoneLines[bone], joints[bone.Item1], joints[bone.Item2],
                                jointPointsInDepthSpace[bone.Item1],
                                jointPointsInDepthSpace[bone.Item2]);
            }
        }

        private void UpdateHead(FrameworkElement face, Body body, FaceFrameResult faceResult, Point point)
        {
            face.Visibility = Visibility.Visible;

            if(faceResult != null)
            {
                var box = faceResult.FaceBoundingBoxInColorSpace;
                face.Width = body.FaceWidth.Update(box.Right - box.Left);
                face.Height = body.FaceHeight.Update(box.Bottom - box.Top);
            }

            if (!Double.IsInfinity(point.X) && !Double.IsInfinity(point.Y))
            {
                Canvas.SetLeft(face, point.X - face.Width / 2  +20);
                Canvas.SetTop(face, point.Y - face.Height / 2);
            }
        }

        private void UpdateHand(FrameworkElement thumbsUp, HandState handState, TrackingConfidence trackingConfidence, Point point)
        {
            thumbsUp.Visibility = (handState == HandState.Lasso) ? Visibility.Visible : Visibility.Collapsed;

            if (!Double.IsInfinity(point.X) && !Double.IsInfinity(point.Y))
            {
                Canvas.SetLeft(thumbsUp, point.X - thumbsUp.Width / 2);
                Canvas.SetTop(thumbsUp, point.Y - thumbsUp.Height / 2);
            }
        }

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


        #region Utils

        private void PopulateVisualObjects()
        {
            foreach (var bodyInfo in this.BodyInfos)
            {
                this.drawingCanvas.Children.Add(bodyInfo.Face);
                this.drawingCanvas.Children.Add(bodyInfo.HandLeftThumbsUp);
                this.drawingCanvas.Children.Add(bodyInfo.HandRightThumbsUp);

                foreach (var bone in bodyInfo.Bones)
                {
                    this.drawingCanvas.Children.Add(bodyInfo.BoneLines[bone]);
                }
            }
        }

        private int GetFaceSourceIndex(FaceFrameSource faceFrameSource)
        {
            int index = -1;

            for (int i = 0; i < this.BodyCount; i++)
            {
                if (this.faceFrameSources[i] == faceFrameSource)
                {
                    index = i;
                    break;
                }
            }

            return index;
        }

        private bool ValidateFaceBoxAndPoints(FaceFrameResult faceResult)
        {
            bool isFaceValid = faceResult != null;
            return isFaceValid;
        }

        private void ClearBody(int bodyIndex)
        {
            var bodyInfo = this.BodyInfos[bodyIndex];

            foreach (var bone in bodyInfo.Bones)
            {
                bodyInfo.BoneLines[bone].Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            bodyInfo.Face.Visibility = Visibility.Collapsed;
            bodyInfo.HandLeftThumbsUp.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            bodyInfo.HandRightThumbsUp.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        #endregion
    }
}
