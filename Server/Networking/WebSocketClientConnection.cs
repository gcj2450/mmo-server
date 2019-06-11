﻿// ================================================================================================================================
// File:        WebSocketClientConnection.cs
// Description: WebSocket implementation of the ClientConnection class
// ================================================================================================================================

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Server.Interface;
using Server.Maths;

namespace Server.Networking
{
    public class WebSocketClientConnection
    {
        public int NetworkID;   //Each client has a unique network ID so they dont get mixed up
        public TcpClient NetworkConnection; //The servers network connection to this client
        private bool ConnectionUpgraded = false;    //When clients first connect we need to handshake them
        public NetworkStream DataStream;    //Information is transmitted back and forth with this
        public byte[] DataBuffer;   //Data is streamed into here during asynchronous reading

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="NewConnection">ConnectionManager detects new client connections then uses it to make this</param>
        public WebSocketClientConnection(TcpClient NewConnection)
        {
            //Store the connection to the new client and assign them a new network ID
            NetworkConnection = NewConnection;
            NetworkID = ((IPEndPoint)NetworkConnection.Client.RemoteEndPoint).Port;

            //Set up the datastream and buffer, then start listening for messages from the client
            NetworkConnection.SendBufferSize = 4096;
            NetworkConnection.ReceiveBufferSize = 4096;
            DataStream = NetworkConnection.GetStream();
            DataBuffer = new byte[NetworkConnection.Available];
            DataStream.BeginRead(DataBuffer, 0, DataBuffer.Length, ReadPacket, null);
        }

