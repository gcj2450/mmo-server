﻿// ================================================================================================================================
// File:        AccountManagementPacketSenders.cs
// Description: Formats and delivers network packets to game clients while they are logging into or creating new user accounts
// Author:	    Harley Laurie https://www.github.com/Swaelo/
// ================================================================================================================================

using Server.Data;
using Server.Database;
using Server.Logging;

namespace Server.Networking.PacketSenders
{
    public static class AccountManagementPacketSenders
    {
        /// <summary>
        /// Replies to a clients request to login to a user account
        /// </summary>
        /// <param name="ClientID">The clients network ID</param>
        /// <param name="LoginSuccess">If the login request was a success</param>
        /// <param name="ReplyMessage">Details what happened if the request was denied</param>
        public static void SendAccountLoginReply(int ClientID, bool LoginSuccess, string ReplyMessage)
        {
            CommunicationLog.LogOut(ClientID + " account login reply");

            //Create a new NetworkPacket object to store the data for this account login reply
            NetworkPacket Packet = new NetworkPacket();

            //Write the relevant data values into the network packet
            Packet.WriteType(ServerPacketType.AccountLoginReply);
            Packet.WriteBool(LoginSuccess);
            Packet.WriteString(ReplyMessage);

            //Add this packet to the target clients outgoing packet queue
            PacketQueue.QueuePacket(ClientID, Packet);
            //ConnectionManager.SendPacket(ClientID, Packet);
        }

        /// <summary>
        /// Replies to a clients request to register a brand new user account
        /// </summary>
        /// <param name="ClientID">The clients network ID</param>
        /// <param name="RegistrationSuccess">If the account registration request was a success</param>
        /// <param name="ReplyMessage">What went wrong if the request was denied</param>
        public static void SendAccountRegistrationReply(int ClientID, bool RegistrationSuccess, string ReplyMessage)
        {
            CommunicationLog.LogOut(ClientID + " account registration reply");

            //Create a new NetworkPacket object to store the data for this account login reply
            NetworkPacket Packet = new NetworkPacket();

            //Write the relevant data values into the network packet
            Packet.WriteType(ServerPacketType.AccountRegistrationReply);
            Packet.WriteBool(RegistrationSuccess);
            Packet.WriteString(ReplyMessage);

            //Add this packet to the target clients outgoign packet queue
            PacketQueue.QueuePacket(ClientID, Packet);
            //ConnectionManager.SendPacket(ClientID, Packet);
        }

        /// <summary>
        /// Sends a client a list of characters that they have created under their account
        /// </summary>
        /// <param name="ClientID">The clients network ID</param>
        /// <param name="AccountName">The account the client is logged into</param>
        public static void SendCharacterDataReply(int ClientID, string AccountName)
        {
            CommunicationLog.LogOut(ClientID + " character data reply");

            //Create a new NetworkPacket object to store the data for this account login reply
            NetworkPacket Packet = new NetworkPacket();

            //Write the packet type and number of existing characters this user currently has
            Packet.WriteType(ServerPacketType.CharacterDataReply);
            int CharacterCount = CharactersDatabase.GetCharacterCount(AccountName);
            Packet.WriteInt(CharacterCount);

            //Loop through all of their characters and write each ones information into the packet data
            for(int i = 0; i < CharacterCount; i++)
            {
                //Get each characters name from the database, then use that to fetch the rest of that characters data from the database also
                string CharacterName = CharactersDatabase.GetCharacterName(AccountName, i + 1);
                CharacterData CharacterData = CharactersDatabase.GetCharacterData(CharacterName);

                //Write all of this characters information into the packet data
                Packet.WriteString(CharacterName);
                Packet.WriteVector3(CharacterData.Position);
                Packet.WriteQuaternion(CharacterData.Rotation);
                Packet.WriteFloat(CharacterData.CameraZoom);
                Packet.WriteFloat(CharacterData.CameraXRotation);
                Packet.WriteFloat(CharacterData.CameraYRotation);
            }

            //Add this to the packet queue
            PacketQueue.QueuePacket(ClientID, Packet);
            //ConnectionManager.SendPacket(ClientID, Packet);
        }

        /// <summary>
        /// Replies to a clients new character creation request
        /// </summary>
        /// <param name="ClientID">The clients network ID</param>
        /// <param name="CreationSuccess">If the new character creation request was granted</param>
        /// <param name="ReplyMessage">What went wrong if the request was denied</param>
        public static void SendCreateCharacterReply(int ClientID, bool CreationSuccess, string ReplyMessage)
        {
            CommunicationLog.LogOut(ClientID + " character creation reply");

            //Create a new NetworkPacket object to store the data for this account login reply
            NetworkPacket Packet = new NetworkPacket();

            //Write the relevant data values into the packet data
            Packet.WriteType(ServerPacketType.CharacterCreationReply);
            Packet.WriteBool(CreationSuccess);
            Packet.WriteString(ReplyMessage);

            //Add this packet to the target clients outgoing packets queue
            PacketQueue.QueuePacket(ClientID, Packet);
            //ConnectionManager.SendPacket(ClientID, Packet);
        }
    }
}