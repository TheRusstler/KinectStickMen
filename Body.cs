using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
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

            if(isLeft)
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

        public Body(Color bodyColor)
        {
            this.BodyColor = bodyColor;

            this.HandLeftThumbsUp = GetHand(true);
            this.HandRightThumbsUp = GetHand(false);

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
