using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.Windows.Interop;
using Microsoft.Azure.Devices;
using Newtonsoft.Json;
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
        private PXCMSenseManager senseManager;
        private Thread processingThread;
        private DeviceClient deviceClient;
        private ImageConverter imageConverter;

        public MainWindow()
        {
            InitializeComponent();


            imageConverter = new ImageConverter();

            senseManager = PXCMSenseManager.CreateInstance();
            //senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, 320, 240, 60);
            senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_DEPTH, 320, 240, 30);

            pxcmStatus initStatus = senseManager.Init();

            if (initStatus == pxcmStatus.PXCM_STATUS_ITEM_UNAVAILABLE)
            {
                // No camera, load data from file...
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "RSSDK clip|*.rssdk|All files|*.*";
                ofd.CheckFileExists = true;
                ofd.CheckPathExists = true;
                Nullable<bool> result = ofd.ShowDialog();
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

            Device device = IoTHub.Instance.AddDeviceAsync("bed1").Result;
            deviceClient = DeviceClient.Create(IoTHub.Instance.HostName, new DeviceAuthenticationWithRegistrySymmetricKey("bed1", device.Authentication.SymmetricKey.PrimaryKey), Microsoft.Azure.Devices.Client.TransportType.Http1);

            processingThread = new Thread(new ThreadStart(ProcessingDepthThread));
            processingThread.Start();
        }

        private void ProcessingRGBThread()
        {
            PXCMCapture.Sample sample;
            PXCMImage.ImageData colorData;
            Bitmap colorBitmap;
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                sample = senseManager.QuerySample();
                sample.color.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB24, out colorData);
                colorBitmap = colorData.ToBitmap(0, sample.color.info.width, sample.color.info.height);

                //UpdateUI(colorBitmap);

                colorBitmap.Dispose();
                sample.color.ReleaseAccess(colorData);
                senseManager.ReleaseFrame();

                //Thread.Sleep(200);
            }
        }

        private void ProcessingDepthThread()
        {
            PXCMCapture.Sample sample;
            PXCMImage.ImageData imageData;
            //PXCMImage.ImageData previousImageData = null;
            //Bitmap bitmap;
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                sample = senseManager.QuerySample();
                sample.depth.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_DEPTH, out imageData);
                //bitmap = imageData.ToBitmap(0, sample.depth.info.width, sample.depth.info.height);

                var image = imageData.ToBitmap(0, sample.depth.info.width, sample.depth.info.height);

                //if (previousImageData != null)
                //{
                    //var diff = ThresholdDepth(previousImageData, imageData, sample);
                    //UpdateUI(diff);
                    SendData(image);
                //}
                //previousImageData = imageData;

                //bitmap.Dispose();
                sample.depth.ReleaseAccess(imageData);
                senseManager.ReleaseFrame();

                //Thread.Sleep(200);
            }
        }

        private Bitmap ThresholdDepth(PXCMImage.ImageData previousDepthData, PXCMImage.ImageData depthData, PXCMCapture.Sample sample)
        {
            int[] oldValues = new int[sample.depth.info.width * sample.depth.info.height];
            int[] newValues = new int[sample.depth.info.width * sample.depth.info.height];
            var oldDepthValues = previousDepthData.ToIntArray(0, oldValues);
            var newDepthValues = depthData.ToIntArray(0, newValues);

            //int[] diff = 

            var image = (Bitmap) imageConverter.ConvertFrom(newDepthValues);
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
            //var messageString = JsonConvert.SerializeObject(str);
            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Jpeg);
                var message = new Microsoft.Azure.Devices.Client.Message(ms);
                await deviceClient.SendEventAsync(message);
            }
        }
    }
}