        /// <summary>
        /// Triggers after completion of asynchronous datastream reading has completed transmitting data
        /// </summary>
        /// <param name="Result">Represents the status of an asynchronous operation</param>
        private void ReadPacket(IAsyncResult Result)
        {
            //Copy the current packet data into a new array, then clear it out and immediately start using it to start streaming in data again from the client
            int PacketSize = DataStream.EndRead(Result);
            byte[] PacketBuffer = new byte[PacketSize];
            Array.Copy(DataBuffer, PacketBuffer, PacketSize);
            DataBuffer = new byte[NetworkConnection.Available];
            DataStream.BeginRead(DataBuffer, 0, DataBuffer.Length, ReadPacket, null);

            //Upgrade this clients connection if it is brand new
            if (!ConnectionUpgraded)
                UpgradeConnection(PacketBuffer);
            //Otherwise we need to extract the clients message from the buffer and decode it to become readable again
            else if(PacketSize != 0)
            {
                Log.PrintDebugMessage("Packet Size: " + PacketSize);
                //When recieving messages from clients they will be encoded, visit https://tools.ietf.org/html/rfc6455#section-5.2 for more information on how decoding works

                //Lets first extract the data from the first byte
                byte FirstByte = PacketBuffer[0];
                bool FIN = DataExtractor.ReadBit(FirstByte, 0);   //Value of 1 indicates if this is the final fragment of the message, this first fragment MAY also be the final fragment
                bool RSV1 = DataExtractor.ReadBit(FirstByte, 1);  //Set to 0 unless an extension is negotatied that defines meanings for non-zero values. Unexpected non-zero values means we should close down the connection.
                bool RSV2 = DataExtractor.ReadBit(FirstByte, 2);
                bool RSV3 = DataExtractor.ReadBit(FirstByte, 3);
                bool[] OpCode = DataExtractor.ReadBits(FirstByte, 4, 7);

                //Extracting the second byte from the packet buffer
                byte SecondByte = PacketBuffer[1];
                bool MASK = DataExtractor.ReadBit(SecondByte, 0);

                //Before we go any further we need to figure out the size of the payload data, as this may effect where we read the rest of the data from
                //Converting the 2nd byte to a binary string, then converting bits 1-7 to decimal gives us the first possible length value of the payload data
                string SecondByteBinary = BinaryConverter.ByteToBinaryString(PacketBuffer[1]);
                string PayloadBinary = SecondByteBinary.Substring(1, 7);
                int PayloadLength = BinaryConverter.BinaryStringToDecimal(PayloadBinary);

                //Byte indices where we will begin reading in the decoding mask and payload data later on, these will be updated if we needed to read extra bytes to find out the payload length
                int DecodingMaskIndex = 2;
                int PayloadDataIndex = 6;

                //With a length between 0-125 we continue as normal
                //With a length equal to 126, we read bytes 3-4 to find the actual length
                if(PayloadLength == 126)
                {
                    byte[] PayloadBytes = DataExtractor.ReadBytes(PacketBuffer, 3, 4);
                    PayloadBinary = BinaryConverter.ByteArrayToBinaryString(PayloadBytes);
                    PayloadLength = BinaryConverter.BinaryStringToDecimal(PayloadBinary);
                    //Increment the DecodingMask and PayloadData indices by 2, as 3,4 contained the payload length
                    DecodingMaskIndex += 2;
                    PayloadDataIndex += 2;
                }
                //With a length equal to 127, we read bytes 3-10 to find the actual length
                else if(PayloadLength == 127)
                {
                    byte[] PayloadBytes = DataExtractor.ReadBytes(PacketBuffer, 3, 10);
                    PayloadBinary = BinaryConverter.ByteArrayToBinaryString(PayloadBytes);
                    PayloadLength = BinaryConverter.BinaryStringToDecimal(PayloadBinary);
                    //Increment the DecodingMask and PayloadData indices by 8, as bytes 3-10 contained the payload length
                    DecodingMaskIndex += 8;
                    PayloadDataIndex += 8;
                }

                //Extract the decoding mask bytes from the packet buffer
                byte[] DecodingMask = new byte[4] { PacketBuffer[DecodingMaskIndex], PacketBuffer[DecodingMaskIndex + 1], PacketBuffer[DecodingMaskIndex + 2], PacketBuffer[DecodingMaskIndex + 3] };

                //Extract the payload data from the packet buffer, using the mask to decode each byte as we extract it from the packet buffer
                byte[] PayloadData = new byte[PayloadLength];
                for (int i = 0; i < PayloadLength; i++)
                    PayloadData[i] = (byte)(PacketBuffer[PayloadDataIndex + i] ^ DecodingMask[i % 4]);

                //Convert the PayloadData array into an ASCII string
                string FinalMessage = Encoding.ASCII.GetString(PayloadData);

                Log.PrintDebugMessage(FinalMessage);
            }
        }
        
        /// <summary>
        /// Handshakes with a new network client, upgrading their connection to WebSocket from HTTP
        /// </summary>
        private void UpgradeConnection(byte[] PacketBuffer)
        {
            //Convert the data in the packet buffer into string format
            string PacketData = Encoding.UTF8.GetString(PacketBuffer);

            //Make sure the new client sent a proper GET request before we complete the handshake
            if(new System.Text.RegularExpressions.Regex("^GET").IsMatch(Encoding.UTF8.GetString(PacketBuffer)))
            {
                //Return the correct response to complete the handshake and upgrade the client to websockets
                string EOL = "\r\n";
                byte[] HandshakeResponse = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + EOL
                    + "Connection: Upgrade" + EOL
                    + "Upgrade: websocket" + EOL
                    + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                        System.Security.Cryptography.SHA1.Create().ComputeHash(
                            Encoding.UTF8.GetBytes(
                                new System.Text.RegularExpressions.Regex("Sec-WebSocket-Key: (.*)").Match(PacketData).Groups[1].Value.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                                )
                            )
                        ) + EOL
                        + EOL);

                //Send the completed handshake response to the client
                DataStream.BeginWrite(HandshakeResponse, 0, HandshakeResponse.Length, null, null);
            }

            //Take note that we have completed upgrading this clients connection
            ConnectionUpgraded = true;
        }
    }
}