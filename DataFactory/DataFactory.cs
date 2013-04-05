using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using DataFeed = DataWrangler.BloombergRTDataProvider;

namespace DataWrangler
{
    public class DataFactory
    {
        private readonly string _security = String.Empty;
        private readonly Security _securityObj;
        private DateTime _lastUpdate = DateTime.MinValue;
        private DateTime _currentIntervalDt;
        private MarketAggregator _markets;

        // read only properties
        public string Security { get { return _security; } }
        public uint SecurityId { get; private set; }
        public Security SecurityObj { get { return _securityObj; } }
        public DateTime LastUpdate { get { return _lastUpdate; } }
        public DateTime CurrentIntervalDt { get { return _currentIntervalDt; } }
        public MarketState CurrentInterval { get { return GetCurrentState(); } }
        public DataRow CurrentBarDataRow { get { return GetCurrentBarAsDataRow(); } }

        bool _mktInitialized;

        public bool LogEachTick = false;

        // index tracking latest value in time bin
        private readonly Dictionary<DateTime, uint> _marketDataLastest = new Dictionary<DateTime, uint>();

        // Main data repository
        private readonly SortedDictionary<DateTime, SortedDictionary<uint, MarketState>> _marketData
            = new SortedDictionary<DateTime, SortedDictionary<uint, MarketState>>();

        // read only access to market data.
        public SortedDictionary<DateTime, SortedDictionary<uint, MarketState>> MarketData { get { return _marketData; } }

        private DataFeed _blbgFeed;

        public DataFactory(Security security)
        {
            _securityObj = security;
            _security = security.Name;
            SecurityId = security.Id;
        }

        public void SubscribeToDataFeedEvents(DataFeed dataFeed)
        {
            _blbgFeed = dataFeed;
            _blbgFeed.BBRTDUpdate += RTDataHandler;
        }

        public void AddReferenceToMarkets(MarketAggregator markets)
        {
            _markets = markets;
        }

        private void RTDataHandler(object sender, EventArgs e)
        {
            if (e is DataFeed.BBRTDEventArgs)
            {

                var eventArgs = e as DataFeed.BBRTDEventArgs;
                if (eventArgs.cObj == this)
                {
                    switch (eventArgs.MsgType)
                    {
                        case BloombergRTDataProvider.EventType.DataMsg:
                        case BloombergRTDataProvider.EventType.DataInit:
                            ProcessBbDataMsg(eventArgs);
                            break;
                        case BloombergRTDataProvider.EventType.StatusMsg:
                            ProcessBbStatusMsg(eventArgs);
                            break;
                        case BloombergRTDataProvider.EventType.ErrorMsg:
                            break;
                    }
                }
            }
        }

        private static void ProcessBbStatusMsg(DataFeed.BBRTDEventArgs eventArgs)
        {
            if (eventArgs.Msg == null) return;
            String status = eventArgs.Msg;
            Console.WriteLine(status);
        }

        private void ProcessBbDataMsg(DataFeed.BBRTDEventArgs eventArgs)
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
            }
        }

        public void FirstTick(TickData bid, TickData ask, TickData trade)
        {
            Console.WriteLine("Summary for " + _securityObj.Name);
            DateTime timeBin = bid.TimeStamp; // no timestamp
            if (!_mktInitialized)
            {
                _mktInitialized = true;
                _marketData.Add(timeBin, new SortedDictionary<uint, MarketState>());

                lock (_marketData[timeBin])
                {

                    // initialize the market
                    var newState = new MarketState(_securityObj, bid, ask, trade);

                    // Add the new state to its time bin
                    _marketData[newState.TimeStamp].Add(newState.BinCnt, newState);

                    // log the latest market state
                    _currentIntervalDt = timeBin;
                    _marketDataLastest[newState.TimeStamp] = newState.BinCnt;
                    _lastUpdate = timeBin;

                    _markets.AddTickData(this, _marketData[newState.TimeStamp], newState.TimeStamp);
                }
            }
        }

        public void NewTick(TickData newData)
        {
            // get the current market state
            SortedDictionary<uint, MarketState> currentTimeBin = _marketData[_currentIntervalDt];
            MarketState currentState = currentTimeBin[currentTimeBin.Keys.Max()];

            // disregard duplicates
            if (!Duplicate(newData, currentState))
            {
                if (!_marketData.ContainsKey(newData.TimeStamp))
                {
                    _marketData.Add(newData.TimeStamp, new SortedDictionary<uint, MarketState>());
                }
                
                DateTime newTimeStamp = DateTime.MinValue;
                lock (_marketData[newData.TimeStamp])
                {
                    // create a new updated market state
                    var newState = new MarketState(_securityObj, currentState, newData);

                    if (!_marketDataLastest.ContainsKey(newState.TimeStamp))
                    {
                        _marketDataLastest.Add(newState.TimeStamp, 0);
                        _currentIntervalDt = newState.TimeStamp;
                        newTimeStamp = _currentIntervalDt;
                    }
                    else
                    {
                        _marketDataLastest[newState.TimeStamp]++;
                        newState.BinCnt = _marketDataLastest[newState.TimeStamp];
                    }

                    // Add the new state to its time bin
                    _marketData[newState.TimeStamp].Add(newState.BinCnt, newState);

                    if (LogEachTick)
                    {
                        string output = newState.ToStringAllData();
                        if (newState.StateType == MktStateType.Trade) output +=  " " + newState.ToStringAllTradesNoIndentity();
                        Console.WriteLine(output);
                    }

                }

                _lastUpdate = newData.TimeStamp > _lastUpdate ? newData.TimeStamp : _lastUpdate;

                // let the market aggregator know there is a new timestamp to aggregate
                if (newTimeStamp != DateTime.MinValue)
                    _markets.AddTickData(this, _marketData[newTimeStamp], newTimeStamp);

            }
        }

        private bool Duplicate(TickData newData, MarketState current)
        {
            double currPrice = 0;
            bool hasSizeData = false;

            switch (newData.Type)
            {
                case Type.Ask:
                    currPrice = current.Ask;
                    hasSizeData = ((_securityObj.HasQuoteSize) && (newData.Size != current.AskVol));
                    break;
                case Type.Bid:
                    currPrice = current.Bid;
                    hasSizeData = ((_securityObj.HasQuoteSize) && (newData.Size != current.BidVol));
                    break;
                case Type.Trade:
                    currPrice = current.LastTrdPrice;
                    hasSizeData = (_securityObj.HasTradeSize);
                    break;
            }

            return (!hasSizeData) && Math.Abs(newData.Price - currPrice) < Double.Epsilon;
        }

        private MarketState GetCurrentState()
        {
            SortedDictionary<uint, MarketState> lastestBin = _marketData[_currentIntervalDt];
            return lastestBin[lastestBin.Keys.Max()];

        }

        public SortedDictionary<uint, MarketState> GetCurrentOrBefore(DateTime timestamp)
        {
            // get the data from the same time stamp,
            // or if that does not exist, the closest previous time stamp
            if (_marketData.ContainsKey(timestamp))
            {
                return _marketData[timestamp];
            }

            int responseIndex = 0;
            for (int i = _marketData.Count - 1; i >= 0; i--)
            {
                if (_marketData.ElementAt(i).Key <= timestamp)
                {
                    responseIndex = i;
                    break;
                }
            }

            return _marketData.Count == 0 ? null : _marketData.ElementAt(responseIndex).Value;
        }

        private static DataRow GetCurrentBarAsDataRow()
        {
            var dataTable = new DataTable();
            DataRow barAsDataRow = dataTable.NewRow();

            return barAsDataRow;
        }

    }
}
