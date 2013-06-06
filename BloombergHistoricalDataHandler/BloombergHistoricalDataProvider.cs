using System;
using System.Collections.Generic;
using System.Linq;
//using System.Windows.Forms;
using System.Diagnostics;
using CorrelationID = Bloomberglp.Blpapi.CorrelationID;
using Event = Bloomberglp.Blpapi.Event;
using Message = Bloomberglp.Blpapi.Message;
using Element = Bloomberglp.Blpapi.Element;
using Name = Bloomberglp.Blpapi.Name;
using Request = Bloomberglp.Blpapi.Request;
using Service = Bloomberglp.Blpapi.Service;
using Session = Bloomberglp.Blpapi.Session;
using SessionOptions = Bloomberglp.Blpapi.SessionOptions;
using EventHandler = Bloomberglp.Blpapi.EventHandler;


namespace DataWrangler
{

    public delegate void BBHEventHandler(object sender, EventArgs e);

    public class BlbgHistoricalData
    {
        # region BBH Status Events
        public event BBHEventHandler BBHUpdate;
        public void OnBBHUpdate(BBHEventArgs e)
        {
            if (BBHUpdate != null)
                BBHUpdate(this, e);
        }

        public class BBHEventArgs : EventArgs
        {
            public string status { get; set; }
            public BBHEventArgs(string status)
            {
                this.status = status;
            }
        }

        # endregion

        private static readonly Name EXCEPTIONS = new Name("exceptions");
        private static readonly Name FIELD_ID = new Name("fieldId");
        private static readonly Name REASON = new Name("reason");
        private static readonly Name CATEGORY = new Name("category");
        private static readonly Name DESCRIPTION = new Name("description");
        private static readonly Name ERROR_CODE = new Name("errorCode");
        private static readonly Name SOURCE = new Name("source");
        private static readonly Name SECURITY_ERROR = new Name("securityError");
        private static readonly Name SECURITY = new Name("security");
        private static readonly Name MESSAGE = new Name("message");
        private static readonly Name RESPONSE_ERROR = new Name("responseError");
        private static readonly Name SECURITY_DATA = new Name("securityData");
        private static readonly Name FIELD_EXCEPTIONS = new Name("fieldExceptions");
        private static readonly Name ERROR_INFO = new Name("errorInfo");

        private SessionOptions d_sessionOptions;
        private Session d_session;
        private List<string> fieldNames;
        public List<object[]> results;
        private string queryStatus;
        private bool isAsync = false;
        private bool initialized = false;
        private Dictionary<int, BBHistQuery> requests;
        private int requestIndex = 0;
        public int queryQuePosition { get; private set; }

        public enum Acct_Type
        {
            Consolidated,
            Parent,
            none,
        }
        public enum Periodicity
        {
            Daily,
            Weekly,
            Monthly,
            Quarterly,
            SemiAnnual,
            Yearly
        }
        public enum PeriodAdj
        {
            Actual,
            Fiscal,
            Calendar
        }
        public enum Eqy_Consolidated
        {
            None,
            Parent,
            Consolidated
        }

        public Periodicity periodicity { get; set; }
        public PeriodAdj periodAdj { get; set; }
        public Eqy_Consolidated eqy_Consolidated { get; set; }
        public bool isAdjusted { get; set; }

        public BlbgHistoricalData()
        {
            string serverHost = "localhost";
            int serverPort = 8194;

            // set sesson options
            d_sessionOptions = new SessionOptions();
            d_sessionOptions.ServerHost = serverHost;
            d_sessionOptions.ServerPort = serverPort;


            periodicity = Periodicity.Daily;
            periodAdj = PeriodAdj.Actual;
            eqy_Consolidated = Eqy_Consolidated.None;
            isAdjusted = true;

            queryQuePosition = 0;
        }

