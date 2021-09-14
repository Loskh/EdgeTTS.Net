using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using EdgeTTS;
using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace EdgeTTS.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()  //输出到控制台
                .MinimumLevel.Verbose()
                .CreateLogger();
            var t = new EdgeTTS(true, true);
            var str = "在很久很久以前。世界上还不存在能向其祈祷的神明，或者说当时的人类既是神明。在他们生活的星球上、重叠着一个有[生命]游荡的领域。那个被称为以太界的领域、在不同的时代也有着各种各样的异名。在他们生活的时代，也同样——因为被认为是看不见的领域、死者的返还场所，所以也被称为[冥界]。冥界对身为神的人们来说，是非常亲近的存在.。就像水流从地面注入大海、在大海中产生云、之后再变作雨回到大地一样、冥界在生命的循环中也担任着非常重要的一环。但是，如果被问到冥界是否在他们的掌控中，所有人都会摇头否认吧。因为即使是他们，穷极智慧也只能窥见冥界的冰山一角，就算能从中使用一部分的力量，也无法控制在其中循环的生命。但是，在人类当中极少数、有被冥界爱着的人存在着。";
            //var str = "今天天气真的热";
            Say(t, str, "zh-CN-XiaoxiaoNeural");
            Console.ReadKey();

        }

        public static void Say(EdgeTTS t, string str, string voice)
        {
            var ms = t.SynthesisAsync(str, voice).Result.Data;
            var file = $"test-{voice}.mp3";
            FileStream fs = new FileStream(file, FileMode.OpenOrCreate);
            BinaryWriter w = new BinaryWriter(fs);
            w.Write(ms.ToArray()); ;
            fs.Close();

            var mMDeviceEnumerator = new MMDeviceEnumerator();
            var defaultDevice = mMDeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            Play(defaultDevice, file);
        }


        public static void Play(MMDevice mDevice, string waveFile)
        {
            using (var audioFile = new AudioFileReader(waveFile))
            using (var outputDevice = new WasapiOut(mDevice, AudioClientShareMode.Shared, false, 0))
            {
                outputDevice.Init(audioFile);
                outputDevice.Play();
                while (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
