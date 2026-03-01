﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;

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

            On.Terraria.Net.NetManager.Broadcast_NetPacket_int += NetModuleHandler.OnBroadcast;
            On.Terraria.Net.NetManager.SendToClient += NetModuleHandler.OnSendToClient;

            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData, int.MaxValue);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);

            GeneralHooks.ReloadEvent += OnReload;
            Commands.ChatCommands.Add(new Command("crossplay.settings", CrossplayCommand, "crossplay"));
        }

        private void OnInitialize(EventArgs args)
        {
            if (!File.Exists(TShock.SavePath))
            {
                Directory.CreateDirectory(TShock.SavePath);
            }
            bool writeConfig = true;
            if (File.Exists(SavePath))
            {
                Config.Read(SavePath, out writeConfig);
            }
            if (writeConfig)
            {
                Config.Write(SavePath);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                On.Terraria.Net.NetManager.Broadcast_NetPacket_int -= NetModuleHandler.OnBroadcast;
                On.Terraria.Net.NetManager.SendToClient -= NetModuleHandler.OnSendToClient;

                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);

                GeneralHooks.ReloadEvent -= OnReload;
            }
            base.Dispose(disposing);
        }

        private void OnReload(ReloadEventArgs args)
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
        }

        private void CrossplayCommand(CommandArgs args)
        {
            if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "reload")
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
                args.Player.SendSuccessMessage("[Crossplay] Configuration reloaded successfully.");
                return;
            }
            args.Player.SendErrorMessage("Usage: /crossplay reload");
        }

        private void OnPostInitialize(EventArgs e)
        {
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

            // Optimization: Skip unnecessary allocations for unhandled packets
            if (args.MsgID != PacketTypes.ConnectRequest &&
                args.MsgID != PacketTypes.PlayerInfo &&
                args.MsgID != PacketTypes.NpcAddBuff &&
                args.MsgID != PacketTypes.ProjectileNew)
            {
                return;
            }

            // Optimization: Ignore packets from unknown clients (except ConnectRequest)
            if (ClientVersions[index] == 0 && args.MsgID != PacketTypes.ConnectRequest)
            {
                return;
            }

            // Optimization: Handle ProjectileNew without allocations
            if (args.MsgID == PacketTypes.ProjectileNew)
            {
                // Identity(2) + X(4) + Y(4) + VX(4) + VY(4) + Owner(1) = 19 bytes offset
                if (args.Length < 22) return;

                short type = BitConverter.ToInt16(args.Msg.readBuffer, args.Index + 19);

                if (Config.Settings.DebugMode)
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
                        if (debugType != type)
                        {
                            Log($"[OFFSET ERROR] BitConverter read {type}, BinaryReader read {debugType}", true, ConsoleColor.Red);
                        }
                    }
                }

                // Whitelist specific projectiles here (e.g., Harpoon = 33)
                if (Config.Settings.WhitelistedProjectiles.Contains(type))
                {
                    byte owner = args.Msg.readBuffer[args.Index + 18];

                    // Security: Prevent spawning projectiles for other players
                    if (owner != index) return;

                    if (!TShock.Players[index].HasPermission("crossplay.bypass"))
                    {
                        Log($"Player {TShock.Players[index].Name} tried to use bypassed projectile {type} without permission.", color: ConsoleColor.Yellow);
                        return;
                    }

                    short identity = BitConverter.ToInt16(args.Msg.readBuffer, args.Index);
                    float x = BitConverter.ToSingle(args.Msg.readBuffer, args.Index + 2);
                    float y = BitConverter.ToSingle(args.Msg.readBuffer, args.Index + 6);
                    float vx = BitConverter.ToSingle(args.Msg.readBuffer, args.Index + 10);
                    float vy = BitConverter.ToSingle(args.Msg.readBuffer, args.Index + 14);

                    byte flags = args.Msg.readBuffer[args.Index + 21];

                    // Validation: Ensure packet is large enough for the flags
                    int requiredLength = 22;
                    if ((flags & 1) == 1) requiredLength += 4;
                    if ((flags & 2) == 2) requiredLength += 4;
                    if ((flags & 4) == 4) requiredLength += 4;
                    if (args.Length < requiredLength) return;

                    float[] ai = new float[Projectile.maxAI];
                    int offset = args.Index + 22;

                    if ((flags & 1) == 1) { ai[0] = BitConverter.ToSingle(args.Msg.readBuffer, offset); offset += 4; }
                    if ((flags & 2) == 2) { ai[1] = BitConverter.ToSingle(args.Msg.readBuffer, offset); offset += 4; }
                    if ((flags & 4) == 4) { ai[2] = BitConverter.ToSingle(args.Msg.readBuffer, offset); offset += 4; }

                    int projIndex = -1;
                    // Find existing projectile to update
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        if (Main.projectile[i].active && Main.projectile[i].owner == owner && Main.projectile[i].identity == identity)
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
                    if (!proj.active || proj.type != type)
                    {
                        proj.SetDefaults(type);
                        proj.miscText = "";
                    }
                    proj.identity = identity;
                    proj.position = new Microsoft.Xna.Framework.Vector2(x, y);
                    proj.velocity = new Microsoft.Xna.Framework.Vector2(vx, vy);
                    proj.owner = owner;
                    proj.type = type;
                    if ((flags & 1) == 1) proj.ai[0] = ai[0];
                    if ((flags & 2) == 2) proj.ai[1] = ai[1];
                    if ((flags & 4) == 4) proj.ai[2] = ai[2];
                    proj.active = true;

                    NetMessage.SendData((int)PacketTypes.ProjectileNew, -1, index, null, projIndex);
                    args.Handled = true;
                }
                return;
            }

            // Optimization: Handle NpcAddBuff without allocations
            if (args.MsgID == PacketTypes.NpcAddBuff)
            {
                if (args.Length < 6) return;

                short npcId = BitConverter.ToInt16(args.Msg.readBuffer, args.Index);
                ushort buffType = BitConverter.ToUInt16(args.Msg.readBuffer, args.Index + 2);
                int buffTime;

                if (args.Length >= 8)
                    buffTime = BitConverter.ToInt32(args.Msg.readBuffer, args.Index + 4);
                else
                    buffTime = BitConverter.ToInt16(args.Msg.readBuffer, args.Index + 4);

                if (npcId >= 0 && npcId < Main.maxNPCs)
                {
                    if (Main.npc[npcId].active)
                    {
                        Main.npc[npcId].AddBuff(buffType, buffTime);
                        NetMessage.SendData((int)PacketTypes.NpcAddBuff, -1, args.Msg.whoAmI, null, npcId, buffType, buffTime);
                    }
                    args.Handled = true;
                }
                return;
            }

            // Optimization: Handle PlayerInfo without allocations
            if (args.MsgID == PacketTypes.PlayerInfo)
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
                return;
            }

            if (args.MsgID == PacketTypes.ConnectRequest)
            {
                using (var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
                {
                    try
                    {
                            string clientVersion = reader.ReadString();
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
                            byte[] connectRequest = new PacketFactory()
                                .SetType(1)
                                .PackString($"Terraria{Main.curRelease}")
                                .GetByteData();
                            string targetVersion = _supportedVersions.ContainsKey(Main.curRelease) ? _supportedVersions[Main.curRelease] : $"Unknown(v{Main.curRelease})";
                            Log($"Changing version of index {args.Msg.whoAmI} from {_supportedVersions[versionNumber]} => {targetVersion}", color: ConsoleColor.Green);

                            // Safety: Ensure we don't overwrite memory beyond the packet buffer
                            if (connectRequest.Length > args.Length + 3)
                            {
                                Log($"[Crossplay] Error: Generated ConnectRequest ({connectRequest.Length} bytes) is larger than received buffer ({args.Length + 3} bytes).", color: ConsoleColor.Red);
                                return;
                            }

                            Buffer.BlockCopy(connectRequest, 0, args.Msg.readBuffer, args.Index - 3, connectRequest.Length);
                    }
                    catch { }
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
                    Console.WriteLine($"[Crossplay Debug] {message}");
                    Console.ResetColor();
                }
                return;
            }
            Console.ForegroundColor = color;
            Console.WriteLine($"[Crossplay] {message}");
            Console.ResetColor();
        }
    }
}
