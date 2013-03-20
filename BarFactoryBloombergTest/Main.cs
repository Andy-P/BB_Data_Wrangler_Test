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
//using BarFactory = DataWrangler.BarFactory;

namespace BarFactoryBloombergTest
{
    public partial class Main : Form
    {
        BloombergRTDataProvider blbgFeed = new BloombergRTDataProvider();

        public Main()
        {
            InitializeComponent();
            initializeDataHandler();
        }

        private void initializeDataHandler()
        {
            MarketAggregator Markets = new MarketAggregator();

            DataFactory NKM3 = new DataFactory(new Security("NKM3 Index", 13, Security.SecurityType.IndexFuture));
            Markets.AddSecurity(NKM3);
            blbgFeed.AddSecurity(NKM3);
            NKM3.SubscribeToDataFeedEvents(blbgFeed);
            NKM3.AddReferenceToMarkets(Markets);
            NKM3.LogEachTick = true;

            DataFactory NOM3 = new DataFactory(new Security("NOM3 Index", 18, Security.SecurityType.IndexFuture));
            Markets.AddSecurity(NOM3);
            blbgFeed.AddSecurity(NOM3);
            NOM3.SubscribeToDataFeedEvents(blbgFeed);
            NOM3.AddReferenceToMarkets(Markets);
            //NOM3.LogEachTick = true;

            DataFactory JBM3 = new DataFactory(new Security("JBM3 Comdty", 18, Security.SecurityType.IndexFuture));
            Markets.AddSecurity(JBM3);
            blbgFeed.AddSecurity(JBM3);
            JBM3.SubscribeToDataFeedEvents(blbgFeed);
            JBM3.AddReferenceToMarkets(Markets);
            //JBM3.LogEachTick = true;

            DataFactory JPY = new DataFactory(new Security("USDJPY CURNCY", 9, Security.SecurityType.Curncy));
            Markets.AddSecurity(JPY);
            blbgFeed.AddSecurity(JPY.Security, JPY);
            JPY.SubscribeToDataFeedEvents(blbgFeed);
            JPY.AddReferenceToMarkets(Markets);

            //DataFactory NOM3 = new DataFactory(new Security("NOM3 Index", 18, Security.SecurityType.IndexFuture));
            //blbgFeed.AddSecurity(NOM3);
            //NOM3.SubscribeToDataFeedEvents(blbgFeed);

            //DataFactory JBH3 = new DataFactory(new Security("JBH3 Comdty", 31, Security.SecurityType.IndexFuture));
            //blbgFeed.AddSecurity(JBH3);
            //JBH3.SubscribeToDataFeedEvents(blbgFeed);

            //DataFactory JBM3 = new DataFactory(new Security("JBM3 Comdty", 32, Security.SecurityType.IndexFuture));
            //blbgFeed.AddSecurity(JBM3);
            //JBM3.SubscribeToDataFeedEvents(blbgFeed);

            blbgFeed.Subscribe();
        }
 
    }
}
