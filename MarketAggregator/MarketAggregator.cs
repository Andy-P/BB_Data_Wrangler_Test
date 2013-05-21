using System;
using System.Collections.Generic;
using System.Text;

namespace DataWrangler
{
    public class MarketAggregator
    {
        public enum Mode { RealTime = 1, Historical = 0 }
        public Mode InputMode { get; set; }

        public enum OutPutMode { FlatFile, Xml, Binary, SqlTable }
        public OutPutMode ExportMode { get; set; }

        public string OutputPath { get; set; }

        // main data repository
        public SortedDictionary<DateTime, Dictionary<Security, SortedDictionary<uint, MarketState>>>
            Markets = new SortedDictionary<DateTime, Dictionary<Security, SortedDictionary<uint, MarketState>>>();

        private DateTime _lastState = DateTime.MinValue;

        private readonly List<DataFactory> _securitites = new List<DataFactory>();

        public MarketAggregator()
        {
            InputMode = Mode.RealTime;
        }

        public void AddSecurity(DataFactory factory)
        {
            _securitites.Add(factory);
        }

        public void AddTickData(DataFactory factory, SortedDictionary<uint, MarketState> state, DateTime stateTime)
        {

            if (!Markets.ContainsKey(stateTime))
            {
                Markets.Add(stateTime, new Dictionary<Security, SortedDictionary<uint, MarketState>>());
            }

            lock (Markets[stateTime])
            {
                Dictionary<Security, SortedDictionary<uint, MarketState>> allMarketsAtTime = Markets[stateTime];


                foreach (DataFactory f in _securitites)
                {
                    // no market data for this security, for this time stamp exists
                    if (!allMarketsAtTime.ContainsKey(f.SecurityObj))
                    {
                        SortedDictionary<uint, MarketState> mktData = factory.Equals(f) ? state : f.GetCurrentOrBefore(stateTime);
                        allMarketsAtTime.Add(f.SecurityObj, mktData);
                    }
                    else // market data for this security, for this time stamp exists already
                    {
                        if (factory.Equals(f))
                        {
                            allMarketsAtTime[f.SecurityObj] = state;
                        }
                    }
                }

                if (_lastState < stateTime) _lastState = stateTime;

            }
        }

        public void BatchWriteOutData(OutPutMode outPutMode)
        {
            BatchWriteOutData(outPutMode, String.Empty, 0);
        }

        public void BatchWriteOutData(OutPutMode outPutMode, string filePath, int cutOffHour)
        {
            switch (outPutMode)
            {
                case OutPutMode.FlatFile:
                    WriteOutFlatFile(filePath, cutOffHour);
                    break;
                case OutPutMode.Xml:
                    break;
                case OutPutMode.Binary:
                    break;
                case OutPutMode.SqlTable:
                    break;
                default:
                    throw new ArgumentOutOfRangeException("outPutMode");
            }
        }

        private void WriteOutFlatFile(string filePath, int cutOffHour)
        {
            bool headerWritten = false;

            DateTime date = DateTime.MinValue;
            List<string> dataCache = new List<string>();
            StringBuilder fileName = new StringBuilder();
            StringBuilder header = new StringBuilder();
            
            foreach (var timeStamp in Markets)
            {
                // calculate the header using the tickdata's built-in funnction
                if (!headerWritten)
                {
                    foreach (var security in timeStamp.Value)
                    {
                        MarketState lastTick = security.Value[0];
                        header.Append(lastTick.GetHeadersString() + lastTick.GetTradesHeaderString(5));
                    }

                    Console.WriteLine(header.ToString());
                    headerWritten = true;
                }


                // Output a new file for each day. The end of each day is defined by a cutOffHour
                DateTime current = timeStamp.Key;
                if (date == DateTime.MinValue || ((current.Day != date.Day) && (current.Hour >= cutOffHour)))
                {
                    if (dataCache.Count > 0)
                        writeCacheToFile(fileName.ToString(), dataCache);
                    dataCache.Add(header.ToString());

                    date = current;
                    fileName.Clear();
                    fileName.Append(filePath);
                    fileName.Append(date.Year.ToString() + date.Month.ToString("00") + date.Day.ToString("00") + ".csv");
                    Console.WriteLine(fileName.ToString());

                }
                
                StringBuilder data = new StringBuilder();
                foreach (var security in timeStamp.Value)
                {
                    MarketState lastTick = security.Value[(uint)(security.Value.Count - 1)];
                    data.Append(MarketStateToString(lastTick) + ",");
                }

                dataCache.Add(data.ToString());
                Console.WriteLine(data.ToString());
            }

            if (dataCache.Count > 0)
                writeCacheToFile(fileName.ToString(), dataCache);
        }

        private void writeCacheToFile(string path, List<string> dataCache)
        {
            System.IO.File.WriteAllLines(path, dataCache);
            dataCache.Clear();
        }


        private string MarketStateToString(MarketState lastTick)
        {
            string output = lastTick.ToFlatFileStringAllData() + lastTick.ToFlatFileStringAllTrades(5);

            return output;
        }


    }
}
