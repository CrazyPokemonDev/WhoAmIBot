using System.Collections.Generic;
using System.Data.SQLite;
using Telegram.Bot.Types.ReplyMarkups;
using WhoAmIBotSpace.Classes;

namespace WhoAmIBotSpace.Helpers
{

    public class ReplyMarkupMaker
    {
        public List<List<InlineKeyboardButton>> Rows { get; } = new List<List<InlineKeyboardButton>>();

        public ReplyMarkupMaker AddRow()
        {
            var newRow = new List<InlineKeyboardButton>();
            Rows.Add(newRow);
            return this;
        }

        public ReplyMarkupMaker AddCallbackButton(string text, string callback)
        {
            var last = Rows[Rows.Count - 1];
            last.Add(new InlineKeyboardButton { Text = text, CallbackData = callback });
            return this;
        }

        public InlineKeyboardMarkup Finish()
        {
            return new InlineKeyboardMarkup(Rows);
        }
    }



    public static class MyReplyMarkupMaker
    {
        #region Yes No (Idk)
        public static InlineKeyboardMarkup InlineYesNo(string yes, string yesCallback, string no, string noCallback)
        {
            return new ReplyMarkupMaker()
                .AddRow().AddCallbackButton(yes, yesCallback).AddCallbackButton(no, noCallback).Finish();
        }

        public static InlineKeyboardMarkup InlineYesNoIdk(string yes, string yesCallback, string no, string noCallback, 
            string idk, string idkCallback)
        {
            return new ReplyMarkupMaker()
                .AddRow().AddCallbackButton(yes, yesCallback).AddCallbackButton(no, noCallback)
                .AddRow().AddCallbackButton(idk, idkCallback).Finish();
        }
        #endregion
        #region Guess, give up
        public static InlineKeyboardMarkup InlineGuessGiveUp(string guess, string guessCallback, string giveUp, string giveUpCallback)
        {
            return new ReplyMarkupMaker()
                .AddRow().AddCallbackButton(guess, guessCallback).AddCallbackButton(giveUp, giveUpCallback).Finish();
        }
        #endregion
        #region Choose language
        public static InlineKeyboardMarkup InlineChooseLanguage(SQLiteCommand cmd, long chatId)
        {
            List<List<InlineKeyboardButton>> bGrid = new List<List<InlineKeyboardButton>>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var l = new List<InlineKeyboardButton>
                    {
                        new InlineKeyboardButton() { Text = (string)reader["name"], CallbackData = $"lang:{reader["key"]}@{chatId}" }
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
        public static InlineKeyboardMarkup InlineStartMe(string username)
        {
            InlineKeyboardButton b = new InlineKeyboardButton() { Text = "Start", Url = $"http://t.me/{username}" };
            return new InlineKeyboardMarkup(new InlineKeyboardButton[] { b });
        }
        #endregion
        #region Get Games
        public static InlineKeyboardMarkup InlineGetGames(List<NodeGame> games, long chatid)
        {
            ReplyMarkupMaker maker = new ReplyMarkupMaker();
            foreach (var g in games)
            {
                maker.AddRow().AddCallbackButton(g.GroupId.ToString(), "null").AddCallbackButton("Cancel", $"cancel:{g.GroupId}@{chatid}")
                    .AddCallbackButton("Communicate", $"communicate:{g.GroupId}@{chatid}");
            }
            return (InlineKeyboardMarkup)maker.AddRow().AddCallbackButton("Close", $"close@{chatid}").Finish();
        }
        #endregion
        #region Settings
        public static InlineKeyboardMarkup InlineSettings(long groupId, string joinTimeout,
            string gameTimeout, string cancelgameAdmin, string autoEnd, string close)
        {
            return new ReplyMarkupMaker()
                .AddRow().AddCallbackButton(joinTimeout, $"joinTimeout@{groupId}").AddCallbackButton(gameTimeout, $"gameTimeout@{groupId}")
                .AddRow().AddCallbackButton(cancelgameAdmin, $"cancelgameAdmin@{groupId}").AddCallbackButton(autoEnd, $"autoEnd@{groupId}")
                .AddRow().AddCallbackButton(close, $"closesettings@{groupId}").Finish();
        }
        #endregion
        #region Cancel Nextgame
        public static InlineKeyboardMarkup InlineCancelNextgame(string cancel, long groupid)
        {
            InlineKeyboardButton b = new InlineKeyboardButton() { Text = cancel, CallbackData = $"cancelnextgame@{groupid}" };
            InlineKeyboardButton[] row = { b };
            return new InlineKeyboardMarkup(row);
        }
        #endregion
    }
}
