using SteamKit2;
using System;
using System.Collections.Generic;
using SteamTrade;

namespace SteamBot
{
    public class SimpleUserHandler : UserHandler
    {
        TradeOffers TradeOffers;

        public SimpleUserHandler(Bot bot, SteamID sid)
            : base(bot, sid)
        {
            TradeOffers = new TradeOffers(MySID, bot.apiKey, bot.sessionId, bot.token);
        }

        public override void OnLoginCompleted()
        {
            AddInventoriesToFetch(440, 2);
            var userInventory = CSGOInventory.FetchInventory(76561198137207530, Bot.apiKey);
        }

        public override bool OnGroupAdd()
        {
            return false;
        }

        public override bool OnFriendAdd()
        {
            return true;
        }

        public override void OnPusherConnected()
        {
            
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
                if (message == "test")
                {
                    var rand = new Random();
                    var inventory = MyInventory.GetInventory(440, 2);
                    var randomItem = inventory[rand.Next(inventory.Count)];
                    var tradeOffer = TradeOffers.CreateTrade(OtherSID);
                    tradeOffer.AddMyItem(randomItem.AppId, randomItem.ContextId, randomItem.Id);
                    tradeOffer.SendTrade("test");
                }
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
    }
}