using System;

namespace IntelPredictionSandbox
{
    public class DataPoint
    {
        public Coordinate nearest { get; set; }
        public Coordinate farthest { get; set; }
        public double averageDepth { get; set; }

        public DateTimeOffset timeStamp { get; set; }

        public bool isGettingUp { get; set; }
    }

    public class Coordinate
    {
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
    }
}
