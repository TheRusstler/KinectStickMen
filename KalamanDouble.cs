using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectFaces
{
    public class KalamanDouble
    {
        // Simple Kalaman filter from: http://www.dyadica.co.uk/very-simple-kalman-in-c/
        private double Q = 0.000001;
        private double R = 0.0001;
        private double P = 1, X = 0, K;

        private void measurementUpdate()
        {
            K = (P + Q) / (P + Q + R);
            P = R * (P + Q) / (R + P + Q);
        }

        public double Update(double measurement)
        {
            measurementUpdate();
            double result = X + (measurement - X) * K;
            X = result;
            // Debug.WriteLine("Measurement " + result + " y: " + y);
            return result;
        }
    }
}
