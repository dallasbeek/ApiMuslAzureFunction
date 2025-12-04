using ApiMuslProxy.ApiService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApiMuslProxy;

public class GrandPrizeFunction(IConfiguration configuration, ILogger<GrandPrizeFunction> logger)
{
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions());

    [Function("GetGrandPrize")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");

        var pbGrandPrize = await GetPowerballGrandPrizeAsync();
        var mmGrandPrize = await GetMegaMillionsGrandPrizeAsync();

        var response = new
        {
            Powerball = pbGrandPrize.GrandPrize.NextPrizeText,
            MegaMillions = mmGrandPrize.GrandPrize.NextPrizeText
        };

        return new OkObjectResult(response);
    }


    private async Task<GrandPrizeResponseModel> GetPowerballGrandPrizeAsync()
    {
        var cacheKey = "powerball-grand-prize";

        if (Cache.TryGetValue(cacheKey, out GrandPrizeResponseModel? gpResponse))
            if (gpResponse != null)
            {
                logger.LogInformation("Powerball Data retrieved from in-memory cache.");
                return gpResponse;
            }


        gpResponse = await CallGrandPrizeApiAsync("powerball");

        Cache.Set(cacheKey, gpResponse, TimeSpan.FromMinutes(1));

        return gpResponse;
    }


    private async Task<GrandPrizeResponseModel> GetMegaMillionsGrandPrizeAsync()
    {
        var cacheKey = "mega-millions-grand-prize";

        if (Cache.TryGetValue(cacheKey, out GrandPrizeResponseModel? gpResponse))
            if (gpResponse != null)
            {
                logger.LogInformation("Mega Millions Data retrieved from in-memory cache.");
                return gpResponse;
            }


        gpResponse = await CallGrandPrizeApiAsync("mega-millions");

        Cache.Set(cacheKey, gpResponse, TimeSpan.FromMinutes(1));

        return gpResponse;
    }

    private async Task<GrandPrizeResponseModel> CallGrandPrizeApiAsync(string gameCode)
    {
        logger.LogInformation("Calling MUSL API for {0}.", gameCode);

        var endpoint = configuration["ApiMuslComEndpoint"];
        var apiKey = configuration["ApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException("ApiMuslComEndpoint configuration value is missing or empty.");
    
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ApiKey configuration value is missing or empty.");

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(endpoint);
        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        var client = new ApiMuslService(httpClient);
        return await client.GameServiceGetGrandPrizeAsync(null, null, null, gameCode);
    }
}