using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Timers;
using LogWriterAPI;
using PacketAPI;
using ClientAPI;
using System.IO;

namespace TestUdpClient
{
    class ChatClient
    {
        private static Socket clientSocket;
        private static Socket LBClientSocket;


        private EndPoint epServer;

        private byte[] dataStream = new byte[1024];

        private static string fileName = @"C:\Users\minaam.fasihi\Documents\Projects\Client-logs-";

        private static int sequenceNumber = 0;
        private static int expectedSequenceNumber;

        Client client;

        private static int port;
        private static int LBport = 9000;
        private static int latestSendPktACKED = 0;
        private static int latestReceivePktACKED = 0;
        private static int windowSize = 4;
        private static int oldestSendPacketSeqNum = 0;
        private static int latestSendPacketSeqNum = 0;
        private static int oldestReceivePacketSeqNum = 0;
        private static int latestReceivePacketSeqNum = 0;
        private static int numOfPktsProduced = 0;
        private static int prevNumOfPktsProduced = 0;
        private static bool alreadySwapped = false;

        private static SortedDictionary<int, Packet> receiveMessageBuffer = new SortedDictionary<int, Packet>();

        public static ManualResetEvent allDone = new ManualResetEvent(false);

        private static AutoResetEvent processSendQueue = new AutoResetEvent(false);
        private static AutoResetEvent processReceiveQueue = new AutoResetEvent(false);
        private static AutoResetEvent cleanerSendQueue = new AutoResetEvent(false);
        private static AutoResetEvent cleanerReceiveQueue = new AutoResetEvent(false);
        private static AutoResetEvent partialCleanerSendBuffer = new AutoResetEvent(false);

        private static LogWriter logger = Logger.Instance;

        private static System.Timers.Timer sendTimer;
        private static System.Timers.Timer receiveTimer;
        private static System.Timers.Timer aTimer;

        private static object syncSendBuffer = new object();
        private static object syncReceiveBuffer = new object();

        private static string serverIPAddress;
        private static string LBIPAddress;
        private static string clientIPAddress;

        public ChatClient()
        {
            sequenceNumber = 0;
        }

        private void ConnectToLoadBalancer()
        {
            string logMsg = "";
            try
            {
                logMsg = DateTime.Now + ":\t In ConnectToLoadBalancer()";
                logger.Log(logMsg);
                allDone.Reset();
                IPAddress ipAddr = IPAddress.Parse(LBIPAddress);
                IPEndPoint remoteEndPoint = new IPEndPoint(ipAddr, LBport);

                LBClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                LBClientSocket.BeginConnect(remoteEndPoint, new AsyncCallback(LBConnectCallback), LBClientSocket);
                allDone.WaitOne();
                logMsg = DateTime.Now + ":\t Client has started and is trying to connect to the LB.";

                Console.WriteLine(logMsg);
                logger.Log(logMsg);
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
            logMsg = DateTime.Now + ":\t Exiting ConnectToLoadBalancer()";
            logger.Log(logMsg);
        }

        private void LBConnectCallback(IAsyncResult ar)
        {
            string logMsg = "";
            try
            {
                logMsg = DateTime.Now + ":\t In LBConnectCallback()";
                logger.Log(logMsg);
                Socket sock = (Socket)ar.AsyncState;
                sock.EndConnect(ar);
                LBRequestForServer();
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + ":\t " + e.ToString();
                logger.Log(logMsg);
            }
            logMsg = DateTime.Now + ":\t Exiting LBConnectCallback()";
            logger.Log(logMsg);
        }

        private void LBRequestForServer()
        {
            string logMsg = "";
            try
            {
                logMsg = DateTime.Now + ":\t In LBRequestForServer()";
                logger.Log(logMsg);
                IPEndPoint clientEP = new IPEndPoint(IPAddress.Parse(clientIPAddress), port);
                client = new Client("minaam", "fasihi", (EndPoint)clientEP);

                Packet sendData = new Packet(client.FriendName);
                sendData.SenderName = client.Name;
                sendData.ChatMessage = "request";
                sendData.RecipientName = client.FriendName;
                byte[] byteData = sendData.GetDataStream();

                logMsg = DateTime.Now + ":\t Requesting for server.";
                Console.WriteLine(logMsg);
                logger.Log(logMsg);
                LBClientSocket.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(LBRequestForServerCallback), LBClientSocket);
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + ":\t " + e.ToString();
                logger.Log(logMsg);
            }
            logMsg = DateTime.Now + ":\t Exiting LBRequestForServer()";
            logger.Log(logMsg);
        }

