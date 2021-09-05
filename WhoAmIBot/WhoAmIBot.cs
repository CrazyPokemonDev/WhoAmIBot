using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using File = System.IO.File;
using UpdateEventArgs = Telegram.Bot.Args.UpdateEventArgs;
using WhoAmIBotSpace.Classes;
using WhoAmIBotSpace.Helpers;
using System.Linq;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace WhoAmIBotSpace
{
    public class WhoAmIBot
    {
        // Telegram.Bot is deprecating long polling but idc for now
#pragma warning disable CS0618 // Type or member is obsolete

        #region Properties
        public string Name => "Who am I bot";
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
        protected static readonly Flom Flom = new Flom();
        private static readonly string releaseDirectory = Path.Combine(baseFilePath, "Release\\");
        private static readonly string nodeReleaseDirectory = Path.Combine(releaseDirectory, "Node\\");
        private const long tracingChannelId = -1001383830638;
        #endregion
        #region Fields
        private TelegramBotClient client;
        private string Token { get; }
        private SQLiteConnection sqliteConn;
        private List<Node> Nodes = new List<Node>();
        private bool updating = false;
        #endregion

        #region Helpers
        private bool GlobalAdminExists(long id)
        {
            var par = new Dictionary<string, object>()
            {
                { "id", id }
            };
            var q = ExecuteSql("SELECT Id FROM GlobalAdmins WHERE Id=@id", par);
            if (q.Count < 1 || q[0].Count < 1 || q[0][0] != id.ToString()) return false;
            else return true;
        }

        private bool StandaloneCommandExists(string trigger)
        {
            var par = new Dictionary<string, object>()
            {
                { "trigger", trigger }
            };
            var q = ExecuteSql("SELECT Standalone FROM Commands WHERE trigger=@trigger", par);
            if (q.Count < 1 || q[0].Count < 1 || !bool.TryParse(q[0][0], out bool result)) return false;
            else return result;
        }

        private bool GameExists(long gameid)
        {
            var par = new Dictionary<string, object>()
            {
                { "id", gameid }
            };
            var q = ExecuteSql("SELECT Id FROM Games WHERE Id=@id", par);
            if (q.Count < 1 || q[0].Count < 1 || q[0][0] != gameid.ToString()) return false;
            else return true;
        }

        private bool GroupExists(long groupid)
        {
            var par = new Dictionary<string, object>()
            {
                { "id", groupid }
            };
            var q = ExecuteSql("SELECT id FROM Groups WHERE Id=@id", par);
            if (q.Count < 1 || q[0].Count < 1 || q[0][0] != groupid.ToString()) return false;
            else return true;
        }

        private bool UserExists(long userid)
        {
            var par = new Dictionary<string, object>()
            {
                { "id", userid }
            };
            var q = ExecuteSql("SELECT id FROM Users WHERE Id=@id", par);
            if (q.Count < 1 || q[0].Count < 1 || q[0][0] != userid.ToString()) return false;
            else return true;
        }

        private bool LangKeyExists(string key)
        {
            var par = new Dictionary<string, object>()
            {
                { "key", key }
            };
            var q = ExecuteSql("SELECT Key FROM ExistingLanguages WHERE Key=@key", par);
            if (q.Count < 1 || q[0].Count < 1 || q[0][0] != key) return false;
            else return true;
        }

        private string GetValue(string table, string column, object identifier, string identifierName = "Id")
        {
            var par = new Dictionary<string, object>()
            {
                { "id", identifier }
            };
            string query = $"SELECT {column} FROM {table} WHERE {identifierName}=@id";
            var q = ExecuteSql(query, par);
            if (q.Count < 1 || q[0].Count < 1) throw new Exception($"Helper did not find anything. Query: {query}");
            else return q[0][0];
        }

        private string GetGroupValue(string column, long id)
        {
            return GetValue("Groups", column, id, "Id");
        }

        private string GetUserValue(string column, long id)
        {
            return GetValue("Users", column, id, "Id");
        }
        #endregion
        #region Constructors and FlomBot stuff
        public WhoAmIBot(string token)
        {
            Token = token;
            client = new TelegramBotClient(Token);
            if (!Directory.Exists(baseFilePath)) Directory.CreateDirectory(baseFilePath);
            if (!File.Exists(sqliteFilePath)) SQLiteConnection.CreateFile(sqliteFilePath);
            InitSqliteConn();
            JsonConvert.DefaultSettings = () => { return new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore }; };
        }

        private void InitSqliteConn()
        {
            sqliteConn = new SQLiteConnection(connectionString);
            sqliteConn.Open();
            ExecuteSql("DELETE FROM Games");
        }

        public void StartBot()
        {
            var task = client.GetMeAsync();
            task.Wait();
            Username = task.Result.Username;
            int offset = 0;
            Update[] updates;
            for (int i = 0; i < 10; i++)
            {
                updates = client.GetUpdatesAsync(offset: offset).Result;
                if (updates.Length < 1) break;
                offset = updates?.OrderBy(x => x.Id)?.Last()?.Id ?? 0;
            }
            //client.OnReceiveError += Client_OnReceiveError;
            //client.OnReceiveGeneralError += Client_OnReceiveError;
            client.OnUpdate += Client_OnUpdate;
            client.OnCallbackQuery += Client_OnCallbackQuery;
            /*var dir = defaultNodeDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "WhoAmIBotNode.exe");
            Node n = new Node(path);
            n.NodeStopped += (sender, node) => Nodes.Remove(n);
            n.Start(Token);
            Nodes.Add(n);*/
            StartNode();
            client.StartReceiving();
        }

        public void StopBot()
        {
            while (Nodes.Count > 0)
            {
                switch (Nodes[0].State)
                {
                    case NodeState.Primary:
                        Nodes[0].SoftStop();
                        break;
                    case NodeState.Stopped:
                        //Nodes.RemoveAt(0);
                        break;
                    case NodeState.Stopping:
                        Nodes[0].Queue("PING");
                        break;
                }
            }
            //client.OnReceiveError -= Client_OnReceiveError;
            //client.OnReceiveGeneralError -= Client_OnReceiveError;
            client.OnUpdate -= Client_OnUpdate;
            client.OnCallbackQuery -= Client_OnCallbackQuery;
            client.StopReceiving();
        }
        #endregion
        #region On Update
        protected void Client_OnUpdate(object sender, UpdateEventArgs e)
        {
            try
            {
            if (e.Update.Type != UpdateType.Message && e.Update.Type != UpdateType.CallbackQuery) return;
            if (e.Update.Type == UpdateType.Message &&
                (e.Update.Message.Type != MessageType.Text)) return; //workaround for the bug
            if (e.Update.Type == UpdateType.Message && e.Update.Message.ReplyToMessage != null 
                && e.Update.Message.ReplyToMessage.Type != MessageType.Text && 
                !(e.Update.Message.ReplyToMessage.Type == MessageType.Document && e.Update.Message.ReplyToMessage.Document.FileName.ToLower().EndsWith(".txt"))) 
                e.Update.Message.ReplyToMessage = null;
            if (e.Update.Type == UpdateType.Message && e.Update.Message.Type == MessageType.Text && e.Update.Message.Entities != null)
            {
                if (e.Update.Message.Entities.Length > 0 && e.Update.Message.Entities[0].Type == MessageEntityType.BotCommand
                    && e.Update.Message.Entities[0].Offset == 0)
                {
                    var cmdEntity = e.Update.Message.Entities[0];
                    var cmd = e.Update.Message.Text.Substring(cmdEntity.Offset, cmdEntity.Length);
                    cmd = cmd.ToLower();
                    cmd = cmd.Contains($"@{Username.ToLower()}") ? cmd.Remove(cmd.IndexOf($"@{Username.ToLower()}")) : cmd;
                    

                    if (StandaloneCommandExists(cmd))
                    {
                        var node = Nodes.FirstOrDefault(x => x.State == NodeState.Primary);
                        if (node == null && !updating)
                        {
                            StartNode();
                            node = Nodes.FirstOrDefault(x => x.State == NodeState.Primary);
                        }
                        node?.Queue(JsonConvert.SerializeObject(e.Update));
                        return;
                    }
                }
            }
            client.SendTextMessageAsync(tracingChannelId, $"Update incoming: `Type: {e.Update.Type}`", ParseMode.Markdown);
            foreach (var node in Nodes)
            {
                node.Queue(JsonConvert.SerializeObject(e.Update));
            }
            if (e.Update.Type == UpdateType.Message && e.Update.Message.Type == MessageType.Text
            && e.Update.Message.Text == "I hereby grant you permission." && e.Update.Message.From.Id == Flom)
            {
                var msg = e.Update.Message;
                if (msg.ReplyToMessage == null) return;
                var par = new Dictionary<string, object>() { { "id", msg.ReplyToMessage.From.Id } };
                ExecuteSql("INSERT INTO GlobalAdmins(Id) VALUES(@id)", par);
                SendLangMessage(msg.Chat.Id, Strings.PowerGranted);
            }
            } catch 
            {
                client.GetUpdatesAsync(offset: e.Update.Id).Wait();
            }
        }
        #endregion
        #region On callback query
        private void Client_OnCallbackQuery(object sender, CallbackQueryEventArgs e)
        {
            if (!e.CallbackQuery.Data.Contains("@")
                || !long.TryParse(e.CallbackQuery.Data.Substring(e.CallbackQuery.Data.IndexOf("@") + 1), out long id)
                || e.CallbackQuery.Message == null) return;
            if (GameExists(id)) return;
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
        #endregion

        #region Start Node
        private void StartNode()
        {
            var dir = Directory.EnumerateDirectories(nodeReleaseDirectory, "WhoAmIBotNode_*").OrderBy(x => x).Last();
            Node n = new Node(Path.Combine(dir, "WhoAmIBotNode.exe"));
            n.Start(Token);
            n.NodeStopped += (sender, node) => Nodes.Remove(n);
            Nodes.Add(n);
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
            string key = "";
            if (GroupExists(id)) key = GetGroupValue("LangKey", id);
            else if (UserExists(id)) key = GetUserValue("LangKey", id);
            else key = defaultLangCode;
            if (LangKeyExists(key)) return key;
            else
            {
                var par = new Dictionary<string, object>() { { "id", id } };
                var q = ExecuteSql("SELECT key FROM existinglanguages WHERE key LIKE " +
                    "(SUBSTR((SELECT langkey FROM users where id=@p0), 0, 3)||'-%')", par);
                if (q.Count > 0 && q[0].Count > 0) return q[0][0];
                else return defaultLangCode;
            }
        }
        #endregion
        #region Send Lang Message
        private bool SendLangMessage(long chatid, string key, InlineKeyboardMarkup markup = null)
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
        /*private void Client_OnReceiveError(object sender, EventArgs ea)
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
        }*/
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
