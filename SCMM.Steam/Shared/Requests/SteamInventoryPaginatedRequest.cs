﻿using System;

namespace SCMM.Steam.Shared.Requests
{
    public class SteamInventoryPaginatedRequest : SteamPaginatedRequest
    {
        public string SteamId { get; set; }

        public string AppId { get; set; }

        public Uri Uri => new Uri(
            $"{SteamConstants.SteamCommunityUrl}/inventory/{Uri.EscapeDataString(SteamId)}/{Uri.EscapeDataString(AppId)}/2?start={Start}&count={Count}&language={Uri.EscapeDataString(Language)}&currency={Uri.EscapeDataString(CurrencyId)}&norender={(NoRender ? "1" : "0")}"
        );
    }
}
