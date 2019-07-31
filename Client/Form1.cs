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
using System.Threading;
using System.Resources;
using System.IO;
using Newtonsoft.Json;

namespace Client
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        //static Socket server;
        static Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        bool Check = true;

        Thread Thread1;

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                //Обновляем данные
                Properties.Settings.Default.Address = textBox3.Text;
                Properties.Settings.Default.Port = textBox4.Text;
                Properties.Settings.Default.Nick = textBox5.Text;
                Properties.Settings.Default.Save();

                //Соединяем сокет с удаленной точкой
                server.Connect(IPAddress.Parse(textBox3.Text), Convert.ToInt32(textBox4.Text));
                //server.Connect(ipEndPoint);

                Thread1 = new Thread(delegate () 
                {
                    ReceiveMesssage();
                });

                Thread1.Start();


                textBox3.Enabled = false;
                textBox4.Enabled = false;
                textBox5.Enabled = false;

                Send("Подключиться", textBox5.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Send("Сообщение", textBox2.Text + "\r\n");
            textBox2.Text = null;
        }

        static void Send(string commanda, string messageString)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(messageString);

            Packet packet = new Packet();
            packet.commanda = commanda;
            packet.data = messageBytes;

            string packetString = JsonConvert.SerializeObject(packet);
            byte[] packetBytes = Encoding.UTF8.GetBytes(packetString);

            try
            {
                server.Send(packetBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        static void Send(string commanda)
        {
            Packet packet = new Packet();
            packet.commanda = commanda;

            string packetString = JsonConvert.SerializeObject(packet);
            byte[] packetBytes = Encoding.UTF8.GetBytes(packetString);

            try
            {
                server.Send(packetBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void ReceiveMesssage()
        {
            try
            {
                while (true)
                {
                    byte[] ping = new byte[1] { 0 }; //Синхронизация
                    string message;

                    // Получаем ответ от сервера
                    Packet packet = GetPacket();

                    switch (packet.commanda)
                    {
                        case "Сообщение":
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                message = Encoding.UTF8.GetString(packet.data);
                                message = JsonConvert.DeserializeObject<string>(message);

                                textBox1.Text += message;
                                textBox1.SelectionStart = textBox1.TextLength;
                                textBox1.ScrollToCaret();
                                //Всплывающие уведомления
                                if (this.WindowState == FormWindowState.Minimized)
                                {
                                    notifyIcon1.Visible = true;
                                    notifyIcon1.Icon = SystemIcons.Application;
                                    int max = message.Length;
                                    int IndexSkobka = message.IndexOf("]");
                                    int IndexDvoeto4ie = message.IndexOf("]") + message.Substring(IndexSkobka, max - IndexSkobka).IndexOf(":");
                                    int DlinaMessage = max - IndexDvoeto4ie;
                                    int DlinaNickName = max - (IndexSkobka + DlinaMessage + 3);
                                    string NickName = message.Substring(IndexSkobka + 2, DlinaNickName);
                                    string messageText = message.Substring(IndexDvoeto4ie + 2, DlinaMessage - 4);
                                    notifyIcon1.ShowBalloonTip(10 * 1000,
                                        NickName,
                                        messageText,
                                        System.Windows.Forms.ToolTipIcon.Info);
                                }
                            });
                            break;
                        case "Логины":
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                listBox1.Items.Clear();
                            });

                            message = Encoding.UTF8.GetString(packet.data);
                            string[] loginsName = JsonConvert.DeserializeObject<string[]>(message);

                            for (int i = 0; i < loginsName.Length; i++)
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    listBox1.Items.Add(loginsName[i]);
                                });
                            }

                            Send("Синхронизация");
                            //server.Send(ping);
                            break;
                        case "Файлы":
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                dataGridView1.Rows.Clear();
                            });

                            message = Encoding.UTF8.GetString(packet.data);
                            FileInfoKratko[] fileInfos = JsonConvert.DeserializeObject<FileInfoKratko[]>(message);

                            for (int i = 0; i < fileInfos.Length; i++)
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    dataGridView1.Rows.Add();
                                    dataGridView1.Rows[i].Cells[0].Value = fileInfos[i].name;
                                    dataGridView1.Rows[i].Cells[1].Value = fileInfos[i].size;
                                });
                            }
                            break;
                        case "Загрузить":
                            string fileName = Encoding.UTF8.GetString(packet.data);
                            Send("Синхронизация");

                            FileStream stream = new FileStream(Directory.GetCurrentDirectory() + "\\" + fileName, FileMode.Create, FileAccess.Write);
                            BinaryWriter f = new BinaryWriter(stream);
                            byte[] buffer = new byte[8192]; //Буфер для файла
                            byte[] bFSize = new byte[512]; //Размер файла

                            int bytesiRec = server.Receive(bFSize); //Принимаем размер
                            Send("Синхронизация");
                            int fSize = Convert.ToInt32(Encoding.UTF8.GetString(bFSize, 0, bytesiRec));

                            int processed = 0; //Байт принято
                            while (processed < fSize) //Принимаем файл
                            {
                                if ((fSize - processed) < 8192)
                                {
                                    int bytesi = (fSize - processed);
                                    byte[] buf = new byte[bytesi];
                                    bytesi = server.Receive(buf);
                                    f.Write(buf, 0, bytesi);
                                }
                                else
                                {
                                    int bytesi = server.Receive(buffer);
                                    f.Write(buffer, 0, bytesi);
                                }
                                Send("Синхронизация");
                                processed += 8192;
                            }
                            f.Close();
                            stream.Close();
                            MessageBox.Show("Файл загружен\r\nName: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[0].Value +
                                "\r\nSize: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[1].Value + " байт", "Загрузка файла", MessageBoxButtons.OK);
                            if (MessageBox.Show("Открыть папку с файлом?\r\nName: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[0].Value +
                                "\r\nSize: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[1].Value + " байт", "Загрузка файла", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            {
                                System.Diagnostics.Process.Start(Directory.GetCurrentDirectory());
                            }
                            break;
                        case "Синхронизация":
                            //Тупо синхронизация
                            break;
                        default:
                            break;
                    }

                    /*
                    switch (data)
                    {
                        case "!история\r\n":
                            break;
                        case "!отключиться":
                            break;
                        default:
                            //Обновление списка ников
                            if ((data.Length > 7) && (data.Substring(0, 7) == "!логины"))
                            {
                                this.Invoke((MethodInvoker)delegate ()
                                {
                                    listBox1.Items.Clear();
                                    dataGridView1.Rows.Clear();
                                });
                                //Ники
                                int pred = data.IndexOf("|");
                                for (int i = pred + 1; i < data.Length; i++)
                                {
                                    if (data.Substring(i, 1) == "|")
                                    {
                                        this.Invoke((MethodInvoker)delegate ()
                                        {
                                            listBox1.Items.Add(data.Substring(pred + 1, data.Length - (data.Length - i) - pred - 1));
                                        });
                                        pred = i;
                                    }
                                }
                                server.Send(ping);
                                bytesRec = server.Receive(bytes);
                                data = Encoding.UTF8.GetString(bytes, 0, bytesRec); //Имена файлов
                                pred = data.IndexOf("|");
                                for (int i = pred + 1; i < data.Length; i++)
                                {
                                    if (data.Substring(i, 1) == "|")
                                    {
                                        this.Invoke((MethodInvoker)delegate ()
                                        {
                                            if (Check)
                                            {
                                                dataGridView1.Rows.Add();
                                                dataGridView1.Rows[dataGridView1.Rows.Count - 1].Cells[0].Value = data.Substring(pred + 1, data.Length - (data.Length - i) - pred - 1);
                                                Check = false;
                                            }
                                            else
                                            {
                                                dataGridView1.Rows[dataGridView1.Rows.Count - 1].Cells[1].Value = data.Substring(pred + 1, data.Length - (data.Length - i) - pred - 1);
                                                Check = true;
                                            }
                                        });
                                        pred = i;
                                    }
                                }
                            }
                            else
                            {
                                if ((data.Length > 10) && (data.Substring(0, 10) == "!загрузить"))
                                {
                                    string fileName = data.Substring(10, data.Length - 10);
                                    server.Send(ping); // 1

                                    FileStream stream = new FileStream(Directory.GetCurrentDirectory() + "\\" + fileName, FileMode.Create, FileAccess.Write);
                                    BinaryWriter f = new BinaryWriter(stream);
                                    byte[] buffer = new byte[8192]; //Буфер для файла
                                    byte[] bFSize = new byte[512]; //Размер файла

                                    int bytesiRec = server.Receive(bFSize); //Принимаем размер
                                    server.Send(ping); // 2
                                    int fSize = Convert.ToInt32(Encoding.UTF8.GetString(bFSize, 0, bytesiRec));

                                    int processed = 0; //Байт принято
                                    while (processed < fSize) //Принимаем файл
                                    {
                                        if ((fSize - processed) < 8192)
                                        {
                                            int bytesi = (fSize - processed);
                                            byte[] buf = new byte[bytesi];
                                            bytesi = server.Receive(buf);
                                            f.Write(buf, 0, bytesi);
                                        }
                                        else
                                        {
                                            int bytesi = server.Receive(buffer);
                                            f.Write(buffer, 0, bytesi);
                                        }
                                        server.Send(ping); // 3
                                        processed += 8192;
                                    }
                                    f.Close();
                                    stream.Close();
                                    MessageBox.Show("Файл загружен\r\nName: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[0].Value + 
                                        "\r\nSize: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[1].Value + " байт", "Загрузка файла", MessageBoxButtons.OK);
                                    if (MessageBox.Show("Открыть папку с файлом?\r\nName: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[0].Value +
                                        "\r\nSize: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[1].Value + " байт", "Загрузка файла", MessageBoxButtons.YesNo) == DialogResult.Yes)
                                    {
                                        System.Diagnostics.Process.Start(Directory.GetCurrentDirectory());
                                    }
                                }
                                else
                                {
                                    //Вывод сообщений
                                    this.Invoke((MethodInvoker)delegate ()
                                   {
                                       string message = Encoding.UTF8.GetString(bytes, 0, bytesRec);

                                       textBox1.Text += message;
                                       textBox1.SelectionStart = textBox1.TextLength;
                                       textBox1.ScrollToCaret();
                                       //Всплывающие уведомления
                                       if (this.WindowState == FormWindowState.Minimized)
                                       {
                                           notifyIcon1.Visible = true;
                                           notifyIcon1.Icon = SystemIcons.Application;
                                           int max = message.Length;
                                           int IndexSkobka = message.IndexOf("]");
                                           int IndexDvoeto4ie = message.IndexOf("]") + message.Substring(IndexSkobka, max - IndexSkobka).IndexOf(":");
                                           int DlinaMessage = max - IndexDvoeto4ie;
                                           int DlinaNickName = max - (IndexSkobka + DlinaMessage + 3);
                                           string NickName = message.Substring(IndexSkobka + 2, DlinaNickName);
                                           string messageText = message.Substring(IndexDvoeto4ie + 2, DlinaMessage - 4);
                                           notifyIcon1.ShowBalloonTip(10 * 1000,
                                               NickName,
                                               messageText,
                                               System.Windows.Forms.ToolTipIcon.Info);
                                       }
                                   });
                                }
                            }
                            break;
                    }
                    */
                }
                //textBox1.Text += "\nОтвет \r\n" + 
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
                server.Close();
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            textBox3.Text = Properties.Settings.Default.Address;
            textBox4.Text = Properties.Settings.Default.Port;
            textBox5.Text = Properties.Settings.Default.Nick;

            Size sizeForm = Size;
            sizeForm.Height += 100;
            Size = sizeForm;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                Send("Отключиться");
                server.Disconnect(false);
                Environment.Exit(1);
            }
            catch
            {

            }
        }

        private void textBox2_Up(object sender, KeyEventArgs e)
        {
            if (!e.Shift && e.KeyCode == Keys.Enter)
            {
                Send("Сообщение", textBox2.Text);
                textBox2.Text = null;
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (textBox2.Text == "")
                {
                    textBox2.Text += " " + listBox1.Items[listBox1.SelectedIndex] + ", ";
                }
                else
                {
                    textBox2.Text += " " + listBox1.Items[listBox1.SelectedIndex] + " ";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        static Packet GetPacket()
        {
            byte[] bytes = new byte[1024];
            int bytesRec = server.Receive(bytes);
            string data = Encoding.UTF8.GetString(bytes, 0, bytesRec);
            Packet packet = JsonConvert.DeserializeObject<Packet>(data);

            return packet;
        }

        static bool Sinhronizatiya()
        {
            if (GetPacket().commanda == "Синхронизация")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void button2_Click(object sender, EventArgs e)  
        {
            try
            {
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    string fileName = openFileDialog1.FileName;

                    //byte[] ping = new byte[1] { 0 }; //Синхронизация
                    Send("Выгрузить", Path.GetFileName(fileName));

                    if (Sinhronizatiya())
                    {
                        return;
                    }
                    //server.Receive(ping); // 1

                    FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                    BinaryReader f = new BinaryReader(stream);
                    byte[] buffer = new byte[8192]; //Буфер для файла
                    int bytesi = 8192;
                    int fSize = Convert.ToInt32(stream.Length);

                    byte[] bFSize = Encoding.UTF8.GetBytes(Convert.ToString(fSize)); //Размер файла
                    server.Send(bFSize); //Передаем размер

                    if (Sinhronizatiya())
                    {
                        return;
                    }
                    //server.Receive(ping); // 2

                    int processed = 0; //Байт передано
                    while (processed < fSize) //Передаем файл
                    {
                        if ((fSize - processed) < 8192)
                        {
                            bytesi = Convert.ToInt32(fSize - processed);
                            byte[] buf = new byte[bytesi];
                            f.Read(buf, 0, bytesi);
                            server.Send(buf);
                        }
                        else
                        {
                            f.Read(buffer, 0, bytesi);
                            server.Send(buffer);
                        }

                        if (Sinhronizatiya())
                        {
                            return;
                        }
                        //server.Receive(ping); // 3
                                              /*
                                              if ((fSize / 100) * progressBar1.Value < processed)
                                              {
                                                  progressBar1.Value++;
                                              }*/
                        processed += 8192;
                    }
                    f.Close();
                    stream.Close();
                    //progressBar1.Value = 100;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                string commanda = listBox2.Items[listBox2.SelectedIndex].ToString();
                Send(commanda);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void dataGridView1_DoubleClick(object sender, EventArgs e)  //Переписать
        {
            try
            {
                if (MessageBox.Show("Вы точно хотите загрузить файл?\r\nИмя: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[0].Value + 
                    "\r\nSize: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[1].Value + " байт", "Загрузка файла", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Send("Загрузить", dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[0].Value.ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }

        }
    }
}