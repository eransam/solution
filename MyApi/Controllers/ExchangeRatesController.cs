using YourName_DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microservices.Api
{
    
    [Route("api/[controller]")]
    [ApiController]
    public class ExchangeRatesController : ControllerBase
    {
        private readonly ILogger<ExchangeRatesController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public ExchangeRatesController(ILogger<ExchangeRatesController> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public async Task<ActionResult<ExchangeRatesResult>> GetExchangeRates()
        {
            // Call the CoinGecko API to get the exchange rates
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync("https://api.coingecko.com/api/v3/exchange_rates");
            response.EnsureSuccessStatusCode();
            var exchangeRatesJson = await response.Content.ReadAsStringAsync();
            var exchangeRates = JsonConvert.DeserializeObject<Dictionary<string, decimal>>(exchangeRatesJson);

            // Calculate the max rate and multiply all rates by 1000
            var maxRate = exchangeRates.Max(kvp => kvp.Value);
            var multipliedRates = exchangeRates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value * 1000);

            // Order the results by type and then by name
            var orderedRates = multipliedRates.OrderBy(kvp => kvp.Key).ThenBy(kvp => kvp.Value);

            // Save the max rate in the database if it has changed
            using (var db = new YourName_DB())
            {
                var previousMaxRate = db.RatesHistory.OrderByDescending(r => r.Id).FirstOrDefault()?.Value;
                if (previousMaxRate != maxRate)
                {
                    db.RatesHistory.Add(new RateHistory
                    {
                        DateTime = DateTime.Now,
                        Value = maxRate
                    });
                    await db.SaveChangesAsync();
                }
            }

            // Write the results to the log file in JSON format
            var result = new ExchangeRatesResult
            {
                MaxRate = maxRate,
                MaxRatePrevious = previousMaxRate,
                Rates = orderedRates
            };
            var resultJson = JsonConvert.SerializeObject(result);
            _logger.LogInformation(resultJson);

            return result;
        }
    }

    public class ExchangeRatesResult
    {
        public decimal MaxRate { get; set; }
        public decimal? MaxRatePrevious { get; set; }
        public IEnumerable<KeyValuePair<string, decimal>> Rates { get; set; }
    }
}
