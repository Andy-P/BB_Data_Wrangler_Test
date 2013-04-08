using System;
using System.Collections.Generic;

namespace DataWrangler
{
    // Data container: Holds processed tick data and analytics
    public enum MktStateType { Summary = -1, Trade = 0, Ask = 1, Bid = 2 }
    
    public class MarketState
    {
        #region properties

        // Identifiers
        private readonly Security _securityObj = null;
        public string Name { get { return _securityObj.Name; } }
        public uint Id { get { return _securityObj.Id; } }
        private uint _binCnt = 0;

        // Populated by data feed
        private DateTime timeStamp = DateTime.MinValue;
        private MktStateType stateType;
        private double bid;
        private uint bidVol;
        private double prevBid;
        private uint prevBidVol;
        private double ask;
        private uint askVol;
        private double prevAsk;
        private uint prevAskVol;
        private double lastTrdPrice;
        private uint lastTrdSize;

        // Calculated values
        private double mid;
        private double midScaled;

        // Initialized on start of new interval (i.e. every 1 sec)
        private double bidOpen;
        private uint bidVolOpen;
        private double askOpen;
        private uint askVolOpen;
        private double midOpen;
        private double midScaledOpen;
        private double lastPriceOpn;
        private bool firstOfInterval = false;

        // running totals from beginning of interval
        private int bidVolChg = 0;
        private int bidVolChgSum = 0;
        private double volAtBid = 0;
        private double trdCntBid = 0;
        private int askVolChg = 0;
        private int askVolChgSum = 0;
        private double volAtAsk = 0;
        private double trdCntAsk = 0;

        // OrderFlow running totals for day
        private double volAtAskTdy = 0;
        private double volAtBidTdy = 0;
        private double orderFlowTdy = 0;
        private double volumeTdy = 0;

        // Read only properties
        public DateTime TimeStamp { get { return timeStamp; } }
        public MktStateType StateType { get { return stateType; } }

        public uint BinCnt
        {
            get { return _binCnt; }
            set { _binCnt = value; }
        }

        public double Bid { get { return bid; } }
        public uint BidVol { get { return bidVol; } }
        public int BidVolChg { get { return bidVolChg; } }
        public int BidVolChgSum { get { return bidVolChgSum; }}
        public double Ask { get { return ask; } }
        public uint AskVol { get { return askVol; } }
        public int AskVolChg { get { return askVolChg; } }
        public int AskVolChgSum { get { return askVolChgSum; } }
        public double LastTrdPrice { get { return lastTrdPrice; } }
        public uint LastTrdSize { get { return lastTrdSize; } }
        public double BidOpen { get { return bidOpen; } }
        public uint BidVolOpen { get { return bidVolOpen; } }
        public double AskOpen { get { return askOpen; } }
        public uint AskVolOpen { get { return askVolOpen; } }
        public double MidOpen { get { return midOpen; } }
        public double MidScaledOpen { get { return midScaledOpen; } }
        public double LastPriceOpn { get { return lastPriceOpn; } }
        public bool FirstOfInterval { get { return firstOfInterval; } }
        public double Mid { get { return mid; } }
        public double MidScaled { get { return midScaled; } }
        public double VolAtBid { get { return volAtBid; } }
        public double TrdCntBid { get { return trdCntBid; } }
        public double VolAtAsk { get { return volAtAsk; } }
        public double TrdCntAsk { get { return trdCntAsk; } }
        public double VolAtAskTdy { get { return volAtAskTdy; } }
        public double VolAtBidTdy { get { return volAtBidTdy; } }
        public double OrderFlowTdy { get { return orderFlowTdy; } }
        public double VolumeTdy { get { return volumeTdy; } }

        public SortedDictionary<double, TradesAtPrice> TrdsAtPrice = new SortedDictionary<double, TradesAtPrice>();

        public Dictionary<string, string> Codes;

        #endregion

        // constructor used on very first event to initialize market state
        public MarketState(Security security, TickData bid, TickData ask, TickData trade)
        {
            _securityObj = security;
            stateType = MktStateType.Summary;

            timeStamp = bid.TimeStamp;
            firstOfInterval = true;
            OnBidQuote(bid);
            SetBidOpen(bid);

            OnAskQuote(ask);
            SetAskOpen(ask);

            OnTrade(trade);
            SetTradeOpn(trade);

            SetMid();
            SetMidOpen();
        }

