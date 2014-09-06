using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SteamKit2;
using System.Net;
using System.Web;
using System.IO;
using Newtonsoft.Json;

namespace SteamTrade
{
    public class TradeOfferException : Exception
    {
        public int ErrorCode = 0;        

        public TradeOfferException() { }

        public TradeOfferException(string message, int code)
            : base(message)
        {
            this.ErrorCode = code;
        }
    }

    public class TradeOffers
    {
        private SteamID BotId;
        private CookieContainer Cookies = new CookieContainer();
        private string SessionId;
        private string Token;
        private string ApiKey;

        public TradeOffers(SteamID botId, string apiKey, string sessionId, string token)
        {
            this.BotId = botId;
            Cookies.Add(new Cookie("sessionid", sessionId, String.Empty, "steamcommunity.com"));
            Cookies.Add(new Cookie("steamLogin", token, String.Empty, "steamcommunity.com"));
            SessionId = sessionId;
            Token = token;
            ApiKey = apiKey;
        }

        /// <summary>
        /// Create a new trade offer session.
        /// </summary>
        /// <param name="partnerId">The SteamID of the user you want to send a trade offer to.</param>
        /// <returns>A 'Trade' object in which you can apply further actions</returns>
        public Trade CreateTrade(SteamID partnerId)
        {
            return new Trade(this, partnerId, SessionId, Token);
        }        

        public class Trade
        {
            private CookieContainer cookies = new CookieContainer();
            private string sessionId;
            private SteamID partnerId;
            private TradeOffers tradeOffers;
            public TradeStatus tradeStatus;

            public Trade(TradeOffers tradeOffers, SteamID partnerId, string sessionId, string token)
            {
                this.tradeOffers = tradeOffers;
                this.partnerId = partnerId;
                this.sessionId = sessionId;
                cookies.Add(new Cookie("sessionid", sessionId, String.Empty, "steamcommunity.com"));
                cookies.Add(new Cookie("steamLogin", token, String.Empty, "steamcommunity.com"));
                tradeStatus = new TradeStatus();
                tradeStatus.version = 1;
                tradeStatus.newversion = true;
                tradeStatus.me = new TradeStatusUser(ref tradeStatus);
                tradeStatus.them = new TradeStatusUser(ref tradeStatus);
            }

