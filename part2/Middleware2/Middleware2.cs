using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;

public struct TotalOrderMessage
{
    public string message { get; set; }
    public int senderId { get; set; }
    public int senderMsgNo { get; set; }
    public int timeStamp { get; set; }
    public bool deliverable { get; set; }

    public TotalOrderMessage(string data, int sid, int no, int ts)
        : this()
    {
        this.message = data;
        this.senderId = sid;
        this.senderMsgNo = no;
        this.timeStamp = ts;
        this.deliverable = false;
    }
}
public class Middleware2 : Form
{
    const int myPort = 1083;
    const int myID = myPort - 1081;
    int MsgCounter, clock, priority = 0;
    List<TotalOrderMessage> holdingQ = new List<TotalOrderMessage>();
    List<TotalOrderMessage> deliveryQ = new List<TotalOrderMessage>();
    //the dictionary store priority data, key = {senderid,senderMsgNo} value = 5 priorities
    Dictionary<int, List<int>> proposedInfo = new Dictionary<int, List<int>>();
    public Middleware2()
    {
        InitializeComponent();

    }
    public List<TotalOrderMessage> sortHoldingQueue(List<TotalOrderMessage> inputQ)
    {
        List<TotalOrderMessage> resultHoldingQ = new List<TotalOrderMessage>();
        if (inputQ.Count > 0)
        {
            inputQ.Sort((a, b) =>
            {
                int TSresult = a.timeStamp.CompareTo(b.timeStamp);  //compare timestamp first
                int SenderRes = TSresult == 0 ? a.senderId.CompareTo(b.senderId) : TSresult;    //compare senderId second
                int msgNoRes = SenderRes == 0 ? a.senderMsgNo.CompareTo(b.senderMsgNo) : SenderRes; //finally compare message number
                return msgNoRes;
            });
            inputQ.ForEach(x => resultHoldingQ.Add(x));
        }
        return resultHoldingQ;
    }
    private bool SetMessagePriorityInQueue(int sender, int senderNo, int priority)
    {
        bool succeed = false;
        int index = -1;
        int counter = -1;
        foreach (TotalOrderMessage x in holdingQ)
        {
            counter++;
            if (x.senderId == sender && x.senderMsgNo == senderNo)
            {
                index = counter;

                break;
            }
        }
        if (index != -1)
        {
            TotalOrderMessage tom = holdingQ[index];
            tom.timeStamp = priority;
            tom.deliverable = true;
            string originalMsg = tom.message;
            string newMsg = originalMsg.Substring(0, originalMsg.IndexOf('['));
            newMsg += priority + "]<EOM>\n";
            holdingQ[index] = tom;
            succeed = true;
        }
        return succeed;
    }

    private void Send_Click(object sender, EventArgs e)
    {
        SendMessage();
    }

    public void SentBoxAppend(string msg)
    {
        SentBox.Items.Add(msg);
    }
    public void ReceivedBoxAppend(string msg)
    {
        ReceivedBox.Items.Add(msg);
    }
    public void ReadyBoxAppend(TotalOrderMessage tom)
    {
        ReadyBox.Items.Add(tom.message);
    }
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
        listener.Start(88);

