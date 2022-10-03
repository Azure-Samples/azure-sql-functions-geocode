using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Azure.Samples
{
    public static class ListWorkorders
    {
        // Visit https://aka.ms/sqlbindingsinput to learn how to use this input binding
        [FunctionName("ListWorkorders")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            [Sql("SELECT * FROM [dbo].[Workorders]",
            CommandType = System.Data.CommandType.Text,
            ConnectionStringSetting = "SqlConnectionString")] IEnumerable<Object> result,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger with SQL Input Binding function processed a request.");

            return new OkObjectResult(result);
        }
    }


    public static class GetWorkorder
    {
        // Visit https://aka.ms/sqlbindingsinput to learn how to use this input binding
        [FunctionName("Workorder")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "Workorder/{id}")] HttpRequest req,
            [Sql("SELECT * FROM [dbo].[Workorders] WHERE Id = @Id",
            CommandType = System.Data.CommandType.Text, Parameters = "@Id={id}",
            ConnectionStringSetting = "SqlConnectionString")] IEnumerable<Object> result,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger with SQL Input Binding function processed a request.");

            return new OkObjectResult(result);
        }
    }

    public static class CreateWorkorder
    {
        [FunctionName("CreateWorkorder")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "Workorder")] [FromBody] Workorder workorder,
            [Sql("dbo.Workorders", ConnectionStringSetting = "SqlConnectionString")] IAsyncCollector<Workorder> workorders)
            {
                // post body does not contain Id, create one here
                workorder.Id = Guid.NewGuid();

                // look up the location and add the coordinates
                if (workorder.Location is not null && workorder.Location != "") {
                    GeoCoordinates coordinates = await WorkorderEnteredOrchestration.GetLocation(workorder.Location);
                    if (coordinates is not null) {
                        workorder.Latitude = coordinates.Latitude;
                        workorder.Longitude = coordinates.Longitude;
                    }
                }

                await workorders.AddAsync(workorder);
                
                return new CreatedResult("/api/Workorder", workorder);
            }
    }

}
