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
        private BloombergRTDataProvider blbgFeed = new BloombergRTDataProvider();
        private HistoricalDataHandler histFeed;

        public Main()
        {
            InitializeComponent();
            initializeDataHandler();
        }

        private void initializeDataHandler()
        {
            string dsPath = "TickData.qbd";
            histFeed = new HistoricalDataHandler(dsPath);
            histFeed.AddDataInterval(new DateTime(2013, 3, 6, 23, 59, 0), new DateTime(2013, 3, 7, 6, 15, 0));

            DataFactory NKH3 = new DataFactory(new Security("NKH3 Index", 12, Security.SecurityType.IndexFuture));
            histFeed.AddSecurity(NKH3);

            DataFactory NOH3 = new DataFactory(new Security("NOH3 Index", 17, Security.SecurityType.IndexFuture));
            histFeed.AddSecurity(NOH3);

            DataFactory NIH3 = new DataFactory(new Security("NIH3 Index", 21, Security.SecurityType.IndexFuture));
            histFeed.AddSecurity(NIH3);

            DataFactory TPH3 = new DataFactory(new Security("TPH3 Index", 26, Security.SecurityType.IndexFuture));
            histFeed.AddSecurity(TPH3);

            DataFactory JBH3 = new DataFactory(new Security("JBH3 Comdty", 31, Security.SecurityType.IndexFuture));
            histFeed.AddSecurity(JBH3);
            
            DataFactory JPY = new DataFactory(new Security("JPY Curncy", 9, Security.SecurityType.IndexFuture));
            histFeed.AddSecurity(JPY);

            histFeed.LoadHistoricalData();


            MarketAggregator Markets = new MarketAggregator();

            //DataFactory NKM3 = new DataFactory(new Security("NKM3 Index", 13, Security.SecurityType.IndexFuture));
            //Markets.AddSecurity(NKM3);
            //blbgFeed.AddSecurity(NKM3);
            //NKM3.SubscribeToDataFeedEvents(blbgFeed);
            //NKM3.AddReferenceToMarkets(Markets);
            //NKM3.LogEachTick = true;

            //DataFactory NOM3 = new DataFactory(new Security("NOM3 Index", 18, Security.SecurityType.IndexFuture));
            //Markets.AddSecurity(NOM3);
            //blbgFeed.AddSecurity(NOM3);
            //NOM3.SubscribeToDataFeedEvents(blbgFeed);
            //NOM3.AddReferenceToMarkets(Markets);
            //NOM3.LogEachTick = true;

            //DataFactory JPY = new DataFactory(new Security("JPY Curncy", 9, Security.SecurityType.Curncy));
            //Markets.AddSecurity(JPY);
            //blbgFeed.AddSecurity(JPY.Security, JPY);
            //JPY.SubscribeToDataFeedEvents(blbgFeed);
            //JPY.AddReferenceToMarkets(Markets);
            //JPY.LogEachTick = true;

            //DataFactory NIM3 = new DataFactory(new Security("NIM3 Index", 22, Security.SecurityType.IndexFuture));
            //Markets.AddSecurity(NIM3);
            //blbgFeed.AddSecurity(NIM3);
            //NIM3.SubscribeToDataFeedEvents(blbgFeed);
            //NIM3.AddReferenceToMarkets(Markets);
            //NIM3.LogEachTick = true;

            //DataFactory JBM3 = new DataFactory(new Security("JBM3 Comdty", 32, Security.SecurityType.IndexFuture));
            //Markets.AddSecurity(JBM3);
            //blbgFeed.AddSecurity(JBM3);
            //JBM3.SubscribeToDataFeedEvents(blbgFeed);
            //JBM3.AddReferenceToMarkets(Markets);
            //JBM3.LogEachTick = true;

            //blbgFeed.Subscribe();
        }
 
    }
}
