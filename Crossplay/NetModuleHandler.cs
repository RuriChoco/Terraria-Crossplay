﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.Net;

namespace Crossplay
{
    internal class NetModuleHandler
    {
        internal static void OnBroadcast(On.Terraria.Net.NetManager.orig_Broadcast_NetPacket_int orig, NetManager self, NetPacket packet, int ignoreClient)
        {
            for (int i = 0; i <= Main.maxPlayers; i++)
            {
                if (i != ignoreClient && Netplay.Clients[i].IsConnected() && !InvalidNetPacket(packet, i))
                {
                    self.SendData(Netplay.Clients[i].Socket, packet);
                }
            }
        }

        internal static void OnSendToClient(On.Terraria.Net.NetManager.orig_SendToClient orig, NetManager self, NetPacket packet, int playerId)
        {
            if (!InvalidNetPacket(packet, playerId))
            {
                orig(self, packet, playerId);
            }
        }

        private static bool InvalidNetPacket(NetPacket packet, int playerId)
        {
            return ShouldFilterPacket(packet.Id, packet.Buffer.Data, CrossplayPlugin.Instance.ClientVersions[playerId], CrossplayPlugin.Instance.MaxItems);
        }

        internal static bool ShouldFilterPacket(int packetId, byte[] data, int clientVersion, Dictionary<int, int> maxItems)
        {
            switch (packetId)
            {
                case 5:
                    {
                        // Ensure data is long enough: Offset (6) + sizeof(short) (2) = 8
                        if (data.Length < 8)
                        {
                            return false;
                        }
                        var itemNetID = MemoryMarshal.Cast<byte, short>(data.AsSpan(6))[0];
                        
                        if (maxItems.TryGetValue(clientVersion, out int maxItem) &&
                            itemNetID > maxItem)
                        {
                            return true;
                        }
                    }
                    break;
            }
            return false;
        }
    }
}
