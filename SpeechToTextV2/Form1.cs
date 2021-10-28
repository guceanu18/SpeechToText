using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Utils;
using NAudio.Wave;
using Newtonsoft.Json.Linq;

namespace SpeechToTextV2
{
    public partial class Form1 : Form
    {
        private bool isRecording;
        private ClientWebSocket ws;
        private string finalResult;
        MemoryStream memoryStream;
        //private WaveStream source;

        private WaveFileWriter RecordedAudioWriter = null;
        //private WasapiLoopbackCapture wave = null;
        private WaveIn wave = null;
        public Form1()
        {
            InitializeComponent();
            //Task task1 = ConnectToServer();
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            await ConnectToServer();
            // Enable "Stop button" and disable "Start Button"
            this.btnStart.Enabled = false;
            this.btnStop.Enabled = true;
            isRecording = true;

            // Redefine the capturer instance with a new instance of the LoopbackCapture class
            //this.CaptureInstance = new WasapiLoopbackCapture();
            wave = new WaveIn();
            wave.WaveFormat = new WaveFormat(16000, 16, 1);
            memoryStream = new MemoryStream();
            // Redefine the audio writer instance with the given configuration
            this.RecordedAudioWriter = new WaveFileWriter(new IgnoreDisposeStream(memoryStream), wave.WaveFormat);

            // When the capturer receives audio, start writing the buffer into the mentioned file
            this.wave.DataAvailable += async (s, a) =>
            {
                this.RecordedAudioWriter.Write(a.Buffer, 0, a.BytesRecorded);
                byte[] result = new byte[16000];
                await ws.SendAsync(new ArraySegment<byte>(a.Buffer, 0, a.BytesRecorded), WebSocketMessageType.Binary, true, CancellationToken.None);
                var receivedString = Encoding.UTF8.GetString(result, 0, ws.ReceiveAsync(new ArraySegment<byte>(result), CancellationToken.None).Result.Count);
                Debug.WriteLine("Result {0}", receivedString);
                jsonHandler(receivedString);

            };

            // When the Capturer Stops
            this.wave.RecordingStopped += (s, a) =>
            {
                this.RecordedAudioWriter.Dispose();
                this.RecordedAudioWriter = null;
                wave.Dispose();
            };

            // Start recording !
            this.wave.StartRecording();
        }

        void jsonHandler(string receivedString)
        {
            JObject obj = JObject.Parse(receivedString);
            if (receivedString.Contains("partial"))
            {
                var item = obj["partial"].ToString();
                System.Diagnostics.Debug.WriteLine(item);
                finalResult = item + " ";
                textBox.Text = finalResult;
            }
            if (receivedString.Contains("text"))
            {
                var item = obj["text"].ToString();
                System.Diagnostics.Debug.WriteLine(item);
                finalResult += item + " ";
                textBox.Text += finalResult;
            }

        }


        private async void btnStop_Click(object sender, EventArgs e)
        {
            // Stop recording !
            this.wave.StopRecording();
            isRecording = false;
            // Enable "Start button" and disable "Stop Button"
            this.btnStart.Enabled = true;
            this.btnStop.Enabled = false;
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None);
        }

        public async Task ConnectToServer()
        {
            ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri("ws://live-transcriber.zevo-tech.com:" + 12320), CancellationToken.None);
            string key = "e4b70d22ca4b47369fbbc46b2afa3c33";
            byte[] api = Encoding.UTF8.GetBytes("{\"config\": {\"key\": \"" + key + "\"}}");
            await ws.SendAsync(new ArraySegment<byte>(api, 0, api.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        
    }
}
