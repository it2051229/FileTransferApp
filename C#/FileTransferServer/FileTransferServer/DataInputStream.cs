using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FileTransferServer
{
    class DataInputStream
        : BinaryReader
    {
        // Create a data input stream
        public DataInputStream(Stream input)
            : base(input)
        {
        }

        // Read an integer value
        public int ReadInt()
        {
            return IPAddress.NetworkToHostOrder(ReadInt32());
        }

        // Read a long value
        public long ReadLong()
        {
            return IPAddress.NetworkToHostOrder(ReadInt64());
        }

        // Override the read string functionality to read a string value
        public override String ReadString()
        {
            int length = ReadInt();
            byte[] bytes = ReadBytes(length);
 
            // convert bytes using given encoding
            string str = Encoding.UTF8.GetString(bytes);
            return str;
        }
    }
}