        public void defineQuery(Dictionary<string, DateTime> securityList, List<string> fieldList, int maxBatchSize, int sec_type, bool clearPrevious, bool lastBatch)
        {
            defineQuery(securityList, fieldList, maxBatchSize, sec_type, DateTime.Now, clearPrevious, lastBatch);
        }
        public void defineQuery(Dictionary<string, DateTime> securityList, List<string> fieldList, int maxBatchSize, int sec_type, DateTime end, bool clearPrevious, bool lastBatch)
        {
            if (!createSession())
            {
                queryStatus = "Failed to start session.";
                OnBBHUpdate(new BBHEventArgs(queryStatus));
                return;
            }
            // start reference 
            if (!d_session.OpenService("//blp/refdata"))
            {
                queryStatus = "Failed to open //blp/refdata";
                OnBBHUpdate(new BBHEventArgs(queryStatus));
                return;
            }
            queryStatus = "Connected sucessfully";
            OnBBHUpdate(new BBHEventArgs(queryStatus));

            // get refdata service
            Service refDataService = d_session.GetService("//blp/refdata");


            fieldNames = fieldList;

            // prepare request containers
            if ((clearPrevious) || (requests == null))
            {
                requests = new Dictionary<int, BBHistQuery>();
                requestIndex = 0;
            }

            var sortedBatches = securityList
                .OrderBy(kv => kv.Key)
                .GroupBy(o => o.Value)
                .Select(g => new
                {
                    batch = g.Select((o, i) => new { Val = o, Index = i })
                              .GroupBy(item => item.Index / maxBatchSize)
                });

            foreach (var Batch in sortedBatches.SelectMany(item => item.batch))
            {
                DateTime start = Batch.First().Val.Value;
                var secBatch = new List<string>();
                foreach (var element in Batch)
                {
                    secBatch.Add(element.Val.Key);
                }

                requests.Add(requestIndex, getDailyRequest(refDataService, requestIndex, secBatch, fieldList, start, end, sec_type));
                requestIndex++;
            }
            initialized = true;
            if (lastBatch)
                OnBBHUpdate(new BBHEventArgs("INITIALIZED"));
        }

        private BBHistQuery getDailyRequest(Service refDataService, int queryId, List<string> securityList, List<string> fieldList, DateTime start, DateTime end, int sec_type)
        {
            BBHistQuery query = new BBHistQuery() { corrId = queryId };

            // create historical request
            Request request = refDataService.CreateRequest("HistoricalDataRequest");

            // set security to request
            Element securities = request.GetElement("securities");
            foreach (string security in securityList)
            {
                if ((sec_type == 2) || (sec_type == 4)) //  INDEX = 2, COMDTY=4
                {
                    string Modified_Sec = security.Split(' ').First();
                    string Sec_Class = security.Split(' ').Last();
                    if (isAdjusted)
                    {
                        Modified_Sec += " A:00_0_D PIT " + Sec_Class; // generic difference adjusted single futures series
                    }
                    else
                    {
                        Modified_Sec += " A:00_0_N PIT " + Sec_Class; // generic non-adjusted single futures series
                    }
                    securities.AppendValue(Modified_Sec);
                }
                else
                    securities.AppendValue(security);
            }

            // set fields
            Element fields = request.GetElement("fields");
            query.fields = fieldList;
            //fields.AppendValue("security");
            //fields.AppendValue("date");
            foreach (var fld in fieldList)
            {
                if (fld.ToUpper() == "RELATIVE_DATE")
                {
                    request.Set("returnRelativeDate", true);
                }
                else
                {
                    if ((fld.ToUpper() != "SECURITY") && ((fld.ToUpper() != "DATE")))
                        fields.AppendValue(fld.ToString());
                }
            }

            // set the periodicity of the historical data
            query.periodicity = periodicity;
            switch (periodicity)
            {
                case Periodicity.Daily:
                    request.Set("periodicitySelection", "DAILY");
                    break;
                case Periodicity.Weekly:
                    request.Set("periodicitySelection", "WEEKLY");
                    break;
                case Periodicity.Monthly:
                    request.Set("periodicitySelection", "MONTHLY");
                    break;
                case Periodicity.Quarterly:
                    request.Set("periodicitySelection", "QUARTERLY");
                    break;
                case Periodicity.SemiAnnual:
                    request.Set("periodicitySelection", "SEMI_ANNUALLY");
                    break;
                case Periodicity.Yearly:
                    request.Set("periodicitySelection", "YEARLY");
                    break;
                default:
                    request.Set("periodicitySelection", "DAILY");
                    break;
            }


            query.periodAdj = periodAdj;
            switch (periodAdj)
            {
                case PeriodAdj.Actual:
                    request.Set("periodicityAdjustment", "ACTUAL");
                    break;
                case PeriodAdj.Fiscal:
                    request.Set("periodicityAdjustment", "FISCAL");
                    break;
                case PeriodAdj.Calendar:
                    request.Set("periodicityAdjustment", "CALENDAR");
                    break;
                default:
                    request.Set("periodicityAdjustment", "ACTUAL");
                    break;
            }

            // set the adjustment option
            query.adjusted = isAdjusted;
            if (sec_type == 1)
                if (isAdjusted)
                {
                    request.Set("adjustmentSplit", true);
                    query.adjusted = true;
                }
                else
                {
                    request.Set("adjustmentSplit", false);
                    query.adjusted = false;
                }


            // set the consolidate/parent is required
            query.eqy_Consolidated = eqy_Consolidated;
            if (eqy_Consolidated != Eqy_Consolidated.None)
            {
                Element requestOverrides = request.GetElement("overrides");
                Element ovr = requestOverrides.AppendElement();
                ovr.SetElement(FIELD_ID, "EQY_CONSOLIDATED");
                if (eqy_Consolidated == Eqy_Consolidated.Consolidated)
                {
                    ovr.SetElement("value", "Y");
                }
                else
                {
                    if (eqy_Consolidated == Eqy_Consolidated.Parent)
                    {
                        ovr.SetElement("value", "N");
                    }
                }
            }

            request.Set("startDate", start.ToString("yyyyMMdd"));
            request.Set("endDate", end.ToString("yyyyMMdd"));
            request.Set("nonTradingDayFillOption", "ACTIVE_DAYS_ONLY");
            request.Set("nonTradingDayFillMethod", "NIL_VALUE");
            request.Set("overrideOption", "OVERRIDE_OPTION_CLOSE");
            Debug.Print("Dates {0} {1}", start.ToString("yyyyMMdd"), end.ToString("yyyyMMdd"));

            query.request = request;
            OnBBHUpdate(new BBHEventArgs("Batched Request Que Created"));
            return query;
        }

