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

        private ClientWebSocket ws;

        //private WaveStream source;

        private WaveFileWriter RecordedAudioWriter = null;
        //private WasapiLoopbackCapture CaptureInstance = null;
        private WaveIn wave = null;
        private MemoryStream memoryStream;
        public Form1()
        {
            InitializeComponent();
            Task task1 = ConnectToServer();
        }

        async private void btnStart_Click(object sender, EventArgs e)
        {
            // Define the output wav file of the recorded audio
            string outputFilePath = @"C:\Users\gucea\Desktop\system_recorded_audio.wav";

            // Redefine the capturer instance with a new instance of the LoopbackCapture class
            //this.CaptureInstance = new WasapiLoopbackCapture();
            wave = new WaveIn();
            wave.WaveFormat = new WaveFormat(16000, 16, 1);
            memoryStream = new MemoryStream();
            // Redefine the audio writer instance with the given configuration
            this.RecordedAudioWriter = new WaveFileWriter(new IgnoreDisposeStream(memoryStream), wave.WaveFormat);

            // When the capturer receives audio, start writing the buffer into the mentioned file
            this.wave.DataAvailable += (s, a) =>
            {
                this.RecordedAudioWriter.Write(a.Buffer, 0, a.BytesRecorded);
            };

            // When the Capturer Stops
            this.wave.RecordingStopped += (s, a) =>
            {
                this.RecordedAudioWriter.Dispose();
                this.RecordedAudioWriter = null;
                wave.Dispose();
            };

            // Enable "Stop button" and disable "Start Button"
            this.btnStart.Enabled = false;
            this.btnStop.Enabled = true;

            // Start recording !
            this.wave.StartRecording();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            // Stop recording !
            this.wave.StopRecording();

            // Enable "Start button" and disable "Stop Button"
            this.btnStart.Enabled = true;
            this.btnStop.Enabled = false;
        }

        public async Task ConnectToServer()
        {
            ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri("ws://live-transcriber.zevo-tech.com:" + 12320), CancellationToken.None);
            string key = "e4b70d22ca4b47369fbbc46b2afa3c33";
            byte[] api = Encoding.UTF8.GetBytes("{\"config\": {\"key\": \"" + key + "\"}}");
            await ws.SendAsync(new ArraySegment<byte>(api, 0, api.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        async Task DecodeFile()
        {
            /*string outputFilePath = @"C:\Users\gucea\Desktop\system_recorded_audio.wav";
            FileStream fsSource = new FileStream(outputFilePath,
                       FileMode.Open, FileAccess.Read);*/

            byte[] data = new byte[16000];
            while (true)
            {
                int count = memoryStream.Read(data, 0, 16000);
                if (count == 0)
                    break;
                await ProcessData(ws, data, count);
            }
            await ProcessFinalData(ws);

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None);
        }

        async Task RecieveResult(ClientWebSocket ws)
        {
            byte[] result = new byte[16000];
            Task<WebSocketReceiveResult> receiveTask = ws.ReceiveAsync(new ArraySegment<byte>(result), CancellationToken.None);
            await receiveTask;
            var receivedString = Encoding.UTF8.GetString(result, 0, receiveTask.Result.Count);
            Debug.WriteLine("Result {0}", receivedString);
        }

        async Task ProcessData(ClientWebSocket ws, byte[] data, int count)
        {
            await ws.SendAsync(new ArraySegment<byte>(data, 0, count), WebSocketMessageType.Binary, true, CancellationToken.None);
            await RecieveResult(ws);
        }

        async Task ProcessFinalData(ClientWebSocket ws)
        {
            byte[] eof = Encoding.UTF8.GetBytes("{\"eof\" : 1}");
            await ws.SendAsync(new ArraySegment<byte>(eof), WebSocketMessageType.Text, true, CancellationToken.None);
            await RecieveResult(ws);
        }

        async private void transmitBtn_Click(object sender, EventArgs e)
        {
            await DecodeFile();
        }
    }

}
