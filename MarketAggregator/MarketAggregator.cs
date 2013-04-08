using System;
using System.Collections.Generic;

namespace DataWrangler
{
    public class MarketAggregator
    {
        // main data repository
        public SortedDictionary<DateTime, Dictionary<Security, SortedDictionary<uint, MarketState>>>
            Markets = new SortedDictionary<DateTime, Dictionary<Security, SortedDictionary<uint, MarketState>>>();

        private DateTime _lastState = DateTime.MinValue;

        private readonly List<DataFactory> _securitites = new List<DataFactory>();

        //public MarketAggregator()
        //{

        //}

        public void AddSecurity(DataFactory factory)
        {
            _securitites.Add(factory);
        }

        public void AddTickData(DataFactory factory, SortedDictionary<uint, MarketState> state, DateTime stateTime)
        {

            if (!Markets.ContainsKey(stateTime))
            {
                Markets.Add(stateTime, new Dictionary<Security, SortedDictionary<uint, MarketState>>());
            }

            lock (Markets[stateTime])
            {
                Dictionary<Security, SortedDictionary<uint, MarketState>> allMarketsAtTime = Markets[stateTime];


                foreach (DataFactory f in _securitites)
                {
                    // no market data for this security, for this time stamp exists
                    if (!allMarketsAtTime.ContainsKey(f.SecurityObj))
                    {
                        SortedDictionary<uint, MarketState> mktData = factory.Equals(f) ? state : f.GetCurrentOrBefore(stateTime);
                        allMarketsAtTime.Add(f.SecurityObj, mktData);
                    }
                    else // market data for this security, for this time stamp exists already
                    {
                        if (factory.Equals(f))
                        {
                            allMarketsAtTime[f.SecurityObj] = state;
                        }
                    }
                }

                if (_lastState < stateTime) _lastState = stateTime;

            }
            // check if DateTime stamp exists.
            //  If not, create it then
            //  create a security entry for each security and fill it with last seconds state

            
            // if exsits, replace with latest data
        }
        
        // 1. Need container for each market
        // 2. Need container for all markets
        // 3. Need to keep references only
        // 4. Need to use last second's state as final output
        // 5. Needs to work in both R/T and Off-line

    }
}
