using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InlineKeyboardButtons;
using Telegram.Bot.Types.ReplyMarkups;

namespace WhoAmIBotSpace.Helpers
{
    public static class ReplyMarkupMaker
    {
        public static IReplyMarkup InlineYesNo(string yes, string yesCallback, string no, string noCallback)
        {
            InlineKeyboardButton yesButton = new InlineKeyboardCallbackButton(yes, yesCallback);
            InlineKeyboardButton noButton = new InlineKeyboardCallbackButton(no, noCallback);
            InlineKeyboardButton[] row = new InlineKeyboardButton[] { yesButton, noButton };
            return new InlineKeyboardMarkup(new InlineKeyboardButton[][] { row });
        }

        public static IReplyMarkup InlineYesNoIdk(string yes, string yesCallback, string no, string noCallback, 
            string idk, string idkCallback)
        {
            InlineKeyboardButton yesButton = new InlineKeyboardCallbackButton(yes, yesCallback);
            InlineKeyboardButton idkButton = new InlineKeyboardCallbackButton(idk, idkCallback);
            InlineKeyboardButton noButton = new InlineKeyboardCallbackButton(no, noCallback);
            InlineKeyboardButton[] row = new InlineKeyboardButton[] { yesButton, noButton };
            InlineKeyboardButton[] row2 = new InlineKeyboardButton[] { idkButton };
            return new InlineKeyboardMarkup(new InlineKeyboardButton[][] { row, row2 });
        }

        public static IReplyMarkup InlineGuessGiveUp(string guess, string guessCallback, string giveUp, string giveUpCallback)
        {
            InlineKeyboardButton guessButton = new InlineKeyboardCallbackButton(guess, guessCallback);
            InlineKeyboardButton giveUpButton = new InlineKeyboardCallbackButton(giveUp, giveUpCallback);
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
                l.Add(new InlineKeyboardCallbackButton(name, $"lang:{key}@{chatId}"));
                bGrid.Add(l);
            }
            var aGrid = new List<InlineKeyboardButton[]>();
            for (int i = 0; i < bGrid.Count; i++)
            {
                if (i%2 == 0)
                {
                    if (i < bGrid.Count - 1)
                    {
                        InlineKeyboardButton[] aRow = new InlineKeyboardButton[2];
                        aRow[0] = bGrid[i][0];
                        aGrid.Add(aRow);
                    }
                    else
                    {
                        aGrid.Add(bGrid[i].ToArray());
                    }
                }
                else
                {
                    aGrid[i / 2][1] = bGrid[i][0];
                }
            }
            return new InlineKeyboardMarkup(aGrid.ToArray());
        }

        public static IReplyMarkup InlineStartMe(string username)
        {
            InlineKeyboardButton b = InlineKeyboardButton.WithUrl("Start", $"http://t.me/{username}");
            return new InlineKeyboardMarkup(new InlineKeyboardButton[] { b });
        }
    }
}
