using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataWrangler
{
    public class MarketAggregator
    {
        // main data respository
        public SortedDictionary<DateTime, Dictionary<Security, SortedDictionary<uint, MarketState>>>
            Markets = new SortedDictionary<DateTime, Dictionary<Security, SortedDictionary<uint, MarketState>>>();

        private DateTime LastState = DateTime.MinValue;

        private List<DataFactory> Securitites = new List<DataFactory>();

        public MarketAggregator()
        {

        }

        public void AddSecurity(DataFactory factory)
        {
            Securitites.Add(factory);
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


                foreach (DataFactory f in Securitites)
                {
                    // no market data for this security, for this time stamp exists
                    if (!allMarketsAtTime.ContainsKey(f.SecurityObj))
                    {
                        SortedDictionary<uint, MarketState> mktData;

                        if (factory.Equals(f))
                        {
                            mktData = state;
                        }
                        else
                        {
                            // get the data from the same time stamp,
                            // or if that does not exist, the closest previous time stamp
                            mktData = f.GetCurrentOrBefore(stateTime);
                        }

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

                //string nudge = "";
                //Console.WriteLine(stateTime.ToLongTimeString());
                //foreach (SortedDictionary<uint, MarketState> item in allMarketsAtTime.Values)
                //{
                //    if (item != null)
                //    {
                //        int cnt = item.Count - 1;
                //        Console.WriteLine(nudge + item[(uint)cnt].ToStringAllData());
                //        nudge += "   ";
                //    }
                //}
                //Console.WriteLine(" ");

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
