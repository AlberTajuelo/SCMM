﻿using SCMM.Steam.Data.Models.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SCMM.Steam.Data.Models.Enums
{
    public enum PriceType : byte
    {
        [Display(Name = "Unknown")]
        Unknown = 0,

        [Display(Name = "Steam Store")]
        [BuyFrom(Url = "https://store.steampowered.com/itemstore/{0}/")]
        SteamStore,

        [Display(Name = "Steam Community Market")]
        [BuyFrom(Url = "https://steamcommunity.com/market/listings/{0}/{3}")]
        [SellTo(Url = "https://steamcommunity.com/market/listings/{0}/{3}", FeeRate = 13.0f)]
        SteamCommunityMarket,

        [Display(Name = "Skinport")]
        [BuyFrom(Url = "https://skinport.com/{1}/market?r=scmm&item={3}", FeeRate = 7.0f)] // Unconfirmed
        // TODO: SellTo (not supported in some countries)
        Skinport,

        [Display(Name = "LOOT.Farm")]
        [BuyFrom(Url = "https://loot.farm/", FeeRate = -20.0f)] // Unconfirmed, first time only?
        // TODO: SellTo
        LOOTFarm,

        [Display(Name = "swap.gg")]
        [BuyFrom(Url = "https://market.swap.gg/browse?idev_id=326&appId={0}&search={3}", FeeRate = 3.0f, FeeSurcharge = 40)] // Unconfirmed
        // TODO: SellTo
        SwapGG,

        [Display(Name = "Tradeit.gg")]
        [BuyFrom(Url = "https://tradeit.gg/{1}/store?aff=scmm&search={3}", FeeRate = -20.0f)] // Unconfirmed, first time only?
        // TODO: SellTo
        TradeitGG,

        [Display(Name = "CS.Deals")]
        [BuyFrom(Url = "https://cs.deals/market/{1}/?name={3}&sort=price")]
        // TODO: SellTo
        CSDeals,

        [Display(Name = "SkinBaron")]
        [BuyFrom(Url = "https://skinbaron.de/en/{1}?str={3}&sort=CF", FeeRate = 5.0f, FeeSurcharge = 40)]
        // https://skinbaron.de/misc/apidoc/
        // TODO: BuyNow
        // TODO: SellTo
        SkinBaron,

        [Display(Name = "RUSTSkins")]
        [BuyFrom(Url = "https://rustskins.com/market?search={3}&sort=p-ascending")]
        // TODO: BuyNow
        // TODO: SellTo
        RUSTSkins,

        [Display(Name = "Dmarket")]
        [BuyFrom(Url = "https://dmarket.com/ingame-items/item-list/{1}-skins?title={3}")]
        // https://docs.dmarket.com/v1/swagger.html#/Buy%20items/GetAggregatedPrices
        // https://api.dmarket.com/price-aggregator/v1/aggregated-prices?Titles=Blackout%20Hoodie&Titles=Blackout%20Pants&Limit=100
        // TODO: BuyNow
        // TODO: SellTo
        Dmarket,

        [Display(Name = "BUFF")]
        [BuyFrom(Url = "https://buff.163.com/market/{1}")]
        // TODO: BuyNow
        // TODO: SellTo
        Buff
    }
}