            /// <summary>
            /// Send the current trade offer with a token.
            /// </summary>
            /// <param name="message">Message to send with trade offer.</param>
            /// <param name="token">Trade offer token.</param>
            /// <returns>-1 if response fails to deserialize (general error), 0 if no tradeofferid exists (Steam error), or the Trade Offer ID of the newly created trade offer.</returns>
            public ulong SendTradeWithToken(string message, string token)
            {
                return SendTrade(message, token);
            }
            /// <summary>
            /// Send the current trade offer.
            /// </summary>
            /// <param name="message">Message to send with trade offer.</param>
            /// <param name="token">Optional trade offer token.</param>
            /// <returns>-1 if response fails to deserialize (general error), 0 if no tradeofferid exists (Steam error), or the Trade Offer ID of the newly created trade offer.</returns>
            public ulong SendTrade(string message, string token = "")
            {
                var url = "https://steamcommunity.com/tradeoffer/new/send";
                var referer = "http://steamcommunity.com/tradeoffer/new/?partner=" + partnerId.AccountID;
                var data = new NameValueCollection();
                data.Add("sessionid", sessionId);
                data.Add("partner", partnerId.ConvertToUInt64().ToString());
                data.Add("tradeoffermessage", message);                
                data.Add("json_tradeoffer", JsonConvert.SerializeObject(this.tradeStatus));
                data.Add("trade_offer_create_params", token == "" ? "{}" : "{\"trade_offer_access_token\":\"" + token + "\"}");
                try
                {
                    string result = "";
                    for (int i = 0; i < 10; i++)
                    {
                        try
                        {
                            var response = SteamWeb.Request(url, "POST", data, cookies, true, referer);
                            using (System.IO.Stream responseStream = response.GetResponseStream())
                            {
                                using (var reader = new System.IO.StreamReader(responseStream))
                                {
                                    result = reader.ReadToEnd();
                                    if (string.IsNullOrEmpty(result))
                                    {
                                        Console.WriteLine("Web request failed (status: {0}). Retrying...", response.StatusCode);
                                        System.Threading.Thread.Sleep(1000);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                        catch (WebException ex)
                        {
                            try
                            {
                                int statusCode = 0;
                                if (ex.Status == WebExceptionStatus.ProtocolError)
                                {
                                    statusCode = (int)((HttpWebResponse)ex.Response).StatusCode;
                                    Console.WriteLine("Status Code: {0}, {1}", statusCode, ((HttpWebResponse)ex.Response).StatusDescription);
                                }
                                string errorMessage = new System.IO.StreamReader(ex.Response.GetResponseStream()).ReadToEnd();
                                Console.WriteLine("Error: {0}", errorMessage);
                                if (statusCode == 500 && errorMessage.Contains("There was an error sending your trade offer."))
                                {
                                    var errorJson = JsonConvert.DeserializeObject<dynamic>(errorMessage);
                                    if (errorJson.strError != null)
                                    {
                                        string errorString = errorJson.strError;
                                        int errorCode = Convert.ToInt32(errorString.Split(new char[] { '(', ')' })[1]);
                                        if (errorCode == 16 || errorCode == 11)
                                        {
                                            Console.WriteLine("Encountered Steam error code {0}, manually checking for completion...", errorCode);
                                            var tradeOfferList = tradeOffers.GetTradeOffers();
                                            foreach (var tradeOffer in tradeOfferList)
                                            {
                                                if (tradeStatus.me.assets.Count == tradeOffer.ItemsToGive.Length && tradeStatus.them.assets.Count == tradeOffer.ItemsToReceive.Length)
                                                {
                                                    foreach (var item in tradeOffer.ItemsToGive)
                                                    {
                                                        var asset = new TradeAsset(item.AppId, Convert.ToInt64(item.ContextId), item.AssetId.ToString(), item.Amount);
                                                        if (!tradeStatus.me.assets.Contains(asset))
                                                        {
                                                            Console.WriteLine("Could not validate that this trade offer was sent successfully. (1)");
                                                            return 0;
                                                        }
                                                    }
                                                    foreach (var item in tradeOffer.ItemsToReceive)
                                                    {
                                                        var asset = new TradeAsset(item.AppId, Convert.ToInt64(item.ContextId), item.AssetId.ToString(), item.Amount);
                                                        if (!tradeStatus.them.assets.Contains(asset))
                                                        {
                                                            Console.WriteLine("Could not validate that this trade offer was sent successfully. (2)");
                                                            return 0;
                                                        }
                                                    }
                                                    Console.WriteLine("Successfully validated!");
                                                    return tradeOffer.Id;
                                                }
                                            }
                                        }
                                        else if (errorCode == 15)
                                        {
                                            throw new TradeOfferException(errorString, errorCode);
                                        }
                                    }                                    
                                }
                            }
                            catch
                            {

                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    }
                    var jsonResponse = JsonConvert.DeserializeObject<dynamic>(result);
                    try
                    {
                        return Convert.ToUInt64(jsonResponse.tradeofferid);
                    }
                    catch
                    {
                        return 0;
                    }
                }
                catch
                {
                    return 0;
                }
            }

            /// <summary>
            /// Add a bot's item to the trade offer.
            /// </summary>
            /// <param name="asset">TradeAsset object</param>
            /// <returns>True if item hasn't been added already, false if it has.</returns>
            public bool AddMyItem(TradeAsset asset)
            {
                return tradeStatus.me.AddItem(asset);
            }
            /// <summary>
            /// Add a bot's item to the trade offer.
            /// </summary>
            /// <param name="appId">App ID of item</param>
            /// <param name="contextId">Context ID of item</param>
            /// <param name="assetId">Asset (unique) ID of item</param>
            /// <param name="amount">Amount to add (default = 1)</param>
            /// <returns>True if item hasn't been added already, false if it has.</returns>
            public bool AddMyItem(int appId, long contextId, ulong assetId, int amount = 1)
            {
                var asset = new TradeAsset(appId, contextId, assetId.ToString(), amount);
                return tradeStatus.me.AddItem(asset);
            }

            /// <summary>
            /// Add a user's item to the trade offer.
            /// </summary>
            /// <param name="asset">TradeAsset object</param>
            /// <returns>True if item hasn't been added already, false if it has.</returns>
            public bool AddOtherItem(TradeAsset asset)
            {
                return tradeStatus.them.AddItem(asset);
            }
            /// <summary>
            /// Add a user's item to the trade offer.
            /// </summary>
            /// <param name="appId">App ID of item</param>
            /// <param name="contextId">Context ID of item</param>
            /// <param name="assetId">Asset (unique) ID of item</param>
            /// <param name="amount">Amount to add (default = 1)</param>
            /// <returns>True if item hasn't been added already, false if it has.</returns>
            public bool AddOtherItem(int appId, long contextId, ulong assetId, int amount = 1)
            {
                var asset = new TradeAsset(appId, contextId, assetId.ToString(), amount);
                return tradeStatus.them.AddItem(asset);
            }

            public class TradeStatus
            {
                public bool newversion { get; set; }
                public int version { get; set; }
                public TradeStatusUser me { get; set; }
                public TradeStatusUser them { get; set; }
                [JsonIgnore]
                public string message { get; set; }
                [JsonIgnore]
                public int tradeid { get; set; }
            }

            public class TradeStatusUser
            {                
                public List<TradeAsset> assets { get; set; }
                public List<TradeAsset> currency = new List<TradeAsset>();
                public bool ready { get; set; }
                [JsonIgnore]
                public TradeStatus tradeStatus;
                [JsonIgnore]
                public SteamID steamId;

                public TradeStatusUser(ref TradeStatus tradeStatus)
                {
                    this.tradeStatus = tradeStatus;
                    ready = false;
                    assets = new List<TradeAsset>();
                }

                public bool AddItem(TradeAsset asset)
                {
                    if (!assets.Contains(asset))
                    {
                        tradeStatus.version++;
                        assets.Add(asset);
                        return true;
                    }
                    return false;
                }
                public bool AddItem(int appId, long contextId, ulong assetId, int amount = 1)
                {
                    var asset = new TradeAsset(appId, contextId, assetId.ToString(), amount);
                    return AddItem(asset);
                }
            }

            public class TradeAsset : IEquatable<TradeAsset>
            {
                public int appid;
                public long contextid;
                public int amount;
                public string assetid;

                public TradeAsset(int appId, long contextId, string itemId, int amount)
                {
                    this.appid = appId;
                    this.contextid = contextId;
                    this.assetid = itemId;
                    this.amount = amount;
                }

                public bool Equals(TradeAsset tradeAsset)
                {
                    return appid == tradeAsset.appid &&
                        contextid == tradeAsset.contextid &&
                        amount == tradeAsset.amount &&
                        assetid == tradeAsset.assetid;
                }
                public override bool Equals(object obj)
                {
                    if (obj != null)
                        return Equals(obj as TradeAsset);
                    return false;
                }

                public override int GetHashCode()
                {
                    return base.GetHashCode();
                }
            }
        }

        /// <summary>
        /// Accepts a pending trade offer.
        /// </summary>
        /// <param name="trade">A 'TradeStatus' object</param>
        /// <returns>True if successful, false if not</returns>
        public bool AcceptTrade(Trade.TradeStatus trade)
        {
            int tradeId = trade.tradeid;
            var url = "https://steamcommunity.com/tradeoffer/" + tradeId + "/accept";
            var referer = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
            var sessionid = SessionId;
            var data = new NameValueCollection();
            data.Add("sessionid", sessionid);
            data.Add("tradeofferid", tradeId.ToString());
            data.Add("partner", trade.them.steamId.ConvertToUInt64().ToString());
            string response = RetryWebRequest(url, "POST", data, Cookies, true, referer);
            if (string.IsNullOrEmpty(response))
            {
                return ValidateTradeAccept(trade);
            }
            else
            {
                try
                {
                    dynamic json = JsonConvert.DeserializeObject(response);
                    var id = json.tradeid;
                    return true;
                }
                catch
                {
                    return ValidateTradeAccept(trade);
                }
            }
        }

        /// <summary>
        /// Declines a pending trade offer
        /// </summary>
        /// <param name="trade">A 'TradeStatus' object</param>
        /// <returns>True if successful, false if not</returns>
        public bool DeclineTrade(Trade.TradeStatus trade)
        {
            var url = "https://steamcommunity.com/tradeoffer/" + trade.tradeid + "/decline";
            var referer = "http://steamcommunity.com/";
            var sessionid = SessionId;
            var data = new NameValueCollection();
            data.Add("sessionid", sessionid);
            string response = RetryWebRequest(url, "POST", data, Cookies, true, referer);
            try
            {
                dynamic json = JsonConvert.DeserializeObject(response);
                var id = json.tradeofferid;
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Get a list of incoming trade offers.
        /// </summary>
        /// <returns>An 'int' list of trade offer IDs</returns>
        public List<int> GetIncomingTradeOffers()
        {
            List<int> IncomingTradeOffers = new List<int>();
            var url = "http://steamcommunity.com/profiles/" + BotId.ConvertToUInt64() + "/tradeoffers/";
            var html = RetryWebRequest(url, "GET", null, Cookies);
            var reg = new Regex("ShowTradeOffer\\((.*?)\\);");
            var matches = reg.Matches(html);
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    var tradeId = Convert.ToInt32(match.Groups[1].Value.Replace("'", ""));
                    if (!IncomingTradeOffers.Contains(tradeId))
                        IncomingTradeOffers.Add(tradeId);
                }
            }
            return IncomingTradeOffers;
        }

        /// <summary>
        /// Get specific details about a trade offer.
        /// Will not work with Sent Offers.
        /// </summary>
        /// <returns>null if something went wrong</returns>
        /// <param name="tradeId"></param>
        public Trade.TradeStatus GetTradeOffer(int tradeId)
        {
            var url = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
            var html = RetryWebRequest(url, "GET", null, Cookies);
            Regex reg = new Regex("var g_rgCurrentTradeStatus = (.*?);");
            Match m = reg.Match(html);
            if (m.Success)
            {
                try
                {
                    string json = m.Groups[1].Value;
                    var tradeStatus = JsonConvert.DeserializeObject<Trade.TradeStatus>(json);
                    reg = new Regex("var g_ulTradePartnerSteamID = (.*?);");
                    m = reg.Match(html);
                    if (m.Success)
                    {
                        tradeStatus.them.steamId = Convert.ToUInt64(m.Groups[1].Value.Replace("'", ""));
                    }
                    tradeStatus.tradeid = tradeId;

                    reg = new Regex("<div.*?class=\"quote\".*?>.*</div>");
                    m = reg.Match(html);
                    if (m.Success)
                    {
                        tradeStatus.message = m.Groups[1].Value;
                    }
                    else
                    {
                        tradeStatus.message = "";
                    }
                    return tradeStatus;
                }
                catch
                {
                    return null;
                }
            }
            else return null;
        }

        public List<TradeOffer> GetTradeOffers()
        {
            var temp = new List<TradeOffer>();
            var url = "https://api.steampowered.com/IEconService/GetTradeOffers/v1/?key=" + ApiKey + "&get_sent_offers=1&get_received_offers=1&active_only=0";
            var response = RetryWebRequest(url, "GET", null, null, false, "http://steamcommunity.com");
            var json = JsonConvert.DeserializeObject<dynamic>(response);
            var sentTradeOffers = json.response.trade_offers_sent;
            if (sentTradeOffers != null)
            {
                foreach (var tradeOffer in sentTradeOffers)
                {
                    temp.Add(JsonConvert.DeserializeObject<TradeOffer>(Convert.ToString(tradeOffer)));                    
                }
            }
            var receivedTradeOffers = json.response.trade_offers_received;
            if (receivedTradeOffers != null)
            {
                foreach (var tradeOffer in receivedTradeOffers)
                {
                    temp.Add(JsonConvert.DeserializeObject<TradeOffer>(Convert.ToString(tradeOffer)));
                }
            }
            return temp;
        }

        public enum TradeOfferState
        {
            Invalid = 1,
            Active = 2,
            Accepted = 3,
            Countered = 4,
            Expired = 5,
            Canceled = 6,
            Declined = 7,
            InvalidItems = 8
        }

        public class TradeOffer
        {
            [JsonProperty("tradeofferid")]
            public ulong Id { get; set; }

            [JsonProperty("accountid_other")]
            public ulong OtherAccountId { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }

            [JsonProperty("expiration_time")]
            public ulong ExpirationTime { get; set; }

            [JsonProperty("trade_offer_state")]
            private int state { get; set; }
            public TradeOfferState State { get { return (TradeOfferState)state; } set { state = (int)value; } }

            [JsonProperty("items_to_give")]
            private CEconAsset[] itemsToGive { get; set; }
            public CEconAsset[] ItemsToGive
            {
                get
                {
                    if (itemsToGive == null)
                    {
                        return new CEconAsset[0];
                    }
                    else return itemsToGive;
                }
                set { itemsToGive = value; }
            }

            [JsonProperty("items_to_receive")]
            private CEconAsset[] itemsToReceive { get; set; }
            public CEconAsset[] ItemsToReceive
            {
                get
                {
                    if (itemsToReceive == null)
                    {
                        return new CEconAsset[0];
                    }
                    else return itemsToReceive;
                }
                set { itemsToReceive = value; }
            }

            [JsonProperty("is_our_offer")]
            public bool IsOurOffer { get; set; }

            [JsonProperty("time_created")]
            public ulong TimeCreated { get; set; }

            [JsonProperty("time_updated")]
            public ulong TimeUpdated { get; set; }
        }

        public class CEconAsset
        {
            [JsonProperty("appid")]
            public int AppId { get; set; }

            [JsonProperty("contextid")]
            public ulong ContextId { get; set; }

            [JsonProperty("assetid")]
            public ulong AssetId { get; set; }

            [JsonProperty("currencyid")]
            public ulong CurrencyId { get; set; }

            [JsonProperty("classid")]
            public ulong ClassId { get; set; }

            [JsonProperty("instanceid")]
            public ulong InstanceId { get; set; }

            [JsonProperty("amount")]
            public int Amount { get; set; }

            [JsonProperty("missing")]
            public bool IsMissing { get; set; }
        }

        /// <summary>
        /// Manually validate if a trade offer went through by checking /inventoryhistory/
        /// </summary>
        /// <param name="trade">A 'TradeStatus' object</param>
        /// <returns>True if the trade offer was successfully accepted, false if otherwise</returns>
        public bool ValidateTradeAccept(Trade.TradeStatus trade)
        {
            var history = GetTradeHistory(1);
            foreach (var completedTrade in history)
            {
                var givenItems = new List<Trade.TradeAsset>();
                foreach (var myItem in trade.me.assets)
                {
                    var genericItem = new Trade.TradeAsset(myItem.appid, myItem.contextid, myItem.assetid, myItem.amount);
                    givenItems.Add(genericItem);
                }
                var receivedItems = new List<Trade.TradeAsset>();
                foreach (var otherItem in trade.them.assets)
                {
                    var genericItem = new Trade.TradeAsset(otherItem.appid, otherItem.contextid, otherItem.assetid, otherItem.amount);
                    receivedItems.Add(genericItem);
                }
                if (givenItems.Count == completedTrade.GivenItems.Count && receivedItems.Count == completedTrade.ReceivedItems.Count)
                {
                    foreach (var item in completedTrade.GivenItems)
                    {
                        var genericItem = new Trade.TradeAsset(item.appid, item.contextid, item.assetid, item.amount);
                        if (!givenItems.Contains(genericItem))
                            return false;
                    }
                    foreach (var item in completedTrade.ReceivedItems)
                    {
                        var genericItem = new Trade.TradeAsset(item.appid, item.contextid, item.assetid, item.amount);
                        if (!receivedItems.Contains(genericItem))
                            return false;
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Retrieves completed trades from /inventoryhistory/
        /// </summary>
        /// <param name="limit">Max number of trades to retrieve</param>
        /// <returns>A List of 'TradeHistory' objects</returns>
        public List<TradeHistory> GetTradeHistory(int limit = 0)
        {
            // most recent trade is first
            List<TradeHistory> TradeHistoryList = new List<TradeHistory>();
            var url = "http://steamcommunity.com/profiles/" + BotId.ConvertToUInt64() + "/inventoryhistory/";
            var html = RetryWebRequest(url, "GET", null, Cookies);
            // TODO: handle rgHistoryCurrency as well
            Regex reg = new Regex("rgHistoryInventory = (.*?)};");
            Match m = reg.Match(html);
            if (m.Success)
            {
                var json = m.Groups[1].Value + "}";
                var schemaResult = JsonConvert.DeserializeObject<Dictionary<int, Dictionary<long, Dictionary<ulong, GenericInventory.Inventory.Item>>>>(json);
                var trades = new Regex("HistoryPageCreateItemHover\\((.*?)\\);");
                var tradeMatches = trades.Matches(html);
                foreach (Match match in tradeMatches)
                {
                    if (match.Success)
                    {
                        var tradeHistoryItem = new TradeHistory();
                        tradeHistoryItem.ReceivedItems = new List<Trade.TradeAsset>();
                        tradeHistoryItem.GivenItems = new List<Trade.TradeAsset>();
                        var historyString = match.Groups[1].Value.Replace("'", "").Replace(" ", "");
                        var split = historyString.Split(',');
                        var tradeString = split[0];
                        var tradeStringSplit = tradeString.Split('_');
                        var tradeNum = Convert.ToInt32(tradeStringSplit[0].Replace("trade", ""));
                        if (limit > 0 && tradeNum >= limit) break;
                        var appId = Convert.ToInt32(split[1]);
                        var contextId = Convert.ToInt64(split[2]);
                        var itemId = Convert.ToUInt64(split[3]);
                        var amount = Convert.ToInt32(split[4]);
                        var historyItem = schemaResult[appId][contextId][itemId];
                        var genericItem = new Trade.TradeAsset(appId, contextId, itemId.ToString(), amount);
                        // given item has ownerId of 0
                        // received item has ownerId of own SteamID
                        if (historyItem.OwnerId == 0)
                            tradeHistoryItem.GivenItems.Add(genericItem);
                        else
                            tradeHistoryItem.ReceivedItems.Add(genericItem);
                        TradeHistoryList.Add(tradeHistoryItem);
                    }
                }
            }
            return TradeHistoryList;
        }

        public class TradeHistory
        {
            public List<Trade.TradeAsset> ReceivedItems { get; set; }
            public List<Trade.TradeAsset> GivenItems { get; set; }
        }

        private static string RetryWebRequest(string url, string method, NameValueCollection data, CookieContainer cookies, bool ajax = false, string referer = "")
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var response = SteamWeb.Request(url, method, data, cookies, ajax, referer);
                    using (System.IO.Stream responseStream = response.GetResponseStream())
                    {
                        using (var reader = new System.IO.StreamReader(responseStream))
                        {
                            string result = reader.ReadToEnd();
                            if (string.IsNullOrEmpty(result))
                            {
                                Console.WriteLine("Web request failed (status: {0}). Retrying...", response.StatusCode);
                                System.Threading.Thread.Sleep(1000);
                            }
                            else
                            {
                                return result;
                            }
                        }
                    }
                }
                catch (WebException ex)
                {
                    try
                    {
                        if (ex.Status == WebExceptionStatus.ProtocolError)
                        {
                            Console.WriteLine("Status Code: {0}, {1}", (int)((HttpWebResponse)ex.Response).StatusCode, ((HttpWebResponse)ex.Response).StatusDescription);
                        }
                        Console.WriteLine("Error: {0}", new System.IO.StreamReader(ex.Response.GetResponseStream()).ReadToEnd());
                    }
                    catch
                    {

                    }                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            return "";
        }
    }
}