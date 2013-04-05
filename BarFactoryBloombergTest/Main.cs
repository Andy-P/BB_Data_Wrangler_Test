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
        //private readonly BloombergRTDataProvider _blbgFeed = new BloombergRTDataProvider();

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
            _histFeed.AddDataInterval(new DateTime(2013, 3, 6, 23, 59, 0), new DateTime(2013, 3, 7, 0, 5, 0));

            var NKH3 = new DataFactory(new Security("NKH3 Index", 12, Security.SecurityType.IndexFuture));
            _histFeed.AddSecurity(NKH3);

            var NOH3 = new DataFactory(new Security("NOH3 Index", 17, Security.SecurityType.IndexFuture));
            _histFeed.AddSecurity(NOH3);

            var NIH3 = new DataFactory(new Security("NIH3 Index", 21, Security.SecurityType.IndexFuture));
            _histFeed.AddSecurity(NIH3);

            var TPH3 = new DataFactory(new Security("TPH3 Index", 26, Security.SecurityType.IndexFuture));
            _histFeed.AddSecurity(TPH3);

            var JBH3 = new DataFactory(new Security("JBH3 Comdty", 31, Security.SecurityType.IndexFuture));
            _histFeed.AddSecurity(JBH3);

            var JPY = new DataFactory(new Security("JPY Curncy", 9, Security.SecurityType.IndexFuture));
            _histFeed.AddSecurity(JPY);

            _histFeed.LoadHistoricalData();


            _markets = new MarketAggregator();

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

            //var NIM3 = new DataFactory(new Security("NIM3 Index", 22, Security.SecurityType.IndexFuture));
            //_markets.AddSecurity(NIM3);
            //_blbgFeed.AddSecurity(NIM3);
            //NIM3.SubscribeToDataFeedEvents(_blbgFeed);
            //NIM3.AddReferenceToMarkets(_markets);
            //NIM3.LogEachTick = true;

            //var JBM3 = new DataFactory(new Security("JBM3 Comdty", 32, Security.SecurityType.IndexFuture));
            //_markets.AddSecurity(JBM3);
            //_blbgFeed.AddSecurity(JBM3);
            //JBM3.SubscribeToDataFeedEvents(_blbgFeed);
            //JBM3.AddReferenceToMarkets(_markets);
            //JBM3.LogEachTick = true;

            //blbgFeed.Subscribe();
        }
 
    }
}
