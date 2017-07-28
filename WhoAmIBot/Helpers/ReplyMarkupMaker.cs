using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace WhoAmIBotSpace.Helpers
{
    public static class ReplyMarkupMaker
    {
        public static IReplyMarkup InlineYesNo(string yes, string yesCallback, string no, string noCallback)
        {
            InlineKeyboardButton yesButton = new InlineKeyboardButton(yes, yesCallback);
            InlineKeyboardButton noButton = new InlineKeyboardButton(no, noCallback);
            InlineKeyboardButton[] row = new InlineKeyboardButton[] { yesButton, noButton };
            return new InlineKeyboardMarkup(new InlineKeyboardButton[][] { row });
        }

        public static IReplyMarkup InlineYesNoIdk(string yes, string yesCallback, string no, string noCallback, 
            string idk, string idkCallback)
        {
            InlineKeyboardButton yesButton = new InlineKeyboardButton(yes, yesCallback);
            InlineKeyboardButton idkButton = new InlineKeyboardButton(idk, idkCallback);
            InlineKeyboardButton noButton = new InlineKeyboardButton(no, noCallback);
            InlineKeyboardButton[] row = new InlineKeyboardButton[] { yesButton, idkButton, noButton };
            return new InlineKeyboardMarkup(new InlineKeyboardButton[][] { row });
        }

        public static IReplyMarkup InlineGuessGiveUp(string guess, string guessCallback, string giveUp, string giveUpCallback)
        {
            InlineKeyboardButton guessButton = new InlineKeyboardButton(guess, guessCallback);
            InlineKeyboardButton giveUpButton = new InlineKeyboardButton(giveUp, giveUpCallback);
            InlineKeyboardButton[] row = new InlineKeyboardButton[] { guessButton, giveUpButton };
            return new InlineKeyboardMarkup(new InlineKeyboardButton[][] { row });
        }

        public static IReplyMarkup InlineChooseLanguage(List<List<string>> grid, long chatId)
        {
            List<List<InlineKeyboardButton>> bGrid = new List<List<InlineKeyboardButton>>();
            foreach (var row in grid)
            {
                var l = new List<InlineKeyboardButton>();
                string key = row[0];
                string name = row[1];
                l.Add(new InlineKeyboardButton(name, $"lang:{key}@{chatId}"));
                bGrid.Add(l);
            }
            var aGrid = new List<InlineKeyboardButton[]>();
            foreach (var bRow in bGrid)
            {
                aGrid.Add(bRow.ToArray());
            }
            return new InlineKeyboardMarkup(aGrid.ToArray());
        }
    }
}
