using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using System.ComponentModel;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;

namespace KinectPredictionWPF
{
    public enum DisplayFrameType
    {
        Infrared,
        Color,
        Depth
    }

    public class DataPoint
    {
        public Coordinate nearest { get; set; }
        public Coordinate farthest { get; set; }
        public double averageDepth { get; set; }

        public DateTime timeStamp { get; set; }

        public bool outOfBed { get; set; }
    }

    public class Coordinate
    {
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const DisplayFrameType DEFAULT_DISPLAYFRAMETYPE = DisplayFrameType.Depth;
        private FrameDescription currentFrameDescription;
        private DisplayFrameType currentDisplayFrameType;
        private string statusText = null;
        public event PropertyChangedEventHandler PropertyChanged;


        public string StatusText
        {
            get { return this.statusText; }
            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        public FrameDescription CurrentFrameDescription
        {
            get { return this.currentFrameDescription; }
            set
            {
                if (this.currentFrameDescription != value)
                {
                    this.currentFrameDescription = value;
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("CurrentFrameDescription"));
                    }
                }
            }
        }

        private const float InfraredSourceValueMaximum = (float)ushort.MaxValue;
        private const float InfraredOutputValueMinimum = 0.01f;
        private const float InfraredOutputValueMaximum = 1.0f;
        private const float InfraredSceneValueAverage = 0.08f;
        private const float InfraredSceneStandardDeviations = 3.0f;


        // Size of the RGB pixel in the bitmap
        private const int BytesPerPixel = 4;

        private KinectSensor kinectSensor = null;
        private WriteableBitmap bitmap = null;
        private MultiSourceFrameReader multiSourceFrameReader = null;

        // Infrared Frame
        private ushort[] infraredFrameData = null;
        private byte[] infraredPixels = null;

        // Colour Frame
        private ushort[] colourFrameData = null;
        private byte[] colourPixels = null;

        // Depth Frame
        // Depth and Infrared could be used together to remove background.
        private ushort[] depthFrameData = null;
        private byte[] depthPixels = null;

        // Depth frame analysis
        private ushort averageDistance = 0;
        private DataPoint depthDataPoint = new DataPoint { outOfBed = false };

        private int framesProcessed = 0;

