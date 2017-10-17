using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.Threading;
using TestLoadBalancer;
using System.IO;
using System.Diagnostics;
using LogWriterAPI;

public class LoadBalancer
{
    private static Hashtable serverList = new Hashtable();
    public static ManualResetEvent allDone = new ManualResetEvent(false);
    private static byte[] dataStream = new byte[1024];
    private static ArrayList connectedServers = new ArrayList();
    private static LogWriter logger = Logger.Instance;
    private static string fileName = @"C:\Users\minaam.fasihi\Documents\Projects\LB-logs-";
    private static string LBIPAddress;

    public LoadBalancer()
    {
    }

    public static void StartListening()
    {
        string logMsg = DateTime.Now + ":\t In StartListening()";
        logger.Log(logMsg);
        // Data buffer for incoming data.  
        byte[] bytes = new Byte[1024];

        IPAddress ipAddress = IPAddress.Parse(LBIPAddress);
        // Listen on 9000 port
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 9000);

        // Create a TCP/IP socket.  
        Socket listener = new Socket(AddressFamily.InterNetwork,
            SocketType.Stream, ProtocolType.Tcp);

        // Bind the socket to the local endpoint and listen for incoming connections.  
        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(100);

            while (true)
            {
                // Set the event to nonsignaled state.  
                allDone.Reset();
                // Start an asynchronous socket to listen for connections.  
                Console.WriteLine("Waiting for a connection...");
                listener.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    listener);
                // Wait until a connection is made before continuing.  
                allDone.WaitOne();
            }
        }
        catch (Exception e)
        {
            logMsg = DateTime.Now + ":\t" + e.ToString();
            logger.Log(logMsg);
        }

        logMsg = DateTime.Now + ":\t Exiting StartListening()";
        logger.Log(logMsg);
        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();
    }

    public static void AcceptCallback(IAsyncResult ar)
    {
        try
        {
            string logMsg = DateTime.Now + ":\t In AcceptCallback()";
            logger.Log(logMsg);
            // Signal the main thread to continue.
            allDone.Set();
            // Get the socket that handles the client request.
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);
            Console.WriteLine("The port of the connector server is {0}", handler.LocalEndPoint.ToString());
            handler.BeginReceive(dataStream, 0, dataStream.Length, 0, new AsyncCallback(ReceiveCallback), handler);
            connectedServers.Add(handler);
            logMsg = DateTime.Now + ":\t Exiting AcceptCallback()";
            logger.Log(logMsg);
        }
        catch (Exception e)
        {
            string logMsg = DateTime.Now + ":\t" + e.ToString();
            logger.Log(logMsg);
        }
    }

    private static void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            string logMsg = DateTime.Now + ":\t In ReceiveCallback()";
            logger.Log(logMsg);
            Socket listener = (Socket)ar.AsyncState;
            listener.EndReceive(ar);
            Packet receivedData = new Packet(dataStream);
            Console.WriteLine("This is what I have received: {0}", receivedData.SenderName);
            if (receivedData.ChatMessage == "request")
            {
                string assignedServer = null;
                foreach (DictionaryEntry dict in serverList)
                {
                    if ((int)(dict.Value) < 35000)
                    {
                        Console.WriteLine("this is the server you should connect to");
                        Packet serverDetails = new Packet();
                        serverDetails.SenderName = "LoadBalancer";
                        serverDetails.RecipientName = "client";
                        serverDetails.ChatMessage = (String)dict.Key;
                        serverDetails.ChatDataIdentifier = DataIdentifier.Message;
                        Console.WriteLine(serverDetails.ChatMessage);
                        byte[] byteData = serverDetails.GetDataStream();
                        listener.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(InformClient), listener);
                        assignedServer = (String)dict.Key;
                        break;
                    }
                }
                if (assignedServer != null)
                {
                    int c = (int)serverList[assignedServer];
                    serverList[assignedServer] = ++c;
                    assignedServer = null;
                }
            }
            else
            {
                if (!serverList.ContainsKey(receivedData.SenderName))
                {
                    serverList.Add(receivedData.SenderName, 0);
                }

                // Inform the new server about the connected servers
                Packet sendData = new Packet(serverList);
                byte[] byteData = sendData.GetDataStream();
                listener.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(InformServer), listener);

                foreach (var server in connectedServers)
                {
                    Socket s = (Socket)server;
                }

                // Inform the connected servers about the new server
                foreach (var server in connectedServers)
                {
                    Socket s = (Socket)server;
                    Packet p = new Packet();
                    p.SenderName = "LoadBalancer";
                    p.ChatDataIdentifier = DataIdentifier.Message;
                    p.ChatMessage = receivedData.SenderName;
                    byte[] data = p.GetDataStream();
                    s.BeginSend(data, 0, data.Length, 0, new AsyncCallback(UpdateConnectedServersCallback), s);
                }

                Console.WriteLine("You have total: {0}", (int)serverList[receivedData.SenderName]);
            }
            logMsg = DateTime.Now + ":\t Exiting ReceiveCallback()";
            logger.Log(logMsg);
        }
        catch (Exception e)
        {
            string logMsg = DateTime.Now + ":\t" + e.ToString();
            logger.Log(logMsg);
        }
    }

    private static void UpdateConnectedServersCallback(IAsyncResult ar)
    {
        try
        {
            string logMsg = DateTime.Now + ":\t In UpdateConnectedServersCallback()";
            logger.Log(logMsg);
            Socket serv = (Socket)ar.AsyncState;
            serv.EndSend(ar);
            logMsg = DateTime.Now + ":\t Exiting UpdateConnectedServersCallback()";
            logger.Log(logMsg);
        }
        catch (Exception e)
        {
            string logMsg = DateTime.Now + ":\t" + e.ToString();
            logger.Log(logMsg);
        }
    }

    private static void UpdateExistingServersCallback (IAsyncResult ar)
    {
        try
        {
            string logMsg = DateTime.Now + ":\t In UpdateExistingServersCallback()";
            logger.Log(logMsg);
            Socket listener = (Socket)ar.AsyncState;
            listener.EndSend(ar);
            logMsg = DateTime.Now + ":\t Exiting UpdateExistingServersCallback()";
            logger.Log(logMsg);
        }
        catch (Exception e)
        {
            string logMsg = DateTime.Now + ":\t" + e.ToString();
            logger.Log(logMsg);
        }
    }

    private static void InformClient(IAsyncResult ar)
    {
        try
        {
            string logMsg = DateTime.Now + ":\t In InformClient()";
            logger.Log(logMsg);
            Socket server = (Socket)ar.AsyncState;
            server.EndSend(ar);
            logMsg = DateTime.Now + ":\t Exiting InformClient()";
            logger.Log(logMsg);
        }
        catch (Exception e)
        {
            string logMsg = DateTime.Now + ":\t" + e.ToString();
            logger.Log(logMsg);
        }
    }

    private static void InformServer(IAsyncResult ar)
    {
        try
        {
            string logMsg = DateTime.Now + ":\t In InformServer()";
            logger.Log(logMsg);
            Socket server = (Socket)ar.AsyncState;
            server.EndSend(ar);
            logMsg = DateTime.Now + ":\t Exiting Inform Server()";
            logger.Log(logMsg);
        }
        catch (Exception e)
        {
            string logMsg = DateTime.Now + ":\t" + e.ToString();
            logger.Log(logMsg);
        }
    }

    public static int Main(String[] args)
    {
        fileName += args[0].ToString() + ".txt";
        LBIPAddress = args[1].ToString();
        Thread t = new Thread(() => logger.WriteToFile(fileName));
        t.Start();
        StartListening();
        t.Join();
        return 0;
    }
}