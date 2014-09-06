using SteamKit2;
using System;
using System.Collections.Generic;
using SteamTrade;
using System.Linq;
using Newtonsoft.Json;

namespace SteamBot
{
    public class PendingStorageRetrieval : IEquatable<PendingStorageRetrieval>
    {
        public ulong OrderId { get; set; }
        public ulong UserId { get; set; }
        public ulong StorageBotId { get; set; }
        public ulong TradeOfferId { get; set; }
        public List<string> ItemIdsMappedToOriginalIds { get; set; }
        public List<ulong> AllOrderItemIds { get; set; }

        public static List<PendingStorageRetrieval> LoadStorageRetrievals()
        {
            var temp = new List<PendingStorageRetrieval>();
            var pendingFile = "pendingStorageRetrievals.txt";
            if (!System.IO.File.Exists(pendingFile)) System.IO.File.Create(pendingFile).Close();
            var content = System.IO.File.ReadAllLines(pendingFile);
            foreach (var line in content)
            {
                var pendingStorageRetrieval = new PendingStorageRetrieval();
                if (string.IsNullOrEmpty(line)) continue;
                var split = line.Split(';');
                var listingAndItems = split[0].Split('|');
                var listingId = Convert.ToUInt64(listingAndItems[0]);
                
                var itemIdsMappedToOriginalIdsList = new List<string>();
                var itemIdsMappedToOriginalIds = listingAndItems[1].Split(',');
                foreach (var itemIdMappedToOriginalId in itemIdsMappedToOriginalIds)
                {
                    itemIdsMappedToOriginalIdsList.Add(itemIdMappedToOriginalId);
                }                
                var allOrderItemIdsSplit = split[2].Split(',');
                var allOrderItemIds = new List<ulong>();
                foreach (var item in allOrderItemIdsSplit)
                {
                    allOrderItemIds.Add(Convert.ToUInt64(item));
                }                
                var userIdAndStorageBotIdAndTradeId = split[1].Split('|');
                var userIdAndStorageBotId = userIdAndStorageBotIdAndTradeId[0].Split('_');
                var userId = Convert.ToUInt64(userIdAndStorageBotId[0]);
                var storageBotId = Convert.ToUInt64(userIdAndStorageBotId[1]);
                var userIdAndStorageBotIdPair = new KeyValuePair<ulong, ulong>(userId, storageBotId);
                var tradeOfferId = Convert.ToUInt64(userIdAndStorageBotIdAndTradeId[1]);

                pendingStorageRetrieval.OrderId = listingId;
                pendingStorageRetrieval.UserId = userId;
                pendingStorageRetrieval.StorageBotId = storageBotId;
                pendingStorageRetrieval.TradeOfferId = tradeOfferId;
                pendingStorageRetrieval.ItemIdsMappedToOriginalIds = itemIdsMappedToOriginalIdsList;
                pendingStorageRetrieval.AllOrderItemIds = allOrderItemIds;
                temp.Add(pendingStorageRetrieval);
            }
            return temp;
        }

        public bool Equals(PendingStorageRetrieval pendingStorageRetrieval)
        {
            return OrderId == pendingStorageRetrieval.OrderId &&
                        UserId == pendingStorageRetrieval.UserId &&
                        StorageBotId == pendingStorageRetrieval.StorageBotId &&
                        TradeOfferId == pendingStorageRetrieval.TradeOfferId &&
                        ItemIdsMappedToOriginalIds.HasSameElementsAs(pendingStorageRetrieval.ItemIdsMappedToOriginalIds) &&
                        AllOrderItemIds.HasSameElementsAs(pendingStorageRetrieval.AllOrderItemIds);
        }

