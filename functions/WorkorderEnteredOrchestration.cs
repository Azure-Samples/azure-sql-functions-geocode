using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Azure.Samples
{
    public static class WorkorderEnteredOrchestration
    {
        [FunctionName("WorkorderEnteredOrchestration")]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            string sqlInput = context.GetInput<string>();
            string output = await context.CallActivityAsync<string>("WorkorderEntered_Analyze", sqlInput);

            return output;
        }

        [FunctionName("WorkorderEntered_Analyze")]
        public static async Task<string> ProcessLocation([ActivityTrigger] string sqlInput,
            // SQL output binding to write coordinates back to the database
            [Sql("dbo.Workorders", ConnectionStringSetting = "SqlConnectionString")] IAsyncCollector<WorkorderEntered> workorders,
            ILogger log)
        {
            WorkorderEntered input = JsonConvert.DeserializeObject<WorkorderEntered>(sqlInput);
            log.LogInformation($"Geocoding input of {sqlInput}");

            if (input.Location is not null && input.Location != "") {
                GeoCoordinates coordinates = await GetLocation(input.Location);

                if (coordinates is not null) {
                    input.Latitude = coordinates.Latitude;
                    input.Longitude = coordinates.Longitude;
                    await workorders.AddAsync(input);
                }
                return "Geocoding Completed";
            } else {
                log.LogError($"Geocoding input of {sqlInput} failed");
                return "Geocoding Failed";
            }
        }

        // this function is called by sp_invoke_external_rest_endpoint from SQL
        // POST body format
        // {
        //     "Id": xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx,
        //     "Summary": "This is a summary",
        //     "Description": "This is a much longer description",
        //     "Location": "Seattle, WA"
        // }
        [FunctionName("WorkorderEnteredOrchestration_HttpStart")]
        public static async Task<IActionResult> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content
            string eventData = await new StreamReader(req.Body).ReadToEndAsync();
            string instanceId = await starter.StartNewAsync("WorkorderEnteredOrchestration", null, eventData);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        private static async Task<GeoCoordinates> GetLocation(string location)
        {
            GeoCoordinates newCoordinates = new GeoCoordinates();

            // call GET https://atlas.microsoft.com/geocode?api-version=2022-02-01-preview&query={some location}
            // to get the coordinates for the location

            using (HttpClient client = new HttpClient()) {
                string azureMapKey = Environment.GetEnvironmentVariable("AzureMapKey");
                string response = await client.GetStringAsync($"https://atlas.microsoft.com/geocode?api-version=2022-02-01-preview&query={location}&subscription-key={azureMapKey}");
                dynamic json = JsonConvert.DeserializeObject(response);
                newCoordinates.Latitude = json.features[0].geometry.coordinates[0];
                newCoordinates.Longitude = json.features[0].geometry.coordinates[1];
            }

            return newCoordinates;
        }
    }
    
    public class GeoCoordinates {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
    public class WorkorderEntered {
        public Guid Id { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}