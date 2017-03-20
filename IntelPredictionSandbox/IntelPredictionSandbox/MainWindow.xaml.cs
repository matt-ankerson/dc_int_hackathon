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
using Microsoft.Win32;
using System.IO;

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

        public MainWindow()
        {
            InitializeComponent();

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

            //device device = iothub.instance.adddeviceasync("bed1").result;
            //deviceclient = deviceclient.create(iothub.instance.hostname, new deviceauthenticationwithregistrysymmetrickey("bed1", device.authentication.symmetrickey.primarykey), microsoft.azure.devices.client.transporttype.mqtt);
            //senddata(10, 10, 10);

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

                UpdateUI(colorBitmap);

                colorBitmap.Dispose();
                sample.color.ReleaseAccess(colorData);
                senseManager.ReleaseFrame();

                //Thread.Sleep(200);
            }
        }

        private void ProcessingDepthThread()
        {
            PXCMCapture.Sample sample;
            PXCMImage.ImageData depthData;
            Bitmap colorBitmap;

            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                sample = senseManager.QuerySample();
                sample.depth.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_DEPTH, out depthData);
                colorBitmap = depthData.ToBitmap(0, sample.depth.info.width, sample.depth.info.height);

                var image = depthData.ToBitmap(0, sample.depth.info.width, sample.depth.info.height);

                //UpdateUI(image);

                colorBitmap.Dispose();
                sample.depth.ReleaseAccess(depthData);
                senseManager.ReleaseFrame();

                //Thread.Sleep(200);
            }
        }

        private void UpdateUI(Bitmap bitmap)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
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
                    Feed.Source = IntelPredictionSandbox.ConvertBitmap.BitmapToBitmapSource(bitmap);
                }
            }));
        }

        private async void SendData(double x, double y, double z)
        {
            var point = new
            {
                x = x,
                y = y,
                z = z
            };

            var messageString = JsonConvert.SerializeObject(point);
            var message = new Microsoft.Azure.Devices.Client.Message(Encoding.ASCII.GetBytes(messageString));

            await deviceClient.SendEventAsync(message);
        }
    }
}
