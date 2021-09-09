using System;
using System.IO;
using System.Threading;
using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using EdgeTTS;

namespace EdgeTTS.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var t = new EdgeTTS();
            //t.GetVoiceList();
            //Thread.Sleep(5000);
            Say(t,"今天天气真的热", "zh-CN-XiaoxiaoNeural");
            Say(t,"今天天气真的热", "zh-CN-YunyangNeural");
            Say(t,"今天天气真的热", "zh-HK-HiuMaanNeural");
            Say(t,"今天天气真的热", "zh-TW-HsiaoChenNeural");
            Console.ReadKey();
        }

        public static void Say(EdgeTTS t,string str, string voice) {
            var ms = t.SayAsync(str, voice).Result;
            var file = $"{str}-{voice}.mp3";
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
