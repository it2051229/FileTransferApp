
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.net.InetAddress;
import java.net.NetworkInterface;
import java.net.ServerSocket;
import java.net.Socket;
import java.util.Collections;
import java.util.Scanner;

public class FileTransferServer {

    private final static int BYTE_TRANSFER_RATE = 8192;
    private final static int SOCKET_TIME_OUT = 30000;
    private static Scanner in = new Scanner(System.in);
    private static FileOutputStream outFile = null;

    // Utility function that forces the user to enter an integer value
    public static int readInt() {
        while (true) {
            try {
                return Integer.parseInt(in.nextLine());
            } catch (Exception e) {
                System.out.print("Enter an integer value: ");
            }
        }
    }

    // Read a string without using the read UTF function to be more portable
    public static String readString(DataInputStream inputStream) throws Exception {
        // Expect the number of bytes
        int bytesLength = inputStream.readInt();

        // Read the bytes
        byte[] bytes = new byte[bytesLength];
        inputStream.read(bytes, 0, bytesLength);
        return new String(bytes);
    }

    // Write a string without using the write UTF function to be more portable
    public static void writeString(DataOutputStream outputStream, String str) throws Exception {
        // Send how many bytes to expect
        byte[] bytes = str.getBytes();
        outputStream.writeInt(bytes.length);

        // Write the bytes
        outputStream.writeBytes(str);
    }

    // List all the IP addresses of the computer where this server is running
    // This will only list IPv4 addresses
    public static void displayIPAddresses() {
        System.out.println("Listing all your IP addresses...");

        try {
            // Scan each network interface hardware
            for (NetworkInterface networkInterface : Collections.list(NetworkInterface.getNetworkInterfaces())) {
                networkInterface.getName();

                // Scan all IP addresses of a hardware
                for (InetAddress address : Collections.list(networkInterface.getInetAddresses())) {
                    if (address.getHostAddress().matches("^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$")) {
                        System.out.println("IP Address: " + address.getHostAddress() + ", Name: " + networkInterface.getDisplayName() + " (" + networkInterface.getName() + ")");
                    }
                }
            }

            System.out.println("NOTE: One of the IP addresses above is what the file transfer client needs to connect to this server.");
        } catch (Exception e) {
            System.out.println("Error displayIPAddresses(): " + e.getMessage());
            System.exit(0);
        }
    }

    // Attempt to open an available server socket
    public static ServerSocket openServerSocket() {
        // Let the user enter a port number where to listen connection        
        while (true) {
            System.out.print("Enter an available port number where to listen for incoming connection: ");

            // Attempt to open the connection
            try {
                return new ServerSocket(readInt());
            } catch (Exception e) {
                System.out.println("Error openServerSocket(): " + e.getMessage());
            }
        }
    }

    // Transfer file from client 
    public static void receiveFile(DataInputStream dataInFromClient) throws Exception {
        System.out.println("File upload requested...");
        String filename = readString(dataInFromClient);
        long fileSizeInBytes = dataInFromClient.readLong();
        boolean isNewFile = dataInFromClient.readBoolean();

        System.out.println("Filename : " + filename);
        System.out.println("File size: " + fileSizeInBytes + " bytes");
        System.out.println("New File : " + isNewFile);

        File file = new File(filename);

        if (isNewFile) {
            if (file.exists()) {
                file.delete();
            }
            
            file.createNewFile();
        }

        // Tell client what byte should we start
        long receivedBytes = file.length();
        outFile = new FileOutputStream(file, true);

        byte[] data = new byte[BYTE_TRANSFER_RATE];

        while (receivedBytes < fileSizeInBytes) {
            int readBytes = dataInFromClient.read(data, 0, data.length);
            outFile.write(data, 0, readBytes);

            receivedBytes += readBytes;

            // Update stats
            double progress = ((double) receivedBytes / (double) fileSizeInBytes) * 100;
            System.out.println("Receiving " + filename + ": " + String.format("%.12f", progress) + "% ...");
        }

        System.out.println("Received " + filename + " complete!");
    }

    // Transfer file to client
    public static void sendFile(DataInputStream dataInFromClient, DataOutputStream dataOutToClient) throws Exception {
        System.out.println("File download requested...");
        String filename = readString(dataInFromClient);

        System.out.println("Filename : " + filename);

        // Check the existence of file
        File file = new File(filename);

        if (!file.exists()) {
            // Tell client request is invalid
            System.out.println("File does not exist, request rejected!");
            dataOutToClient.writeBoolean(false);
            return;
        }

        // Tell client request is valid
        dataOutToClient.writeBoolean(true);

        // Send the data to client
        long fileSizeInBytes = file.length();
        dataOutToClient.writeLong(fileSizeInBytes);

        // Check the starting byte download        
        long sentBytes = dataInFromClient.readLong();
        System.out.println("File size: " + fileSizeInBytes + " bytes");
        System.out.println("Starting download at: " + sentBytes + " bytes");

        FileInputStream inFile = new FileInputStream(file);
        inFile.skip(sentBytes);

        byte[] data = new byte[BYTE_TRANSFER_RATE];
        int readBytes = inFile.read(data);

        while (readBytes > 0) {
            dataOutToClient.write(data, 0, readBytes);

            // Update stats
            sentBytes += readBytes;
            double progress = ((double) sentBytes / (double) fileSizeInBytes) * 100;
            System.out.println("Sending " + filename + ": " + String.format("%.12f", progress) + "% ...");

            // Read next data
            readBytes = inFile.read(data);
        }

        System.out.println("Sending " + filename + " complete!");
        inFile.close();
    }

    // Entry point of the program
    public static void main(String[] args) {
        // List all IP so the user has an idea what to put in the file transfer client later on
        displayIPAddresses();

        // Open a server socket where to listen connection
        ServerSocket serverSocket = openServerSocket();
        System.out.println("Server is running (press CTRL+C or CTRL+D to exit)...");

        while (true) {
            Socket clientSocket = null;

            try {
                // Wait for a file transfer request
                clientSocket = serverSocket.accept();
                clientSocket.setSoTimeout(SOCKET_TIME_OUT);

                System.out.println("A client connected...");

                // Setup communication point
                DataInputStream dataInFromClient = new DataInputStream(clientSocket.getInputStream());
                DataOutputStream dataOutToClient = new DataOutputStream(clientSocket.getOutputStream());

                // Get the request
                String request = readString(dataInFromClient);

                if (request.equalsIgnoreCase("upload")) {
                    receiveFile(dataInFromClient);
                } else if (request.equalsIgnoreCase("download")) {
                    sendFile(dataInFromClient, dataOutToClient);
                } else if (request.equalsIgnoreCase("ping")) {
                    System.out.println("Client pinged server connection!");
                }

                // We're done with the request
            } catch (Exception e) {
                System.out.println("Error main(): " + e.getMessage());
            } finally {
                if (clientSocket != null) {
                    try {
                        clientSocket.close();
                    } catch (Exception e) {
                    }
                }

                if (outFile != null) {
                    try {
                        outFile.close();
                    } catch (Exception e) {
                    }

                    outFile = null;
                }
            }
        }
    }

}
