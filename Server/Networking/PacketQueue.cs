﻿// ================================================================================================================================
// File:        PacketQueue.cs
// Description: Stores a list of outgoing network packets to be sent to their target game clients in the next communication interval
// Author:	    Harley Laurie https://www.github.com/Swaelo/
// ================================================================================================================================

using System.Collections.Generic;
using Server.Logging;

namespace Server.Networking
{
    public static class PacketQueue
    {
        private static float CommunicationInterval = 0.1f;    //How often the outgoing packets list will be transmitted to each client
        private static float NextCommunication = 0.1f;    //Time remaining before we next transmitted all queued packets to their clients

        //Adds a network packet onto one of the clients outgoing packet queues
        public static void QueuePacket(int ClientID, NetworkPacket Packet)
        {
            //Get the client this packet will eventually be sent to
            ClientConnection Client = ConnectionManager.GetClient(ClientID);

            //Make sure were still connected to them
            if (Client == null)
            {
                MessageLog.Print("ERROR: Client not found, cant add packet to their queue.");
                return;
            }

            //Add the packet to their queue
            Client.QueuePacket(Packet);
        }

        //Tracks time and sends out all clients packet queues every interval
        public static void UpdateQueue(float DeltaTime, bool Transmit)
        {
            //Count down the timer until it reaches zero
            NextCommunication -= DeltaTime;
            if (NextCommunication <= 0f)
                TransmitPackets(Transmit);
        }

        //Transmits the packets in all clients outgoing queues to them
        private static void TransmitPackets(bool Transmit)
        {
            //Loop through all the active clients in the game and have each one sent their queue
            foreach (ClientConnection Client in ConnectionManager.GetClients())
                Client.TransmitPackets(Transmit);

            //Re-enable the queue updater and reset the communication interval timer
            NextCommunication = CommunicationInterval;
        }
    }
}