        private bool createSession()
        {
            if (d_session != null)
                d_session.Stop();
            queryStatus = "Connecting...";
            if (isAsync)
            {
                // create asynchronous session
                d_session = new Session(d_sessionOptions, new EventHandler(processEvent));
            }
            else
            {
                // create synchronous session
                d_session = new Session(d_sessionOptions);
            }
            return d_session.Start();
        }

        public void sendNextRequest()
        {
            if (initialized)
            {
                if (queryQuePosition < requests.Count)
                {
                    BBHistQuery nextRequest = requests[queryQuePosition];

                    results = new List<object[]>();
                    CorrelationID cID = new CorrelationID(queryQuePosition);

                    //// send request
                    d_session.SendRequest(nextRequest.request, cID);

                    OnBBHUpdate(new BBHEventArgs("Submitted request. Waiting for response..."));
                    if (!isAsync)
                    {
                        while (true)
                        {
                            // process data
                            Event eventObj = d_session.NextEvent();
                            OnBBHUpdate(new BBHEventArgs("Receiving data..."));
                            processEvent(eventObj, d_session);
                            if (eventObj.Type == Event.EventType.RESPONSE)
                            {
                                // response completed
                                break;
                            }
                        }
                    }
                }
                else
                {
                    queryQuePosition = 0;
                    OnBBHUpdate(new BBHEventArgs("COMPLETED"));
                }
            }
        }

        private void processEvent(Event eventObj, Session session)
        {
            switch (eventObj.Type)
            {
                case Event.EventType.RESPONSE:
                    // process data
                    processRequestDataEvent(eventObj, session);
                    queryQuePosition++;
                    OnBBHUpdate(new BBHEventArgs("RESPONSE"));
                    break;
                case Event.EventType.PARTIAL_RESPONSE:
                    // process partial data
                    processRequestDataEvent(eventObj, session);
                    break;
                default:
                    // process misc events
                    processMiscEvents(eventObj, session);
                    break;
            }

        }

