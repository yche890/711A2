using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

public class Middleware
{
    const int myPort = 1085;

    // This method sets up a socket for receiving messages from the Network
    private async void ReceiveMulticast()
    {
        // Data buffer for incoming data.
        byte[] bytes = new Byte[1024];

        // Determine the IP address of localhost
        IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress ipAddress = null;
        foreach (IPAddress ip in ipHostInfo.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                ipAddress = ip;
                break;
            }
        }

        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, myPort);

        // Create a TCP/IP socket for receiving message from the Network.
        TcpListener listener = new TcpListener(localEndPoint);
        listener.Start(10);

        try
        {
            string data = null;

            // Start listening for connections.
            while (true)
            {
                Console.WriteLine("Waiting for a connection...");

                // Program is suspended while waiting for an incoming connection.
                TcpClient tcpClient = await listener.AcceptTcpClientAsync();

                Console.WriteLine("connectted");
                data = null;

                // Receive one message from the network
                while (true)
                {
                    bytes = new byte[1024];
                    NetworkStream readStream = tcpClient.GetStream();
                    int bytesRec = await readStream.ReadAsync(bytes, 0, 1024);
                    data += Encoding.ASCII.GetString(bytes, 0, bytesRec);

                    // All messages ends with "<EOM>"
                    // Check whether a complete message has been received
                    if (data.IndexOf("<EOM>") > -1)
                    {
                        break;
                    }
                }
                Console.WriteLine("msg received:    {0}", data);
            }

        }
        catch (Exception ee)
        {
            Console.WriteLine(ee.ToString());
        }
    }

    // This method first sets up a task for receiving messages from the Network.
    // Then, it sends a multicast message to the Netwrok.
    public void DoWork()
    {
        // Sets up a task for receiving messages from the Network.
        ReceiveMulticast();

        Console.WriteLine("Press ENTER to continue ...");
        Console.ReadLine();

        // Send a multicast message to the Network
        try
        {
            // Find the IP address of localhost
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = null;
            foreach (IPAddress ip in ipHostInfo.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = ip;
                    break;
                }
            }
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, 1081);
            Socket sendSocket;
            try
            {
                // Create a TCP/IP  socket.
                sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the Network 
                sendSocket.Connect(remoteEP);

                // Generate and encode the multicast message into a byte array.
                byte[] msg = Encoding.ASCII.GetBytes("From "+myPort + ": This is a test<EOM>\n");

                // Send the data to the network.
                int bytesSent = sendSocket.Send(msg);

                sendSocket.Shutdown(SocketShutdown.Both);
                sendSocket.Close();

                Console.WriteLine("Press ENTER to terminate ...");
                Console.ReadLine();
            }
            catch (ArgumentNullException ane)
            {
                Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
            }
            catch (SocketException se)
            {
                Console.WriteLine("SocketException : {0}", se.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected exception : {0}", e.ToString());
            }

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    public static int Main(String[] args)
    {
        Middleware m = new Middleware();
        m.DoWork();
        return 0;
    }
}