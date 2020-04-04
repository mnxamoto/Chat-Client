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
using System.Security.Cryptography;

using Client.Ciphers;

namespace Client
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Сокет сервера
        /// </summary>
        static Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        /// <summary>
        /// Отдельный поток для приёма пакетов и ответа на них
        /// </summary>
        Thread Thread1;

        Random random = new Random();

        /// <summary>
        /// Ключ
        /// </summary>
        static byte[] key = new byte[32];

        DateTime dateTime;

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                Connect("Подключиться");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Подключение к серверу
        /// </summary>
        /// <param name="mode"></param>
        private void Connect(string mode)
        {
            //Записываем данные с текстбоксов в кеш программы для сохранения
            Properties.Settings.Default.Address = textBox3.Text;
            Properties.Settings.Default.Port = textBox4.Text;
            Properties.Settings.Default.Nick = textBox5.Text;
            Properties.Settings.Default.Password = textBox6.Text;
            Properties.Settings.Default.Save(); //Сохраняем

            //Соединяем сокет с удаленной точкой (сервером)
            server.Connect(IPAddress.Parse(textBox3.Text), Convert.ToInt32(textBox4.Text));

            //Отдельный поток для приёма входящих пакетов и ответа на них
            Thread1 = new Thread(delegate ()
            {
                ReceiveMesssage();
            });

            //Запускаем этот поток
            Thread1.Start();

            //Выключаем текстбоксы
            textBox3.Enabled = false;
            textBox4.Enabled = false;
            textBox5.Enabled = false;
            textBox6.Enabled = false;

            byte[] PSP = new byte[32];
            //Формируем ПСП
            random.NextBytes(PSP);

            //Формируем ключ с помощью стрибога
            Stribog stribog = new Stribog(Stribog.lengthHash.Length256);
            key = stribog.GetHash(PSP);

            User user = new User();
            user.Name = Properties.Settings.Default.Nick;
            user.Password = Properties.Settings.Default.Password.GetHashCode();
            user.key = PSP;

            //Отправляем информацию о клиенте на сервер
            Send(mode, user);

            //Включаем комбокс со списком шифров
            comboBox1.Enabled = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //Отправить сообщение из текстбокса на сервер
            Send("Сообщение", textBox2.Text + "\r\n");
            textBox2.Text = null;
        }

        /// <summary>
        /// Шифратор
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private Packet Encrypt(string data)
        {
            byte[] result;
            byte[] dataBytes;
            int sizeBlock;

            string cipher = comboBox1.SelectedItem.ToString();

            switch (cipher)
            {
                case "Нет":
                    result = Encoding.GetEncoding(866).GetBytes(data);
                    break;
                case "Кузнечик":
                    Kuznyechik kuznyechik = new Kuznyechik();
                    sizeBlock = 16;

                    while (data.Length % sizeBlock != 0)
                    {
                        data += "┼";
                    }

                    dataBytes = Encoding.GetEncoding(866).GetBytes(data);
                    result = new byte[dataBytes.Length];

                    for (int i = 0; i < dataBytes.Length; i += sizeBlock)
                    {
                        byte[] data8bytes = new byte[sizeBlock];

                        for (int k = 0; k < sizeBlock; k++)
                        {
                            data8bytes[k] = dataBytes[k + i];
                        }

                        byte[] dataE8bytes = kuznyechik.encrypt(data8bytes, key);

                        for (int k = 0; k < sizeBlock; k++)
                        {
                            result[k + i] = dataE8bytes[k];
                        }
                    }
                    break;
                case "Магма":
                    Magma magma = new Magma();
                    sizeBlock = 8;

                    while (data.Length % sizeBlock != 0)
                    {
                        data += "┼";
                    }

                    dataBytes = Encoding.GetEncoding(866).GetBytes(data);
                    result = magma.Encode(dataBytes, key);
                    break;
                case "AES":
                    sizeBlock = 16;

                    Aes aes = Aes.Create();
                    aes.Mode = CipherMode.ECB;
                    aes.KeySize = 256;
                    aes.BlockSize = sizeBlock * 8;
                    aes.Key = key;
                    aes.Padding = PaddingMode.Zeros;

                    ICryptoTransform encryptor = aes.CreateEncryptor();

                    while (data.Length % sizeBlock != 0)
                    {
                        data += "┼";
                    }

                    dataBytes = Encoding.GetEncoding(866).GetBytes(data);
                    result = encryptor.TransformFinalBlock(dataBytes, 0, sizeBlock);
                    break;
                default:
                    result = null;
                    break;
            }

            Packet packet = new Packet();
            packet.data = result;
            packet.cipher = cipher;
            return packet;
        }

        /// <summary>
        /// Дешифратор
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        static string Decrypt(Packet packet)
        {
            string result = "";
            byte[] dataD;
            int sizeBlock;

            switch (packet.cipher)
            {
                case "Нет":
                case null:
                    result = Encoding.GetEncoding(866).GetString(packet.data);
                    break;
                case "Кузнечик":
                    Kuznyechik kuznyechik = new Kuznyechik();
                    sizeBlock = 16;
                    dataD = new byte[packet.data.Length];

                    for (int i = 0; i < packet.data.Length; i += sizeBlock)
                    {
                        byte[] data8bytes = new byte[sizeBlock];

                        for (int k = 0; k < sizeBlock; k++)
                        {
                            data8bytes[k] = packet.data[k + i];
                        }

                        byte[] dataD8bytes = kuznyechik.decrypt(data8bytes, key);

                        for (int k = 0; k < sizeBlock; k++)
                        {
                            dataD[k + i] = dataD8bytes[k];
                        }
                    }

                    result = Encoding.GetEncoding(866).GetString(dataD);
                    result = result.Replace("┼", "");
                    break;
                case "Магма":
                    Magma magma = new Magma();
                    dataD = magma.Decode(packet.data, key);
                    result = Encoding.GetEncoding(866).GetString(dataD);
                    result = result.Replace("┼", "");
                    break;
                case "AES":
                    sizeBlock = 16;

                    Aes aes = Aes.Create();
                    aes.Mode = CipherMode.ECB;
                    aes.KeySize = 256;
                    aes.BlockSize = sizeBlock * 8;
                    aes.Key = key;
                    aes.Padding = PaddingMode.Zeros;

                    ICryptoTransform decryptor = aes.CreateDecryptor();

                    dataD = decryptor.TransformFinalBlock(packet.data, 0, packet.data.Length);
                    result = Encoding.GetEncoding(866).GetString(dataD);
                    result = result.Replace("┼", "");
                    break;
                default:
                    result = null;
                    break;
            }

            return result;
        }

        private byte[] GetEDS(byte[] data)
        {
            byte[] EDS = new byte[data.Length];

            for (int i = 0; i < data.Length / 32; i += 32)
            {
                byte[] iData = data.Skip(i).Take(32).ToArray();

                Stribog stribog = new Stribog(Stribog.lengthHash.Length256);
                byte[] iEDS = stribog.GetHash(iData);

                EDS.Concat(iEDS);
            }

            return EDS;
        }

        /// <summary>
        /// Отправляет данные на сервер
        /// </summary>
        /// <param name="commanda"></param>
        /// <param name="messageObject"></param>
        private void Send(string commanda, object messageObject)
        {
            string messageString = JsonConvert.SerializeObject(messageObject);

            dateTime = DateTime.Now;

            Packet packet = Encrypt(messageString);
            packet.EDS = GetEDS(packet.data);

            addRowInTable("Шифрование", packet);

            packet.commanda = commanda;
            packet.timeSend = DateTime.Now.Ticks;

            string packetString = JsonConvert.SerializeObject(packet);
            byte[] packetBytes = Encoding.GetEncoding(866).GetBytes(packetString);

            dateTime = DateTime.Now; //Начало отправки сообщениея

            try
            {
                server.Send(packetBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Отправляем определённую команду на сервер
        /// </summary>
        /// <param name="commanda"></param>
        private void Send(string commanda)
        {
            Packet packet = new Packet();
            packet.commanda = commanda;

            string packetString = JsonConvert.SerializeObject(packet);
            byte[] packetBytes = Encoding.GetEncoding(866).GetBytes(packetString);

            try
            {
                server.Send(packetBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Функция приёма входящих пакетов и ответа на них
        /// </summary>
        private void ReceiveMesssage()
        {
            try
            {
                //Постоянно слушаем входящий трафик
                while (true)
                {
                    string message;

                    // Получаем пакет от сервера
                    Packet packet = GetPacket();

                    //Проверка ЭЦП
                    if ((packet.EDS != GetEDS(packet.data)) && false)
                    {
                        continue;
                    }

                    //В зависимости от команды выполняется определённое действие
                    switch (packet.commanda)
                    {
                        case "Сообщение":
                            //Invoke - доступ к элементу, находящемуся в другом потоке (сама форма (Form1) находится в основном потоке)
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                addRowInTable("Отправка", packet); //Конец отправки сообщения

                                dateTime = DateTime.Now;

                                //Дешифруем данные из пакета
                                message = Decrypt(packet);

                                addRowInTable("Дешифрование", packet); //Конец дешифрования сообщения

                                //Десериализуем данные в текст
                                message = JsonConvert.DeserializeObject<string>(message);

                                //Выводим текст на форму
                                textBox1.Text += message;
                                textBox1.SelectionStart = textBox1.TextLength;
                                textBox1.ScrollToCaret();

                                //Если окно свёрнуто, показывается всплывающие уведомление с сообщением
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
                                //очиста листбокса
                                listBox1.Items.Clear();
                            });

                            message = Decrypt(packet);
                            string[] loginsName = JsonConvert.DeserializeObject<string[]>(message);

                            //Заполнение листбокс списком логинов (клиентов)
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
                                //Очистка таблицы
                                dataGridView1.Rows.Clear();
                            });

                            message = Decrypt(packet);
                            FileInfoKratko[] fileInfos = JsonConvert.DeserializeObject<FileInfoKratko[]>(message);

                            //Заполнение таблицы информацией о файлах на сервере
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
                            message = Decrypt(packet);
                            string fileName = JsonConvert.DeserializeObject<string>(message);
                            Send("Синхронизация");

                            FileStream stream = new FileStream(Directory.GetCurrentDirectory() + "\\" + fileName, FileMode.Create, FileAccess.Write);
                            BinaryWriter f = new BinaryWriter(stream);
                            byte[] buffer = new byte[8192]; //Буфер для файла
                            byte[] bFSize = new byte[512]; //Размер файла

                            int bytesiRec = server.Receive(bFSize); //Принимаем размер
                            Send("Синхронизация");
                            int fSize = Convert.ToInt32(Encoding.GetEncoding(866).GetString(bFSize, 0, bytesiRec));

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

                            //Показываем мессаджбокс о выполнении загрузки
                            MessageBox.Show("Файл загружен\r\nName: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[0].Value +
                                "\r\nSize: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[1].Value + " байт", "Загрузка файла", MessageBoxButtons.OK);
                            if (MessageBox.Show("Открыть папку с файлом?\r\nName: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[0].Value +
                                "\r\nSize: " + dataGridView1.Rows[dataGridView1.CurrentRow.Index].Cells[1].Value + " байт", "Загрузка файла", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            {
                                System.Diagnostics.Process.Start(Directory.GetCurrentDirectory());
                            }
                            break;
                        case "Синхронизация":
                            
                            break;
                        case "Ошибка":
                            //Дешифруем данные из пакета
                            message = Decrypt(packet);
                            //Десериализуем данные в текст
                            message = JsonConvert.DeserializeObject<string>(message);
                            this.Invoke((MethodInvoker)delegate ()
                            {
                                MessageBox.Show(message, "Ответ от сервера");

                                textBox3.Enabled = true;
                                textBox4.Enabled = true;
                                textBox5.Enabled = true;
                                textBox6.Enabled = true;
                            });
                            Disconnect();
                            break;
                        default:
                            break;
                    }
                }
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
            //Вывод в текст боксы информацию сохранённую в кеше
            textBox3.Text = Properties.Settings.Default.Address;
            textBox4.Text = Properties.Settings.Default.Port;
            textBox5.Text = Properties.Settings.Default.Nick;
            textBox6.Text = Properties.Settings.Default.Password;

            comboBox1.SelectedIndex = 0;
            comboBox1.Enabled = false;

            dataGridView2.Rows.Add(2);
            dataGridView2.Rows[0].Cells[0].Value = "Кол. символов";
            dataGridView2.Rows[1].Cells[0].Value = "Кол. байт";

            dataGridView3.Rows.Add(3);
            dataGridView3.Rows[0].Cells[0].Value = "Мин";
            dataGridView3.Rows[1].Cells[0].Value = "Макс";
            dataGridView3.Rows[2].Cells[0].Value = "Шаг";

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (server.Connected)
            {
                Disconnect();
            }
            Environment.Exit(1);
        }

        /// <summary>
        /// Отключение от сервера
        /// </summary>
        private void Disconnect()
        {
            //Отправляем на сервер команду об отключении
            Send("Отключиться");
            server.Disconnect(false);

            if (Thread1.ThreadState == ThreadState.Aborted)
            {
                Thread1.Abort();
            }
        }

        private void textBox2_Up(object sender, KeyEventArgs e)
        {
            //если был нажат Ентер...
            if (!e.Shift && e.KeyCode == Keys.Enter)
            {
                //...то отправляем сообщение из текстбокса
                Send("Сообщение", textBox2.Text);
                textBox2.Text = null;
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                //Добавляем на текстбокс выбранный ник
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

        /// <summary>
        /// Получаем пакет из входящего потока трафика
        /// </summary>
        /// <returns></returns>
        static Packet GetPacket()
        {
            byte[] bytes = new byte[1024];
            int bytesRec = server.Receive(bytes);
            string data = Encoding.GetEncoding(866).GetString(bytes, 0, bytesRec);
            Packet packet = JsonConvert.DeserializeObject<Packet>(data);

            return packet;
        }

        /// <summary>
        /// Синхронизация
        /// </summary>
        /// <returns></returns>
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
                //Выбираем файл, который хоти загрузить на сервер
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    //Путь к файлу
                    string fileName = openFileDialog1.FileName;

                    Send("Выгрузить", Path.GetFileName(fileName));

                    if (!Sinhronizatiya())
                    {
                        return;
                    }

                    FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                    BinaryReader f = new BinaryReader(stream);
                    byte[] buffer = new byte[8192]; //Буфер для файла
                    int bytesi = 8192;
                    int fSize = Convert.ToInt32(stream.Length);

                    byte[] bFSize = Encoding.GetEncoding(866).GetBytes(Convert.ToString(fSize)); //Размер файла
                    server.Send(bFSize); //Передаем размер

                    if (!Sinhronizatiya())
                    {
                        return;
                    }

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

                        if (!Sinhronizatiya())
                        {
                            return;
                        }
                        processed += 8192;
                    }
                    f.Close();
                    stream.Close();
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
                //Отправляем выбранную команду на сервер
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
                //Показываем мессаджбокс с информацией о выбранном файле
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

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                Connect("Регистрация");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            int n = Convert.ToInt32(dataGridView2.Rows[0].Cells[1].Value);

            string[] arraySimbols = new string[6];
            arraySimbols[0] = "0123456789";
            arraySimbols[1] = " ~!@#$%^&*()_+`-=:;'\"/?\\|№<>,.";
            arraySimbols[2] = "qwertyuiopasdfghjklzxcvbnm";
            arraySimbols[3] = "QWERTYUIOPASDFGHJKLZXCVBNM";
            arraySimbols[4] = "йцукенгшщзфывапролджячсмитьбюё";
            arraySimbols[5] = "ЙЦУКЕНГШЩЗФЫВАПРОЛДЖЯЧСМИТЬБЮЁ";

            string simbols = "";

            for (int i = 0; i < 6; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    simbols += arraySimbols[i];
                }
            }

            string result = "";

            for (int i = 0; i < n; i++)
            {
                result += simbols.Substring(random.Next(simbols.Length - 1), 1);
            }

            textBox8.Text = result;
            dataGridView2.Rows[1].Cells[1].Value = Encoding.Default.GetByteCount(result);
        }

        Thread threadTest;

        private void button13_Click(object sender, EventArgs e)
        {
            dataGridView5.Rows.Clear();

            threadTest = new Thread(issledovanie1);

            threadTest.Start();
        }

        private void issledovanie1() 
        {
            for (int i = 0; i < 3; i++)
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    comboBox1.SelectedIndex = i;

                    Send("Сообщение", textBox8.Text);
                });

                Thread.Sleep(1000);
            }
        }

        private void addRowInTable(string sobitie, Packet packet)
        {
            //Эксперимент 1
            string[] row = new string[3];
            row[0] = packet.cipher;
            row[1] = sobitie;

            switch (sobitie)
            {
                case "Шифрование":
                case "Дешифрование":
                    DateTime dateTime2 = DateTime.Now;
                    row[2] = (dateTime2 - dateTime).TotalMilliseconds.ToString();
                    break;
                case "Отправка":
                    DateTime dateTimeSend = new DateTime(packet.timeSend);
                    row[2] = (DateTime.Now - dateTimeSend).TotalMilliseconds.ToString();
                    break;
                default:
                    break;
            }

            dataGridView5.Rows.Add(row);

            //Эксперимент 2
            DataGridView dataGridViewFormat = new DataGridView();

            switch (comboBox1.SelectedItem.ToString())
            {
                case "Нет":
                    dataGridViewFormat = dataGridView4;
                    break;
                case "Кузнечик":
                    dataGridViewFormat = dataGridView6;
                    break;
                case "Магма":
                    dataGridViewFormat = dataGridView7;
                    break;
                case "AES":
                    dataGridViewFormat = dataGridView8;
                    break;
                default:
                    break;
            }

            int countSimbols = packet.data.Length - 3;

            switch (sobitie)
            {
                case "Шифрование":
                    dataGridViewFormat.Rows.Add();
                    dataGridViewFormat.Rows[dataGridViewFormat.Rows.Count - 1].Cells[0].Value = countSimbols.ToString();
                    dataGridViewFormat.Rows[dataGridViewFormat.Rows.Count - 1].Cells[1].Value = (DateTime.Now - dateTime).TotalMilliseconds.ToString();
                    break;
                case "Отправка":
                    DateTime dateTimeSend = new DateTime(packet.timeSend);
                    dataGridViewFormat.Rows[dataGridViewFormat.Rows.Count - 1].Cells[2].Value = (DateTime.Now - dateTimeSend).TotalMilliseconds.ToString();
                    break;
                case "Дешифрование":
                    dataGridViewFormat.Rows[dataGridViewFormat.Rows.Count - 1].Cells[3].Value = (DateTime.Now - dateTime).TotalMilliseconds.ToString();
                    break;
                default:
                    break;
            }

        }

        private void button7_Click(object sender, EventArgs e)
        {
            dataGridView4.Rows.Clear();
            dataGridView6.Rows.Clear();
            dataGridView7.Rows.Clear();

            threadTest = new Thread(issledovanie2);

            threadTest.Start();
        }

        private void issledovanie2()
        {
            int min = 1;
            int max = 100;
            int step = 1;

            this.Invoke((MethodInvoker)delegate ()
            {
                min = Convert.ToInt32(dataGridView3.Rows[0].Cells[1].Value);
                max = Convert.ToInt32(dataGridView3.Rows[1].Cells[1].Value);
                step = Convert.ToInt32(dataGridView3.Rows[2].Cells[1].Value);
            });

            string[] arraySimbols = new string[6];
            arraySimbols[0] = "0123456789";
            arraySimbols[1] = " ~!@#$%^&*()_+`-=:;'\"/?\\|№<>,.";
            arraySimbols[2] = "qwertyuiopasdfghjklzxcvbnm";
            arraySimbols[3] = "QWERTYUIOPASDFGHJKLZXCVBNM";
            arraySimbols[4] = "йцукенгшщзфывапролджячсмитьбюё";
            arraySimbols[5] = "ЙЦУКЕНГШЩЗФЫВАПРОЛДЖЯЧСМИТЬБЮЁ";

            string simbols = "";

            for (int i = 0; i < 6; i++)
            {
                this.Invoke((MethodInvoker)delegate ()
                {
                    if (checkedListBox2.GetItemChecked(i))
                    {
                        simbols += arraySimbols[i];
                    }
                });
            }

            string result = "";

            for (int i = 0; i < min; i++)
            {
                result += simbols.Substring(random.Next(simbols.Length - 1), 1);
            }

            for (int i = min; i < max; i += step)
            {
                for (int k = 0; k < step; k++)
                {
                    result += simbols.Substring(random.Next(simbols.Length - 1), 1);
                }

                this.Invoke((MethodInvoker)delegate ()
                {
                    textBox10.Text = result;
                });

                for (int k = 0; k < 3; k++)
                {
                    this.Invoke((MethodInvoker)delegate ()
                    {
                        comboBox1.SelectedIndex = k;

                        Send("Сообщение", result);
                    });

                    Thread.Sleep(1000);
                }

            }
        }
    }
}