        // constructor used for each successive data event after the initial event
        public MarketState(Security security, MarketState previousMktState, TickData tickData) 
        {
            _securityObj = security;

            if (previousMktState == null)
                throw new ArgumentException("Previous MarketState object must not be null", "previousMktState");

            timeStamp = tickData.TimeStamp;
            firstOfInterval = (tickData.TimeStamp.Subtract(previousMktState.timeStamp).TotalSeconds != 0);

            CopyPrevState(previousMktState, firstOfInterval);

            switch (tickData.Type)
            {
                case Type.Ask:
                    stateType = MktStateType.Ask;
                    OnAskQuote(tickData);
                    SetAskVolChg(previousMktState);
                    SetMid();
                    if (firstOfInterval)
                    {
                        SetAskOpen(tickData);
                        SetMidOpen();
                    }
                    break;
                case Type.Bid:
                    stateType = MktStateType.Bid;
                    OnBidQuote(tickData);
                    SetBidVolChg(previousMktState);
                    SetMid();
                    if (firstOfInterval)
                    {
                        SetBidOpen(tickData);
                        SetMidOpen();
                    }
                    break;
                case Type.Trade:
                    stateType = MktStateType.Trade;
                    OnTrade(tickData);
                    if (firstOfInterval)
                    {
                        SetTradeOpn(tickData);
                    }
                    break;
                default:
                    throw new ArgumentException("TickData's 'Type' parameter must be of enum of type TickData.Type", "tickData");
            }

            if ((firstOfInterval) || (Codes == null))
            {
                
                Codes = tickData.Codes;
            }
            else
            {
                if (tickData.Codes != null)
                {
                    if (tickData.Codes.Count > 0)
                    {
                        foreach (var code in tickData.Codes)
                        {
                            if (!Codes.ContainsKey(code.Key))
                                Codes.Add(code.Key, code.Value);
                        }
                    }
                }
            }
        }

        private void CopyPrevState(MarketState previous, bool isFirstOfInterval)
        {
            bid = previous.bid;
            bidVol = previous.bidVol;
            ask = previous.ask;
            askVol = previous.askVol;

            prevBid = previous.prevBid;
            prevBidVol = previous.prevBidVol;
            prevAsk = previous.prevAsk;
            prevAskVol = previous.prevAskVol;

            mid = previous.mid;
            midScaled = previous.midScaled;

            if (!isFirstOfInterval)
            {
                bidOpen = previous.bidOpen;
                bidVolOpen = previous.bidVolOpen;
                askOpen = previous.askOpen;
                askVolOpen = previous.askVolOpen;
                midOpen = previous.midOpen;
                midScaledOpen = previous.midScaledOpen;
                lastPriceOpn = previous.lastPriceOpn;

                volAtBid = previous.volAtBid;
                volAtAsk = previous.volAtAsk;
                trdCntBid = previous.trdCntBid;
                trdCntAsk = previous.trdCntAsk;
                bidVolChgSum = previous.bidVolChgSum;
                askVolChgSum = previous.askVolChgSum;
                TrdsAtPrice = previous.TrdsAtPrice;
            }
            else
            {
                bidOpen = previous.bid;
                bidVolOpen = previous.bidVol;
                askOpen = previous.ask;
                askVolOpen = previous.askVol;
                midOpen = previous.mid;
                midScaledOpen = previous.midScaled;
                lastPriceOpn = previous.lastTrdPrice;
            }

        }

        private void OnBidQuote(TickData bidEvent)
        {
            prevBid = bid;
            bid = bidEvent.Price;

            prevBidVol = bidVol;
            bidVol = _securityObj.HasQuoteSize ? bidEvent.Size : 0; 
        }

