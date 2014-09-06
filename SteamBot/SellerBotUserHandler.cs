using SteamKit2;
using System;
using System.Collections.Generic;
using SteamTrade;
using PusherClient;
using System.Linq;
using Newtonsoft.Json;
using System.Threading;

namespace SteamBot
{
    public class PendingItem : IEquatable<PendingItem>
    {
        public ulong ListingId { get; set; }
        public ulong TradeOfferId { get; set; }
        public List<string> ItemIdsMappedToOriginalIds { get; set; }

        public static List<PendingItem> LoadPendingListings()
        {
            return LoadPendingItems("pendingListings.txt");
        }
        public static List<PendingItem> LoadPendingStorage()
        {
            return LoadPendingItems("pendingStorage.txt");
        }
        public static List<PendingItem> LoadPendingReturns()
        {
            return LoadPendingItems("pendingReturns.txt");
        }        
        public static List<PendingItem> LoadPendingItems(string pendingFile)
        {
            var temp = new List<PendingItem>();
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

                var pendingItem = new PendingItem();
                pendingItem.ListingId = listingId;
                pendingItem.TradeOfferId = tradeofferId;
                pendingItem.ItemIdsMappedToOriginalIds = itemIdsMappedToOriginalIdsList;
                temp.Add(pendingItem);
            }
            return temp;
        }

        public bool Equals(PendingItem pendingItem)
        {
            return ListingId == pendingItem.ListingId &&
                TradeOfferId == pendingItem.TradeOfferId &&
                ItemIdsMappedToOriginalIds.HasSameElementsAs(pendingItem.ItemIdsMappedToOriginalIds);
        }

