﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;

using System.Runtime.InteropServices;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using TerrariaApi.Server;

using TShockAPI;
using TShockAPI.Hooks;

[assembly: InternalsVisibleTo("Crossplay.Tests")]
namespace Crossplay
{
    [ApiVersion(2, 1)]
    public class CrossplayPlugin : TerrariaPlugin
    {
        private readonly Dictionary<int, string> _supportedVersions = new()
        {
            { 312, "v1.4.5" },
            { 313, "v1.4.5.1" },
            { 314, "v1.4.5.2" },
            { 315, "v1.4.5.3" },
            { 316, "v1.4.5.4" },
            { 317, "v1.4.5.5" },
            { 318, "v1.4.5.6" },
        };

        public override string Name => "Crossplay";

        public override string Author => "Moneylover3246";

        public override string Description => "Enables crossplay for terraria";

        public override Version Version => new("2.6.1");

        public CrossplayConfig Config { get; } = new();

        public int[] ClientVersions { get; } = new int[256];

        public static CrossplayPlugin Instance { get; private set; }

        public static string SavePath => Path.Combine(TShock.SavePath, "Crossplay.json");

        private HashSet<int> _whitelistedProjectiles = new();

        private byte[] _cachedVersionFixPacket;

        private DateTime _lastItemCheck = DateTime.UtcNow;

        private readonly List<(int index, int time)> _survivingItemsCache = new();

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private readonly struct ProjectilePacketCore
        {
            public readonly short Identity;
            public readonly float X;
            public readonly float Y;
            public readonly float VX;
            public readonly float VY;
            public readonly byte Owner;
            public readonly short Type;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private readonly struct NpcAddBuffPacketSmall
        {
            public readonly short NpcId;
            public readonly ushort BuffType;
            public readonly short BuffTime;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private readonly struct NpcAddBuffPacketLarge
        {
            public readonly short NpcId;
            public readonly ushort BuffType;
            public readonly int BuffTime;
        }

        public readonly Dictionary<int, int> MaxItems = new()
        {
            { 312, 6100 },
            { 313, 6100 },
            { 314, 6100 },
            { 315, 6100 },
            { 316, 6100 },
            { 317, 6100 },
            { 318, 6100 },
        };

        public CrossplayPlugin(Main game) : base(game)
        {
            Instance = this;
            Order = -1;
        }

        public override void Initialize()
        {
            if (!_supportedVersions.ContainsKey(Main.curRelease))
            {
                Console.WriteLine($"[Crossplay] Warning: Server version {Main.curRelease} is not explicitly supported. Plugin loading anyway.");
            }

            try
            {
                On.Terraria.Net.NetManager.Broadcast_NetPacket_int += NetModuleHandler.OnBroadcast;
                On.Terraria.Net.NetManager.SendToClient += NetModuleHandler.OnSendToClient;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Crossplay] Warning: Failed to register NetManager hooks. Creative item filtering will be disabled. Error: {ex.Message}");
                if (ex.Message.Contains("clrjit"))
                {
                    Console.WriteLine("[Crossplay] Fix: On Linux/macOS, launch the server using: DOTNET_ROLL_FORWARD=Major dotnet TShock.Server.dll");
                }
            }

            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData, int.MaxValue);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);

            GeneralHooks.ReloadEvent += OnReload;
            Commands.ChatCommands.Add(new Command(new List<string> { "crossplay.settings", "crossplay.clear", "crossplay.check" }, CrossplayCommand, "crossplay"));

            // Pre-generate the version fix packet since Main.curRelease is constant
            using (var factory = new PacketFactory())
            {
                _cachedVersionFixPacket = factory
                    .SetType(1)
                    .PackString($"Terraria{Main.curRelease}")
                    .GetByteData();
            }
        }

