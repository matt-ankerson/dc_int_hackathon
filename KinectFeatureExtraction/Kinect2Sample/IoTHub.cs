//using System;
//using System.IO;
//using System.Threading.Tasks;

//namespace Kinect2Sample
//{
//    public class IoTHub
//    {
//        private string hostName = "HealthyBedHub.azure-devices.net";
//        private string sharedAccessKeyName = "iothubowner";
//        private string sharedAccessKey = ""; // Remove this key!!
//        private string deviceId = "KinectCamera";
//        private string api = "2016-02-03";
//        private string restUri => String.Format("https://{0}.azure-devices.net/devices/{1}/messages/events?api-version={2}", hostName, deviceId, api);

//        private static IoTHub instance;
//        public static IoTHub Instance { get { instance = instance ?? new IoTHub(); return instance; } }

//        private IoTHub()
//        {
//        }

//        public async Task<int> PostMessage(string message)
//        {
//            string sas = $"SharedAccessSignature sr={hostName}.azure-devices.net%2Fdevices%2F{deviceId}&sig={sharedAccessKey}&se=";

//            HttpClient client = new HttpClient();
//            client.DefaultRequestHeaders.Add("Authorization", sas);

//            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
//            var result = client.PostAsync(restUri, content).Result;

//            if (result.StatusCode.ToString() != "204")
//            {
//                Console.WriteLine("Message Failed with code {0}", result.StatusCode.ToString());
//            }
//            else
//            {
//                Console.WriteLine("Success");
//            }
//        }
//    }
//}
