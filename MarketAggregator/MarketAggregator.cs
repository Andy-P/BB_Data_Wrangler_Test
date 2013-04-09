using System;
using System.Collections.Generic;

namespace DataWrangler
{
    public class MarketAggregator
    {
        public enum Mode { RealTime = 1, Historical = 0 }
        public Mode InputMode { get; set; }

        public enum OutPutMode { FlatFile, Xml, Binary, SqlTable }
        public OutPutMode ExporttMode { get; set; }


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
            // check if DateTime stamp exists.
            //  If not, create it then
            //  create a security entry for each security and fill it with last seconds state

            
            // if exsits, replace with latest data
        }

        public static void BatchWriteOutData(OutPutMode outPutMode)
        {
            switch (outPutMode)
            {
                case OutPutMode.FlatFile:
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

    }
}
