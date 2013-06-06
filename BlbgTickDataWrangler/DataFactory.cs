using System;
using System.Collections.Generic;
using System.Linq;
using DataFeed = DataWrangler.BloombergRTDataProvider;

namespace DataWrangler
{
    public class DataFactory
    {
        //private readonly string _securityName = String.Empty;
        private readonly Security _securityObj;
        private MarketAggregator _markets;

        // read only properties
        public string SecurityName { get; private set; }
        public uint SecurityId { get; private set; }
        public Security SecurityObj { get { return _securityObj; } }
        public DateTime CurrentIntervalDt
        {
            get
            {
                DateTime currentIntervalDt = DateTime.MinValue;
                if (_marketData.Count > 0)
                    currentIntervalDt = _marketData.ElementAt(_marketData.Count - 1).Key;

                return currentIntervalDt;
            }
        }
        public MarketState CurrentInterval { get { return GetLatestState(); } }
        public bool MktInitialized { get { return _mktInitialized; } }

        bool _mktInitialized;

        public bool LogEachTick = false;

        // Main data repository
        private readonly SortedDictionary<DateTime, SortedDictionary<uint, MarketState>> _marketData
            = new SortedDictionary<DateTime, SortedDictionary<uint, MarketState>>();

        // read only access to market data.
        public SortedDictionary<DateTime, SortedDictionary<uint, MarketState>> MarketData { get { return _marketData; } }

        private DataFeed _blbgFeed;

        public DataFactory(Security security)
        {
            _securityObj = security;
            SecurityName = security.Name;
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

                    _markets.AddTickData(this, _marketData[newState.TimeStamp], newState.TimeStamp);
                }
            }
        }

        public void NewTick(TickData newData)
        {
            DateTime currTimeBinTimeStamp = getCurrentInterval(newData.TimeStamp);

            if (currTimeBinTimeStamp > DateTime.MinValue) // check to make sure the initialzation has happend
            {
                // get the current market state
                SortedDictionary<uint, MarketState> currTimeBin = _marketData[currTimeBinTimeStamp];
                MarketState currentState = currTimeBin.ElementAt(currTimeBin.Count - 1).Value; // last data point in time bin

                // disregard duplicates
                if (!DuplicateOfPrevDataPoint(newData, currentState))
                {
                    bool addedNewTimeStamp = false;
                    if (!_marketData.ContainsKey(newData.TimeStamp))
                    {
                        _marketData.Add(newData.TimeStamp, new SortedDictionary<uint, MarketState>());
                        addedNewTimeStamp = true;
                    }

                    var marketDataForTimeStamp = _marketData[newData.TimeStamp];
                    lock (marketDataForTimeStamp)
                    {
                        // create a new updated market state
                        var newState = new MarketState(_securityObj, currentState, newData);

                        newState.BinCnt = (uint)marketDataForTimeStamp.Count;
                        marketDataForTimeStamp.Add(newState.BinCnt, newState);

                        if (LogEachTick)
                        {
                            string output = newState.ToStringAllData();
                            if (newState.StateType == MktStateType.Trade) output += " " + newState.ToStringAllTradesNoIndentity();
                            Console.WriteLine(output);
                        }

                    }

                    // let the market aggregator know there is a new timestamp to aggregate
                    if (addedNewTimeStamp)
                        _markets.AddTickData(this, _marketData[newData.TimeStamp], newData.TimeStamp);

                }
            }
        }

        private DateTime getCurrentInterval(DateTime dataTimeStamp)
        {
            // latest timeStamp in collection
            DateTime maxTimeStamp = _marketData.ElementAt(_marketData.Count - 1).Key;

            // use this timeStamp unless the dateTime of data is before max dateTime
            if (maxTimeStamp <= dataTimeStamp)
                return maxTimeStamp;

            // if dateTime of data is before the max dateTime, use that time stamp if it exists
            if (_marketData.ContainsKey(dataTimeStamp))
                return dataTimeStamp;

            // if dateTime of data doesn't exist, use the most recent on before it
            for (int i = _marketData.Count - 1; i >= 0; i--)
            {
                if (_marketData.ElementAt(i).Key <= dataTimeStamp)
                {
                    return _marketData.ElementAt(i).Key;
                }
            }

            // if there is no data at all
            return DateTime.MinValue;
        }

        private bool DuplicateOfPrevDataPoint(TickData newData, MarketState current)
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

            // if it doesn't have size data and the price hasn't changed flag it as as duplicate
            return ((!hasSizeData) && (Math.Abs(newData.Price - currPrice) < Double.Epsilon));
        }

        private MarketState GetLatestState()
        {
            MarketState currentState = null;
            if (_marketData.Count > 0) // check to make sure we have some data
            {
                // get the current market state
                SortedDictionary<uint, MarketState> currTimeBin = _marketData.ElementAt(_marketData.Count - 1).Value;
                currentState = currTimeBin.ElementAt(currTimeBin.Count - 1).Value;
            }

            return currentState;

        }

        public SortedDictionary<uint, MarketState> GetLatestOrBefore(DateTime timeStamp)
        {
            if (_marketData.Count == 0) return null;

            // get the data from the same time stamp,
            if (_marketData.ContainsKey(timeStamp))
            {
                return _marketData[timeStamp];
            }

            // or if that does not exist, the closest previous time stamp
            DateTime currTimeBinsTimeStamp = getCurrentInterval(timeStamp);
            return DuplicateTick(_marketData[currTimeBinsTimeStamp], timeStamp);
        }

        public SortedDictionary<uint, MarketState> DuplicateTick(SortedDictionary<uint, MarketState> mostRecentState, DateTime timestamp)
        {
            // create a new time stamp if it doesn't exist (Only exist if there is a race condition)
            if (!_marketData.ContainsKey(timestamp))
                _marketData.Add(timestamp, new SortedDictionary<uint, MarketState>());

            var marketDataForTimeStamp = _marketData[timestamp];
            lock (marketDataForTimeStamp)
            {
                DateTime newTimeStamp = DateTime.MinValue;

                var prevState = mostRecentState[(uint)mostRecentState.Count - 1];
                var newState = new MarketState(_securityObj, prevState, timestamp);
                SortedDictionary<uint, MarketState> timeBin = _marketData[timestamp];

                newState.BinCnt = (uint)marketDataForTimeStamp.Count;
                marketDataForTimeStamp.Add(newState.BinCnt, newState);

                if (LogEachTick)
                {
                    string output = newState.ToStringAllData();
                    if (newState.StateType == MktStateType.Trade) output += " " + newState.ToStringAllTradesNoIndentity();
                    Console.WriteLine(output);
                }
            }

            return _marketData[timestamp];
        }


    }
}
