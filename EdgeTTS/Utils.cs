using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdgeTTS.Net
{
    public static class Utils
    {


        public static string GetUUID() => Guid.NewGuid().ToString().Replace("-", "");
        public static string GetFormatedDate()
        {
            return DateTime.UtcNow.ToString("ddd MMM yyyy H:m:s", CultureInfo.CreateSpecificCulture("en-GB")) + " GMT+0000 (Coordinated Universal Time)";
        }
    }
}
