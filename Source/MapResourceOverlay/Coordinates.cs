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
    }
}