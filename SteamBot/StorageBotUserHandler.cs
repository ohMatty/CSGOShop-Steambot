using System;
using SteamKit2;
using System.Collections.Generic;
using SteamTrade;
using Newtonsoft.Json;

namespace SteamBot
{
    public class StorageUserHandler : UserHandler
    {
        TradeOffers TradeOffers;

        public StorageUserHandler(Bot bot, SteamID sid)
            : base(bot, sid)
        {
            TradeOffers = new TradeOffers(MySID, bot.apiKey, bot.sessionId, bot.token);
        }        

        public override void OnLoginCompleted()
        {
            AddInventoriesToFetch(GenericInventory.InventoryTypes.CSGO);
            new System.Threading.Thread(CheckTradeOffersThread).Start();
        }

        void CheckTradeOffersThread()
        {
            while (true)
            {
                CheckTradeOffers();
                System.Threading.Thread.Sleep(2000);
            }
        }        

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

        public override void OnTradeAccept() { }
        #endregion

        void CheckTradeOffers()
        {
            foreach (var tradeID in TradeOffers.GetIncomingTradeOffers())
            {
                Bot.log.Info("Retrieving information for trade offer " + tradeID + "...");
                var trade = TradeOffers.GetTradeOffer(tradeID);
                if (trade != null && Bot.BotSteamIds.Contains(trade.them.steamId))
                {
                    Bot.log.Info("This is a trade offer from a CSGOShop Bot.");
                    // trade offer sent by trade bot
                    if (TradeOffers.AcceptTrade(trade))
                    {
                        Bot.log.Success("Successfully accepted trade offer!");
                    }
                    else
                    {
                        Bot.log.Error("Failed to accept trade offer; trying again next check.");
                    }
                }
                else
                {
                    Bot.log.Warn("This is not from a CSGOShop bot.");
                }
            }
        }
    }
}