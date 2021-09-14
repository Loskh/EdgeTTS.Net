
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
//using System.Text.Json;
using System.Net.Sockets;
using System.Diagnostics;
//using WebSocket4Net;
using Serilog;

namespace EdgeTTS
{
    public class EdgeTTS : IDisposable
    {

        public bool KeepConnection;

        private ClientWebSocket webSocket = new ClientWebSocket();

        public WebSocketState WebSocketState => webSocket.State;

        SemaphoreSlim slimlock = new SemaphoreSlim(1, 1);

        private bool disposedValue;

        private Timer ConnectionKeeper;

        private const string TRUSTED_CLIENT_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";

        public EdgeTTS(bool keepConnection = true, bool useConnectionKeeper = false, int keepAliveInterval = 1000)
        {
            this.KeepConnection = keepConnection;
            if (useConnectionKeeper)
            {
                ConnectionKeeper = new Timer(ConnnectKeeper, null, 0, keepAliveInterval);
            }
        }

        private void ConnnectKeeper(object state)
        {
            slimlock.Wait();
#if DEBUG
            Debug.WriteLine($"[{DateTime.Now}] Keeper WS_STATE={WebSocketState}");
#endif
            try
            {
                switch (webSocket.State)
                {
                    case WebSocketState.Closed:
                        webSocket.Abort();
                        webSocket.Dispose();
                        webSocket = new ClientWebSocket();
                        EstablishConnectionAsync(CancellationToken.None).Wait();
                        break;
                    case WebSocketState.Aborted:
                        webSocket = new ClientWebSocket();
                        EstablishConnectionAsync(CancellationToken.None).Wait();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[{DateTime.Now}] Keeper {ex.Message}");
#endif
            }
            finally
            {
                slimlock.Release();
            }
        }

        private async Task EstablishConnectionAsync(CancellationToken token)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var options = webSocket.Options;
            options.SetRequestHeader("Pragma", "no-cache");
            options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br");
            options.SetRequestHeader("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            //options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.77 Safari/537.36 Edg/91.0.864.41");
            options.SetRequestHeader("Cache-Control", "no-cache");
            //毫无卵用的参数
            //options.KeepAliveInterval = TimeSpan.FromSeconds(5);
            var host = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";
            await webSocket.ConnectAsync(new Uri($"{host}?TrustedClientToken={TRUSTED_CLIENT_TOKEN}&ConnectionId={Utils.GetUUID()}"), token);
            //await webSocket.ConnectAsync(new Uri($"ws://speech.est.institute/consumer/speech/synthesize/readaloud/edge/v1?TrustedClientToken={TRUSTED_CLIENT_TOKEN}&ConnectionId={Helpers.GetUUID()}"), CancellationToken.None);
            //stopwatch.Stop();
            Log.Debug($"EstablishConnection Time={ stopwatch.Elapsed.TotalMilliseconds}ms");
            stopwatch.Reset(); ;
        }


        public async Task<Result> SynthesisAsync(string str, string voice = "zh-CN-XiaoxiaoNeural", string pitch = "+0Hz", string rate = "+0%", string volume = "+100%")
        {
            var message = "X-Timestamp:" + Utils.GetFormatedDate() + "\r\nContent-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n";
            message += "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":" + "\"false\"" + ",\"wordBoundaryEnabled\":" + "\"false\"" + "}," + "\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}\r\n";
            //var s = new WsItem() { message = message, SSMLPayload = new SSML(str, voice, pitch, rate, volume) };
            var SSMLPayload = new SSML(str, voice, pitch, rate, volume);
            var retryTimes = 0;
            await slimlock.WaitAsync();
            while (retryTimes < 3)
            {
                try
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    Log.Debug($"开始TTS转换...");
                    var token = CancellationToken.None;

                    switch (webSocket.State)
                    {
                        //Steal Fox's Code
                        case WebSocketState.None:
                            await EstablishConnectionAsync(token);
                            break;
                        case WebSocketState.Connecting:
                        case WebSocketState.Open:
                            // All good
                            break;
                        case WebSocketState.CloseSent:
                        case WebSocketState.CloseReceived:
                        case WebSocketState.Closed:
                            webSocket.Abort();
                            webSocket.Dispose();
                            webSocket = new ClientWebSocket();
                            await EstablishConnectionAsync(token);
                            break;
                        case WebSocketState.Aborted:
                            webSocket.Dispose();
                            webSocket = new ClientWebSocket();
                            await EstablishConnectionAsync(token);
                            break;
                        default:
                            break;
                    }

                    var audio = ReceiveAudioAsync(webSocket, SSMLPayload.RequestID, token);
                    await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, token);
                    await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(SSMLPayload.ToString())), WebSocketMessageType.Text, true, token);
                    while (!audio.IsCompleted)
                    {
                        await Task.Delay(10);
                    }
                    Log.Debug($"[EdegTTS]接收用时:{ stopwatch.Elapsed.TotalMilliseconds}ms");
                    //stopwatch.Reset(); stopwatch.Start();
                    if (audio.Result.Length == 0)
                        throw new IOException("Received Empty Aduio!");

