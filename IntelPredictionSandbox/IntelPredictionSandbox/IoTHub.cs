using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client.Exceptions;
using System;
using System.Threading.Tasks;

namespace IntelPredictionSandbox
{
    public class IoTHub
    {
        private string hostName = "HealthyBedHub.azure-devices.net";
        private string sharedAccessKeyName = "iothubowner";
        private string sharedAccessKey = "";

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
    }
}
