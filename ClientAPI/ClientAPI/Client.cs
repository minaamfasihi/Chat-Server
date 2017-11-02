using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using PacketAPI;
using System.Threading.Tasks;
using System.Linq;

namespace ClientAPI
{
    public class Client
    {
        string _name;
        string _friendName;

        SortedDictionary<int, byte[]> _producerSendBuffer;
        SortedDictionary<int, byte[]> _consumerSendBuffer;
        SortedDictionary<int, byte[]> _sendBuffer1;
        SortedDictionary<int, byte[]> _sendBuffer2;
        SortedDictionary<int, byte[]> _receiveBuffer = new SortedDictionary<int, byte[]>();

        Socket _socket;
        EndPoint epSender;
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
            _sendBuffer1 = new SortedDictionary<int, byte[]>();
            _sendBuffer2 = new SortedDictionary<int, byte[]>();
            _producerSendBuffer = _sendBuffer1;
            _consumerSendBuffer = _sendBuffer2;

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

        public SortedDictionary<int, byte[]> ProducerSendBuffer
        {
            get { return _producerSendBuffer; }
        }

        public SortedDictionary<int, byte[]> ConsumerSendBuffer
        {
            get { return _consumerSendBuffer; }
        }

        public SortedDictionary<int, byte[]> ReceiveBuffer
        {
            get { return _receiveBuffer; }
        }

        public EndPoint EpSender
        {
            get { return epSender; }
            set { epSender = value; }
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
            //lock (lockProducerBuffer)
            //{
            //    if (_producerSendBuffer.Count != 0)
            //    {
            //        byte[] byteData = 
            //        _producerSendQueue.Dequeue();
            //    }
            //}
            return null;
        }

        public void SwapProducerBuffer()
        {
            if (_producerSendBuffer == _sendBuffer1)
            {
                _producerSendBuffer = _sendBuffer2;
            }
            else
            {
                _producerSendBuffer = _sendBuffer1;
            }
        }

        public void SwapConsumerBuffer()
        {
            if (_consumerSendBuffer == _sendBuffer1)
            {
                _consumerSendBuffer = _sendBuffer2;
            }
            else
            {
                _consumerSendBuffer = _sendBuffer1;
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

        public void InsertInSendBuffer(int sequenceNumber, byte[] byteData)
        {
            lock (lockProducerBuffer)
            {
                if (!_producerSendBuffer.ContainsKey(sequenceNumber))
                {
                    _producerSendBuffer.Add(sequenceNumber, byteData);
                }
            }
        }

        public void CleanUpSendQueue(int lastACK)
        {
            if (_consumerSendBuffer.Count != 0)
            {
                foreach (int i = _consumerSendBuffer.Keys.)
                {

                }
            }
        }

        public bool ReceiveBufferHasKey(int key)
        {
            if (_receiveBuffer.ContainsKey(key))
            {
                return true;
            }
            return false;
        }

        public void SendMessage(Packet pkt, EndPoint epServer)
        {
            try
            {
                byte[] byteData = pkt.GetDataStream();
                InsertInSendBuffer(pkt.SequenceNumber, byteData);
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
