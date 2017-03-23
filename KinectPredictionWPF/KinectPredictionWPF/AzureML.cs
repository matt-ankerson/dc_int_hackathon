using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KinectPredictionWPF
{

    public class StringTable
    {
        public string[] ColumnNames { get; set; }
        public string[,] Values { get; set; }
    }

    public class Results
    {
        public Output output1 { get; set; }
    }

    public class Output
    {
        public string type { get; set; }
        public StringTable value { get; set; }
    }

    public class PredictionResponse
    {
        public Results Results { get; set; }
    }

//    {
//  "Results": {
//    "output1": {
//      "type": "DataTable",
//      "value": {
//        "ColumnNames": [
//          "outofbed",
//          "averagedepth",
//          "nearx",
//          "neary",
//          "nearz",
//          "farx",
//          "fary",
//          "farz",
//          "Scored Labels",
//          "Scored Probabilities"
//        ],
//        "ColumnTypes": [
//          "String",
//          "String",
//          "String",
//          "String",
//          "String",
//          "String",
//          "String",
//          "String",
//          "String",
//          "Numeric"
//        ],
//        "Values": [
//          [
//            "value",
//            "value",
//            "value",
//            "value",
//            "value",
//            "value",
//            "value",
//            "value",
//            "value",
//            "0"
//          ],
//          [
//            "value",
//            "value",
//            "value",
//            "value",
//            "value",
//            "value",
//            "value",
//            "value",
//            "value",
//            "0"
//          ]
//        ]
//      }
//    }
//  }
//}

    public static class AzureML
    {
        public static async Task<ClassificationResult> InvokeRequestResponseService(DataPoint data)
        {
            using (var client = new HttpClient())
            {
                var scoreRequest = new
                {
                    Inputs = new Dictionary<string, StringTable>() {
                        {
                            "input1",
                            new StringTable()
                            {
                                ColumnNames = new string[] {"device", "devicetype", "stuff", "time", "outofbed", "averagedepth", "nearx", "neary", "nearz", "farx", "fary", "farz", "accelx", "accely", "accelz", "presure1", "presure2", "presure3", "presure4", "presure5", "presure6", "presure7"},
                                Values = new string[,] {
                                    { "KinectCamera", "camera", "0", $"{data.timeStamp}", "0", $"{data.averageDepth}", $"{data.nearest.x}", $"{data.nearest.y}", $"{data.nearest.z}", $"{data.farthest.x}", $"{data.farthest.y}", $"{data.farthest.z}", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" } }
                            }
                        },
                    },
                    GlobalParameters = new Dictionary<string, string>()
                    {
                    }
                };

                string apiKey = File.ReadAllLines("../../key_ml.txt")[0];

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                client.BaseAddress = new Uri("https://ussouthcentral.services.azureml.net/workspaces/04cee42a7db84a27b1900fc39ef4de46/services/449907c3dbdc4ddbac0f57b9e674c7d6/execute?api-version=2.0&details=true");

                // WARNING: The 'await' statement below can result in a deadlock if you are calling this code from the UI thread of an ASP.Net application.
                // One way to address this would be to call ConfigureAwait(false) so that the execution does not attempt to resume on the original context.
                // For instance, replace code such as:
                //      result = await DoSomeTask()
                // with the following:
                //      result = await DoSomeTask().ConfigureAwait(false)


                HttpResponseMessage response = await client.PostAsJsonAsync("", scoreRequest);

                if (!response.IsSuccessStatusCode)
                {
                    return ClassificationResult.Unknown;
                }

                string result = await response.Content.ReadAsStringAsync();
                var parsedResponse = JsonConvert.DeserializeObject<PredictionResponse>(result);

                // The binary score will be the first item in the results list.
                var binaryResponse = parsedResponse.Results.output1.value.Values[0,0];

                if (binaryResponse == "0")
                    return ClassificationResult.InBed;
                if (binaryResponse == "1")
                    return ClassificationResult.OutOfBed;

                throw new InvalidOperationException("Could not determine the classification result.");
            }
        }
    }
}