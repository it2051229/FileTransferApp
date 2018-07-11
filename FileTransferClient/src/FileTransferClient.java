
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.net.Socket;
import java.util.Scanner;

public class FileTransferClient {

    private final static int BYTE_TRANSFER_RATE = 8192;
    private final static int SOCKET_TIME_OUT = 30000;

    private static Scanner in = new Scanner(System.in);
    private static String serverIP;
    private static int serverPort;

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

    // Connect to server and return the socket
    public static void readServerDetails() {
        while (true) {
            try {
                // Get server details
                System.out.print("Enter Server's IP Address: ");
                serverIP = in.nextLine();

                System.out.print("Enter Server's Port Number: ");
                serverPort = readInt();

                // Do test and connect
                Socket socket = new Socket(serverIP, serverPort);
                DataOutputStream dataOutToServer = new DataOutputStream(socket.getOutputStream());
                dataOutToServer.writeUTF("ping");
                socket.close();

                return;
            } catch (Exception e) {
                System.out.println("Error readServerDetails(): " + e.getMessage());
            }
        }
    }

    // Upload a file continuing a given starting byte
    public static long sendFile(String filename, long startByte) {
        long finishedBytes = startByte;
        long returnStatus;

        // Check that the file exists
        File file = new File(filename);

        if (!file.exists()) {
            System.out.println("File does not exist, upload rejected!");
            return -1; // Do not restart send file
        }

        // Do upload
        long fileSizeInBytes = file.length();

        // Tell server we're sending a file
        FileInputStream inFile = null;
        Socket socket = null;

        try {

            socket = new Socket(serverIP, serverPort);
            socket.setSoTimeout(SOCKET_TIME_OUT);
            
            DataOutputStream dataOutToServer = new DataOutputStream(socket.getOutputStream());
            dataOutToServer.writeUTF("upload");
            dataOutToServer.writeUTF(filename);
            dataOutToServer.writeLong(fileSizeInBytes);

            // Tell server if this is a continue upload or new one
            // True = New, False = Continue
            dataOutToServer.writeBoolean(startByte == 0);
			dataOutToServer.writeLong(startByte);

			System.out.println("File size: " + fileSizeInBytes + " bytes");
            System.out.println("Starting upload at " + startByte + " bytes");

            byte[] data = new byte[BYTE_TRANSFER_RATE];

            inFile = new FileInputStream(file);
            inFile.skip(startByte);
            int readBytes = inFile.read(data);

            while (readBytes >= 0) {
                dataOutToServer.write(data, 0, readBytes);

                // Update stats
                finishedBytes += readBytes;
                double progress = ((double) finishedBytes / (double) fileSizeInBytes) * 100;
                System.out.println("Sending " + filename + ": " + String.format("%.12f", progress) + "% ...");

                // Read next data
                readBytes = inFile.read(data);
            }
            
            // Upload complete
            System.out.println("Sent " + filename + ", upload complete!");
            returnStatus = -2; // Means don't restart the upload
        } catch (Exception e) {
            System.out.println("Error sendFile(): " + e.getMessage());
            returnStatus = finishedBytes;
        } finally {
            // Close resources
            try {
                if (inFile != null) {
                    inFile.close();
                }
            } catch (Exception e) {
            }

            try {
                if (socket != null) {
                    socket.close();
                }
            } catch (Exception e) {
            }
        }

        return returnStatus;
    }

    // Upload a file to server, keep uploading even with
    // intermittent network connection
    public static void sendFile(String filename) throws Exception {
        System.out.println("Uploading file " + filename + " ...");

        long sendStatus = 0;

        while (sendStatus >= 0) {
            sendStatus = sendFile(filename, sendStatus);
        }
    }

    // Receive a file continuing at a given starting byte
    // Returns the number of total finished bytes
    public static long receiveFile(String filename, long startByte) {
        long finishedBytes = startByte;
        long returnStatus;

        FileOutputStream outFile = null;
        Socket socket = null;

        try {
            socket = new Socket(serverIP, serverPort);
            socket.setSoTimeout(SOCKET_TIME_OUT);

            DataOutputStream dataOutToServer = new DataOutputStream(socket.getOutputStream());
            DataInputStream dataInFromServer = new DataInputStream(socket.getInputStream());
            dataOutToServer.writeUTF("download");
            dataOutToServer.writeUTF(filename);

            if (dataInFromServer.readBoolean()) {
                // Tell server what byte to start downloading
                dataOutToServer.writeLong(startByte);

                // Get the expected length of file
                long fileSizeInBytes = dataInFromServer.readLong();

                // Continue and append to file
                outFile = new FileOutputStream(filename, true);
                byte[] data = new byte[BYTE_TRANSFER_RATE];

                System.out.println("Starting download at " + startByte + " bytes");

                while (finishedBytes < fileSizeInBytes) {
                    int readBytes = dataInFromServer.read(data, 0, data.length);
                    outFile.write(data, 0, readBytes);

                    finishedBytes += readBytes;

                    // Update stats
                    double progress = ((double) finishedBytes / (double) fileSizeInBytes) * 100;
                    System.out.println("Receiving " + filename + ": " + String.format("%.12f", progress) + "% ...");
                }

                // Download complete
                System.out.println("Received " + filename + ", download complete!");
                returnStatus = -2; // Means don't restart the download
            } else {
                // Stop if the file in server does not exist
                System.out.println("File does not exist, download rejected!");
                returnStatus = -1; // Means don't restart the download
            }
        } catch (Exception e) {
            System.out.println("Error receiveFile(): " + e.getMessage());

            // It means restart the download on error, and continue on this byte when restarting
            returnStatus = finishedBytes;
        } finally {
            // Close resources
            try {
                if (outFile != null) {
                    outFile.close();
                }
            } catch (Exception e) {
            }

            try {
                if (socket != null) {
                    socket.close();
                }
            } catch (Exception e) {
            }
        }

        return returnStatus;
    }

    // Download a file from server, keep dowloading even with
    // intermittent network connection
    public static void receiveFile(String filename) throws Exception {
        // If a local file exists, then delete it
        File file = new File(filename);

        if (file.exists()) {
            System.out.println("A file with the same name in the directory exists, it was deleted.");
            file.delete();
        }

        System.out.println("Downloading file " + filename + " ...");
        long receiveStatus = 0;

        while (receiveStatus >= 0) {
            receiveStatus = receiveFile(filename, receiveStatus);
        }
    }

    // Entry point of client
    public static void main(String[] args) throws Exception {
        // Test connection
        readServerDetails();

        // Wait for client commands
        System.out.println("To upload a file to server:");
        System.out.println("    > upload <filename>");
        System.out.println("To download a file from server:");
        System.out.println("    > download <filename>");
        System.out.println("To exit:");
        System.out.println("   > exit");

        try {
            while (true) {
                System.out.print("server@" + serverIP + ":" + serverPort + "> ");
                Scanner scanner = new Scanner(in.nextLine());
                String command = scanner.next();

                if (command.equalsIgnoreCase("exit")) {
                    break;
                }

                if (command.equalsIgnoreCase("upload") && scanner.hasNext()) {
                    sendFile(scanner.next());
                } else if (command.equalsIgnoreCase("download") && scanner.hasNext()) {
                    receiveFile(scanner.next());
                } else {
                    System.out.println("Error: Invalid command.");
                }
            }
        } catch (Exception e) {
            System.out.println("Error main(): " + e.getMessage());
        }
    }
}
