using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FileTransferServer
{
    class Program
    {
        private const int BYTE_TRANSFER_RATE = 8192;
        private const int SOCKET_TIME_OUT = 30000;
        private static BinaryWriter outFile = null;
    
        // Attempt to open an available server socket
        public static TcpListener OpenServerSocket() 
        {
            return new TcpListener(8000);
        }

        // Transfer file from client
        public static void ReceiveFile(DataInputStream dataInFromClient)
        {
            Console.WriteLine("File upload requested...");
            string filename = dataInFromClient.ReadString();
            long fileSizeInBytes = dataInFromClient.ReadLong();
            bool isNewFile = dataInFromClient.ReadBoolean();

            Console.WriteLine("Filename : " + filename);
            Console.WriteLine("File size: " + fileSizeInBytes + " bytes");
            Console.WriteLine("New File : " + isNewFile);

            // Do file transfer
            long receivedBytes = dataInFromClient.ReadLong();

            FileStream fileStream = new FileStream(filename, isNewFile ? FileMode.Create : FileMode.Append);
            fileStream.Seek(receivedBytes, SeekOrigin.Begin);
            
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
            fileStream.Close();
            outFile.Close();
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

            FileStream fileStream = new FileStream(filename, FileMode.Open);
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
                        ReceiveFile(dataInFromClient);
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

                    // Close the used file for the request
                    if(outFile != null)
                    {
                        try
                        {
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
