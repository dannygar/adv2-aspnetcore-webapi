using System.Collections.Generic;
using System.Threading.Tasks;
using model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using web.client.Helpers;

namespace web.client.Controllers
{
    [Route("api/[controller]")]
    public class WeatherController : Controller
    {
        private readonly AzureAdOptions _azureAdOptions;

        public WeatherController(IOptions<AzureAdOptions> options)
        {
            _azureAdOptions = options.Value;
        }

        [HttpGet("[action]")]
        public async Task<IEnumerable<WeatherForecast>> WeatherForecasts()
        {
            var webApiUrl = _azureAdOptions.WebApiBaseAddress;
            var getForecastUrl = $"{webApiUrl}/weather/forecast";

            //Create an instance of httpClient that includes the access token to the Web API
            var httpClient = new HttpClientHelper(getForecastUrl, _azureAdOptions);

            //Call the Web API and return the list of WeatherForecast objects
            return await httpClient.GetItemAsync<IEnumerable<WeatherForecast>>("");
        }


    }
}
