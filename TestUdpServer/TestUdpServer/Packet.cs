using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestUdpServer
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
            set { dataIdentifier = value;  }
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
            this.dataIdentifier = DataIdentifier.Null;
            this.message = null;
            this.senderName = null;
            this.recipientName = null;
            this.seqNumber = 0;
        }

        public Packet(Packet pkt)
        {
            this.ChatDataIdentifier = pkt.ChatDataIdentifier;
            this.ChatMessage = pkt.ChatMessage;
            this.SenderName = pkt.SenderName;
            this.RecipientName = pkt.RecipientName;
            this.SequenceNumber = pkt.SequenceNumber;
        }

        public Packet(byte[] dataStream)
        {
            // Read the sequence number from the packet
            this.seqNumber = BitConverter.ToInt32(dataStream, 0);
            
            // Read the data identifier from the beginning of the stream (4 bytes)
            this.dataIdentifier = (DataIdentifier)BitConverter.ToInt32(dataStream, 4);

            // Read the length of the name (4 bytes)
            int senderNameLength = BitConverter.ToInt32(dataStream, 8);

            int recipientNameLength = BitConverter.ToInt32(dataStream, 12);

            // Read the length of the message (4 bytes)
            int msgLength = BitConverter.ToInt32(dataStream, 16);

            // Read the name field
            if (senderNameLength > 0)
                this.senderName = Encoding.UTF8.GetString(dataStream, 20, senderNameLength);
            else
                this.senderName = null;

            if (recipientNameLength > 0)
                this.recipientName = Encoding.UTF8.GetString(dataStream, 20 + senderNameLength, recipientNameLength);
            else
                this.recipientName = null;

            // Read the message field
            if (msgLength > 0)
                this.message = Encoding.UTF8.GetString(dataStream, 20 + senderNameLength + recipientNameLength, msgLength);
            else
                this.message = null;
        }

        // Converts the packet into a byte array for sending/receiving 
        public byte[] GetDataStream()
        {
            List<byte> dataStream = new List<byte>();

            // Add the sequence number
            dataStream.AddRange(BitConverter.GetBytes(this.seqNumber));

            // Add the dataIdentifier
            dataStream.AddRange(BitConverter.GetBytes((int)this.dataIdentifier));

            // Add the name length
            if (this.senderName != null)
                dataStream.AddRange(BitConverter.GetBytes(this.senderName.Length));
            else
                dataStream.AddRange(BitConverter.GetBytes(0));

            if (this.recipientName != null)
                dataStream.AddRange(BitConverter.GetBytes(this.recipientName.Length));
            else
                dataStream.AddRange(BitConverter.GetBytes(0));

            // Add the message length
            if (this.message != null)
                dataStream.AddRange(BitConverter.GetBytes(this.message.Length));
            else
                dataStream.AddRange(BitConverter.GetBytes(0));

            // Add the sender's name
            if (this.senderName != null)
                dataStream.AddRange(Encoding.UTF8.GetBytes(this.senderName));

            // Add the recipient's name
            if (this.recipientName != null)
                dataStream.AddRange(Encoding.UTF8.GetBytes(this.recipientName));

            // Add the message
            if (this.message != null)
                dataStream.AddRange(Encoding.UTF8.GetBytes(this.message));

            return dataStream.ToArray();
        }
        #endregion
    }
}
