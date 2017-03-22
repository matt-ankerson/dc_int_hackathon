using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Drawing;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using System.IO;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace IntelPredictionSandbox
{
    public partial class MainWindow : Window
    {
        private const string deviceId = "Bed1";

        private PXCMSenseManager senseManager;
        private DeviceClient deviceClient;
        private ImageConverter imageConverter;

        public bool OutOfBed { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            imageConverter = new ImageConverter();

            InitVideoStream();

            Thread processingThread = new Thread(new ThreadStart(ProcessingThread));
            processingThread.Start();

            //Blob();
        }

        private void InitVideoStream()
        {
            senseManager = PXCMSenseManager.CreateInstance();
            senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_DEPTH, 320, 240, 30);
            pxcmStatus initStatus = senseManager.Init();
            if (initStatus == pxcmStatus.PXCM_STATUS_ITEM_UNAVAILABLE)
            {
                // No camera, load data from file...
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "RSSDK clip|*.rssdk|All files|*.*";
                ofd.CheckFileExists = true;
                ofd.CheckPathExists = true;
                bool? result = ofd.ShowDialog();
                if (result == true)
                {
                    senseManager.captureManager.SetFileName(ofd.FileName, false);
                    initStatus = senseManager.Init();
                }
            }
            if (initStatus < pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                throw new Exception(String.Format("Init failed: {0}", initStatus));
            }
        }

        private void ProcessingThread()
        {
            deviceClient = IoTHub.Instance.AddDeviceAsync(deviceId).Result;
            ProcessDepth();
        }

        private void ProcessDepth()
        {
            PXCMCapture.Sample sample;
            PXCMImage.ImageData imageData;
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                sample = senseManager.QuerySample();
                sample.depth.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_DEPTH, out imageData);

                var image = ConvertDepthToBitmap(imageData, sample);

                UpdateUI(image);

                sample.depth.ReleaseAccess(imageData);
                senseManager.ReleaseFrame();

                Thread.Sleep(TimeSpan.FromSeconds(0.5));
            }
        }

        private Bitmap ConvertDepthToBitmap(PXCMImage.ImageData depthData, PXCMCapture.Sample sample)
        {
            Bitmap bmp = new Bitmap(sample.depth.info.width, sample.depth.info.height);

            var size = sample.depth.info.width * sample.depth.info.height;
            Int16[] values = new Int16[size];
            var depthValues = depthData.ToShortArray(0, values);

            var minDistance = 500; // mm
            var maxDistance = 2000; // mm
            float scale = 255.0f / (maxDistance - minDistance);

            var dataPoint = new DataPoint
            {
                nearest = new Coordinate { x = -1, y = -1, z = short.MaxValue },
                farthest = new Coordinate { x = -1, y = -1, z = short.MinValue }
            };
            var i = 0;
            var sum = 0.0;
            var width = sample.depth.info.width;
            var height = sample.depth.info.height;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var distance = depthValues[i++]; // mm
                    var brightness = 0;

                    if (distance > 0 && distance < dataPoint.nearest.z)
                        dataPoint.nearest = new Coordinate { x = (double) x / width, y = (double) y / height, z = distance };
                    if (distance > dataPoint.farthest.z)
                        dataPoint.farthest = new Coordinate { x = (double) x / width, y = (double) y / height, z = distance };
                    sum += distance;

                    if (distance > minDistance && distance < maxDistance)
                    {
                        brightness = 255 - (int)((distance - minDistance) * scale);
                    }
                    System.Drawing.Color color = System.Drawing.Color.FromArgb(brightness, brightness, brightness);
                    bmp.SetPixel(x, y, color);
                }
            }

            if (dataPoint.nearest.z == short.MaxValue)
                dataPoint.nearest = null;
            if (dataPoint.farthest.z == short.MinValue)
                dataPoint.farthest = null;
            dataPoint.averageDepth = sum / size;
            dataPoint.outOfBed = OutOfBed;
            dataPoint.timeStamp = DateTime.UtcNow;
            SendDataPoint(dataPoint);

            return bmp;
        }

        private async Task SendDataPoint(DataPoint dataPoint)
        {
            var messageString = JsonConvert.SerializeObject(dataPoint);
            await IoTHub.Instance.SendStringToHub(deviceClient, messageString);
        }

        private void SaveDataPoint(DataPoint dataPoint)
        {
            var messageString = JsonConvert.SerializeObject(dataPoint);

            string filePath = @"C:\Users\HaydonB\Desktop\points2.txt";
            if (File.Exists(filePath))
                using (StreamWriter sw = File.AppendText(filePath))
                    sw.WriteLine(messageString);
        }

        private void UpdateUI(Bitmap bitmap)
        {
            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
            {
                if (bitmap != null)
                {
                    // Mirror the color stream Image control
                    Feed.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                    ScaleTransform mainTransform = new ScaleTransform();
                    mainTransform.ScaleX = -1;
                    mainTransform.ScaleY = 1;
                    Feed.RenderTransform = mainTransform;

                    // Display the color stream
                    Feed.Source = ConvertBitmap.BitmapToBitmapSource(bitmap);
                }
            }));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OutOfBed = !OutOfBed;
        }

        //private void Blob()
        //{
        //    PXCMBlobModule blobModule = senseManager.QueryBlob();
        //    PXCMBlobConfiguration blobConfiguration = blobModule.CreateActiveConfiguration();
        //    PXCMBlobData blobData = blobModule.CreateOutput();

        //    senseManager.AcquireFrame(true);
        //    PXCMCapture.Sample sample = senseManager.QueryBlobSample();
        //    blobConfiguration.SetBlobSmoothing(1);
        //    blobConfiguration.SetMaxDistance(1500);
        //    blobConfiguration.SetMaxBlobs(1);

        //    blobConfiguration.EnableContourExtraction(true);
        //    blobConfiguration.EnableSegmentationImage(true);
        //    blobConfiguration.ApplyChanges();

        //    blobData.Update();
        //}
    }
}
