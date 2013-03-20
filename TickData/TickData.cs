using System;
using System.Collections.Generic;

namespace DataWrangler
{
    // Data container for both live data event and historical tick data
    public enum Type { Trade = 0, Ask = 1, Bid = 2 }

    public class TickData
    {
        public string Security = String.Empty;
        public uint SecurityID = 0;
        public object SecurityObj = null;
        public DateTime TimeStamp = DateTime.MinValue;
        public Type Type;
        public double Price = 0;
        public uint Size = 0;
        public Dictionary<string, string> Codes;
    }

}