        private void processRequestDataEvent(Event eventObj, Session session)
        {
            string securityName = string.Empty;

            Boolean hasFieldError = false;
            // field error messages.
            Dictionary<string, string> fieldErrors = new Dictionary<string, string>();

            foreach (var field in fieldNames)
            {
                fieldErrors.Add(field, null);
            }
            // process message
            foreach (Message msg in eventObj.GetMessages())
            {
                if (msg.MessageType.Equals(Bloomberglp.Blpapi.Name.GetName("HistoricalDataResponse")))
                {
                    // process errors
                    if (msg.HasElement(RESPONSE_ERROR))
                    {
                        Element error = msg.GetElement(RESPONSE_ERROR);
                        results.Add(new object[] { error.GetElementAsString(MESSAGE) });
                        Debug.Print("Error {0}", error.GetElementAsString(MESSAGE));
                    }
                    else
                    {
                        Element secDataArray = msg.GetElement(SECURITY_DATA);
                        int numberOfSecurities = secDataArray.NumValues;
                        if (secDataArray.HasElement(SECURITY_ERROR))
                        {
                            // security error
                            Element secError = secDataArray.GetElement(SECURITY_ERROR);
                            securityName = secDataArray.GetElementAsString(SECURITY);
                            string err = secError.GetElementAsString(MESSAGE) + "(" + securityName + ")";
                            results.Add(new object[] { err });
                            Debug.Print("Error {0}", err);
                        }
                        if (secDataArray.HasElement(FIELD_EXCEPTIONS))
                        {
                            // field error
                            Element error = secDataArray.GetElement(FIELD_EXCEPTIONS);
                            for (int errorIndex = 0; errorIndex < error.NumValues; errorIndex++)
                            {
                                Element errorException = error.GetValueAsElement(errorIndex);
                                string field = errorException.GetElementAsString(FIELD_ID);
                                Element errorInfo = errorException.GetElement(ERROR_INFO);
                                string message = errorInfo.GetElementAsString(MESSAGE);
                                fieldErrors[field] = message;
                                hasFieldError = true;
                            } // end for 
                        } // end if
                        // process securities data
                        for (int index = 0; index < numberOfSecurities; index++)
                        {
                            foreach (Element secData in secDataArray.Elements)
                            {
                                switch (secData.Name.ToString())
                                {
                                    case "eidsData":
                                        // process security eid data here
                                        break;
                                    case "security":
                                        // security name
                                        securityName = secData.GetValueAsString();
                                        string statusMsg = "Receiving " + securityName + " data...";
                                        OnBBHUpdate(new BBHEventArgs(statusMsg));
                                        break;
                                    case "fieldData":
                                        if (hasFieldError && secData.NumValues == 0)
                                        {
                                            // no data but have field error
                                            object[] dataValues = new object[fieldNames.Count];
                                            dataValues[0] = securityName;
                                            int fieldIndex = 0;
                                            foreach (string field in fieldNames)
                                            {
                                                if (fieldErrors[field] != null)
                                                {
                                                    dataValues[fieldIndex] = fieldErrors[field];
                                                }
                                                fieldIndex++;
                                            }
                                            results.Add(dataValues);
                                        }
                                        else
                                        {
                                            // get field data
                                            for (int pointIndex = 0; pointIndex < secData.NumValues; pointIndex++)
                                            {
                                                int fieldIndex = 0;
                                                object[] dataValues = new object[fieldNames.Count + 1];
                                                Element fields = secData.GetValueAsElement(pointIndex);
                                                foreach (string field in fieldNames)
                                                {
                                                    //try
                                                    //{
                                                    if (field.ToUpper() == "SECURITY")
                                                        dataValues[fieldIndex] = securityName;
                                                    else
                                                    {
                                                        if (fields.HasElement(field))
                                                        {
                                                            Element item = fields.GetElement(field);
                                                            if (item.IsArray)
                                                            {
                                                                // bulk field data
                                                                dataValues[fieldIndex] = "Bulk Data";
                                                            }
                                                            else
                                                            {
                                                                // field data
                                                                dataValues[fieldIndex] = item.GetValueAsString();
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // no field value
                                                            if (fieldErrors[field] == null)
                                                            {
                                                                dataValues[fieldIndex] = DBNull.Value;
                                                            }
                                                            else
                                                            {
                                                                if (fieldErrors[field].ToString().Length > 0)
                                                                {
                                                                    // field has error
                                                                    dataValues[fieldIndex] = fieldErrors[field];
                                                                }
                                                                else
                                                                {
                                                                    dataValues[fieldIndex] = DBNull.Value;
                                                                }
                                                            }
                                                        }
                                                    }  // end if
                                                    //}
                                                    //catch (Exception ex)
                                                    //{
                                                    //    // display error
                                                    //    dataValues[fieldIndex] = ex.Message;
                                                    //}
                                                    //finally
                                                    //{
                                                    fieldIndex++;
                                                    //}
                                                } // end foreach 
                                                // add query specific data
                                                //dataValues[fieldNames.Count] = requests[queryQuePosition].value;
                                                dataValues[fieldNames.Count] = (BBHistQuery)requests[queryQuePosition];
                                                // add data to data table
                                                results.Add(dataValues);
                                            } // end for
                                        }
                                        break;
                                } // end switch
                            } // end foreach
                        } // end for
                    } // end else 
                } // end if
            }
        }

        private void processMiscEvents(Event eventObj, Session session)
        {
            foreach (Message msg in eventObj.GetMessages())
            {
                switch (msg.MessageType.ToString())
                {
                    case "SessionStarted":
                        // "Session Started"
                        break;
                    case "SessionTerminated":
                    case "SessionStopped":
                        // "Session Terminated"
                        break;
                    case "ServiceOpened":
                        // "Reference Service Opened"
                        break;
                    case "RequestFailure":
                        Element reason = msg.GetElement(REASON);
                        string message = string.Concat("Error: Source-", reason.GetElementAsString(SOURCE),
                            ", Code-", reason.GetElementAsString(ERROR_CODE), ", category-", reason.GetElementAsString(CATEGORY),
                            ", desc-", reason.GetElementAsString(DESCRIPTION));
                        queryStatus = message;
                        break;
                    default:
                        queryStatus = msg.MessageType.ToString();
                        break;
                }
            }
        }

        public class BBHistQuery
        {
            public Request request;
            public int corrId;
            public bool adjusted;
            public Eqy_Consolidated eqy_Consolidated;
            public Acct_Type acct_Type;
            public Periodicity periodicity;
            public PeriodAdj periodAdj;
            public List<string> fields;
        }

    }
}
