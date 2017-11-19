using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestClientSimulator
{
    class ClientsList
    {
        private List<string> _senderClientsProducerList = null;
        private List<string> _senderClientsConsumerList = null;
        private List<string> _senderClientsList1 = new List<string>();
        private List<string> _senderClientsList2 = new List<string>();

        private static object senderClientsProducerLock = new object();
        private static object senderClientsConsumerLock = new object();

        private List<string> _senderAwaitingACKsProducerList = new List<string>();
        private List<string> _senderAwaitingACKsConsumerList = new List<string>();
        private List<string> _senderAwaitingACKsClientsList1 = new List<string>();
        private List<string> _senderAwaitingACKsClientsList2 = new List<string>();

        private static object senderAwaitingACKsProducerLock = new object();
        private static object senderAwaitingACKsConsumerLock = new object();

        public ClientsList()
        {
            _senderClientsProducerList = _senderClientsList1;
            _senderClientsConsumerList = _senderClientsList2;

            _senderAwaitingACKsProducerList = _senderAwaitingACKsClientsList1;
            _senderAwaitingACKsConsumerList = _senderAwaitingACKsClientsList2;
        }

        public void InsertInSenderClientsProducerList(string clientName)
        {
            lock (senderClientsProducerLock)
            {
                lock (senderClientsConsumerLock)
                {
                    if (!_senderClientsProducerList.Contains(clientName) && !_senderClientsConsumerList.Contains(clientName))
                    {
                        _senderClientsProducerList.Add(clientName);
                    }
                }
            }
        }

        public void RemoveFromProducerConsumerSendList(string clientName)
        {
            lock (senderClientsProducerLock)
            {
                if (_senderClientsProducerList.Contains(clientName))
                {
                    _senderClientsProducerList.Remove(clientName);
                }
            }

            lock (senderClientsConsumerLock)
            {
                if (_senderClientsConsumerList.Contains(clientName))
                {
                    _senderClientsConsumerList.Contains(clientName);
                }
            }
        }

        public void InsertInSenderClientsAwaitingACKsProducerList(string clientName)
        {
            lock (senderAwaitingACKsProducerLock)
            {
                if (!_senderAwaitingACKsProducerList.Contains(clientName))
                {
                    _senderAwaitingACKsProducerList.Add(clientName);
                }
            }
        }

        public List<string> SenderConsumerList
        {
            get { return _senderClientsConsumerList; }
        }

        public List<string> SenderAwaitingACKsConsumerList
        {
            get { return _senderAwaitingACKsConsumerList; }
        }

        public void RemoveFromConsumerSendList(string clientName)
        {
            lock (senderClientsConsumerLock)
            {
                if (_senderClientsConsumerList.Contains(clientName))
                {
                    _senderClientsConsumerList.Remove(clientName);
                }
            }
        }

        public void RemoveFromConsumerSendAwaitingACKsList(string clientName)
        {
            lock (senderClientsConsumerLock)
            {
                if (_senderClientsConsumerList.Contains(clientName))
                {
                    _senderClientsConsumerList.Remove(clientName);
                }
            }
        }

        private void SwapAwaitingACKsProducerList()
        {
            if (_senderAwaitingACKsProducerList == _senderAwaitingACKsClientsList1)
            {
                _senderAwaitingACKsProducerList = _senderAwaitingACKsClientsList2;
            }
            else
            {
                _senderAwaitingACKsProducerList = _senderAwaitingACKsClientsList1;
            }
        }

        private void SwapAwaitingACKsConsumerList()
        {
            if (_senderAwaitingACKsConsumerList == _senderAwaitingACKsClientsList1)
            {
                _senderAwaitingACKsConsumerList = _senderAwaitingACKsClientsList2;
            }
            else
            {
                _senderAwaitingACKsConsumerList = _senderAwaitingACKsClientsList1;
            }
        }

        public void SwapAwaitingACKsProducerConsumer()
        {
            lock (senderAwaitingACKsProducerLock)
            {
                lock (senderAwaitingACKsConsumerLock)
                {
                    SwapAwaitingACKsProducerList();
                    SwapAwaitingACKsConsumerList();
                }
            }
        }

        private void SwapProducerList()
        {
            if (_senderClientsProducerList == _senderClientsList1)
            {
                _senderClientsProducerList = _senderClientsList2;
            }
            else
            {
                _senderClientsProducerList = _senderClientsList1;
            }
        }

        private void SwapConsumerList()
        {
            if (_senderClientsConsumerList == _senderClientsList1)
            {
                _senderClientsConsumerList = _senderClientsList2;
            }
            else
            {
                _senderClientsConsumerList = _senderClientsList1;
            }
        }

        public void SwapProducerConsumerList()
        {
            lock (senderClientsProducerLock)
            {
                lock (senderClientsConsumerLock)
                {
                    SwapProducerList();
                    SwapConsumerList();
                }
            }
        }
    }
}