        public override bool Equals(object obj)
        {
            if (obj != null)
            {
                try
                {
                    var orderId = Convert.ToUInt64(obj);
                    if (orderId == OrderId)
                        return true;
                }
                catch
                {
                    return Equals(obj as PendingStorageRetrieval);
                }
            }            
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class PendingOrder : IEquatable<PendingOrder>
    {
        public ulong OrderId { get; set; }
        public ulong TradeOfferId { get; set; }
        public List<string> ItemIdsMappedToOriginalIds { get; set; }

        public static List<PendingOrder> LoadPendingOrders()
        {
            var temp = new List<PendingOrder>();
            var pendingFile = "pendingOrders.txt";
            if (!System.IO.File.Exists(pendingFile)) System.IO.File.Create(pendingFile).Close();
            var content = System.IO.File.ReadAllLines(pendingFile);
            foreach (var line in content)
            {
                if (string.IsNullOrEmpty(line)) continue;
                var split = line.Split(';');
                var listingIdAndItemIdsMappedToOriginalIds = split[0].Split('|');
                var listingId = Convert.ToUInt64(listingIdAndItemIdsMappedToOriginalIds[0]);
                var itemIdsMappedToOriginalIdsList = new List<string>();
                var itemIdsMappedToOriginalIds = listingIdAndItemIdsMappedToOriginalIds[1].Split(',');
                foreach (var itemIdMappedToOriginalId in itemIdsMappedToOriginalIds)
                {
                    itemIdsMappedToOriginalIdsList.Add(itemIdMappedToOriginalId);
                }
                var tradeofferId = Convert.ToUInt64(split[1]);

                var pendingOrder = new PendingOrder();
                pendingOrder.OrderId = listingId;
                pendingOrder.TradeOfferId = tradeofferId;
                pendingOrder.ItemIdsMappedToOriginalIds = itemIdsMappedToOriginalIdsList;
                temp.Add(pendingOrder);
            }
            return temp;
        }

        public bool Equals(PendingOrder pendingOrder)
        {            
            return OrderId == pendingOrder.OrderId &&
                    TradeOfferId == pendingOrder.TradeOfferId &&
                    ItemIdsMappedToOriginalIds.HasSameElementsAs(pendingOrder.ItemIdsMappedToOriginalIds);
        }

        public override bool Equals(object obj)
        {
            if (obj != null)
            {
                return Equals(obj as PendingOrder);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class TradeUserHandler : UserHandler
    {
        static TradeOffers TradeOffers;
        static List<PendingStorageRetrieval> PendingStorageRetrievals = new List<PendingStorageRetrieval>();
        static List<PendingOrder> PendingOrders = new List<PendingOrder>();
        static List<string> ProcessingEvents = new List<string>();

        static bool ongoingAction = false;
        static bool isCheckingTradeOffers = false;

        public TradeUserHandler(Bot bot, SteamID sid)
            : base(bot, sid)
        {
            TradeOffers = new TradeOffers(MySID, bot.apiKey, bot.sessionId, bot.token);
        }

        public override void OnLoginCompleted()
        {
            AddInventoriesToFetch(GenericInventory.InventoryTypes.CSGO);
            Bot.log.Info("Loading pending storage retrievals...");
            PendingStorageRetrievals = PendingStorageRetrieval.LoadStorageRetrievals();
            Bot.log.Info(PendingStorageRetrievals.Count + " loaded.");
            Bot.log.Info("Loading pending orders...");
            PendingOrders = PendingOrder.LoadPendingOrders();
            Bot.log.Info(PendingOrders.Count + " loaded.");
        }

        void CheckTradeOffersThread()
        {
            while (true)
            {
                CheckTradeOffers();
                System.Threading.Thread.Sleep(2000);
            }
        }

        public override void OnChannelSubscribed()
        {
            Bot.log.Success("Listening for events!");
            new System.Threading.Thread(() =>
            {
                while (true)
                {
                    if (!ongoingAction)
                    {
                        try
                        {
                            var messages = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(PostGetMessages());
                            foreach (dynamic messageJson in messages)
                            {
                                Message message = JsonConvert.DeserializeObject<Message>(messageJson.ToString());
                                if (!ProcessingEvents.Contains(JsonConvert.SerializeObject(message.Data)))
                                {
                                    ProcessingEvents.Add(JsonConvert.SerializeObject(message.Data));
                                    if (message.EventName == "paidOrder")
                                    {
                                        new System.Threading.Thread(() => { OnPaidOrder(message.Data); }).Start();
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Bot.log.Error(ex.ToString());
                        }
                    }
                    System.Threading.Thread.Sleep(60 * 1000);
                }
            }).Start();
            Bot.Channel.Bind("paidOrder", (dynamic data) =>
            {
                ProcessingEvents.Add(JsonConvert.SerializeObject(data));
                new System.Threading.Thread(() => { OnPaidOrder(data); }).Start();
            });            
            new System.Threading.Thread(CheckTradeOffersThread).Start();
            base.OnChannelSubscribed();
        }

        public void OnPaidOrder(dynamic data)
        {
            ongoingAction = true;
            Bot.log.Info("paidOrder event fired!");            
            var botOrderItems = new Dictionary<ulong, List<ulong>>(); // botId, list of original itemIds
            var allOrderItemIds = new List<ulong>();
            ulong orderId = 0;
            ulong userId = 0;
            foreach (var order in data)
            {
                orderId = Convert.ToUInt64(order.order_id);               
                userId = Convert.ToUInt64(order.user_id);
                ulong thisBotId = Convert.ToUInt64(order.bot_id);
                ulong itemId = Convert.ToUInt64(order.item_id);
                if (!botOrderItems.ContainsKey(thisBotId))
                    botOrderItems.Add(thisBotId, new List<ulong>());
                botOrderItems[thisBotId].Add(itemId);
                allOrderItemIds.Add(itemId);
            }
            bool alreadyInTradeBot = false;
            foreach (var storageBotItem in botOrderItems)
            {
                var botId = storageBotItem.Key;
                var originalItemIds = storageBotItem.Value;
                try
                {
                    var itemIdsMappedToOriginalIds = new List<string>();
                    var tradeOffer = TradeOffers.CreateTrade(botId);
                    var storageBotInventory = CSGOInventory.FetchInventory(botId, Bot.apiKey);
                    foreach (var item in storageBotInventory.Items)
                    {
                        if (botOrderItems[botId].Contains(item.OriginalId))
                        {
                            tradeOffer.AddOtherItem(730, 2, item.Id);
                            itemIdsMappedToOriginalIds.Add(item.Id + "_" + item.OriginalId);
                        }
                    }                    
                    foreach (var pendingStorageRetrieval in PendingStorageRetrievals)
                    {
                        if (pendingStorageRetrieval.OrderId == orderId)
                        {
                            bool isDuplicate = true;
                            foreach (var element in itemIdsMappedToOriginalIds)
                            {
                                if (!pendingStorageRetrieval.ItemIdsMappedToOriginalIds.Contains(element))
                                    isDuplicate = false;
                            }
                            if (isDuplicate)
                            {
                                Bot.log.Warn("This is a duplicate event (pending storage retrieval state). Ignoring.");
                                ongoingAction = false;
                                return;
                            }                                                        
                        }
                    }
                    if (botOrderItems[botId].Count == tradeOffer.tradeStatus.them.assets.Count)
                    {
                        Bot.log.Info(string.Format("Sending trade offer to Storage Bot Name: {0} (SteamID: {1}) for retrieval...", Bot.SteamFriends.GetFriendPersonaName(botId), botId));
                        var tradeId = tradeOffer.SendTrade(Convert.ToString(orderId));
                        if (tradeId > 0)
                        {
                            Bot.log.Success("Successfully sent trade offer! TradeOfferID: " + tradeId);
                            var tradeUrl = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
                            AddPendingStorageRetrieval(orderId, userId, botId, tradeId, itemIdsMappedToOriginalIds, allOrderItemIds);
                        }
                        else
                        {
                            // handle this
                            Bot.log.Error("Failed to send trade offer!");
                        }
                    }
                    else
                    {
                        // handle this
                        if (tradeOffer.tradeStatus.them.assets.Count == 0)
                        {
                            // item is already in trade bot
                            alreadyInTradeBot = true;
                        }
                        else
                        {
                            Bot.log.Error(string.Format("Couldn't add all items to trade offer! Found {0}, expecting {1}.", tradeOffer.tradeStatus.them.assets.Count, botOrderItems[botId].Count));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Bot.log.Error(ex.ToString());
                    System.Threading.Thread.Sleep(2000);
                }
            }
            if (alreadyInTradeBot)
            {
                Bot.log.Info("Creating trade offer for user " + userId + "...");
                var orderTradeOffer = TradeOffers.CreateTrade(userId);
                var tradeToken = PostGetTradeToken(userId);
                var itemIds = new List<ulong>();
                var itemIdsMappedToOriginalIds = new List<string>();
                var myInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
                foreach (var inventoryItem in myInventory.Items)
                {
                    if (allOrderItemIds.Contains(inventoryItem.OriginalId))
                    {
                        orderTradeOffer.AddMyItem(730, 2, inventoryItem.Id);
                        itemIdsMappedToOriginalIds.Add(inventoryItem.Id + "_" + inventoryItem.OriginalId);
                        Bot.log.Info("Adding my item with original id " + inventoryItem.OriginalId + " to trade offer...");
                    }
                }
                foreach (var pendingOrder in PendingOrders)
                {
                    if (pendingOrder.OrderId == orderId)
                    {
                        bool isDuplicate = true;
                        foreach (var element in itemIdsMappedToOriginalIds)
                        {
                            if (!pendingOrder.ItemIdsMappedToOriginalIds.Contains(element))
                                isDuplicate = false;
                        }
                        if (isDuplicate)
                        {
                            Bot.log.Warn("This is a duplicate event (pending order state). Ignoring.");
                            ongoingAction = false;
                            return;
                        }                        
                    }
                }
                if (allOrderItemIds.Count == orderTradeOffer.tradeStatus.me.assets.Count)
                {
                    Bot.log.Info("Sending trade offer to user...");
                    var tradeCode = Bot.GenerateTradeCode();
                    try
                    {
                        var tradeId = orderTradeOffer.SendTradeWithToken("Order | Security Code " + tradeCode, tradeToken);
                        if (tradeId > 0)
                        {
                            Bot.log.Success("Successfully sent trade offer. TradeOfferID: " + tradeId);
                            var tradeUrl = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
                            RemovePendingStorageRetrieval(orderId, userId, 0, tradeId, new List<string>(), new List<ulong>());
                            PostTransfer(tradeUrl, orderId, tradeCode);
                            AddPendingOrder(orderId, Convert.ToUInt64(tradeId), itemIdsMappedToOriginalIds);
                        }
                        else
                        {
                            Bot.log.Error("Error sending trade offer!");
                        }
                    }
                    catch (TradeOfferException ex)
                    {
                        if (ex.ErrorCode == 15)
                        {
                            Bot.log.Warn("Unable to send the trade offer. Possibly an invalid trade token.");
                            PostInvalidTradeURL(userId);
                        }
                    }
                }
                else
                {
                    Bot.log.Error("Couldn't add all order #" + orderId + " items to the trade offer! Trying again next check. (Recorded count: " + orderTradeOffer.tradeStatus.me.assets.Count + ", expected count: " + allOrderItemIds.Count);
                }
            }
            ongoingAction = false;
        }

        #region PendingDicts
        void AddPendingStorageRetrieval(ulong orderId, ulong userId, ulong storageBotId, ulong tradeOfferId, List<string> itemIdsMappedToOriginalIds, List<ulong> allOrderItemIds)
        {
            var pendingFile = "pendingStorageRetrievals.txt";
            var content = System.IO.File.ReadAllText(pendingFile);
            var line = Environment.NewLine;
            var listingAndItems = orderId + "|" + string.Join(",", itemIdsMappedToOriginalIds.ToArray());
            line += listingAndItems + ";" + userId + "_" + storageBotId + "|" + tradeOfferId + ";" + string.Join(",", allOrderItemIds.ToArray());
            content += line;
            System.IO.File.WriteAllText(pendingFile, content);

            var pendingStorageRetrieval = new PendingStorageRetrieval();
            pendingStorageRetrieval.OrderId = orderId;
            pendingStorageRetrieval.UserId = userId;
            pendingStorageRetrieval.StorageBotId = storageBotId;
            pendingStorageRetrieval.TradeOfferId = tradeOfferId;
            pendingStorageRetrieval.ItemIdsMappedToOriginalIds = itemIdsMappedToOriginalIds;
            pendingStorageRetrieval.AllOrderItemIds = allOrderItemIds;
            PendingStorageRetrievals.Add(pendingStorageRetrieval);
        }

        void RemovePendingStorageRetrieval(ulong orderId, ulong userId, ulong storageBotId, ulong tradeOfferId, List<string> itemIdsMappedToOriginalIds, List<ulong> allOrderItemIds)
        {
            var pendingFile = "pendingStorageRetrievals.txt";
            if (storageBotId == 0)
            {
                var sb = new System.Text.StringBuilder();
                var content = System.IO.File.ReadAllLines(pendingFile);
                foreach (var line in content)
                {
                    if (!line.StartsWith(orderId.ToString() + "|"))
                    {
                        sb.AppendLine(line);
                    }
                }
                System.IO.File.WriteAllText(pendingFile, sb.ToString());

                var pendingStorageRetrievals = PendingStorageRetrievals.ToList();
                foreach (var pendingStorageRetrieval in pendingStorageRetrievals)
                {
                    if (pendingStorageRetrieval.Equals(orderId))
                    {
                        PendingStorageRetrievals.Remove(pendingStorageRetrieval);
                    }
                }
            }
            else
            {
                var content = System.IO.File.ReadAllText(pendingFile);
                var line = Environment.NewLine;
                var listingAndItems = orderId + "|" + string.Join(",", itemIdsMappedToOriginalIds.ToArray());
                line += listingAndItems + ";" + userId + "_" + storageBotId + "|" + tradeOfferId + ";" + string.Join(",", allOrderItemIds.ToArray());
                content = content.Replace(line, "");
                System.IO.File.WriteAllText(pendingFile, content);

                var pendingStorageRetrieval = new PendingStorageRetrieval();
                pendingStorageRetrieval.OrderId = orderId;
                pendingStorageRetrieval.UserId = userId;
                pendingStorageRetrieval.StorageBotId = storageBotId;
                pendingStorageRetrieval.TradeOfferId = tradeOfferId;
                pendingStorageRetrieval.ItemIdsMappedToOriginalIds = itemIdsMappedToOriginalIds;
                pendingStorageRetrieval.AllOrderItemIds = allOrderItemIds;
                foreach (var obj in PendingStorageRetrievals)
                {
                    if (pendingStorageRetrieval.Equals(obj))
                    {
                        PendingStorageRetrievals.Remove(obj);
                        break;
                    }
                }
            }
        }

        void AddPendingOrder(ulong orderId, ulong tradeofferId, List<string> itemIdsMappedToOriginalIds)
        {
            var pendingFile = "pendingOrders.txt";
            var content = System.IO.File.ReadAllText(pendingFile);
            content += Environment.NewLine + orderId + "|" + string.Join(",", itemIdsMappedToOriginalIds.ToArray()) + ";" + tradeofferId;
            System.IO.File.WriteAllText(pendingFile, content);
            
            var pendingOrder = new PendingOrder();
            pendingOrder.OrderId = orderId;
            pendingOrder.TradeOfferId = tradeofferId;
            pendingOrder.ItemIdsMappedToOriginalIds = itemIdsMappedToOriginalIds;
            if (!PendingOrders.Contains(pendingOrder))
                PendingOrders.Add(pendingOrder);
        }

        void RemovePendingOrder(ulong orderId, ulong tradeofferId, List<string> itemIdsMappedToOriginalIds)
        {
            var pendingFile = "pendingOrders.txt";
            if (itemIdsMappedToOriginalIds.Count == 0)
            {
                var sb = new System.Text.StringBuilder();
                var content = System.IO.File.ReadAllLines(pendingFile);
                foreach (var line in content)
                {
                    if (!line.StartsWith(orderId.ToString() + "|"))
                    {
                        sb.AppendLine(line);
                    }
                }
                System.IO.File.WriteAllText(pendingFile, sb.ToString());
            }
            else
            {
                var content = System.IO.File.ReadAllText(pendingFile);
                content = content.Replace(Environment.NewLine + orderId + "|" + string.Join(",", itemIdsMappedToOriginalIds.ToArray()) + ";" + tradeofferId.ToString(), "");
                System.IO.File.WriteAllText(pendingFile, content);
            }

            var pendingOrder = new PendingOrder();
            pendingOrder.OrderId = orderId;
            pendingOrder.TradeOfferId = tradeofferId;
            pendingOrder.ItemIdsMappedToOriginalIds = itemIdsMappedToOriginalIds;
            PendingOrders.Remove(pendingOrder);
        }
        #endregion

        #region APIPosts
        string PostGetMessages()
        {
            Bot.log.Info("Submitting /api/messages/...");
            var postUrl = "http://staging.csgoshop.com/api/messages/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("key", Bot.CSGOShopAPIKey);
            postUrl += "?sig=" + Bot.CreateSHA256(Bot.CSGOShopAPIKey, Bot.CSGOShopAPIKey);
            return SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com");
        }

        void PostTransfer(string tradeUrl, ulong orderId, string tradeCode)
        {
            Bot.log.Info("Submitting /api/transfer/...");
            var postUrl = "http://staging.csgoshop.com/api/transfer/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("bot_id", Bot.SteamUser.SteamID.ConvertToUInt64().ToString());
            data.Add("key", Bot.CSGOShopAPIKey);
            data.Add("order_id", orderId.ToString());
            data.Add("trade_code", tradeCode);
            data.Add("trade_url", tradeUrl);
            postUrl += "?sig=" + Bot.CreateSHA256(Bot.SteamUser.SteamID.ConvertToUInt64() + Bot.CSGOShopAPIKey + orderId + tradeCode + tradeUrl, Bot.CSGOShopAPIKey);
            Bot.log.Info("/api/transfer/ response:" + SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com"));
        }

        void PostTransferComplete(ulong orderId)
        {
            Bot.log.Info("Submitting /api/transferComplete/...");
            var postUrl = "http://staging.csgoshop.com/api/transferComplete/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("key", Bot.CSGOShopAPIKey);
            data.Add("order_id", orderId.ToString());
            postUrl += "?sig=" + Bot.CreateSHA256(Bot.CSGOShopAPIKey + orderId, Bot.CSGOShopAPIKey);
            Bot.log.Info("/api/transferComplete/ response:" + SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com"));
        }

        string PostGetTradeToken(ulong userId)
        {
            Bot.log.Info("Submitting /api/tradeUrl/...");
            var postUrl = "http://staging.csgoshop.com/api/tradeUrl/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("key", Bot.CSGOShopAPIKey);
            data.Add("user_id", userId.ToString());
            postUrl += "?sig=" + Bot.CreateSHA256(Bot.CSGOShopAPIKey + userId, Bot.CSGOShopAPIKey);
            var response = SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com");
            var json = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(response);
            Bot.log.Info("Encountered: \r\n" + response);
            var tradeUrl = new Uri(Convert.ToString(json.trade_url)); // <--- problem is here
            var token = System.Web.HttpUtility.ParseQueryString(tradeUrl.Query).Get("token");
            return token;
        }

        List<ulong> PostGetStorageBotIds()
        {
            var steamIds = new List<ulong>();
            Bot.log.Info("Submitting /api/storage/...");
            var postUrl = "http://staging.csgoshop.com/api/storage/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("key", Bot.CSGOShopAPIKey);
            postUrl += "?sig=" + Bot.CreateSHA256(Bot.CSGOShopAPIKey, Bot.CSGOShopAPIKey);
            var response = SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com");
            Bot.log.Info("/api/storage response: " + response);
            var json = JsonConvert.DeserializeObject<dynamic>(response);
            var botIds = json.bot_ids;
            foreach (var botId in botIds)
            {
                steamIds.Add(Convert.ToUInt64(botId));
            }
            return steamIds;
        }

        void PostInvalidTradeURL(ulong userId)
        {
            Bot.log.Info("Submitting /api/invalidTradeURL/...");
            var postUrl = "http://staging.csgoshop.com/api/invalidTradeURL/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("key", Bot.CSGOShopAPIKey);
            data.Add("user_id", userId.ToString());
            postUrl += "?sig=" + Bot.CreateSHA256(Bot.CSGOShopAPIKey + userId, Bot.CSGOShopAPIKey);
            Bot.log.Info("/api/invalidTradeURL/ response:" + SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com"));
        }
        #endregion

        #region UserHandlerFunctions
        public override bool OnGroupAdd()
        {
            return false;
        }

        public override bool OnFriendAdd()
        {
            return true;
        }

        public override void OnChatRoomMessage(SteamID chatID, SteamID sender, string message)
        {
            Log.Info(Bot.SteamFriends.GetFriendPersonaName(sender) + ": " + message);
            base.OnChatRoomMessage(chatID, sender, message);
        }

        public override void OnFriendRemove() { }

        public override void OnMessage(string message, EChatEntryType type)
        {
            message = message.ToLower();
            if (IsAdmin)
            {
                if (message == "retrieve")
                {
                    var tradeOffer = TradeOffers.CreateTrade(OtherSID);
                    var myInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
                    foreach (var item in myInventory.Items)
                    {
                        tradeOffer.AddMyItem(item.AppId, item.ContextId, item.Id);
                    }
                    var tradeId = tradeOffer.SendTrade("");
                    if (tradeId > 0)
                    {
                        Bot.log.Success("Successfully sent a trade offer for all my items.");
                    }
                }
            }
            else
            {
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, Bot.ChatResponse);
            }
        }

        public override bool OnTradeRequest()
        {
            Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "You can't trade with me directly!");
            return false;
        }

        public override void OnTradeError(string error) { }

        public override void OnTradeTimeout() { }

        public override void OnTradeInit() { }

        public override void OnTradeAddItem(GenericInventory.Inventory.Item inventoryItem) { }

        public override void OnTradeRemoveItem(GenericInventory.Inventory.Item inventoryItem) { }

        public override void OnTradeMessage(string message) { }

        public override void OnTradeReady(bool ready) { }

        public override void OnTradeSuccess() { }

        public override void OnTradeAccept()
        {

        }
        #endregion

        void CheckTradeOffers()
        {
            if (!isCheckingTradeOffers)
            {
                isCheckingTradeOffers = true;
                ongoingAction = true;
                var completedStorageRetrievals = new List<PendingStorageRetrieval>();
                var completedOrders = new List<PendingOrder>();

                var list = TradeOffers.GetTradeOffers();
                foreach (var tradeOffer in list)
                {
                    #region PendingStorageRetrievals
                    var pendingStorageRetrievals = PendingStorageRetrievals.ToList();
                    foreach (var pendingStorageRetrieval in pendingStorageRetrievals)
                    {
                        if (tradeOffer.Id == pendingStorageRetrieval.TradeOfferId && tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.Accepted)
                        {
                            Bot.log.Success("Storage Bot has accepted the trade offer for order #" + pendingStorageRetrieval.OrderId + "!");
                            completedStorageRetrievals.Add(pendingStorageRetrieval);
                        }
                        else if (tradeOffer.Id == pendingStorageRetrieval.TradeOfferId && tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.InvalidItems)
                        {
                            Bot.log.Warn("The trade offer to the storage bot has been marked as invalid by Steam. Manually checking my inventory to see if it has been accepted...");
                            foreach (var item in pendingStorageRetrievals)
                            {
                                var itemIdToOriginalId = new Dictionary<ulong, ulong>();
                                foreach (var itemIdMappedToOriginalId in item.ItemIdsMappedToOriginalIds)
                                {
                                    var split = itemIdMappedToOriginalId.Split('_');
                                    var itemId = Convert.ToUInt64(split[0]);
                                    var originalId = Convert.ToUInt64(split[1]);
                                    itemIdToOriginalId.Add(itemId, originalId);
                                }
                                var originalItemIds = new List<ulong>();
                                if (tradeOffer.ItemsToReceive != null)
                                {
                                    foreach (var tradeOfferItem in tradeOffer.ItemsToReceive)
                                    {
                                        if (itemIdToOriginalId.ContainsKey(tradeOfferItem.AssetId))
                                        {
                                            var originalItemId = itemIdToOriginalId[tradeOfferItem.AssetId];
                                            originalItemIds.Add(originalItemId);
                                        }
                                    }
                                }
                                if (tradeOffer.ItemsToGive != null)
                                {
                                    foreach (var tradeOfferItem in tradeOffer.ItemsToGive)
                                    {
                                        if (itemIdToOriginalId.ContainsKey(tradeOfferItem.AssetId))
                                        {
                                            var originalItemId = itemIdToOriginalId[tradeOfferItem.AssetId];
                                            originalItemIds.Add(originalItemId);
                                        }
                                    }
                                }
                                if (originalItemIds.Count == item.ItemIdsMappedToOriginalIds.Count)
                                {
                                    var itemIds = new List<ulong>();
                                    var myInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
                                    foreach (var inventoryItem in myInventory.Items)
                                    {
                                        if (originalItemIds.Contains(inventoryItem.OriginalId) && !itemIds.Contains(inventoryItem.Id))
                                            itemIds.Add(inventoryItem.Id);
                                    }
                                    if (itemIds.Count == originalItemIds.Count)
                                    {
                                        Bot.log.Warn("I found all of the items in my inventory, so it will be treated as accepted.");
                                        Bot.log.Success("Storage Bot has accepted the trade offer for order #" + pendingStorageRetrieval.OrderId + "!");
                                        completedStorageRetrievals.Add(pendingStorageRetrieval);
                                    }
                                    else
                                    {
                                        Bot.log.Error(string.Format("I was unable to find all the items for the trade offer in my inventory (found {0}, expecting {1}). Wat do?", itemIds.Count, originalItemIds.Count));
                                    }
                                }
                                else
                                {
                                    Bot.log.Error(string.Format("I was unable to find the original item IDs for all the trade offer items in my inventory (found {0}, expecting {1}). Wat do?", originalItemIds.Count, item.ItemIdsMappedToOriginalIds.Count));
                                }
                            }
                        }
                    }
                    #endregion
                    #region PendingOrders
                    var pendingOrders = PendingOrders.ToList();
                    foreach (var pendingOrder in pendingOrders)
                    {                        
                        if (tradeOffer.Id == pendingOrder.TradeOfferId && tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.Accepted)
                        {
                            Bot.log.Success("User has accepted the trade offer for order #" + pendingOrder.OrderId + ". Order complete!");
                            PostTransferComplete(pendingOrder.OrderId);
                            completedOrders.Add(pendingOrder);
                        }
                        else if (tradeOffer.Id == pendingOrder.TradeOfferId && tradeOffer.State != SteamTrade.TradeOffers.TradeOfferState.Active)
                        {
                            if (tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.InvalidItems)
                            {
                                Bot.log.Warn("The trade offer to the user has been marked as invalid by Steam. Manually checking their inventory to see if it has been accepted...");
                                foreach (var item in pendingOrders)
                                {
                                    var itemIdToOriginalId = new Dictionary<ulong, ulong>();
                                    foreach (var itemIdMappedToOriginalId in item.ItemIdsMappedToOriginalIds)
                                    {
                                        var split = itemIdMappedToOriginalId.Split('_');
                                        var itemId = Convert.ToUInt64(split[0]);
                                        var originalId = Convert.ToUInt64(split[1]);
                                        itemIdToOriginalId.Add(itemId, originalId);
                                    }
                                    var originalItemIds = new List<ulong>();
                                    if (tradeOffer.ItemsToReceive != null)
                                    {
                                        foreach (var tradeOfferItem in tradeOffer.ItemsToReceive)
                                        {
                                            if (itemIdToOriginalId.ContainsKey(tradeOfferItem.AssetId))
                                            {
                                                var originalItemId = itemIdToOriginalId[tradeOfferItem.AssetId];
                                                originalItemIds.Add(originalItemId);
                                            }
                                        }
                                    }
                                    if (tradeOffer.ItemsToGive != null)
                                    {
                                        foreach (var tradeOfferItem in tradeOffer.ItemsToGive)
                                        {
                                            if (itemIdToOriginalId.ContainsKey(tradeOfferItem.AssetId))
                                            {
                                                var originalItemId = itemIdToOriginalId[tradeOfferItem.AssetId];
                                                originalItemIds.Add(originalItemId);
                                            }
                                        }
                                    }
                                    if (originalItemIds.Count == item.ItemIdsMappedToOriginalIds.Count)
                                    {
                                        var itemIds = new List<ulong>();
                                        var id32 = String.Format("STEAM_0:{0}:{1}", tradeOffer.OtherAccountId & 1, tradeOffer.OtherAccountId >> 1);
                                        var userId = new SteamID(id32).ConvertToUInt64();
                                        var userInventory = CSGOInventory.FetchInventory(userId, Bot.apiKey);
                                        foreach (var inventoryItem in userInventory.Items)
                                        {
                                            if (originalItemIds.Contains(inventoryItem.OriginalId) && !itemIds.Contains(inventoryItem.Id))
                                                itemIds.Add(inventoryItem.Id);
                                        }
                                        if (itemIds.Count == originalItemIds.Count)
                                        {
                                            Bot.log.Warn("I found all of the items in my inventory, so it will be treated as accepted.");
                                            Bot.log.Success("User has accepted the trade offer for order #" + pendingOrder.OrderId + ". Order complete!");
                                            PostTransferComplete(pendingOrder.OrderId);
                                            completedOrders.Add(pendingOrder);
                                        }
                                        else
                                        {
                                            Bot.log.Error(string.Format("I was unable to find all the items for the trade offer in the user's inventory (found {0}, expecting {1}). Treating it as declined.", itemIds.Count, originalItemIds.Count));
                                            Bot.log.Warn("User has declined the trade offer for order #" + pendingOrder.OrderId + "!");
                                            // resend item
                                            var tradeToken = PostGetTradeToken(userId);
                                            var resendTradeOffer = TradeOffers.CreateTrade(userId);                                            
                                            foreach (var inventoryItem in tradeOffer.ItemsToGive)
                                            {
                                                resendTradeOffer.AddMyItem(730, 2, inventoryItem.AssetId);
                                            }
                                            var tradeCode = Bot.GenerateTradeCode();
                                            try
                                            {
                                                var tradeId = resendTradeOffer.SendTradeWithToken("Order | Security Code: " + tradeCode, tradeToken);
                                                if (tradeId > 0)
                                                {
                                                    Bot.log.Success("Succesfully resent trade offer. TradeOfferID: " + tradeId);
                                                    var tradeUrl = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
                                                    RemovePendingOrder(pendingOrder.OrderId, pendingOrder.TradeOfferId, pendingOrder.ItemIdsMappedToOriginalIds);
                                                    PostTransfer(tradeUrl, pendingOrder.OrderId, tradeCode);
                                                    AddPendingOrder(pendingOrder.OrderId, tradeId, pendingOrder.ItemIdsMappedToOriginalIds);
                                                }
                                                else
                                                {
                                                    // handle this
                                                    Bot.log.Error("Failed to resend trade offer!");
                                                }
                                            }
                                            catch (TradeOfferException ex)
                                            {
                                                if (ex.ErrorCode == 15)
                                                {
                                                    Bot.log.Warn("Unable to send the trade offer. Possibly an invalid trade token.");
                                                    PostInvalidTradeURL(userId);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Bot.log.Error(string.Format("I was unable to find the original item IDs for all the trade offer items in the user's inventory (found {0}, expecting {1}). Treating it as declined.", originalItemIds.Count, pendingOrder.ItemIdsMappedToOriginalIds.Count));
                                        Bot.log.Warn("User has declined the trade offer for order #" + pendingOrder.OrderId + "!");
                                        var id32 = String.Format("STEAM_0:{0}:{1}", tradeOffer.OtherAccountId & 1, tradeOffer.OtherAccountId >> 1);
                                        var userId = new SteamID(id32).ConvertToUInt64();
                                        // resend item
                                        var tradeToken = PostGetTradeToken(userId);
                                        var resendTradeOffer = TradeOffers.CreateTrade(userId);
                                        foreach (var inventoryItem in tradeOffer.ItemsToGive)
                                        {
                                            resendTradeOffer.AddMyItem(730, 2, inventoryItem.AssetId);
                                        }
                                        var tradeCode = Bot.GenerateTradeCode();
                                        try
                                        {
                                            var tradeId = resendTradeOffer.SendTradeWithToken("Order | Security Code: " + tradeCode, tradeToken);
                                            if (tradeId > 0)
                                            {
                                                Bot.log.Success("Succesfully resent trade offer. TradeOfferID: " + tradeId);
                                                var tradeUrl = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
                                                RemovePendingOrder(pendingOrder.OrderId, tradeOffer.Id, pendingOrder.ItemIdsMappedToOriginalIds);
                                                PostTransfer(tradeUrl, pendingOrder.OrderId, tradeCode);
                                                AddPendingOrder(pendingOrder.OrderId, tradeId, pendingOrder.ItemIdsMappedToOriginalIds);
                                            }
                                            else
                                            {
                                                // handle this
                                                Bot.log.Error("Failed to resend trade offer!");
                                            }
                                        }
                                        catch (TradeOfferException ex)
                                        {
                                            if (ex.ErrorCode == 15)
                                            {
                                                Bot.log.Warn("Unable to send the trade offer. Possibly an invalid trade token.");
                                                PostInvalidTradeURL(userId);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Bot.log.Warn("User has declined the trade offer for order #" + pendingOrder.OrderId + "!");
                                var id32 = String.Format("STEAM_0:{0}:{1}", tradeOffer.OtherAccountId & 1, tradeOffer.OtherAccountId >> 1);
                                var userId = new SteamID(id32).ConvertToUInt64();
                                // resend item
                                var tradeToken = PostGetTradeToken(userId);
                                var resendTradeOffer = TradeOffers.CreateTrade(userId);
                                foreach (var item in tradeOffer.ItemsToGive)
                                {
                                    resendTradeOffer.AddMyItem(730, 2, item.AssetId);
                                }
                                var tradeCode = Bot.GenerateTradeCode();
                                try
                                {
                                    var tradeId = resendTradeOffer.SendTradeWithToken("Order | Security Code: " + tradeCode, tradeToken);
                                    if (tradeId > 0)
                                    {
                                        Bot.log.Success("Succesfully resent trade offer. TradeOfferID: " + tradeId);
                                        var tradeUrl = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
                                        RemovePendingOrder(pendingOrder.OrderId, tradeOffer.Id, pendingOrder.ItemIdsMappedToOriginalIds);
                                        PostTransfer(tradeUrl, pendingOrder.OrderId, tradeCode);
                                        AddPendingOrder(pendingOrder.OrderId, tradeId, pendingOrder.ItemIdsMappedToOriginalIds);
                                    }
                                    else
                                    {
                                        // handle this
                                        Bot.log.Error("Failed to resend trade offer!");
                                    }
                                }
                                catch (TradeOfferException ex)
                                {
                                    if (ex.ErrorCode == 15)
                                    {
                                        Bot.log.Warn("Unable to send the trade offer. Possibly an invalid trade token.");
                                        PostInvalidTradeURL(userId);
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }
                foreach (var item in completedStorageRetrievals)
                {
                    Bot.log.Info("Creating trade offer for user " + item.UserId + "...");
                    var tradeOffer = TradeOffers.CreateTrade(item.UserId);
                    var tradeToken = PostGetTradeToken(item.UserId);
                    var itemIdsMappedToOriginalIds = new List<string>();                    
                    var itemIds = new List<ulong>();
                    var userInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
                    foreach (var inventoryItem in userInventory.Items)
                    {
                        if (item.AllOrderItemIds.Contains(inventoryItem.OriginalId))
                        {
                            tradeOffer.tradeStatus.me.AddItem(730, 2, inventoryItem.Id);
                            Bot.log.Info("Adding my item with original id " + inventoryItem.OriginalId + " to trade offer...");
                            itemIdsMappedToOriginalIds.Add(inventoryItem.Id + "_" + inventoryItem.OriginalId);
                        }
                    }
                    if (item.ItemIdsMappedToOriginalIds.Count == tradeOffer.tradeStatus.me.assets.Count)
                    {
                        Bot.log.Info("Sending trade offer to user...");
                        var tradeCode = Bot.GenerateTradeCode();
                        try
                        {
                            var tradeId = tradeOffer.SendTradeWithToken("Order | Security Code " + tradeCode, tradeToken);
                            if (tradeId > 0)
                            {
                                Bot.log.Success("Successfully sent trade offer. TradeOfferID: " + tradeId);
                                var tradeUrl = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
                                RemovePendingStorageRetrieval(item.OrderId, item.UserId, item.StorageBotId, item.TradeOfferId, item.ItemIdsMappedToOriginalIds, item.AllOrderItemIds);
                                PostTransfer(tradeUrl, item.OrderId, tradeCode);
                                AddPendingOrder(item.OrderId, Convert.ToUInt64(tradeId), itemIdsMappedToOriginalIds);
                            }
                            else
                            {
                                Bot.log.Error("Error sending trade offer!");
                            }
                        }
                        catch (TradeOfferException ex)
                        {
                            if (ex.ErrorCode == 15)
                            {
                                Bot.log.Warn("Unable to send the trade offer. Possibly an invalid trade token.");
                                PostInvalidTradeURL(item.UserId);
                            }
                        }
                    }
                    else
                    {
                        Bot.log.Error("Couldn't add all order #" + item.OrderId + " items to the trade offer! Trying again next check. (Expected count: " + item.ItemIdsMappedToOriginalIds.Count + ", recorded count: " + tradeOffer.tradeStatus.me.assets.Count + "). Possibly because not all storage items have been retrieved yet?");
                    }
                }
                foreach (var item in completedOrders)
                {
                    RemovePendingOrder(item.OrderId, item.TradeOfferId, item.ItemIdsMappedToOriginalIds);
                }
                ongoingAction = false;
                isCheckingTradeOffers = false;
            }
        }
    }
}