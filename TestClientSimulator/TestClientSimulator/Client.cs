using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TestClientSimulator
{
    class Client
    {
        Queue<byte[]> _sendQueue;
        Queue<byte[]> _receiveQueue;
        int _lastReceiveACK;
        int _lastSentACK;
        int _portNum;
        Socket _socket;

        public Client(Socket s)
        {
            _sendQueue = new Queue<byte[]>();
            _lastReceiveACK = 0;
            _lastSentACK = 0;
            _portNum = 0;
            _socket = s;
        }

        public Socket socket
        {
            get { return _socket; }
            set { _socket = value; }
        }

        public int LastReceiveACK
        {
            get { return _lastReceiveACK; }
            set { _lastReceiveACK = value; }
        }

        public int LastSentACK
        {
            get { return _lastSentACK; }
            set { _lastSentACK = value; }
        }

        public int PortNumber
        {
            get { return _portNum; }
            set { _portNum = value; }
        }

        public Queue<byte[]> SendQueue
        {
            get { return _sendQueue; }
        }

        public Queue<byte[]> ReceiveQueue
        {
            get { return _receiveQueue; }
        }

        public void InsertInSendQueue(byte[] byteData)
        {
            _sendQueue.Enqueue(byteData);
        }

        public byte[] PeekAtSendQueue()
        {
            if (_sendQueue.Count != 0)
            {
                return _sendQueue.Peek();
            }
            return null;
        }

        public byte[] RemoveFromSendQueue()
        {
            if (_sendQueue.Count != 0)
            {
                return _sendQueue.Dequeue();
            }
            return null;
        }

        public void InsertInReceiveQueue(byte[] byteData)
        {
            _receiveQueue.Enqueue(byteData);
        }

        public byte[] PeekAtReceiveQueue()
        {
            if (_receiveQueue.Count != 0)
            {
                return _receiveQueue.Peek();
            }
            return null;
        }

        public byte[] RemoveFromReceiveQueue()
        {
            if (_receiveQueue.Count != 0)
            {
                return _receiveQueue.Dequeue();
            }
            return null;
        }

        public void SendMessage(Packet pkt, EndPoint epServer)
        {
            try
            {
                byte[] byteData = pkt.GetDataStream();
                InsertInSendQueue(byteData);
                _socket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None, epServer, new AsyncCallback(SendCallback), _socket);
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
                _socket.BeginReceiveFrom(obj.dataStream, 0, obj.dataStream.Length, SocketFlags.None, ref epServer, new AsyncCallback(ReceiveCallback), obj);
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
                _socket.EndReceive(ar);
                _sendQueue.Enqueue(clientObj.dataStream);
            }
            catch (Exception e)
            {

            }
        }
    }
}
