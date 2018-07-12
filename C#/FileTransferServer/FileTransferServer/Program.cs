using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FileTransferServer
{
    class Program
    {
        private const int BYTE_TRANSFER_RATE = 8192;
        private const int SOCKET_TIME_OUT = 30000;

        private static FileStream fileStream = null;
        private static BinaryWriter outFile = null;

        // Utility function that forces the user to enter an integer value
        public static int ReadInt()
        {
            while (true)
            {
                int value;

                if (int.TryParse(Console.ReadLine(), out value))
                    return value;

                Console.WriteLine("Enter an integer value: ");
            }
        }
    
        // Display all the IP addresses of this computer, IPv4 only
        public static void DisplayIPAddresses()
        {
            Console.WriteLine("Listing all your IP addresses...");

            try
            {
                // Scan each network interface hardware
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 
                        || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                        foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                Console.WriteLine("IP Address: " + ip.Address.ToString() + ", Name: " + ni.Name);
                }

                Console.WriteLine("NOTE: One of the IP addresses above is what the file transfer client needs to connect to this server.");
            }
            catch(Exception e)
            {
                Console.WriteLine("Error DisplayIPAddresses(): " + e.Message);
                Environment.Exit(0);
            }
        }
        
        // Attempt to open an available server socket
        public static TcpListener OpenServerSocket() 
        {
            while(true)
            {
                Console.Write("Enter an available port number where to listen for incoming connection: ");

                // Attempt to open the connection
                try
                {
                    return new TcpListener(IPAddress.Any, ReadInt());
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error OpenServerSocket(): " + e.Message);
                }
            }
        }

        // Transfer file from client
        public static void ReceiveFile(DataInputStream dataInFromClient, DataOutputStream dataOutToClient)
        {
            Console.WriteLine("File upload requested...");
            string filename = dataInFromClient.ReadString();
            long fileSizeInBytes = dataInFromClient.ReadLong();
            bool isNewFile = dataInFromClient.ReadBoolean();

            Console.WriteLine("Filename : " + filename);
            Console.WriteLine("File size: " + fileSizeInBytes + " bytes");
            Console.WriteLine("New File : " + isNewFile);

            if(isNewFile)
            {
                // Delete old file if it exists
                if (File.Exists(filename))
                    File.Delete(filename);

                File.Create(filename).Close();
            }

            // Tell client the bytes we have for the file so that they can continue uploading on that point
            long receivedBytes = new FileInfo(filename).Length;
            dataOutToClient.WriteLong(receivedBytes);

            fileStream = new FileStream(filename, FileMode.Append);                        
            outFile = new BinaryWriter(fileStream);

            byte[] data = new byte[BYTE_TRANSFER_RATE];

            while (receivedBytes < fileSizeInBytes) {
                int readBytes = dataInFromClient.Read(data, 0, data.Length);
                outFile.Write(data, 0, readBytes);

                receivedBytes += readBytes;

                // Update stats
                double progress = ((double) receivedBytes / (double) fileSizeInBytes) * 100;
                Console.WriteLine("Receiving " + filename + ": " + progress.ToString("N12") + "% ...");
            }

            Console.WriteLine("Received " + filename + " complete!");
        }

        // Transfer file to client
        public static void SendFile(DataInputStream dataInFromClient, DataOutputStream dataOutToClient)
        {
            Console.WriteLine("File download requested...");
            string filename = dataInFromClient.ReadString();

            Console.WriteLine(filename);

            // Check the existence of file
            if (!File.Exists(filename))
            {
                Console.WriteLine("File does not exist, request rejected!");
                dataOutToClient.WriteBoolean(false);
                return;
            }

            // Tell client request is valid
            dataOutToClient.WriteBoolean(true);

            // Send the data to client
            long fileSizeInBytes = new FileInfo(filename).Length;
            dataOutToClient.WriteLong(fileSizeInBytes);

            // Check the starting byte download        
            long sentBytes = dataInFromClient.ReadLong();
            Console.WriteLine("File size: " + fileSizeInBytes + " bytes");
            Console.WriteLine("Starting download at: " + sentBytes + " bytes");

            fileStream = new FileStream(filename, FileMode.Open);
            fileStream.Seek(sentBytes, SeekOrigin.Begin);

            BinaryReader inFile = new BinaryReader(fileStream);

            byte[] data = new byte[BYTE_TRANSFER_RATE];
            int readBytes = inFile.Read(data, 0, data.Length);

            while (readBytes > 0) {
                dataOutToClient.Write(data, 0, readBytes);

                // Update stats
                sentBytes += readBytes;
                double progress = ((double) sentBytes / (double) fileSizeInBytes) * 100;
                Console.WriteLine("Sending " + filename + ": " + progress.ToString("N12") + "% ...");

                // Read next data
                readBytes = inFile.Read(data, 0, data.Length);
            }

            Console.WriteLine("Sending " + filename + " complete!");
            inFile.Close();
            fileStream.Close();
        }

        // Entry point of the program
        static void Main(string[] args)
        {
            // Display all IP addresses
            DisplayIPAddresses();

            // Start the server
            TcpListener serverSocket = OpenServerSocket();
            serverSocket.Start();

            Console.WriteLine("Server is running (press CTRL+C or CTRL+D to exit)...");

            while (true)
            {
                TcpClient clientSocket = null;

                try
                {
                    // Wait for a file transfer request
                    clientSocket = serverSocket.AcceptTcpClient();
                    clientSocket.ReceiveTimeout = SOCKET_TIME_OUT;

                    Console.WriteLine("A client connected...");

                    DataInputStream dataInFromClient = new DataInputStream(clientSocket.GetStream());
                    DataOutputStream dataOutToClient = new DataOutputStream(clientSocket.GetStream());

                    // Get the request
                    string request = dataInFromClient.ReadString().ToLower();

                    if (request.Equals("upload"))
                        ReceiveFile(dataInFromClient, dataOutToClient);
                    else if (request.Equals("download"))
                        SendFile(dataInFromClient, dataOutToClient);
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error Main(): " + e.Message);
                }
                finally
                {
                    // Close the used socket
                    if(clientSocket != null)
                    {
                        try
                        {
                            clientSocket.Close();
                        }
                        catch { }

                        clientSocket = null;
                    }

                    // Close any filestream
                    if (fileStream != null)
                    {
                        try
                        {
                            fileStream.Flush();
                            fileStream.Close();
                        }
                        catch { }

                        fileStream = null;
                    }

                    // Close the used file for the request
                    if(outFile != null)
                    {
                        try
                        {
                            outFile.Flush();
                            outFile.Close();
                        }
                        catch { }

                        outFile = null;
                    }
                }
            }
        }
    }
}
