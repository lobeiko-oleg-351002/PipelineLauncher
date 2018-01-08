﻿using BllEntities;
using Client.Forms;
using ServerInterface;
using ServiceChannelManager;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace Client
{
    public partial class MainForm : ParentForm, IServerCallBack
    {
        public MainForm()
        {
            InitializeComponent();
        }

        const string LOGIN_TAG = "login";
        const string PASSWORD_TAG = "password";
        const string DATE_FORMAT = "dd.MM.yyyy";
        const string TIME_FORMAT = "HH:mm";
        const string STATUS_NOT_CHANGED = "Статус не изменён";


        const int PING_SLEEPTIME_MS = 10000;

        IBusinessService server;

        BllUser User = null;
        List<BllEvent> EventList;
        List<BllStatus> Statuses;
        int SelectedRowIndex;
        int newEventsCount;
        List<int> NewEventRowIndexes = new List<int>();
        NotifyIcon notifyIcon = new NotifyIcon();

        bool isAppClosed;

        private bool _isServerOnline;

        bool isServerOnline {
            get { return _isServerOnline; }
            set
            {
                if ((value == true) && (_isServerOnline == false))
                {
                    Invoke(new Action(() =>
                    {
                        textBox1.Text = GetConstFromResources("SERVER_ONLINE");
                        создатьСобытиеToolStripMenuItem.Enabled = true;
                        if (comboBox1.SelectedIndex > 0)
                        {
                            button1.Enabled = true;
                        }
                        if (comboBox1.Items.Count == 1)
                        {
                            InitStatuses();
                        }
                    }));
                }
                if ((value == false) && (_isServerOnline == true))
                {
                    Invoke(new Action(() =>
                    {
                        SetControlsServerOffline();
                    }));

 
                }
                _isServerOnline = value;
            }
        }

        private void LogMessage(string message)
        {
            //richTextBox2.Text += "[" + DateTime.Now.ToString(TIME_FORMAT) + "] " + message + "\n";
        }

        private void SetControlsServerOffline()
        {
            textBox1.Text = GetConstFromResources("SERVER_OFFLINE");
            создатьСобытиеToolStripMenuItem.Enabled = false;
            button1.Enabled = false;
        }

        private string GetConstFromResources(string name)
        {
            return Properties.Resources.ResourceManager.GetString(name);
        }

        private void SetSelectedEventToControls(BllEvent Event)
        {
            textBox2.Text = Event.Sender.Fullname;
            textBox3.Text = Event.Name;
            textBox4.Text = Event.Date.ToString(DATE_FORMAT);
            textBox5.Text = Event.Date.ToString(TIME_FORMAT);
            listBox1.Items.Clear();
            foreach(var status in Event.StatusLib.SelectedEntities)
            {
                listBox1.Items.Add(status.Entity.Name + " " + status.Date);
            }
            listBox3.Items.Clear();
            foreach (var attr in Event.AttributeLib.SelectedEntities)
            {
                listBox3.Items.Add(attr.Entity.Name);
            }
            listBox2.Items.Clear();
            foreach (var filename in Event.FilepathLib.Entities)
            {
                listBox2.Items.Add(filename.Path);
            }
            richTextBox1.Text = Event.Description;

            comboBox1.SelectedIndex = 0;
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            server = ServiceChannelManagerSingleton.Instance.GetServerMethods(this);
            EventList = new List<BllEvent>();

            notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon.BalloonTipText = "Программа работает в фоновом режиме.";
            notifyIcon.BalloonTipTitle = "Pipeline";
            notifyIcon.Icon = this.Icon;
            notifyIcon.Text = "Pipeline";
            notifyIcon.MouseDoubleClick += notifyIcon_MouseDoubleClick;

            comboBox1.Items.Add(STATUS_NOT_CHANGED);



            Authorize(server);
            if (!isAppClosed)
            {
                SetControlsServerOffline();
                PingServer();
                new Thread(() =>
                {
                    while (!isAppClosed)
                    {
                        Thread.CurrentThread.IsBackground = true;
                        PingServer();
                        Thread.Sleep(PING_SLEEPTIME_MS);
                    }
                }).Start();
                GetEventList();
                
            }

            dataGridView1.CurrentRow.Selected = false;
        }

        private void PingServer()
        {
            try
            {
                if (isServerOnline == false)
                {
                    server = ServiceChannelManagerSingleton.Instance.GetServerMethods(this);
                    server.RegisterClient(User.Login);
                }
                server.PingServer();
                isServerOnline = true;
            }
            catch(Exception ex)
            {
                //MessageBox.Show(ex.Message);
                isServerOnline = false;
            }
        }

        private void InitStatuses()
        {
            Statuses = server.GetAllStatuses();
            foreach (var item in Statuses)
            {
                comboBox1.Items.Add(item.Name);
            }
        }

        private void GetEventList()
        {
            if (isServerOnline == false)
            {
                DeserializeEventsToDataGrid();
            }
            else
            {
                EventList = server.GetEventsForUser(User);
                foreach (var item in EventList)
                {
                    AddEventToDataGrid(item);
                    foreach (var name in item.FilepathLib.Entities)
                    {
                        new Thread(delegate ()
                        {
                            DownloadFile(name.Path, item.FilepathLib.FolderName);
                        }).Start();

                    }
                }
                HighlightNewRows();
                FlashWindow.Start(this);
                SetEventsCountInPanel();
                SerializeEvents();
            }
            
        }

        private void HighlightNewRows()
        {
            var EventsFromCache = DeserializeEvents();
            for(int i = 0; i < EventList.Count; i++)
            {
                bool isAdmited = false;
                foreach(var item in EventsFromCache)
                {
                    if (item.Id == EventList[i].Id)
                    {
                        if (item.IsAdmited)
                        {
                            EventList[i].IsAdmited = true;
                            isAdmited = true;
                        }
                        break;
                    }
                }
                if (!isAdmited)
                {
                    HighlightRow(i);
                    NewEventRowIndexes.Add(i);
                }
            }
        }

        private void AddEventToDataGrid(BllEvent Event)
        {
            DataGridViewRow row = new DataGridViewRow();
            row.CreateCells(dataGridView1);
            row.Cells[0].Value = Event.Sender.Fullname;
            row.Cells[1].Value = Event.Name;
            row.Cells[2].Value = Event.Date.Date.ToString(DATE_FORMAT);
            row.Cells[3].Value = Event.Date.ToString(TIME_FORMAT);
            var status = Event.StatusLib.SelectedEntities.Last();
            row.Cells[4].Value = status.Entity.Name + " " + status.Date;
           
            foreach (var attr in Event.AttributeLib.SelectedEntities)
            {
                row.Cells[5].Value += attr.Entity.Name + "; ";
            }
            ((DataGridViewButtonCell)row.Cells[6]).Value += " " + Event.FilepathLib.Entities.Count + " ф.";
            row.Cells[7].Value = Event.Description;

            dataGridView1.Rows.Add(row);
        }

        private void Authorize(IBusinessService server)
        {
            string login = ConfigurationManager.AppSettings[LOGIN_TAG];
            string password = ConfigurationManager.AppSettings[PASSWORD_TAG];
            try
            {
                if (login == null)
                {
                    SignInForm signInForm = new SignInForm(server);
                    signInForm.ShowDialog();
                    User = signInForm.User;
                    if (User == null)
                    {
                        ExitApp();
                    }
                    else
                    {
                        WriteLoginAndPasswordToConfig(User.Login, User.Password);
                    }
                }
                else
                {
                    User = server.SignIn(login, password);
                }
                if (User != null)
                {
                    label9.Text = User.Fullname;
                }
            }
            catch
            {
                if ((login == null) && (password == null))
                {
                    MessageBox.Show(GetConstFromResources("SERVER_NOT_FOUND"));
                    ExitApp();
                }
            }
        }

        private void WriteLoginAndPasswordToConfig(string login, string password)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);
            config.AppSettings.Settings.Add(LOGIN_TAG, login);
            config.AppSettings.Settings.Add(PASSWORD_TAG, password);
            config.Save(ConfigurationSaveMode.Minimal);
        }

        private void ExitApp()
        {
            Application.Exit();
            isAppClosed = true;
            Close();
        }

        private void создатьСобытиеToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PingServer();
            if (isServerOnline)
            {
                if (User == null)
                {
                    Authorize(server);
                }
                AddEventForm addEventForm = new AddEventForm(server, User);
                addEventForm.ShowDialog();
                if (addEventForm.Event != null)
                {
                    EventList.Add(addEventForm.Event);
                    AddEventToDataGrid(addEventForm.Event);
                    SerializeEventsBackground();
                }
            }
        }
        private void HighlightRow(int i)
        {
            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Font = new Font(dataGridView1.Font, FontStyle.Bold);
            foreach (DataGridViewCell cell in dataGridView1.Rows[i].Cells)
            {
                cell.Style.Font = style.Font;
            }

        }

        private void RowCommonFont(int i)
        {
            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Font = new Font(dataGridView1.Font, FontStyle.Regular);
            foreach (DataGridViewCell cell in dataGridView1.Rows[i].Cells)
            {
                cell.Style.Font = style.Font;
            }
        }

        public void GetEvent(BllEvent Event)
        {
            EventList.Add(Event);
            AddEventToDataGrid(Event);
            SerializeEventsBackground();
            HighlightRow(dataGridView1.RowCount - 1);
            NewEventRowIndexes.Add(dataGridView1.Rows.Count - 1);
            SetEventsCountInPanel();
            SystemSounds.Beep.Play();
            FlashWindow.Start(this);
        }

        private void SerializeEventsBackground()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                SerializeEvents();
            }).Start();
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            
            var senderGrid = (DataGridView)sender;
            
            if (senderGrid.Columns[e.ColumnIndex] is DataGridViewButtonColumn &&
                e.RowIndex >= 0)
            {
                foreach (var name in EventList[e.RowIndex].FilepathLib.Entities)
                {
                    try
                    {
                        Process.Start(DownloadFile(name.Path, EventList[e.RowIndex].FilepathLib.FolderName));
                    }
                    catch
                    {
                        MessageBox.Show(GetConstFromResources("CANNOT_OPEN_FILE"), name.Path);
                    }
                }
            }
        }

        private string DownloadFile(string name, string folderName)
        {
            string downloadsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + GetConstFromResources("DOWNLOADS_FOLDER");
            string eventFolderPath = Path.Combine(downloadsPath, folderName);
            string filePath = Path.Combine(downloadsPath, folderName, name);
            if (!Directory.Exists(eventFolderPath))
            {
                Directory.CreateDirectory(eventFolderPath);
            }
            if (!File.Exists(filePath))
            {
                using (FileStream output = new FileStream(filePath, FileMode.Create))
                {
                    Stream downloadStream;

                    using (FileServiceClient client = new FileServiceClient())
                    {
                        downloadStream = client.GetFile(Path.Combine(folderName, name));
                    }

                    downloadStream.CopyTo(output);
                }
            }
            return filePath;
        }

        public void Ping()
        {
            
        }

        private void SerializeEvents()
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<BllEvent>));
                string mydoc = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                using (FileStream stream = new FileStream(mydoc + GetConstFromResources("CACHE_XML_FILE"), FileMode.Create))
                {
                    serializer.Serialize(stream, EventList);
                }
            }
            catch (IOException)
            {

            }
        }

        private void DeserializeEventsToDataGrid()
        {
            try
            {
                string mydoc = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                using (Stream stream = File.Open(mydoc + GetConstFromResources("CACHE_XML_FILE"), FileMode.Open))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<BllEvent>));
                    EventList = (List<BllEvent>)serializer.Deserialize(stream);
                    foreach (BllEvent item in EventList)
                    {
                        AddEventToDataGrid(item);
                    }
                }
            }
            catch (IOException)
            {
            }
        }

        private List<BllEvent> DeserializeEvents()
        {
            try
            {
                string mydoc = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                using (Stream stream = File.Open(mydoc + GetConstFromResources("CACHE_XML_FILE"), FileMode.Open))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<BllEvent>));
                    return (List<BllEvent>)serializer.Deserialize(stream);
                }
            }
            catch (IOException)
            {
            }
            return null;

        }

        
        private void SetEventsCountInPanel()
        {
            int n = NewEventRowIndexes.Count;
            Text = GetConstFromResources("CLIENT_NAME");
            if (n > 0)
            {
                Text += " (" + n + ")";
            }
            else
            {
                FlashWindow.Stop(this);
            }
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            if (NewEventRowIndexes.Contains(e.RowIndex))
            {
                EventList[e.RowIndex].IsAdmited = true;
                RowCommonFont(e.RowIndex);
                NewEventRowIndexes.Remove(e.RowIndex);
                SetEventsCountInPanel();
            }
            SelectedRowIndex = e.RowIndex;
            SetSelectedEventToControls(EventList[e.RowIndex]);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PingServer();
            
            if (isServerOnline)
            {
                EventList[SelectedRowIndex].StatusLib.SelectedEntities.Add(new BllSelectedStatus { Entity = Statuses[comboBox1.SelectedIndex - 1] });
                EventList[SelectedRowIndex] = server.UpdateAndSendOutEvent(EventList[SelectedRowIndex], User);
                EventList[dataGridView1.SelectedRows[0].Index] = EventList[SelectedRowIndex];
                var newStatus = EventList[SelectedRowIndex].StatusLib.SelectedEntities.Last();
                listBox1.Items.Add(newStatus.Entity.Name + " " + newStatus.Date);
                UpdateEventStatusInDataGrid(newStatus, dataGridView1.SelectedRows[0].Index, false);
                comboBox1.SelectedIndex = 0;
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex == 0)
            {
                button1.Enabled = false;
            }
            else
            {
                if (isServerOnline)
                {
                    button1.Enabled = true;
                }
            }

        }

        public void UpdateEvent(BllEvent Event)
        {
            int i = 0;
            for (i = 0; i < EventList.Count; i++)
            {
                if (EventList[i].Id == Event.Id)
                {
                    EventList[i] = Event;
                    break;
                }
            }
            var status = Event.StatusLib.SelectedEntities.Last();
            UpdateEventStatusInDataGrid(status, i, true);
            if (dataGridView1.SelectedRows.Count > 0)
            {
                if (dataGridView1.SelectedRows[0].Index == i)
                {
                    listBox1.Items.Add(status.Entity.Name + " " + status.Date);
                }
            }
            SerializeEventsBackground();
            SystemSounds.Beep.Play();
        }

        private void UpdateEventStatusInDataGrid(BllSelectedStatus status, int index, bool isBold)
        {
            var cell = dataGridView1.Rows[index].Cells[4];
            cell.Value = status.Entity.Name + " " + status.Date;
            if (isBold)
            {
                cell.Style.Font = new Font(dataGridView1.Font, FontStyle.Bold);
            }

        }

        private void listBox2_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                Process.Start(DownloadFile(EventList[SelectedRowIndex].FilepathLib.Entities[listBox2.SelectedIndex].Path, EventList[SelectedRowIndex].FilepathLib.FolderName));
            }
            catch
            {
                MessageBox.Show(GetConstFromResources("CANNOT_OPEN_FILE"), EventList[SelectedRowIndex].FilepathLib.Entities[listBox2.SelectedIndex].Path);
            }
        }

        private void comboBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            SerializeEvents();
            notifyIcon.Visible = false;
        }

        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.ShowInTaskbar = true;
            notifyIcon.Visible = false;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(3000);
            this.ShowInTaskbar = false;
            this.Hide();
            e.Cancel = true;
        }
    }
}
