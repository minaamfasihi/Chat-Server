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
using System.Collections.Concurrent;

namespace TestClientSimulator
{

    class StateObject
    {
        public Socket client = null;
        public byte[] dataStream = new byte[1024];
    }

    class Client
    {
        Queue<Packet> sendQueue;
        int lastReceiveACK;
        int lastSentACK;
        int portNum;

        public Client()
        {
            this.sendQueue = new Queue<Packet>();
            lastReceiveACK = 0;
            lastSentACK = 0;
            portNum = 0;
        }

        void ReceiveMessage()
        {

        }
    }

    class ClientSimulator
    {
        private static ConcurrentDictionary<string, Client> clientObjects = new ConcurrentDictionary<string, Client>();

        // Server End Point
        private static EndPoint epServer;

        public static AutoResetEvent connectDone = new AutoResetEvent(false);
        public static AutoResetEvent receiveDone = new AutoResetEvent(false);
        public static AutoResetEvent sendDone = new AutoResetEvent(false);
        public static AutoResetEvent incrementPortNumber = new AutoResetEvent(false);
        private static ManualResetEvent epSet = new ManualResetEvent(false);
        private static AutoResetEvent processSendQueue = new AutoResetEvent(false);
        private static AutoResetEvent cleanSendQueue = new AutoResetEvent(false);
        private static AutoResetEvent cleanReceiveQueue = new AutoResetEvent(false);
        private static AutoResetEvent throttleSender = new AutoResetEvent(false);
        // Instance level sequence numbers

        private static Hashtable ClientSockets = new Hashtable();

        private static int LBport = 9000;
        private static int availablePortNumsOffset;
        private static int startPortNumber;
        private static int endPortNumber;
        private static int numOfPktsSent = 0;
        private static int numOfPktsReceived = 0;
        private static int windowSize = 4;
        private static int numOfPktsProduced = 0;
        private static int prevNumOfPktsProduced = 0;

        private static System.Timers.Timer aTimer;

        private static Hashtable sendMessageBuffer = new Hashtable();
        private static Hashtable generateSequenceNumbers = new Hashtable();
        private static Hashtable receiveMessageBuffer = new Hashtable();
        private static Hashtable sentACKEDSequenceNumbers = new Hashtable();
        private static Hashtable receivedACKEDSequenceNumbers = new Hashtable();
        private static Hashtable friendOf = new Hashtable();

        private static readonly Random getrandom = new Random();

        private static LogWriter logger = Logger.Instance;

        private static readonly object syncLock = new object();

        private static int totalNumOfClients;

        private static object syncSendBuffer = new object();
        private static object syncReceiveBuffer = new object();

        private static string LBIPAddress;
        private static string serverIPAddress;
        private static string clientIPAddress;
        private static string fileName = @"C:\Users\minaam.fasihi\Documents\Projects\Client-Simulator-logs-";

        public static int GetRandomNumber(int min, int max)
        {
            lock (syncLock)
            { 
                return getrandom.Next(min, max);
            }
        }

        public ClientSimulator()
        {
        }

        public static void ConnectToLoadBalancer()
        {
            string logMsg;
            try
            {
                logMsg = DateTime.Now + "\t In ConnectToLoadBalancer()";
                logger.Log(logMsg);;

                IPAddress ipAddr = IPAddress.Parse(LBIPAddress);
                IPEndPoint remoteEndPoint = new IPEndPoint(ipAddr, LBport);

                Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                connectDone.Reset();
                client.BeginConnect(remoteEndPoint, new AsyncCallback(LBConnectCallback), client);
                connectDone.WaitOne();

                logMsg = DateTime.Now + "\t Exiting ConnectToLoadBalancer()";
                logger.Log(logMsg);;
            }

            catch (Exception e)
            {
                logMsg = DateTime.Now + "\t " + e.ToString();
                logger.Log(logMsg);;
            }
        }

