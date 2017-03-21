using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client.Exceptions;
using System;
using System.IO;
using System.Threading.Tasks;

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
            return device;
        }
    }
}
