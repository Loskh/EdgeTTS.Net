using EdgeTTS.Net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
//using System.Text.Json;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Threading;
//using WebSocket4Net;

namespace EdgeTTS
{

    public enum ServiceStatus
    {
        Running,
        Stopping,
        Stopped,
        InnerStop,
    }

    public class WsItem
    {
        public string message;
        public SSML SSMLPayload;
    }

    public class SSML
    {
        public string Voice;
        public string Pitch;
        public string Rate;
        public string Volume;
        public string Sentence;
        public SSML(string sentence, string voice, string pitch, string rate, string volume)
        {
            this.Voice = voice;
            this.Pitch = pitch;
            this.Rate = rate;
            this.Volume = volume;
            this.Sentence = sentence;
        }
        public override string ToString()
        {
            var message = "X-RequestId:" + Utils.GetUUID() + "\r\nContent-Type:application/ssml+xml\r\n";
            message += "X-Timestamp:" + Utils.GetFormatedDate() + "Z\r\nPath:ssml\r\n\r\n";
            message += "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>";
            message += "<voice  name='" + Voice + "'>" + "<prosody pitch='" + Pitch + "' rate ='" + Rate + "' volume='" + Volume + "'>";
            //message += $"<mstts:express-as style=\"{style}\" styledegree=\"2\">";
            message += Sentence;
            //message += "</mstts:express-as>";
            message += "</prosody></voice></speak>";
            return message;
        }
    }

    public class EdgeTTS
    {
        private const string TRUSTED_CLIENT_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";

        private ClientWebSocket webSocket;

        public ServiceStatus Status { get; internal set; } = ServiceStatus.Stopped;
        private MemoryStream buffer;

        public EdgeTTS()
        {

            EstablishConnectionAsync();
            StartReceiving(webSocket);
        }


        private async void EstablishConnectionAsync()
        {
            webSocket = new ClientWebSocket();
            webSocket.Options.SetRequestHeader("Pragma", "no-cache");
            webSocket.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
            webSocket.Options.SetRequestHeader("Accept-Encoding", "en-US,en;q=0.9");
            //webSocket.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.77 Safari/537.36 Edg/91.0.864.41");
            webSocket.Options.SetRequestHeader("Cache-Control", "no-cache");
            await webSocket.ConnectAsync(new Uri($"wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1?TrustedClientToken={TRUSTED_CLIENT_TOKEN}&ConnectionId={Utils.GetUUID()}"), CancellationToken.None);
        }

        private async void StartReceiving(ClientWebSocket client)
        {
            if (webSocket == null)
                return;
            if (buffer != null)
            {
                buffer = new MemoryStream(); ;
            }
            var sb = new StringBuilder();
            bool DownloadStarted = false;

            while (true)
            {
                if (webSocket.State != WebSocketState.Open)
                    continue;
                try
                {
                    if (client.CloseStatus == WebSocketCloseStatus.EndpointUnavailable ||
                        client.CloseStatus == WebSocketCloseStatus.InternalServerError ||
                        client.CloseStatus == WebSocketCloseStatus.EndpointUnavailable)
                    {
                        Status = ServiceStatus.Stopped;
                        return;
                    }
                    var array = new byte[100000];
                    var receive = await client.ReceiveAsync(new ArraySegment<byte>(array), CancellationToken.None);
                    switch (receive.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            if (receive.Count <= 0)
                            {
                                continue;
                            }

                            string tempMsg = Encoding.UTF8.GetString(array, 0, receive.Count);
                            sb.Append(tempMsg);
                            if (receive.EndOfMessage == false)
                            {
                                continue;
                            }
                            Console.WriteLine(sb);
                            if (sb.ToString().Contains("Path:turn.start"))
                            {
                                DownloadStarted = true;
                                buffer = new MemoryStream();
                            }
                            else if (sb.ToString().Contains("Path:turn.end"))
                            {
                                DownloadStarted = false;
                                Status = ServiceStatus.Stopped;
                            }

                            sb.Clear();
                            break;
                        case WebSocketMessageType.Binary:
                            if (receive.Count < 0x82)
                                continue;
                            if (DownloadStarted)
                                buffer.Write(array, 0x82, receive.Count - 0x82);
                            break;
                        case WebSocketMessageType.Close:
                            break;
                    }
                }
                catch (WebSocketException)
                {
                    Status = ServiceStatus.Stopped;
                    return;
                }
            }
        }


        public async Task<MemoryStream> SayAsync(string str, string voice = "zh-CN-XiaoxiaoNeural", string pitch = "+0Hz", string rate = "+0%", string volume = "+0%")
        {
            var message = "X-Timestamp:" + Utils.GetFormatedDate() + "\r\nContent-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n";
            message += "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":" + "\"false\"" + ",\"wordBoundaryEnabled\":" + "\"false\"" + "}," + "\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}\r\n";
            //sentenceQ.Enqueue(new WsItem() { message = message, SSMLPayload = new SSML(str, voice, pitch, rate, volume) });
            var s = new WsItem() { message = message, SSMLPayload = new SSML(str, voice, pitch, rate, volume) };
            if (webSocket.State != WebSocketState.Open) {
                EstablishConnectionAsync();
                Console.WriteLine("重建链接");
            }

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(s.message)), WebSocketMessageType.Text, true, CancellationToken.None);
            //CurrentSentence = s.SSMLPayload;
            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(s.SSMLPayload.ToString())), WebSocketMessageType.Text, true, CancellationToken.None);
            Status = ServiceStatus.Running;
            while (Status == ServiceStatus.Running)
            {
                await Task.Delay(10);
            }
            return buffer;
        }


        //public void GetVoiceList()
        //{
        //    HttpWebRequest req = WebRequest.CreateHttp($"https://speech.platform.bing.com/consumer/speech/synthesize/readaloud/voices/list?trustedclienttoken={TRUSTED_CLIENT_TOKEN}");
        //    req.Method = "GET";
        //    req.Headers.Add("Authority", "speech.platform.bing.com");
        //    req.Headers.Add("Sec-CH-UA", "\" Not;A Brand\";v=\"99\", \"Microsoft Edge\";v=\"91\", \"Chromium\";v=\"91\"");
        //    req.Headers.Add("Sec-CH-UA-Mobile", "?0");
        //    req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.77 Safari/537.36 Edg/91.0.864.41");
        //    req.Headers.Add("Accept", "*/*");
        //    req.Headers.Add("Sec-Fetch-Site", "none");
        //    req.Headers.Add("Sec-Fetch-Mode", "cors");
        //    req.Headers.Add("Sec-Fetch-Dest", "empty");
        //    req.Headers.Add("Accept-Encoding", "gzip, deflate, br");
        //    req.Headers.Add("Accept-Language", "en-US,en;q=0.9");
        //    var res = req.GetResponse();
        //    var resString = string.Empty;
        //    using (var steam = new GZipStream(res.GetResponseStream(), CompressionMode.Decompress))
        //    {
        //        var sr = new StreamReader(steam, System.Text.Encoding.UTF8);
        //        resString = sr.ReadToEnd();

        //    }
        //    var voiceList = JsonSerializer.Deserialize<List<Voice>>(resString);
        //    voiceList.ForEach(x => Console.WriteLine(x.ShortName));
        //    Voices = voiceList;
        //}

    }
}