        public DataPoint DepthDataPoint
        {
            get { return this.depthDataPoint; }
            set
            {
                if (this.depthDataPoint != value)
                {
                    this.depthDataPoint = value;
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("DepthDataPoint"));
                    }
                }
            }
        }

        public ushort AverageDistance
        {
            get { return this.averageDistance; }
            set
            {
                if (this.averageDistance != value)
                {
                    this.averageDistance = value;
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("AverageDistance"));
                    }
                }
            }
        }

        // IoT Hub information
        private DeviceClient deviceClient;
        // private Thread processingThread;

        public MainWindow()
        {
            // Get the Kinect Sensor (only 1 is supported):
            kinectSensor = KinectSensor.GetDefault();

            SetupCurrentDisplay(DEFAULT_DISPLAYFRAMETYPE);

            this.multiSourceFrameReader = this.kinectSensor.OpenMultiSourceFrameReader(
                FrameSourceTypes.Infrared | 
                FrameSourceTypes.Color | 
                FrameSourceTypes.Depth);

            this.multiSourceFrameReader.MultiSourceFrameArrived += this.Reader_MultiSourceFrameArrived;

            // set IsAvailableChanged event notifier
            kinectSensor.IsAvailableChanged += Sensor_IsAvailableChanged;

            // use the window object as the view model in this example
            this.DataContext = this;

            // Open the sensor
            kinectSensor.Open();

            new Thread(InitIoTHubConnection).Start();

            InitializeComponent();
        }
        
        public void InitIoTHubConnection()
        {
            // Init connection to IoT Hub
            var device = IoTHub.Instance.AddDeviceAsync("KinectCamera").Result;
        }

        private void InfraredButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Infrared);
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Color);
        }

        private void DepthButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Depth);
        }

        private void SetupCurrentDisplay(DisplayFrameType newDisplayFrameType)
        {
            currentDisplayFrameType = newDisplayFrameType;

            // Frames used by more than one type are declared outside the switch
            FrameDescription colorFrameDescription = null;

            switch (currentDisplayFrameType)
            {
                case DisplayFrameType.Infrared:
                    FrameDescription infraredFrameDescription = this.kinectSensor.InfraredFrameSource.FrameDescription;
                    this.CurrentFrameDescription = infraredFrameDescription;
                    // allocate space to put the pixels being 
                    // received and converted
                    this.infraredFrameData = new ushort[infraredFrameDescription.Width * infraredFrameDescription.Height];
                    this.infraredPixels = new byte[infraredFrameDescription.Width * infraredFrameDescription.Height * BytesPerPixel];
                    bitmap = new WriteableBitmap(infraredFrameDescription.Width, infraredFrameDescription.Height, 96, 96, PixelFormats.Bgr32, null);
                    break;

                case DisplayFrameType.Color:
                    colorFrameDescription = this.kinectSensor.ColorFrameSource.FrameDescription;
                    this.CurrentFrameDescription = colorFrameDescription;
                    // allocate space to put the pixels being 
                    // received and converted
                    this.colourFrameData = new ushort[colorFrameDescription.Width * colorFrameDescription.Height];
                    this.colourPixels = new byte[colorFrameDescription.Width * colorFrameDescription.Height * BytesPerPixel];
                    this.bitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96, 96, PixelFormats.Bgr32, null);
                    break;

                case DisplayFrameType.Depth:
                    FrameDescription depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
                    this.CurrentFrameDescription = depthFrameDescription;
                    // allocate space to put the pixels being 
                    // received and converted
                    this.depthFrameData = new ushort[depthFrameDescription.Width * depthFrameDescription.Height];
                    this.depthPixels = new byte[depthFrameDescription.Width * depthFrameDescription.Height * BytesPerPixel];
                    this.bitmap = new WriteableBitmap(depthFrameDescription.Width, depthFrameDescription.Height, 96, 96, PixelFormats.Bgr32, null);
                    break;

                default:
                    break;
            }
        }

        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();

            // If the Frame has expired by the time we process this event, return.
            if (multiSourceFrame == null)
            {
                return;
            }

            DepthFrame depthFrame = null;
            ColorFrame colorFrame = null;
            InfraredFrame infraredFrame = null;

            switch (currentDisplayFrameType)
            {
                case DisplayFrameType.Infrared:
                    using (infraredFrame = multiSourceFrame.InfraredFrameReference.AcquireFrame())
                    {
                        ShowInfraredFrame(infraredFrame);
                    }
                    break;
                case DisplayFrameType.Color:
                    using (colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame())
                    {
                        ShowColorFrame(colorFrame);
                    }
                    break;
                case DisplayFrameType.Depth:
                    using (depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame())
                    {
                        ShowDepthFrame(depthFrame);
                    }
                    break;
                default:
                    break;
            }
        }

        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs args)
        {
            this.StatusText = this.kinectSensor.IsAvailable ?
                 "Running" : "Not Available";
        }

        private void ShowInfraredFrame(InfraredFrame infraredFrame)
        {
            bool infraredFrameProcessed = false;

            if (infraredFrame != null)
            {
                FrameDescription infraredFrameDescription = infraredFrame.FrameDescription;

                // verify data and write the new infrared frame data to the display bitmap
                if (((infraredFrameDescription.Width * infraredFrameDescription.Height)
                == this.infraredFrameData.Length) &&
                    (infraredFrameDescription.Width == this.bitmap.PixelWidth) &&
                (infraredFrameDescription.Height == this.bitmap.PixelHeight))
                {
                    // Copy the pixel data from the image to a temporary array
                    infraredFrame.CopyFrameDataToArray(this.infraredFrameData);

                    infraredFrameProcessed = true;
                }
            }

            // we got a frame, convert and render
            if (infraredFrameProcessed)
            {
                this.ConvertInfraredDataToPixels();
                this.RenderPixelArray(this.infraredPixels);
            }
        }

        private void ShowColorFrame(ColorFrame colorFrame)
        {
            bool colorFrameProcessed = false;

            if (colorFrame != null)
            {
                FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                // verify data and write the new color frame data to 
                // the Writeable bitmap
                if ((colorFrameDescription.Width == this.bitmap.PixelWidth) &&
                    (colorFrameDescription.Height == this.bitmap.PixelHeight))
                {
                    if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
                    {
                        // Copy the pixel data from the image to a temporary array
                        colorFrame.CopyRawFrameDataToArray(this.colourPixels);
                    }
                    else
                    {
                        colorFrame.CopyConvertedFrameDataToArray(this.colourPixels, ColorImageFormat.Bgra);
                    }

                    colorFrameProcessed = true;
                }
            }

            if (colorFrameProcessed)
            {
                RenderPixelArray(this.colourPixels);
            }
        }

        private void ShowDepthFrame(DepthFrame depthFrame)
        {
            bool depthFrameProcessed = false;
            ushort minDepth = 0;
            ushort maxDepth = 0;

            if (depthFrame != null)
            {
                FrameDescription depthFrameDescription = depthFrame.FrameDescription;

                // Verify data and write the new infrared frame data to the display bitmap
                if (((depthFrameDescription.Width * depthFrameDescription.Height) == this.depthFrameData.Length) &&
                    (depthFrameDescription.Width == this.bitmap.PixelWidth) &&
                    (depthFrameDescription.Height == this.bitmap.PixelHeight))
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyFrameDataToArray(this.depthFrameData);

                    minDepth = depthFrame.DepthMinReliableDistance;
                    maxDepth = depthFrame.DepthMaxReliableDistance;

                    ExtractDepthMetrics(depthFrameDescription.Width, depthFrameDescription.Height);

                    depthFrameProcessed = true;
                }
            }

            // we got a frame, convert and render
            if (depthFrameProcessed)
            {
                ConvertDepthDataToPixels(minDepth, maxDepth);
                RenderPixelArray(this.depthPixels);
            }
        }

        private void ExtractDepthMetrics(int frameWidth, int frameHeight)
        {
            // Get indexes of nearest and farthest values.
            // This allows us to get the values, and the pixel coordinates.
            int nearestIndex = Array.IndexOf(this.depthFrameData, this.depthFrameData.Where(x => x > 0).Min());
            int farthestIndex = Array.IndexOf(this.depthFrameData, this.depthFrameData.Max());

            DepthDataPoint = new DataPoint
            {
                averageDepth = (ushort)this.depthFrameData.Select(x => (double)x).Average(),
                timeStamp = DateTime.UtcNow,
                outOfBed = DepthDataPoint.outOfBed,
                nearest = new Coordinate
                {
                    x = (nearestIndex % frameWidth) / (double)frameWidth,
                    y = (nearestIndex / frameHeight) / (double)frameHeight,
                    z = this.depthFrameData[nearestIndex]
                },
                farthest = new Coordinate
                {
                    x = (farthestIndex % frameWidth) / (double)frameWidth,
                    y = (farthestIndex / frameHeight) / (double)frameHeight,
                    z = this.depthFrameData[farthestIndex]
                }
            };

            // Push data point to IoT Hub (every 10th frame)
            if (this.framesProcessed % 30 == 0)
            {
                // Launch a new thread to do push out a message.
                new Thread(SendDataPoint).Start();
            }

            // Don't let the integer count overflow
            if (this.framesProcessed > 1000)
            {
                this.framesProcessed = 0;
            }
        }

        private void SendDataPoint()
        {
            var messagePayload = JsonConvert.SerializeObject(this.DepthDataPoint);
            var sendResult = IoTHub.Instance.SendStringToHub(messagePayload).Result;
        }

        /// <summary>
        /// This method results in a byte array of colour data stored in the infraredPixels[] class variable.
        /// </summary>
        private void ConvertInfraredDataToPixels()
        {
            // Convert the infrared to RGB
            int colorPixelIndex = 0;
            for (int i = 0; i < this.infraredFrameData.Length; ++i)
            {
                // normalize the incoming infrared data (ushort) to 
                // a float ranging from InfraredOutputValueMinimum
                // to InfraredOutputValueMaximum] by

                // 1. dividing the incoming value by the 
                // source maximum value
                float intensityRatio = (float)this.infraredFrameData[i] / InfraredSourceValueMaximum;

                // 2. dividing by the 
                // (average scene value * standard deviations)
                intensityRatio /= InfraredSceneValueAverage * InfraredSceneStandardDeviations;

                // 3. limiting the value to InfraredOutputValueMaximum
                intensityRatio = Math.Min(InfraredOutputValueMaximum, intensityRatio);

                // 4. limiting the lower value InfraredOutputValueMinimum
                intensityRatio = Math.Max(InfraredOutputValueMinimum, intensityRatio);

                // 5. converting the normalized value to a byte and using 
                // the result as the RGB components required by the image
                byte intensity = (byte)(intensityRatio * 255.0f);
                this.infraredPixels[colorPixelIndex++] = intensity; //Blue
                this.infraredPixels[colorPixelIndex++] = intensity; //Green
                this.infraredPixels[colorPixelIndex++] = intensity; //Red
                this.infraredPixels[colorPixelIndex++] = 255;       //Alpha           
            }
        }

        private void ConvertDepthDataToPixels(ushort minDepth, ushort maxDepth)
        {
            int colorPixelIndex = 0;
            // Shape the depth to the range of a byte
            int mapDepthToByte = maxDepth / 256;

            for (int i = 0; i < this.depthFrameData.Length; ++i)
            {
                // Get the depth for this pixel
                ushort depth = this.depthFrameData[i];

                // To convert to a byte, we're mapping the depth value
                // to the byte range.
                // Values outside the reliable depth range are 
                // mapped to 0 (black).
                byte intensity = (byte)(depth >= minDepth &&
                    depth <= maxDepth ? (depth / mapDepthToByte) : 0);

                this.depthPixels[colorPixelIndex++] = intensity; //Blue
                this.depthPixels[colorPixelIndex++] = intensity; //Green
                this.depthPixels[colorPixelIndex++] = intensity; //Red
                this.depthPixels[colorPixelIndex++] = 255; //Alpha
            }
        }

        /// <summary>
        /// Push pixel data into class level bitmap image.
        /// ConvertInfraredDataToPixels() needs to happen before this.
        /// </summary>
        /// <param name="pixels"></param>
        private void RenderPixelArray(byte[] pixels)
        {
            var width = this.CurrentFrameDescription.Width;
            var height = this.CurrentFrameDescription.Height;
            var stride = this.CurrentFrameDescription.Width * BytesPerPixel;

            var sourceRect = new Int32Rect(0, 0, width, height);

            this.bitmap.WritePixels(sourceRect, pixels, stride, 0);

            // Update the display's bitmap image.
            FrameDisplayImage.Source = this.bitmap;
        }

        private async void SendData(Bitmap image)
        {
            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Jpeg);
                ms.Position = 0;
                // Why are we pushing to blob storage here?
                await deviceClient.UploadToBlobAsync(DateTime.Now.ToString("yyyyMMddHHmmss") + ".jpg", ms);
            }
        }
    }
}
