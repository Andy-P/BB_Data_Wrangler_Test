using System;
using System.Collections.Generic;
using System.Text;

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

        public override string ToString()
        {
            StringBuilder output = new StringBuilder();
            output.Append(Security).Append(" ");
            output.Append(Type.ToString()).Append(" ");
            output.Append(TimeStamp.ToString("yyyy/MM/dd HH:mm:ss.ffffff")).Append(" ");
            output.Append(Price.ToString()).Append(" ");
            output.Append(Price.ToString()).Append(" ");
            output.Append(Price.ToString()).Append(" ");
            output.Append(Size.ToString()).Append(" ");
            return output.ToString();
        }

        public string ToStringAllCodes()
        {
            if (Codes == null) return String.Empty;

            StringBuilder codesToStr = new StringBuilder();
            foreach (var Code in Codes)
                codesToStr.Append(Code.Value).Append("|");

            return codesToStr.ToString();
        }
    }
}