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
        SortedDictionary<DateTime, Dictionary<DataFactory, List<TickData>>> rawTickData
            = new SortedDictionary<DateTime, Dictionary<DataFactory, List<TickData>>>();

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

        public void AddDataIntervals(DateTime start, DateTime end)
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

        public void LoadHistoricalData()
        {
            getData();
        }

        private void getData()
        {
            foreach (DataInterval interval in Intervals)
            {
                foreach (var sec in securities)
                {
                    DataTable data = histDS.getTickDataSeries(sec.Key, interval.start, interval.end);
                    if (data.Rows.Count > 0) ParseDataTable(sec.Value, data);
                }                
            }
        }

        private void ParseDataTable(DataFactory factory, DataTable dt)
        {
            foreach (DataRow row in dt.Rows)
            {
                TickData tick = dataRowToTickData(factory, row);
                if( tick != null) addHistDataToCache(factory, tick);                            
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

            return tick;
        }

        private void addHistDataToCache(DataFactory factory, TickData tick)
        {
            if (!rawTickData.ContainsKey(tick.TimeStamp))
                rawTickData.Add(tick.TimeStamp, new Dictionary<DataFactory, List<TickData>>());

            Dictionary<DataFactory, List<TickData>> TimeInterval = rawTickData[tick.TimeStamp];

            if (!TimeInterval.ContainsKey(factory))
                TimeInterval.Add(factory, new List<TickData>());
            List<TickData> tickData = TimeInterval[factory];

            tickData.Add(tick);

            //rawTickData.Add(
            //   tick.TimeStamp,
            //   new Dictionary<DataFactory, List<TickData>>() { { factory, new List<TickData>() { tick } } });
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

        public struct DataInterval
        {
            public DateTime start;
            public DateTime end;            
        }
    }
}
