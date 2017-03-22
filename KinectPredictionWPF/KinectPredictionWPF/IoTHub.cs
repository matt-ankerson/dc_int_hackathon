using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Exceptions;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;

namespace KinectPredictionWPF
{
    public class IoTHub
    {
        private string hostName = "HealthyBedHub.azure-devices.net";
        private string sharedAccessKeyName = "iothubowner";
        private string sharedAccessKey = File.ReadAllLines("../../key.txt")[0];

        public string HostName => hostName;
        public string SharedAccessKeyName => sharedAccessKeyName;
        public string SharedAccessKey => sharedAccessKey;
        public string ConnectionString => nameof(HostName) + "=" + HostName + ";" + 
            nameof(SharedAccessKeyName) + "=" + SharedAccessKeyName + ";" + nameof(SharedAccessKey) + "=" + SharedAccessKey;

        RegistryManager registryManager;
        private Device iotDevice;
        private DeviceClient deviceClient;
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
            catch (Exception ex)
            {
                // There's a chance the device was already registered
                device = await registryManager.GetDeviceAsync(deviceId);
            }

            this.iotDevice = device;

            deviceClient = DeviceClient.Create(HostName, 
                new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, device.Authentication.SymmetricKey.PrimaryKey), Microsoft.Azure.Devices.Client.TransportType.Http1);

            return device;
        }

        public async Task<int> SendStringToHub(string str)
        {
            try
            {
                var message = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(str));
                await deviceClient.SendEventAsync(message);
                return 1;
            }
            catch (Exception ex)
            {
                return 0;
            }
        }
    }
}
