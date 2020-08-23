﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SCMM.Steam.Shared;
using SCMM.Web.Server.API.Controllers.Extensions;
using SCMM.Web.Server.Data;
using SCMM.Web.Server.Domain;
using SCMM.Web.Server.Domain.Models.Steam;
using SCMM.Web.Server.Extensions;
using SCMM.Web.Shared;
using SCMM.Web.Shared.Domain.DTOs.InventoryItems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SCMM.Web.Server.API.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly ILogger<InventoryController> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMapper _mapper;

        public InventoryController(ILogger<InventoryController> logger, IServiceScopeFactory scopeFactory, IMapper mapper)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _mapper = mapper;
        }

        [HttpGet("me")]
        public async Task<ProfileInventoryDetailsDTO> GetMyInventoryProfile([FromQuery] bool sync = false)
        {
            return await GetInventoryProfile(Request.ProfileId(), sync);
        }

        [HttpGet("{steamId}")]
        public async Task<ProfileInventoryDetailsDTO> GetInventoryProfile([FromRoute] string steamId, [FromQuery] bool sync = false)
        {
            if (String.IsNullOrEmpty(steamId))
            {
                throw new ArgumentNullException(nameof(steamId));
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetService<SteamService>();
                var db = scope.ServiceProvider.GetService<SteamDbContext>();
                var currency = Request.Currency();
                var profile = (SteamProfile)null;

                if (sync)
                {
                    // Load the profile and force an inventory sync
                    profile = await service.LoadAndRefreshProfileInventory(steamId);
                }
                else
                {
                    // Load the profile
                    var inventory = await db.SteamProfiles
                        .Where(x => x.SteamId == steamId || x.ProfileId == steamId)
                        .Select(x => new
                        {
                            Profile = x,
                            TotalItems = x.InventoryItems.Count
                        })
                        .FirstOrDefaultAsync();

                    // If the profile inventory hasn't been loaded before, fetch it now
                    if (inventory == null || inventory.Profile == null || inventory.TotalItems == 0)
                    {
                        profile = await service.LoadAndRefreshProfileInventory(steamId);
                    }
                    else
                    {
                        profile = inventory.Profile;
                    }
                }

                if (profile == null)
                {
                    throw new Exception($"Profile with SteamID '{steamId}' was not found");
                }

                profile.LastViewedInventoryOn = DateTimeOffset.Now;
                db.SaveChanges();

                return _mapper.Map<SteamProfile, ProfileInventoryDetailsDTO>(
                    profile, Request
                );
            }
        }

        [HttpGet("me/total")]
        public async Task<ProfileInventoryTotalsDTO> GetMyInventoryTotal()
        {
            return await GetInventoryTotal(Request.ProfileId());
        }

        [HttpGet("{steamId}/total")]
        public async Task<ProfileInventoryTotalsDTO> GetInventoryTotal([FromRoute] string steamId)
        {
            if (String.IsNullOrEmpty(steamId))
            {
                throw new ArgumentNullException(nameof(steamId));
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<SteamDbContext>();
                var service = scope.ServiceProvider.GetService<SteamService>();
                var currency = Request.Currency();

                var profileInventoryItems = db.SteamInventoryItems
                    .Where(x => x.Owner.SteamId == steamId || x.Owner.ProfileId == steamId)
                    .Where(x => x.MarketItemId != null)
                    .Select(x => new
                    {
                        Quantity = x.Quantity,
                        BuyPrice = x.BuyPrice,
                        ExchangeRateMultiplier = x.Currency.ExchangeRateMultiplier,
                        MarketItemLast1hrValue = x.MarketItem.Last1hrValue,
                        MarketItemLast24hrValue = x.MarketItem.Last24hrValue,
                        MarketItemResellPrice = x.MarketItem.ResellPrice,
                        MarketItemResellTax = x.MarketItem.ResellTax,
                        MarketItemExchangeRateMultiplier = x.MarketItem.Currency.ExchangeRateMultiplier
                    })
                    .ToList();

                var profileInventory = new
                {
                    TotalItems = profileInventoryItems
                        .Sum(x => x.Quantity),
                    TotalInvested = profileInventoryItems
                        .Where(x => x.BuyPrice != null && x.BuyPrice != 0 && x.ExchangeRateMultiplier != 0)
                        .Sum(x => (x.BuyPrice / x.ExchangeRateMultiplier) * x.Quantity),
                    TotalMarketValueLast1hr = profileInventoryItems
                        .Where(x => x.MarketItemLast1hrValue != 0 && x.MarketItemExchangeRateMultiplier != 0)
                        .Sum(x => (x.MarketItemLast1hrValue / x.MarketItemExchangeRateMultiplier) * x.Quantity),
                    TotalMarketValueLast24hr = profileInventoryItems
                        .Where(x => x.MarketItemLast24hrValue != 0 && x.MarketItemExchangeRateMultiplier != 0)
                        .Sum(x => (x.MarketItemLast24hrValue / x.MarketItemExchangeRateMultiplier) * x.Quantity),
                    TotalResellValue = profileInventoryItems
                        .Where(x => x.MarketItemResellPrice != 0 && x.MarketItemExchangeRateMultiplier != 0)
                        .Sum(x => (x.MarketItemResellPrice / x.MarketItemExchangeRateMultiplier) * x.Quantity),
                    TotalResellTax = profileInventoryItems
                        .Where(x => x.MarketItemResellTax != 0 && x.MarketItemExchangeRateMultiplier != 0)
                        .Sum(x => (x.MarketItemResellTax / x.MarketItemExchangeRateMultiplier) * x.Quantity)
                };

                return new ProfileInventoryTotalsDTO()
                {
                    TotalItems = profileInventory.TotalItems,
                    TotalInvested = currency.CalculateExchange(profileInventory.TotalInvested ?? 0),
                    TotalMarketValue = currency.CalculateExchange(profileInventory.TotalMarketValueLast1hr),
                    TotalMarket24hrMovement = currency.CalculateExchange(profileInventory.TotalMarketValueLast1hr - profileInventory.TotalMarketValueLast24hr),
                    TotalResellValue = currency.CalculateExchange(profileInventory.TotalResellValue),
                    TotalResellTax = currency.CalculateExchange(profileInventory.TotalResellTax),
                    TotalResellProfit = (
                        currency.CalculateExchange(profileInventory.TotalResellValue - profileInventory.TotalResellTax) - currency.CalculateExchange(profileInventory.TotalInvested ?? 0)
                    )
                };
            }
        }

        [HttpGet("me/summary")]
        public async Task<IList<ProfileInventoryItemSummaryDTO>> GetMyInventorySummary()
        {
            return await GetInventorySummary(Request.ProfileId());
        }

        [HttpGet("{steamId}/summary")]
        public async Task<IList<ProfileInventoryItemSummaryDTO>> GetInventorySummary([FromRoute] string steamId)
        {
            if (String.IsNullOrEmpty(steamId))
            {
                throw new ArgumentNullException(nameof(steamId));
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetService<SteamService>();
                var db = scope.ServiceProvider.GetService<SteamDbContext>();
                var currency = Request.Currency();

                var profileInventoryItems = db.SteamInventoryItems
                    .Where(x => x.Owner.SteamId == steamId || x.Owner.ProfileId == steamId)
                    .Where(x => x.MarketItem != null)
                    .Select(x => new
                    {
                        MarketItem = x.MarketItem,
                        MarketItemApp = x.MarketItem.App,
                        MarketItemDescription = x.MarketItem.Description,
                        MarketItemCurrency = x.MarketItem.Currency,
                        Quantity = x.Quantity
                    })
                    .ToList();

                var profileInventoryItemsSummaries = new List<ProfileInventoryItemSummaryDTO>();
                var marketItems = profileInventoryItems
                    .Select(x => x.MarketItem)
                    .Where(x => x != null)
                    .OrderByDescending(x => x.Last1hrValue);

                foreach (var marketItem in marketItems)
                {
                    if (!profileInventoryItemsSummaries.Any(x => x.Item.SteamId == marketItem.SteamId))
                    {
                        var inventoryMarketItem = _mapper.Map<SteamMarketItem, InventoryMarketItemDTO>(
                            marketItem, Request
                        );
                        profileInventoryItemsSummaries.Add(new ProfileInventoryItemSummaryDTO()
                        {
                            Item = inventoryMarketItem,
                            Quantity = profileInventoryItems
                                .Where(x => x.MarketItem?.SteamId == marketItem.SteamId)
                                .Sum(x => x.Quantity)
                        });
                    }
                }

                return profileInventoryItemsSummaries;
            }
        }

        [HttpGet("me/returnOnInvestment")]
        public async Task<IList<InventoryItemListDTO>> GetMyInventoryDetails()
        {
            return await GetInventoryDetails(Request.ProfileId());
        }

        [HttpGet("{steamId}/returnOnInvestment")]
        public async Task<IList<InventoryItemListDTO>> GetInventoryDetails([FromRoute] string steamId)
        {
            if (String.IsNullOrEmpty(steamId))
            {
                throw new ArgumentNullException(nameof(steamId));
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetService<SteamService>();
                var db = scope.ServiceProvider.GetService<SteamDbContext>();
                var currency = Request.Currency();

                var profileInventoryItems = db.SteamInventoryItems
                    .Where(x => x.Owner.SteamId == steamId || x.Owner.ProfileId == steamId)
                    .Select(x => new
                    {
                        Item = x,
                        ItemDescription = x.Description,
                        ItemCurrency = x.Currency,
                        MarketItem = x.MarketItem,
                        MarketItemApp = x.MarketItem.App,
                        MarketItemDescription = x.MarketItem.Description,
                        MarketItemCurrency = x.MarketItem.Currency
                    })
                    .ToList();

                var profileInventoryItemsDetails = new List<InventoryItemListDTO>();
                foreach (var profileInventoryItem in profileInventoryItems)
                {
                    profileInventoryItemsDetails.Add(
                         _mapper.Map<SteamInventoryItem, InventoryItemListDTO>(profileInventoryItem.Item, Request)
                    );
                }

                return profileInventoryItemsDetails;
            }
        }

        [HttpGet("me/activity")]
        public async Task<IList<ProfileInventoryActivityDTO>> GetMyInventoryActivity()
        {
            return await GetInventoryActivity(Request.ProfileId());
        }

        [HttpGet("{steamId}/activity")]
        public async Task<IList<ProfileInventoryActivityDTO>> GetInventoryActivity([FromRoute] string steamId)
        {
            if (String.IsNullOrEmpty(steamId))
            {
                throw new ArgumentNullException(nameof(steamId));
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetService<SteamService>();
                var db = scope.ServiceProvider.GetService<SteamDbContext>();
                var currency = Request.Currency();

                var recentActivityCutoff = DateTimeOffset.Now.Subtract(TimeSpan.FromHours(24));
                var profileInventoryActivities = db.SteamInventoryItems
                    .Where(x => x.Owner.SteamId == steamId || x.Owner.ProfileId == steamId)
                    .Where(x => x.MarketItem != null)
                    .SelectMany(x => 
                        x.MarketItem.Activity.Where(x => x.Timestamp >= recentActivityCutoff)
                    )
                    .OrderByDescending(x => x.Timestamp)
                    .Include(x => x.Item)
                    .Include(x => x.Item.Description)
                    .Include(x => x.Item.Currency)
                    .Take(100)
                    .ToList();

                var profileInventoryActivitiesDetails = new List<ProfileInventoryActivityDTO>();
                foreach (var profileInventoryActivity in profileInventoryActivities)
                {
                    profileInventoryActivitiesDetails.Add(
                         _mapper.Map<SteamMarketItemActivity, ProfileInventoryActivityDTO>(profileInventoryActivity, Request)
                    );
                }

                return profileInventoryActivitiesDetails;
            }
        }

        [HttpGet("me/performance")]
        public async Task<ProfileInventoryPerformanceDTO> GetMyInventoryPerformance()
        {
            return await GetInventoryPerformance(Request.ProfileId());
        }

        [HttpGet("{steamId}/performance")]
        public async Task<ProfileInventoryPerformanceDTO> GetInventoryPerformance([FromRoute] string steamId)
        {
            if (String.IsNullOrEmpty(steamId))
            {
                throw new ArgumentNullException(nameof(steamId));
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetService<SteamService>();
                var db = scope.ServiceProvider.GetService<SteamDbContext>();
                var currency = Request.Currency();

                var profileInventoryItems = db.SteamInventoryItems
                    .Where(x => x.Owner.SteamId == steamId || x.Owner.ProfileId == steamId)
                    .Where(x => x.MarketItemId != null)
                    .Select(x => new
                    {
                        Quantity = x.Quantity,
                        BuyPrice = x.BuyPrice,
                        ExchangeRateMultiplier = x.Currency.ExchangeRateMultiplier,
                        MarketItemLast1hrValue = x.MarketItem.Last1hrValue,
                        MarketItemLast24hrValue = x.MarketItem.Last24hrValue,
                        MarketItemLast48hrValue = x.MarketItem.Last48hrValue,
                        MarketItemLast72hrValue = x.MarketItem.Last72hrValue,
                        MarketItemLast96hrValue = x.MarketItem.Last96hrValue,
                        MarketItemLast120hrValue = x.MarketItem.Last120hrValue,
                        MarketItemLast144hrValue = x.MarketItem.Last144hrValue,
                        MarketItemLast168hrValue = x.MarketItem.Last168hrValue,
                        MarketItemExchangeRateMultiplier = x.MarketItem.Currency.ExchangeRateMultiplier
                    })
                    .ToList();

                var profileInventory = new
                {
                    Invested = profileInventoryItems
                        .Where(x => x.BuyPrice != null && x.BuyPrice != 0 && x.ExchangeRateMultiplier != 0)
                        .Sum(x => (x.BuyPrice / x.ExchangeRateMultiplier) * x.Quantity),
                    Last1hrValue = profileInventoryItems
                        .Where(x => x.MarketItemLast1hrValue != 0 && x.MarketItemExchangeRateMultiplier != 0)
                        .Sum(x => (x.MarketItemLast1hrValue / x.MarketItemExchangeRateMultiplier) * x.Quantity),
                    Last24hrValue = profileInventoryItems
                        .Where(x => x.MarketItemLast24hrValue != 0 && x.MarketItemExchangeRateMultiplier != 0)
                        .Sum(x => (x.MarketItemLast24hrValue / x.MarketItemExchangeRateMultiplier) * x.Quantity),
                    Last48hrValue = profileInventoryItems
                        .Where(x => x.MarketItemLast48hrValue != 0 && x.MarketItemExchangeRateMultiplier != 0)
                        .Sum(x => (x.MarketItemLast48hrValue / x.MarketItemExchangeRateMultiplier) * x.Quantity),
                    Last72hrValue = profileInventoryItems
                        .Where(x => x.MarketItemLast72hrValue != 0 && x.MarketItemExchangeRateMultiplier != 0)
                        .Sum(x => (x.MarketItemLast72hrValue / x.MarketItemExchangeRateMultiplier) * x.Quantity),
                    Last96hrValue = profileInventoryItems
                        .Where(x => x.MarketItemLast96hrValue != 0 && x.MarketItemExchangeRateMultiplier != 0)
                        .Sum(x => (x.MarketItemLast96hrValue / x.MarketItemExchangeRateMultiplier) * x.Quantity),
                    Last120hrValue = profileInventoryItems
                        .Where(x => x.MarketItemLast120hrValue != 0 && x.MarketItemExchangeRateMultiplier != 0)
                        .Sum(x => (x.MarketItemLast120hrValue / x.MarketItemExchangeRateMultiplier) * x.Quantity),
                    Last144hrValue = profileInventoryItems
                        .Where(x => x.MarketItemLast144hrValue != 0 && x.MarketItemExchangeRateMultiplier != 0)
                        .Sum(x => (x.MarketItemLast144hrValue / x.MarketItemExchangeRateMultiplier) * x.Quantity),
                    Last168hrValue = profileInventoryItems
                        .Where(x => x.MarketItemLast168hrValue != 0 && x.MarketItemExchangeRateMultiplier != 0)
                        .Sum(x => (x.MarketItemLast168hrValue / x.MarketItemExchangeRateMultiplier) * x.Quantity)
                };

                var today = DateTimeOffset.UtcNow.Date;
                var totalInvested = currency.CalculateExchange(profileInventory.Invested ?? 0);
                var last168hrValue = currency.CalculateExchange(profileInventory.Last168hrValue);
                var last144hrValue = currency.CalculateExchange(profileInventory.Last144hrValue);
                var last120hrValue = currency.CalculateExchange(profileInventory.Last120hrValue);
                var last96hrValue = currency.CalculateExchange(profileInventory.Last96hrValue);
                var last72hrValue = currency.CalculateExchange(profileInventory.Last72hrValue);
                var last48hrValue = currency.CalculateExchange(profileInventory.Last48hrValue);
                var last24hrValue = currency.CalculateExchange(profileInventory.Last24hrValue);
                var last1hrValue = currency.CalculateExchange(profileInventory.Last1hrValue);

                var valueHistory = new Dictionary<DateTimeOffset, long>();
                valueHistory[today.Subtract(TimeSpan.FromDays(7))] = last168hrValue;
                valueHistory[today.Subtract(TimeSpan.FromDays(6))] = last144hrValue;
                valueHistory[today.Subtract(TimeSpan.FromDays(5))] = last120hrValue;
                valueHistory[today.Subtract(TimeSpan.FromDays(4))] = last96hrValue;
                valueHistory[today.Subtract(TimeSpan.FromDays(3))] = last72hrValue;
                valueHistory[today.Subtract(TimeSpan.FromDays(2))] = last48hrValue;
                valueHistory[today.Subtract(TimeSpan.FromDays(1))] = last24hrValue;
                valueHistory[today.Subtract(TimeSpan.FromDays(0))] = last1hrValue;

                var profitHistory = new Dictionary<DateTimeOffset, long>();
                profitHistory[today.Subtract(TimeSpan.FromDays(7))] = last168hrValue - SteamEconomyHelper.GetSteamFeeAsInt(last168hrValue) - totalInvested;
                profitHistory[today.Subtract(TimeSpan.FromDays(6))] = last144hrValue - SteamEconomyHelper.GetSteamFeeAsInt(last144hrValue) - totalInvested;
                profitHistory[today.Subtract(TimeSpan.FromDays(5))] = last120hrValue - SteamEconomyHelper.GetSteamFeeAsInt(last120hrValue) - totalInvested;
                profitHistory[today.Subtract(TimeSpan.FromDays(4))] = last96hrValue - SteamEconomyHelper.GetSteamFeeAsInt(last96hrValue) - totalInvested;
                profitHistory[today.Subtract(TimeSpan.FromDays(3))] = last72hrValue - SteamEconomyHelper.GetSteamFeeAsInt(last72hrValue) - totalInvested;
                profitHistory[today.Subtract(TimeSpan.FromDays(2))] = last48hrValue - SteamEconomyHelper.GetSteamFeeAsInt(last48hrValue) - totalInvested;
                profitHistory[today.Subtract(TimeSpan.FromDays(1))] = last24hrValue - SteamEconomyHelper.GetSteamFeeAsInt(last24hrValue) - totalInvested;
                profitHistory[today.Subtract(TimeSpan.FromDays(0))] = last1hrValue - SteamEconomyHelper.GetSteamFeeAsInt(last1hrValue) - totalInvested;

                return new ProfileInventoryPerformanceDTO()
                {
                    ValueHistoryGraph = valueHistory.ToDictionary(
                        x => x.Key.ToString("dd MMM yyyy"),
                        x => x.Value
                    ),
                    ProfitHistoryGraph = profitHistory.ToDictionary(
                        x => x.Key.ToString("dd MMM yyyy"),
                        x => x.Value
                    )
                };
            }
        }

        [HttpPut("item/{inventoryItemId}")]
        public async void SetInventoryItemBuyPrice([FromRoute] Guid inventoryItemId, [FromBody] UpdateInventoryItemPriceCommand command)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<SteamDbContext>();
                var inventoryItem = await db.SteamInventoryItems.SingleOrDefaultAsync(x => x.Id == inventoryItemId);
                if (inventoryItem == null)
                {
                    _logger.LogError($"Inventory item with id '{inventoryItemId}' was not found");
                    return;
                }

                inventoryItem.CurrencyId = command.CurrencyId;
                inventoryItem.BuyPrice = SteamEconomyHelper.GetPriceValueAsInt(command.Price);
                await db.SaveChangesAsync();
            }
        }
    }
}
