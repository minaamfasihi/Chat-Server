using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PacketAPI
{
    // ----------------
    // Packet Structure
    // ----------------

    // Description   -> |dataIdentifier|name length|message length|    name   |    message   |
    // Size in bytes -> |       4      |     4     |       4      |name length|message length|

    public enum DataIdentifier
    {
        Message,
        LogIn,
        LogOut,
        Null,
        Broadcast
    }

    public class Packet
    {
        #region Private Members
        private DataIdentifier dataIdentifier;
        private string senderName;
        private string recipientName;
        private string message;
        private int seqNumber;
        #endregion

        #region Public Properties
        public DataIdentifier ChatDataIdentifier
        {
            get { return dataIdentifier; }
            set { dataIdentifier = value; }
        }

        public string SenderName
        {
            get { return senderName; }
            set { senderName = value; }
        }

        public string RecipientName
        {
            get { return recipientName; }
            set { recipientName = value; }
        }

        public string ChatMessage
        {
            get { return message; }
            set { message = value; }
        }

        public int SequenceNumber
        {
            get { return seqNumber; }
            set { seqNumber = value; }
        }
        #endregion

        #region Methods
        //Default Constructor
        public Packet()
        {
            dataIdentifier = DataIdentifier.Null;
            message = null;
            senderName = null;
            recipientName = null;
            seqNumber = 0;
        }

        public Packet(string friendName)
        {
            dataIdentifier = DataIdentifier.Null;
            message = null;
            senderName = null;
            recipientName = friendName;
            seqNumber = 0;
        }

        public Packet(Packet pkt)
        {
            ChatDataIdentifier = pkt.ChatDataIdentifier;
            ChatMessage = pkt.ChatMessage;
            SenderName = pkt.SenderName;
            RecipientName = pkt.RecipientName;
            SequenceNumber = pkt.SequenceNumber;
        }

        public Packet(Hashtable dict)
        {
            ChatMessage = null;
            foreach (DictionaryEntry itr in dict)
            {
                ChatMessage += itr.Key.ToString() + "&";
            }
            senderName = "LoadBalancer";
            ChatDataIdentifier = DataIdentifier.Message;
            recipientName = null;
        }

        public Packet(byte[] dataStream)
        {
            // Read the sequence number from the packet
            seqNumber = BitConverter.ToInt32(dataStream, 0);

            // Read the data identifier from the beginning of the stream (4 bytes)
            dataIdentifier = (DataIdentifier)BitConverter.ToInt32(dataStream, 4);

            // Read the length of the name (4 bytes)
            int senderNameLength = BitConverter.ToInt32(dataStream, 8);

            int recipientNameLength = BitConverter.ToInt32(dataStream, 12);

            // Read the length of the message (4 bytes)
            int msgLength = BitConverter.ToInt32(dataStream, 16);

            // Read the name field
            if (senderNameLength > 0)
                senderName = Encoding.UTF8.GetString(dataStream, 20, senderNameLength);
            else
                senderName = null;

            if (recipientNameLength > 0)
                recipientName = Encoding.UTF8.GetString(dataStream, 20 + senderNameLength, recipientNameLength);
            else
                recipientName = null;

            // Read the message field
            if (msgLength > 0)
                message = Encoding.UTF8.GetString(dataStream, 20 + senderNameLength + recipientNameLength, msgLength);
            else
                message = null;
        }

        // Converts the packet into a byte array for sending/receiving 
        public byte[] GetDataStream()
        {
            List<byte> dataStream = new List<byte>();

            // Add the sequence number
            dataStream.AddRange(BitConverter.GetBytes(seqNumber));

            // Add the dataIdentifier
            dataStream.AddRange(BitConverter.GetBytes((int)dataIdentifier));

            // Add the sender name length
            if (senderName != null)
                dataStream.AddRange(BitConverter.GetBytes(senderName.Length));
            else
                dataStream.AddRange(BitConverter.GetBytes(0));

            // Add the recipient name length
            if (recipientName != null)
                dataStream.AddRange(BitConverter.GetBytes(recipientName.Length));
            else
                dataStream.AddRange(BitConverter.GetBytes(0));

            // Add the message length
            if (message != null)
                dataStream.AddRange(BitConverter.GetBytes(message.Length));
            else
                dataStream.AddRange(BitConverter.GetBytes(0));

            // Add the sender's name
            if (senderName != null)
                dataStream.AddRange(Encoding.UTF8.GetBytes(senderName));

            // Add the recipient's name
            if (recipientName != null)
                dataStream.AddRange(Encoding.UTF8.GetBytes(recipientName));

            // Add the message
            if (message != null)
                dataStream.AddRange(Encoding.UTF8.GetBytes(message));

            return dataStream.ToArray();
        }
        #endregion
    }
}