        private void OnInitialize(EventArgs args)
        {
            if (!Directory.Exists(TShock.SavePath))
            {
                Directory.CreateDirectory(TShock.SavePath);
            }
            ReloadConfig();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    On.Terraria.Net.NetManager.Broadcast_NetPacket_int -= NetModuleHandler.OnBroadcast;
                    On.Terraria.Net.NetManager.SendToClient -= NetModuleHandler.OnSendToClient;
                }
                catch
                {
                    // Ignore errors if hooks were not registered
                }

                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);

                GeneralHooks.ReloadEvent -= OnReload;
            }
            base.Dispose(disposing);
        }

        private void OnReload(ReloadEventArgs args)
        {
            ReloadConfig();
        }

        private void CrossplayCommand(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                string subCommand = args.Parameters[0].ToLower();
                if (subCommand == "reload")
                {
                    if (!args.Player.HasPermission("crossplay.settings"))
                    {
                        args.Player.SendErrorMessage("You do not have permission to reload the configuration.");
                        return;
                    }
                    if (ReloadConfig())
                    {
                        args.Player.SendSuccessMessage("[Crossplay] Configuration reloaded successfully.");
                    }
                    else
                    {
                        args.Player.SendErrorMessage("[Crossplay] Failed to reload configuration. Check logs for details.");
                    }
                    return;
                }
                if (subCommand == "clear")
                {
                    if (!args.Player.HasPermission("crossplay.clear"))
                    {
                        args.Player.SendErrorMessage("You do not have permission to clear dropped items.");
                        return;
                    }
                    int count = 0;
                    for (int i = 0; i < Main.maxItems; i++)
                    {
                        dynamic item = Main.item[i];
                        if (item.active)
                        {
                            item.SetDefaults(0);
                            NetMessage.SendData((int)PacketTypes.UpdateItemDrop, -1, -1, null, i);
                            count++;
                        }
                    }
                    args.Player.SendSuccessMessage($"[Crossplay] Cleared {count} dropped items.");
                    return;
                }
                if (subCommand == "version" || subCommand == "check")
                {
                    if (!Config.Settings.EnableVersionCheckCommand)
                    {
                        args.Player.SendErrorMessage("The version check command is disabled.");
                        return;
                    }
                    if (!args.Player.HasPermission("crossplay.check"))
                    {
                        args.Player.SendErrorMessage("You do not have permission to check player versions.");
                        return;
                    }
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Usage: /crossplay version <player>");
                        return;
                    }
                    string targetName = string.Join(" ", args.Parameters.Skip(1));
                    var players = TSPlayer.FindByNameOrID(targetName);
                    if (players.Count == 0)
                    {
                        args.Player.SendErrorMessage("No players matched.");
                        return;
                    }
                    if (players.Count > 1)
                    {
                        args.Player.SendMultipleMatchError(players.Select(p => p.Name));
                        return;
                    }

                    var target = players[0];
                    if (target.Index < 0 || target.Index >= ClientVersions.Length)
                    {
                        args.Player.SendErrorMessage("Player index out of bounds.");
                        return;
                    }
                    int version = ClientVersions[target.Index];
                    string versionString = _supportedVersions.TryGetValue(version, out string label) ? $"{label} ({version})" : $"Unknown ({version})";
                    if (version == -1) versionString = $"Native ({Main.curRelease})";

                    args.Player.SendSuccessMessage($"[Crossplay] {target.Name} is on version: {versionString}");
                    return;
                }
                if (subCommand == "status")
                {
                    if (!args.Player.HasPermission("crossplay.settings"))
                    {
                        args.Player.SendErrorMessage("You do not have permission to view crossplay status.");
                        return;
                    }
                    args.Player.SendSuccessMessage("[Crossplay] Current Status:");
                    args.Player.SendInfoMessage($"Journey Support: {(Config.Settings.SupportJourneyClients ? "Enabled" : "Disabled")}");
                    args.Player.SendInfoMessage($"Item Limiter: {(Config.Settings.EnableItemLimits ? "Enabled" : "Disabled")}");
                    if (Config.Settings.EnableItemLimits)
                    {
                        args.Player.SendInfoMessage($" - Max Items: {Config.Settings.MaxDroppedItems}");
                        args.Player.SendInfoMessage($" - Despawn Time: {Config.Settings.ItemDespawnSeconds}s");
                    }
                    args.Player.SendInfoMessage($"Version Check Cmd: {(Config.Settings.EnableVersionCheckCommand ? "Enabled" : "Disabled")}");
                    args.Player.SendInfoMessage($"Debug Mode: {(Config.Settings.DebugMode ? "Enabled" : "Disabled")}");
                    return;
                }
                if (subCommand == "resetconfig")
                {
                    if (!args.Player.HasPermission("crossplay.settings"))
                    {
                        args.Player.SendErrorMessage("You do not have permission to reset the configuration.");
                        return;
                    }
                    try
                    {
                        Config.Reset();
                        Config.Write(SavePath);
                        _whitelistedProjectiles = new HashSet<int>(Config.Settings.WhitelistedProjectiles);
                        args.Player.SendSuccessMessage("[Crossplay] Configuration has been reset to default values and reloaded.");
                        Log("Configuration has been reset to default values.", false, ConsoleColor.Yellow);
                    }
                    catch (Exception ex)
                    {
                        args.Player.SendErrorMessage("[Crossplay] An error occurred while resetting the configuration. Check logs for details.");
                        Log($"Failed to reset configuration: {ex.Message}", false, ConsoleColor.Red);
                    }
                    return;
                }
            }
            args.Player.SendErrorMessage("Usage: /crossplay <reload|clear|version|status|resetconfig>");
        }

        private bool ReloadConfig()
        {
            try
            {
                bool writeConfig = true;
                if (File.Exists(SavePath))
                {
                    Config.Read(SavePath, out writeConfig);
                }
                if (writeConfig)
                {
                    Config.Write(SavePath);
                }
                _whitelistedProjectiles = new HashSet<int>(Config.Settings.WhitelistedProjectiles);
                Log("Configuration reloaded from disk.", false, ConsoleColor.Green);
                return true;
            }
            catch (Exception ex)
            {
                Log($"Failed to reload configuration: {ex.Message}", false, ConsoleColor.Red);
                return false;
            }
        }

        private void OnPostInitialize(EventArgs e)
        {
            if (!Config.Settings.ShowStartupBanner) return;

            StringBuilder sb = new StringBuilder()
                .Append("Crossplay has been enabled & has whitelisted the following versions:\n")
                .Append(string.Join(", ", _supportedVersions.Values))
                .Append("\n\nIf there are any issues please report them here: https://github.com/RuriChoco/Terraria-Crossplay/issues");

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("-------------------------------------");

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(sb.ToString());

            if (_supportedVersions.TryGetValue(Main.curRelease, out string versionLabel))
            {
                Console.WriteLine($"[Crossplay] SUCCESS: Detected server version {Main.curRelease} as {versionLabel}");
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("-------------------------------------");
            Console.ResetColor();
        }

        private void OnGetData(GetDataEventArgs args)
        {
            int index = args.Msg.whoAmI;

            // Optimization: Ignore packets from unknown clients (except ConnectRequest)
            if (ClientVersions[index] == 0 && args.MsgID != PacketTypes.ConnectRequest)
            {
                return;
            }

            switch (args.MsgID)
            {
                case PacketTypes.ProjectileNew:
                    HandleProjectileNew(args);
                    break;
                case PacketTypes.NpcAddBuff:
                    if (Config.Settings.EnableNpcBuffFix)
                    {
                        HandleNpcAddBuff(args);
                    }
                    break;
                case PacketTypes.PlayerInfo:
                    HandlePlayerInfo(args);
                    break;
                case PacketTypes.ConnectRequest:
                    HandleConnectRequest(args);
                    break;
            }
        }

        private void HandleProjectileNew(GetDataEventArgs args)
        {
            int index = args.Msg.whoAmI;
            // Core packet data is 21 bytes, plus 1 for flags.
            if (args.Length < 22) return;

            var coreData = MemoryMarshal.Read<ProjectilePacketCore>(args.Msg.readBuffer.AsSpan(args.Index));

            if (Config.Settings.DebugMode) // Keep debug logic for validation
            {
                using (var debugReader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
                {
                    debugReader.ReadInt16(); // Identity
                    debugReader.ReadSingle(); // X
                    debugReader.ReadSingle(); // Y
                    debugReader.ReadSingle(); // VX
                    debugReader.ReadSingle(); // VY
                    debugReader.ReadByte();   // Owner
                    short debugType = debugReader.ReadInt16();
                    if (debugType != coreData.Type)
                    {
                        Log($"[OFFSET ERROR] MemoryMarshal read {coreData.Type}, BinaryReader read {debugType}", true, ConsoleColor.Red);
                    }
                }
            }

            // Whitelist specific projectiles here (e.g., Harpoon = 33)
            if (_whitelistedProjectiles.Contains(coreData.Type))
            {
                // Security: Prevent spawning projectiles for other players
                if (coreData.Owner != index) return;

                if (!TShock.Players[index].HasPermission("crossplay.bypass"))
                {
                    Log($"Player {TShock.Players[index].Name} tried to use bypassed projectile {coreData.Type} without permission.", color: ConsoleColor.Yellow);
                    return;
                }

                byte flags = args.Msg.readBuffer[args.Index + 21];

                // Validation: Ensure packet is large enough for the flags
                int requiredLength = 22;
                if ((flags & 1) == 1) requiredLength += 4; // ai[0]
                if ((flags & 2) == 2) requiredLength += 4; // ai[1]
                if ((flags & 4) == 4) requiredLength += 4; // ai[2]
                if (args.Length < requiredLength) return;

                float ai0 = 0, ai1 = 0, ai2 = 0;
                int offset = args.Index + 22;

                if ((flags & 1) == 1) { ai0 = BitConverter.ToSingle(args.Msg.readBuffer, offset); offset += 4; }
                if ((flags & 2) == 2) { ai1 = BitConverter.ToSingle(args.Msg.readBuffer, offset); offset += 4; }
                if ((flags & 4) == 4) { ai2 = BitConverter.ToSingle(args.Msg.readBuffer, offset); offset += 4; }

                int projIndex = -1;
                // Find existing projectile to update
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    if (Main.projectile[i].active && Main.projectile[i].owner == coreData.Owner && Main.projectile[i].identity == coreData.Identity)
                    {
                        projIndex = i;
                        break;
                    }
                }
                // Or find a free slot
                if (projIndex == -1)
                {
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        if (!Main.projectile[i].active)
                        {
                            projIndex = i;
                            break;
                        }
                    }
                }
                if (projIndex == -1) projIndex = Projectile.FindOldestProjectile();

                Projectile proj = Main.projectile[projIndex];
                if (!proj.active || proj.type != coreData.Type)
                {
                    proj.SetDefaults(coreData.Type);
                    proj.miscText = "";
                }
                proj.identity = coreData.Identity;
                proj.position = new Microsoft.Xna.Framework.Vector2(coreData.X, coreData.Y);
                proj.velocity = new Microsoft.Xna.Framework.Vector2(coreData.VX, coreData.VY);
                proj.owner = coreData.Owner;
                proj.type = coreData.Type;
                if ((flags & 1) == 1) proj.ai[0] = ai0;
                if ((flags & 2) == 2) proj.ai[1] = ai1;
                if ((flags & 4) == 4) proj.ai[2] = ai2;
                proj.active = true;

                NetMessage.SendData((int)PacketTypes.ProjectileNew, -1, index, null, projIndex);
                args.Handled = true;
            }
        }

        private void HandleNpcAddBuff(GetDataEventArgs args)
        {
            if (args.Length < 6) return;

            short npcId;
            ushort buffType;
            int buffTime;

            if (args.Length >= 8)
            {
                var packet = MemoryMarshal.Read<NpcAddBuffPacketLarge>(args.Msg.readBuffer.AsSpan(args.Index));
                npcId = packet.NpcId;
                buffType = packet.BuffType;
                buffTime = packet.BuffTime;
            }
            else
            {
                var packet = MemoryMarshal.Read<NpcAddBuffPacketSmall>(args.Msg.readBuffer.AsSpan(args.Index));
                npcId = packet.NpcId;
                buffType = packet.BuffType;
                buffTime = packet.BuffTime;
            }

            if (npcId >= 0 && npcId < Main.maxNPCs)
            {
                if (Main.npc[npcId].active)
                {
                    Main.npc[npcId].AddBuff(buffType, buffTime);
                    NetMessage.SendData((int)PacketTypes.NpcAddBuff, -1, args.Msg.whoAmI, null, npcId, buffType, buffTime);
                }
                args.Handled = true;
            }
        }

        private void HandlePlayerInfo(GetDataEventArgs args)
        {
            if (!Config.Settings.SupportJourneyClients)
            {
                return;
            }
            if (args.Length < 1) return;
            ref byte gameModeFlags = ref args.Msg.readBuffer[args.Index + args.Length - 1];
            if (Main.GameMode == 3)
            {
                if ((gameModeFlags & 8) != 8)
                {
                    Log($"Enabled journey mode for index {args.Msg.whoAmI}", color: ConsoleColor.Green);
                    gameModeFlags |= 8;
                    if (Main.ServerSideCharacter)
                    {
                        NetMessage.SendData(4, args.Msg.whoAmI, -1, null, args.Msg.whoAmI);
                    }
                }
                return;
            }
            if (TShock.Config.Settings.SoftcoreOnly && (gameModeFlags & 3) != 0)
            {
                return;
            }
            if ((gameModeFlags & 8) == 8)
            {
                Log($"Disabled journey mode for index {args.Msg.whoAmI}", color: ConsoleColor.Green);
                gameModeFlags &= 247;
            }
        }

        private void HandleConnectRequest(GetDataEventArgs args)
        {
            int index = args.Msg.whoAmI;
            // Optimization: Read string manually to avoid BinaryReader/MemoryStream allocation
            // Version string is short ("Terraria" + version), so length is always 1 byte (< 128)
            if (args.Length < 1) return;
            int strLen = args.Msg.readBuffer[args.Index];
            if (strLen >= 128) return;
            if (args.Length < strLen + 1) return;

            string clientVersion = Encoding.UTF8.GetString(args.Msg.readBuffer, args.Index + 1, strLen);
            if (clientVersion.Length != 11)
            {
                args.Handled = true;
                return;
            }
            if (!int.TryParse(clientVersion.AsSpan(clientVersion.Length - 3), out int versionNumber))
            {
                return;
            }
            if (versionNumber == Main.curRelease)
            {
                ClientVersions[index] = -1;
                return;
            }
            if (!_supportedVersions.ContainsKey(versionNumber))
            {
                return;
            }
            ClientVersions[index] = versionNumber;
            if (!MaxItems.ContainsKey(versionNumber))
            {
                Log($"Warning: Version {_supportedVersions[versionNumber]} ({versionNumber}) is missing from MaxItems. Item filtering disabled for index {index}.", color: ConsoleColor.Yellow);
            }
            NetMessage.SendData(9, args.Msg.whoAmI, -1, NetworkText.FromLiteral("Fixing Version..."), 1);
            string targetVersion = _supportedVersions.ContainsKey(Main.curRelease) ? _supportedVersions[Main.curRelease] : $"Unknown(v{Main.curRelease})";
            Log($"Changing version of index {args.Msg.whoAmI} from {_supportedVersions[versionNumber]} => {targetVersion}", color: ConsoleColor.Green);

            // Safety: Ensure we don't overwrite memory beyond the packet buffer
            if (_cachedVersionFixPacket.Length > args.Length + 3)
            {
                Log($"[Crossplay] Error: Generated ConnectRequest ({_cachedVersionFixPacket.Length} bytes) is larger than received buffer ({args.Length + 3} bytes).", color: ConsoleColor.Red);
                return;
            }

            Buffer.BlockCopy(_cachedVersionFixPacket, 0, args.Msg.readBuffer, args.Index - 3, _cachedVersionFixPacket.Length);
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (!Config.Settings.EnableItemLimits) return;

            if ((DateTime.UtcNow - _lastItemCheck).TotalSeconds < 1) return;
            _lastItemCheck = DateTime.UtcNow;

            int maxDroppedItems = Config.Settings.MaxDroppedItems;
            int maxTime = Config.Settings.ItemDespawnSeconds * 60;

            _survivingItemsCache.Clear();

            // Single pass to despawn old items and collect survivors.
            for (int i = 0; i < Main.maxItems; i++)
            {
                dynamic item = Main.item[i];
                if (item.active)
                {
                    if (item.time >= maxTime)
                    {
                        item.SetDefaults(0);
                        NetMessage.SendData((int)PacketTypes.UpdateItemDrop, -1, -1, null, i);
                    }
                    else
                    {
                        _survivingItemsCache.Add((i, (int)item.time));
                    }
                }
            }

            // Now, enforce the max limit only on the surviving items.
            if (_survivingItemsCache.Count > maxDroppedItems)
            {
                // Sort by time to find the oldest items to remove (oldest have highest time).
                _survivingItemsCache.Sort((a, b) => b.time.CompareTo(a.time));

                int toRemove = _survivingItemsCache.Count - maxDroppedItems;
                for (int i = 0; i < toRemove; i++)
                {
                    int idx = _survivingItemsCache[i].index;
                    dynamic item = Main.item[idx];
                    item.SetDefaults(0);
                    NetMessage.SendData((int)PacketTypes.UpdateItemDrop, -1, -1, null, idx);
                }
            }
        }

        private void OnLeave(LeaveEventArgs args)
        {
            ClientVersions[args.Who] = 0;
        }

        public void Log(string message, bool debug = false, ConsoleColor color = ConsoleColor.White)
        {
            if (debug)
            {
                if (Config.Settings.DebugMode)
                {
                    Console.ForegroundColor = color;
                    // Adding timestamp for consistency with TShock logs
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [Crossplay Debug] {message}");
                    Console.ResetColor();
                }
                return;
            }

            string logMessage = $"[Crossplay] {message}";
            switch (color)
            {
                case ConsoleColor.Red:
                    TShock.Log.Error(logMessage);
                    break;
                case ConsoleColor.Yellow:
                    TShock.Log.Warn(logMessage);
                    break;
                default:
                    TShock.Log.Info(logMessage);
                    break;
            }
        }
    }
}
