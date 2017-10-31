using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using PacketAPI;
using System.Threading.Tasks;

namespace TestClientSimulator
{
    class Client
    {
        Queue<byte[]> _producerSendQueue;
        Queue<byte[]> _consumerSendQueue;
        Queue<byte[]> _sendBuffer1;
        Queue<byte[]> _sendBuffer2;

        Queue<byte[]> _receiveQueue;

        Socket _socket;

        object lockProducerBuffer = new object();
        object lockConsumerBuffer = new object ();

        int _lastReceiveACK;
        int _lastSentACK;
        int _portNum;

        public Client(Socket s)
        {
            _sendBuffer1 = new Queue<byte[]>();
            _sendBuffer2 = new Queue<byte[]>();
            _producerSendQueue = _sendBuffer1;
            _consumerSendQueue = _sendBuffer2;

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

        public Queue<byte[]> ProducerSendQueue
        {
            get { return _producerSendQueue; }
        }

        public Queue<byte[]> ConsumerSendQueue
        {
            get { return _consumerSendQueue; }
        }

        public Queue<byte[]> ReceiveQueue
        {
            get { return _receiveQueue; }
        }

        public byte[] PeekAtSendQueue()
        {
            return null;
        }

        public byte[] RemoveFromSendQueue()
        {
            lock (lockProducerBuffer)
            {
                if (_producerSendQueue.Count != 0)
                {
                    _producerSendQueue.Dequeue();
                }
            }
            return null;
        }

        public void SwapProducerBuffer()
        {
            if (_producerSendQueue == _sendBuffer1)
            {
                _producerSendQueue = _sendBuffer2;
            }
            else
            {
                _producerSendQueue = _sendBuffer1;
            }
        }

        public void SwapConsumerBuffer()
        {
            if (_consumerSendQueue == _sendBuffer1)
            {
                _consumerSendQueue = _sendBuffer2;
            }
            else
            {
                _consumerSendQueue = _sendBuffer1;
            }
        }

        public void SwapSendBuffers()
        {
            lock (lockConsumerBuffer)
            {
                lock (lockProducerBuffer)
                {
                    SwapProducerBuffer();
                    SwapConsumerBuffer();
                }
            }
        }

        public void InsertInSendQueue(byte[] byteData)
        {
            lock (lockProducerBuffer)
            {
                _producerSendQueue.Enqueue(byteData);
            }
        }

        public void InsertInReceiveQueue(byte[] byteData)
        {
            //lock (lockReceiveBuffer)
            //{
            //    _receiveQueue.Enqueue(byteData);
            //}
        }

        public byte[] PeekAtReceiveQueue()
        {
            //if (_receiveQueue.Count != 0)
            //{
            //    return _receiveQueue.Peek();
            //}
            return null;
        }

        public byte[] RemoveFromReceiveQueue()
        {
            //if (_receiveQueue.Count != 0)
            //{
            //    return _receiveQueue.Dequeue();
            //}
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
                //_readSendBuffer.Enqueue(clientObj.dataStream);
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
