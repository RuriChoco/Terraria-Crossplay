﻿using Newtonsoft.Json;
using System.Collections.Generic;
using TShockAPI.Configuration;

namespace Crossplay
{
    public class CrossplaySettings
    {
        [JsonProperty("support_journey_clients")]
        public bool SupportJourneyClients = false;

        [JsonProperty("debug_mode")]
        public bool DebugMode = false;

        [JsonProperty("whitelisted_projectiles")]
        public List<int> WhitelistedProjectiles = new List<int> { 33 };

        [JsonProperty("enable_item_limits")]
        public bool EnableItemLimits = false;

        [JsonProperty("max_dropped_items")]
        public int MaxDroppedItems = 200;

        [JsonProperty("item_despawn_seconds")]
        public int ItemDespawnSeconds = 180;
    }

    public class CrossplayConfig : ConfigFile<CrossplaySettings>
    {
    }
}
