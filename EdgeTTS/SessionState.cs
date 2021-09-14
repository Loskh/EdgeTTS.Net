using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdgeTTS
{
    public enum SessionState
    {
        NotStarted,
        TurnStarted, // turn.start received
        Streaming, // audio binary started
    }
}
