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
using PacketAPI;
using ClientAPI;

namespace TestUdpServer
{
    class ChatServer
    {
        #region Private Members
        private static Socket LBConnectorSocket;
        private static ArrayList serversList = new ArrayList();
        public static Hashtable recipientClients = new Hashtable();
        private Socket serverSocket;
        private byte[] dataStream = new byte[1024];
        private static AutoResetEvent allDone = new AutoResetEvent(false);
        private static int numOfPktsReceived = 0;
        private static int numOfPktsSent = 0;
        private static int prevNumOfPktsSent = 0;
        private static Queue<byte[]> tempReceiveBuffer = new Queue<byte[]>();

        private static ConcurrentDictionary<string, Client> 
            clientBuffers = new ConcurrentDictionary<string, Client>();

        private static ConcurrentDictionary<string, Client> 
            clientBuffersForBroadcast = new ConcurrentDictionary<string, Client>();

        private static ConcurrentDictionary<string, SortedDictionary<int, byte[]>> 
            senderWaitingForACKs = new ConcurrentDictionary<string, SortedDictionary<int, byte[]>>();

        private static object tempReceiveBufferLock = new object();
        private static object clientBufferBroadcastLock = new object();
        private static int windowSize = 4;
        private static System.Timers.Timer aTimer;
        private const int LBport = 9000;
        private static EndPoint epSender;
        private static AutoResetEvent processSendEvent = new AutoResetEvent(false);
        private static AutoResetEvent processBroadcastEvent = new AutoResetEvent(false);
        private static AutoResetEvent processSendACKBufferEvent = new AutoResetEvent(false);
        private static AutoResetEvent receiveDataEvent = new AutoResetEvent(false);
        private static LogWriter logger = Logger.Instance;
        private object clientBufferLock = new object();
        private static int serverPort;
        private static string fileName = @"C:\Users\minaam.fasihi\Documents\Projects\Server-logs-";
        private static string serverIPAddress;
        private static string LBIPAddress;
        private static int rawNumOfPktsReceived = 0;
        private static int prevRawNumOfPktsReceived = 0;
        private static ClientsList senderClientsObject = new ClientsList();
        #endregion

