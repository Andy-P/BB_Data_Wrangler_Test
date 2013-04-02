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
        SortedDictionary<DateTime, Dictionary<Security, List<TickData>>> rawTickData 
            = new SortedDictionary<DateTime, Dictionary<Security, List<TickData>>>();

        private string dsPath = "TickData.qbd";

        private Dictionary<string, object> securities = new Dictionary<string, object>();
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

        public void AddSecurity(string security, object dataFactoryObject)
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

        public void getData()
        {
            foreach (DataInterval interval in Intervals)
            {
                foreach (var sec in securities)
                {
                    DataTable data = histDS.getTickDataSeries(sec.Key, interval.start, interval.end);
                    
                }
                
            }
        }

        public struct DataInterval
        {
            public DateTime start;
            public DateTime end;            
        }
    }
}
