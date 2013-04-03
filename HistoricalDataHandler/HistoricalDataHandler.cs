using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using QRDataSource;

namespace DataWrangler
{
    public class HistoricalDataHandler
    {
        private QRDataSource.QRDataSource histDS = new QRDataSource.QRDataSource();
        public bool dsInitialized = false;
        public bool dsConnected = false;

        // cached historical data
        public SortedDictionary<DateTime, Dictionary<DataFactory, List<TickData>>> CachedTickData
            = new SortedDictionary<DateTime, Dictionary<DataFactory, List<TickData>>>();

        private Dictionary<DataFactory, MktSummaryEvent>
            MktSummaryEvents = new Dictionary<DataFactory, MktSummaryEvent>();

        private Dictionary<DataFactory, DateTime>
            MktSummaryEventsTiming = new Dictionary<DataFactory, DateTime>();

        private string dsPath = "TickData.qbd";

        private Dictionary<string, DataFactory> securities = new Dictionary<string, DataFactory>();
        private List<DataInterval> Intervals = new List<DataInterval>();

        public HistoricalDataHandler(string dsPath)
        {            
            histDS.loadDataSource(dsPath);
            dsInitialized = histDS.initialized;

            if (histDS.initialized)
            {
                this.dsPath = dsPath;
                dsConnected = histDS.getSQLconnection();
            }

            Console.WriteLine("hist DSInitialized = {0}", dsConnected.ToString());
            Console.WriteLine("hist DSConnected = {0}", dsConnected.ToString());
            Console.WriteLine(" ");
        }

        #region Historical Data Setup & initialization

        public void AddSecurity(string security, DataFactory dataFactoryObject)
        {
            if (security.Trim().Length > 0)
            {
                securities.Add(security, dataFactoryObject);
            }
        }

        public void AddSecurity(DataFactory dataFactoryObject)
        {
            securities.Add(dataFactoryObject.Security, dataFactoryObject);
        }

        public void AddDataInterval(DateTime start, DateTime end)
        {
            if (end > start)
            {
                Intervals.Add(new DataInterval() { start = start, end = end });
            }
            else
            {
                Console.WriteLine("Bad interval! end {0} <= start {1}! ", end, start);
            }
        }

        public struct DataInterval
        {
            public DateTime start;
            public DateTime end;
        }

        #endregion

        #region Historical Data Caching

        public void LoadHistoricalData()
        {
            foreach (DataInterval interval in Intervals)
            {

                Console.WriteLine("Requesting data from {0} to {1}", interval.start.ToLongTimeString(), interval.end.ToLongTimeString());

                foreach (var sec in securities)
                {

                    Console.WriteLine(" ");
                    Console.WriteLine("Requesting {0} historical data", sec.Key);
                    DataTable data = histDS.getTickDataSeries(sec.Key, interval.start, interval.end);
                    if (data.Rows.Count > 0) ParseDataTable(sec.Value, data);
                }                
            }
        }

        private void ParseDataTable(DataFactory factory, DataTable dt)
        {
            Console.WriteLine("Parsing {0} DataTable({1} rows)", factory.Security, dt.Rows.Count.ToString());
         
            if (!MktSummaryEvents.ContainsKey(factory))
                MktSummaryEvents.Add(factory, new MktSummaryEvent() { Complete = false});
             MktSummaryEvent MktSummary = MktSummaryEvents[factory];

            foreach (DataRow row in dt.Rows)
            {
                TickData tick = dataRowToTickData(factory, row);
                if (tick != null)
                {
                    if (!MktSummary.Complete)
                        MktSummary = prepareMktSummaryEvent(factory, MktSummary, tick);

                    if (MktSummary.Complete)
                        addHistDataToCache(factory, tick);
                }
            }
        }

