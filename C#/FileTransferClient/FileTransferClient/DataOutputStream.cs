using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FileTransferClient
{
    class DataOutputStream
        : BinaryWriter
    {
        // Create an output stream
        public DataOutputStream(Stream output)
            : base(output)
        {
        }

        // Write a boolean value
        public void WriteBoolean(bool value)
        {
            Write(value);
        }

        // Write an integer value
        public void WriteInt(int value)
        {
            Write(IPAddress.HostToNetworkOrder(value));
        }

        // Write a long value
        public void WriteLong(long value)
        {
            Write(IPAddress.HostToNetworkOrder(value));
        }

        // Write a string 
        public void WriteString(String s)
        {
            // Transform string to bytes
            byte[] bytes = Encoding.UTF8.GetBytes(s);

            // Write length
            WriteInt(bytes.Length);

            // Write content to stream
            Write(bytes, 0, bytes.Length);
        }
    }
}
