using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestClientSimulator
{
    class Client
    {
        Queue<byte[]> sendQueue;
        Queue<byte[]> receiveQueue;
        int lastReceiveACK;
        int lastSentACK;
        int portNum;
        Socket sock;

        public Client(Socket s)
        {
            sendQueue = new Queue<byte[]>();
            lastReceiveACK = 0;
            lastSentACK = 0;
            portNum = 0;
            sock = s;
        }

        public void SendMessage(Packet pkt, EndPoint epServer)
        {
            try
            {
                byte[] byteData = pkt.GetDataStream();
                sendQueue.Enqueue(byteData);
                sock.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None, epServer, new AsyncCallback(SendCallback), sock);
            }
            catch (Exception e)
            {

            }
        }

        public static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndSendTo(ar);
                Console.WriteLine();
                //sendDone.Set();
            }
            catch (Exception e)
            {

            }
        }

        public void ReceiveMessage(Packet pkt, EndPoint epServer)
        {
            try
            {
                StateObject obj = new StateObject();
                obj.dataStream = pkt.GetDataStream();
                sock.BeginReceiveFrom(obj.dataStream, 0, obj.dataStream.Length, SocketFlags.None, ref epServer, new AsyncCallback(ReceiveCallback), obj);
            }
            catch (Exception e)
            {

            }
        }

        public void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                StateObject clientObj = (StateObject)ar.AsyncState;
                sock.EndReceive(ar);
                sendQueue.Enqueue(clientObj.dataStream);
            }
            catch (Exception e)
            {

            }
        }
    }
}
