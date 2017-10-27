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
        Queue<byte[]> _readSendBuffer;
        Queue<byte[]> _writeSendBuffer;
        Queue<byte[]> _worker1;
        Queue<byte[]> _worker2;
        Queue<byte[]> _receiveQueue;
        int _lastReceiveACK;
        int _lastSentACK;
        int _portNum;
        Socket _socket;

        public Client(Socket s)
        {
            _writeSendBuffer = new Queue<byte[]>();
            _readSendBuffer = new Queue<byte[]>();
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
            get { return _writeSendBuffer; }
        }

        public Queue<byte[]> ReceiveQueue
        {
            get { return _receiveQueue; }
        }

        public void InsertInSendQueue(byte[] byteData)
        {
            _writeSendBuffer.Enqueue(byteData);
        }

        public byte[] PeekAtSendQueue()
        {
            if (_readSendBuffer.Count != 0)
            {
                return _readSendBuffer.Peek();
            }
            return null;
        }

        public byte[] RemoveFromSendQueue()
        {
            if (_readSendBuffer.Count != 0)
            {
                return _readSendBuffer.Dequeue();
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
                Packet p = new Packet(clientObj.dataStream);
                Console.WriteLine("Sender: {0}", p.SenderName);
                Console.WriteLine("Recipient: {0}", p.RecipientName);
                Console.WriteLine("Chat Message: {0}", p.ChatMessage);
            }
            catch (Exception e)
            {

            }
        }
    }
}