        private TickData dataRowToTickData(DataFactory factory, DataRow row)
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
                            if (price != 0)
                            {
                                // if there are any codes, add to the tickData event
                                if ((row[4].ToString() != String.Empty) || (row[5].ToString() != String.Empty))
                                {
                                    codes = getCodes(row[4].ToString(), row[5].ToString(), type);
                                }

                                // create a new tick data event
                                tick = new TickData()
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
                            }
                        }

            return tick;
        }

        private MktSummaryEvent prepareMktSummaryEvent(DataFactory factory, MktSummaryEvent MktSummary, TickData tick)
        {
            switch (tick.Type)
            {
                case Type.Ask:
                    if (MktSummary.Ask == null)
                    {
                        MktSummary.Ask = tick;
                        if (tick.TimeStamp > MktSummary.EventTime) MktSummary.EventTime = tick.TimeStamp;
                        MktSummary = checkForSyntheticTradeCondition(factory, MktSummary, tick);
                    }
                    break;
                case Type.Bid:
                    if (MktSummary.Bid == null)
                    {
                        MktSummary.Bid = tick;
                        if (tick.TimeStamp > MktSummary.EventTime) MktSummary.EventTime = tick.TimeStamp;
                        MktSummary = checkForSyntheticTradeCondition(factory, MktSummary, tick);
                    }
                    break;
                case Type.Trade:
                    if (MktSummary.Trade == null)
                    {
                        MktSummary.Trade = tick;
                        if (tick.TimeStamp > MktSummary.EventTime) MktSummary.EventTime = tick.TimeStamp;
                        MktSummary = checkForSyntheticTradeCondition(factory, MktSummary, tick);
                    }
                    break;
                default:
                    break;

            }

            if ((MktSummary.Ask != null) && (MktSummary.Bid != null) && (MktSummary.Trade != null))
            {
                MktSummary.Complete = true;
                Console.WriteLine("Mkt summary {0} {1} ask {2} bid {3} trd {4} complete {5}", tick.Security,
                        MktSummary.EventTime.ToLongTimeString(),
                        MktSummary.Ask.Price.ToString(),
                        MktSummary.Bid.Price.ToString(),
                        MktSummary.Trade.Price.ToString(),
                        MktSummary.Complete.ToString());
            }

            return MktSummary;

        }

        private MktSummaryEvent checkForSyntheticTradeCondition(DataFactory factory, MktSummaryEvent MktSummary, TickData tick)
        {
            if ((MktSummary.Ask != null) && (MktSummary.Bid != null))
            {
                //if (MktSummary.Ask.Price == MktSummary.Bid.Price)
                //{
                MktSummary.Trade = new TickData()
                {
                    Type = Type.Trade,
                    TimeStamp = MktSummary.EventTime,
                    Price = (MktSummary.Bid.Price + MktSummary.Ask.Price) / 2,
                    Size = 0,
                    Codes = null,
                    Security = factory.SecurityObj.Name,
                    SecurityObj = factory.SecurityObj,
                    SecurityID = factory.SecurityObj.Id
                };
                // }
            }

            return MktSummary;
        }

        private void addHistDataToCache(DataFactory factory, TickData tick)
        {
            if (!CachedTickData.ContainsKey(tick.TimeStamp))
                CachedTickData.Add(tick.TimeStamp, new Dictionary<DataFactory, List<TickData>>());

            Dictionary<DataFactory, List<TickData>> TimeInterval = CachedTickData[tick.TimeStamp];

            if (!TimeInterval.ContainsKey(factory))
                TimeInterval.Add(factory, new List<TickData>());
            List<TickData> tickData = TimeInterval[factory];

            tickData.Add(tick);
        }
        
        private Dictionary<string, string> getCodes(string condCode, string exchCode, Type type)
        {
            Dictionary<string, string> codes = new Dictionary<string, string>();

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
                    default:
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

        public struct MktSummaryEvent
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
                    foreach (TickData tickData in security.Value)
                    {
                        factory.NewTick(tickData);
                    } 
                }                                
            }
        }

        public void PrepSummaryDataEvent()
        {

        }
        
        #endregion
    }
}
