
namespace DataWrangler
{
    // Data container for static Security data (only one per security)
    public class Security
    {
        public enum SecurityType { Equity, Index, Curncy, Comdty, IndexFuture, IndexOption }

        private string name;
        public string Name { get { return name; } }
        private uint id;
        public uint Id { get { return id; } }
        private bool hasQuoteSize;
        public bool HasQuoteSize { get { return hasQuoteSize; } }
        private bool hasTradeSize;
        public bool HasTradeSize { get { return hasTradeSize; } }
        private SecurityType secType;
        public SecurityType SecType { get { return secType; } }

        public Security(string name, uint id, SecurityType securityType)
        {
            this.name = name.Trim();
            this.id = id;
            this.secType = securityType;

            switch (secType)
            {
                case SecurityType.Equity:
                case SecurityType.Comdty:
                case SecurityType.IndexFuture:
                case SecurityType.IndexOption:
                    hasQuoteSize = true;
                    hasTradeSize = true;
                    break;
                case SecurityType.Index:
                case SecurityType.Curncy:
                default:
                    hasQuoteSize = false;
                    hasTradeSize = false;
                    break;
            }
        }

    }
}
