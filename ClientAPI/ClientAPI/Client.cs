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
        SortedDictionary<int, byte[]> _awaitingBroadcastACKsBuffer = new SortedDictionary<int, byte[]>();
        SortedDictionary<int, byte[]> _producerBroadcastBuffer = new SortedDictionary<int, byte[]>();
        SortedDictionary<int, byte[]> _consumerBroadcastBuffer = new SortedDictionary<int, byte[]>();
        SortedDictionary<int, byte[]> _broadcastBuffer1 = new SortedDictionary<int, byte[]>();
        SortedDictionary<int, byte[]> _broadcastBuffer2 = new SortedDictionary<int, byte[]>();

        Socket _socket;
        EndPoint _epSender;
        byte[] dataStream = new byte[1024];

        object lockProducerBuffer = new object();
        object lockConsumerBuffer = new object();
        object lockAwaitingACKsSendBuffer = new object();

        object lockBroadcastProducerBuffer = new object();
        object lockBroadcastConsumerBuffer = new object();
        object lockAwaitingACKsBroadcastBuffer = new object();

        int _lastIncomingACKForSend;
        int _lastIncomingACKForBroadcast;
        int _lastOutgoingACKForSend;
        int _lastOutgoingACKForBroadcast;
        int _portNum;

        public Client (string name, string friendName, EndPoint epSender)
        {
            _name = name;
            _friendName = friendName;
            _epSender = epSender;

            _awaitingSendACKsBuffer = new SortedDictionary<int, byte[]>();
            _awaitingBroadcastACKsBuffer = new SortedDictionary<int, byte[]>();

            _sendBuffer1 = new SortedDictionary<int, byte[]>();
            _sendBuffer2 = new SortedDictionary<int, byte[]>();
            
            _producerSendBuffer = _sendBuffer1;
            _consumerSendBuffer = _sendBuffer2;

            _broadcastBuffer1 = new SortedDictionary<int, byte[]>();
            _broadcastBuffer2 = new SortedDictionary<int, byte[]>();

            _producerBroadcastBuffer = _broadcastBuffer1;
            _consumerBroadcastBuffer = _broadcastBuffer2;

            _lastIncomingACKForSend = 0;
            _lastOutgoingACKForSend = 0;

            _lastIncomingACKForBroadcast = 0;
            _lastOutgoingACKForBroadcast = 0;

            _portNum = 0;
            _socket = null;
        }

        public Client(Socket s, string name)
        {
            _name = name;
            _awaitingSendACKsBuffer = new SortedDictionary<int, byte[]>();
            _awaitingBroadcastACKsBuffer = new SortedDictionary<int, byte[]>();

            _sendBuffer1 = new SortedDictionary<int, byte[]>();
            _sendBuffer2 = new SortedDictionary<int, byte[]>();

            _producerSendBuffer = _sendBuffer1;
            _consumerSendBuffer = _sendBuffer2;

            _broadcastBuffer1 = new SortedDictionary<int, byte[]>();
            _broadcastBuffer2 = new SortedDictionary<int, byte[]>();

            _producerBroadcastBuffer = _broadcastBuffer1;
            _consumerBroadcastBuffer = _broadcastBuffer2;

            _lastIncomingACKForSend = 0;
            _lastOutgoingACKForSend = 0;

            _lastIncomingACKForBroadcast = 0;
            _lastOutgoingACKForBroadcast = 0;

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

        public int LastIncomingACKForSend
        {
            get { return _lastIncomingACKForSend; }
            set { _lastIncomingACKForSend = value; }
        }

        public int LastOutgoingACKForSend
        {
            get { return _lastOutgoingACKForSend; }
            set { _lastOutgoingACKForSend = value; }
        }

        public int LastIncomingACKForBroadcast
        {
            get { return _lastIncomingACKForBroadcast; }
            set { _lastIncomingACKForBroadcast = value; }
        }

        public int LastOutgoingACKForBroadcast
        {
            get { return _lastOutgoingACKForBroadcast; }
            set { _lastOutgoingACKForBroadcast = value; }
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

        public SortedDictionary<int, byte[]> AwaitingBroadcastACKsBuffer
        {
            get { return _awaitingBroadcastACKsBuffer; }
        }

        public SortedDictionary<int, byte[]> ProducerBroadcastBuffer
        {
            get { return _producerBroadcastBuffer; }
        }

        public SortedDictionary<int, byte[]> ConsumerBroadcastBuffer
        {
            get { return _consumerBroadcastBuffer; }
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

        public void InsertInAwaitingBroadcastACKsBuffer(int sequenceNumber, byte[] byteData)
        {
            if (!_awaitingBroadcastACKsBuffer.ContainsKey(sequenceNumber))
            {
                _awaitingBroadcastACKsBuffer.Add(sequenceNumber, byteData);
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

        public void SwapProducerBroadcastBuffer()
        {
            if (_producerBroadcastBuffer == _broadcastBuffer1)
            {
                _producerBroadcastBuffer = _broadcastBuffer2;
            }
            else
            {
                _producerBroadcastBuffer = _broadcastBuffer1;
            }
        }

        public void SwapConsumerBroadcastBuffer()
        {
            if (_consumerBroadcastBuffer == _broadcastBuffer1)
            {
                _consumerBroadcastBuffer = _broadcastBuffer2;
            }
            else
            {
                _consumerBroadcastBuffer = _broadcastBuffer2;
            }
        }

        public void SwapBroadcastBuffers()
        {
            lock (lockConsumerBuffer)
            {
                lock (lockProducerBuffer)
                {
                    SwapProducerBroadcastBuffer();
                    SwapConsumerBroadcastBuffer();
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

        public void InsertInBroadcastBuffer(int sequenceNumber, byte[] byteData)
        {
            lock (lockBroadcastProducerBuffer)
            {
                if (!_producerBroadcastBuffer.ContainsKey(sequenceNumber))
                {
                    _producerBroadcastBuffer.Add(sequenceNumber, byteData);
                }
            }
        }

        public void MoveFromConsumerBroadcastToACKBuffer(int sequenceNumber, byte[] byteData)
        {
            lock (lockBroadcastConsumerBuffer)
            {
                if (_consumerBroadcastBuffer.ContainsKey(sequenceNumber))
                {
                    InsertInAwaitingBroadcastACKsBuffer(sequenceNumber, byteData);
                    _consumerBroadcastBuffer.Remove(sequenceNumber);
                }
            }
        }

        public void MoveFromConsumerToACKBuffer(int sequenceNumber, byte[] byteData)
        {
            lock (lockConsumerBuffer)
            {
                if (_consumerSendBuffer.ContainsKey(sequenceNumber))
                {
                    InsertInAwaitingSendACKsBuffer(sequenceNumber, byteData);
                    _consumerSendBuffer.Remove(sequenceNumber);
                }
            }
        }

        public void CleanAwaitingACKsSendBuffer()
        {
            if (_awaitingSendACKsBuffer.Count != 0)
            {
                for (int i = _awaitingSendACKsBuffer.Keys.First(); (_awaitingSendACKsBuffer.Count != 0) && i < _lastIncomingACKForSend; i++)
                {
                    if (_awaitingSendACKsBuffer.ContainsKey(i))
                    {
                        lock (lockAwaitingACKsSendBuffer)
                        {
                            _awaitingSendACKsBuffer.Remove(i);
                        }
                    }
                    else break;
                }
            }

            if (_awaitingBroadcastACKsBuffer.Count != 0)
            {
                for (int i = _awaitingBroadcastACKsBuffer.Keys.First(); (_awaitingBroadcastACKsBuffer.Count != 0) && i < _lastIncomingACKForBroadcast; i++)
                {
                    if (_awaitingBroadcastACKsBuffer.ContainsKey(i))
                    {
                        lock (lockAwaitingACKsBroadcastBuffer)
                        {
                            _awaitingBroadcastACKsBuffer.Remove(i);
                        }
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
