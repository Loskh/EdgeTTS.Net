using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdgeTTS
{
    public class SSML
    {
        public string Voice;
        public string Pitch;
        public string Rate;
        public string Volume;
        public string Sentence;
        public string RequestID;

        public SSML(string sentence, string voice, string pitch, string rate, string volume)
        {
            Voice = voice;
            Pitch = pitch;
            Rate = rate;
            Volume = volume;
            Sentence = sentence;
            RequestID = Utils.GetUUID();
        }

        public override string ToString()
        {
            var message = "X-RequestId:" + RequestID + "\r\nContent-Type:application/ssml+xml\r\n";
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
}
