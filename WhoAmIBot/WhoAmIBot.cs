using FlomBotFactory;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using Telegram.Bot.Types;
using UpdateEventArgs = Telegram.Bot.Args.UpdateEventArgs;
using File = System.IO.File;
using Telegram.Bot.Types.Enums;
using WhoAmIBotSpace.Classes;
using WhoAmIBotSpace.Helpers;
using Telegram.Bot.Args;
using Telegram.Bot.Types.ReplyMarkups;
using System.Linq;
using Newtonsoft.Json;

namespace WhoAmIBotSpace
{
    public class WhoAmIBot : FlomBot
    {
        #region Properties
        public override string Name => "Who am I bot";
        public string Username { get; set; }
        #endregion
        #region Constants
        private const string baseFilePath = "C:\\Olfi01\\WhoAmIBot\\";
        private const string sqliteFilePath = baseFilePath + "db.sqlite";
        private const string connectionString = "Data Source=\"" + sqliteFilePath + "\";";
        private const string defaultLangCode = "en-US";
        private const long testingGroupId = -1001070844778;
        private static readonly List<string> gameQueries = new List<string>()
        {
            "yes@",
            "idk@",
            "no@",
            "guess@",
            "giveup@"
        };
        protected new static readonly Flom Flom = new Flom();
        #endregion
        #region Fields
        private SQLiteConnection sqliteConn;
        private List<Node> Nodes = new List<Node>();
        #endregion

        #region Constructors and FlomBot stuff
        public WhoAmIBot(string token) : base(token)
        {
            if (!Directory.Exists(baseFilePath)) Directory.CreateDirectory(baseFilePath);
            if (!File.Exists(sqliteFilePath)) SQLiteConnection.CreateFile(sqliteFilePath);
            InitSqliteConn();
        }

        private void InitSqliteConn()
        {
            sqliteConn = new SQLiteConnection(connectionString);
            sqliteConn.Open();
        }

        public override bool StartBot()
        {
            try
            {
                var task = client.GetMeAsync();
                task.Wait();
                Username = task.Result.Username;
                client.OnReceiveError += Client_OnReceiveError;
                client.OnReceiveGeneralError += Client_OnReceiveError;
                client.OnCallbackQuery += Client_OnCallbackQuery;
            }
            catch
            {
                return false;
            }
            return base.StartBot();
        }

        public override bool StopBot()
        {
            foreach (var node in Nodes)
            {
                node.Stop();
            }
            client.OnReceiveError -= Client_OnReceiveError;
            client.OnReceiveGeneralError -= Client_OnReceiveError;
            client.OnCallbackQuery -= Client_OnCallbackQuery;
            return base.StopBot();
        }
        #endregion
        #region On Update
        protected override void Client_OnUpdate(object sender, UpdateEventArgs e)
        {
            if (e.Update.Type == UpdateType.CallbackQueryUpdate)
            {
                foreach (var node in Nodes)
                {
                    using (var sw = new StreamWriter(node.Pipe))
                    {
                        sw.WriteLine(JsonConvert.SerializeObject(e.Update));
                        sw.Flush();
                    }
                }
                return;
            }
            if (e.Update.Type == UpdateType.MessageUpdate && e.Update.Message.Type == MessageType.TextMessage)
            {

            }
            if (e.Update.Type == UpdateType.MessageUpdate && e.Update.Message.Type == MessageType.TextMessage
            && e.Update.Message.Text == "I hereby grant you permission." && e.Update.Message.From.Id == Flom)
            {
                using (var db = new WhoAmIBotContext())
                {
                    var msg = e.Update.Message;
                    if (msg.ReplyToMessage == null) return;
                    db.GlobalAdmins.Add(new GlobalAdmin() { Id = msg.ReplyToMessage.From.Id });
                    db.SaveChanges();
                    SendLangMessage(msg.Chat.Id, Strings.PowerGranted);
                }
            }
        }
        #endregion
        #region On callback query
        private void Client_OnCallbackQuery(object sender, CallbackQueryEventArgs e)
        {
            if (!e.CallbackQuery.Data.Contains("@")
                || !long.TryParse(e.CallbackQuery.Data.Substring(e.CallbackQuery.Data.IndexOf("@") + 1), out long id)
                || e.CallbackQuery.Message == null) return;
            using (var db = new WhoAmIBotContext())
            {
                if (db.Games.Any(x => x.Id == id)) return;
                if (!gameQueries.Exists(x => e.CallbackQuery.Data.StartsWith(x))) return;
                else
                {
                    Message cmsg = e.CallbackQuery.Message;
                    var query = ExecuteSql("SELECT seq FROM sqlite_sequence WHERE name = 'Games'");
                    long last = 0;
                    if (query.Count < 1) last = 0;
                    else
                    {
                        if (query[0].Count < 1) last = 0;
                        else if (!long.TryParse(query[0][0], out last)) last = 0;
                    }
                    if (id < last + 1)
                    {
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, GetString(Strings.GameNoLongerRunning, e.CallbackQuery.From.Id));
                        client.EditMessageReplyMarkupAsync(cmsg.Chat.Id, cmsg.MessageId, null);
                    }
                }
            }
        }
        #endregion

