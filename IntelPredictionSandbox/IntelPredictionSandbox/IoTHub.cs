using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Common.Exceptions;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntelPredictionSandbox
{
    public class IoTHub
    {
        private string hostName = "HealthyBedHub.azure-devices.net";
        private string sharedAccessKeyName = "iothubowner";
        private string sharedAccessKey = File.ReadAllText("../../key.txt");

        public string HostName => hostName;
        public string SharedAccessKeyName => sharedAccessKeyName;
        public string SharedAccessKey => sharedAccessKey;
        public string ConnectionString => nameof(HostName) + "=" + HostName + ";" + nameof(SharedAccessKeyName) + "=" + SharedAccessKeyName + ";" + nameof(SharedAccessKey) + "=" + SharedAccessKey;

        RegistryManager registryManager;

        private static IoTHub instance;
        public static IoTHub Instance { get { instance = instance ?? new IoTHub(); return instance; } }

        private IoTHub()
        {
            registryManager = RegistryManager.CreateFromConnectionString(ConnectionString);
        }

        public async Task<Device> AddDeviceAsync(string deviceId)
        {
            Device device;
            try
            {
                device = await registryManager.AddDeviceAsync(new Device(deviceId));
            }
            catch (DeviceAlreadyExistsException)
            {
                device = await registryManager.GetDeviceAsync(deviceId);
            }
            return device;
        }

        public async Task SendStringToHub(DeviceClient deviceClient, string str)
        {
            var message = new Microsoft.Azure.Devices.Client.Message(Encoding.ASCII.GetBytes(str));
            await deviceClient.SendEventAsync(message);
        }

        public async Task SendImageToBlobStorage(DeviceClient deviceClient, Bitmap image)
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