        private void SetBidVolChg(MarketState prevMktState)
        {
            if (_securityObj.HasQuoteSize)
            {
                if ((bid == prevMktState.Bid))
                {   
                    bidVolChg = (int)(bidVol - prevMktState.BidVol);
                    bidVolChgSum += bidVolChg;
                }
                else
                {
                    if ((bid == prevMktState.Ask)) // just ticked up
                    {
                        bidVolChg = (int)(bidVol + prevMktState.AskVol);
                        bidVolChgSum += bidVolChg;
                        //Console.WriteLine(SecurityObj.Name + " went Bid @" + timeStamp.ToLongTimeString());
                    }
                    else
                    {
                        if ((bid == prevMktState.prevAsk)) // just ticked up, but need to look two data points back
                        {
                            bidVolChg = (int)(bidVol + prevMktState.prevAskVol);
                            bidVolChgSum += bidVolChg;
                            //Console.WriteLine(SecurityObj.Name + " went Bid @" + timeStamp.ToLongTimeString());
                        }
                    }
                }
            }
        }

        private void SetBidOpen(TickData bidEvent)
        {
            bidOpen = bidEvent.Price;
            bidVolOpen = _securityObj.HasQuoteSize ? bidEvent.Size : 0;
        }

        private void OnAskQuote(TickData askEvent)
        {
            prevAsk = ask;
            ask = askEvent.Price;

            prevAskVol = askVol;
            askVol = _securityObj.HasQuoteSize? askEvent.Size : 0;  
        }

        private void SetAskVolChg(MarketState prevMktState)
        {
            if (_securityObj.HasQuoteSize)
            {
                if (ask == prevMktState.Ask)
                {
                    askVolChg = (int)(askVol - prevMktState.AskVol);
                    askVolChgSum += askVolChg;
                }
                else
                {
                    if (ask == prevMktState.Bid) // just ticked down
                    {
                        askVolChg = (int)(askVol + prevMktState.BidVol);
                        askVolChgSum += askVolChg;
                        //Console.WriteLine(SecurityObj.Name + " went offered @" + timeStamp.ToLongTimeString());
                    }
                    else
                    {
                        if (ask == prevMktState.prevBid) // just ticked down, but we need to look two data points back for price/volume
                        {
                            askVolChg = (int)(askVol + prevMktState.prevBidVol);
                            askVolChgSum += askVolChg;
                            //Console.WriteLine(SecurityObj.Name + " went offered @" + timeStamp.ToLongTimeString());
                        }

                    }
                }
            }
        }

        private void SetAskOpen(TickData askEvent)
        {
            askOpen = askEvent.Price;
            askVolOpen = _securityObj.HasQuoteSize ? askEvent.Size : 0;
        }

        private void OnTrade(TickData trade)
        {
            lastTrdPrice = trade.Price;
            if (lastTrdPrice == Bid) trdCntBid++;
            if (lastTrdPrice == Ask) trdCntAsk++;

            if ((_securityObj.HasTradeSize) && (trade.Size > 0))
            {
                lastTrdSize = trade.Size;
                if (lastTrdPrice == Bid) volAtBid += lastTrdSize;
                if (lastTrdPrice == Ask) volAtAsk += lastTrdSize;                
            }
            else
            {
                lastTrdSize = 0;
                volAtBid = 0;
                volAtAsk = 0;                
            }

            if (TrdsAtPrice.ContainsKey(trade.Price))
            {
                TrdsAtPrice[trade.Price].NewTradeAtPrice(lastTrdSize, this);
            }
            else
            {
                if ((_securityObj.HasTradeSize) && (trade.Size > 0))
                    TrdsAtPrice.Add(lastTrdPrice, new TradesAtPrice(lastTrdPrice, lastTrdSize, this));
                else
                    TrdsAtPrice.Add(lastTrdPrice, new TradesAtPrice(lastTrdPrice, this));
            }
        }
        
        private void SetTradeOpn(TickData trade)
        {
            lastPriceOpn = trade.Price;
        }

        private void SetMid()
        {
            mid = (bid + ask) / 2;
            if ((_securityObj.HasQuoteSize) && (bidVol > 0) && (askVol > 0))
            {
                midScaled = (ask - bid) * bidVol / (bidVol + askVol) + bid;
            }
            else
            {
                midScaled = mid;
            }
        }
        
        private void SetMidOpen()
        {
            midOpen = mid;
            midScaledOpen = midScaled;
        }

