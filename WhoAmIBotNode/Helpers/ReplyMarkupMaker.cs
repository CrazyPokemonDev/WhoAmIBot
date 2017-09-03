using System.Collections.Generic;
using System.Data.SQLite;
using Telegram.Bot.Types.InlineKeyboardButtons;
using Telegram.Bot.Types.ReplyMarkups;
using WhoAmIBotSpace.Classes;

namespace WhoAmIBotSpace.Helpers
{
    public static class ReplyMarkupMaker
    {
        #region Yes No (Idk)
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
        #endregion
        #region Guess, give up
        public static IReplyMarkup InlineGuessGiveUp(string guess, string guessCallback, string giveUp, string giveUpCallback)
        {
            InlineKeyboardButton guessButton = new InlineKeyboardCallbackButton(guess, guessCallback);
            InlineKeyboardButton giveUpButton = new InlineKeyboardCallbackButton(giveUp, giveUpCallback);
            InlineKeyboardButton[] row = new InlineKeyboardButton[] { guessButton, giveUpButton };
            return new InlineKeyboardMarkup(new InlineKeyboardButton[][] { row });
        }
        #endregion
        #region Choose language
        public static IReplyMarkup InlineChooseLanguage(SQLiteCommand cmd, long chatId)
        {
            List<List<InlineKeyboardButton>> bGrid = new List<List<InlineKeyboardButton>>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var l = new List<InlineKeyboardButton>
                    {
                        new InlineKeyboardCallbackButton((string)reader["name"], $"lang:{reader["key"]}@{chatId}")
                    };
                    bGrid.Add(l);
                }
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
        #endregion
        #region Start me
        public static IReplyMarkup InlineStartMe(string username)
        {
            InlineKeyboardButton b = InlineKeyboardButton.WithUrl("Start", $"http://t.me/{username}");
            return new InlineKeyboardMarkup(new InlineKeyboardButton[] { b });
        }
        #endregion
        #region Get Games
        public static IReplyMarkup InlineGetGames(List<NodeGame> games, long chatid)
        {
            var rows = new List<InlineKeyboardButton[]>();
            foreach (var g in games)
            {
                rows.Add(new InlineKeyboardButton[] 
                {
                    new InlineKeyboardCallbackButton(g.GroupId.ToString(), "null"),
                    new InlineKeyboardCallbackButton("Cancel", $"cancel:{g.GroupId}@{chatid}"),
                    new InlineKeyboardCallbackButton("Communicate", $"communicate:{g.GroupId}@{chatid}")
                });
            }
            rows.Add(new InlineKeyboardButton[] { new InlineKeyboardCallbackButton("Close", $"close@{chatid}") });
            return new InlineKeyboardMarkup(rows.ToArray());
        }
        #endregion
        #region Settings
        public static IReplyMarkup InlineSettings(long groupId, string joinTimeout,
            string gameTimeout, string cancelgameAdmin, string autoEnd, string close)
        {
            var row1 = new InlineKeyboardButton[2];
            row1[0] = new InlineKeyboardCallbackButton(joinTimeout, $"joinTimeout@{groupId}");
            row1[1] = new InlineKeyboardCallbackButton(gameTimeout, $"gameTimeout@{groupId}");
            var row2 = new InlineKeyboardButton[2];
            row2[0] = new InlineKeyboardCallbackButton(cancelgameAdmin, $"cancelgameAdmin@{groupId}");
            row2[1] = new InlineKeyboardCallbackButton(autoEnd, $"autoEnd@{groupId}");
            var row3 = new InlineKeyboardButton[1];
            row3[0] = new InlineKeyboardCallbackButton(close, $"closesettings@{groupId}");
            return new InlineKeyboardMarkup(new InlineKeyboardButton[][] { row1, row2, row3 });
        }
        #endregion
        #region Cancel Nextgame
        public static IReplyMarkup InlineCancelNextgame(string cancel, long groupid)
        {
            InlineKeyboardButton b = new InlineKeyboardCallbackButton(cancel, $"cancelnextgame@{groupid}");
            InlineKeyboardButton[] row = { b };
            return new InlineKeyboardMarkup(row);
        }
        #endregion
    }
}
