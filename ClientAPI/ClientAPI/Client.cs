using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using PacketAPI;
using System.Threading.Tasks;

namespace ClientAPI
{
    public class Client
    {
        string _name;
        string _friendName;

        Queue<byte[]> _producerSendQueue;
        Queue<byte[]> _consumerSendQueue;
        Queue<byte[]> _sendBuffer1;
        Queue<byte[]> _sendBuffer2;

        SortedDictionary<int, byte[]> _receiveBuffer = new SortedDictionary<int, byte[]>();

        Socket _socket;
        byte[] dataStream = new byte[1024];

        object lockProducerBuffer = new object();
        object lockConsumerBuffer = new object();

        int _lastReceiveACK;
        int _oldestReceiveACK;
        int _lastSentACK;
        int _oldestSentACK;
        int _portNum;

        public Client ()
        {
        }

        public Client(Socket s, string name)
        {
            _name = name;
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

        public SortedDictionary<int, byte[]> ReceiveBuffer
        {
            get { return _receiveBuffer; }
        }

        public void InsertInReceiveBuffer(byte[] byteData, int sequenceNumber)
        {
            if (!_receiveBuffer.ContainsKey(sequenceNumber))
            {
                _receiveBuffer.Add(sequenceNumber, byteData);
            }
        }

        public byte[] DataStream
        {
            get { return dataStream; }
        }

        public void ResetDataStream()
        {
            dataStream = new byte[1024];
        }

        public byte[] PeekAtSendQueue()
        {
            return null;
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public string FriendName
        {
            get { return _friendName; }
            set { _friendName = value; }
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

        public void SendMessage(Packet pkt, EndPoint epServer)
        {
            try
            {
                byte[] byteData = pkt.GetDataStream();
                InsertInSendQueue(byteData);
                _socket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None, epServer, new AsyncCallback(SendCallback), null);
            }
            catch (Exception e)
            {

            }
        }

        public void SendCallback(IAsyncResult ar)
        {
            try
            {
                _socket.EndSendTo(ar);
            }
            catch (Exception e)
            {

            }
        }

        public void ReceiveMessage(Packet pkt, EndPoint epServer)
        {
            try
            {
                dataStream = pkt.GetDataStream();
                _socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref epServer, new AsyncCallback(ReceiveCallback), this);
            }
            catch (Exception e)
            {

            }
        }

        public void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                Client client = (Client)ar.AsyncState;
                _socket.EndReceive(ar);
                //_readSendBuffer.Enqueue(clientObj.dataStream);
                Packet p = new Packet(client.dataStream);
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
