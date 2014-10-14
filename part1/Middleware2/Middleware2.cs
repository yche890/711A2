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







    public class Middleware2 : Form
    {
        const int myPort = 1083;
        const int myID = myPort - 1081;
        int MsgCounter = 0;
        int[] Vi= new int[5];
        List<string> buffer = new List<string>();
        public Middleware2()
        {
            InitializeComponent();

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
        public void ReadyBoxAppend(string msg)
        {
            ReadyBox.Items.Add(msg);
            string display = "[";
            Vi.ToList().ForEach(x => display += x + ",");
            textBox1.Text = display + "]";
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
                    //Console.WriteLine("msg received:    {0}", data);
                    ReceivedBoxAppend(data);
                    buffer.Add(data);
                    string readyMsg = null;
                    int readySender = -1;
                    foreach (string msgs in buffer)
                    {
                        string keyword = "Middleware";
                        int senderPositionInMsg = msgs.IndexOf(keyword) + keyword.Length;
                        int columPosition = msgs.IndexOf(":");
                        string senderStr = msgs.Substring(senderPositionInMsg, columPosition - senderPositionInMsg);
                        int sender = Int32.Parse(senderStr);    //the sender id

                        int AtPosition = data.IndexOf("[");
                        string MsgStamp = msgs.Substring(AtPosition + 1, data.IndexOf("]") - AtPosition - 1);
                        string[] tokens = MsgStamp.Split(',');
                        if (sender == myID)
                        {
                            //ReadyBoxAppend(msgs);
                            //buffer.Remove(msgs);
                            readyMsg = msgs;
                            readySender = sender;
                        }
                        else
                        {
                            bool CausalOrdered = true;

                            for (int ix = 0; ix < Vi.Length; ix++)
                            {
                                int Vj_value = Int32.Parse(tokens[ix]);
                                if (ix + 1 == sender)   //index is starting from 0
                                {
                                    if (Vj_value != Vi[ix] + 1)
                                    {
                                        CausalOrdered = false;
                                    }

                                }
                                else if (ix + 1 != myID)
                                {
                                    if (Vj_value > Vi[ix])
                                    {
                                        CausalOrdered = false;
                                    }
                                }
                            }
                            if (CausalOrdered)
                            {
                                //ReadyBoxAppend(msgs);
                                //buffer.Remove(msgs);
                                readyMsg = msgs;
                                readySender = sender;
                            }
                        }
                    }
                    if (readyMsg != null && readySender != -1)
                    {
                        if (readySender != myID)
                        {
                            Vi[readySender - 1] += 1;
                        }
                        ReadyBoxAppend(readyMsg);
                        buffer.Remove(readyMsg);
                    }

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

            //Console.WriteLine("Press ENTER to continue ...");
            //Console.ReadLine();

            // Send a multicast message to the Network

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
                    Vi[myID - 1]++;
                    string timestamp = "[";
                    foreach (int i in Vi)
                    {
                        timestamp += i + ",";
                    }
                    timestamp = timestamp.Substring(0, timestamp.Length-1) + "]";
                    string message = "Msg #" + MsgCounter + " from Middleware" + myID + ":" + timestamp + "<EOM>\n";
                    byte[] msg = Encoding.ASCII.GetBytes(message);
                    
                    SentBoxAppend(message);
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
        /// <summary>
        /// Required designer variable.
        /// </summary>
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
            this.label1 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // Send
            // 
            this.Send.Location = new System.Drawing.Point(22, 15);
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
            this.SentBox.Location = new System.Drawing.Point(22, 97);
            this.SentBox.Name = "SentBox";
            this.SentBox.Size = new System.Drawing.Size(298, 604);
            this.SentBox.TabIndex = 1;
            // 
            // ReceivedBox
            // 
            this.ReceivedBox.FormattingEnabled = true;
            this.ReceivedBox.ItemHeight = 20;
            this.ReceivedBox.Location = new System.Drawing.Point(342, 97);
            this.ReceivedBox.Name = "ReceivedBox";
            this.ReceivedBox.Size = new System.Drawing.Size(298, 604);
            this.ReceivedBox.TabIndex = 2;
            // 
            // ReadyBox
            // 
            this.ReadyBox.FormattingEnabled = true;
            this.ReadyBox.ItemHeight = 20;
            this.ReadyBox.Location = new System.Drawing.Point(664, 97);
            this.ReadyBox.Name = "ReadyBox";
            this.ReadyBox.Size = new System.Drawing.Size(298, 604);
            this.ReadyBox.TabIndex = 3;
            // 
            // Sent
            // 
            this.Sent.Location = new System.Drawing.Point(22, 57);
            this.Sent.Name = "Sent";
            this.Sent.Size = new System.Drawing.Size(100, 26);
            this.Sent.TabIndex = 4;
            this.Sent.Text = "Sent";
            // 
            // Received
            // 
            this.Received.Location = new System.Drawing.Point(342, 57);
            this.Received.Name = "Received";
            this.Received.Size = new System.Drawing.Size(100, 26);
            this.Received.TabIndex = 5;
            this.Received.Text = "Received";
            // 
            // Ready
            // 
            this.Ready.Location = new System.Drawing.Point(664, 57);
            this.Ready.Name = "Ready";
            this.Ready.Size = new System.Drawing.Size(100, 26);
            this.Ready.TabIndex = 6;
            this.Ready.Text = "Ready";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(398, 18);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(24, 20);
            this.label1.TabIndex = 10;
            this.label1.Text = "V:";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(433, 13);
            this.textBox1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(298, 26);
            this.textBox1.TabIndex = 9;
            // 
            // Middleware2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(975, 713);
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

        private Label label1;
        private TextBox textBox1;
        
    }