                    if (!KeepConnection)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "NormalClosure", token);
                        webSocket.Abort();
                        webSocket.Dispose();
                    }

                    slimlock.Release();
                    return new Result()
                    {
                        Code = ResultCode.Success,
                        Data = audio.Result
                    };
                }
                catch (Exception ex)
                {
                    retryTimes++;
                    //服务器主动断开连接
                    if (ex.InnerException != null && ex.InnerException is SocketException && ((SocketException)ex.InnerException).SocketErrorCode == SocketError.ConnectionReset)
                        Log.Error("[EdgeTTS]Azure服务器主动断开连接");
                    else
                        Log.Error("[EdgeTTS]Azure服务器连接异常");

                    Log.Error(ex.ToString());
                    Log.Information($"[EdegTTS]连接失败，开始第{retryTimes}次重试 :(");
                }
                //finally
                //{
                //    Log.D($"[EdegTTS]TTS转换结束");
                //}
            }
            Log.Information($"[EdegTTS]重试失败 :(");
            slimlock.Release();
            return new Result()
            {
                Code = ResultCode.Fail
            };
        }

        private async Task<MemoryStream> ReceiveAudioAsync(WebSocket client, string requestId, CancellationToken token)
        {
            //https://github.com/Noisyfox/ACT.FoxTTS/blob/master/ACT.FoxTTS/ACT.FoxTTS/engine/edge/EdgeTTSEngine.cs
            //Steal Fox's code
            var buffer = new MemoryStream(10 * 1024);
            var audioBuffer = new MemoryStream();
            var state = SessionState.NotStarted;
            //throw new IOException("Test");
            while (true)
            {
                if (client.CloseStatus == WebSocketCloseStatus.EndpointUnavailable ||
                    client.CloseStatus == WebSocketCloseStatus.InternalServerError ||
                    client.CloseStatus == WebSocketCloseStatus.EndpointUnavailable)
                {
                    return audioBuffer;
                }
                var array = new byte[5 * 1024];
                var receive = await client.ReceiveAsync(new ArraySegment<byte>(array), token);
                if (receive.Count == 0)
                    continue;
                //处理非完整消息
                buffer.Write(array, (int)buffer.Position, receive.Count);
                if (receive.EndOfMessage == false)
                {
                    continue;
                }

                array = buffer.ToArray();
#if DEBUG
                //模拟烂网，不是很靠谱
                //var ra = new Random();
                //if (ra.NextDouble() > 0.9)
                //{
                //    webSocket.Abort();
                //    Log.D("烂网模拟");
                //}

#endif
                //continue;
                switch (receive.MessageType)
                {
                    case WebSocketMessageType.Text:
                        var content = Encoding.UTF8.GetString(array, 0, array.Length);
                        //Log.D(content);
                        if (!content.StartsWith($"X-RequestId:{requestId}"))
                            throw new IOException($"Unexpected request id during streaming:{content}");
                        switch (state)
                        {
                            case SessionState.NotStarted:
                                if (content.Contains("Path:turn.start"))
                                {
                                    state = SessionState.TurnStarted;
                                }
                                break;
                            case SessionState.TurnStarted:
                                if (content.Contains("Path:turn.end"))
                                {
                                    throw new IOException("Unexpected turn.end");
                                }
                                else if (content.Contains("Path:turn.start"))
                                {
                                    throw new IOException("Turn already started");
                                }
                                break;
                            case SessionState.Streaming:
                                if (content.Contains("Path:turn.end"))
                                {
                                    // All done
                                    return audioBuffer;
                                }
                                else
                                {
                                    throw new IOException($"Unexpected message during streaming: {content}");
                                }
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        break;

                    case WebSocketMessageType.Binary:
                        if (array.Length < 2)
                            throw new IOException("Message too short");
                        var headerLen = (array[0] << 8) + array[1];
                        if (buffer.Length < 2 + headerLen)
                            throw new IOException("Message too short");
                        var header = Encoding.UTF8.GetString(array, 2, headerLen);
                        //Log.D(header);
                        if (!header.StartsWith($"X-RequestId:{requestId}"))
                            throw new IOException("Unexpected request id during streaming");
                        switch (state)
                        {
                            case SessionState.NotStarted:
                                throw new IOException($"Unexpected Binary");
                            case SessionState.TurnStarted:
                            case SessionState.Streaming:
                                if (!header.EndsWith("Path:audio\r\n"))
                                {
                                    throw new IOException($"Unexpected Binary with header: {header}");
                                }
                                state = SessionState.Streaming;
                                audioBuffer.Write(array, headerLen + 2, receive.Count - headerLen - 2);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        break;

                    case WebSocketMessageType.Close:
                        throw new IOException("Unexpected closing of connection");
                }
                buffer = new MemoryStream();
            }
        }

        public void GetVoiceList()
        {
            HttpWebRequest req = WebRequest.CreateHttp($"https://speech.platform.bing.com/consumer/speech/synthesize/readaloud/voices/list?trustedclienttoken={TRUSTED_CLIENT_TOKEN}");
            req.Method = "GET";
            req.Headers.Add("Authority", "speech.platform.bing.com");
            req.Headers.Add("Sec-CH-UA", "\" Not;A Brand\";v=\"99\", \"Microsoft Edge\";v=\"91\", \"Chromium\";v=\"91\"");
            req.Headers.Add("Sec-CH-UA-Mobile", "?0");
            req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.77 Safari/537.36 Edg/91.0.864.41");
            req.Headers.Add("Accept", "*/*");
            req.Headers.Add("Sec-Fetch-Site", "none");
            req.Headers.Add("Sec-Fetch-Mode", "cors");
            req.Headers.Add("Sec-Fetch-Dest", "empty");
            req.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            req.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            var res = req.GetResponse();
            var resString = string.Empty;
            using (var steam = new GZipStream(res.GetResponseStream(), CompressionMode.Decompress))
            {
                var sr = new StreamReader(steam, Encoding.UTF8);
                resString = sr.ReadToEnd();

            }
            var voiceList = JsonSerializer.Deserialize<List<Voice>>(resString);
            voiceList.ForEach(x => Debug.WriteLine(x.ShortName));
            //Voices = voiceList;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    webSocket.Abort();
                    webSocket.Dispose();
                }
                //tokenSource.Cancel();
                ConnectionKeeper.Dispose();
                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~EdgeTTS()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
