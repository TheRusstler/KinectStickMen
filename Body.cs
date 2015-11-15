﻿using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
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
}
