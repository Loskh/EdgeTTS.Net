using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EdgeTTS
{
    public static class Utils
    {


        public static string GetUUID() => Guid.NewGuid().ToString().Replace("-", "");
        public static string GetFormatedDate()
        {
            return DateTime.UtcNow.ToString("ddd MMM yyyy H:m:s", CultureInfo.CreateSpecificCulture("en-GB")) + " GMT+0000 (Coordinated Universal Time)";
        }
    }
    public static class Extension
    {
        //快乐CV，面向爆栈编程
        //https://stackoverflow.com/questions/25400610/most-efficient-way-to-find-pattern-in-byte-array
        /// <summary>Looks for the next occurrence of a sequence in a byte array</summary>
        /// <param name="array">Array that will be scanned</param>
        /// <param name="start">Index in the array at which scanning will begin</param>
        /// <param name="sequence">Sequence the array will be scanned for</param>
        /// <returns>
        ///   The index of the next occurrence of the sequence of -1 if not found
        /// </returns>
        public static int IndexOf(this byte[] array, int start, byte[] sequence)
        {
            int end = array.Length - sequence.Length; // past here no match is possible
            byte firstByte = sequence[0]; // cached to tell compiler there's no aliasing

            while (start <= end)
            {
                // scan for first byte only. compiler-friendly.
                if (array[start] == firstByte)
                {
                    // scan for rest of sequence
                    for (int offset = 1; ; ++offset)
                    {
                        if (offset == sequence.Length)
                        { // full sequence matched?
                            return start;
                        }
                        else if (array[start + offset] != sequence[offset])
                        {
                            break;
                        }
                    }
                }
                ++start;
            }

            // end of array reached without match
            return -1;
        }
    }
}
