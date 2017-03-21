using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Drawing;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using System.IO;
using System.Drawing.Imaging;
using Microsoft.Win32;

namespace IntelPredictionSandbox
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string deviceId = "Bed1";

        private PXCMSenseManager senseManager;
        private Thread processingThread;
        private DeviceClient deviceClient;
        private ImageConverter imageConverter;

        public MainWindow()
        {
            InitializeComponent();

            imageConverter = new ImageConverter();

            //senseManager = PXCMSenseManager.CreateInstance();
            //senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_DEPTH, 320, 240, 30);
            //pxcmStatus initStatus = senseManager.Init();
            //if (initStatus == pxcmStatus.PXCM_STATUS_ITEM_UNAVAILABLE)
            //{
            //    // No camera, load data from file...
            //    OpenFileDialog ofd = new OpenFileDialog();
            //    ofd.Filter = "RSSDK clip|*.rssdk|All files|*.*";
            //    ofd.CheckFileExists = true;
            //    ofd.CheckPathExists = true;
            //    bool? result = ofd.ShowDialog();
            //    if (result == true)
            //    {
            //        senseManager.captureManager.SetFileName(ofd.FileName, false);
            //        initStatus = senseManager.Init();
            //    }
            //}
            //if (initStatus < pxcmStatus.PXCM_STATUS_NO_ERROR)
            //{
            //    throw new Exception(String.Format("Init failed: {0}", initStatus));
            //}

            // Register the device
            //Device device = IoTHub.Instance.AddDeviceAsync(deviceId).Result;
            //deviceClient = DeviceClient.Create(IoTHub.Instance.HostName, new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, device.Authentication.SymmetricKey.PrimaryKey), Microsoft.Azure.Devices.Client.TransportType.Mqtt);
            var primaryKey = "Yif0xNK5SFGxb02e3aW+J3vgyFv5TDKIKTIQa+sW4AU=";
            deviceClient = DeviceClient.Create(IoTHub.Instance.HostName, new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, primaryKey), Microsoft.Azure.Devices.Client.TransportType.Http1);

            // Begin processing and uploading data
            processingThread = new Thread(new ThreadStart(ProcessingDepthThread));
            processingThread.Start();
        }

        private void ProcessingDepthThread()
        {
            PXCMCapture.Sample sample;
            PXCMImage.ImageData imageData;
            PXCMImage.ImageData previousImageData = null;
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                sample = senseManager.QuerySample();
                sample.depth.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_DEPTH, out imageData);

                if (previousImageData != null)
                {
                    var diff = ThresholdDepth(previousImageData, imageData, sample);
                    //UpdateUI(diff);
                    SendData(diff);
                    diff.Dispose();
                }
                previousImageData = imageData;

                sample.depth.ReleaseAccess(imageData);
                senseManager.ReleaseFrame();

                //Thread.Sleep(200); // In future, do once for every time interval
                break; // Only do this once for now
            }
        }

        private Bitmap ThresholdDepth(PXCMImage.ImageData previousDepthData, PXCMImage.ImageData depthData, PXCMCapture.Sample sample)
        {
            var size = sample.depth.info.width * sample.depth.info.height;
            var oldValues = new int[size];
            var newValues = new int[size];
            var oldDepthValues = previousDepthData.ToIntArray(0, oldValues);
            var newDepthValues = depthData.ToIntArray(0, newValues);

            var diff = new int[size];
            for (var i = 0; i < size; i++)
            {
                diff[i] = Math.Abs(oldDepthValues[i] - newDepthValues[i]);
            }

            var image = (Bitmap) imageConverter.ConvertFrom(diff);
            return image;
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

        private async void SendData(Bitmap image)
        {
            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Jpeg);
                ms.Position = 0;
                await deviceClient.UploadToBlobAsync(DateTime.Now.ToString("yyyyMMddHHmmss") + ".jpg", ms);
            }
        }
    }
}
