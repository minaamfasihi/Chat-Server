using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Concurrent;
using System.Timers;
using System.IO;
using LogWriterAPI;

namespace TestUdpServer
{

    class Server
    {
        #region Private Members
        private struct Client
        {
            public EndPoint endPoint;
            public string name;

            public Client(EndPoint ep, string name)
            {
                this.endPoint = ep;
                this.name = name;
            }
        }

        private static EndPoint epSender;
        private static Socket LBConnectorSocket;
        public static Hashtable clientsList = new Hashtable();
        private static ArrayList serversList = new ArrayList();
        public static Hashtable recipientClients = new Hashtable();
        private Socket serverSocket;
        private byte[] dataStream = new byte[1024];
        private static AutoResetEvent allDone = new AutoResetEvent(false);
        private static int numOfPktsReceived = 0;
        private static int numOfPktsSent = 0;
        private static int prevNumOfPktsSent = 0;
        private static Queue<Packet> buffer = new Queue<Packet>();
        private static Hashtable clientBuffers = new Hashtable();
        private static Hashtable clientBuffersForBroadcast = new Hashtable();
        private static object clientBufferLock = new object();
        private static object clientBufferBroadcastLock = new object();
        private static int windowSize = 4;
        private static System.Timers.Timer aTimer;
        private const int LBport = 9000;
        private static AutoResetEvent processSendEvent = new AutoResetEvent(false);
        private static AutoResetEvent processBroadcastEvent = new AutoResetEvent(false);
        private static AutoResetEvent msgsEvent = new AutoResetEvent(false);
        private static LogWriter logger = Logger.Instance;
        private static int serverPort;
        private static string fileName = @"C:\Users\minaam.fasihi\Documents\Projects\Server-logs-";
        private static string serverIPAddress;
        private static string LBIPAddress;
        private static int rawNumOfPktsReceived = 0;
        private static int prevRawNumOfPktsReceived = 0;
        #endregion

