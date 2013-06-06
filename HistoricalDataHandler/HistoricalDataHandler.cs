using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace DataWrangler
{
    public class HistoricalDataHandler
    {
        private readonly QRDataSource.QRDataSource _histDs = new QRDataSource.QRDataSource();
        public bool DsInitialized = false;
        public bool DsConnected = false;

        // cached historical data
        public SortedDictionary<DateTime, Dictionary<DataFactory, List<TickData>>> CachedTickData
            = new SortedDictionary<DateTime, Dictionary<DataFactory, List<TickData>>>();

        private readonly Dictionary<DataFactory, MktSummaryEvent>
            _mktSummaryEvents = new Dictionary<DataFactory, MktSummaryEvent>();

        private readonly Dictionary<string, DataFactory> _securities = new Dictionary<string, DataFactory>();
        private readonly List<DataInterval> _intervals = new List<DataInterval>();

        public HistoricalDataHandler(string dsPath)
        {            
            _histDs.loadDataSource(dsPath);
            DsInitialized = _histDs.initialized;

            if (_histDs.initialized)
            {
                DsConnected = _histDs.getSQLconnection();
            }

            Console.WriteLine("HistoricalDataHandler DSInitialized = {0}", DsInitialized);
            Console.WriteLine("HistoricalDataHandler DSConnected = {0}", DsConnected);
            Console.WriteLine(" ");
        }

        #region Historical Data Setup & initialization

        public void AddSecurity(string security, DataFactory dataFactoryObject)
        {
            if (security.Trim().Length > 0)
            {
                _securities.Add(security, dataFactoryObject);
            }
        }

        public void AddSecurity(DataFactory dataFactoryObject)
        {
            _securities.Add(dataFactoryObject.SecurityName, dataFactoryObject);
        }

        public void AddDataInterval(DateTime start, DateTime end)
        {
            if (end > start)
            {
                _intervals.Add(new DataInterval { Start = start, End = end });
            }
            else
            {
                Console.WriteLine("Bad interval! end {0} <= start {1}! ", end, start);
            }
        }

        public struct DataInterval
        {
            public DateTime Start;
            public DateTime End;
        }

        #endregion

        #region Historical Data Caching

        public void LoadHistoricalData()
        {
            foreach (DataInterval interval in _intervals)
            {

                Console.WriteLine("Requesting data from {0} to {1}", interval.Start.ToLongTimeString(), interval.End.ToLongTimeString());

                foreach (var sec in _securities)
                {

                    Console.WriteLine(" ");
                    Console.WriteLine("Requesting {0} historical data", sec.Key);
                    DataTable data = _histDs.getTickDataSeries(sec.Key, interval.Start, interval.End);
                    if (data.Rows.Count > 0) ParseDataTable(sec.Value, data);
                }                
            }
        }

        private void ParseDataTable(DataFactory factory, DataTable dt)
        {
            Console.WriteLine("Parsing {0} DataTable({1} rows)", factory.SecurityName, dt.Rows.Count.ToString());

            if (!_mktSummaryEvents.ContainsKey(factory))
                _mktSummaryEvents.Add(factory, new MktSummaryEvent {Complete = false});
            MktSummaryEvent mktSummary = _mktSummaryEvents[factory];

            foreach (DataRow row in dt.Rows)
            {
                TickData tick = DataRowToTickData(factory, row);
                if (tick != null)
                {
                    if (!mktSummary.Complete)
                    {
                        mktSummary = PrepareMktSummaryEvent(factory, mktSummary, tick);
                        _mktSummaryEvents[factory] = mktSummary;
                    }
                    else
                    {
                        AddHistDataToCache(factory, tick);
                    }
                }
            }
        }

        private static TickData DataRowToTickData(DataFactory factory, DataRow row)
        {
            Type type;
            DateTime timeStamp;
            Double price;
            uint size;
            Dictionary<string, string> codes = null;
            TickData tick = null;

            // try parse dataRow for tick data values
            if (Enum.TryParse(row[0].ToString(), out type))
                if (DateTime.TryParse(row[1].ToString(), out timeStamp))
                    if (Double.TryParse(row[2].ToString(), out price))
                        if (uint.TryParse(row[3].ToString(), out size))
                        {
                            if ((price > 0) || (price < 0))
                            {
                                // if there are any codes, add to the tickData event
                                if ((row[4].ToString() != String.Empty) || (row[5].ToString() != String.Empty))
                                {
                                    codes = GetCodes(row[4].ToString(), row[5].ToString(), type);
                                }

                                // create a new tick data event
                                tick = new TickData
                                {
                                    Type = type,
                                    TimeStamp = timeStamp,
                                    Price = price,
                                    Size = size,
                                    Codes = codes,
                                    Security = factory.SecurityObj.Name,
                                    SecurityObj = factory.SecurityObj,
                                    SecurityID = factory.SecurityObj.Id
                                };

                                //Console.WriteLine(tick.ToString());
                            }
                        }

            return tick;
        }

        private static MktSummaryEvent PrepareMktSummaryEvent(DataFactory factory, MktSummaryEvent mktSummary, TickData tick)
        {
            switch (tick.Type)
            {
                case Type.Ask:
                    if (mktSummary.Ask == null)
                    {
                        mktSummary.Ask = tick;
                        if (tick.TimeStamp > mktSummary.EventTime) mktSummary.EventTime = tick.TimeStamp;
                        mktSummary = CheckForSyntheticTradeCondition(factory, mktSummary);
                    }
                    break;
                case Type.Bid:
                    if (mktSummary.Bid == null)
                    {
                        mktSummary.Bid = tick;
                        if (tick.TimeStamp > mktSummary.EventTime) mktSummary.EventTime = tick.TimeStamp;
                        mktSummary = CheckForSyntheticTradeCondition(factory, mktSummary);
                    }
                    break;
                case Type.Trade:
                    if (mktSummary.Trade == null)
                    {
                        mktSummary.Trade = tick;
                        if (tick.TimeStamp > mktSummary.EventTime) mktSummary.EventTime = tick.TimeStamp;
                        mktSummary = CheckForSyntheticTradeCondition(factory, mktSummary);
                    }
                    break;
            }

            if ((mktSummary.Ask != null) && (mktSummary.Bid != null) && mktSummary.Trade != null)
            {
                mktSummary.Complete = true;
                Console.WriteLine("Mkt summary {0} {1} ask {2} bid {3} trade {4}", tick.Security,
                        mktSummary.EventTime.ToLongTimeString(),
                        mktSummary.Ask.Price, mktSummary.Bid.Price, mktSummary.Trade.Price);
            }

            return mktSummary;

        }

        private static MktSummaryEvent CheckForSyntheticTradeCondition(DataFactory factory, MktSummaryEvent mktSummary)
        {
            if ((mktSummary.Ask != null) && (mktSummary.Bid != null))
            {
                mktSummary.Trade = new TickData
                {
                    Type = Type.Trade,
                    TimeStamp = mktSummary.EventTime,
                    Price = (mktSummary.Bid.Price + mktSummary.Ask.Price) / 2,
                    Size = 0,
                    Codes = null,
                    Security = factory.SecurityObj.Name,
                    SecurityObj = factory.SecurityObj,
                    SecurityID = factory.SecurityObj.Id
                };
            }

            return mktSummary;
        }
        
        private static Dictionary<string, string> GetCodes(string condCode, string exchCode, Type type)
        {
            var codes = new Dictionary<string, string>();

            if (exchCode != String.Empty)
            {
                switch (type)
                {
                    case Type.Ask:
                        codes.Add("EXCH_CODE_LAST", exchCode);
                        break;
                    case Type.Bid:
                        codes.Add("EXCH_CODE_BID", exchCode);
                        break;
                    case Type.Trade:
                        codes.Add("EXCH_CODE_ASK", exchCode);
                        break;
                }
            }
            else
            {
                if (condCode != String.Empty)
                {
                    codes.Add("COND_CODE", condCode);
                }
            }

            if (codes.Count == 0) codes = null;

            return codes;
        }

        private void AddHistDataToCache(DataFactory factory, TickData tick)
        {
            if (!CachedTickData.ContainsKey(tick.TimeStamp))
                CachedTickData.Add(tick.TimeStamp, new Dictionary<DataFactory, List<TickData>>());

            Dictionary<DataFactory, List<TickData>> timeInterval = CachedTickData[tick.TimeStamp];

            if (!timeInterval.ContainsKey(factory))
                timeInterval.Add(factory, new List<TickData>());
            List<TickData> tickData = timeInterval[factory];

            tickData.Add(tick);
        }
        
        private struct MktSummaryEvent
        {
            public DateTime EventTime;
            public TickData Bid;
            public TickData Ask;
            public TickData Trade;
            public bool Complete;
        }

        #endregion

        #region Historical Data Playback

        public void PlayBackData()
        {
            foreach (var secondsBin in CachedTickData)
            {
                foreach (var security in secondsBin.Value)
                {
                    DataFactory factory = security.Key;
                    if (_mktSummaryEvents.ContainsKey(factory))
                    {
                        MktSummaryEvent mktSummaryEvent = _mktSummaryEvents[factory];
                        if (mktSummaryEvent.EventTime <= secondsBin.Key)
                        {
                            factory.FirstTick(mktSummaryEvent.Bid, mktSummaryEvent.Ask, mktSummaryEvent.Trade);
                            _mktSummaryEvents.Remove(factory);
                        }
                    }

                    // begin play back only after we have a summary event for each security
                    if (_mktSummaryEvents.Count < 1)
                    {
                        foreach (TickData tickData in security.Value)
                        {
                            factory.NewTick(tickData);
                        }
                    }
                }                                
            }
        }

        
        #endregion
    }
}