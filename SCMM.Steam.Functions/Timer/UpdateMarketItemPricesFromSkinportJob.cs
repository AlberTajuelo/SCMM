﻿using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCMM.Market.Skinport.Client;
using SCMM.Shared.Data.Models.Extensions;
using SCMM.Steam.Data.Models;
using SCMM.Steam.Data.Models.Enums;
using SCMM.Steam.Data.Models.Extensions;
using SCMM.Steam.Data.Store;
using SCMM.Steam.Data.Store.Types;

namespace SCMM.Steam.Functions.Timer;

public class UpdateMarketItemPricesFromSkinportJob
{
    private readonly SteamDbContext _db;
    private readonly SkinportWebClient _skinportWebClient;

    public UpdateMarketItemPricesFromSkinportJob(SteamDbContext db, SkinportWebClient skinportWebClient)
    {
        _db = db;
        _skinportWebClient = skinportWebClient;
    }

    [Function("Update-Market-Item-Prices-From-Skinport")]
    public async Task Run([TimerTrigger("0 10 * * * *")] /* every hour, 10 minutes after the hour */ TimerInfo timerInfo, FunctionContext context)
    {
        var logger = context.GetLogger("Update-Market-Item-Prices-From-Skinport");

        var steamApps = await _db.SteamApps.ToListAsync();
        if (!steamApps.Any())
        {
            return;
        }

        var currencies = await _db.SteamCurrencies.ToListAsync();
        if (currencies == null)
        {
            return;
        }

        var usdCurrency = currencies.FirstOrDefault(x => x.Name == Constants.SteamCurrencyUSD);
        if (usdCurrency == null)
        {
            return;
        }

        foreach (var app in steamApps)
        {
            try
            {
                logger.LogTrace($"Updating market item price information from Skinport (appId: {app.SteamId})");
                var items = await _db.SteamMarketItems
                    .Select(x => new
                    {
                        Name = x.Description.NameHash,
                        Currency = x.Currency,
                        Item = x,
                    })
                    .ToListAsync();

                var skinportItems = await _skinportWebClient.GetItemListAsync(app.SteamId, currency: usdCurrency.Name);
                if (skinportItems?.Any() != true)
                {
                    continue;
                }

                foreach (var skinportItem in skinportItems)
                {
                    var item = items.FirstOrDefault(x => x.Name == skinportItem.MarketHashName)?.Item;
                    var currency = currencies.FirstOrDefault(x => String.Equals(x.Name, skinportItem.Currency, StringComparison.OrdinalIgnoreCase));
                    if (item != null && currency != null)
                    {
                        item.Prices[PriceType.Skinport] = new PriceStock
                        {
                            Price = item.Currency.CalculateExchange((skinportItem.MinPrice ?? skinportItem.SuggestedPrice).ToString().SteamPriceAsInt(), currency),
                            Stock = skinportItem.Quantity
                        };
                    }
                }

                var missingItems = items.Where(x => !skinportItems.Any(y => x.Name == y.MarketHashName) && x.Item.Prices.ContainsKey(PriceType.Skinport));
                foreach (var missingItem in missingItems)
                {
                    missingItem.Item.Prices.Remove(PriceType.Skinport);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to update market item price information from Skinport (appId: {app.SteamId}). {ex.Message}");
                continue;
            }

            _db.SaveChanges();
        }
    }
}
