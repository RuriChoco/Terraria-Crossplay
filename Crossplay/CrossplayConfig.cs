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
    }

    public class CrossplayConfig : ConfigFile<CrossplaySettings>
    {
    }
}
