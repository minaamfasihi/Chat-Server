using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestUdpServer
{
    class ClientsList
    {
        private List<string> senderClientsListProducer = null;
        private List<string> senderClientsListConsumer = null;
        private List<string> senderClientsList1 = new List<string>();
        //private List<string>
        private static object senderClientsListLock = new object();
    }
}
