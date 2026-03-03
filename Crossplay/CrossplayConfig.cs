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

        [JsonProperty("enable_version_check_command")]
        public bool EnableVersionCheckCommand { get; set; } = true;

        [JsonProperty("show_startup_banner")]
        public bool ShowStartupBanner { get; set; } = true;

        [JsonProperty("enable_npc_buff_fix")]
        public bool EnableNpcBuffFix { get; set; } = true;
    }

    public class CrossplayConfig : ConfigFile<CrossplaySettings>
    {
        public void Reset()
        {
            Settings = new CrossplaySettings();
        }
    }
}
