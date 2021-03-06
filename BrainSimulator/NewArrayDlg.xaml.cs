﻿//
// Copyright (c) Charles Simon. All rights reserved.  
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//  

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace BrainSimulator
{
    /// <summary>
    /// Interaction logic for NewArray.xaml
    /// </summary>
    public partial class NewArrayDlg : Window
    {
        private const int sizeCount = 1000;
        string crlf = "\r\n\r\n";
        public bool returnValue = false;
        ulong approxSynapseSize = 16;
        ulong assumedSynapseCount = 20;
        [ThreadStatic]
        static Random rand = new Random();

        int arraySize;

        //for the progress bar
        DispatcherTimer barUpdateTimer = new DispatcherTimer() { Interval = new TimeSpan(0, 0, 1) };


        public NewArrayDlg()
        {
            InitializeComponent();

            cbUseServers.IsChecked = MainWindow.useServers;
            buttonSpeedTest.IsEnabled = MainWindow.useServers;
            buttonRefresh.IsEnabled = MainWindow.useServers;

            ulong StartBytes = (ulong)System.GC.GetTotalMemory(true);
            //NeuronBase[] n = new NeuronBase[sizeCount];
            //for (int i = 0; i < sizeCount; i++)
            //    n[i] = new NeuronBase(false);
            ulong StopBytes = (ulong)System.GC.GetTotalMemory(true);
            ulong neuronSize1 = (StopBytes - StartBytes) / sizeCount;

            ulong availablePhysicalMemory = new Microsoft.VisualBasic.Devices.ComputerInfo().AvailablePhysicalMemory;
            ulong totalPhysicalMemory = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
            long memoryCurrentlyInUse = GC.GetTotalMemory(true);
            //ulong neuronSize = approxNeuronSize + (approxSynapseSize * assumedSynapseCount);
            ulong neuronSize = neuronSize1 + (approxSynapseSize * assumedSynapseCount);
            ulong maxNeurons = availablePhysicalMemory / neuronSize;

            string text = "";
            text += "Total Pysical Memory: " + totalPhysicalMemory.ToString("##,#") + crlf;
            text += "Available Physical Memory: " + availablePhysicalMemory.ToString("##,#") + crlf;
            text += "Max Neurons Possible in RAM: " + maxNeurons.ToString("##,#") + crlf;
            text += "Assuming average " + assumedSynapseCount + " synapses per neuron" + crlf;
            textBlock.Text = text;

            UpdateServerTextBox();
        }

        private void UpdateServerTextBox()
        {
            if (MainWindow.useServers)
            {
                NeuronClient.GetServerList();
                Thread.Sleep(1000);
                if (NeuronClient.serverList.Count == 0)
                    ServerList.Text = "No Servers Detected";
                else
                {
                    int.TryParse(textBoxColumns.Text, out cols);
                    int.TryParse(textBoxRows.Text, out rows);
                    ServerList.Text = "";
                    int numServers = NeuronClient.serverList.Count;
                    int neuronsNeeded = rows * cols;
                    for (int i = 0; i < numServers; i++)
                    {
                        NeuronClient.Server s = NeuronClient.serverList[i];
                        s.firstNeuron = i * neuronsNeeded / numServers;
                        s.lastNeuron = (i + 1) * neuronsNeeded / numServers;
                        ServerList.Text += s.ipAddress.ToString() + " " + s.name + " " + s.firstNeuron + " " + s.lastNeuron + "\n";
                    }
                }
            }
            else
            {
                ServerList.Text = "";
            }
        }

        BackgroundWorker bgw = new BackgroundWorker();
        int rows;
        int cols;
        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            buttonOK.IsEnabled = false;
            MainWindow.CloseAllModuleDialogs();
            MainWindow.CloseHistoryWindow();
            MainWindow.CloseNotesWindow();
            if (MainWindow.theNeuronArray != null)
                MainWindow.theNeuronArray.Modules.Clear();
            MainWindow.arrayView.ClearSelection();
            MainWindow.theNeuronArray = new NeuronArray();

            if (!int.TryParse(textBoxColumns.Text, out cols)) return;
            if (!int.TryParse(textBoxRows.Text, out rows)) return;
            if (checkBoxSynapses.IsChecked == true) doSynapses = true;

            arraySize = rows * cols;
            progressBar.Maximum = arraySize;

            int.TryParse(textBoxSynapses.Text, out synapsesPerNeuron);
            MainWindow.arrayView.Dp.NeuronDisplaySize = 62;
            MainWindow.arrayView.Dp.DisplayOffset = new Point(0, 0);

            if (MainWindow.useServers)
            {
                //TODO: Replace this with a multicolumn UI
                MainWindow.theNeuronArray.Initialize(arraySize, rows);
                string[] lines = ServerList.Text.Split('\n');
                NeuronClient.serverList.Clear();
                foreach (string line in lines)
                {
                    if (line == "") continue;
                    string[] command = line.Split(' ');
                    NeuronClient.Server s = new NeuronClient.Server();
                    s.ipAddress = IPAddress.Parse(command[0]);
                    s.name = command[1];
                    int.TryParse(command[2], out s.firstNeuron);
                    int.TryParse(command[3], out s.lastNeuron);
                    NeuronClient.serverList.Add(s);
                }

                int totalNeuronsInServers = 0;
                for (int i = 0; i < NeuronClient.serverList.Count; i++)
                    totalNeuronsInServers += NeuronClient.serverList[i].lastNeuron - NeuronClient.serverList[i].firstNeuron;
                if (totalNeuronsInServers != arraySize)
                {
                    MessageBox.Show("Server neuron allocation does not equal total neurons!");
                    buttonOK.IsEnabled = true;
                    returnValue = false;
                    return;
                }

                if (!doSynapses) synapsesPerNeuron = 0;
                NeuronClient.InitServers(synapsesPerNeuron, arraySize);
                NeuronClient.WaitForDoneOnAllServers();
                returnValue = true;
                Close();
            }
            else
            {
                bgw.DoWork += AsyncCreateNeurons;
                bgw.RunWorkerAsync();

                barUpdateTimer.Tick += Dt_Tick;
                barUpdateTimer.Start();

            }
        }

        bool done = false;
        bool doSynapses = false;
        int synapsesPerNeuron = 10;
        private void Dt_Tick(object sender, EventArgs e)
        {
            progressBar.Maximum = MainWindow.theNeuronArray.arraySize * synapsesPerNeuron;
            MainWindow.theNeuronArray.GetCounts(out long synapseCount, out int useCount);
            progressBar.Value = synapseCount;
            if (done)
            {
                barUpdateTimer.Stop();
                returnValue = true;
                Close();
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AsyncCreateNeurons(object sender, DoWorkEventArgs e)
        {
            GC.Collect(3, GCCollectionMode.Forced, true);
            MainWindow.theNeuronArray.Initialize(arraySize, rows);
            if (doSynapses)
            {
                GC.Collect(3, GCCollectionMode.Forced, true);
#if !DEBUG
                Parallel.For(0, MainWindow.theNeuronArray.arraySize, i => CreateRandomSynapses(i));
#else
                for (int i = 0; i < MainWindow.theNeuronArray.arraySize; i++)
                    CreateRandomSynapses(i);
#endif
            }
            done = true;
        }

        private void CreateRandomSynapses(int i)
        {
            if (rand == null) rand = new Random();
            for (int j = 0; j < synapsesPerNeuron; j++)
            {
                int targetNeuron = i + rand.Next() % (2 * synapsesPerNeuron) - synapsesPerNeuron;
                int rowOffset = rand.Next() % 10 - 5;
                int colOffset = rand.Next() % 10 - 5;
                targetNeuron = i + (colOffset * rows) + rowOffset;

                while (targetNeuron < 0) targetNeuron += arraySize;
                while (targetNeuron >= arraySize) targetNeuron -= arraySize;
                float weight = (rand.Next(1000) / 750f) - .5f;
                MainWindow.theNeuronArray.AddSynapse(i, targetNeuron, weight, false, true);
            }
        }

        private void Button_Refresh(object sender, RoutedEventArgs e)
        {
            UpdateServerTextBox();
        }

        //PING speed test
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string s = ServerList.SelectedText;
            if (!IPAddress.TryParse(s, out IPAddress targetIp))
            {
                MessageBox.Show("Highlight an IP address");
                return;
            }
            NeuronClient.pingCount = 0;
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            string payload = NeuronClient.CreatePayload(1450);
            for (int i = 0; i < 100000; i++)
            {
                NeuronClient.SendToServer(targetIp, "Ping");
            }
            sw.Stop();
            double packetSendNoPayload = ((double)sw.ElapsedMilliseconds) / 100000.0;
            Thread.Sleep(1000);

            sw.Start();
            for (int i = 0; i < 100000; i++)
            {
                NeuronClient.SendToServer(targetIp, "Ping " + payload);
            }
            sw.Stop();
            double packetSendBigPayload = ((double)sw.ElapsedMilliseconds) / 100000.0;
            Thread.Sleep(1000);

            List<long> rawData = new List<long>();
            for (int i = 0; i < 1000; i++)
                rawData.Add(NeuronClient.Ping(targetIp, ""));
            double latencyNoPayload = ((double)rawData.Average()) / 10000.0;
            rawData.Clear();
            for (int i = 0; i < 1000; i++)
                rawData.Add(NeuronClient.Ping(targetIp, payload));
            double latencyBigPayload = ((double)rawData.Average()) / 10000.0;

            PingLabel.Content = "Packet Spd: " + packetSendNoPayload.ToString("F4") + "ms-" + packetSendBigPayload.ToString("F4") + "ms  R/T Latency:  "
                + latencyNoPayload.ToString("F4") + "ms-" + latencyBigPayload.ToString("F4") + "ms " + NeuronClient.pingCount;
            PingLabel1.Visibility = Visibility.Visible;
        }

        private void CheckBoxUseServers_Checked(object sender, RoutedEventArgs e)
        {
            MainWindow.useServers = true;
            buttonSpeedTest.IsEnabled = MainWindow.useServers;
            buttonRefresh.IsEnabled = MainWindow.useServers;
            NeuronClient.Init();
            UpdateServerTextBox();
        }

        private void CheckBoxUseServers_Unchecked(object sender, RoutedEventArgs e)
        {
            MainWindow.useServers = false;
            buttonSpeedTest.IsEnabled = MainWindow.useServers;
            buttonRefresh.IsEnabled = MainWindow.useServers;
            UpdateServerTextBox();
        }
    }
}