        public string ToStringAllData()
        {
            string dataStr = _securityObj.Name +
                             " " + timeStamp.ToLongTimeString() +
                             "  Type " + stateType.ToString() +
                             "  Bid " + Bid.ToString() +
                             //"  BidOpn " + BidOpen.ToString() +
                             "  BidVol " + BidVol.ToString() +
                             //"  BidVolOpen " + BidVolOpen.ToString() +
                             //"  BidVolChg " + BidVolChg.ToString() +
                             //"  BidVolChgSum " + BidVolChgSum.ToString() +
                             //"  VolAtBid " + VolAtBid.ToString() +
                             //"  TrdCntBid " + TrdCntBid.ToString() +
                             "  Ask " + Ask.ToString() +
                             //"  AskOpen " + AskOpen.ToString() +
                             "  AskVol " + AskVol.ToString() +
                             //"  AskVolOpen " + AskVolOpen.ToString() +
                             //"  AskVolChg " + AskVolChg.ToString() +
                             //"  AskVolChgSum " + AskVolChgSum.ToString() +
                             //"  VolAtAsk " + VolAtAsk.ToString() +
                             //"  TrdCntAsk  " + TrdCntAsk.ToString() +
                             //"  Mid " + Mid.ToString() +
                             //"  MidOpn " + MidOpen.ToString() +
                             //"  MidScaled " + MidScaled.ToString("#.00") +
                             //"  MidScaledOpen " + MidScaledOpen.ToString("#.00") +
                             "  LastPrice " + LastTrdPrice.ToString() +
                             //"  LastPriceOpn " + LastPriceOpn.ToString() +
                             "  LastSize " + LastTrdSize.ToString();

            //dataStr = SecurityObj.Name + Environment.NewLine + " {" + Environment.NewLine +
            //    "  LastSize " + LastTrdSize.ToString() + Environment.NewLine + " }";

            return dataStr;
        }

        public string ToStringAllTrades()
        {
            string output = _securityObj.Name +
                " " + timeStamp.ToLongTimeString() + " " +
                ToStringAllTradesNoIndentity();

            return output.TrimEnd();
        }
        
        public string ToStringAllTradesNoIndentity()
        {
            string output = String.Empty;

            int prcCnt = 0;
            foreach (var p in TrdsAtPrice.Values)
            {
                string cnt = prcCnt.ToString() + " ";

                string priceStr = "Price" + cnt + p.Price.ToString() +
                    " Vol" + cnt + p.TotalVolume.ToString() +
                    " VolBid" + cnt + p.VolAtBid.ToString() +
                    " VolAsk" + cnt + p.VolAtAsk.ToString() +
                    " Cnt" + cnt + p.TradeCount.ToString() +
                    " CntBid" + cnt + p.CntAtBid.ToString() +
                    " CntAsk" + cnt + p.CntAtAsk.ToString();

                output += priceStr + " ";

                prcCnt++;
            }

            return output.TrimEnd();
        }
        
        public class TradesAtPrice
        {
            public double Price;

            public uint TotalVolume = 0;
            public uint VolAtBid = 0;
            public uint VolAtAsk = 0;

            public double TradeCount = 0;
            public uint CntAtBid = 0;
            public uint CntAtAsk = 0;

            public TradesAtPrice(double price, MarketState state)
            {
                Price = price;
                TradeCount++;

                if (price == state.Bid) CntAtBid++;
                else
                    if (price == state.Ask) CntAtAsk++;

            }

            public TradesAtPrice(double price, uint volume, MarketState state)
            {
                Price = price;
                TotalVolume = volume;
                TradeCount++;

                if (price == state.Bid)
                {
                    VolAtBid += volume;
                    CntAtBid++;
                }
                else
                {
                    if (price == state.Ask)
                    {
                        VolAtAsk += volume;
                        CntAtAsk++;
                    }
                }
            }

            public void NewTradeAtPrice(uint volume, MarketState state)
            {
                TotalVolume += volume;
                TradeCount++;

                if (Price == state.Bid)
                {
                    VolAtBid += volume;
                    CntAtBid++;
                }
                else
                {
                    if (Price == state.Ask)
                    {
                        VolAtAsk += volume;
                        CntAtAsk++;
                    }
                }
            }
        }
    }
}
