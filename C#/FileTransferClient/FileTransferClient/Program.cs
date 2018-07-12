using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FileTransferClient
{
    class Program
    {
        private const int BYTE_TRANSFER_RATE = 8192;
        private const int SOCKET_TIME_OUT = 30000;
    
        private static string serverIP;
        private static int serverPort;

        // Utility function that forces the user to enter an integer value
        public static int ReadInt() 
        {
            while (true) 
            {
                int value;

                if(int.TryParse(Console.ReadLine(), out value))
                    return value;

                Console.WriteLine("Enter an integer value: ");
            }
        }

        // Connect to server and return the socket
        public static void ReadServerDetails() 
        {
            while (true) 
            {
                try 
                {
                    // Get server details
                    Console.Write("Enter Server's IP Address: ");
                    serverIP = Console.ReadLine();
                
                    Console.Write("Enter Server's Port Number: ");
                    serverPort = ReadInt();

                    // Do test and connect
                    TcpClient socket = new TcpClient(serverIP, serverPort); 
                    DataOutputStream dataOutToServer = new DataOutputStream(socket.GetStream());
                    dataOutToServer.WriteString("ping");
                    socket.Close();
                
                    return;
                } 
                catch (Exception e) 
                {
                    Console.WriteLine("Error readServerDetails(): " + e.Message);
                }
            }
        }

        // Upload a file continuing a given starting byte
        public static long SendFile(string filename, bool isNewFile) 
        {
            long returnStatus;

            // Check that the file exists        
            if (!File.Exists(filename)) 
            {
                Console.WriteLine("File does not exist, upload rejected!");
                return -1; // Do not restart send file
            }

            // Do upload
            long fileSizeInBytes = new FileInfo(filename).Length;

            // Tell server we're sending a file
            FileStream fileStream = new FileStream(filename, FileMode.Open);
            BinaryReader inFile = null;
            TcpClient socket = null;
        
            try 
            {
                socket = new TcpClient(serverIP, serverPort);
                socket.ReceiveTimeout = SOCKET_TIME_OUT;
            
                DataOutputStream dataOutToServer = new DataOutputStream(socket.GetStream());
                DataInputStream dataInFromServer = new DataInputStream(socket.GetStream());

                dataOutToServer.WriteString("upload");
                dataOutToServer.WriteString(filename);
                dataOutToServer.WriteLong(fileSizeInBytes);
                dataOutToServer.WriteBoolean(isNewFile);

                // Get how much upload was sent to the server
                long finishedBytes = dataInFromServer.ReadLong();
            
                Console.WriteLine("File size: " + fileSizeInBytes + " bytes");
                Console.WriteLine("Starting upload at " + finishedBytes + " bytes");
            
                byte[] data = new byte[BYTE_TRANSFER_RATE];

                fileStream.Seek(finishedBytes, SeekOrigin.Begin);
                inFile = new BinaryReader(fileStream);
                int readBytes = inFile.Read(data, 0, data.Length);
            
                while (readBytes > 0) 
                {
                    dataOutToServer.Write(data, 0, readBytes);

                    // Update stats
                    finishedBytes += readBytes;
                    double progress = ((double) finishedBytes / (double) fileSizeInBytes) * 100;
                    Console.WriteLine("Sending " + filename + ": " + progress.ToString("N12") + "% ...");

                    // Read next data
                    readBytes = inFile.Read(data, 0, data.Length);
                }

                // Upload complete
                Console.WriteLine("Sent " + filename + ", upload complete!");
                returnStatus = -2; // Means don't restart the upload
            } 
            catch (Exception e) 
            {
                Console.WriteLine("Error sendFile(): " + e.Message);
                returnStatus = 0; // Restart download
            } 
            finally 
            {
                // Close resources
                try 
                {
                    if (inFile != null) 
                    {
                        inFile.Close();
                        fileStream.Close();
                    }
                } 
                catch { }
            
                try 
                {
                    if (socket != null) 
                        socket.Close();
                } 
                catch { }
            }
        
            return returnStatus;
        }

        // Upload a file to server, keep uploading even with
        // intermittent network connection
        public static void SendFile(string filename) 
        {
            Console.WriteLine("Uploading file " + filename + " ...");
        
            long sendStatus = 0;
            bool isNewFile = true;

            while (sendStatus >= 0)
            {
                sendStatus = SendFile(filename, isNewFile);
                isNewFile = false;
            }
        }

        // Receive a file continuing at a given starting byte
        // Returns the number of total finished bytes
        public static long ReceiveFile(string filename, long startByte) {
            long finishedBytes = startByte;
            long returnStatus;
            
            FileStream fileStream = null;
            BinaryWriter outFile = null;
            TcpClient socket = null;
        
            try 
            {
                socket = new TcpClient(serverIP, serverPort);
                socket.ReceiveTimeout = SOCKET_TIME_OUT;
            
                DataOutputStream dataOutToServer = new DataOutputStream(socket.GetStream());
                DataInputStream dataInFromServer = new DataInputStream(socket.GetStream());
            
                dataOutToServer.WriteString("download");
                dataOutToServer.WriteString(filename);
            
                if (dataInFromServer.ReadBoolean()) 
                {
                    // Tell server what byte to start downloading
                    dataOutToServer.WriteLong(startByte);

                    // Get the expected length of file
                    long fileSizeInBytes = dataInFromServer.ReadLong();

                    // Continue and append to file
                    fileStream = new FileStream(filename, FileMode.Append);
                    outFile = new BinaryWriter(fileStream);

                    byte[] data = new byte[BYTE_TRANSFER_RATE];
                
                    Console.WriteLine("Starting download at " + startByte + " bytes");
                
                    while (finishedBytes < fileSizeInBytes) 
                    {
                        int readBytes = dataInFromServer.Read(data, 0, data.Length);
                        outFile.Write(data, 0, readBytes);
                        
                        finishedBytes += readBytes;

                        // Update stats
                        double progress = ((double) finishedBytes / (double) fileSizeInBytes) * 100;
                        Console.WriteLine("Receiving " + filename + ": " + progress.ToString("N12") + "% ...");
                    }

                    // Download complete
                    Console.WriteLine("Received " + filename + ", download complete!");
                    returnStatus = -2; // Means don't restart the download
                } 
                else 
                {
                    // Stop if the file in server does not exist
                    Console.WriteLine("File does not exist, download rejected!");
                    returnStatus = -1; // Means don't restart the download
                }
            } 
            catch (Exception e) 
            {
                Console.WriteLine("Error ReceiveFile(): " + e.Message);

                // It means restart the download on error, and continue on this byte when restarting
                returnStatus = finishedBytes;
            } 
            finally 
            {
                // Close resources
                try 
                {
                    if (outFile != null) 
                    {
                        outFile.Close();
                        fileStream.Close();
                    }
                } 
                catch { }
            
                try 
                {
                    if (socket != null)
                        socket.Close();
                } 
                catch { }
            }
        
            return returnStatus;
        }

        // Download a file from server, keep dowloading even with
        // intermittent network connection
        public static void ReceiveFile(string filename) {
            // If a local file exists, then delete it            
            if(File.Exists(filename))
            {
                Console.WriteLine("A file with the same name in the directory exists, it was deleted.");
                File.Delete(filename);
            }
                
            Console.WriteLine("Downloading file " + filename + " ...");
            long receiveStatus = 0;
        
            while (receiveStatus >= 0)
                receiveStatus = ReceiveFile(filename, receiveStatus);
        }

        // Entry point of client
        public static void Main(string[] args) {
            // Test connection
            ReadServerDetails();

            // Wait for client commands
            Console.WriteLine("To upload a file to server:");
            Console.WriteLine("    > upload <filename>");
            Console.WriteLine("To download a file from server:");
            Console.WriteLine("    > download <filename>");
            Console.WriteLine("To exit:");
            Console.WriteLine("   > exit");
        
            try 
            {
                while (true) 
                {
                    Console.Write("server@" + serverIP + ":" + serverPort + "> ");
                    string[] tokens = Console.ReadLine().Split(null);
                
                    if(tokens.Length == 0)
                    {
                        Console.WriteLine("Error: Invalid command.");
                        continue;
                    }
                    
                    string command = tokens[0].ToLower();

                    if(command.Equals("exit"))
                        break;
                    else if(command.Equals("upload") && tokens.Length >= 2)
                        SendFile(tokens[1]);
                    else if(command.Equals("download") && tokens.Length >= 2)
                        ReceiveFile(tokens[1]);
                    else
                        Console.WriteLine("Error: Invalid command.");
                }
            } 
            catch (Exception e) 
            {
                Console.WriteLine("Error Main(): " + e.Message);
            }
        }
    }
}