        public static void LBConnectCallback(IAsyncResult ar)
        {
            string logMsg = DateTime.Now + "\t In LBConnectToCallback()";
            logger.Log(logMsg);;

            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndConnect(ar);
                connectDone.Set();
                LBRequestForServer(client);
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + "\t " + e.ToString();
                logger.Log(logMsg);;
            }
            logMsg = DateTime.Now + "\t Exiting LBConnectCallback()";
            logger.Log(logMsg);;
        }

        public static void LBRequestForServer(Socket client)
        {
            string logMsg = DateTime.Now + "\t In LBRequestForServer()";
            logger.Log(logMsg);;

            try
            {
                string friendName = "fasihi";
                Packet sendData = new Packet(friendName);
                sendData.SenderName = "minaam";
                sendData.ChatMessage = "request";
                byte[] byteData = sendData.GetDataStream();

                sendDone.Reset();
                client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(LBRequestForServerCallback), client);
                sendDone.WaitOne();
            }

            catch (Exception e)
            {
                logMsg = DateTime.Now + "\t " + e.ToString();
                logger.Log(logMsg);;
            }

            logMsg = DateTime.Now + "\t Exiting LBRequestForServer()";
            logger.Log(logMsg);;
        }

        public static void LBRequestForServerCallback(IAsyncResult ar)
        {
            string logMsg = DateTime.Now + "\t In LBRequestForServerCallback()";
            logger.Log(logMsg);;
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndSend(ar);
                sendDone.Set();
                receiveDone.Reset();
                StateObject clientObj = new StateObject();
                clientObj.client = client;
                client.BeginReceive(clientObj.dataStream, 0, clientObj.dataStream.Length, 0, new AsyncCallback(LBReceiveCallback), clientObj);
                receiveDone.WaitOne();
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + "\t " + e.ToString();
                logger.Log(logMsg);;
            }
            logMsg = DateTime.Now + "\t Exiting LBRequestForServerCallback()";
            logger.Log(logMsg);;
        }

        public static void LBReceiveCallback(IAsyncResult ar)
        {
            string logMsg = DateTime.Now + "\t In LBReceiveCallBack()";
            logger.Log(logMsg);;
            try
            {
                StateObject clientObj = (StateObject)ar.AsyncState;
                Socket client = clientObj.client;
                byte[] dataStream = clientObj.dataStream;
                client.EndReceive(ar);
                
                Packet receivedData = new Packet(dataStream);
                char[] delimiters = { ':' };
                string[] serverAddress = receivedData.ChatMessage.Split(delimiters);
                IPAddress serverIP = IPAddress.Parse(serverAddress[0]);
                IPEndPoint server = new IPEndPoint(serverIP, int.Parse(serverAddress[1]));
                epServer = (EndPoint)server;
                Console.WriteLine("This is the address of server received from LB: {0} : {1}", server.Address, server.Port);
                receiveDone.Set();
                epSet.Set();
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + "\t " + e.ToString();
                logger.Log(logMsg);;
            }
            logMsg = DateTime.Now + "\t Exiting LBReceiveCallback()";
            logger.Log(logMsg);;
        }

        public static void ConnectToServer()
        {
            string logMsg = DateTime.Now + "\t In ConnectToServer()";
            logger.Log(logMsg);;

            try
            {

                incrementPortNumber.WaitOne();
                String name = availablePortNumsOffset.ToString();
                availablePortNumsOffset++;

                epSet.WaitOne();
                // Initialise socket
                Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                client.Bind(new IPEndPoint(IPAddress.Parse(clientIPAddress), int.Parse(name)));

                name = client.LocalEndPoint.ToString();

                Packet sendData = new Packet();
                sendData.SenderName = name;
                sendData.RecipientName = null;
                sendData.ChatMessage = null;
                sendData.ChatDataIdentifier = DataIdentifier.LogIn;


                ClientSockets.Add(name, client);
                incrementPortNumber.Set();
                // Get packet as byte array
                byte[] data = sendData.GetDataStream();

                // Send data to server
                client.BeginSendTo(data, 0, data.Length, SocketFlags.None, epServer, new AsyncCallback(SendDataSocket), client);

                // Begin listening for messages
                numOfPktsReceived++;
                sendDone.WaitOne();
                StateObject clientObj = new StateObject();
                clientObj.client = client;
                client.BeginReceiveFrom(clientObj.dataStream, 0, clientObj.dataStream.Length, SocketFlags.None, ref epServer, new AsyncCallback(ReceiveData), clientObj);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                logMsg = DateTime.Now + "\t " + e.ToString();
                logger.Log(logMsg);;
                incrementPortNumber.Set();
            }

            logMsg = DateTime.Now + "\t Exiting ConnectToServer()";
            logger.Log(logMsg);;
        }

        private static bool liesInRangeForSend(Queue<Packet> queue)
        {
            if (queue.Count != 0)
            {
                return (queue.Last().SequenceNumber - queue.First().SequenceNumber) < windowSize;
            }
            return true;
        }

        private static int nextSequenceNumber(string clientName)
        {
            if (!generateSequenceNumbers.ContainsKey(clientName))
            {
                generateSequenceNumbers.Add(clientName, 1);
            }
            int nextSeqNo = (int)generateSequenceNumbers[clientName];
            generateSequenceNumbers[clientName] = nextSeqNo + 1;
            return nextSeqNo;
        }

        private static void OnTimedEvent(object course, ElapsedEventArgs e)
        {
            Console.WriteLine("Total number of packets received: {0}", numOfPktsReceived);
            Console.WriteLine("Total number of packets sent: {0}", numOfPktsSent);
            Console.WriteLine("Current port number is: {0}", availablePortNumsOffset);
        }

        public static void SendMessage(Socket client)
        {
            string logMsg = DateTime.Now + "\t In SendMessage()";
            logger.Log(logMsg);;

            try
            {
                string sender = client.LocalEndPoint.ToString();
                string friend = "";
                if (friendOf.ContainsKey(sender))
                {
                    friend = friendOf[sender].ToString();
                }
                else
                {
                    return;
                }
                numOfPktsProduced++;
                if (friend != "")
                {
                    Packet sendData = new Packet(friend);
                    string clientName = client.LocalEndPoint.ToString();
                    sendData.SenderName = clientName;
                    //sendData.ChatMessage = @"TestingTestingTestingTestingTestingTestingTestingTestingTestingTesting\" +
                    //    "TestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTestingTesting";
                    sendData.ChatMessage = "Hello";
                    sendData.SequenceNumber = 1; // nextSequenceNumber(clientName);
                    sendData.ChatDataIdentifier = DataIdentifier.Message;

                    if (sendData.ChatMessage.ToLower() == "quit")
                    {
                        return;
                    }
                    if (sendMessageBuffer.Contains(sendData.SenderName))
                    {
                        Queue<Packet> q = (Queue<Packet>)sendMessageBuffer[sendData.SenderName];
                        //if (q.Count < windowSize && liesInRangeForSend(q)) 
                        {
                            lock (syncSendBuffer)
                            {
                                q.Enqueue(sendData);
                            }
                        }
                    }
                    else
                    {
                        Queue<Packet> q = new Queue<Packet>();
                        lock (syncSendBuffer)
                        {
                            q.Enqueue(sendData);
                            sendMessageBuffer.Add(sendData.SenderName, q);
                        }
                    }
                    processSendQueue.Set();
                }
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + "\t " + e.ToString();
                logger.Log(logMsg);;
            }
            logMsg = DateTime.Now + "\t Exiting SendMessage()";
            logger.Log(logMsg);;
        }

        //private void Logout(Packet sendData)
        //{
        //    try
        //    {
        //        if (clientSocket != null)
        //        {
        //            // Initialise a packet object to store the data to be sent
        //            sendData.ChatDataIdentifier = DataIdentifier.LogOut;
        //            sendData.SenderName = name;
        //            sendData.ChatMessage = null;

        //            // Get packet as byte array
        //            byte[] byteData = sendData.GetDataStream();

        //            // Send packet to the server
        //            this.clientSocket.SendTo(byteData, 0, byteData.Length, SocketFlags.None, epServer);

        //            // Close the socket
        //            this.clientSocket.Close();
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine(e.Message);
        //    }
        //}

        public static void SendDataSocket(IAsyncResult ar)
        {
            string logMsg = DateTime.Now + "\t In SendData()";
            logger.Log(logMsg);;

            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndSendTo(ar);
                sendDone.Set();
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + "\t " + e.ToString();
                logger.Log(logMsg);;
            }

            logMsg = DateTime.Now + "\t Exiting SendData()";
            logger.Log(logMsg);;
        }

        public static void SendDataObject(IAsyncResult ar)
        {
            string logMsg = DateTime.Now + "\t In SendData()";
            logger.Log(logMsg);;

            try
            {
                StateObject clientObj = (StateObject)ar.AsyncState;
                clientObj.client.EndSendTo(ar);
                sendDone.Set();
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + "\t " + e.ToString();
                logger.Log(logMsg);;
            }

            logMsg = DateTime.Now + "\t Exiting SendData()";
            logger.Log(logMsg);;
        }

        private void ProcessSendQueue()
        {
            while (true)
            {
                processSendQueue.WaitOne();

                string logMsg = "";
                logMsg = DateTime.Now + ":\t In ProcessSendQueue()";
                logger.Log(logMsg);;

                try
                {
                    Hashtable tempSendBuffer = null;

                    lock (syncSendBuffer)
                    {
                        if (sendMessageBuffer.Count != 0)
                        {
                            tempSendBuffer = new Hashtable(sendMessageBuffer);
                        }
                    }

                    if (tempSendBuffer.Count != 0)
                    {
                        foreach (DictionaryEntry keyVal in tempSendBuffer)
                        {
                            Queue<Packet> tempQueue = new Queue<Packet>((Queue<Packet>)keyVal.Value);
                            foreach (var pkt in tempQueue)
                            {
                                byte[] byteData = pkt.GetDataStream();
                                if (ClientSockets.Contains(pkt.SenderName))
                                {
                                    Socket clientSocket = (Socket)ClientSockets[pkt.SenderName];
                                    clientSocket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None, epServer, new AsyncCallback(SendDataSocket), clientSocket);
                                }
                            }
                        }
                        //Thread.Sleep(1);
                    }

                    logMsg = DateTime.Now + ":\t Exiting ProcessSendQueue()";
                    logger.Log(logMsg);;
                }
                catch (Exception e)
                {
                    logMsg = DateTime.Now + ":\t " + e.ToString();
                    logger.Log(logMsg);;
                }

                //Thread.Sleep(10);
            }
        }

        public static void ReceiveData(IAsyncResult ar)
        {
            string logMsg = DateTime.Now + "\t In ReceiveData()";
            logger.Log(logMsg);

            try
            {
                // Receive all data
                StateObject clientObj = (StateObject)ar.AsyncState;
                Socket client = clientObj.client;
                client.EndReceive(ar);

                // Initialise a packet object to store the received data
                Packet receivedData = new Packet(clientObj.dataStream);

                if (receivedData.ChatMessage == "ACK")
                {
                    if (sentACKEDSequenceNumbers.Contains(receivedData.RecipientName))
                    {
                        sentACKEDSequenceNumbers[receivedData.RecipientName] = receivedData.SequenceNumber;
                    }
                    else
                    {
                        sentACKEDSequenceNumbers.Add(receivedData.RecipientName, receivedData.SequenceNumber);
                    }
                    cleanSendQueue.Set();
                }

                else
                {
                    if (liesInRangeForReceive(receivedData))
                    {
                        if (receiveMessageBuffer.Contains(receivedData.SenderName))
                        {
                            SortedDictionary<int, Packet> temp = (SortedDictionary<int, Packet>)receiveMessageBuffer[receivedData.SenderName];
                            lock (syncReceiveBuffer)
                            {
                                temp.Add(receivedData.SequenceNumber, receivedData);
                            }
                        }
                        else
                        {
                            SortedDictionary<int, Packet> temp = new SortedDictionary<int, Packet>();
                            temp.Add(receivedData.SequenceNumber, receivedData);

                            lock (syncReceiveBuffer)
                            {
                                receiveMessageBuffer.Add(receivedData.SenderName, temp);
                            }
                        }
                        SendACKToServer(receivedData.SenderName, receivedData.RecipientName);
                    }
                }

                // Reset data stream
                StateObject obj = new StateObject();
                obj.dataStream = new byte[1024];
                obj.client = client;
                receiveDone.Set();
                // Continue listening for broadcasts
                client.BeginReceiveFrom(obj.dataStream, 0, obj.dataStream.Length, SocketFlags.None, ref epServer, new AsyncCallback(ReceiveData), obj);
            }

            catch (ObjectDisposedException o)
            {
                logMsg = DateTime.Now + "\t " + o.ToString();
                logger.Log(logMsg);;
            }

            catch (Exception e)
            {
                logMsg = DateTime.Now + "\t " + e.ToString();
                logger.Log(logMsg);;
            }

            logMsg = DateTime.Now + "\t Exiting ReceiveData()";
            logger.Log(logMsg);;
        }

        private static void SendACKToServer(string senderName, string recipientName)
        {
            string logMsg = DateTime.Now + ":\t In SendACKToServer()";
            logger.Log(logMsg);;
            Packet sendData = new Packet();

            SortedDictionary<int, Packet> sortedDict = new SortedDictionary<int, Packet>((SortedDictionary <int, Packet>)receiveMessageBuffer[senderName]);

            if (sortedDict.Count != 0)
            {
                int lastValidSeqNum = sortedDict.Keys.First();
                sendData.ChatMessage = "ACK";
                sendData.RecipientName = senderName;
                sendData.SenderName = recipientName;
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

                if (receivedACKEDSequenceNumbers.Contains(senderName))
                {
                    receivedACKEDSequenceNumbers[senderName] = lastValidSeqNum;
                }
                
                cleanReceiveQueue.Set();
                byte[] data = sendData.GetDataStream();
                StateObject obj = new StateObject();
                obj.client = (Socket)ClientSockets[senderName];
                obj.client.BeginSendTo(data, 0, data.Length, SocketFlags.None, epServer, new AsyncCallback(SendDataObject), obj);
            }

            logMsg = DateTime.Now + ":\t Exiting SendACKToServer()";
            logger.Log(logMsg);;
        }

        private static void Initialize()
        {
            char[] delimiters = { ':' };
            string friendAddress = "";

            foreach (DictionaryEntry dict in ClientSockets)
            {
                string[] friendEp = dict.Key.ToString().Split(delimiters);
                int friendNum = int.Parse(friendEp[1]) + 1;

                receivedACKEDSequenceNumbers[dict.Key] = 0;
                sentACKEDSequenceNumbers[dict.Key] = 0;

                if (friendNum > endPortNumber)
                {
                    friendNum = startPortNumber;
                }
                friendAddress = clientIPAddress.ToString() + ":" + friendNum.ToString();
                if (ClientSockets.ContainsKey(friendAddress))
                {
                    friendOf.Add(dict.Key, friendAddress);
                }
            }
        }

        private static bool liesInRangeForReceive(Packet pkt)
        {
            if (receivedACKEDSequenceNumbers.Contains(pkt.SenderName))
            {
                int lastACKED = (int)receivedACKEDSequenceNumbers[pkt.SenderName];
                return (pkt.SequenceNumber >= lastACKED) && (pkt.SequenceNumber <= lastACKED + windowSize);
            }
            return false;
        }

        private static void CleanUpSendQueue()
        {
            while (true)
            {
                cleanSendQueue.WaitOne();

                lock (syncSendBuffer)
                {
                    foreach (DictionaryEntry dict in sendMessageBuffer)
                    {
                        Queue<Packet> q = (Queue<Packet>)dict.Value;
                        int ackedSeqNum = 0;

                        if (sentACKEDSequenceNumbers.Contains(dict.Key))
                        {
                            ackedSeqNum = (int)sentACKEDSequenceNumbers[dict.Key];
                            while (q.Any())
                            {
                                if (q.Peek().SequenceNumber < ackedSeqNum)
                                {
                                    q.Dequeue();
                                }
                                else break;
                            }
                        }
                    }
                }
                throttleSender.Set();
            }
        }

        private static void CleanUpReceiveQueue()
        {
            while (true)
            {
                cleanReceiveQueue.WaitOne();

                lock (syncReceiveBuffer)
                {
                    foreach (DictionaryEntry dict in receiveMessageBuffer)
                    {
                        SortedDictionary<int, Packet> temp = (SortedDictionary<int, Packet>)dict.Value;
                        int lastACKED = (int)receivedACKEDSequenceNumbers[dict.Key];
                        if (temp != null && temp.Count != 0)
                        {
                            for (int i = temp.Keys.First(); temp.Any() && i <= temp.Keys.Last(); i++)
                            {
                                if (temp.Keys.Contains(i) && lastACKED > i)
                                {
                                    temp.Remove(i);
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void SimulateClients()
        {
            string logMsg = DateTime.Now + "\t In SimulateClients()";
            logger.Log(logMsg);

            establishConnections();

            logMsg = DateTime.Now + "\t Exiting SimulateClients()";
            logger.Log(logMsg);
        }

        public static void establishConnections()
        {
            string logMsg = "";
            try
            {
                logMsg = DateTime.Now + "\t In establishConnections()";
                logger.Log(logMsg);

                ConnectToLoadBalancer();

                Console.WriteLine("Total number of clients: {0}", totalNumOfClients);
                for (int i = 0; i < totalNumOfClients; i++)
                {
                    ConnectToServer();
                    //Thread.Sleep(1);
                }

                Console.WriteLine("Made all the connections");

                logMsg = DateTime.Now + "\t Exiting establishConnections()";
                logger.Log(logMsg);
            }
            catch (Exception e)
            {
                logMsg = DateTime.Now + "\t " + e.ToString();
                logger.Log(logMsg);
            }
        }

        private static void SendMessage()
        {
            while (true)
            {
                foreach (DictionaryEntry dict in ClientSockets)
                {
                    Socket client = (Socket)dict.Value;
                    SendMessage(client);
                    //Thread.Sleep(1);
                }
            }
        }

        private static void MessagesProduced(object course, ElapsedEventArgs e)
        {
            int pktsProduced = (numOfPktsProduced - prevNumOfPktsProduced);
            Console.WriteLine("Packets processed: {0}", pktsProduced);
            prevNumOfPktsProduced = numOfPktsProduced;
        }

        private void MessageProductionRate()
        {
            aTimer = new System.Timers.Timer(1000);
            aTimer.Elapsed += MessagesProduced;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }

        static void Main(string[] args)
        {
            fileName += args[0].ToString() + ".txt";
            startPortNumber = int.Parse(args[1]);
            endPortNumber = int.Parse(args[2]);
            totalNumOfClients = (endPortNumber - startPortNumber) + 1;
            availablePortNumsOffset = int.Parse(args[3]);
            LBIPAddress = args[4].ToString();
            serverIPAddress = args[5].ToString();
            clientIPAddress = args[6].ToString();
            incrementPortNumber.Set();

            SimulateClients();

            ClientSimulator simulator = new ClientSimulator();
            Thread t1 = new Thread(simulator.ProcessSendQueue);
            t1.Start();

            Thread t2 = new Thread(() => logger.WriteToFile(fileName));
            t2.Start();

            Thread t3 = new Thread(CleanUpSendQueue);
            t3.Start();

            Thread t4 = new Thread(CleanUpReceiveQueue);
            t4.Start();

            Thread t5 = new Thread(simulator.MessageProductionRate);
            t5.Start();

            Initialize();
            SendMessage();

            t1.Join();
            t2.Join();
            t3.Join();
            t4.Join();
            t5.Join();
        }
    }
}