        try
        {
            string data = null;

            // Start listening for connections.
            while (true)
            {
                //Console.WriteLine("Waiting for a connection...");

                // Program is suspended while waiting for an incoming connection.
                TcpClient tcpClient = await listener.AcceptTcpClientAsync();

                //Console.WriteLine("connectted");
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
                //***** the data is received, select the type of the data.
                char preamble = data[0];
                switch (preamble)
                {
                    case 'M':
                        // a typical message is like: "Msg #3 from Middleware3:[7]<EOM>\n"

                        // retrive sender id and such
                        string keyword = "Middleware";
                        int senderPositionInMsg = data.IndexOf(keyword) + keyword.Length;
                        int columPosition = data.IndexOf(":");
                        string senderStr = data.Substring(senderPositionInMsg, columPosition - senderPositionInMsg);


                        int AtPosition = data.IndexOf("[");
                        int EndPosition = data.IndexOf("]");
                        int hashPosition = data.IndexOf("#");
                        int fromPosition = data.IndexOf("from");
                        string restOfString = data.Substring(hashPosition + 1, fromPosition - hashPosition - 1);

                        string MsgStamp = data.Substring(AtPosition + 1, EndPosition - AtPosition - 1);
                        int sender = Int32.Parse(senderStr);    //the sender id
                        int msgTS = Int32.Parse(MsgStamp);
                        int senderMsgNo = Int32.Parse(restOfString);    //the no of msg from a particular sender

                        priority = Math.Max(priority + 1, msgTS);
                        TotalOrderMessage currentMsg = new TotalOrderMessage(data, sender, senderMsgNo, priority);
                        holdingQ.Add(currentMsg);
                        ReceivedBoxAppend(data);
                        //*** send proposed message
                        SendProposedMessage(sender, senderMsgNo, priority);
                        break;

                    //msessage looks like: Proposed receiver sender msgno priority <EOM>
                    case 'P':
                        string[] tokensP = data.Split('.');

                        if (Int32.Parse(tokensP[2]) == myID)
                        {
                            //ReceivedBoxAppend(data);
                            //int receiver = Int32.Parse(tokens[1]);
                            int thisMsgNo = Int32.Parse(tokensP[3]);
                            int thisPrio = Int32.Parse(tokensP[4]);
                            proposedInfo[thisMsgNo].Add(thisPrio);
                            bool porposedFinished = false;
                            if (proposedInfo[thisMsgNo].Count == 5)
                            {
                                proposedInfo[thisMsgNo].Sort();
                                int maxPriority = proposedInfo[thisMsgNo].Last();
                                //send final
                                SendFinalMessage(thisMsgNo, maxPriority);
                                clock = Math.Max(clock, maxPriority);
                                porposedFinished = true;
                            }
                            if (porposedFinished)
                            {
                                proposedInfo.Remove(thisMsgNo);
                            }
                        }
                        break;
                    //msessage looks like: final sender msgno priority <EOM>
                    case 'F':
                        //ReceivedBoxAppend(data);
                        string[] tokensF = data.Split('.');
                        int finalSender = Int32.Parse(tokensF[1]);
                        int finalMsgNo = Int32.Parse(tokensF[2]);
                        int finalPrio = Int32.Parse(tokensF[3]);

                        //TotalOrderMessage tom = holdingQ.Find(x => ());
                        bool succeed = SetMessagePriorityInQueue(finalSender, finalMsgNo, finalPrio);
                        if (!succeed)
                        {
                            Console.WriteLine("Error, cant find a final message in queue");
                            return;
                        }
                        holdingQ = sortHoldingQueue(holdingQ);
                        //if ((holdingQ.First().senderId != finalSender || holdingQ.First().senderMsgNo != finalMsgNo) && holdingQ.First().deliverable == true)
                        //    Console.WriteLine("assertion failed");

                        /*if (holdingQ.First().senderId == finalSender && holdingQ.First().senderMsgNo == finalMsgNo)
                        {
                            TotalOrderMessage tommy = holdingQ.First();
                            ReadyBoxAppend(tommy);
                            deliveryQ.Add(tommy);
                            holdingQ.Remove(tommy);
                            clock = Math.Max(tommy.timeStamp, clock) + 1;
                            while (holdingQ.Count > 0 && holdingQ.First().deliverable == true)
                            {
                                TotalOrderMessage jerry = holdingQ.First();
                                ReadyBoxAppend(jerry);
                                deliveryQ.Add(jerry);
                                holdingQ.Remove(jerry);
                                clock = Math.Max(jerry.timeStamp, clock) + 1;
                            }
                        }*/
                        while (holdingQ.Count > 0 && holdingQ.First().deliverable == true)
                        {
                            TotalOrderMessage jerry = holdingQ.First();
                            ReadyBoxAppend(jerry);
                            deliveryQ.Add(jerry);
                            holdingQ.Remove(jerry);
                            clock = Math.Max(jerry.timeStamp, clock) + 1;
                        }
                        break;
                }
                string display = "[";
                display += clock;
                textBox1.Text = display + "]";

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



    }
    public void SendMessage()
    {
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
                MsgCounter++;
                clock++;

                // I removed the first space in this message
                string message = "Msg #" + MsgCounter + " from Middleware" + myID + ":[" + clock + "]<EOM>\n";
                byte[] msg = Encoding.ASCII.GetBytes(message);


                // Send the data to the network.
                int bytesSent = sendSocket.Send(msg);

                sendSocket.Shutdown(SocketShutdown.Both);
                sendSocket.Close();

                SentBoxAppend(message);
                proposedInfo.Add(MsgCounter, new List<int>());

                //Console.WriteLine("Press ENTER to terminate ...");
                //Console.ReadLine();
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

    public void SendProposedMessage(int target, int messageNo, int myPriority)
    {
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

                // I removed the first space in this message
                //message looks like: Proposed receiver sender msgno priority <EOM>
                string message = "Proposed." + myID + "." + target + "." + messageNo + "." + myPriority + ".<EOM>\n";
                byte[] msg = Encoding.ASCII.GetBytes(message);

                //SentBoxAppend(message);
                // Send the data to the network.
                int bytesSent = sendSocket.Send(msg);

                sendSocket.Shutdown(SocketShutdown.Both);
                sendSocket.Close();


                //Console.WriteLine("Press ENTER to terminate ...");
                //Console.ReadLine();
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
    public void SendFinalMessage(int messageNo, int myPriority)
    {
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

                // I removed the first space in this message
                //message looks like: Final.1.2.10.<EOM>
                string message = "Final." + myID + "." + messageNo + "." + myPriority + ".<EOM>\n";
                byte[] msg = Encoding.ASCII.GetBytes(message);

                //SentBoxAppend(message);
                // Send the data to the network.
                int bytesSent = sendSocket.Send(msg);

                sendSocket.Shutdown(SocketShutdown.Both);
                sendSocket.Close();


                //Console.WriteLine("Press ENTER to terminate ...");
                //Console.ReadLine();
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

    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
            this.Send = new System.Windows.Forms.Button();
            this.SentBox = new System.Windows.Forms.ListBox();
            this.ReceivedBox = new System.Windows.Forms.ListBox();
            this.ReadyBox = new System.Windows.Forms.ListBox();
            this.Sent = new System.Windows.Forms.TextBox();
            this.Received = new System.Windows.Forms.TextBox();
            this.Ready = new System.Windows.Forms.TextBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // Send
            // 
            this.Send.Location = new System.Drawing.Point(12, 12);
            this.Send.Name = "Send";
            this.Send.Size = new System.Drawing.Size(90, 29);
            this.Send.TabIndex = 0;
            this.Send.Text = "Send";
            this.Send.UseVisualStyleBackColor = true;
            this.Send.Click += new System.EventHandler(this.Send_Click);
            // 
            // SentBox
            // 
            this.SentBox.FormattingEnabled = true;
            this.SentBox.ItemHeight = 20;
            this.SentBox.Location = new System.Drawing.Point(12, 95);
            this.SentBox.Name = "SentBox";
            this.SentBox.Size = new System.Drawing.Size(298, 604);
            this.SentBox.TabIndex = 1;
            // 
            // ReceivedBox
            // 
            this.ReceivedBox.FormattingEnabled = true;
            this.ReceivedBox.ItemHeight = 20;
            this.ReceivedBox.Location = new System.Drawing.Point(334, 95);
            this.ReceivedBox.Name = "ReceivedBox";
            this.ReceivedBox.Size = new System.Drawing.Size(298, 604);
            this.ReceivedBox.TabIndex = 2;
            // 
            // ReadyBox
            // 
            this.ReadyBox.FormattingEnabled = true;
            this.ReadyBox.ItemHeight = 20;
            this.ReadyBox.Location = new System.Drawing.Point(658, 95);
            this.ReadyBox.Name = "ReadyBox";
            this.ReadyBox.Size = new System.Drawing.Size(298, 604);
            this.ReadyBox.TabIndex = 3;
            // 
            // Sent
            // 
            this.Sent.Location = new System.Drawing.Point(12, 55);
            this.Sent.Name = "Sent";
            this.Sent.Size = new System.Drawing.Size(100, 26);
            this.Sent.TabIndex = 4;
            this.Sent.Text = "Sent";
            // 
            // Received
            // 
            this.Received.Location = new System.Drawing.Point(334, 55);
            this.Received.Name = "Received";
            this.Received.Size = new System.Drawing.Size(100, 26);
            this.Received.TabIndex = 5;
            this.Received.Text = "Received";
            // 
            // Ready
            // 
            this.Ready.Location = new System.Drawing.Point(658, 55);
            this.Ready.Name = "Ready";
            this.Ready.Size = new System.Drawing.Size(100, 26);
            this.Ready.TabIndex = 6;
            this.Ready.Text = "Ready";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(424, 10);
            this.textBox1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(298, 26);
            this.textBox1.TabIndex = 7;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(371, 13);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(45, 20);
            this.label1.TabIndex = 8;
            this.label1.Text = "clock";
            // 
            // Middleware2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(975, 717);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.Ready);
            this.Controls.Add(this.Received);
            this.Controls.Add(this.Sent);
            this.Controls.Add(this.ReadyBox);
            this.Controls.Add(this.ReceivedBox);
            this.Controls.Add(this.SentBox);
            this.Controls.Add(this.Send);
            this.Location = new System.Drawing.Point(1200, 10);
            this.Name = "Middleware2";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Middleware 2";
            this.ResumeLayout(false);
            this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.Button Send;
    public System.Windows.Forms.ListBox SentBox;
    private System.Windows.Forms.ListBox ReceivedBox;
    private System.Windows.Forms.ListBox ReadyBox;
    private System.Windows.Forms.TextBox Sent;
    private System.Windows.Forms.TextBox Received;
    private System.Windows.Forms.TextBox Ready;
    public static int Main(String[] args)
    {
        Middleware2 m = new Middleware2();
        Application.EnableVisualStyles();
        //Application.SetCompatibleTextRenderingDefault(false);
        m.DoWork();
        Application.Run(m);


        return 0;
    }

    private TextBox textBox1;
    private Label label1;

}