        #region Constructor
        public Server(int port)
        {
            try
            {
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint server = new IPEndPoint(IPAddress.Parse(serverIPAddress), port);
                serverSocket.Bind(server);
                IPEndPoint clients = new IPEndPoint(IPAddress.Any, 0);
                epSender = clients;
                string logMsg = DateTime.Now + ":\t Server has started. Waiting for clients to connect.";

                Console.WriteLine(logMsg);
                logger.Log(logMsg);
                ConnectToLoadBalancer();
            }
            catch (Exception e)
            {
                string logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
        }
        #endregion

        public void ConnectToLoadBalancer()
        {
            try
            {
                string logMsg = DateTime.Now + ":\t In ConnectToLoadBalancer()";
                logger.Log(logMsg);
                IPAddress ipAddr = IPAddress.Parse(LBIPAddress);
                IPEndPoint remoteEndPoint = new IPEndPoint(ipAddr, LBport);

                LBConnectorSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                LBConnectorSocket.BeginConnect(remoteEndPoint, new AsyncCallback(LBConnectCallback), LBConnectorSocket);
                allDone.WaitOne();
                logMsg = DateTime.Now + ":\t Exiting ConnectToLoadBalancer()";
                logger.Log(logMsg);
            }
            catch (Exception e)
            {
                string logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
        }

        private void LBConnectCallback(IAsyncResult ar)
        {
            try
            {
                string logMsg = DateTime.Now + ":\t In LBConnectCallback()";
                logger.Log(logMsg);
                Socket handler = (Socket)ar.AsyncState;
                handler.EndConnect(ar);
                logMsg = DateTime.Now + ":\tConnected with Load Balancer successfully";
                Console.WriteLine(logMsg);
                logger.Log(logMsg);
                LBInform();
                logMsg = DateTime.Now + ":\t Exiting LBConnectCallback()";
                logger.Log(logMsg);
                allDone.Set();
            }
            catch (Exception e)
            {
                string logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
        }

        private void LBInform()
        {
            string logMsg = DateTime.Now + ":\t In LBInform()";
            logger.Log(logMsg);
            Packet sendData = new Packet();
            sendData.SenderName = serverSocket.LocalEndPoint.ToString();
            byte[] byteData = sendData.GetDataStream();

            LBConnectorSocket.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(LBInformCallback), LBConnectorSocket);
            logMsg = DateTime.Now + ":\t Exiting LBInform()";
            logger.Log(logMsg);
        }

        private void LBInformCallback(IAsyncResult ar)
        {
            string logMsg = DateTime.Now + ":\t In LBInformCallback()";
            logger.Log(logMsg);
            Socket connector = (Socket)ar.AsyncState;
            connector.EndSend(ar);
            connector.BeginReceive(this.dataStream, 0, this.dataStream.Length, 0, new AsyncCallback(updateNewServer), connector);
            connector.BeginReceive(this.dataStream, 0, this.dataStream.Length, 0, new AsyncCallback(updateExistingServers), connector);
            logMsg = DateTime.Now + ":\t Exiting LBInformCallback()";
            logger.Log(logMsg);
        }

        private void updateNewServer(IAsyncResult ar)
        {
            string logMsg = DateTime.Now + ":\t In updateNewServer()";
            logger.Log(logMsg);
            Socket serverSocketForLB = (Socket)ar.AsyncState;
            Packet receivedData = new Packet(this.dataStream);
            serverSocketForLB.EndReceive(ar);

            string msg = receivedData.ChatMessage;
            if (!String.IsNullOrEmpty(msg))
            {
                char[] delimiters = { '&' };
                string[] message = receivedData.ChatMessage.Split(delimiters);

                foreach (string s in message)
                {
                    if (!String.IsNullOrEmpty(s))
                    {
                        serversList.Add(s);
                    }
                }
            }
            logMsg = DateTime.Now + "\t---Updating new server with existing servers information---";
            logger.Log(logMsg);
            allDone.Set();
            logMsg = DateTime.Now + ":\t Exiting updateNewServer()";
            logger.Log(logMsg);
        }

        private void updateExistingServers(IAsyncResult ar)
        {
            string logMsg = DateTime.Now + ":\t In updateExistingServers()";
            logger.Log(logMsg);
            Socket s = (Socket)ar.AsyncState;
            s.EndReceive(ar);
            Packet receivedData = new Packet(this.dataStream);
            if (receivedData.ChatMessage != serverSocket.LocalEndPoint.ToString())
            {
                serversList.Add(receivedData.ChatMessage);
            }
            s.BeginReceive(this.dataStream, 0, this.dataStream.Length, 0, new AsyncCallback(updateExistingServers), s);

            logMsg = DateTime.Now + "\t---Updating servers with the new server information---";
            logger.Log(logMsg);
            allDone.Set();
            logMsg = DateTime.Now + ":\t In updateExistingServers()";
            logger.Log(logMsg);
        }

        public void StartListening()
        {
            string logMsg = DateTime.Now + ":\t In StartListening()";
            logger.Log(logMsg);
            try
            {
                allDone.Set();
                while (true)
                {
                    allDone.WaitOne();
                    serverSocket.BeginReceiveFrom(this.dataStream, 0, this.dataStream.Length, SocketFlags.None, ref epSender, new AsyncCallback(ReceiveData), epSender);
                }
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
            logMsg = DateTime.Now + ":\t Exiting StartListening()";
            logger.Log(logMsg);
        }

        public void SendData(IAsyncResult asyncResult)
        {
            try
            {
                string logMsg = DateTime.Now + ":\t In SendData()";
                logger.Log(logMsg);
                serverSocket.EndSendTo(asyncResult);
                logMsg = DateTime.Now + "\tSending data to client";
                logger.Log(logMsg);
                logMsg = DateTime.Now + ":\t Exiting SendData()";
                logger.Log(logMsg);
            }
            catch (Exception e)
            {
                string logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
        }

        private static void OnTimedEvent(object course, ElapsedEventArgs e)
        {
            int pktsProcessed = (numOfPktsSent - prevNumOfPktsSent);
            Console.WriteLine("Packets processed: {0}", pktsProcessed);
            Console.WriteLine("Raw number of packets received: {0}", rawNumOfPktsReceived - prevRawNumOfPktsReceived);
            prevRawNumOfPktsReceived = rawNumOfPktsReceived;
            prevNumOfPktsSent = numOfPktsSent;
        }

        private void messagesRate()
        {
            aTimer = new System.Timers.Timer(1000);
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }

        private void ReceiveData(IAsyncResult asyncResult)
        {
            try
            {
                string logMsg = DateTime.Now + ":\t In ReceiveData()";
                logger.Log(logMsg);

                numOfPktsReceived++;
                // Initialise a packet object to store the received data
                Packet receivedData = new Packet(this.dataStream);

                // Initialise a packet object to store the data to be sent
                Packet sendData = new Packet();

                // Initialise the IPEndPoint for the clients
                IPEndPoint clients = new IPEndPoint(IPAddress.Any, 0);

                // Initialise the EndPoint for the clients
                EndPoint epSender = (EndPoint)clients;

                // Receive all data
                serverSocket.EndReceiveFrom(asyncResult, ref epSender);

                // Start populating the packet to be sent
                sendData.ChatDataIdentifier = receivedData.ChatDataIdentifier;
                sendData.SenderName = receivedData.SenderName;
                sendData.RecipientName = receivedData.RecipientName;
                rawNumOfPktsReceived++;

                switch (receivedData.ChatDataIdentifier)
                {
                    case DataIdentifier.Message:
                    case DataIdentifier.Broadcast:
                        if (receivedData.ChatMessage == "ACK")
                        {
                            PartialCleanUpSendBuffer(receivedData);
                        }
                        else
                        {
                            string senderName = receivedData.SenderName.ToLower();
                            sendData.ChatMessage = receivedData.ChatMessage;

                            if (clientBuffers.ContainsKey(receivedData.SenderName))
                            {
                                SortedDictionary<int, Packet> sortedDict;
                                lock (clientBufferLock)
                                {
                                    sortedDict = new SortedDictionary<int, Packet>((SortedDictionary<int, Packet>)clientBuffers[receivedData.SenderName]);
                                }
                                if (sortedDict.Count != 0)
                                {
                                    int startOffset = sortedDict.Keys.First();
                                    // Check if packet belongs in current window

                                    if (receivedData.SequenceNumber >= startOffset && receivedData.SequenceNumber <= startOffset + windowSize)
                                    {
                                        if (!sortedDict.ContainsKey(receivedData.SequenceNumber))
                                        {
                                            lock (clientBufferLock)
                                            {
                                                ((SortedDictionary<int, Packet>)clientBuffers[receivedData.SenderName]).Add(receivedData.SequenceNumber, receivedData);
                                                SendACKToClient(receivedData.SenderName);
                                                processSendEvent.Set();
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    lock (clientBufferLock)
                                    {
                                        ((SortedDictionary<int, Packet>)clientBuffers[receivedData.SenderName]).Add(receivedData.SequenceNumber, receivedData);
                                    }
                                    SendACKToClient(receivedData.SenderName);
                                    processSendEvent.Set();
                                }
                            }
                            else if (clientBuffers.ContainsKey(receivedData.RecipientName) && receivedData.ChatDataIdentifier == DataIdentifier.Broadcast)
                            {
                                if (clientBuffersForBroadcast.ContainsKey(receivedData.RecipientName))
                                {
                                    lock (clientBufferBroadcastLock)
                                    {
                                        SortedDictionary<int, Packet> recipientDict = (SortedDictionary<int, Packet>)clientBuffersForBroadcast[receivedData.RecipientName];
                                        if (!recipientDict.ContainsKey(receivedData.SequenceNumber))
                                        {
                                            ((SortedDictionary<int, Packet>)clientBuffersForBroadcast[receivedData.RecipientName]).Add(receivedData.SequenceNumber, receivedData);
                                        }
                                    }
                                }
                                else
                                {
                                    SortedDictionary<int, Packet> broadcastMsgs = new SortedDictionary<int, Packet>();
                                    broadcastMsgs.Add(receivedData.SequenceNumber, receivedData);
                                    lock (clientBufferBroadcastLock)
                                    {
                                        clientBuffersForBroadcast.Add(receivedData.RecipientName, broadcastMsgs);
                                    }
                                }
                                processBroadcastEvent.Set();
                                SendACKToServerForBroadcast(receivedData);
                            }
                        }
                        break;

                    case DataIdentifier.LogIn:
                        // Populate client object
                        Client client = new Client();
                        client.endPoint = epSender;
                        client.name = receivedData.SenderName;

                        if (!clientBuffers.ContainsKey(client.name))
                        {
                            SortedDictionary<int, Packet> bufferMessages = new SortedDictionary<int, Packet>();
                            lock (clientBufferLock)
                            {
                                clientBuffers.Add(client.name, bufferMessages);
                            }
                        }

                        // Add client to list if not present already
                        if (!clientsList.ContainsKey(client.name.ToLower()))
                        {
                            clientsList.Add(client.name.ToLower(), client);
                        }

                        SendACKToClient(receivedData.SenderName);
                        sendData.ChatMessage = string.Format("-- {0} is online --", receivedData.SenderName);
                        break;

                    case DataIdentifier.LogOut:
                        foreach (DictionaryEntry dict in clientsList)
                        {
                            if (clientsList.ContainsKey(receivedData.SenderName))
                            {
                                clientsList.Remove(receivedData.SenderName);
                                break;
                            }
                        }

                        if (clientsList.ContainsKey(receivedData.SenderName))
                        {
                            Console.WriteLine(receivedData.SenderName + " has not been removed");
                        }
                        else
                        {
                            Console.WriteLine(receivedData.SenderName + " has been removed");
                        }
                        sendData.ChatMessage = string.Format("-- {0} has gone offline --", receivedData.SenderName);
                        break;
                }
                allDone.Set();
                logMsg = DateTime.Now + ":\t Exiting ReceiveData()";
                logger.Log(logMsg);
            }
            catch (Exception e)
            {
                string logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
        }

        private void PartialCleanUpSendBuffer(Packet pkt)
        {
            string recipientName = pkt.RecipientName;
            string senderName = pkt.SenderName;
            int seqNumACKed = pkt.SequenceNumber;
            SortedDictionary<int, Packet> sortedDict = null;

            if (clientBuffers.Contains(recipientName))
            {
                lock (clientBufferLock)
                {
                    sortedDict = (SortedDictionary<int, Packet>)clientBuffers[recipientName];
                    if (sortedDict != null && sortedDict.Count != 0)
                    {
                        for (int i = sortedDict.Keys.First(); sortedDict.Any() && i <= sortedDict.Keys.Last(); i++)
                        {
                            if (sortedDict.ContainsKey(i) && i < seqNumACKed)
                            {
                                sortedDict.Remove(i);
                            }
                        }
                    }
                }
            }

            else if (clientBuffersForBroadcast.ContainsKey(senderName))
            {
                lock (clientBufferBroadcastLock)
                {
                    sortedDict = (SortedDictionary<int, Packet>)clientBuffersForBroadcast[senderName];
                    if (sortedDict != null && sortedDict.Count != 0)
                    {
                        for (int i = sortedDict.Keys.First(); sortedDict.Any() && i <= sortedDict.Keys.Last(); i++)
                        {
                            if (sortedDict.ContainsKey(i) && i < seqNumACKed)
                            {
                                sortedDict.Remove(i);
                            }
                        }
                    }
                }
            }
        }

        private void ProcessSendBuffer()
        {
            string logMsg = "";
            while (true)
            {
                processSendEvent.WaitOne();
                logMsg = "In ProcessSendBuffer()";
                logger.Log(logMsg);

                Hashtable tempBuffer;
                try
                {
                    lock (clientBufferLock)
                    {
                        tempBuffer = new Hashtable(clientBuffers);
                    }

                    foreach (DictionaryEntry dict in tempBuffer)
                    {
                        SortedDictionary<int, Packet> tempDict;
                        lock (clientBufferLock)
                        {
                            tempDict = new SortedDictionary<int, Packet>((SortedDictionary<int, Packet>)dict.Value);
                        }

                        foreach (KeyValuePair<int, Packet> entry in tempDict)
                        {
                            Packet pkt = new Packet(entry.Value);
                            //Console.WriteLine("In Process Send Buffer");
                            //Console.WriteLine("Sender Name: {0}", pkt.SenderName);
                            //Console.WriteLine("Recipient Name: {0}", pkt.RecipientName);
                            //Console.WriteLine("Message: {0}", pkt.ChatMessage);
                            RelayMessage(pkt);
                        }
                    }
                    tempBuffer = null;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    logMsg = e.ToString();
                    logger.Log(logMsg);
                }
            }
        }

        private void ProcessBroadcastBuffer()
        {
            string logMsg = "";
            while (true)
            {
                processBroadcastEvent.WaitOne();

                Hashtable tempBrodcastBuffer;
                try
                {
                    lock (clientBufferBroadcastLock)
                    {
                        tempBrodcastBuffer = new Hashtable(clientBuffersForBroadcast);
                    }

                    foreach (DictionaryEntry dict in tempBrodcastBuffer)
                    {
                        SortedDictionary<int, Packet> tempDict;
                        lock (clientBufferBroadcastLock)
                        {
                            tempDict = new SortedDictionary<int, Packet>((SortedDictionary<int, Packet>)dict.Value);
                        }

                        foreach (KeyValuePair<int, Packet> entry in tempDict)
                        {
                            Packet pkt = new Packet(entry.Value);
                            //Console.WriteLine("In Process Broadcast Buffer");
                            //Console.WriteLine("Sender Name: {0}", pkt.SenderName);
                            //Console.WriteLine("Recipient Name: {0}", pkt.RecipientName);
                            //Console.WriteLine("Message: {0}", pkt.ChatMessage);
                            RelayMessage(pkt);
                        }
                    }
                }
                catch (Exception e)
                {
                    logMsg = e.ToString();
                    logger.Log(logMsg);
                }
            }
        }

        private void ProcessQueue()
        {
            while (buffer.Any())
            {
                Console.WriteLine(buffer.Dequeue().ChatMessage);
            }
        }

        private void BroadcastToAllServers(Packet sendData)
        {
            string logMsg = "";
            try
            {
                logMsg = DateTime.Now + ":\t In BroadcastToAllServers()";
                logger.Log(logMsg);
                byte[] data = sendData.GetDataStream();
                foreach (var server in serversList)
                {
                    char[] delimiters = { ':' };
                    string[] ipAddrArray = server.ToString().Split(delimiters);
                    EndPoint recipient = (EndPoint)new IPEndPoint(IPAddress.Parse(ipAddrArray[0]), int.Parse(ipAddrArray[1]));

                    if (recipient.ToString() != serverSocket.LocalEndPoint.ToString())
                    {
                        serverSocket.BeginSendTo(data, 0, data.Length, SocketFlags.None, recipient, new AsyncCallback(BroadcastToServersCallback), serverSocket);
                    }
                }
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
            logMsg = DateTime.Now + ":\t Exiting BroadcastToAllServers()";
            logger.Log(logMsg);
        }

        private void BroadcastToServersCallback(IAsyncResult ar)
        {
            string logMsg = "";
            try
            {
                logMsg = DateTime.Now + ":\t In BroadcastToServersCallback()";
                logger.Log(logMsg);
                Socket s = (Socket)ar.AsyncState;
                s.EndSend(ar);
                Packet receivedData = new Packet(this.dataStream);
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
            logMsg = DateTime.Now + ":\t Exiting BroadcastToServersCallback()";
            logger.Log(logMsg);
        }

        private void SendMessageToClient(Packet sendData)
        {
            string logMsg = DateTime.Now + ":\t In SendMessageToClient()";
            logger.Log(logMsg);
            byte[] data = sendData.GetDataStream();
            Client recipient = (Client)clientsList[sendData.RecipientName];
            serverSocket.BeginSendTo(data, 0, data.Length, SocketFlags.None, recipient.endPoint, new AsyncCallback(SendData), recipient.endPoint);
            logMsg = DateTime.Now + ":\t Exiting SendMessageToClient()";
            logger.Log(logMsg);
        }

        private void RelayMessage(Packet sendData)
        {
            string logMsg = DateTime.Now + ":\t In RelayMessage()";
            logger.Log(logMsg);
            if (sendData.RecipientName != null)
            {
                if (clientsList.ContainsKey(sendData.RecipientName))
                {
                    SendMessageToClient(sendData);
                    numOfPktsSent++;
                }
                else if (sendData.ChatDataIdentifier != DataIdentifier.Broadcast)
                {
                    sendData.ChatDataIdentifier = DataIdentifier.Broadcast;
                    BroadcastToAllServers(sendData);
                    numOfPktsSent++;
                }
            }
            
            logMsg = DateTime.Now + ":\t Exiting RelayMessage()";
            logger.Log(logMsg);
        }

        private void SendACKToClient(string clientName)
        {
            string logMsg = DateTime.Now + ":\t In SendACKToClient()";
            logger.Log(logMsg);
            Packet sendData = new Packet();
            SortedDictionary<int, Packet> sortedDict;
            lock (clientBufferLock)
            {
                sortedDict = new SortedDictionary<int, Packet>((SortedDictionary<int, Packet>)clientBuffers[clientName]);
            }

            if (sortedDict.Count != 0)
            {
                int lastValidSeqNum = sortedDict.Keys.First();
                sendData.ChatMessage = "ACK";
                sendData.RecipientName = clientName;
                sendData.SenderName = "Server";

                for (int i = sortedDict.Keys.First(); i <= sortedDict.Keys.Last(); i++)
                {
                    if (sortedDict.ContainsKey(i) && i == lastValidSeqNum)
                    {
                        lastValidSeqNum++;
                    }
                    else break;
                }

                sendData.SequenceNumber = lastValidSeqNum;
                byte[] data = sendData.GetDataStream();
                Client recipient = (Client)clientsList[sendData.RecipientName];
                serverSocket.BeginSendTo(data, 0, data.Length, SocketFlags.None, recipient.endPoint, new AsyncCallback(SendData), recipient.endPoint);
            }

            logMsg = DateTime.Now + ":\t Exiting SendACKToClient()";
            logger.Log(logMsg);
        }

        private void SendACKToServerForBroadcast(Packet pkt)
        {
            string logMsg = DateTime.Now + ":\t In SendACKToServerForBroadcast()";
            logger.Log(logMsg);
            Packet sendData = new Packet();
            SortedDictionary<int, Packet> sortedDict;
            lock (clientBufferLock)
            {
                sortedDict = new SortedDictionary<int, Packet>((SortedDictionary<int, Packet>)clientBuffersForBroadcast[pkt.RecipientName]);
            }

            if (sortedDict.Count != 0)
            {
                int lastValidSeqNum = sortedDict.Keys.First();
                sendData.ChatMessage = "ACK";
                sendData.RecipientName = pkt.SenderName;
                sendData.SenderName = pkt.RecipientName;

                for (int i = sortedDict.Keys.First(); i <= sortedDict.Keys.Last(); i++)
                {
                    if (sortedDict.ContainsKey(i) && i == lastValidSeqNum)
                    {
                        lastValidSeqNum++;
                    }
                    else break;
                }

                sendData.SequenceNumber = lastValidSeqNum;
                sendData.ChatDataIdentifier = DataIdentifier.Broadcast;
                BroadcastToAllServers(sendData);
            }

            logMsg = DateTime.Now + ":\t Exiting SendACKToClient()";
            logger.Log(logMsg);
        }

        static void Main(string[] args)
        {
            allDone.Reset();
            serverPort = int.Parse(args[0]);
            fileName += args[1].ToString() + ".txt";
            LBIPAddress = args[2].ToString();
            serverIPAddress = args[3].ToString();
            Thread t1 = new Thread(() => logger.WriteToFile(fileName));
            t1.Start();
            Server server = new Server(serverPort);
            Thread t2 = new Thread(() => server.ProcessSendBuffer());
            t2.Start();
            Thread t3 = new Thread(server.messagesRate);
            t3.Start();
            Thread t4 = new Thread(server.ProcessBroadcastBuffer);
            t4.Start();
            server.StartListening();
            t1.Join();
            t2.Join();
            t3.Join();
            t4.Join();
        }
    }
}