        #region Language
        #region Get string
        private string GetString(string key, string langCode)
        {
            var par = new Dictionary<string, object>() { { "key", key } };
            var q = ExecuteSql($"SELECT value FROM '{langCode}' WHERE key=@key", par);
            string query = "";
            if (q.Count < 1 || q[0].Count < 1 || q[0][0].StartsWith("SQL logic error or missing database"))
            {
                q = ExecuteSql($"SELECT value FROM '{defaultLangCode}' WHERE key=@key", par);
                if (q.Count < 1 || q[0].Count < 1)
                {
                    query = $"String {key} missing. Inform @Olfi01.";
                }
                else
                {
                    query = q[0][0];
                }
            }
            else
            {
                query = q[0][0];
            }
            return query;
        }

        private string GetString(string key, long langFrom)
        {
            return GetString(key, LangCode(langFrom));
        }
        #endregion
        #region Lang code
        private string LangCode(long id)
        {
            using (var db = new WhoAmIBotContext())
            {
                string key = "";
                if (db.Groups.Any(x => x.Id == id)) key = db.Groups.Find(id).LangKey;
                else if (db.Users.Any(x => x.Id == id)) key = db.Users.Find(id).LangKey;
                else key = defaultLangCode;
                var l = db.ExistingLanguages.Find(key);
                if (l != null) return l.Key;
                else
                {
                    var par = new Dictionary<string, object>() { { "id", id } };
                    var q = ExecuteSql("SELECT key FROM existinglanguages WHERE key LIKE " +
                        "(SUBSTR((SELECT langkey FROM users where id=@p0), 0, 3)||'-%')", par);
                    if (q.Count > 0 && q[0].Count > 0) return q[0][0];
                    else return defaultLangCode;
                }
            }
        }
        #endregion
        #region Send Lang Message
        private bool SendLangMessage(long chatid, string key, IReplyMarkup markup = null)
        {
            try
            {
                var task = client.SendTextMessageAsync(chatid, GetString(key, LangCode(chatid)),
                    replyMarkup: markup, parseMode: ParseMode.Html);
                task.Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion
        #endregion

        #region Error handling
        private void Client_OnReceiveError(object sender, EventArgs ea)
        {
            if (ea is ReceiveErrorEventArgs e)
            {
                var ex = e.ApiRequestException;
                client.SendTextMessageAsync(Flom, $"WhoAmIBot: {ex.Message}\n{ex.StackTrace}");
                if (ex.InnerException != null)
                    client.SendTextMessageAsync(Flom, $"WhoAmIBot (inner exception): {ex.InnerException.Message}\n{ex.InnerException.StackTrace}");
            }
            else if (ea is ReceiveGeneralErrorEventArgs ge)
            {
                var ex = ge.Exception;
                client.SendTextMessageAsync(Flom, $"WhoAmIBot: {ex.Message}\n{ex.StackTrace}");
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    client.SendTextMessageAsync(Flom, $"WhoAmIBot (inner exception): {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
        #endregion

        #region SQLite
        #region Execute SQLite Query
        private List<List<string>> ExecuteSql(string commandText, Dictionary<string, object> parameters = null)
        {
            List<List<string>> r = new List<List<string>>();
            using (var trans = sqliteConn.BeginTransaction())
            {
                using (var comm = new SQLiteCommand(commandText, sqliteConn, trans))
                {
                    if (parameters != null)
                    {
                        foreach (var kvp in parameters)
                        {
                            comm.Parameters.Add(new SQLiteParameter(kvp.Key, kvp.Value));
                        }
                    }
                    else if (commandText.Contains("@")) client.SendTextMessageAsync(Flom, $"Missing parameters in {commandText}");
                    try
                    {
                        using (var reader = comm.ExecuteReader())
                        {
                            /*if (!raw)
                            {
                                if (reader.RecordsAffected >= 0) r += $"_{reader.RecordsAffected} records affected_\n";
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    r += $"{reader.GetName(i)} ({reader.GetFieldType(i).Name})";
                                    if (i < reader.FieldCount - 1) r += " | ";
                                }
                                r += "\n\n";
                            }*/
                            while (reader.HasRows)
                            {
                                var row = new List<string>();
                                if (!reader.Read()) break;
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row.Add(reader.GetValue(i).ToString());
                                }
                                r.Add(row);
                            }
                        }
                    }
                    catch (SQLiteException x)
                    {
                        r = new List<List<string>>() { new List<string>() { x.Message } };
                    }
                }
                trans.Commit();
            }

            return r;
        }

        private string ExecuteSqlRaw(string commandText)
        {
            string r = "";
            using (var trans = sqliteConn.BeginTransaction())
            {
                using (var comm = new SQLiteCommand(commandText, sqliteConn, trans))
                {
                    try
                    {
                        using (var reader = comm.ExecuteReader())
                        {
                            if (reader.RecordsAffected >= 0) r += $"<i>{reader.RecordsAffected} records affected</i>\n";
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                r += $"{reader.GetName(i)} ({reader.GetFieldType(i).Name})";
                                if (i < reader.FieldCount - 1) r += " | ";
                            }
                            r += "\n\n";
                            while (reader.HasRows)
                            {
                                if (!reader.Read()) break;
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    r += reader.GetValue(i);
                                    if (i < reader.FieldCount - 1) r += " | ";
                                }
                                r += "\n";
                            }
                        }
                    }
                    catch (SQLiteException x)
                    {
                        r = x.Message;
                    }
                }
                trans.Commit();
            }

            return r;
        }
        #endregion
        #endregion
    }
}
