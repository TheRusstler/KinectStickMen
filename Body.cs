using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using WindowsPreview.Kinect;

namespace KinectFaces
{
    public class TupleList<T1, T2> : List<Tuple<T1, T2>>
    {
        public void Add(T1 item, T2 item2)
        {
            this.Add(new Tuple<T1, T2>(item, item2));
        }
    }

    public class Body
    {
        public Color BodyColor { get; set; }

        public FrameworkElement Face { get; set; }

        public KalamanDouble FaceWidth = new KalamanDouble(), FaceHeight = new KalamanDouble();

        public FrameworkElement Smile { get; set; }
        public FrameworkElement PlainMouth { get; set; }


        public FrameworkElement LeftEyeOpen { get; set; }
        public FrameworkElement LeftEyeClosed { get; set; }
        public FrameworkElement RightEyeOpen { get; set; }
        public FrameworkElement RightEyeClosed { get; set; }

        public FrameworkElement HandLeftThumbsUp { get; set; }
        public FrameworkElement HandRightThumbsUp { get; set; }

        public TupleList<JointType, JointType> Bones { get; private set; }
        public Dictionary<Tuple<JointType, JointType>, Line> BoneLines { get; private set; }

        private FrameworkElement GetHand(bool isLeft)
        {
            var vb = new Viewbox();
            vb.Width = 50;
            vb.Height = 50;

            var canvas = new Canvas();
            canvas.Style = Application.Current.Resources["ThumbsUpCanvas"] as Style;

            var path = new Path();
            path.Fill = new SolidColorBrush(BodyColor);

            if (isLeft)
            {
                path.Style = Application.Current.Resources["ThumbsUpRight"] as Style;
            }
            else
            {
                path.Style = Application.Current.Resources["ThumbsUpLeft"] as Style;
            }

            canvas.Children.Add(path);
            vb.Child = canvas;
            vb.Visibility = Visibility.Collapsed;

            return vb;
        }

        public FrameworkElement GetFace()
        {
            var vb = new Viewbox();
            vb.Width = 50;
            vb.Height = 50;

            var canvas = new Canvas();
            canvas.Style = Application.Current.Resources["FaceCanvas"] as Style;

            var faceBorder = new Ellipse();
            faceBorder.Stroke = new SolidColorBrush(BodyColor);
            faceBorder.Style = Application.Current.Resources["FaceBorder"] as Style;

            canvas.Children.Add(faceBorder);
            vb.Child = canvas;
            vb.Visibility = Visibility.Collapsed;

            return vb;
        }

        public FrameworkElement GetEye(bool isOpen, bool isLeft)
        {
            var canvas = (this.Face as Viewbox).Child as Canvas;

            Shape eye = isOpen ? new Ellipse() as Shape : new Rectangle() as Shape;
            
            eye.Stroke = new SolidColorBrush(BodyColor);

            eye.Style = isOpen ? Application.Current.Resources["EyeOpen"] as Style : Application.Current.Resources["EyeClosed"] as Style;
            eye.Visibility = Visibility.Collapsed;

            var eyeSpacing = 150;
            if (isLeft)
            {
                Canvas.SetLeft(eye, eyeSpacing);
            }
            else
            {
                Canvas.SetLeft(eye, canvas.Width - eyeSpacing);
            }

            canvas.Children.Add(eye);
            return eye;
        }

        public FrameworkElement GetMouth(bool isSmile)
        {
            var canvas = (this.Face as Viewbox).Child as Canvas;
            Shape mouth = new Path();
            mouth.Style = isSmile ? Application.Current.Resources["Smile"] as Style : Application.Current.Resources["PlainMouth"] as Style;
            mouth.Stroke = new SolidColorBrush(BodyColor);
            canvas.Children.Add(mouth);
            return mouth;
        }

        public void UpdateMouth(bool isSmile)
        {
            Smile.Visibility = isSmile ? Visibility.Visible : Visibility.Collapsed;
            PlainMouth.Visibility = !isSmile ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateEye(bool isOpen, bool isLeft)
        {
            if (isLeft)
            {
                this.LeftEyeClosed.Visibility = !isOpen ? Visibility.Visible : Visibility.Collapsed;
                this.LeftEyeOpen.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                this.RightEyeClosed.Visibility = !isOpen ? Visibility.Visible : Visibility.Collapsed;
                this.RightEyeOpen.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Body(Color bodyColor)
        {
            this.BodyColor = bodyColor;

            this.HandLeftThumbsUp = GetHand(true);
            this.HandRightThumbsUp = GetHand(false);
            this.Face = GetFace();
            this.Smile = GetMouth(true);
            this.PlainMouth = GetMouth(false);
            this.LeftEyeOpen = GetEye(true, true);
            this.LeftEyeClosed = GetEye(false, true);
            this.RightEyeOpen = GetEye(true, false);
            this.RightEyeClosed = GetEye(false, false);

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
                    //{ JointType.HandRight, JointType.HandTipRight },
                    //{ JointType.WristRight, JointType.ThumbRight },

                    // Left Arm
                    { JointType.ShoulderLeft, JointType.ElbowLeft },
                    { JointType.ElbowLeft, JointType.WristLeft },
                    { JointType.WristLeft, JointType.HandLeft },
                    //{ JointType.HandLeft, JointType.HandTipLeft },
                    //{ JointType.WristLeft, JointType.ThumbLeft },

                    // Right Leg
                    { JointType.HipRight, JointType.KneeRight },
                    { JointType.KneeRight, JointType.AnkleRight },
                    { JointType.AnkleRight, JointType.FootRight },
                
                    // Left Leg
                    { JointType.HipLeft, JointType.KneeLeft },
                    { JointType.KneeLeft, JointType.AnkleLeft },
                    { JointType.AnkleLeft, JointType.FootLeft },
                };

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
}
