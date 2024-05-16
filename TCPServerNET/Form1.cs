using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace TCPServerNET
{
    public partial class Form1 : Form
    {
        private List<IPAddress> ipList = new List<IPAddress>();
        private List<TcpClient> tcplists = new List<TcpClient>();
        private TcpListener tcpListener;
        private Thread listenThread;
        private UdpClient udpClient;
        private bool isConnected = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            cmbProtocol.Items.AddRange(new string[] { "UDP", "TCP Server", "TCP Client" });
            cmbProtocol.SelectedIndex = 2;  // Default to TCP Client

            ipList = GetIPAddresses();
            if (ipList.Count > 0)
            {
                cmbIpAddr.Items.AddRange(ipList.ToArray());
                cmbIpAddr.SelectedIndex = 0;
            }
            txtIpPort.Text = "25565";
            UpdateConnectionControls(false);
        }

        private List<IPAddress> GetIPAddresses()
        {
            var addresses = new List<IPAddress>();
            foreach (NetworkInterface intf in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (UnicastIPAddressInformation ip in intf.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        addresses.Add(ip.Address);
                    }
                }
            }
            return addresses;
        }

        private void connectBtn_Click(object sender, EventArgs e)
        {
            if (!isConnected)
            {
                StartConnection();
            }
            else
            {
                StopConnection();
            }
        }

        private void StartConnection()
        {
            int port = int.Parse(txtIpPort.Text);
            switch (cmbProtocol.SelectedItem.ToString())
            {
                case "TCP Server":
                    tcpListener = new TcpListener(IPAddress.Parse(cmbIpAddr.Text), port);
                    tcpListener.Start();
                    listenThread = new Thread(TCPReceiveData);
                    listenThread.IsBackground = true;
                    listenThread.Start();
                    break;
                case "TCP Client":
                    var client = new TcpClient();
                    client.Connect(IPAddress.Parse(cmbIpAddr.Text), port);
                    tcplists.Add(client);
                    break;
                case "UDP":
                    udpClient = new UdpClient(port);
                    listenThread = new Thread(UdpReceiveData);
                    listenThread.IsBackground = true;
                    listenThread.Start();
                    break;
            }
            isConnected = true;
            UpdateConnectionControls(true);
        }


        private void StopConnection()
        {
            if (tcpListener != null)
            {
                tcpListener.Stop();
                tcpListener = null;
            }
            if (listenThread != null)
            {
                listenThread.Abort();
                listenThread = null;
            }
            foreach (var client in tcplists)
            {
                client.Close();
            }
            tcplists.Clear();
            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
            }
            isConnected = false;
            UpdateConnectionControls(false);
        }

        private void UpdateConnectionControls(bool isConnected)
        {
            btnConnect.Text = isConnected ? "断开" : "连接";
            cmbIpAddr.Enabled = !isConnected;
            txtIpPort.Enabled = !isConnected;
            cmbProtocol.Enabled = !isConnected;
        }
        private void UdpReceiveData()
        {
            try
            {
                IPEndPoint remoteEP = null;
                while (true)
                {
                    byte[] data = udpClient.Receive(ref remoteEP);
                    string message = Encoding.UTF8.GetString(data);
                    UpdateUI(() =>
                    {
                        rtxRec.SelectionColor = Color.Blue;
                        rtxRec.AppendText("[REC UDP]: " + message + "\n");
                        rtxRec.ScrollToCaret();
                    });
                }
            }
            catch (ThreadAbortException)
            {
                // Handle thread abort for clean exit
            }
            catch (Exception ex)
            {
                MessageBox.Show("接收UDP数据时发生错误：" + ex.Message);
            }
        }
        private void TCPReceiveData()
        {
            while (true)
            {
                if (tcpListener != null && tcpListener.Pending())
                {
                    var client = tcpListener.AcceptTcpClient();
                    tcplists.Add(client);
                    Thread receiveThread = new Thread(() => ProcessReceivedData(client));
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }
            }
        }

        private void ProcessReceivedData(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];  // 可以根据实际情况调整缓冲区大小
            int bytesRead;

            try
            {
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);

                    // 判断是否以十六进制显示
                    string message;
                    if (chkRxHex.Checked)
                    {
                        message = BitConverter.ToString(data).Replace("-", " ");  // 将字节转换为十六进制字符串
                    }
                    else
                    {
                        message = Encoding.UTF8.GetString(data);  // 将字节转换为字符串
                    }

                    string formattedMessage = "[REC] " + message + "\n";

                    // 在UI线程上更新富文本框
                    UpdateUI(() => {
                        rtxRec.SelectionColor = Color.Blue;  // 设置文本颜色
                        rtxRec.AppendText(formattedMessage);
                        rtxRec.ScrollToCaret();  // 滚动到新添加的文本位置
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("接收数据时发生错误：" + ex.Message);
            }
        }




        private void btnSend_Click(object sender, EventArgs e)
        {
            string message = rtxSend.Text.Trim();
            if (string.IsNullOrEmpty(message))
            {
                MessageBox.Show("请输入要发送的数据");
                return;
            }

            byte[] data;
            if (chkTxHex.Checked)
            {
                try
                {
                    data = ConvertHexStringToByteArray(message);
                }
                catch (FormatException fe)
                {
                    MessageBox.Show("输入的十六进制字符串格式不正确：" + fe.Message);
                    return;
                }
            }
            else
            {
                data = Encoding.UTF8.GetBytes(message);
            }

            switch (cmbProtocol.SelectedItem.ToString())
            {
                case "TCP Server":
                case "TCP Client":
                    SendTcpData(data);
                    break;
                case "UDP":
                    SendUdpData(data);
                    break;
            }
        }
        private void SendTcpData(byte[] data)
        {
            foreach (var client in tcplists)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    if (stream.CanWrite)
                    {
                        stream.Write(data, 0, data.Length);
                        UpdateUI(() =>
                        {
                            rtxRec.SelectionColor = Color.Green;
                            rtxRec.AppendText("[SENT TCP]: " + Encoding.UTF8.GetString(data) + "\n");
                            rtxRec.ScrollToCaret();
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("发送TCP数据出错：" + ex.Message);
                }
            }
        }


        private void SendUdpData(byte[] data)
        {
            try
            {
                if (udpClient != null)
                {
                    // Assuming txtRemoteIp and txtRemotePort contain the remote endpoint information
                    IPAddress remoteIp = IPAddress.Parse(txtRemoteIp.Text);
                    int remotePort = int.Parse(txtRemotePort.Text);
                    IPEndPoint remoteEndPoint = new IPEndPoint(remoteIp, remotePort);

                    udpClient.Send(data, data.Length, remoteEndPoint);  // Send data to specified endpoint
                    UpdateUI(() =>
                    {
                        rtxRec.SelectionColor = Color.Green;
                        rtxRec.AppendText("[SENT UDP]: " + Encoding.UTF8.GetString(data) + "\n");
                        rtxRec.ScrollToCaret();
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("发送UDP数据出错：" + ex.Message);
            }
        }

        private void UpdateUI(Action updateAction)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(updateAction);
            }
            else
            {
                updateAction();
            }
        }

        private byte[] ConvertHexStringToByteArray(string hexString)
        {
            try
            {
                int numberChars = hexString.Length;
                byte[] bytes = new byte[numberChars / 2];
                for (int i = 0; i < numberChars; i += 2)
                {
                    bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
                }
                return bytes;
            }
            catch
            {
                MessageBox.Show("非法的 hex string");
                return new byte[] { };
            }
        }

        private void chkTxHex_CheckedChanged(object sender, EventArgs e)
        {
            if (chkTxHex.Checked)
            {
                string text = rtxSend.Text;
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                string hex = BitConverter.ToString(bytes).Replace("-", " ");
                rtxSend.Text = hex;
            }
            else
            {
                string hex = rtxSend.Text;
                byte[] bytes = ConvertHexStringToByteArray(hex);
                string text = Encoding.UTF8.GetString(bytes);
                rtxSend.Text = text;
            }
        }
    }
}
