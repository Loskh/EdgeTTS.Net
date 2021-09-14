using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdgeTTS
{
    public enum ResultCode
    {
        Success,
        Fail,
        NetworkFail,
    }
    public class Result
    {
        public string Message { get; set; }

        public ResultCode Code { get; set; }

        public MemoryStream Data { get; set; }
    }
}