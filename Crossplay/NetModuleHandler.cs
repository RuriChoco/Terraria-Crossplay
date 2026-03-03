﻿using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Net;

namespace Crossplay
{
    internal class NetModuleHandler
    {
        [ThreadStatic]
        private static byte[] _reusableBuffer;

        internal static void OnBroadcast(On.Terraria.Net.NetManager.orig_Broadcast_NetPacket_int orig, NetManager self, NetPacket packet, int ignoreClient)
        {
            // Optimization: Only intercept packets that might contain unsupported items.
            if (packet.Id != 5 && packet.Id != 21) // PlayerSlot, UpdateItemDrop
            {
                orig(self, packet, ignoreClient);
                return;
            }

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (i != ignoreClient && Netplay.Clients[i].IsConnected() && !InvalidNetPacket(packet, i))
                {
                    self.SendData(Netplay.Clients[i].Socket, packet);
                }
            }
        }

        internal static void OnSendToClient(On.Terraria.Net.NetManager.orig_SendToClient orig, NetManager self, NetPacket packet, int playerId)
        {
            // Before intercepting, ensure the client is actually connected to avoid issues on disconnect.
            if (!Netplay.Clients[playerId].IsConnected())
            {
                orig(self, packet, playerId);
                return;
            }

            // Optimization: Only intercept packets that might contain unsupported items.
            if (packet.Id != 5 && packet.Id != 21) // PlayerSlot, UpdateItemDrop
            {
                orig(self, packet, playerId);
                return;
            }

            if (!InvalidNetPacket(packet, playerId))
            {
                orig(self, packet, playerId);
            }
        }

        private static bool InvalidNetPacket(NetPacket packet, int playerId)
        {
            int clientVersion = CrossplayPlugin.Instance.ClientVersions[playerId];
            if (clientVersion <= 0) // Same version or not a crossplay client
            {
                return false;
            }

            if (ShouldFilterPacket(packet.Id, packet.Buffer.Data, 0, clientVersion, CrossplayPlugin.Instance.MaxItems, out int netIdOffset))
            {
                // Optimization: To avoid allocations, use a reusable thread-static buffer.
                // This prevents inventory desync without constant GC pressure.
                if (_reusableBuffer == null || _reusableBuffer.Length < packet.Length)
                {
                    _reusableBuffer = new byte[packet.Length];
                }
                Buffer.BlockCopy(packet.Buffer.Data, 0, _reusableBuffer, 0, packet.Length);

                // If the packet should be filtered, we zero out its netID to prevent
                // clients with older versions from crashing or seeing "ghost" items.
                if (netIdOffset > 0 && _reusableBuffer.Length >= netIdOffset + 2)
                {
                    // Set netID to 0 directly. Assumes little-endian, which is safe for Terraria.
                    _reusableBuffer[netIdOffset] = 0;
                    _reusableBuffer[netIdOffset + 1] = 0;
                }

                Netplay.Clients[playerId].Socket.AsyncSend(_reusableBuffer, 0, packet.Length, delegate { }, null);
                return true; // Packet was handled (filtered), so don't call orig.
            }

            return false;
        }

        internal static bool ShouldFilterPacket(int packetId, byte[] data, int offset, int clientVersion, Dictionary<int, int> maxItems, out int netIdOffset)
        {
            netIdOffset = -1;
            short itemNetID;

            switch (packetId)
            {
                case 5: // PlayerInventorySlot
                    {
                        // Packet 5: PlayerSlot. netID is at an offset in the buffer.
                        // Header(3) + player(1) + slot(2) + stack(2) + prefix(1) = 9
                        netIdOffset = offset + 9;
                        if (data.Length < netIdOffset + 2) return false;
                        itemNetID = BitConverter.ToInt16(data, netIdOffset);
                        
                        if (maxItems.TryGetValue(clientVersion, out int maxItem) &&
                            itemNetID > maxItem)
                        {
                            return true;
                        }
                    }
                    break;
                case 21: // UpdateItemDrop
                    {
                        // Payload: itemIndex(2), pos(8), vel(8), stack(2), prefix(1), noDelay(1), netID(2)
                        // netID is at payload offset 22. Packet data starts at index 3.
                        // So netID is at index 3 + 22 = 25.
                        netIdOffset = offset + 25;
                        if (data.Length < netIdOffset + 2) return false;
                        itemNetID = BitConverter.ToInt16(data, netIdOffset);

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
