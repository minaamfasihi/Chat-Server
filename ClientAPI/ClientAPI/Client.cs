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
        SortedDictionary<int, byte[]> _awaitingSendACKsBuffer = new SortedDictionary<int, byte[]>();

        Socket _socket;
        EndPoint _epSender;
        byte[] dataStream = new byte[1024];

        object lockProducerBuffer = new object();
        object lockConsumerBuffer = new object();

        int _lastIncomingACK;
        int _lastOutgoingACK;
        int _portNum;

        public Client (string name, string friendName, EndPoint epSender)
        {
            _name = name;
            _friendName = friendName;
            _epSender = epSender;
            _sendBuffer1 = new SortedDictionary<int, byte[]>();
            _sendBuffer2 = new SortedDictionary<int, byte[]>();
            _producerSendBuffer = _sendBuffer1;
            _consumerSendBuffer = _sendBuffer2;

            _lastIncomingACK = 0;
            _lastOutgoingACK = 0;
            _portNum = 0;
            _socket = null;
        }

        public Client(Socket s, string name)
        {
            _name = name;
            _sendBuffer1 = new SortedDictionary<int, byte[]>();
            _sendBuffer2 = new SortedDictionary<int, byte[]>();
            _producerSendBuffer = _sendBuffer1;
            _consumerSendBuffer = _sendBuffer2;

            _lastIncomingACK = 0;
            _lastOutgoingACK = 0;
            _portNum = 0;
            _socket = s;
        }

        public Client()
        {

        }

        public Socket socket
        {
            get { return _socket; }
            set { _socket = value; }
        }

        public int LastIncomingACK
        {
            get { return _lastIncomingACK; }
            set { _lastIncomingACK = value; }
        }

        public int LastOutgoingACK
        {
            get { return _lastOutgoingACK; }
            set { _lastOutgoingACK = value; }
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

        public SortedDictionary<int, byte[]> AwaitingSendACKsBuffer
        {
            get { return _awaitingSendACKsBuffer; }
        }

        public EndPoint EpSender
        {
            get { return _epSender; }
            set { _epSender = value; }
        }

        public void InsertInReceiveBuffer(byte[] byteData, int sequenceNumber)
        {
            if (!_receiveBuffer.ContainsKey(sequenceNumber))
            {
                _receiveBuffer.Add(sequenceNumber, byteData);
            }
        }

        public void InsertInAwaitingSendACKsBuffer(int sequenceNumber, byte[] byteData)
        {
            if (!_awaitingSendACKsBuffer.ContainsKey(sequenceNumber))
            {
                _awaitingSendACKsBuffer.Add(sequenceNumber, byteData);
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

        public void MoveFromConsumerToACKBuffer(int sequenceNumber, Packet pkt)
        {
            InsertInAwaitingSendACKsBuffer(sequenceNumber, pkt.GetDataStream());

            if (_consumerSendBuffer.ContainsKey(sequenceNumber))
            {
                _consumerSendBuffer.Remove(sequenceNumber);
            }
        }

        public void CleanAwaitingACKsSendBuffer()
        {
            if (_awaitingSendACKsBuffer.Count != 0)
            {
                for (int i = _awaitingSendACKsBuffer.Keys.First(); (_awaitingSendACKsBuffer.Count != 0) && i < _lastIncomingACK; i++)
                {
                    if (_awaitingSendACKsBuffer.ContainsKey(i))
                    {
                        _awaitingSendACKsBuffer.Remove(i);
                    }
                    else break;
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
