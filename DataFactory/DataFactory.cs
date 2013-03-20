using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using DataWrangler;
using DataFeed = DataWrangler.BloombergRTDataProvider;

namespace DataWrangler
{
    public class DataFactory
    {
        private string security = String.Empty;
        private uint securityID = 0;
        private Security securityObj = null;
        private DateTime lastUpdate = DateTime.MinValue;
        private DateTime currentIntervalDT;
        private MarketAggregator markets;

        // read only properties
        public string Security { get { return security; } }
        public uint SecurityID { get { return securityID; } }
        public Security SecurityObj { get { return securityObj; } }
        public DateTime LastUpdate { get { return lastUpdate; } }
        public DateTime CurrentIntervalDT { get { return currentIntervalDT; } }
        public MarketState CurrentInterval { get { return getCurrentState(); } }
        public DataRow CurrentBarDataRow { get { return getCurrentBarAsDataRow(); } }

        bool mktInitialized = false;

        public bool LogEachTick = false;

        // index tracking latest value in time bin
        private Dictionary<DateTime, uint> marketDataLastest = new Dictionary<DateTime, uint>();

        // Main data repository
        private SortedDictionary<DateTime, SortedDictionary<uint, MarketState>> marketData
            = new SortedDictionary<DateTime, SortedDictionary<uint, MarketState>>();

        // read only access to market data.
        public SortedDictionary<DateTime, SortedDictionary<uint, MarketState>> MarketData { get { return marketData; } }

        private DataFeed blbgFeed;

        public DataFactory(Security security)
        {
            this.securityObj = security;
            this.security = security.Name;
            this.securityID = security.Id;
        }

        public void SubscribeToDataFeedEvents(DataFeed dataFeed)
        {
            blbgFeed = dataFeed;
            blbgFeed.BBRTDUpdate += new BloombergRTDataEventHandler(RTDataHandler);
        }

        public void AddReferenceToMarkets(MarketAggregator markets)
        {
            this.markets = markets;
        }