        public override bool Equals(object obj)
        {
            if (obj != null)
            {
                return Equals(obj as PendingItem);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class PendingCheckinCheckout : IEquatable<PendingCheckinCheckout>
    {
        public ulong UserId { get; set; }
        public ulong ItemId { get; set; }
        public ulong TradeOfferId { get; set; }

        public static List<PendingCheckinCheckout> LoadPendingCheckins()
        {
            return LoadPendingCheckinCheckout("pendingCheckins.txt");
        }
        public static List<PendingCheckinCheckout> LoadPendingCheckouts()
        {
            return LoadPendingCheckinCheckout("pendingCheckouts.txt");
        }
        public static List<PendingCheckinCheckout> LoadPendingCheckinCheckout(string pendingFile)
        {
            var temp = new List<PendingCheckinCheckout>();
            if (!System.IO.File.Exists(pendingFile)) System.IO.File.Create(pendingFile).Close();
            var content = System.IO.File.ReadAllLines(pendingFile);
            foreach (var line in content)
            {
                if (string.IsNullOrEmpty(line)) continue;
                var split = line.Split('|');
                var userIdAndItemId = split[0].Split(';');
                var userId = Convert.ToUInt64(userIdAndItemId[0]);
                var itemId = Convert.ToUInt64(userIdAndItemId[1]);
                var tradeOfferId = Convert.ToUInt64(split[1]);

                var pendingItem = new PendingCheckinCheckout();
                pendingItem.UserId = userId;
                pendingItem.ItemId = itemId;
                pendingItem.TradeOfferId = tradeOfferId;
                temp.Add(pendingItem);
            }
            return temp;
        }

        public bool Equals(PendingCheckinCheckout pendingItem)
        {
            return UserId == pendingItem.UserId &&
                ItemId == pendingItem.ItemId &&
                TradeOfferId == pendingItem.TradeOfferId;
        }

        public override bool Equals(object obj)
        {
            if (obj != null)
            {
                return Equals(obj as PendingCheckinCheckout);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class SellerUserHandler : UserHandler
    {
        static TradeOffers TradeOffers;        
        static List<PendingItem> PendingListings = new List<PendingItem>();
        static List<PendingItem> PendingReturns = new List<PendingItem>();        
        static List<PendingItem> PendingStorage = new List<PendingItem>();
        static List<PendingCheckinCheckout> PendingCheckins = new List<PendingCheckinCheckout>();
        static List<PendingCheckinCheckout> PendingCheckouts = new List<PendingCheckinCheckout>();
        static List<string> ProcessingEvents = new List<string>();

        const int MAX_ITEMS = 1000;

        static bool ongoingAction = false;
        static bool isCheckingTradeOffers = false;

        public SellerUserHandler(Bot bot, SteamID sid)
            : base(bot, sid)
        {
            TradeOffers = new TradeOffers(MySID, bot.apiKey, bot.sessionId, bot.token);
        }

        public override void OnLoginCompleted()
        {
            AddInventoriesToFetch(GenericInventory.InventoryTypes.CSGO);
            Bot.log.Info("Loading pending listings...");
            PendingListings = PendingItem.LoadPendingListings();
            Bot.log.Info(PendingListings.Count + " loaded.");
            Bot.log.Info("Loading pending returns...");
            PendingReturns = PendingItem.LoadPendingReturns();
            Bot.log.Info(PendingReturns.Count + " loaded.");
            Bot.log.Info("Loading pending storage...");
            PendingStorage = PendingItem.LoadPendingStorage();
            Bot.log.Info(PendingStorage.Count + " loaded.");
            Bot.log.Info("Loading pending checkouts...");
            PendingCheckouts = PendingCheckinCheckout.LoadPendingCheckouts();
            Bot.log.Info(PendingCheckouts.Count + " loaded.");
            Bot.log.Info("Loading pending checkins...");
            PendingCheckins = PendingCheckinCheckout.LoadPendingCheckins();
            Bot.log.Info(PendingCheckins.Count + " loaded.");
        }

        void CheckSentTradeOffersThread()
        {
            while (true)
            {
                CheckSentTradeOffers();
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
                                    if (message.EventName == "requestBulkListing")
                                    {
                                        new Thread(() => { OnRequestBulkListing(message.Data); }).Start();
                                    }
                                    else if (message.EventName == "denyBulkListing")
                                    {
                                        new Thread(() => { OnDenyBulkListing(message.Data); }).Start();
                                    }
                                    else if (message.EventName == "approveBulkListing")
                                    {
                                        new Thread(() => { OnApproveBulkListing(message.Data); }).Start();
                                    }
                                    else if (message.EventName == "requestListing")
                                    {
                                        new Thread(() => { OnRequestListing(message.Data); }).Start();
                                    }
                                    else if (message.EventName == "denyListing")
                                    {
                                        new Thread(() => { OnDenyListing(message.Data); }).Start();
                                    }
                                    else if (message.EventName == "approveListing")
                                    {
                                        new Thread(() => { OnApproveListing(message.Data); }).Start();
                                    }
                                    else if (message.EventName == "cancelListing")
                                    {
                                        new Thread(() => { OnCancelListing(message.Data); }).Start();
                                    }
                                    else if (message.EventName == "cancelBulkListing")
                                    {
                                        new Thread(() => { OnCancelBulkListing(message.Data); }).Start();
                                    }
                                }
                                else
                                {
                                    Bot.log.Warn("This is a duplicate event. Ignoring.");
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
            Bot.Channel.Bind("requestBulkListing", (dynamic data) =>
            {
                ProcessingEvents.Add(JsonConvert.SerializeObject(data));
                new Thread(() => { OnRequestBulkListing(data); }).Start();
            });
            Bot.Channel.Bind("denyBulkListing", (dynamic data) =>
            {
                ProcessingEvents.Add(JsonConvert.SerializeObject(data));
                new Thread(() => { OnDenyBulkListing(data); }).Start();
            });
            Bot.Channel.Bind("approveBulkListing", (dynamic data) =>
            {
                ProcessingEvents.Add(JsonConvert.SerializeObject(data));
                new Thread(() => { OnApproveBulkListing(data); }).Start();
            });
            Bot.Channel.Bind("requestListing", (dynamic data) =>
            {
                ProcessingEvents.Add(JsonConvert.SerializeObject(data));
                new Thread(() => { OnRequestListing(data); }).Start();
            });
            Bot.Channel.Bind("denyListing", (dynamic data) =>
            {
                ProcessingEvents.Add(JsonConvert.SerializeObject(data));
                new Thread(() => { OnDenyListing(data); }).Start();
            });
            Bot.Channel.Bind("approveListing", (dynamic data) =>
            {
                ProcessingEvents.Add(JsonConvert.SerializeObject(data));
                new Thread(() => { OnApproveListing(data); }).Start();
            });
            Bot.Channel.Bind("cancelListing", (dynamic data) =>
            {
                ProcessingEvents.Add(JsonConvert.SerializeObject(data));
                new Thread(() => { OnCancelListing(data); }).Start();
            });
            Bot.Channel.Bind("cancelBulkListing", (dynamic data) =>
            {
                ProcessingEvents.Add(JsonConvert.SerializeObject(data));
                new Thread(() => { OnCancelBulkListing(data); }).Start();
            });
            new System.Threading.Thread(CheckSentTradeOffersThread).Start();
            base.OnChannelSubscribed();
        }

        void OnRequestBulkListing(dynamic data)
        {
            ongoingAction = true;
            Bot.log.Info("requestBulkListing event fired!");            
            ulong listingId = data.listing_id;            
            ulong userId = data.user_id;
            var originalItemIds = data.items;
            var originalItemIdsList = new List<ulong>();
            foreach (var originalId in originalItemIds)
            {
                originalItemIdsList.Add(Convert.ToUInt64(originalId));
            }
            var tradeToken = PostGetTradeToken(userId);
            Bot.log.Info("Listing ID: " + listingId + ", SteamID: " + userId + ", token: " + tradeToken);
            var itemIds = new List<ulong>();
            var itemIdsMappedToOriginalIds = new List<string>();
            var userInventory = CSGOInventory.FetchInventory(userId, Bot.apiKey);
            foreach (var item in userInventory.Items)
            {
                if (originalItemIdsList.Contains(item.OriginalId) && !itemIds.Contains(item.Id))
                {
                    itemIds.Add(item.Id);
                    itemIdsMappedToOriginalIds.Add(item.Id + "_" + item.OriginalId);
                }
            }
            foreach (var listing in PendingListings.ToList())
            {
                if (listing.ListingId == listingId)
                {
                    bool isDuplicate = true;
                    foreach (var element in itemIdsMappedToOriginalIds)
                    {
                        if (!listing.ItemIdsMappedToOriginalIds.Contains(element))
                            isDuplicate = false;
                    }
                    if (isDuplicate)
                    {
                        Bot.log.Warn("This is a duplicate event. Ignoring.");
                        ongoingAction = false;
                        return;
                    }
                }
            }
            if (itemIds.Count != originalItemIdsList.Count)
            {
                Bot.log.Error(string.Format("Failed to find all items in user's inventory (found {0}, expecting {1}). Private backpack or Steam network down?", itemIds.Count, originalItemIdsList.Count));
                PostRequestCancel(listingId);
            }
            else
            {
                var tradeOffer = TradeOffers.CreateTrade(userId);
                foreach (var itemId in itemIds)
                {
                    tradeOffer.tradeStatus.them.AddItem(730, 2, itemId);
                }
                Bot.log.Info("Sending trade offer to user...");
                var tradeCode = Bot.GenerateTradeCode();
                try
                {
                    var tradeId = tradeOffer.SendTradeWithToken("Listing Request | Security Code: " + tradeCode, tradeToken);
                    if (tradeId > 0)
                    {
                        Bot.log.Success("Successfully sent trade offer. TradeOfferID: " + tradeId);
                        var tradeUrl = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
                        PostListingRequest(tradeUrl, listingId, tradeCode);
                        AddPendingListing(listingId, Convert.ToUInt64(tradeId), itemIdsMappedToOriginalIds);
                    }
                    else
                    {
                        // handle this
                        Bot.log.Error("Failed to send trade offer!");
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
            ongoingAction = false;
        }

        void OnDenyBulkListing(dynamic data)
        {
            ongoingAction = true;
            Bot.log.Info("denyBulkListing event fired!");            
            ulong listingId = data.listing_id;            
            ulong userId = data.user_id;
            var originalItemIds = data.items;
            var originalItemIdsList = new List<ulong>();
            var itemIdsMappedToOriginalIds = new List<string>();
            foreach (var originalId in originalItemIds)
            {
                originalItemIdsList.Add(Convert.ToUInt64(originalId));
            }
            var itemIds = new List<ulong>();
            var tradeToken = PostGetTradeToken(userId);
            var botInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
            foreach (var item in botInventory.Items)
            {
                if (originalItemIdsList.Contains(item.OriginalId) && !itemIds.Contains(item.Id))
                {
                    itemIds.Add(item.Id);
                    itemIdsMappedToOriginalIds.Add(item.Id + "_" + item.OriginalId);
                }
            }
            foreach (var listing in PendingReturns.ToList())
            {
                if (listing.ListingId == listingId)
                {
                    bool isDuplicate = true;
                    foreach (var element in itemIdsMappedToOriginalIds)
                    {
                        if (!listing.ItemIdsMappedToOriginalIds.Contains(element))
                            isDuplicate = false;
                    }
                    if (isDuplicate)
                    {
                        Bot.log.Warn("This is a duplicate event. Ignoring.");
                        ongoingAction = false;
                        return;
                    }
                }
            }
            if (itemIds.Count != originalItemIdsList.Count)
            {
                Bot.log.Error(string.Format("Failed to find all items in my inventory (found {0}, expecting {1}). Steam network down?", itemIds.Count, originalItemIdsList.Count));
            }
            else
            {
                var tradeOffer = TradeOffers.CreateTrade(userId);
                foreach (var itemId in itemIds)
                {
                    tradeOffer.tradeStatus.me.AddItem(730, 2, itemId);
                }
                Bot.log.Info("Sending trade offer with returned item...");
                var tradeCode = Bot.GenerateTradeCode();
                try
                {
                    var tradeId = tradeOffer.SendTradeWithToken("Refund Request | Security Code: " + tradeCode, tradeToken);
                    if (tradeId > 0)
                    {
                        Bot.log.Success("Successfully sent trade offer. TradeOfferID: " + tradeId);
                        var tradeUrl = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
                        PostReturnItem(tradeUrl, listingId, tradeCode);
                        AddPendingReturn(listingId, tradeId, itemIdsMappedToOriginalIds);
                    }
                    else
                    {
                        // handle this
                        Bot.log.Error("Failed to send trade offer!");
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
            ongoingAction = false;
        }

        void OnApproveBulkListing(dynamic data)
        {
            ongoingAction = true;
            Bot.log.Info("approveBulkListing event fired!");            
            ulong listingId = data.listing_id;            
            var items = data.items;
            var originalItemIds = new List<ulong>();
            foreach (var id in items)
            {
                originalItemIds.Add(Convert.ToUInt64(id));
            }
            var itemIds = new List<ulong>();
            var itemIdsMappedToOriginalIds = new List<string>();
            var botInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
            foreach (var item in botInventory.Items)
            {
                if (originalItemIds.Contains(item.OriginalId) && !itemIds.Contains(item.Id))
                {
                    itemIds.Add(item.Id);
                    itemIdsMappedToOriginalIds.Add(item.Id + "_" + item.OriginalId);
                }
            }
            var storageBotIds = PostGetStorageBotIds();
            if (itemIds.Count != originalItemIds.Count)
            {
                // check storage bot
                bool found = false;
                foreach (var steamId in storageBotIds)
                {
                    itemIds.Clear();
                    var inventory = CSGOInventory.FetchInventory(steamId, Bot.apiKey);
                    foreach (var item in inventory.Items)
                    {
                        if (originalItemIds.Contains(item.OriginalId) && !itemIds.Contains(item.Id))
                        {
                            itemIds.Add(item.Id);
                        }
                    }
                    if (itemIds.Count == originalItemIds.Count)
                    {
                        found = true;
                        Bot.log.Success("Storage of listing #" + listingId + " has been accepted by the bot!");
                        PostStoreComplete(listingId, steamId);
                        RemovePendingStorageItem(listingId);
                    }
                }
                if (!found)
                {
                    Bot.log.Error("Failed to find the items in any of the storage bots.");
                }
            }
            else
            {
                ulong chosenStorageBot = 0;
                foreach (var steamId in storageBotIds)
                {
                    var inventory = CSGOInventory.FetchInventory(steamId, Bot.apiKey);
                    if (inventory.Items.Length < MAX_ITEMS - originalItemIds.Count)
                    {
                        chosenStorageBot = steamId;
                        var tradeOffer = TradeOffers.CreateTrade(chosenStorageBot);
                        foreach (var itemId in itemIds)
                        {
                            tradeOffer.AddMyItem(730, 2, itemId);
                        }
                        Bot.log.Info(string.Format("Sending trade offer to Storage Bot Name: {0} (SteamID: {1})...", Bot.SteamFriends.GetFriendPersonaName(chosenStorageBot), chosenStorageBot));
                        var tradeId = tradeOffer.SendTrade(Convert.ToString(listingId));
                        if (tradeId > 0)
                        {
                            Bot.log.Success("Succesfully sent trade offer. TradeOfferID: " + tradeId);
                            AddPendingStorageItem(listingId, Convert.ToUInt64(tradeId), itemIdsMappedToOriginalIds);
                        }
                        else
                        {
                            // handle this
                            Bot.log.Error("Failed to send trade offer!");
                        }
                        break;
                    }
                }
                if (chosenStorageBot == 0)
                {
                    Bot.log.Error("All storage bots are full! Cannot send a trade offer to any of them.");
                }
            }
            ongoingAction = false;
        }

        void OnRequestListing(dynamic data)
        {
            ongoingAction = true;
            Bot.log.Info("requestListing event fired!");            
            ulong listingId = data.listing_id;
            foreach (var listing in PendingListings.ToList())
            {
                if (listing.ListingId == listingId)
                {
                    Bot.log.Warn("This is a duplicate event. Ignoring.");
                    ongoingAction = false;
                    return;
                }
            }
            ulong userId = data.user_id;
            ulong originalItemId = data.item_id;
            Bot.log.Info("Listing ID: " + listingId + ", SteamID: " + userId + ", Original Item ID: " + originalItemId);
            ulong itemId = 0;
            var tradeToken = PostGetTradeToken(userId);
            Bot.log.Info("Trade token: " + tradeToken);
            var userInventory = CSGOInventory.FetchInventory(userId, Bot.apiKey);
            foreach (var item in userInventory.Items)
            {
                if (item.OriginalId == originalItemId)
                    itemId = item.Id;
            }
            if (itemId == 0)
            {
                Bot.log.Error("Failed to find item in user's inventory. Private backpack or Steam network down?");
                PostRequestCancel(listingId);
            }
            else
            {
                var tradeOffer = TradeOffers.CreateTrade(userId);
                tradeOffer.tradeStatus.them.AddItem(730, 2, itemId);
                Bot.log.Info("Sending trade offer to user...");
                var tradeCode = Bot.GenerateTradeCode();
                try
                {
                    var tradeId = tradeOffer.SendTradeWithToken("Listing Request | Security Code: " + tradeCode, tradeToken);
                    if (tradeId > 0)
                    {
                        Bot.log.Success("Successfully sent trade offer. TradeOfferID: " + tradeId);
                        var tradeUrl = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
                        PostListingRequest(tradeUrl, listingId, tradeCode);
                        AddPendingListing(listingId, Convert.ToUInt64(tradeId), new List<string>() { itemId + "_" + originalItemId });
                    }
                    else
                    {
                        // handle this
                        Bot.log.Error("Failed to send trade offer!");
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
            ongoingAction = false;
        }

        void OnDenyListing(dynamic data)
        {
            ongoingAction = true;
            Bot.log.Info("denyListing event fired!");            
            ulong listingId = data.listing_id;
            foreach (var listing in PendingReturns.ToList())
            {
                if (listing.ListingId == listingId)
                {
                    Bot.log.Warn("This is a duplicate event. Ignoring.");
                    ongoingAction = false;
                    return;
                }
            }
            ulong userId = data.user_id;
            ulong originalItemId = data.item_id;
            ulong itemId = 0;
            var tradeToken = PostGetTradeToken(userId);
            var itemIdsMappedToOriginalIds = new List<string>();
            var botInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
            foreach (var item in botInventory.Items)
            {
                if (item.OriginalId == originalItemId)
                {
                    itemId = item.Id;
                    itemIdsMappedToOriginalIds.Add(item.Id + "_" + item.OriginalId);
                }
            }
            if (itemId == 0)
            {
                Bot.log.Error("Failed to find item in my inventory. Steam network down?");
            }
            else
            {
                var tradeOffer = TradeOffers.CreateTrade(userId);
                tradeOffer.AddMyItem(730, 2, itemId);
                Bot.log.Info("Sending trade offer with returned item...");
                var tradeCode = Bot.GenerateTradeCode();
                try
                {
                    var tradeId = tradeOffer.SendTradeWithToken("Refund Request | Security Code: " + tradeCode, tradeToken);
                    if (tradeId > 0)
                    {
                        Bot.log.Success("Successfully sent trade offer. TradeOfferID: " + tradeId);
                        var tradeUrl = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
                        PostReturnItem(tradeUrl, listingId, tradeCode);
                        AddPendingReturn(listingId, Convert.ToUInt64(tradeId), itemIdsMappedToOriginalIds);
                    }
                    else
                    {
                        // handle this
                        Bot.log.Error("Failed to send trade offer!");
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
            ongoingAction = false;
        }

        void OnApproveListing(dynamic data)
        {
            ongoingAction = true;
            Bot.log.Info("approveListing event fired!");            
            ulong listingId = data.listing_id;            
            ulong originalItemId = data.item_id;
            ulong itemId = 0;
            var itemIdsMappedToOriginalIds = new List<string>();
            var botInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
            foreach (var item in botInventory.Items)
            {
                if (item.OriginalId == originalItemId)
                {
                    itemId = item.Id;
                    itemIdsMappedToOriginalIds.Add(item.Id + "_" + item.OriginalId);
                }
            }
            var storageBotIds = PostGetStorageBotIds();
            if (itemId == 0)
            {
                // check if item is in storage bot   
                bool found = false;
                foreach (var storageBotId in storageBotIds)
                {
                    var storageInventory = CSGOInventory.FetchInventory(storageBotId, Bot.apiKey);
                    foreach (var item in storageInventory.Items)
                    {
                        if (item.OriginalId == originalItemId)
                        {
                            found = true;
                            // send storage complete                           
                            Bot.log.Success("Storage of listing #" + listingId + " has been accepted by the bot!");
                            PostStoreComplete(listingId, storageBotId);
                            RemovePendingStorageItem(listingId);
                        }
                    }
                }
                if (!found)
                {
                    Bot.log.Error("Failed to find item in all storage bots.");
                }
            }
            else
            {
                ulong storageBotSteamId = 0;
                foreach (var storageBotId in storageBotIds)
                {
                    var storageInventory = CSGOInventory.FetchInventory(storageBotId, Bot.apiKey);
                    if (storageInventory.Items.Length < MAX_ITEMS)
                    {
                        storageBotSteamId = storageBotId;
                        break;
                    }
                }
                if (storageBotSteamId != 0)
                {
                    var tradeOffer = TradeOffers.CreateTrade(storageBotSteamId);
                    tradeOffer.AddMyItem(730, 2, itemId);
                    Bot.log.Info("Sending trade offer to Storage Bot...");
                    var tradeId = tradeOffer.SendTrade(Convert.ToString(listingId));
                    if (tradeId > 0)
                    {
                        Bot.log.Success("Succesfully sent trade offer. TradeOfferID: " + tradeId);
                        AddPendingStorageItem(listingId, Convert.ToUInt64(tradeId), itemIdsMappedToOriginalIds);
                    }
                    else
                    {
                        // handle this
                        Bot.log.Error("Failed to send trade offer!");
                    }
                }
                else
                {
                    Bot.log.Error("Couldn't find a suitable storage bot. Wat do?");
                }
            }
            ongoingAction = false;
        }

        void OnCancelListing(dynamic data)
        {
            ongoingAction = true;
            Bot.log.Info("cancelListing event fired!");            
            ulong listingId = data.listing_id;            
            ulong userId = data.user_id;
            ulong originalItemId = data.item_id;
            ulong botId = data.bot_id;
            ulong itemId = 0;
            var itemIdsMappedToOriginalIds = new List<string>();
            var botInventory = CSGOInventory.FetchInventory(botId, Bot.apiKey);
            foreach (var item in botInventory.Items)
            {
                if (item.OriginalId == originalItemId)
                {
                    itemId = item.Id;
                    itemIdsMappedToOriginalIds.Add(item.Id + "_" + item.OriginalId);
                }
            }
            if (itemId == 0)
            {
                bool found = false;
                itemIdsMappedToOriginalIds.Clear();
                var myInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
                foreach (var item in myInventory.Items)
                {
                    if (item.OriginalId == originalItemId)
                    {
                        itemIdsMappedToOriginalIds.Add(item.Id + "_" + item.OriginalId);
                        Bot.log.Success("Item retrieved from storage bot!");
                        var returnTradeOffer = TradeOffers.CreateTrade(userId);
                        returnTradeOffer.AddMyItem(730, 2, item.Id);
                        var tradeToken = PostGetTradeToken(userId);
                        var tradeCode = Bot.GenerateTradeCode();
                        Bot.log.Info("Sending trade offer to Steam ID " + userId + " (trade token: " + tradeToken + ") with return item...");
                        try
                        {
                            var refundTradeId = returnTradeOffer.SendTradeWithToken("Refund Request | Security Code: " + tradeCode, tradeToken);
                            if (refundTradeId > 0)
                            {
                                Bot.log.Success("Successfully sent trade offer. TradeOfferID: " + refundTradeId);
                                var tradeUrl = "http://steamcommunity.com/tradeoffer/" + refundTradeId + "/";
                                PostReturnItem(tradeUrl, listingId, tradeCode);
                                AddPendingReturn(listingId, Convert.ToUInt64(refundTradeId), itemIdsMappedToOriginalIds);
                                found = true;
                            }
                            else
                            {
                                // handle this
                                Bot.log.Error("Failed to send trade offer!");
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
                        break;
                    }
                }
                if (!found)
                {
                    Bot.log.Error("Couldn't find return item in any storage bot.");
                }
            }
            else
            {
                foreach (var listing in PendingReturns.ToList())
                {
                    if (listing.ListingId == listingId)
                    {
                        bool isDuplicate = true;
                        foreach (var element in itemIdsMappedToOriginalIds)
                        {
                            if (!listing.ItemIdsMappedToOriginalIds.Contains(element))
                                isDuplicate = false;
                        }
                        if (isDuplicate)
                        {
                            Bot.log.Warn("This is a duplicate event. Ignoring.");
                            ongoingAction = false;
                            return;
                        }
                    }
                }
                var tradeOffer = TradeOffers.CreateTrade(botId);
                tradeOffer.AddOtherItem(730, 2, itemId);
                Bot.log.Info("Sending trade offer with to retrieve return item from storage bot...");
                var tradeId = tradeOffer.SendTrade(Convert.ToString(listingId));
                if (tradeId > 0)
                {
                    itemIdsMappedToOriginalIds.Clear();
                    bool waiting = true;
                    while (waiting)
                    {                        
                        var myInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
                        foreach (var item in myInventory.Items)
                        {
                            if (item.OriginalId == originalItemId)
                            {
                                itemIdsMappedToOriginalIds.Add(item.Id + "_" + item.OriginalId);
                                Bot.log.Success("Item retrieved from storage bot!");
                                var returnTradeOffer = TradeOffers.CreateTrade(userId);
                                returnTradeOffer.AddMyItem(730, 2, item.Id);
                                var tradeToken = PostGetTradeToken(userId);
                                var tradeCode = Bot.GenerateTradeCode();
                                Bot.log.Info("Sending trade offer to Steam ID " + userId + " (trade token: " + tradeToken + ") with return item...");
                                try
                                {
                                    var refundTradeId = returnTradeOffer.SendTradeWithToken("Refund Request | Security Code: " + tradeCode, tradeToken);
                                    if (refundTradeId > 0)
                                    {
                                        Bot.log.Success("Successfully sent trade offer. TradeOfferID: " + refundTradeId);
                                        var tradeUrl = "http://steamcommunity.com/tradeoffer/" + refundTradeId + "/";
                                        PostReturnItem(tradeUrl, listingId, tradeCode);
                                        AddPendingReturn(listingId, Convert.ToUInt64(refundTradeId), itemIdsMappedToOriginalIds);
                                        waiting = false;
                                        break;
                                    }
                                    else
                                    {
                                        // handle this
                                        Bot.log.Error("Failed to send trade offer!");
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
                                break;
                            }
                        }
                        if (waiting)
                        {
                            Bot.log.Info("Storage Bot hasn't accepted trade offer yet. Sleeping 2.1 seconds before checking again...");
                            System.Threading.Thread.Sleep(2100);
                        }
                    }
                }
            }
            ongoingAction = false;
        }

        void OnCancelBulkListing(dynamic data)
        {
            ongoingAction = true;
            Bot.log.Info("cancelBulkListing event fired!");            
            ulong listingId = data.listing_id;            
            ulong userId = data.user_id;
            var items = data.items;
            var originalItemIds = new List<ulong>();
            foreach (var id in items)
            {
                originalItemIds.Add(Convert.ToUInt64(id));
            }
            var itemIds = new List<ulong>();
            ulong botId = data.bot_id;
            var botInventory = CSGOInventory.FetchInventory(botId, Bot.apiKey);
            foreach (var item in botInventory.Items)
            {
                if (originalItemIds.Contains(item.OriginalId) && !itemIds.Contains(item.Id))
                    itemIds.Add(item.Id);
            }
            if (itemIds.Count != originalItemIds.Count)
            {
                if (itemIds.Count == 0)
                {
                    itemIds.Clear();
                    var itemIdsMappedToOriginalIds = new List<string>();
                    var myInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
                    foreach (var item in myInventory.Items)
                    {
                        if (originalItemIds.Contains(item.OriginalId) && !itemIds.Contains(item.Id))
                        {
                            itemIds.Add(item.Id);
                            itemIdsMappedToOriginalIds.Add(item.Id + "_" + item.OriginalId);
                        }
                    }
                    if (itemIds.Count == originalItemIds.Count)
                    {

                        Bot.log.Success("Items retrieved from storage bot!");
                        var returnTradeOffer = TradeOffers.CreateTrade(userId);
                        foreach (var itemId in itemIds)
                        {
                            returnTradeOffer.AddMyItem(730, 2, itemId);
                        }
                        Bot.log.Info("Sending trade offer with return item...");
                        var tradeToken = PostGetTradeToken(userId);
                        var tradeCode = Bot.GenerateTradeCode();
                        try
                        {
                            var refundTradeId = returnTradeOffer.SendTradeWithToken("Refund Request | Security Code: " + tradeCode, tradeToken);
                            if (refundTradeId > 0)
                            {
                                Bot.log.Success("Successfully sent trade offer. TradeOfferID: " + refundTradeId);
                                var tradeUrl = "http://steamcommunity.com/tradeoffer/" + refundTradeId + "/";
                                PostReturnItem(tradeUrl, listingId, tradeCode);
                                AddPendingReturn(listingId, Convert.ToUInt64(refundTradeId), itemIdsMappedToOriginalIds);
                            }
                            else
                            {
                                // handle this
                                Bot.log.Error("Failed to send trade offer!");
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
                        Bot.log.Error("Failed to find all items in storage bot and my inventory.");
                    }
                }
                else
                {
                    Bot.log.Error("Failed to find all items in storage bot " + botId + " inventory (found " + itemIds.Count + ", expecting " + itemIds.Count + ". Steam network down?");
                }
            }
            else
            {
                foreach (var listing in PendingReturns.ToList())
                {
                    if (listing.ListingId == listingId)
                    {
                        Bot.log.Warn("This is a duplicate event. Ignoring.");
                        ongoingAction = false;
                        return;
                    }
                }
                var tradeOffer = TradeOffers.CreateTrade(botId);
                foreach (var itemId in itemIds)
                {
                    tradeOffer.AddOtherItem(730, 2, itemId);
                }
                Bot.log.Info("Sending trade offer with to retrieve return item from storage bot...");
                var tradeId = tradeOffer.SendTrade(Convert.ToString(listingId));
                if (tradeId > 0)
                {
                    bool waiting = true;
                    while (waiting)
                    {
                        itemIds.Clear();
                        var itemIdsMappedToOriginalIds = new List<string>();
                        var myInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
                        foreach (var item in myInventory.Items)
                        {
                            if (originalItemIds.Contains(item.OriginalId) && !itemIds.Contains(item.Id))
                            {
                                itemIds.Add(item.Id);
                                itemIdsMappedToOriginalIds.Add(item.Id + "_" + item.OriginalId);
                            }
                        }
                        if (itemIds.Count == originalItemIds.Count)
                        {

                            Bot.log.Success("Items retrieved from storage bot!");
                            var returnTradeOffer = TradeOffers.CreateTrade(userId);
                            foreach (var itemId in itemIds)
                            {
                                returnTradeOffer.AddMyItem(730, 2, itemId);
                            }
                            Bot.log.Info("Sending trade offer with return item...");
                            var tradeToken = PostGetTradeToken(userId);
                            var tradeCode = Bot.GenerateTradeCode();
                            try
                            {
                                var refundTradeId = returnTradeOffer.SendTradeWithToken("Refund Request | Security Code: " + tradeCode, tradeToken);
                                if (refundTradeId > 0)
                                {
                                    Bot.log.Success("Successfully sent trade offer. TradeOfferID: " + refundTradeId);
                                    var tradeUrl = "http://steamcommunity.com/tradeoffer/" + refundTradeId + "/";
                                    PostReturnItem(tradeUrl, listingId, tradeCode);
                                    AddPendingReturn(listingId, Convert.ToUInt64(refundTradeId), itemIdsMappedToOriginalIds);
                                    waiting = false;
                                    break;
                                }
                                else
                                {
                                    // handle this
                                    Bot.log.Error("Failed to send trade offer!");
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
                        if (waiting)
                        {
                            Bot.log.Info("Storage Bot hasn't accepted trade offer yet. Sleeping 2.1 seconds before checking again...");
                            System.Threading.Thread.Sleep(2100);
                        }
                        System.Threading.Thread.Sleep(2100);
                    }
                }
            }
            ongoingAction = false;
        }

        void OnCheckin(ulong itemId, ulong userId)
        {
            ongoingAction = true;
            Bot.log.Info("Checkin requested!");            
            var tradeOffer = TradeOffers.CreateTrade(userId);
            var userInventory = CSGOInventory.FetchInventory(userId, Bot.apiKey);
            foreach (var item in userInventory.Items)
            {
                if (item.OriginalId == itemId)
                {
                    tradeOffer.AddOtherItem(730, 2, item.Id);
                    break;
                }
            }
            if (tradeOffer.tradeStatus.them.assets.Count > 0)
            {
                var tradeToken = PostGetTradeToken(userId);
                try
                {
                    var tradeId = tradeOffer.SendTradeWithToken("Checkin item #" + itemId, tradeToken);
                    if (tradeId > 0)
                    {
                        Bot.log.Info("Successfully sent trade offer for checkin item #" + itemId + ". TradeOfferID: " + tradeId);
                        AddPendingCheckin(userId, itemId, tradeId);
                        Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "Successfully sent trade offer for checkin item #" + itemId + ".");
                    }
                    else
                    {
                        Bot.log.Error("Failed to send trade offer!");
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
                Bot.log.Error("Item not found!");
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "Item not found!");
            }
            ongoingAction = false;
        }

        void OnCheckout(ulong itemId, ulong userId)
        {
            ongoingAction = true;
            Bot.log.Info("Checkout requested!");            
            var tradeOffer = TradeOffers.CreateTrade(userId);
            var myInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
            foreach (var item in myInventory.Items)
            {
                if (item.OriginalId == itemId)
                {
                    tradeOffer.AddMyItem(730, 2, item.Id);
                    break;
                }
            }
            if (tradeOffer.tradeStatus.me.assets.Count > 0)
            {
                var tradeToken = PostGetTradeToken(userId);
                try
                {
                    var tradeId = tradeOffer.SendTradeWithToken("Checkout item #" + itemId, tradeToken);
                    if (tradeId > 0)
                    {
                        Bot.log.Info("Successfully sent trade offer for checkout item #" + itemId + ". TradeOfferID: " + tradeId);
                        AddPendingCheckout(userId, itemId, tradeId);
                        Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "Successfully sent trade offer for checkout item #" + itemId + ".");
                    }
                    else
                    {
                        Bot.log.Error("Failed to send trade offer!");
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
                Bot.log.Error("Item not found!");
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "Item not found!");
            }
            ongoingAction = false;
        }

        #region PendingDicts
        void AddPendingStorageItem(ulong listingId, ulong tradeOfferId, List<string> itemIdsMappedToOriginalIds)
        {
            AddPendingItem("pendingStorage.txt", listingId, tradeOfferId, itemIdsMappedToOriginalIds);
            var pendingStorage = new PendingItem();
            pendingStorage.ListingId = listingId;
            pendingStorage.TradeOfferId = tradeOfferId;
            pendingStorage.ItemIdsMappedToOriginalIds = itemIdsMappedToOriginalIds;
            if (!PendingStorage.Contains(pendingStorage))
                PendingStorage.Add(pendingStorage);
        }
        void RemovePendingStorageItem(ulong listingId)
        {
            RemovePendingItem("pendingStorage.txt", listingId);
            foreach (var item in PendingStorage.ToList())
            {
                if (item.ListingId == listingId)
                    PendingStorage.Remove(item);
            }
        }

        void AddPendingReturn(ulong listingId, ulong tradeOfferId, List<string> itemIdsMappedToOriginalIds)
        {
            AddPendingItem("pendingReturns.txt", listingId, tradeOfferId, itemIdsMappedToOriginalIds);
            var pendingReturn = new PendingItem();
            pendingReturn.ListingId = listingId;
            pendingReturn.TradeOfferId = tradeOfferId;
            pendingReturn.ItemIdsMappedToOriginalIds = itemIdsMappedToOriginalIds;
            if (!PendingReturns.Contains(pendingReturn))
                PendingReturns.Add(pendingReturn);
        }
        void RemovePendingReturn(ulong listingId)
        {
            RemovePendingItem("pendingReturns.txt", listingId);
            foreach (var item in PendingReturns.ToList())
            {
                if (item.ListingId == listingId)
                    PendingReturns.Remove(item);
            }
        }

        void AddPendingListing(ulong listingId, ulong tradeOfferId, List<string> itemIdsMappedToOriginalIds)
        {
            AddPendingItem("pendingListings.txt", listingId, tradeOfferId, itemIdsMappedToOriginalIds);
            var pendingListing = new PendingItem();
            pendingListing.ListingId = listingId;
            pendingListing.TradeOfferId = tradeOfferId;
            pendingListing.ItemIdsMappedToOriginalIds = itemIdsMappedToOriginalIds;
            if (!PendingListings.Contains(pendingListing))
                PendingListings.Add(pendingListing);
        }
        void RemovePendingListing(ulong listingId)
        {
            RemovePendingItem("pendingListings.txt", listingId);
            foreach (var item in PendingListings.ToList())
            {
                if (item.ListingId == listingId)
                    PendingListings.Remove(item);
            }
        }

        void AddPendingItem(string pendingFile, ulong listingId, ulong tradeOfferId, List<string> itemIdsMappedToOriginalIds)
        {
            var content = System.IO.File.ReadAllText(pendingFile);
            content += Environment.NewLine + listingId + "|" + string.Join(",", itemIdsMappedToOriginalIds.ToArray()) + ";" + tradeOfferId;
            System.IO.File.WriteAllText(pendingFile, content);
        }
        void RemovePendingItem(string pendingFile, ulong listingId)
        {
            var sb = new System.Text.StringBuilder();
            var content = System.IO.File.ReadAllLines(pendingFile);
            foreach (var line in content)
            {
                if (!line.StartsWith(listingId.ToString() + "|"))
                {
                    sb.AppendLine(line);
                }
            }
            System.IO.File.WriteAllText(pendingFile, sb.ToString());
        }

        void AddPendingCheckin(ulong userId, ulong itemId, ulong tradeOfferId)
        {
            AddPendingCheckinCheckout("pendingCheckins.txt", userId, itemId, tradeOfferId);
            var pendingCheckin = new PendingCheckinCheckout();
            pendingCheckin.UserId = userId;
            pendingCheckin.ItemId = itemId;
            pendingCheckin.TradeOfferId = tradeOfferId;
            PendingCheckins.Add(pendingCheckin);
        }
        void RemovePendingCheckin(ulong userId, ulong itemId, ulong tradeOfferId)
        {
            RemovePendingCheckinCheckout("pendingCheckins.txt", userId, itemId, tradeOfferId);
            foreach (var item in PendingCheckins.ToList())
            {
                if (item.ItemId == itemId && item.UserId == userId && item.TradeOfferId == tradeOfferId)
                    PendingCheckins.Remove(item);
            }
        }

        void AddPendingCheckout(ulong userId, ulong itemId, ulong tradeOfferId)
        {
            AddPendingCheckinCheckout("pendingCheckouts.txt", userId, itemId, tradeOfferId);
            var pendingCheckout = new PendingCheckinCheckout();
            pendingCheckout.UserId = userId;
            pendingCheckout.ItemId = itemId;
            pendingCheckout.TradeOfferId = tradeOfferId;
            PendingCheckouts.Add(pendingCheckout);
        }
        void RemovePendingCheckout(ulong userId, ulong itemId, ulong tradeOfferId)
        {
            RemovePendingCheckinCheckout("pendingCheckouts.txt", userId, itemId, tradeOfferId);
            foreach (var item in PendingCheckouts.ToList())
            {
                if (item.ItemId == itemId && item.UserId == userId && item.TradeOfferId == tradeOfferId)
                    PendingCheckouts.Remove(item);
            }
        }

        void AddPendingCheckinCheckout(string pendingFile, ulong userId, ulong itemId, ulong tradeOfferId)
        {
            var content = System.IO.File.ReadAllText(pendingFile);
            content += Environment.NewLine + userId + ";" + itemId + "|" + tradeOfferId;
            System.IO.File.WriteAllText(pendingFile, content);
        }
        void RemovePendingCheckinCheckout(string pendingFile, ulong userId, ulong itemId, ulong tradeOfferId)
        {
            var sb = new System.Text.StringBuilder();
            var content = System.IO.File.ReadAllLines(pendingFile);
            foreach (var line in content)
            {
                if (line != userId + ";" + itemId + "|" + tradeOfferId)
                {
                    sb.AppendLine(line);
                }
            }
            System.IO.File.WriteAllText(pendingFile, sb.ToString());
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

        void PostListingRequest(string tradeUrl, ulong listingId, string tradeCode)
        {
            Bot.log.Info("Submitting /api/request/...");
            var postUrl = "http://staging.csgoshop.com/api/request/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("bot_id", Bot.SteamUser.SteamID.ConvertToUInt64().ToString());
            data.Add("key", Bot.CSGOShopAPIKey);
            data.Add("listing_id", listingId.ToString());
            data.Add("trade_code", tradeCode);
            data.Add("trade_url", tradeUrl);
            postUrl += "?sig=" + Bot.CreateSHA256(Bot.SteamUser.SteamID.ConvertToUInt64() + Bot.CSGOShopAPIKey + listingId + tradeCode + tradeUrl, Bot.CSGOShopAPIKey);
            Bot.log.Info("/api/request/ response:" + SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com"));
        }

        void PostListingCompleteRequest(ulong listingId)
        {
            Bot.log.Info("Submitting /api/requestComplete/...");
            var postUrl = "http://staging.csgoshop.com/api/requestComplete/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("key", Bot.CSGOShopAPIKey);
            data.Add("listing_id", listingId.ToString());
            postUrl += "?sig=" + Bot.CreateSHA256(Bot.CSGOShopAPIKey + listingId, Bot.CSGOShopAPIKey);
            Bot.log.Info("/api/requestComplete/ response:" + SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com"));
        }

        void PostReturnItem(string tradeUrl, ulong listingId, string tradeCode)
        {
            Bot.log.Info("Submitting /api/return/...");
            var postUrl = "http://staging.csgoshop.com/api/return/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("bot_id", Bot.SteamUser.SteamID.ConvertToUInt64().ToString());
            data.Add("key", Bot.CSGOShopAPIKey);
            data.Add("listing_id", listingId.ToString());
            data.Add("trade_code", tradeCode);
            data.Add("trade_url", tradeUrl);
            postUrl += "?sig=" + Bot.CreateSHA256(Bot.SteamUser.SteamID.ConvertToUInt64() + Bot.CSGOShopAPIKey + listingId + tradeCode + tradeUrl, Bot.CSGOShopAPIKey);
            Bot.log.Info("/api/return/ response:" + SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com"));
        }

        void PostReturnItemComplete(ulong listingId)
        {
            Bot.log.Info("Submitting /api/returnComplete/...");
            var postUrl = "http://staging.csgoshop.com/api/returnComplete/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("key", Bot.CSGOShopAPIKey);
            data.Add("listing_id", listingId.ToString());
            postUrl += "?sig=" + Bot.CreateSHA256(Bot.CSGOShopAPIKey + listingId, Bot.CSGOShopAPIKey);
            Bot.log.Info("/api/returnComplete/ response:" + SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com"));
        }

        void PostStoreComplete(ulong listingId, ulong userId)
        {
            Bot.log.Info("Submitting /api/storeComplete/...");
            var postUrl = "http://staging.csgoshop.com/api/storeComplete/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("bot_id", userId.ToString());
            data.Add("key", Bot.CSGOShopAPIKey);
            data.Add("listing_id", listingId.ToString());
            postUrl += "?sig=" + Bot.CreateSHA256(userId.ToString() + Bot.CSGOShopAPIKey + listingId, Bot.CSGOShopAPIKey);
            Bot.log.Info("/api/storeComplete/ response:" + SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com"));
        }

        void PostRequestCancel(ulong listingId)
        {
            Bot.log.Info("Submitting /api/requestCancel/...");
            var postUrl = "http://staging.csgoshop.com/api/requestCancel/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("key", Bot.CSGOShopAPIKey);
            data.Add("listing_id", listingId.ToString());
            postUrl += "?sig=" + Bot.CreateSHA256(Bot.CSGOShopAPIKey + listingId, Bot.CSGOShopAPIKey);
            Bot.log.Info("/api/requestCancel/ response:" + SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com"));
        }

        string PostGetTradeToken(ulong userId)
        {            
            Bot.log.Info("Submitting /api/tradeUrl/...");
            try
            {
                var postUrl = "http://staging.csgoshop.com/api/tradeUrl/";
                var data = new System.Collections.Specialized.NameValueCollection();
                data.Add("key", Bot.CSGOShopAPIKey);
                data.Add("user_id", userId.ToString());
                postUrl += "?sig=" + Bot.CreateSHA256(Bot.CSGOShopAPIKey + userId, Bot.CSGOShopAPIKey);
                var response = SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com");
                var json = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(response);
                var tradeUrl = new Uri((string)json.trade_url);
                var token = System.Web.HttpUtility.ParseQueryString(tradeUrl.Query).Get("token");
                return token;
            }
            catch
            {
                return "";
            }            
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

        void PostCheckin(ulong itemId)
        {
            Bot.log.Info("Submitting /api/checkin/...");
            var postUrl = "http://staging.csgoshop.com/api/checkin/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("item_id", itemId.ToString());
            data.Add("key", Bot.CSGOShopAPIKey);
            postUrl += "?sig=" + Bot.CreateSHA256(itemId + Bot.CSGOShopAPIKey, Bot.CSGOShopAPIKey);
            Bot.log.Info("/api/checkin/ response:" + SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com"));
        }

        void PostCheckout(ulong itemId, ulong userId)
        {
            Bot.log.Info("Submitting /api/checkout/...");
            var postUrl = "http://staging.csgoshop.com/api/checkout/";
            var data = new System.Collections.Specialized.NameValueCollection();
            data.Add("item_id", itemId.ToString());
            data.Add("key", Bot.CSGOShopAPIKey);
            data.Add("user_id", userId.ToString());
            postUrl += "?sig=" + Bot.CreateSHA256(itemId + Bot.CSGOShopAPIKey + userId, Bot.CSGOShopAPIKey);
            Bot.log.Info("/api/checkout/ response:" + SteamWeb.Fetch(postUrl, "POST", data, null, false, "http://staging.csgoshop.com"));
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
                    var myInventory = new GenericInventory(MySID, MySID);
                    foreach (var item in myInventory.GetInventory(730, 2))
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
            if (message.StartsWith("checkin"))
            {
                bool hasAccess = false;
                foreach (var user in Bot.CheckinCheckoutUsers)
                {
                    if (user == OtherSID)
                    {
                        hasAccess = true;
                        break;
                    }
                }
                if (hasAccess)
                {
                    var args = message.Trim().Split(' ');
                    if (args.Length > 2 || args.Length == 0)
                    {
                        Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "Invalid parameters. Command is: \"checkin ##original_item_id##\"");
                    }
                    else if (args.Length == 2)
                    {
                        ulong itemId = 0;
                        ulong.TryParse(args[1], out itemId);
                        if (itemId == 0)
                        {
                            Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "\"" + args[1] + "\" is not a valid item ID!");
                        }
                        else
                        {
                            OnCheckin(itemId, OtherSID);
                        }
                    }
                }
            }
            else if (message.StartsWith("checkout"))
            {
                bool hasAccess = false;
                foreach (var user in Bot.CheckinCheckoutUsers)
                {
                    if (user == OtherSID)
                    {
                        hasAccess = true;
                        break;
                    }
                }
                if (hasAccess)
                {
                    var args = message.Trim().Split(' ');
                    if (args.Length > 2 || args.Length == 0)
                    {
                        Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "Invalid parameters. Command is: \"checkout ##original_item_id##\"");
                    }
                    else if (args.Length == 2)
                    {
                        ulong itemId = 0;
                        ulong.TryParse(args[1], out itemId);
                        if (itemId == 0)
                        {
                            Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, "\"" + args[1] + "\" is not a valid item ID!");
                        }
                        else
                        {
                            OnCheckout(itemId, OtherSID);
                        }
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

        public override void OnTradeAccept() { }
        #endregion

        void CheckSentTradeOffers()
        {
            if (!isCheckingTradeOffers)
            {
                isCheckingTradeOffers = true;
                ongoingAction = true;
                var completedListings = new List<PendingItem>();
                var completedReturns = new List<PendingItem>();
                var completedStorage = new List<PendingItem>();

                var list = TradeOffers.GetTradeOffers();
                foreach (var tradeOffer in list)
                {
                    #region PendingListings
                    var pendingListings = PendingListings.ToList();
                    foreach (var pendingListing in pendingListings)
                    {
                        if (pendingListing.TradeOfferId == tradeOffer.Id && tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.Accepted)
                        {
                            Bot.log.Success("Listing #" + pendingListing.ListingId + "'s trade offer has been accepted!");
                            PostListingCompleteRequest(pendingListing.ListingId);
                            completedListings.Add(pendingListing);
                        }
                        else if (pendingListing.TradeOfferId == tradeOffer.Id && tradeOffer.State != SteamTrade.TradeOffers.TradeOfferState.Active)
                        {
                            if (tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.InvalidItems)
                            {
                                var itemIdToOriginalId = new Dictionary<ulong, ulong>();
                                foreach (var itemIdMappedToOriginalId in pendingListing.ItemIdsMappedToOriginalIds)
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
                                if (originalItemIds.Count == pendingListing.ItemIdsMappedToOriginalIds.Count)
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
                                        Bot.log.Warn("Trade offer's state says the items are unavailable but I found all of them in my inventory, so it will be treated as accepted.");
                                        Bot.log.Success("Listing #" + pendingListing.ListingId + "'s trade offer has been accepted!");
                                        PostListingCompleteRequest(pendingListing.ListingId);
                                        completedListings.Add(pendingListing);
                                    }
                                    else
                                    {
                                        Bot.log.Error(string.Format("I was unable to find all the items for the trade offer in my inventory (found {0}, expecting {1}). Treating this listing as declined.", itemIds.Count, originalItemIds.Count));
                                        Bot.log.Warn("Listing #" + pendingListing.ListingId + "'s trade offer was declined!");
                                        PostRequestCancel(pendingListing.ListingId);
                                        completedListings.Add(pendingListing);
                                    }
                                }
                                else
                                {
                                    Bot.log.Error(string.Format("I was unable to find the original item IDs for all the trade offer items in my inventory (found {0}, expecting {1}). Treating this listing as declined.", originalItemIds.Count, pendingListing.ItemIdsMappedToOriginalIds.Count));
                                    Bot.log.Warn("Listing #" + pendingListing.ListingId + "'s trade offer was declined!");
                                    PostRequestCancel(pendingListing.ListingId);
                                    completedListings.Add(pendingListing);
                                }
                            }
                            else
                            {
                                Bot.log.Warn("Listing #" + pendingListing.ListingId + "'s trade offer was declined!");
                                PostRequestCancel(pendingListing.ListingId);
                                completedListings.Add(pendingListing);
                            }
                        }
                    }
                    #endregion
                    #region PendingReturns
                    var pendingReturns = PendingReturns.ToList();
                    foreach (var pendingReturn in pendingReturns)
                    {
                        if (pendingReturn.TradeOfferId == tradeOffer.Id && tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.Accepted)
                        {
                            Bot.log.Success("Return #" + pendingReturn.ListingId + "'s trade offer has been accepted!");
                            PostReturnItemComplete(pendingReturn.ListingId);
                            RemovePendingReturn(pendingReturn.ListingId);
                        }
                        else if (pendingReturn.TradeOfferId == tradeOffer.Id && tradeOffer.State != SteamTrade.TradeOffers.TradeOfferState.Active)
                        {
                            var id32 = String.Format("STEAM_0:{0}:{1}", tradeOffer.OtherAccountId & 1, tradeOffer.OtherAccountId >> 1);
                            var userId = new SteamID(id32).ConvertToUInt64();
                            if (tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.InvalidItems)
                            {
                                var itemIdToOriginalId = new Dictionary<ulong, ulong>();
                                foreach (var itemIdMappedToOriginalId in pendingReturn.ItemIdsMappedToOriginalIds)
                                {
                                    var split = itemIdMappedToOriginalId.Split('_');
                                    var itemId = Convert.ToUInt64(split[0]);
                                    var originalId = Convert.ToUInt64(split[1]);
                                    itemIdToOriginalId.Add(itemId, originalId);
                                }
                                var originalItemIds = new List<ulong>();
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
                                if (originalItemIds.Count == pendingReturn.ItemIdsMappedToOriginalIds.Count)
                                {
                                    var itemIds = new List<ulong>();
                                    var userInventory = CSGOInventory.FetchInventory(userId, Bot.apiKey);
                                    foreach (var inventoryItem in userInventory.Items)
                                    {
                                        if (originalItemIds.Contains(inventoryItem.OriginalId) && !itemIds.Contains(inventoryItem.Id))
                                            itemIds.Add(inventoryItem.Id);
                                    }
                                    if (itemIds.Count == originalItemIds.Count)
                                    {
                                        Bot.log.Warn("Trade offer's state says the items are unavailable but I found all of them in the user's inventory, so it will be treated as accepted.");
                                        Bot.log.Success("Listing #" + pendingReturn.ListingId + "'s trade offer has been accepted!");
                                        PostReturnItemComplete(pendingReturn.ListingId);
                                        RemovePendingReturn(pendingReturn.ListingId);
                                    }
                                    else
                                    {
                                        Bot.log.Error(string.Format("I was unable to find all the items for the trade offer in the user's inventory (found {0}, expecting {1}). Treating this listing as declined.", itemIds.Count, originalItemIds.Count));
                                        Bot.log.Warn("Return #" + pendingReturn.ListingId + "'s trade offer was declined!");
                                        // resend item
                                        var tradeToken = PostGetTradeToken(userId);
                                        var resendTradeOffer = TradeOffers.CreateTrade(userId);
                                        if (tradeOffer.ItemsToGive != null)
                                        {
                                            foreach (var inventoryItem in tradeOffer.ItemsToGive)
                                            {
                                                resendTradeOffer.AddMyItem(730, 2, inventoryItem.AssetId);
                                            }
                                        }
                                        var tradeCode = Bot.GenerateTradeCode();
                                        try
                                        {
                                            var tradeId = resendTradeOffer.SendTradeWithToken("Refund Request #" + pendingReturn.ListingId + " | Security Code: " + tradeCode, tradeToken);
                                            if (tradeId > 0)
                                            {
                                                Bot.log.Success("Succesfully resent trade offer. TradeOfferID: " + tradeId);
                                                var tradeUrl = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
                                                RemovePendingReturn(pendingReturn.ListingId);
                                                PostReturnItem(tradeUrl, pendingReturn.ListingId, tradeCode);
                                                AddPendingReturn(pendingReturn.ListingId, tradeId, pendingReturn.ItemIdsMappedToOriginalIds);
                                            }
                                            else
                                            {
                                                // handle this
                                                Bot.log.Error("Failed to send trade offer!");
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
                                    Bot.log.Error(string.Format("I was unable to find the original item IDs for all the trade offer items in the user's inventory (found {0}, expecting {1}). Treating this return as declined.", originalItemIds.Count, pendingReturn.ItemIdsMappedToOriginalIds.Count));
                                    Bot.log.Warn("Return #" + pendingReturn.ListingId + "'s trade offer was declined!");
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
                                        var tradeId = resendTradeOffer.SendTradeWithToken("Refund Request | Security Code: " + tradeCode, tradeToken);
                                        if (tradeId > 0)
                                        {
                                            Bot.log.Success("Succesfully resent trade offer. TradeOfferID: " + tradeId);
                                            var tradeUrl = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
                                            RemovePendingReturn(pendingReturn.ListingId);
                                            PostReturnItem(tradeUrl, pendingReturn.ListingId, tradeCode);
                                            AddPendingReturn(pendingReturn.ListingId, tradeId, pendingReturn.ItemIdsMappedToOriginalIds);
                                        }
                                        else
                                        {
                                            // handle this
                                            Bot.log.Error("Failed to send trade offer!");
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
                                Bot.log.Warn("Return #" + pendingReturn.ListingId + "'s trade offer was declined!");
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
                                    var tradeId = resendTradeOffer.SendTradeWithToken("Refund Request | Security Code: " + tradeCode, tradeToken);
                                    if (tradeId > 0)
                                    {
                                        Bot.log.Success("Succesfully resent trade offer. TradeOfferID: " + tradeId);
                                        var tradeUrl = "http://steamcommunity.com/tradeoffer/" + tradeId + "/";
                                        RemovePendingReturn(pendingReturn.ListingId);
                                        PostReturnItem(tradeUrl, pendingReturn.ListingId, tradeCode);
                                        AddPendingReturn(pendingReturn.ListingId, tradeId, pendingReturn.ItemIdsMappedToOriginalIds);                                        
                                    }
                                    else
                                    {
                                        // handle this
                                        Bot.log.Error("Failed to send trade offer!");
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
                    #region PendingStorage
                    var pendingStorages = PendingStorage.ToList();
                    foreach (var pendingStorage in pendingStorages)
                    {
                        if (pendingStorage.TradeOfferId == tradeOffer.Id && tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.Accepted)
                        {
                            var id32 = String.Format("STEAM_0:{0}:{1}", tradeOffer.OtherAccountId & 1, tradeOffer.OtherAccountId >> 1);
                            var userId = new SteamID(id32).ConvertToUInt64();
                            Bot.log.Success("Storage of listing #" + pendingStorage.ListingId + " has been accepted by the bot!");
                            PostStoreComplete(pendingStorage.ListingId, userId);
                            completedStorage.Add(pendingStorage);
                        }
                        else if (pendingStorage.TradeOfferId == tradeOffer.Id && tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.InvalidItems)
                        {
                            var id32 = String.Format("STEAM_0:{0}:{1}", tradeOffer.OtherAccountId & 1, tradeOffer.OtherAccountId >> 1);
                            var userId = new SteamID(id32).ConvertToUInt64();
                            var itemIdToOriginalId = new Dictionary<ulong, ulong>();
                            foreach (var itemIdMappedToOriginalId in pendingStorage.ItemIdsMappedToOriginalIds)
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
                            if (originalItemIds.Count == pendingStorage.ItemIdsMappedToOriginalIds.Count)
                            {
                                var itemIds = new List<ulong>();
                                var botInventory = CSGOInventory.FetchInventory(userId, Bot.apiKey);
                                foreach (var inventoryItem in botInventory.Items)
                                {
                                    if (originalItemIds.Contains(inventoryItem.OriginalId) && !itemIds.Contains(inventoryItem.Id))
                                        itemIds.Add(inventoryItem.Id);
                                }
                                if (itemIds.Count == originalItemIds.Count)
                                {
                                    Bot.log.Warn("Trade offer's state says the items are unavailable but I found all of them in the storage bot's inventory, so it will be treated as accepted.");
                                    Bot.log.Success("Storage of listing #" + pendingStorage.ListingId + " has been accepted by the bot!");
                                    PostStoreComplete(pendingStorage.ListingId, userId);
                                    completedStorage.Add(pendingStorage);
                                }
                                else
                                {
                                    Bot.log.Error(string.Format("I was unable to find all the items for the trade offer in the storage bot's inventory (found {0}, expecting {1}). Wat do?", itemIds.Count, originalItemIds.Count));
                                }
                            }
                            else
                            {
                                Bot.log.Error(string.Format("I was unable to find the original item IDs for all the trade offer items in the storage bot's inventory (found {0}, expecting {1}). Wat do?", originalItemIds.Count, pendingStorage.ItemIdsMappedToOriginalIds.Count));
                            }
                        }
                    }
                    #endregion
                    #region PendingCheckins
                    var pendingCheckins = PendingCheckins.ToList();
                    foreach (var pendingCheckin in pendingCheckins)
                    {                        
                        if (pendingCheckin.TradeOfferId == tradeOffer.Id && tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.Accepted)
                        {
                            Bot.log.Success("Checkin for item #" + pendingCheckin.ItemId + " has been completed.");
                            PostCheckin(pendingCheckin.ItemId);
                            RemovePendingCheckin(pendingCheckin.UserId, pendingCheckin.ItemId, pendingCheckin.TradeOfferId);
                            Bot.SteamFriends.SendChatMessage(pendingCheckin.UserId, EChatEntryType.ChatMsg, "Checkin for item #" + pendingCheckin.ItemId + " has been completed.");
                        }
                        else if (pendingCheckin.TradeOfferId == tradeOffer.Id && tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.InvalidItems)
                        {
                            bool found = false;
                            var botInventory = CSGOInventory.FetchInventory(MySID, Bot.apiKey);
                            foreach (var inventoryItem in botInventory.Items)
                            {
                                if (inventoryItem.OriginalId == pendingCheckin.ItemId)
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (found)
                            {
                                Bot.log.Warn("Trade offer's state says the checkin item is unavailable but I found it inventory, so it will be treated as accepted.");
                                Bot.log.Success("Checkin for item #" + pendingCheckin.ItemId + " has been completed.");
                                PostCheckin(pendingCheckin.ItemId);
                                RemovePendingCheckin(pendingCheckin.UserId, pendingCheckin.ItemId, pendingCheckin.TradeOfferId);
                                Bot.SteamFriends.SendChatMessage(pendingCheckin.UserId, EChatEntryType.ChatMsg, "Checkin for item #" + pendingCheckin.ItemId + " has been completed.");
                            }
                            else
                            {
                                Bot.log.Error("Trade offer's state says the checkin item is unavailable and I was unable to find the checkin item #" + pendingCheckin.ItemId + " for the trade offer in the my inventory.");
                            }
                        }
                        else if (pendingCheckin.TradeOfferId == tradeOffer.Id && tradeOffer.State != SteamTrade.TradeOffers.TradeOfferState.Active)
                        {
                            RemovePendingCheckin(pendingCheckin.UserId, pendingCheckin.ItemId, pendingCheckin.TradeOfferId);
                        }
                    }
                    #endregion
                    #region PendingCheckouts
                    var pendingCheckouts = PendingCheckouts.ToList();
                    foreach (var pendingCheckout in pendingCheckouts)
                    {
                        if (pendingCheckout.TradeOfferId == tradeOffer.Id && tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.Accepted)
                        {
                            Bot.log.Success("Checkout for item #" + pendingCheckout.ItemId + " has been completed.");
                            PostCheckout(pendingCheckout.ItemId, pendingCheckout.UserId);
                            RemovePendingCheckout(pendingCheckout.UserId, pendingCheckout.ItemId, pendingCheckout.TradeOfferId);
                            Bot.SteamFriends.SendChatMessage(pendingCheckout.UserId, EChatEntryType.ChatMsg, "Checkout for item #" + pendingCheckout.ItemId + " has been completed.");
                        }
                        else if (pendingCheckout.TradeOfferId == tradeOffer.Id && tradeOffer.State == SteamTrade.TradeOffers.TradeOfferState.InvalidItems)
                        {
                            bool found = false;
                            var userInventory = CSGOInventory.FetchInventory(pendingCheckout.UserId, Bot.apiKey);
                            foreach (var inventoryItem in userInventory.Items)
                            {
                                if (inventoryItem.OriginalId == pendingCheckout.ItemId)
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (found)
                            {
                                Bot.log.Warn("Trade offer's state says the checkout item is unavailable but I found it inventory, so it will be treated as accepted.");
                                Bot.log.Success("Checkout for item #" + pendingCheckout.ItemId + " has been completed.");
                                PostCheckout(pendingCheckout.ItemId, pendingCheckout.UserId);
                                RemovePendingCheckout(pendingCheckout.UserId, pendingCheckout.ItemId, pendingCheckout.TradeOfferId);
                                Bot.SteamFriends.SendChatMessage(pendingCheckout.UserId, EChatEntryType.ChatMsg, "Checkout for item #" + pendingCheckout.ItemId + " has been completed.");
                            }
                            else
                            {
                                Bot.log.Error("Trade offer's state says the checkout item is unavailable and I was unable to find the checkout item #" + pendingCheckout.ItemId + " for the trade offer in the my inventory.");
                            }
                        }
                        else if (pendingCheckout.TradeOfferId == tradeOffer.Id && tradeOffer.State != SteamTrade.TradeOffers.TradeOfferState.Active)
                        {
                            RemovePendingCheckout(pendingCheckout.UserId, pendingCheckout.ItemId, pendingCheckout.TradeOfferId);
                        }
                    }
                    #endregion
                }
                foreach (var item in completedListings)
                {
                    RemovePendingListing(item.ListingId);
                }
                foreach (var item in completedStorage)
                {
                    RemovePendingStorageItem(item.ListingId);
                }
                ongoingAction = false;
                isCheckingTradeOffers = false;
            }
        }
    }
}