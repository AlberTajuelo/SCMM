﻿using SCMM.Shared.Data.Models.Enums;
using SCMM.Steam.Data.Models.Enums;

namespace SCMM.Web.Data.Models.UI.Item
{
    public class ItemDescriptionWithPriceDTO : ItemDescriptionDTO, ICanBeOwned, ICanBePurchased, ICanBeInteractedWith
    {
        public long? OriginalPrice { get; set; }

        public MarketType? BuyNowFrom { get; set; }

        public long? BuyNowPrice { get; set; }

        public string BuyNowUrl { get; set; }

        public long? Subscriptions { get; set; }

        public EstimatedAccuracyTypes SupplyEstimationAccuracy { get; set; }

        public long? SupplyTotalEstimated { get; set; }

        public long? Supply { get; set; }

        public long? Demand { get; set; }

        public ItemInteractionDTO[] Actions { get; set; }

        public long? PriceMovement => (BuyNowPrice - OriginalPrice) ?? 0;
    }
}
