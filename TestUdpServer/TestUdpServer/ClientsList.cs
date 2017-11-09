using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestUdpServer
{
    class ClientsList
    {
        private List<string> _senderClientsProducerList = null;
        private List<string> _senderClientsConsumerList = null;
        private List<string> _senderClientsList1 = new List<string>();
        private List<string> _senderClientsList2 = new List<string>();
        private static object senderClientsProducerLock = new object();
        private static object senderClientsConsumerLock = new object();

        public ClientsList()
        {
            _senderClientsProducerList = _senderClientsList1;
            _senderClientsConsumerList = _senderClientsList2;
        }

        public void InsertInSenderClientsProducerList(string clientName)
        {
            lock (senderClientsProducerLock)
            {
                if (!_senderClientsProducerList.Contains(clientName))
                {
                    _senderClientsProducerList.Add(clientName);
                }
            }
        }

        public List<string> SenderConsumerList
        {
            get { return _senderClientsConsumerList; }
        }

        public void RemoveFromConsumerList(string clientName)
        {
            lock (senderClientsConsumerLock)
            {
                if (_senderClientsConsumerList.Contains(clientName))
                {
                    _senderClientsConsumerList.Remove(clientName);
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