        private void LBRequestForServerCallback(IAsyncResult ar)
        {
            string logMsg = "";
            try
            {
                logMsg = DateTime.Now + ":\t In LBRequestForServerCallback()";
                logger.Log(logMsg);
                Socket sock = (Socket)ar.AsyncState;
                sock.EndSend(ar);
                sock.BeginReceive(this.dataStream, 0, this.dataStream.Length, 0, new AsyncCallback(LBReceiveCallback), sock);
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
            logMsg = DateTime.Now + ":\t Exiting LBRequestForServerCallback()";
            logger.Log(logMsg);
        }

        private void LBReceiveCallback(IAsyncResult ar)
        {
            string logMsg = "";
            try
            {
                logMsg = DateTime.Now + ":\t In LBReceiveCallback()";
                logger.Log(logMsg);
                Socket client = (Socket)ar.AsyncState;
                client.EndReceive(ar);
                Packet receivedData = new Packet(this.dataStream);
                Console.WriteLine("My server should be: {0}", receivedData.ChatMessage);
                char[] delimiters = { ':' };
                string[] serverAddress = receivedData.ChatMessage.Split(delimiters);
                IPAddress serverIP = IPAddress.Parse(serverAddress[0]);
                IPEndPoint server = new IPEndPoint(serverIP, int.Parse(serverAddress[1]));
                epServer = (EndPoint)server;
                logMsg = DateTime.Now + ":\t This is the address of server received from LB: " + server.Address + ":" + server.Port;
                logger.Log(logMsg);
                allDone.Set();
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
            logMsg = DateTime.Now + ":\t Exiting LBReceiveCallback()";
            logger.Log(logMsg);
        }

        private void ConnectToServer()
        {
            string logMsg = "";
            try
            {
                logMsg = DateTime.Now + ":\t In ConnectToServer()";
                logger.Log(logMsg);
                Console.WriteLine("Please enter your chat username");
                client.Name = Console.ReadLine();

                // Initialise a packet object to store the data to be sent
                Console.WriteLine("Please enter the name of user who you want to chat with");
                client.FriendName = Console.ReadLine();
                Packet sendData = new Packet(client.FriendName);
                sendData.SenderName = client.Name;
                sendData.ChatMessage = null;
                sendData.ChatDataIdentifier = DataIdentifier.LogIn;
                // Initialise socket
                client.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                client.socket.Bind(new IPEndPoint(IPAddress.Parse(clientIPAddress), port));
                // Get packet as byte array
                byte[] data = sendData.GetDataStream();

                expectedSequenceNumber = windowSize;
                // Send data to server
                client.socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, epServer, new AsyncCallback(SendData), null);

                // Begin listening for broadcasts
                client.socket.BeginReceiveFrom(this.client.DataStream, 0, this.client.DataStream.Length, SocketFlags.None, ref epServer, new AsyncCallback(this.ReceiveData), null);
                logMsg = DateTime.Now + ":\t Client is trying to connect to the server";
                logger.Log(logMsg);
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + ":\t " + e.ToString();
                logger.Log(logMsg);
            }
            logMsg = DateTime.Now + ":\t Exiting ConnectToServer()";
            logger.Log(logMsg);
        }

        private void SendMessage()
        {
            string logMsg = "";
            while (true)
            {
                try
                {
                    Packet sendData = new Packet(client.FriendName);
                    logMsg = DateTime.Now + ":\t In SendMessage()";
                    logger.Log(logMsg);

                    sendData.SenderName = client.Name;
                    sendData.ChatMessage = client.Name + ": ";
                    sendData.ChatMessage += Console.ReadLine();
                    sendData.SequenceNumber = ++sequenceNumber;
                    sendData.ChatDataIdentifier = DataIdentifier.Message;
                    incrementLatestSendPacket(sendData.SequenceNumber);

                    if (sendData.ChatMessage.ToLower() == "quit")
                    {
                        Logout(sendData);
                        return;
                    }

                    try
                    {
                        byte[] byteData = sendData.GetDataStream();
                        client.InsertInSendBuffer(sendData.SequenceNumber, byteData);
                        processSendQueue.Set();
                    }

                    catch (Exception e)
                    {
                        logMsg = DateTime.Now + ":\t " + e.ToString();
                        logger.Log(logMsg);
                        latestSendPacketSeqNum--;
                    }
                }
                catch (Exception e)
                {
                    logMsg = DateTime.Now + ":\t " + e.ToString();
                    logger.Log(logMsg);
                    throw;
                }
            }
        }

        private void ProcessSendQueue()
        {
            while (true)
            {
                processSendQueue.WaitOne();

                string logMsg = "";
                logMsg = DateTime.Now + ":\t In ProcessSendQueue()";
                logger.Log(logMsg);

                try
                {
                    if (client.ConsumerSendBuffer.Count == 0 /*&& !alreadySwapped*/)
                    {
                        client.SwapSendBuffers();
                        alreadySwapped = true;
                    }

                    if (client.ConsumerSendBuffer.Count != 0)
                    {
                        foreach (KeyValuePair<int, byte[]> kvp in client.ConsumerSendBuffer)
                        {
                            client.socket.BeginSendTo(kvp.Value, 0, kvp.Value.Length, SocketFlags.None, epServer, new AsyncCallback(SendData), client.socket);
                            client.InsertInAwaitingSendACKsBuffer(kvp.Key, kvp.Value);
                        }
                    }
                    client.ConsumerSendBuffer.Clear();
                    logMsg = DateTime.Now + ":\t Exiting ProcessSendQueue()";
                    logger.Log(logMsg);
                }
                catch (Exception e)
                {
                    logMsg = DateTime.Now + ":\t " + e.ToString();
                    logger.Log(logMsg);
                }
            }
        }

        private void Logout(Packet sendData)
        {
            string logMsg = "";
            try
            {
                logMsg = DateTime.Now + ":\t In Logout()";
                logger.Log(logMsg);
                if (clientSocket != null)
                {
                    // Initialise a packet object to store the data to be sent
                    sendData.ChatDataIdentifier = DataIdentifier.LogOut;
                    sendData.SenderName = client.Name;
                    sendData.ChatMessage = null;

                    // Get packet as byte array
                    byte[] byteData = sendData.GetDataStream();

                    // Send packet to the server
                    clientSocket.SendTo(byteData, 0, byteData.Length, SocketFlags.None, epServer);

                    // Close the socket
                    clientSocket.Close();
                }
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + ":\t " + e.ToString();
                logger.Log(logMsg);
            }
            logMsg = DateTime.Now + ":\t Exiting Logout()";
            logger.Log(logMsg);
        }

        private void SendData(IAsyncResult ar)
        {
            string logMsg = "";
            try
            {
                logMsg = DateTime.Now + ":\t In SendData()";
                logger.Log(logMsg);
                client.socket.EndSendTo(ar);
                allDone.Set();
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
            logMsg = DateTime.Now + ":\t Exiting SendData()";
            logger.Log(logMsg);
        }

        private void ReceiveData(IAsyncResult ar)
        {
            string logMsg = "";
            try
            {
                logMsg = DateTime.Now + ":\t In ReceiveData()";
                logger.Log(logMsg);
                client.socket.EndReceive(ar);
                Packet receivedData = new Packet(client.DataStream);

                if (receivedData.ChatMessage == "ACK")
                {
                    latestSendPktACKED = receivedData.SequenceNumber;
                    Console.WriteLine("ACK Packet: " + receivedData.SequenceNumber + " " + receivedData.ChatMessage);
                    client.LastIncomingACKForSend = receivedData.SequenceNumber;
                    partialCleanerSendBuffer.Set();
                }
                else
                {
                    if (receivedData.SenderName != "LoadBalancer")
                    {
                        if (!client.ReceiveBufferHasKey(receivedData.SequenceNumber))
                        {
                            client.InsertInReceiveBuffer(receivedData.GetDataStream(), receivedData.SequenceNumber);
                            SendACKToServer();
                            processReceiveQueue.Set();
                        }

                        //if (currentReceiveWindowHasSpace() && liesInRangeForReceive(receivedData.SequenceNumber))
                        //{
                        //    if (!client.ReceiveBufferHasKey(receivedData.SequenceNumber))
                        //    {
                        //        receiveMessageBuffer[receivedData.SequenceNumber] = receivedData;
                        //        SendACKToServer();
                        //        processReceiveQueue.Set();
                        //    }
                        //}
                    }
                }

                // Reset data stream
                client.ResetDataStream();

                // Continue listening for more messages
                client.socket.BeginReceiveFrom(client.DataStream, 0, client.DataStream.Length, SocketFlags.None, ref epServer, new AsyncCallback(ReceiveData), null);
            }
            catch (ObjectDisposedException)
            { }

            catch (Exception e)
            {
                logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
            logMsg = DateTime.Now + ":\t Exiting ReceiveData()";
            logger.Log(logMsg);
        }

        private void SendACKToServer()
        {
            string logMsg = DateTime.Now + ":\t In SendACKToServer()";
            logger.Log(logMsg);
            Packet sendData = new Packet();

            SortedDictionary<int, byte[]> sortedDict = client.ReceiveBuffer;

            if (sortedDict.Count != 0)
            {
                int lastValidSeqNum = sortedDict.Keys.First();
                sendData.ChatMessage = "ACK";
                sendData.RecipientName = client.FriendName;
                sendData.SenderName = client.Name;
                sendData.ChatDataIdentifier = DataIdentifier.Message;

                for (int i = sortedDict.Keys.First(); i <= sortedDict.Keys.Last(); i++)
                {
                    if (sortedDict.ContainsKey(i) && i == lastValidSeqNum)
                    {
                        lastValidSeqNum++;
                    }
                    else break;
                }

                sendData.SequenceNumber = lastValidSeqNum;
                latestReceivePktACKED = lastValidSeqNum - 1;
                oldestReceivePacketSeqNum = sortedDict.Keys.First();
                byte[] data = sendData.GetDataStream();
                client.socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, epServer, new AsyncCallback(SendData), client);
            }

            logMsg = DateTime.Now + ":\t Exiting SendACKToServer()";
            logger.Log(logMsg);
        }

        private void CleanSendQueue()
        {
            while (true)
            {
                partialCleanerSendBuffer.WaitOne();
                client.CleanAwaitingACKsSendBuffer();
            }
        }

        private void CleanUpReceiveQueue(int receivedSeqNum)
        {
            while (true)
            {
                cleanerReceiveQueue.WaitOne();

                string logMsg = "";
                logMsg = DateTime.Now + ":\t In CleanUpQueue()";
                logger.Log(logMsg);

                if (getCurrentReceiveWindowSize() == windowSize)
                {
                    lock (syncReceiveBuffer)
                    {
                        receiveMessageBuffer.Clear();
                    }
                }

                logMsg = DateTime.Now + ":\t Exiting CleanUpQueue()";
                logger.Log(logMsg);
            }
        }

        private void ProcessReceiveQueue()
        {
            while (true)
            {
                processReceiveQueue.WaitOne();

                try
                {
                    string logMsg = "";
                    logMsg = DateTime.Now + ":\t In ProcessReceiveQueue()";
                    logger.Log(logMsg);
                    if (client.ReceiveBuffer.Count != 0)
                    {
                        for (int i = client.ReceiveBuffer.Keys.First(); client.ReceiveBuffer.Any() && i <= client.ReceiveBuffer.Keys.Last(); i++)
                        {
                            if (client.ReceiveBuffer.ContainsKey(i))
                            {
                                Packet pkt = new Packet(client.ReceiveBuffer[i]);
                                Console.WriteLine(pkt.ChatMessage);
                                client.ReceiveBuffer.Remove(i);
                                latestReceivePacketSeqNum = i;
                            }
                            else break;
                        }
                    }

                    logMsg = DateTime.Now + ":\t Exiting ProcessReceiveQueue()";
                    logger.Log(logMsg);
                }
                catch (Exception e)
                {
                    string logMsg = e.ToString();
                    logger.Log(logMsg);
                }
            }
        }

        //private static void CheckSendBuffer(object o, ElapsedEventArgs e)
        //{
        //    if (sendMessageBuffer.Count != 0)
        //    {
        //        processSendQueue.Set();
        //    }
        //}

        private void ResendMechanism()
        {
            //sendTimer = new System.Timers.Timer(1000);
            //sendTimer.Elapsed += CheckSendBuffer;
            //sendTimer.AutoReset = true;
            //sendTimer.Enabled = true;
        }

        private int getCurrentSendWindowSize()
        {
            return latestSendPacketSeqNum - oldestSendPacketSeqNum;
        }

        private int getCurrentReceiveWindowSize()
        {
            return latestReceivePacketSeqNum - oldestReceivePacketSeqNum;
        }

        private bool currentSendWindowHasSpace()
        {
            return (latestSendPacketSeqNum - oldestSendPacketSeqNum) < windowSize;
        }

        private bool liesInRangeForSend(int seqNum)
        {
            return (oldestSendPacketSeqNum < seqNum && (oldestSendPacketSeqNum + windowSize) >= seqNum);
        }

        private void incrementLatestSendPacket(int seqNum)
        {
            if (seqNum > latestSendPacketSeqNum)
            {
                latestSendPacketSeqNum = seqNum;
            }
        }
        
        private bool currentReceiveWindowHasSpace()
        {
            return (latestReceivePktACKED - oldestReceivePacketSeqNum) < windowSize;
        }

        private bool liesInRangeForReceive(int seqNum)
        {
            return (oldestReceivePacketSeqNum < seqNum && (oldestReceivePacketSeqNum + windowSize) >= seqNum);
        }

        private static void OnTimedEvent(object course, ElapsedEventArgs e)
        {
            int pktsProduced = (numOfPktsProduced - prevNumOfPktsProduced);
            Console.WriteLine("Packets processed: {0}", pktsProduced);
            prevNumOfPktsProduced = numOfPktsProduced;
        }

        private void MessageProductionRate()
        {
            aTimer = new System.Timers.Timer(1000);
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }

        static void Main(string[] args)
        {
            try
            {
                port = int.Parse(args[0]);
                fileName += args[1].ToString() + ".txt";
                LBIPAddress = args[2].ToString();
                serverIPAddress = args[3].ToString();
                clientIPAddress = args[4].ToString();
                ChatClient client = new ChatClient();
                client.ConnectToLoadBalancer();
                client.ConnectToServer();
                Thread t1 = new Thread(() => logger.WriteToFile(fileName));
                t1.Start();
                Thread t2 = new Thread(client.ProcessSendQueue);
                t2.Start();
                Thread t3 = new Thread(client.ProcessReceiveQueue);
                t3.Start();
                Thread t4 = new Thread(client.ResendMechanism);
                t4.Start();
                Thread t5 = new Thread(client.CleanSendQueue);
                t5.Start();
                Thread t6 = new Thread(client.MessageProductionRate);
                //t6.Start();
                client.SendMessage();
                t1.Join();
                t2.Join();
                t3.Join();
                t4.Join();
                t5.Join();
                t6.Join();
            }

            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}