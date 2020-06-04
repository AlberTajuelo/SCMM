﻿using Newtonsoft.Json;
using SCMM.Steam.Shared.Models;
using System.Collections.Generic;

namespace SCMM.Steam.Shared.Responses
{
    public class SteamMarketSearchPaginatedResponse : SteamResponse
    {
        [JsonProperty("start")]
        public int Start { get; set; }

        [JsonProperty("page_size")]
        public int PageSize { get; set; }

        [JsonProperty("total_count")]
        public int TotalCount { get; set; }

        [JsonProperty("results")]
        public List<SteamMarketSearchItem> Results { get; set; }
    }
}
