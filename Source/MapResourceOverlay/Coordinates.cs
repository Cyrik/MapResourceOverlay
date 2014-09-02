using System;

namespace MapResourceOverlay
{
    public class Coordinates
    {
        private readonly double _latitude;
        private readonly double _longitude;

        public Coordinates(double latitude, double longitude)
        {
            _latitude = latitude;
            _longitude = longitude;
        }

        public double Latitude
        {
            get { return _latitude; }
        }

        public double Longitude
        {
            get { return _longitude; }
        }

        public double Distance(Coordinates other)
        {
            var distLong = Longitude - other.Longitude;
            var distLat = Latitude - other.Latitude;
            return Math.Sqrt(Math.Pow(distLong, 2) + Math.Pow(distLat, 2));
        }

        public override string ToString()
        {
            return "Latitude " + Latitude + ", Longitude" + Longitude;
        }
    }
}