        #region Constructor
        public ChatServer(int port)
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
            //allDone.Set();
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
            //Console.WriteLine("Raw number of packets received: {0}", rawNumOfPktsReceived - prevRawNumOfPktsReceived);
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
                tempReceiveBuffer.Enqueue(this.dataStream);
                IPEndPoint clients = new IPEndPoint(IPAddress.Any, 0);
                epSender = (EndPoint)clients;
                serverSocket.EndReceiveFrom(asyncResult, ref epSender);
                receiveDataEvent.Set();
            }
            catch (Exception e)
            {
                string logMsg = DateTime.Now + ":\t" + e.ToString();
                logger.Log(logMsg);
            }
        }

        private void ProcessReceiveData()
        {
            SortedDictionary<int, byte[]> sortedDict = new SortedDictionary<int, byte[]>();
            Client currentClient;

            while (true)
            {
                receiveDataEvent.WaitOne();

                string logMsg = "";
                try
                {
                    if (tempReceiveBuffer.Count != 0)
                    {
                        Packet receivedData = null;
                        Packet sendData = new Packet();
                        lock (tempReceiveBufferLock)
                        {
                            receivedData = new Packet(tempReceiveBuffer.Dequeue());
                        }
                        rawNumOfPktsReceived++;

                        switch (receivedData.ChatDataIdentifier)
                        {
                            case DataIdentifier.Message:
                            case DataIdentifier.Broadcast:
                                if (receivedData.ChatMessage == "ACK")
                                {
                                    Console.WriteLine("ACK: {0}", receivedData.ChatMessage);
                                    Console.WriteLine("Sender: {0}", receivedData.SenderName);
                                    Console.WriteLine("Recipient: {0}", receivedData.RecipientName);
                                    PartialCleanUpSendBuffer(receivedData);
                                }
                                else
                                {
                                    string senderName = receivedData.SenderName.ToLower();
                                    sendData.ChatMessage = receivedData.ChatMessage;
                                    sendData.ChatDataIdentifier = receivedData.ChatDataIdentifier;
                                    sendData.SenderName = receivedData.SenderName;
                                    sendData.RecipientName = receivedData.RecipientName;

                                    Console.WriteLine("Message: {0}", sendData.ChatMessage);
                                    Console.WriteLine("Recipient: {0}", sendData.RecipientName);
                                    Console.WriteLine("Sender: {0}", sendData.SenderName);
                                    if (clientBuffers.ContainsKey(receivedData.SenderName))
                                    {
                                        currentClient = clientBuffers[receivedData.SenderName];

                                        if (!currentClient.ProducerSendBuffer.ContainsKey(receivedData.SequenceNumber) ||
                                            !currentClient.ConsumerSendBuffer.ContainsKey(receivedData.SequenceNumber) ||
                                            !currentClient.AwaitingSendACKsBuffer.ContainsKey(receivedData.SequenceNumber)
                                           )
                                        {
                                            currentClient.InsertInSendBuffer(receivedData.SequenceNumber, receivedData.GetDataStream());
                                            senderClientsObject.InsertInSenderClientsProducerList(receivedData.SenderName);
                                            //processSendEvent.Set();
                                        }
                                    }
                                    else if (clientBuffers.ContainsKey(receivedData.RecipientName) && receivedData.ChatDataIdentifier == DataIdentifier.Broadcast)
                                    {
                                        currentClient = clientBuffers[receivedData.RecipientName];
                                        if (!currentClient.ProducerBroadcastBuffer.ContainsKey(receivedData.SequenceNumber) ||
                                            !currentClient.ConsumerBroadcastBuffer.ContainsKey(receivedData.SequenceNumber) ||
                                            !currentClient.AwaitingBroadcastACKsBuffer.ContainsKey(receivedData.SequenceNumber))
                                        {
                                            currentClient.InsertInBroadcastBuffer(receivedData.SequenceNumber, receivedData.GetDataStream());
                                            senderClientsObject.InsertInSenderClientsProducerList(receivedData.RecipientName);
                                            //processSendEvent.Set();
                                        }
                                    }
                                }
                                processSendEvent.Set();
                                break;

                            case DataIdentifier.LogIn:
                                Client client = new Client(receivedData.SenderName, receivedData.RecipientName, epSender);
                                client.InsertInSendBuffer(receivedData.SequenceNumber, receivedData.GetDataStream());

                                if (!clientBuffers.ContainsKey(client.Name))
                                {
                                    clientBuffers.TryAdd(client.Name, client);
                                }

                                client.EpSender = epSender;
                                sendData.ChatMessage = string.Format("-- {0} is online --", receivedData.SenderName);
                                break;

                            case DataIdentifier.LogOut:
                                sendData.ChatMessage = string.Format("-- {0} has gone offline --", receivedData.SenderName);
                                break;
                        }
                    }
                    allDone.Set();
                    logMsg = DateTime.Now + ":\t Exiting ReceiveData()";
                    logger.Log(logMsg);
                }
                catch (Exception e)
                {

                }
            }
        }

        private void PartialCleanUpSendBuffer(Packet pkt)
        {
            string recipientName = pkt.RecipientName;
            string senderName = pkt.SenderName;
            int seqNumACKed = pkt.SequenceNumber;
            SortedDictionary<int, byte[]> sortedDict = null;

            if (clientBuffers.ContainsKey(recipientName))
            {
                lock (clientBufferLock)
                {
                    sortedDict = clientBuffers[recipientName].ConsumerSendBuffer;
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
            Client clientObj;
            while (true)
            {
                processSendEvent.WaitOne();
                logMsg = "In ProcessSendBuffer()";
                logger.Log(logMsg);

                senderClientsObject.SwapProducerConsumerList();
                try
                {
                    foreach (string clientName in senderClientsObject.SenderConsumerList)
                    {
                        if (clientBuffers.ContainsKey(clientName))
                        {
                            clientObj = clientBuffers[clientName];
                            clientObj.SwapSendBuffers();
                            SendACKToClient(clientName);

                            if (clientObj.ConsumerSendBuffer.Any())
                            {
                                int startInd = clientObj.ConsumerSendBuffer.Keys.First();
                                int lastInd = clientObj.ConsumerSendBuffer.Keys.Last();

                                for (int i = startInd; clientObj.ConsumerSendBuffer.Any() && i <= lastInd; i++)
                                {
                                    if (clientObj.ConsumerSendBuffer.ContainsKey(i))
                                    {
                                        Packet pkt = new Packet(clientObj.ConsumerSendBuffer[i]);
                                        RelayMessage(pkt);
                                        clientObj.MoveFromConsumerSendToACKBuffer(pkt.SequenceNumber, pkt.GetDataStream());
                                    }
                                }
                            }
                            senderClientsObject.InsertInSenderClientsAwaitingACKsProducerList(clientName);
                            //if (clientObj.CheckIfProducerConsumerSendEmpty())
                            //{
                            //    senderClientsObject.RemoveFromProducerConsumerSendList(clientName);
                            //}
                            clientObj.SwapBroadcastBuffers();
                            if (clientObj.ConsumerBroadcastBuffer.Count != 0)
                            {
                                Packet p = new Packet();
                                p.SenderName = clientName;
                                p.RecipientName = "Server";
                                p.SequenceNumber = clientObj.GetLastConsecutiveSequenceNumber(clientObj.ConsumerBroadcastBuffer, true);
                                p.ChatDataIdentifier = DataIdentifier.Broadcast;
                                BroadcastToAllServers(p);

                                if (clientObj.ConsumerBroadcastBuffer.Count != 0)
                                {
                                    int startInd = clientObj.ConsumerBroadcastBuffer.Keys.First();
                                    int lastInd = clientObj.ConsumerBroadcastBuffer.Keys.Last();

                                    for (int i = startInd; clientObj.ConsumerBroadcastBuffer.Count != 0 && i <= lastInd; i++)
                                    {
                                        if (clientObj.ConsumerBroadcastBuffer.ContainsKey(i))
                                        {
                                            Packet pkt = new Packet(clientObj.ConsumerBroadcastBuffer[i]);
                                            RelayMessage(pkt);
                                            clientObj.MoveFromConsumerBroadcastToACKBuffer(pkt.SequenceNumber, pkt.GetDataStream());
                                        }
                                    }
                                }
                            }
                            processSendACKBufferEvent.Set();
                        }
                    }
                    senderClientsObject.SwapProducerConsumerList();
                }
                catch (Exception e)
                {
                    logMsg = e.ToString();
                    logger.Log(logMsg);
                }
            }
        }

        private void ProcessSendACKsBuffer()
        {
            SortedDictionary<int, byte[]> sd;
            Client clientObj2;

            while (true)
            {
                Thread.Sleep(1000);

                if (senderClientsObject.SenderAwaitingACKsConsumerList.Count == 0)
                {
                    processSendACKBufferEvent.WaitOne();
                }

                if (senderClientsObject.SenderAwaitingACKsConsumerList.Count != 0)
                {
                    foreach (string c in senderClientsObject.SenderAwaitingACKsConsumerList)
                    {
                        if (clientBuffers.ContainsKey(c))
                        {
                            clientObj2 = clientBuffers[c];
                            sd = clientObj2.ReturnCopyOfSendACKBuffer();
                            
                            if (sd.Count != 0)
                            {
                                foreach (KeyValuePair<int, byte[]> kvp in sd)
                                {
                                    serverSocket.BeginSendTo(kvp.Value, 0, kvp.Value.Length, SocketFlags.None, clientObj2.EpSender, new AsyncCallback(SendData), clientObj2.socket);
                                }
                            }
                        }
                    }
                }
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
                        serverSocket.BeginSendTo(data, 0, data.Length, SocketFlags.None, recipient, new AsyncCallback(BroadcastToServersCallback), null);
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
            Client recipient = clientBuffers[sendData.RecipientName];
            serverSocket.BeginSendTo(data, 0, data.Length, SocketFlags.None, recipient.EpSender, new AsyncCallback(SendData), recipient.socket);
            logMsg = DateTime.Now + ":\t Exiting SendMessageToClient()";
            logger.Log(logMsg);
        }

        private void RelayMessage(Packet sendData)
        {
            string logMsg = DateTime.Now + ":\t In RelayMessage()";
            logger.Log(logMsg);
            if (sendData.RecipientName != null)
            {
                if (clientBuffers.ContainsKey(sendData.RecipientName))
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
            string logMsg;

            logMsg = DateTime.Now + ":\t In SendACKToClient()";
            logger.Log(logMsg);

            SortedDictionary<int, byte[]> tempSD = new SortedDictionary<int, byte[]>();

            tempSD = clientBuffers[clientName].ConsumerSendBuffer;
            SendACKToClient(clientName, tempSD);

            logMsg = DateTime.Now + ":\t Exiting SendACKToClient()";
            logger.Log(logMsg);
        }

        private void SendACKToClient(string clientName, SortedDictionary<int, byte[]> sd)
        {
            if (clientBuffers.ContainsKey(clientName))
            {
                Packet sendData = new Packet();
                Client recipient;
                if (sd.Count != 0)
                {
                    int lastValidSeqNum = sd.Keys.First();
                    sendData.ChatMessage = "ACK";
                    sendData.RecipientName = clientName;
                    sendData.SenderName = "Server";
                    sendData.ChatDataIdentifier = DataIdentifier.Message;

                    for (int i = sd.Keys.First(); i <= sd.Keys.Last(); i++)
                    {
                        if (sd.ContainsKey(i) && i == lastValidSeqNum)
                        {
                            lastValidSeqNum++;
                        }
                        else break;
                    }

                    sendData.SequenceNumber = lastValidSeqNum;
                    byte[] data = sendData.GetDataStream();
                    recipient = clientBuffers[clientName];
                    serverSocket.BeginSendTo(data, 0, data.Length, SocketFlags.None, recipient.EpSender, new AsyncCallback(SendData), recipient.socket);
                }
            }
        }

        public void InsertInSenderWaitingForACKs(string name, int sequenceNumber, byte[] byteData)
        {
            try
            {
                if (!senderWaitingForACKs.ContainsKey(name))
                {
                    senderWaitingForACKs.TryAdd(name, new SortedDictionary<int, byte[]>());
                }
                if (!senderWaitingForACKs[name].ContainsKey(sequenceNumber))
                {
                    senderWaitingForACKs[name].Add(sequenceNumber, byteData);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
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
            ChatServer server = new ChatServer(serverPort);
            Thread t2 = new Thread(() => server.ProcessSendBuffer());
            t2.Start();
            //Thread t3 = new Thread(server.messagesRate);
            //t3.Start();
            //Thread t5 = new Thread(server.SendACKToClient);
            //t5.Start();
            Thread t6 = new Thread(server.ProcessReceiveData);
            t6.Start();
            //Thread t7 = new Thread();
            server.StartListening();
            t1.Join();
            t2.Join();
            //t3.Join();
            //t5.Join();
            t6.Join();
        }
    }
}
