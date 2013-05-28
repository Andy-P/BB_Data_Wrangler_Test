using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DataWrangler;


namespace BarFactoryBloombergTest
{
    public partial class Main : Form
    {
        private readonly BloombergRTDataProvider _blbgFeed = new BloombergRTDataProvider();
        private HistoricalDataHandler _histFeed;
        private MarketAggregator _markets;

        public Main()
        {
            InitializeComponent();
            initializeDataHandler();
        }

        private void initializeDataHandler()
        {
            const string dsPath = "TickData.qbd";
            _histFeed = new HistoricalDataHandler(dsPath);
            _markets = new MarketAggregator();

            _histFeed.AddDataInterval(new DateTime(2013, 3, 4, 23, 59, 50), new DateTime(2013, 3, 5, 0, 1, 0));
            _histFeed.AddDataInterval(new DateTime(2013, 3, 5, 23, 59, 44), new DateTime(2013, 3, 6, 0, 1, 0));
            //_histFeed.AddDataInterval(new DateTime(2013, 3, 6, 23, 59, 50), new DateTime(2013, 3, 7, 0, 1, 0));
            //_histFeed.AddDataInterval(new DateTime(2013, 5, 15, 23, 59, 59), new DateTime(2013, 5, 18, 0, 0, 0));
            //_histFeed.AddDataInterval(new DateTime(2013, 5, 5, 23, 59, 59), new DateTime(2013, 3, 6, 0, 0, 10));

            //var NKH3 = new DataFactory(new Security("NKM3 Index", 13, Security.SecurityType.IndexFuture));
            ////_histFeed.AddSecurity(NKH3);
            //_markets.AddSecurity(NKH3);
            //NKH3.AddReferenceToMarkets(_markets);

            var NOH3 = new DataFactory(new Security("NOH3 Index", 17, Security.SecurityType.IndexFuture));
            _histFeed.AddSecurity(NOH3);
            _markets.AddSecurity(NOH3);
            NOH3.AddReferenceToMarkets(_markets);

            var NIH3 = new DataFactory(new Security("NIH3 Index", 21, Security.SecurityType.IndexFuture));
            _histFeed.AddSecurity(NIH3);
            _markets.AddSecurity(NIH3);
            NIH3.AddReferenceToMarkets(_markets);

            //NIH3.LogEachTick = true;

            //var TPH3 = new DataFactory(new Security("TPH3 Index", 26, Security.SecurityType.IndexFuture));
            //_histFeed.AddSecurity(TPH3);
            //_markets.AddSecurity(TPH3);
            //TPH3.AddReferenceToMarkets(_markets);

            //var JBH3 = new DataFactory(new Security("JBH3 Comdty", 31, Security.SecurityType.IndexFuture));
            //_histFeed.AddSecurity(JBH3);
            //_markets.AddSecurity(JBH3);
            //JBH3.AddReferenceToMarkets(_markets);

            //var JPY = new DataFactory(new Security("JPY Curncy", 9, Security.SecurityType.IndexFuture));
            //_histFeed.AddSecurity(JPY);
            //_markets.AddSecurity(JPY);
            //JPY.AddReferenceToMarkets(_markets);

            _histFeed.LoadHistoricalData();

            DateTime start = DateTime.Now;
            _histFeed.PlayBackData();
            TimeSpan time = DateTime.Now - start;
            Console.WriteLine("Playback time {0} seconds", time.Seconds.ToString());

            const string filePath = @"C:\Users\Andre\Documents\BBDataSource\Market Aggregator OutPut\";
            _markets.BatchWriteOutData(MarketAggregator.OutPutType.FlatFile, MarketAggregator.OutPutMktMode.BothMkts, filePath, 11);



            //var NKM3 = new DataFactory(new Security("NKM3 Index", 13, Security.SecurityType.IndexFuture));
            //_markets.AddSecurity(NKM3);
            //_blbgFeed.AddSecurity(NKM3);
            //NKM3.SubscribeToDataFeedEvents(_blbgFeed);
            //NKM3.AddReferenceToMarkets(_markets);
            //NKM3.LogEachTick = true;

            //var NOM3 = new DataFactory(new Security("NOM3 Index", 18, Security.SecurityType.IndexFuture));
            //_markets.AddSecurity(NOM3);
            //_blbgFeed.AddSecurity(NOM3);
            //NOM3.SubscribeToDataFeedEvents(_blbgFeed);
            //NOM3.AddReferenceToMarkets(_markets);
            //NOM3.LogEachTick = true;

            //var JPY = new DataFactory(new Security("JPY Curncy", 9, Security.SecurityType.Curncy));
            //_markets.AddSecurity(JPY);
            //_blbgFeed.AddSecurity(JPY.Security, JPY);
            //JPY.SubscribeToDataFeedEvents(_blbgFeed);
            //JPY.AddReferenceToMarkets(_markets);
            //JPY.LogEachTick = true;

            ////var NIM3 = new DataFactory(new Security("NIM3 Index", 22, Security.SecurityType.IndexFuture));
            ////_markets.AddSecurity(NIM3);
            ////_blbgFeed.AddSecurity(NIM3);
            ////NIM3.SubscribeToDataFeedEvents(_blbgFeed);
            ////NIM3.AddReferenceToMarkets(_markets);
            ////NIM3.LogEachTick = true;

            //var JBM3 = new DataFactory(new Security("JBM3 Comdty", 32, Security.SecurityType.IndexFuture));
            //_markets.AddSecurity(JBM3);
            //_blbgFeed.AddSecurity(JBM3);
            //JBM3.SubscribeToDataFeedEvents(_blbgFeed);
            //JBM3.AddReferenceToMarkets(_markets);
            //JBM3.LogEachTick = true;

            //_blbgFeed.Subscribe();
        }
 
    }
}