        private void RTDataHandler(object sender, EventArgs e)
        {
            if (e is DataFeed.BBRTDEventArgs)
            {

                DataFeed.BBRTDEventArgs eventArgs = e as DataFeed.BBRTDEventArgs;
                if (eventArgs.cObj == this)
                {
                    switch (eventArgs.MsgType)
                    {
                        case BloombergRTDataProvider.EventType.DataMsg:
                        case BloombergRTDataProvider.EventType.DataInit:
                            processBBDataMsg(eventArgs);
                            break;
                        case BloombergRTDataProvider.EventType.StatusMsg:
                            processBBStatusMsg(eventArgs);
                            break;
                        case BloombergRTDataProvider.EventType.ErrorMsg:
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void processBBStatusMsg(DataFeed.BBRTDEventArgs eventArgs)
        {
            String status = eventArgs.Msg;
        }

        private void processBBDataMsg(DataFeed.BBRTDEventArgs eventArgs)
        {
            switch (eventArgs.DataType)
            {
                case BloombergRTDataProvider.TickType.Ask:
                    NewTick(eventArgs.Ask);
                    break;
                case BloombergRTDataProvider.TickType.Bid:
                    NewTick(eventArgs.Bid);
                    break;
                case BloombergRTDataProvider.TickType.Trade:
                    NewTick(eventArgs.Trade);
                    break;
                case BloombergRTDataProvider.TickType.All:
                    FirstTick(eventArgs.Bid, eventArgs.Ask, eventArgs.Trade);
                    break;
                case BloombergRTDataProvider.TickType.None:
                default:
                    break;
            }
        }

        public void FirstTick(TickData Bid, TickData Ask, TickData Trade)
        {
            Console.WriteLine("Summary for " + securityObj.Name);
            DateTime timeBin = Bid.TimeStamp;
            if (!mktInitialized)
            {
                mktInitialized = true;
                marketData.Add(timeBin, new SortedDictionary<uint, MarketState>());

                lock (marketData[timeBin])
                {

                    // initialize the market
                    MarketState newState = new MarketState(securityObj, Bid, Ask, Trade);

                    // Add the new state to its time bin
                    marketData[newState.TimeStamp].Add(newState.BinCnt, newState);

                    // log the lateset market state
                    currentIntervalDT = timeBin;
                    marketDataLastest[newState.TimeStamp] = newState.BinCnt;
                    lastUpdate = timeBin;

                    markets.AddTickData(this, marketData[newState.TimeStamp], newState.TimeStamp);
                }
            }
        }

        public void NewTick(TickData newData)
        {
            // get the current market state
            SortedDictionary<uint, MarketState> currentTimeBin = marketData[currentIntervalDT];
            MarketState currentState = currentTimeBin[currentTimeBin.Keys.Max()];

            // disregard duplicates
            if (!duplicate(newData, currentState))
            {
                if (!marketData.ContainsKey(newData.TimeStamp))
                {
                    marketData.Add(newData.TimeStamp, new SortedDictionary<uint, MarketState>());
                }
                
                DateTime newTimeStamp = DateTime.MinValue;
                lock (marketData[newData.TimeStamp])
                {
                    // create a new updated market state
                    MarketState newState = new MarketState(securityObj, currentState, newData);

                    if (!marketDataLastest.ContainsKey(newState.TimeStamp))
                    {
                        marketDataLastest.Add(newState.TimeStamp, 0);
                        currentIntervalDT = newState.TimeStamp;
                        newTimeStamp = currentIntervalDT;
                    }
                    else
                    {
                        marketDataLastest[newState.TimeStamp]++;
                        newState.BinCnt = marketDataLastest[newState.TimeStamp];
                    }

                    // Add the new state to its time bin
                    marketData[newState.TimeStamp].Add(newState.BinCnt, newState);

                    if (LogEachTick)
                    {
                        string output = newState.ToStringAllData();
                        if (newState.StateType == MktStateType.Trade) output +=  " " + newState.ToStringAllTradesNoIndentity();
                        Console.WriteLine(output);
                    }

                }

                lastUpdate = newData.TimeStamp > lastUpdate ? newData.TimeStamp : lastUpdate;

                // let the market aggregator know there is a new timestamp to aggregate
                if (newTimeStamp != DateTime.MinValue)
                    markets.AddTickData(this, marketData[newTimeStamp], newTimeStamp);

            }
        }

        private bool duplicate(TickData newData, MarketState current)
        {
            double currPrice = 0;
            bool hasSizeData = false;

            switch (newData.Type)
            {
                case Type.Ask:
                    currPrice = current.Ask;
                    hasSizeData = ((securityObj.HasQuoteSize) && (newData.Size != current.AskVol));
                    break;
                case Type.Bid:
                    currPrice = current.Bid;
                    hasSizeData = ((securityObj.HasQuoteSize) && (newData.Size != current.BidVol));
                    break;
                case Type.Trade:
                    currPrice = current.LastTrdPrice;
                    hasSizeData = (securityObj.HasTradeSize);
                    break;
                default:
                    break;
            }

            if ((hasSizeData) || (newData.Price != currPrice))
            {
                return false;
            }

            return true;
        }

        private MarketState getCurrentState()
        {
            SortedDictionary<uint, MarketState> LastestBin = marketData[currentIntervalDT];
            return LastestBin[LastestBin.Keys.Max()];

        }

        public SortedDictionary<uint, MarketState> GetCurrentOrBefore(DateTime timestamp)
        {
            // get the data from the same time stamp,
            // or if that does not exist, the closest previous time stamp
            if (marketData.ContainsKey(timestamp))
            {
                return marketData[timestamp];
            }

            else
            {
                int responseIndex = 0;
                for (int i = marketData.Count - 1; i >= 0; i--)
                {
                    if (marketData.ElementAt(i).Key <= timestamp)
                    {
                        responseIndex = i;
                        break;
                    }
                }
                if (marketData.Count == 0)
                    return null;
                else
                    return marketData.ElementAt(responseIndex).Value;
            }
        }

        private DataRow getCurrentBarAsDataRow()
        {
            DataTable dataTable = new DataTable();
            DataRow barAsDataRow = dataTable.NewRow();

            return barAsDataRow;
        }

    }
}
