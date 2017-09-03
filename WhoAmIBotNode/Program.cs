using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WhoAmIBotSpace.Classes;
using WhoAmIBotSpace.Helpers;
using File = System.IO.File;
using Telegram.Bot;
using System.Net;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.InlineKeyboardButtons;
using System.Text;
using System.Reflection;
using System.IO.Compression;

namespace WhoAmIBotSpace
{
    class Program
    {
        #region Properties
        public static string Username { get; set; }
        private static NodeState State { get; set; } = NodeState.Primary;
        #endregion
        #region Constants
        private const string baseFilePath = "C:\\Olfi01\\WhoAmIBot\\";
        private const string sqliteFilePath = baseFilePath + "db.sqlite";
        private const string connectionString = "Data Source=\"" + sqliteFilePath + "\";";
        private const string defaultLangCode = "en-US";
        /*private const string yesEmoji = "✅";
        private const string noEmoji = "❌";
        private const string idkEmoji = "🤷‍♂";*/
        private const int minPlayerCount = 2;
        private const long testingGroupId = -1001070844778;
        private const string allLangSelector = "SELECT key, name FROM ExistingLanguages ORDER BY key";
        private static readonly TimeSpan maxIdleJoinTime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan maxIdleGameTime = TimeSpan.FromHours(24);
        private static readonly List<string> gameQueries = new List<string>()
        {
            "yes@",
            "idk@",
            "no@",
            "guess@",
            "giveup@"
        };
        private static readonly Regex SelectLangRegex = new Regex(@"^lang:.+@-*\d+$");
        protected static readonly Flom Flom = new Flom();
        private const long supportId = -1001093405914;
        #endregion
        #region Fields
        private static SQLiteConnection sqliteConn;
        private static Dictionary<string, Action<Message>> commands = new Dictionary<string, Action<Message>>();
        private static bool Maintenance = false;
        private static TelegramBotClient client;
        private static bool running = true;
        private static List<Thread> currentThreads = new List<Thread>();
        private static List<NodeGame> NodeGames = new List<NodeGame>();
        #endregion
        #region Events
        public static event EventHandler<GameFinishedEventArgs> GameFinished;
        public static event EventHandler<CallbackQueryEventArgs> OnCallbackQuery;
        public static event EventHandler<MessageEventArgs> OnMessage;
        public static event EventHandler<AfkEventArgs> OnAfk;
        #endregion

        #region Helpers
        #region Enums
        private enum GameIdType
        {
            Id,
            GroupId
        }
        #endregion
        #region Exists
        private static bool GroupExists(long groupid)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT id FROM Groups WHERE Id=@id", sqliteConn);
            cmd.Parameters.Add(new SQLiteParameter("id", groupid));
            return cmd.ExecuteScalar() != null;
        }

        private static bool UserExists(long userid)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT id FROM Users WHERE Id=@id", sqliteConn);
            cmd.Parameters.Add(new SQLiteParameter("id", userid));
            return cmd.ExecuteScalar() != null;
        }

        private static bool LangKeyExists(string key)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT Key FROM ExistingLanguages WHERE Key=@key", sqliteConn);
            cmd.Parameters.Add(new SQLiteParameter("key", key));
            return cmd.ExecuteScalar() != null;
        }

        private static bool GlobalAdminExists(long id)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT Id FROM GlobalAdmins WHERE Id=@id", sqliteConn);
            cmd.Parameters.Add(new SQLiteParameter("id", id));
            return cmd.ExecuteScalar() != null;
        }

        private static bool GameExists(long groupid)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT GroupId FROM Games WHERE GroupId=@id", sqliteConn);
            cmd.Parameters.Add(new SQLiteParameter("id", groupid));
            return cmd.ExecuteScalar() != null;
        }

        private static bool NextgameExists(long id, long groupId)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT Id, GroupId FROM Nextgame WHERE Id=@id AND GroupId=@groupid", sqliteConn);
            cmd.Parameters.AddRange(new SQLiteParameter[] { new SQLiteParameter("id", id), new SQLiteParameter("groupid", groupId) });
            return cmd.ExecuteScalar() != null;
        }

        private static bool NextgameExists(long groupId)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT GroupId FROM Nextgame WHERE GroupId=@groupid", sqliteConn);
            cmd.Parameters.Add(new SQLiteParameter("groupid", groupId));
            return cmd.ExecuteScalar() != null;
        }
        #endregion
        #region Set Value
        private static void SetValue(string table, string column, object value, object identifier, string identifierName = "Id")
        {
            var cmd = new SQLiteCommand($"UPDATE {table} SET {column}=@val WHERE {identifierName}=@id", sqliteConn);
            cmd.Parameters.AddRange(new SQLiteParameter[] { new SQLiteParameter("val", value), new SQLiteParameter("id", identifier) });
            cmd.ExecuteNonQuery();
        }

        private static void SetGroupValue(string column, object value, long id)
        {
            SetValue("Groups", column, value, id, "Id");
        }

        private static void SetUserValue(string column, object value, long id)
        {
            SetValue("Users", column, value, id, "Id");
        }
        #endregion
        #region Get Value
        private static object GetValue(string table, string column, object identifier, string identifierName = "Id")
        {
            var cmd = new SQLiteCommand($"SELECT {column} FROM {table} WHERE {identifierName}=@id", sqliteConn);
            cmd.Parameters.AddWithValue("id", identifier);
            return cmd.ExecuteScalar();
        }

        private static T GetValue<T>(string table, string column, object identifier, string identifierName = "Id")
        {
            var cmd = new SQLiteCommand($"SELECT {column} FROM {table} WHERE {identifierName}=@id", sqliteConn);
            cmd.Parameters.AddWithValue("id", identifier);
            var res = cmd.ExecuteScalar();
            return res is DBNull ? default(T) : (T)res;
        }

        private static object GetGroupValue(string column, long id)
        {
            return GetValue("Groups", column, id, "Id");
        }

        private static T GetGroupValue<T>(string column, long id)
        {
            return GetValue<T>("Groups", column, id, "Id");
        }

        private static object GetUserValue(string column, long id)
        {
            return GetValue("Users", column, id, "Id");
        }

        private static T GetUserValue<T>(string column, long id)
        {
            return GetValue<T>("Users", column, id, "Id");
        }

        private static object GetGameValue(string column, long id, GameIdType git)
        {
            string idName = "Id";
            switch (git)
            {
                case GameIdType.GroupId:
                    idName = "GroupId";
                    break;
                case GameIdType.Id:
                    idName = "Id";
                    break;
            }
            return GetValue("Games", column, id, idName);
        }

        private static T GetGameValue<T>(string column, long id, GameIdType git)
        {
            string idName = "Id";
            switch (git)
            {
                case GameIdType.GroupId:
                    idName = "GroupId";
                    break;
                case GameIdType.Id:
                    idName = "Id";
                    break;
            }
            return GetValue<T>("Games", column, id, idName);
        }
        #endregion
        #region Add
        private static void AddGroup(long id, string name, string langKey = defaultLangCode)
        {
            var cmd = new SQLiteCommand("INSERT INTO Groups(Id, Name, LangKey, JoinTimeout, GameTimeout) VALUES(@id, @name, @langKey, 10, 1440)", sqliteConn);
            cmd.Parameters.AddRange(new SQLiteParameter[]
            { new SQLiteParameter("id", id),new SQLiteParameter("name", name), new SQLiteParameter("langKey", langKey) });
            cmd.ExecuteNonQuery();
        }

        private static void AddUser(long id, string langKey, string name, string username)
        {
            var cmd = new SQLiteCommand("INSERT INTO Users(Id, LangKey, Name, Username) VALUES(@id, @langKey, @name, @username)", sqliteConn);
            cmd.Parameters.AddRange(new SQLiteParameter[] { new SQLiteParameter("id", id), new SQLiteParameter("name", name),
                new SQLiteParameter("langKey", langKey), new SQLiteParameter("username", username) });
            cmd.ExecuteNonQuery();
        }

        private static void AddNextgame(long id, long groupid)
        {
            var cmd = new SQLiteCommand("INSERT INTO Nextgame(Id, GroupId) VALUES(@id, @groupid)", sqliteConn);
            cmd.Parameters.AddRange(new SQLiteParameter[] { new SQLiteParameter("id", id), new SQLiteParameter("groupid", groupid) });
            cmd.ExecuteNonQuery();
        }

        private static void AddGame(long groupid)
        {
            var cmd = new SQLiteCommand("INSERT INTO Games(GroupId) VALUES(@id)", sqliteConn);
            cmd.Parameters.AddWithValue("id", groupid);
            cmd.ExecuteNonQuery();
        }

        private static void AddGameFinished(long groupid, long winnerid, string winnername)
        {
            var cmd = new SQLiteCommand("INSERT INTO GamesFinished(GroupId, WinnerId, WinnerName) VALUES(@id, @winnerid, @winnername)", sqliteConn);
            cmd.Parameters.AddRange(new SQLiteParameter[]
            { new SQLiteParameter("id", groupid), new SQLiteParameter("winnerid", winnerid), new SQLiteParameter("winnername", winnername) });
            cmd.ExecuteNonQuery();
        }
        #endregion
        #region Get
        private static NodeUser GetNodeUser(long userid)
        {
            var cmd = new SQLiteCommand("SELECT * FROM Users WHERE Id=@id", sqliteConn);
            cmd.Parameters.AddWithValue("id", userid);
            using (var reader = cmd.ExecuteReader())
            {
                reader.Read();
                return new NodeUser((long)reader["Id"])
                {
                    LangKey = reader["LangKey"] is DBNull ? null : (string)reader["LangKey"],
                    Name = reader["Name"] is DBNull ? null : (string)reader["Name"],
                    Username = reader["Username"] is DBNull ? null : (string)reader["Username"]
                };
            }
        }
        #endregion
        #endregion

        #region Settings
        #region Cancelgame
        private static void SetCancelgame(long groupid, long chat)
        {
            var mre = new ManualResetEvent(false);
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                var d = e.CallbackQuery.Data;
                if ((!d.StartsWith("cancelgameYes@") && !d.StartsWith("cancelgameNo@"))
                || d.IndexOf("@") != d.LastIndexOf("@")
                || !long.TryParse(d.Substring(d.IndexOf("@") + 1), out long grp)
                || grp != groupid
                || e.CallbackQuery.Message == null
                || e.CallbackQuery.From.Id != chat) return;
                if (!GroupExists(groupid)) return;
                switch (d.Remove(d.IndexOf("@")))
                {
                    case "cancelgameYes":
                        SetGroupValue("CancelgameAdmin", true, groupid);
                        EditLangMessage(e.CallbackQuery.Message.Chat.Id, groupid, e.CallbackQuery.Message.MessageId,
                            Strings.CancelgameA, null, GetString(Strings.True, groupid));
                        break;
                    case "cancelgameNo":
                        SetGroupValue("CancelgameAdmin", false, groupid);
                        EditLangMessage(e.CallbackQuery.Message.Chat.Id, groupid, e.CallbackQuery.Message.MessageId,
                            Strings.CancelgameA, null, GetString(Strings.False, groupid));
                        break;
                }
                mre.Set();
            };
            if (!GroupExists(groupid)) return;
            SendLangMessage(chat, Strings.CancelgameQ, ReplyMarkupMaker.InlineYesNo(GetString(Strings.Yes, groupid), $"cancelgameYes@{groupid}",
                GetString(Strings.No, groupid), $"cancelgameNo@{groupid}"),
                GetString(GetGroupValue<bool>("CancelgameAdmin", groupid) ? Strings.True : Strings.False, groupid));
            try
            {
                OnCallbackQuery += cHandler;
                mre.WaitOne();
            }
            finally
            {
                OnCallbackQuery -= cHandler;
            }
        }
        #endregion
        #region Join Timeout
        private static void SetJoinTimeout(long groupid, long chat)
        {
            var mre = new ManualResetEvent(false);
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                var d = e.CallbackQuery.Data;
                if (!d.StartsWith("joinTimeout:") || !d.Contains("@") || d.IndexOf("@") != d.LastIndexOf("@")
                || !long.TryParse(d.Substring(d.IndexOf("@") + 1), out long grp)
                || grp != groupid || e.CallbackQuery.From.Id != chat
                || e.CallbackQuery.Message == null
                || !int.TryParse(d.Remove(d.IndexOf("@")).Substring(d.IndexOf(":") + 1), out int val)
                || !GroupExists(groupid)) return;
                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                SetGroupValue("JoinTimeout", val, groupid);
                EditLangMessage(e.CallbackQuery.Message.Chat.Id, groupid, e.CallbackQuery.Message.MessageId,
                    Strings.JoinTimeoutA, null, val.ToString());
                mre.Set();
            };
            var row = new InlineKeyboardButton[3];
            row[0] = new InlineKeyboardCallbackButton("2", $"joinTimeout:2@{groupid}");
            row[1] = new InlineKeyboardCallbackButton("5", $"joinTimeout:5@{groupid}");
            row[2] = new InlineKeyboardCallbackButton("10", $"joinTimeout:10@{groupid}");
            InlineKeyboardMarkup markup = new InlineKeyboardMarkup(row);
            if (!GroupExists(groupid)) return;
            SendLangMessage(chat, groupid, Strings.JoinTimeoutQ, markup,
                maxIdleJoinTime.TotalMinutes.ToString(), GetGroupValue<int>("JoinTimeout", groupid).ToString());
            try
            {
                OnCallbackQuery += cHandler;
                mre.WaitOne();
            }
            finally
            {
                OnCallbackQuery -= cHandler;
            }
        }
        #endregion
        #region Game Timeout
        private static void SetGameTimeout(long groupid, long chat)
        {
            var mre = new ManualResetEvent(false);
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                var d = e.CallbackQuery.Data;
                if (!d.StartsWith("gameTimeout:") || !d.Contains("@") || d.IndexOf("@") != d.LastIndexOf("@")
                || !long.TryParse(d.Substring(d.IndexOf("@") + 1), out long grp)
                || grp != groupid || e.CallbackQuery.From.Id != chat
                || e.CallbackQuery.Message == null
                || !int.TryParse(d.Remove(d.IndexOf("@")).Substring(d.IndexOf(":") + 1), out int val)
                || !GroupExists(groupid)) return;
                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                SetGroupValue("GameTimeout", val, groupid);
                EditLangMessage(e.CallbackQuery.Message.Chat.Id, groupid, e.CallbackQuery.Message.MessageId,
                    Strings.GameTimeoutA, null, $"{val / 60}h");
                mre.Set();
            };
            var row = new InlineKeyboardButton[4];
            row[0] = new InlineKeyboardCallbackButton("1h", $"gameTimeout:60@{groupid}");
            row[1] = new InlineKeyboardCallbackButton("6h", $"gameTimeout:360@{groupid}");
            row[2] = new InlineKeyboardCallbackButton("12h", $"gameTimeout:720@{groupid}");
            row[3] = new InlineKeyboardCallbackButton("24h", $"gameTimeout:1440@{groupid}");
            InlineKeyboardMarkup markup = new InlineKeyboardMarkup(row);
            if (!GroupExists(groupid)) return;
            SendLangMessage(chat, groupid, Strings.GameTimeoutQ, markup,
                $"{maxIdleGameTime.TotalHours}h", $"{GetGroupValue<int>("GameTimeout", groupid) / 60}h");
            try
            {
                OnCallbackQuery += cHandler;
                mre.WaitOne();
            }
            finally
            {
                OnCallbackQuery -= cHandler;
            }
        }
        #endregion
        #region Auto End
        private static void SetAutoEnd(long groupid, long chat)
        {
            var mre = new ManualResetEvent(false);
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                var d = e.CallbackQuery.Data;
                if (!d.StartsWith("autoEnd:") || !d.Contains("@") || d.IndexOf("@") != d.LastIndexOf("@")
                || !long.TryParse(d.Substring(d.IndexOf("@") + 1), out long grp)
                || grp != groupid || e.CallbackQuery.From.Id != chat
                || e.CallbackQuery.Message == null
                || !int.TryParse(d.Remove(d.IndexOf("@")).Substring(d.IndexOf(":") + 1), out int val)
                || !GroupExists(groupid)) return;
                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                SetGroupValue("AutoEnd", val, groupid);
                EditLangMessage(e.CallbackQuery.Message.Chat.Id, groupid, e.CallbackQuery.Message.MessageId,
                    Strings.AutoEndA, null, GetString(GetStringKey((AutoEndSetting)val), groupid));
                mre.Set();
            };
            var rows = new InlineKeyboardButton[3][];
            var none = GetString(GetStringKey(AutoEndSetting.None), groupid);
            var onePlayerGuessed = GetString(GetStringKey(AutoEndSetting.OnePlayerGuessed), groupid);
            var onePlayerLeft = GetString(GetStringKey(AutoEndSetting.OnePlayerLeft), groupid);
            rows[0] = new InlineKeyboardButton[] { new InlineKeyboardCallbackButton(none, $"autoEnd:{(int)AutoEndSetting.None}@{groupid}") };
            rows[1] = new InlineKeyboardButton[] { new InlineKeyboardCallbackButton(onePlayerGuessed, $"autoEnd:{(int)AutoEndSetting.OnePlayerGuessed}@{groupid}") };
            rows[2] = new InlineKeyboardButton[] { new InlineKeyboardCallbackButton(onePlayerLeft, $"autoEnd:{(int)AutoEndSetting.OnePlayerLeft}@{groupid}") };
            InlineKeyboardMarkup markup = new InlineKeyboardMarkup(rows);
            if (!GroupExists(groupid)) return;
            SendLangMessage(chat, groupid, Strings.AutoEndQ, markup,
                GetString(GetStringKey((AutoEndSetting)GetGroupValue<int>("AutoEnd", groupid)), groupid));
            try
            {
                OnCallbackQuery += cHandler;
                mre.WaitOne();
            }
            finally
            {
                OnCallbackQuery -= cHandler;
            }
        }

        private static string GetStringKey(AutoEndSetting val)
        {
            switch (val)
            {
                case AutoEndSetting.None:
                    return Strings.NoAutoEnd;
                case AutoEndSetting.OnePlayerGuessed:
                    return Strings.OnePlayerGuessedAutoEnd;
                case AutoEndSetting.OnePlayerLeft:
                    return Strings.OnePlayerLeftAutoEnd;
                default:
                    return Strings.NotImplemented;
            }
        }
        #endregion
        #endregion

        #region Initialization stuff
        private static void Init()
        {
            if (!Directory.Exists(baseFilePath)) Directory.CreateDirectory(baseFilePath);
            if (!File.Exists(sqliteFilePath)) SQLiteConnection.CreateFile(sqliteFilePath);
            InitSqliteConn();
            ReadCommands();
            var task = client.GetMeAsync();
            task.Wait();
            Username = task.Result.Username;
            OnCallbackQuery += Cancelnextgame_Handler;
        }

        private static void InitSqliteConn()
        {
            sqliteConn = new SQLiteConnection(connectionString);
            sqliteConn.Open();
        }

        private static void Dispose()
        {
            running = false;
            sqliteConn.Close();
            foreach (Thread t in currentThreads)
            {
                t?.Abort();
            }
        }
        #endregion
        #region Main method
        static void Main(string[] args)
        {
            if (args.Length < 1) return;
            using (PipeStream pipeClient = new AnonymousPipeClientStream(PipeDirection.In, args[0]))
            {
                using (var sr = new StreamReader(pipeClient))
                {
                    string data;
                    while (running)
                    {
                        while ((data = sr.ReadLine()) == null) ;
#if DEBUG
                        if (!string.IsNullOrEmpty(data)) Console.WriteLine(data);
#endif
                        if (string.IsNullOrEmpty(data)) continue;
                        if (data.StartsWith("TOKEN:"))
                        {
                            client = new TelegramBotClient(data.Substring(data.IndexOf(":") + 1));
                            Init();
                            Console.WriteLine($"Node: Username: {Username}");
                            continue;
                        }
                        /*if (data.StartsWith("PING:"))
                        {
                            data = data.Substring(5);
                            var s = data.Split(':');
                            var now = DateTime.Now;
                            Console.WriteLine($"{now.Hour - Convert.ToInt64(s[0])}:{now.Minute - Convert.ToInt64(s[1])}:{now.Second - Convert.ToInt64(s[2])}:{now.Millisecond - Convert.ToInt64(s[3])}");
                            continue;
                        }*/
                        if (data.StartsWith("STOP"))
                        {
                            Console.WriteLine("Stopping node at " + Assembly.GetExecutingAssembly().Location);
                            State = NodeState.Stopping;
                            if (NodeGames.Count > 0)
                            {
                                EventHandler<GameFinishedEventArgs> gfHandler = (sender, e) => { };
                                gfHandler = (sender, e) =>
                                {
                                    if (NodeGames.Count < 1)
                                    {
                                        State = NodeState.Stopped;
                                        running = false;
                                        GameFinished -= gfHandler;
                                    }
                                };
                                GameFinished += gfHandler;
                            }
                            else
                            {
                                State = NodeState.Stopped;
                                running = false;
                            }
                            continue;
                        }
                        if (!string.IsNullOrEmpty(data))
                        {
                            HandleData(data);
                        }
                    }
                }
            }
            Dispose();
        }
        #endregion
        #region Handle Data
        private static void HandleData(string data)
        {
#if DEBUG
            do
            {
#else
            try
            {
#endif
                var update = JsonConvert.DeserializeObject<Update>(data);
                if (update.Type == UpdateType.MessageUpdate && update.Message.Type == MessageType.TextMessage)
                {
                    foreach (var entity in update.Message.Entities)
                    {
                        if (entity.Offset != 0) continue;
                        if (entity.Type == MessageEntityType.BotCommand)
                        {
                            string cmd = update.Message.EntityValues[update.Message.Entities.IndexOf(entity)];
                            cmd = cmd.ToLower();
                            cmd = cmd.Contains("@" + Username.ToLower()) ? cmd.Remove(cmd.IndexOf("@" + Username.ToLower())) : cmd;
                            if (commands.ContainsKey(cmd))
                            {
                                Thread t = new Thread(() =>
                                {
                                    try
                                    {
                                        commands[cmd].Invoke(update.Message);
                                    }
                                    catch (Exception ex)
                                    {
                                        client.SendTextMessageAsync(Flom, $"Who am I bot\n{ex.Message}\n{ex.StackTrace}");
                                    }
                                });
                                t.Start();
                                currentThreads.Add(t);
                            }
                        }
                    }
                }
                if (update.Type == UpdateType.MessageUpdate)
                {
                    OnMessage?.Invoke(null, new MessageEventArgs(update.Message));
                }
                if (update.Type == UpdateType.CallbackQueryUpdate)
                {
                    OnCallbackQuery?.Invoke(null, new CallbackQueryEventArgs(update.CallbackQuery));
                }
#if DEBUG
            } while (false);
#else
            }
            catch (Exception x)
            {
                client?.SendTextMessageAsync(Flom,
                    $"Error ocurred in Who Am I Bot:\n{x.Message}\n{x.StackTrace}\n{JsonConvert.SerializeObject(x.Data)}");
                if (client == null)
                    Console.WriteLine($"An error occurred in Node: {x.Message}\n{x.StackTrace}\n{JsonConvert.SerializeObject(x.Data)}");
            }
#endif
        }
        #endregion
        #region Cancelnextgame Handler
        public static void Cancelnextgame_Handler(object sender, CallbackQueryEventArgs e)
        {
            var data = e.CallbackQuery.Data;
            if (!data.Contains("@") || data.Remove(data.IndexOf("@")) != "cancelnextgame" 
                || !long.TryParse(data.Substring(data.IndexOf("@") + 1), out long groupid)
                || e.CallbackQuery.Message == null) return;
            var cmd = new SQLiteCommand("DELETE FROM Nextgame WHERE Id=@id AND GroupId=@groupid", sqliteConn);
            cmd.Parameters.AddRange(new SQLiteParameter[] 
            { new SQLiteParameter("id", e.CallbackQuery.From.Id), new SQLiteParameter("groupid", groupid) });
            cmd.ExecuteNonQuery();
            client.EditMessageTextAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId, 
                GetString(Strings.RemovedFromNextgameList, e.CallbackQuery.From.Id));
        }
        #endregion

        #region Language
        #region Get string
        private static string GetString(string key, string langCode)
        {
            if (string.IsNullOrEmpty(langCode)) langCode = defaultLangCode;
            var cmd = new SQLiteCommand($"SELECT value FROM '{langCode}' WHERE key=@key", sqliteConn);
            cmd.Parameters.AddWithValue("key", key);
            try
            {
                var res = cmd.ExecuteScalar();
                if (res == null) throw new Exception("value not found");
                else return (string)res;
            }
            catch
            {
                string cmdt = $"SELECT value FROM '{defaultLangCode}' WHERE key=@key";
                var cmd2 = new SQLiteCommand(cmdt, sqliteConn);
                cmd2.Parameters.AddWithValue("key", key);
                var res = cmd2.ExecuteScalar();
                return (string)res ?? $"String {key} missing. Inform @Olfi01.";
            }
        }

        private static string GetString(string key, long langFrom)
        {
            return GetString(key, LangCode(langFrom));
        }
        #endregion
        #region Lang code
        private static string LangCode(long id)
        {
            string key = "";
            if (GroupExists(id)) key = GetGroupValue<string>("LangKey", id) ?? defaultLangCode;
            else if (UserExists(id)) key = GetUserValue<string>("LangKey", id) ?? defaultLangCode;
            else key = defaultLangCode;
            if (LangKeyExists(key)) return key;
            else
            {
                var cmd = new SQLiteCommand("SELECT key FROM existinglanguages WHERE key LIKE" +
                    " (SUBSTR((SELECT langkey FROM users where id=@p0), 0, 3)||'-%')", sqliteConn);
                cmd.Parameters.AddWithValue("id", id);
                var res = cmd.ExecuteScalar();
                return res != null ? (string)res : defaultLangCode;
            }
        }
        #endregion
        #region Send Lang Message
        private static bool SendLangMessage(long chatid, string key, IReplyMarkup markup = null)
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

        private static bool SendLangMessage(long chatid, string key, IReplyMarkup markup, params string[] par)
        {
            try
            {
                string toSend = GetString(key, LangCode(chatid));
                for (int i = 0; i < par.Length; i++)
                {
                    toSend = toSend.Replace("{" + i + "}", par[i]);
                }
                var task = client.SendTextMessageAsync(chatid, toSend, replyMarkup: markup, parseMode: ParseMode.Html);
                task.Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool SendLangMessage(long chatid, long langFrom, string key, IReplyMarkup markup = null)
        {
            try
            {
                var task = client.SendTextMessageAsync(chatid, GetString(key, LangCode(langFrom)),
                    replyMarkup: markup, parseMode: ParseMode.Html);
                task.Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool SendLangMessage(long chatid, long langFrom, string key, IReplyMarkup markup, params string[] par)
        {
            try
            {
                string toSend = GetString(key, LangCode(langFrom));
                for (int i = 0; i < par.Length; i++)
                {
                    toSend = toSend.Replace("{" + i + "}", par[i]);
                }
                var task = client.SendTextMessageAsync(chatid, toSend, replyMarkup: markup, parseMode: ParseMode.Html);
                task.Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool SendAndGetLangMessage(long chatid, long langFrom, string key, IReplyMarkup markup,
            out Message message, out string text, params string[] par)
        {
            try
            {
                string toSend = GetString(key, LangCode(langFrom));
                for (int i = 0; i < par.Length; i++)
                {
                    toSend = toSend.Replace("{" + i + "}", par[i]);
                }
                var task = client.SendTextMessageAsync(chatid, toSend, replyMarkup: markup, parseMode: ParseMode.Html);
                task.Wait();
                message = task.Result;
                text = toSend;
                return true;
            }
            catch
            {
                message = null;
                text = null;
                return false;
            }
        }
        #endregion
        #region Edit Lang Message
        private static bool EditLangMessage(long chatid, long langFrom, int messageId, string key,
            IReplyMarkup markup, string appendStart, out Message sent, out string text, params string[] par)
        {
            try
            {
                string toSend = appendStart + GetString(key, LangCode(langFrom));
                for (int i = 0; i < par.Length; i++)
                {
                    toSend = toSend.Replace("{" + i + "}", par[i]);
                }
                var task = client.EditMessageTextAsync(chatid, messageId, toSend, replyMarkup: markup, parseMode: ParseMode.Html);
                task.Wait();
                sent = task.Result;
                text = toSend;
                return true;
            }
            catch
            {
                sent = null;
                text = null;
                return false;
            }
        }

        private static bool EditLangMessage(long chatid, long langFrom, int messageId, string key, IReplyMarkup markup = null)
        {
            return EditLangMessage(chatid, langFrom, messageId, key, markup, "", out var u, out var u2);
        }

        private static bool EditLangMessage(long chatid, long langFrom, int messageId, string key, IReplyMarkup markup, params string[] par)
        {
            return EditLangMessage(chatid, langFrom, messageId, key, markup, "", out var u, out var u2, par);
        }
        #endregion
        #region Get Lang File
#if DEBUG
        public static LangFile GetLangFile(string key, bool completify = true)
#else
        private static LangFile GetLangFile(string key, bool completify = true)
#endif
        {
            var langName = GetValue<string>("ExistingLanguages", "Name", key, "Key");
            LangFile lf = new LangFile()
            {
                LangKey = key,
                Name = langName,
                Strings = new List<JString>()
            };
            string command = $"SELECT Key, Value FROM '{key}'";
            if (completify) command = $"SELECT key, value FROM '{key}' UNION ALL" +
                    $" SELECT key, value FROM '{defaultLangCode}' WHERE key NOT IN (SELECT key FROM '{key}')";
            var cmd = new SQLiteCommand(command, sqliteConn);
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    lf.Strings.Add(new JString((string)reader["key"], (string)reader["value"]));
                }
            }
            return lf;
        }
        #endregion
        #endregion
        #region Command Methods
        #region Read Commands
        private static void ReadCommands()
        {
            commands.Add("/sql", new Action<Message>(SQL_Command));
            commands.Add("/startgame", new Action<Message>(Startgame_Command));
            commands.Add("/start", new Action<Message>(Start_Command));
            commands.Add("/join", new Action<Message>(Join_Command));
            commands.Add("/cancelgame", new Action<Message>(Cancelgame_Command));
            commands.Add("/go", new Action<Message>(Go_Command));
            commands.Add("/setlang", new Action<Message>(Setlang_Command));
            //commands.Add("/setdb", new Action<Message>(Setdb_Command));
            commands.Add("/nextgame", new Action<Message>(Nextgame_Command));
            commands.Add("/stats", new Action<Message>(Stats_Command));
            commands.Add("/getlang", new Action<Message>(Getlang_Command));
            commands.Add("/uploadlang", new Action<Message>(Uploadlang_Command));
            commands.Add("/maint", new Action<Message>(Maint_Command));
            commands.Add("/help", new Action<Message>(Help_Command));
            commands.Add("/getgames", new Action<Message>(Getgames_Command));
            commands.Add("/getroles", new Action<Message>(Getroles_Command));
            commands.Add("/communicate", new Action<Message>(Communicate_Command));
            commands.Add("/giveup", new Action<Message>(Giveup_Command));
            commands.Add("/langinfo", new Action<Message>(Langinfo_Command));
            commands.Add("/backup", new Action<Message>(Backup_Command));
            commands.Add("/rate", new Action<Message>(Rate_Command));
            //commands.Add("/canceljoin", new Action<Message>(Canceljoin_Command));
            commands.Add("/identify", new Action<Message>(Identify_Command));
            commands.Add("/ping", new Action<Message>(Ping_Command));
            commands.Add("/settings", new Action<Message>(Settings_Command));
            commands.Add("/nodes", new Action<Message>(Nodes_Command));
            commands.Add("/test", new Action<Message>(Test_Command));
            commands.Add("/afk", new Action<Message>(Afk_Command));
        }
        #endregion

        #region /test
        private static void Test_Command(Message msg)
        {
            
        }
        #endregion
        #region /afk
        private static void Afk_Command(Message msg)
        {
            if (!msg.Chat.Type.IsGroup())
            {
                if (State == NodeState.Primary)
                    SendLangMessage(msg.Chat.Id, Strings.NotInPrivate);
                return;
            }
            if (!GameExists(msg.Chat.Id) && State == NodeState.Primary)
            {
                SendLangMessage(msg.Chat.Id, Strings.NoGameRunning);
                return;
            }
            if (msg.ReplyToMessage == null)
            {
                SendLangMessage(msg.Chat.Id, Strings.ReplyToSomeone);
                return;
            }
            var gId2 = GetGameValue<long>("Id", msg.Chat.Id, GameIdType.GroupId);
            if (!NodeGames.Exists(x => x.Id == gId2)) return;
            NodeGame g = NodeGames.Find(x => x.Id == gId2);
            if (!g.Players.Exists(x => x.Id == msg.ReplyToMessage.From.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.PlayerNotInGame);
                return;
            }
            if (!g.DictFull())
            {
                SendLangMessage(msg.Chat.Id, Strings.RolesNotSet);
                return;
            }
            var t = client.GetChatMemberAsync(msg.Chat.Id, msg.From.Id);
            t.Wait();
            if (t.Result.Status == ChatMemberStatus.Administrator || t.Result.Status == ChatMemberStatus.Creator)
            {
                OnAfk?.Invoke(null, new AfkEventArgs(g, g.Players.Find(x => x.Id == msg.ReplyToMessage.From.Id)));
                return;
            }
            NodePlayer p = g.Players.Find(x => x.Id == msg.ReplyToMessage.From.Id);
            ManualResetEvent mre = new ManualResetEvent(false);
            List<long> voted = new List<long>();
            int msgId = 0;
            int playersVotedYes = 0;
            bool isAfk = false;
            string text = "";
            string yes = GetString(Strings.Yes, msg.Chat.Id);
            string no = GetString(Strings.No, msg.Chat.Id);
            var markup = ReplyMarkupMaker.InlineYesNo(yes, $"afk@{p.Id}", no, $"notAfk@{p.Id}");
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                var data = e.CallbackQuery.Data;
                if (!data.Contains("@") || !long.TryParse(data.Substring(data.IndexOf("@") + 1), out long pId)
                || pId != p.Id || e.CallbackQuery.Message == null || e.CallbackQuery.Message.MessageId != msgId
                || !g.Players.Exists(x => x.Id == e.CallbackQuery.From.Id)) return;
                if (voted.Contains(e.CallbackQuery.From.Id))
                {
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, GetString(Strings.VotedAlready, g.GroupId));
                    return;
                }
                voted.Add(e.CallbackQuery.From.Id);
                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                switch (data.Remove(data.IndexOf("@")))
                {
                    case "afk":
                        playersVotedYes++;
                        client.EditMessageTextAsync(msg.Chat.Id, msgId,
                            (text = text + $"\n{e.CallbackQuery.From.FullName()}: {yes}"), replyMarkup: markup);
                        break;
                    case "notAfk":
                        if (pId == e.CallbackQuery.From.Id)
                        {
                            mre.Set();
                            return;
                        }
                        client.EditMessageTextAsync(msg.Chat.Id, msgId,
                            (text = text + $"\n{e.CallbackQuery.From.FullName()}: {no}"), replyMarkup: markup);
                        break;
                }
                double perc = (double)playersVotedYes / g.Players.Count;
                if (perc > 0.6)
                {
                    isAfk = true;
                    mre.Set();
                }
                if (voted.Count == g.Players.Count) mre.Set();
            };
            SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, Strings.IsPlayerAfk, markup, out var m, out text);
            msgId = m.MessageId;
            try
            {
                OnCallbackQuery += cHandler;
                Timer timer = new Timer(x => mre.Set(), null, 20 * 1000, Timeout.Infinite);
                mre.WaitOne();
            }
            finally
            {
                OnCallbackQuery -= cHandler;
            }
            client.EditMessageReplyMarkupAsync(msg.Chat.Id, msgId);
            if (isAfk)
            {
                OnAfk?.Invoke(null, new AfkEventArgs(g, p));
            }
            else
            {
                SendLangMessage(msg.Chat.Id, Strings.NotEnoughPlayersAgreed);
            }
        }
        #endregion
        #region /backup
        private static void Backup_Command(Message msg)
        {
            if (!GlobalAdminExists(msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.NoGlobalAdmin);
                return;
            }
            if (!Directory.Exists("zip\\")) Directory.CreateDirectory("zip\\");
            const string temp = "zip\\temp.sqlite";
            if (File.Exists(temp)) File.Delete(temp);
            File.Copy(sqliteFilePath, temp);
            var cmd = new SQLiteCommand(allLangSelector, sqliteConn);
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var filename = $"zip\\{reader["key"]}.txt";
                    File.WriteAllText(filename, JsonConvert.SerializeObject(GetLangFile((string)reader["key"], false)));
                }
            }
            ZipFile.CreateFromDirectory("zip\\", "backup.zip");
            client.SendDocumentAsync(msg.Chat.Id, new FileToSend("backup.zip", File.OpenRead("backup.zip")), caption: "#whoamibotbackup");
        }
        #endregion
        #region /cancelgame
        private static void Cancelgame_Command(Message msg)
        {
            if (msg.Text.Contains(" ") && GlobalAdminExists(msg.From.Id))
            {
                if (long.TryParse(msg.Text.Substring(msg.Text.IndexOf(" ")), out long id))
                {
                    var par = new Dictionary<string, object>()
                    {
                        { "id", id }
                    };
                    var cmd = new SQLiteCommand("SELECT Id FROM Games WHERE Id=@id OR GroupId=@id", sqliteConn);
                    cmd.Parameters.AddWithValue("id", id);
                    var gId = (long)(cmd.ExecuteScalar() ?? 0);
                    if (!NodeGames.Exists(x => x.Id == gId)) return;
                    var g3 = NodeGames.Find(x => x.Id == gId);
                    CancelGame(g3);
                    SendLangMessage(msg.Chat.Id, Strings.GameCancelled);
                    SendLangMessage(g3.GroupId, Strings.GameCancelledByGlobalAdmin);
                    return;
                }
            }
            if (!GameExists(msg.Chat.Id) && State == NodeState.Primary)
            {
                SendLangMessage(msg.Chat.Id, Strings.NoGameRunning);
                return;
            }
            var gId2 = GetGameValue<long>("Id", msg.Chat.Id, GameIdType.GroupId);
            if (!NodeGames.Exists(x => x.Id == gId2)) return;
            NodeGame g = NodeGames.Find(x => x.Id == gId2);
            if (!GroupExists(msg.Chat.Id))
            {
                AddGroup(msg.Chat.Id, msg.Chat.Title);
            }
            if (!GlobalAdminExists(msg.From.Id))
            {
                bool cancancel = !GetGroupValue<bool>("CancelgameAdmin", msg.Chat.Id);
                if (msg.Chat.Type.IsGroup())
                {
                    var t3 = client.GetChatMemberAsync(msg.Chat.Id, msg.From.Id);
                    t3.Wait();
                    if (t3.Result.Status == ChatMemberStatus.Administrator || t3.Result.Status == ChatMemberStatus.Creator) cancancel = true;
                }
                if (!cancancel)
                {
                    SendLangMessage(msg.Chat.Id, Strings.AdminOnly);
                    return;
                }
            }
            if (!Help.Longer(g.Players, g.TotalPlayers).Exists(x => x.Id == msg.From.Id))
            {
                var t3 = client.GetChatMemberAsync(msg.Chat.Id, msg.From.Id);
                t3.Wait();
                if (!(t3.Result.Status == ChatMemberStatus.Administrator || t3.Result.Status == ChatMemberStatus.Creator
                    || GlobalAdminExists(msg.From.Id)))
                {
                    SendLangMessage(msg.Chat.Id, Strings.NotInGame);
                    return;
                }
            }
            CancelGame(g);
            SendLangMessage(msg.Chat.Id, Strings.GameCancelled);
        }
        #endregion
        #region /canceljoin
        /*private static void Canceljoin_Command(Message msg)
        {
            using (var db = new WhoAmIBotContext())
            {
                if (!db.GlobalAdmins.Any(x => x.Id == msg.From.Id))
                {
                    SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.NoGlobalAdmin);
                    return;
                }
                foreach (var g in NodeGames.FindAll(x => x.State == GameState.Joining))
                {
                    CancelGame(g);
                    client.SendTextMessageAsync(msg.Chat.Id, $"A game was cancelled in {g.GroupName} ({g.GroupId})");
                    SendLangMessage(g.GroupId, Strings.GameCancelledByGlobalAdmin);
                }
            }
        }*/
        #endregion
        #region /communicate
        private static void Communicate_Command(Message msg)
        {
            if (State != NodeState.Primary) return;
            if (msg.Chat.Type != ChatType.Private || !msg.Text.Contains(" ")) return;
            if (!GlobalAdminExists(msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NoGlobalAdmin);
                return;
            }
            if (!long.TryParse(msg.Text.Substring(msg.Text.IndexOf(" ")), out long linked)) return;
            else
            {
                ManualResetEvent mre = new ManualResetEvent(false);
                EventHandler<MessageEventArgs> mHandler = (sender, e) =>
                {
                    var msg2 = e.Message;
                    var priv = msg.Chat.Id;
                    if (msg2.Chat.Id != linked && msg2.Chat.Id != priv) return;
                    else if (msg2.Chat.Id == linked) client.ForwardMessageAsync(priv, linked, msg2.MessageId);
                    else if (msg2.Chat.Id == priv)
                    {
                        if (msg2.Type == MessageType.TextMessage &&
                        (msg2.Text == "/communicate" || msg2.Text == $"/communicate@{Username}"))
                        {
                            mre.Set();
                            return;
                        }
                        client.ForwardMessageAsync(linked, priv, msg2.MessageId);
                    }
                };
                try
                {
                    OnMessage += mHandler;
                    client.SendTextMessageAsync(msg.Chat.Id, "Communication started.");
                    SendLangMessage(linked, Strings.GASpeaking);
                    mre.WaitOne();
                }
                finally
                {
                    OnMessage -= mHandler;
                    client.SendTextMessageAsync(msg.Chat.Id, "Communication stopped.");
                    SendLangMessage(linked, Strings.MessageLinkStopped);
                }
            }
        }
        #endregion
        #region /getgames
        private static void Getgames_Command(Message msg)
        {
            if (!GlobalAdminExists(msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.NoGlobalAdmin);
                return;
            }
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) => { };
            List<Message> sent = new List<Message>();
            cHandler = (sender, e) =>
            {
                string data = e.CallbackQuery.Data;
                if ((!data.StartsWith("cancel:") && !data.StartsWith("communicate:") && !data.StartsWith("close@"))
                || !data.Contains("@") || data.IndexOf("@") != data.LastIndexOf("@")
                || !long.TryParse(data.Substring(data.IndexOf("@") + 1), out long chatid)
                || chatid != msg.Chat.Id || e.CallbackQuery.Message == null) return;
                if (!GlobalAdminExists(e.CallbackQuery.From.Id))
                {
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, GetString(Strings.NoGlobalAdmin, LangCode(msg.Chat.Id)));
                    return;
                }
                if (data.StartsWith("close@"))
                {
                    foreach (var m in sent)
                    {
                        client.EditMessageReplyMarkupAsync(m.Chat.Id, m.MessageId, null);
                    }
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                    OnCallbackQuery -= cHandler;
                    return;
                }
                if (!long.TryParse(data.Remove(data.IndexOf("@")).Substring(data.IndexOf(":") + 1), out long groupid)) return;
                var action = data.Remove(data.IndexOf(":"));
                switch (action)
                {
                    case "cancel":
                        if (!NodeGames.Exists(x => x.GroupId == groupid))
                        {
                            if (!GameExists(groupid) && State == NodeState.Primary)
                            {
                                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, "That game is no longer running.");
                            }
                            return;
                        }
                        NodeGame g = NodeGames.Find(x => x.GroupId == groupid);
                        CancelGame(g);
                        SendLangMessage(g.GroupId, Strings.GameCancelledByGlobalAdmin);
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, GetString(Strings.GameCancelled, e.CallbackQuery.From.Id));
                        return;
                    case "communicate":
                        Thread t = new Thread(() =>
                        {
                            try
                            {
                                commands["/communicate"].Invoke(new CommDummyMsg(from: e.CallbackQuery.From.Id, groupid: groupid));
                            }
                            catch (Exception ex)
                            {
                                client.SendTextMessageAsync(Flom, $"Who am I bot\n{ex.Message}\n{ex.StackTrace}");
                            }
                        });
                        t.Start();
                        currentThreads.Add(t);
                        return;
                }
            };
            List<string> list = string.Join("\n\n",
                NodeGames.Select(x => $"{x.Id} - {x.GroupName} ({x.GroupId}): {x.State} {x.GetPlayerList()}")).Split(2000);
            foreach (var s in list)
            {
                if (string.IsNullOrWhiteSpace(s)) continue;
                var t = client.SendTextMessageAsync(msg.Chat.Id, s, replyMarkup: ReplyMarkupMaker.InlineGetGames(NodeGames, msg.Chat.Id));
                t.Wait();
                sent.Add(t.Result);
            }
            if ((list.Count < 1 || list.All(x => string.IsNullOrEmpty(x))) && State == NodeState.Primary)
            {
                client.SendTextMessageAsync(msg.Chat.Id, "No games running");
            }
            OnCallbackQuery += cHandler;
        }
        #endregion
        #region /getlang
        private static void Getlang_Command(Message msg)
        {
            ManualResetEvent mre = new ManualResetEvent(false);
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                if (!e.CallbackQuery.Data.StartsWith("lang:") || e.CallbackQuery.From.Id != msg.From.Id) return;
                var split = e.CallbackQuery.Data.Split(':', '@');
                string key = split[1];
                long groupId = Convert.ToInt64(split[2]);
                if (groupId != msg.Chat.Id) return;
                LangFile lf = GetLangFile(key);
                string path = $"{key}.txt";
                File.WriteAllText(path, JsonConvert.SerializeObject(lf, Formatting.Indented), Encoding.UTF8);
                using (var str = File.OpenRead(path))
                {
                    client.SendDocumentAsync(msg.Chat.Id, new FileToSend(path, str), caption: null).Wait();
                }
                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                mre.Set();
            };
            SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, Strings.SelectLanguage,
                ReplyMarkupMaker.InlineChooseLanguage(new SQLiteCommand(allLangSelector, sqliteConn), msg.Chat.Id),
                out Message sent, out var u);
            try
            {
                OnCallbackQuery += cHandler;
                mre.WaitOne();
            }
            finally
            {
                OnCallbackQuery -= cHandler;
            }
            EditLangMessage(msg.Chat.Id, msg.Chat.Id, sent.MessageId, Strings.OneMoment);

        }
        #endregion
        #region /getroles
        private static void Getroles_Command(Message msg)
        {
            if (!msg.Chat.Type.IsGroup())
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInPrivate);
                return;
            }
            if (!NodeGames.Exists(x => x.GroupId == msg.Chat.Id))
            {
                if (!GameExists(msg.Chat.Id) && State == NodeState.Primary)
                {
                    SendLangMessage(msg.Chat.Id, Strings.NoGameRunning);
                }
                return;
            }
            NodeGame g = NodeGames.Find(x => x.GroupId == msg.Chat.Id);
            if (!g.TotalPlayers.Exists(x => x.Id == msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInGame);
                return;
            }
            var p = g.TotalPlayers.Find(x => x.Id == msg.From.Id);
            if (!g.DictFull())
            {
                SendLangMessage(msg.Chat.Id, Strings.RolesNotSet);
                return;
            }
            string rl = "";
            foreach (var kvp in g.RoleIdDict)
            {
                if (kvp.Key != p.Id) rl += $"\n<b>{WebUtility.HtmlEncode(g.TotalPlayers.Find(x => x.Id == kvp.Key).Name)}:</b> " +
                        $"<i>{WebUtility.HtmlEncode(kvp.Value)}</i>";
            }
            SendLangMessage(msg.From.Id, msg.Chat.Id, Strings.RolesAre, null, rl);
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.SentPM);
        }
        #endregion
        #region /giveup
        private static void Giveup_Command(Message msg)
        {
            if (!msg.Chat.Type.IsGroup())
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInPrivate);
                return;
            }
            if (!NodeGames.Exists(x => x.GroupId == msg.Chat.Id))
            {
                if (!GameExists(msg.Chat.Id) && State == NodeState.Primary)
                {
                    SendLangMessage(msg.Chat.Id, Strings.NoGameRunning);
                }
                return;
            }
            NodeGame g = NodeGames.Find(x => x.GroupId == msg.Chat.Id);
            if (!g.Players.Exists(x => x.Id == msg.From.Id && !x.GaveUp))
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInGame);
                return;
            }
            var p = g.Players.Find(x => x.Id == msg.From.Id);
            if (g.Turn == p)
            {
                SendLangMessage(msg.Chat.Id, Strings.ItsYourTurnCantGiveUp);
                return;
            }
            if (!g.DictFull())
            {
                SendLangMessage(msg.Chat.Id, Strings.RolesNotSet);
                return;
            }
            p.GaveUp = true;
            SendLangMessage(msg.From.Id, g.GroupId, Strings.YouGaveUp);
            SendLangMessage(g.GroupId, Strings.GaveUp, null, p.Name, g.RoleIdDict[msg.From.Id]);
        }
        #endregion
        #region /go
        private static void Go_Command(Message msg)
        {
            if (!NodeGames.Exists(x => x.GroupId == msg.Chat.Id))
            {
                if (!GameExists(msg.Chat.Id) && State == NodeState.Primary)
                {
                    SendLangMessage(msg.Chat.Id, Strings.NoGameRunning);
                }
                return;
            }
            NodeGame g = NodeGames.Find(x => x.GroupId == msg.Chat.Id);
            if (g == null) return;
            if (!g.Players.Exists(x => x.Id == msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInGame);
                return;
            }
            if (g.Players.Count < minPlayerCount && msg.Chat.Id != testingGroupId)
            {
                SendLangMessage(msg.Chat.Id, Strings.NotEnoughPlayers);
                return;
            }
            if (g.State != GameState.Joining)
            {
                SendLangMessage(msg.Chat.Id, Strings.GameRunning);
                return;
            }
            ParameterizedThreadStart pts = new ParameterizedThreadStart(StartGameFlow);
            Thread t = new Thread(pts);
            g.Thread = t;
            g.InactivityTimer.Change(g.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
            t.Start(g);
        }
        #endregion
        #region /help
        private static void Help_Command(Message msg)
        {
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.Help);
        }
        #endregion
        #region /identify
        private static void Identify_Command(Message msg)
        {
            ChatId i = Flom;
            if (i.Identifier != msg.From.Id) return;
            client.SendTextMessageAsync(msg.Chat.Id, "Yep, you are my developer", replyToMessageId: msg.MessageId);
        }
        #endregion
        #region /join
        private static void Join_Command(Message msg)
        {
            if (UserExists(msg.From.Id))
            {
                var u = GetNodeUser(msg.From.Id);
                if (u.Name != msg.From.FullName() || u.Username != msg.From.Username)
                {
                    SetUserValue("Name", msg.From.FullName(), msg.From.Id);
                    SetUserValue("Username", msg.From.Username, msg.From.Id);
                }
            }
            else
            {
                AddUser(msg.From.Id, msg.From.LanguageCode, msg.From.FullName(), msg.From.Username);
            }
            if (!NodeGames.Exists(x => x.GroupId == msg.Chat.Id))
            {
                if (!GameExists(msg.Chat.Id) && State == NodeState.Primary)
                {
                    SendLangMessage(msg.Chat.Id, Strings.NoGameRunning);
                }
                return;
            }
            NodeGame g = NodeGames.Find(x => x.GroupId == msg.Chat.Id);
            if (g == null) return;
            AddPlayer(g, new NodePlayer(msg.From.Id, msg.From.FullName()));
            g.InactivityTimer.Change(g.Group.JoinTimeout * 60 * 1000, Timeout.Infinite);
        }
        #endregion
        #region /langinfo
        private static void Langinfo_Command(Message msg)
        {
            ManualResetEvent mre = new ManualResetEvent(false);
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                if (!SelectLangRegex.IsMatch(e.CallbackQuery.Data)
                || !long.TryParse(e.CallbackQuery.Data.Substring(e.CallbackQuery.Data.IndexOf("@") + 1), out long gId)
                || gId != msg.Chat.Id
                || e.CallbackQuery.Message == null) return;
                Message sent = e.CallbackQuery.Message;
                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                var data = e.CallbackQuery.Data;
                var atI = data.IndexOf("@");
                var dpI = data.IndexOf(":");
                var key = data.Remove(atI).Substring(dpI + 1);
                var cmd = new SQLiteCommand($"SELECT Key, Value FROM '{key}'", sqliteConn);
                var query = new List<JString>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        query.Add(new JString((string)reader["Key"], (string)reader["Value"]));
                    }
                }
                var cmdDefault = new SQLiteCommand($"SELECT key, value FROM '{defaultLangCode}'", sqliteConn);
                var queryDefault = new List<JString>();
                using (var reader = cmdDefault.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        queryDefault.Add(new JString((string)reader["Key"], (string)reader["Value"]));
                    }
                }
                var missing = new List<string>();
                foreach (var js in queryDefault)
                {
                    if (!query.Exists(x => x.Key == js.Key)) missing.Add(js.Key);
                }
                foreach (var js in query)
                {
                    if (!queryDefault.Exists(x => x.Key == js.Key))
                    {
                        client.SendTextMessageAsync(testingGroupId, $"String {js.Key} found in {key} but not in {defaultLangCode}");
                        continue;
                    }

                    var enStr = queryDefault.Find(x => x.Key == js.Key).Value;
                    int extras = 0;
                    while (true)
                    {
                        string tag = "{" + extras + "}";
                        if (enStr.Contains(tag))
                        {
                            extras++;
                            if (!js.Value.Contains(tag))
                            {
                                missing.Add($"{tag} in {js.Key}");
                            }
                        }
                        else break;
                    }
                }
                EditLangMessage(sent.Chat.Id, sent.Chat.Id, sent.MessageId, Strings.LangInfo, null, "",
                    out var u, out var u2, key, "\n" + missing.ToStringList());
                mre.Set();
            };
            SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, Strings.SelectLanguage,
                ReplyMarkupMaker.InlineChooseLanguage(new SQLiteCommand(allLangSelector, sqliteConn), msg.Chat.Id),
                out var u0, out var u1);
            try
            {
                OnCallbackQuery += cHandler;
                mre.WaitOne();
            }
            finally
            {
                OnCallbackQuery -= cHandler;
            }
        }
        #endregion
        #region /maint
        private static void Maint_Command(Message msg)
        {
            if (!GlobalAdminExists(msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.NoGlobalAdmin);
            }
            if (!Maintenance)
            {
                Maintenance = true;
                SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.Maintenance);
                if (NodeGames.Count > 0)
                {
                    EventHandler<GameFinishedEventArgs> handler = null;
                    handler = (sender, e) =>
                    {
                        if (NodeGames.Count < 1)
                        {
                            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.GamesFinished, null, Assembly.GetExecutingAssembly().Location);
                            GameFinished -= handler;
                        }
                    };
                    GameFinished += handler;
                }
                else
                {
                    SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.GamesFinished, null, Assembly.GetExecutingAssembly().Location);
                }
            }
            else
            {
                Maintenance = false;
                SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.MaintenanceOff);
            }
        }
        #endregion
        #region /nextgame
        private static void Nextgame_Command(Message msg)
        {
            if (msg.Chat.Type == ChatType.Channel) return;
            if (msg.Chat.Type == ChatType.Private)
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInPrivate);
                return;
            }
            if (NextgameExists(msg.From.Id, msg.Chat.Id))
            {
                SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.AlreadyOnNextgameList);
                return;
            }
            AddNextgame(msg.From.Id, msg.Chat.Id);
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.PutOnNextgameList, 
                ReplyMarkupMaker.InlineCancelNextgame(GetString(Strings.Cancel, msg.Chat.Id), msg.Chat.Id));
        }
        #endregion
        #region /nodes
        private static void Nodes_Command(Message msg)
        {
            if (!GlobalAdminExists(msg.From.Id)) return;
            client.SendTextMessageAsync(msg.Chat.Id, Assembly.GetExecutingAssembly().Location).Wait();
        }
        #endregion
        #region /ping
        private static void Ping_Command(Message msg)
        {
            DateTime thisTime = DateTime.Now;
            bool isDaylight = TimeZoneInfo.Local.IsDaylightSavingTime(thisTime);
            var now = DateTime.Now.ToUniversalTime();
            var span = now.Subtract(msg.Date.Subtract(TimeSpan.FromHours(isDaylight ? 2 : 1)).ToUniversalTime());
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.Ping, null, Math.Abs(span.TotalSeconds).ToString());
        }
        #endregion
        #region /rate
        private static void Rate_Command(Message msg)
        {
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.RateMe, null, "http://t.me/storebot?start={Username}");
        }
        #endregion
        #region /setdb
        /*private static void Setdb_Command(Message msg)
        {
            if (msg.From.Id != Flom || msg.ReplyToMessage == null || msg.ReplyToMessage.Type != MessageType.DocumentMessage)
            {
                Console.WriteLine("Someone tried to set db");
                return;
            }
            sqliteConn.Close();
            sqliteConn.Dispose();
            File.Delete(sqliteFilePath);
            using (Stream str = File.OpenWrite(sqliteFilePath))
            {
                var task = client.GetFileAsync(msg.ReplyToMessage.Document.FileId, str);
                task.Wait();
                str.Flush();
                str.Close();
            }
            InitSqliteConn();
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.DatabaseUpdated);
        }*/
        #endregion
        #region /setlang
        private static void Setlang_Command(Message msg)
        {
            if (!GlobalAdminExists(msg.From.Id) && msg.Chat.Type.IsGroup())
            {
                var t = client.GetChatMemberAsync(msg.Chat.Id, msg.From.Id);
                t.Wait();
                if (t.Result.Status != ChatMemberStatus.Administrator && t.Result.Status != ChatMemberStatus.Creator)
                {
                    SendLangMessage(msg.Chat.Id, Strings.AdminOnly);
                    return;
                }
            }
            ManualResetEvent mre = new ManualResetEvent(false);
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                if (!e.CallbackQuery.Data.StartsWith("lang:") || e.CallbackQuery.From.Id != msg.From.Id) return;
                var split = e.CallbackQuery.Data.Split(':', '@');
                string key = split[1];
                long groupId = Convert.ToInt64(split[2]);
                if (groupId != msg.Chat.Id) return;
                if (msg.Chat.Type.IsGroup() && !GlobalAdminExists(e.CallbackQuery.From.Id))
                {
                    var task = client.GetChatMemberAsync(msg.Chat.Id, e.CallbackQuery.From.Id);
                    task.Wait();
                    if (task.Result.Status != ChatMemberStatus.Administrator && task.Result.Status != ChatMemberStatus.Creator)
                    {
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, GetString(Strings.AdminOnly, msg.Chat.Id));
                        return;
                    }
                }
                switch (msg.Chat.Type)
                {
                    case ChatType.Private:
                        if (!UserExists(e.CallbackQuery.From.Id))
                        {
                            AddUser(e.CallbackQuery.From.Id, key, e.CallbackQuery.From.FullName(), e.CallbackQuery.From.Username);
                        }
                        else
                        {
                            SetUserValue("LangKey", key, e.CallbackQuery.From.Id);
                        }
                        break;
                    case ChatType.Group:
                    case ChatType.Supergroup:
                        if (!GroupExists(msg.Chat.Id))
                        {
                            AddGroup(msg.Chat.Id, msg.Chat.Title, key);
                        }
                        else
                        {
                            SetGroupValue("LangKey", key, msg.Chat.Id);
                        }
                        break;
                }
                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                mre.Set();
            };
            SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, Strings.SelectLanguage,
                ReplyMarkupMaker.InlineChooseLanguage(new SQLiteCommand(allLangSelector, sqliteConn), msg.Chat.Id),
                out Message sent, out string useless);
            try
            {
                OnCallbackQuery += cHandler;
                mre.WaitOne();
            }
            finally
            {
                OnCallbackQuery -= cHandler;
            }
            EditLangMessage(msg.Chat.Id, msg.Chat.Id, sent.MessageId, Strings.LangSet, null, "", out var u, out var u2);
        }
        #endregion
        #region /settings
        private static void Settings_Command(Message msg)
        {
            if (!msg.Chat.Type.IsGroup())
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInPrivate);
                return;
            }
            if (!GroupExists(msg.Chat.Id))
            {
                AddGroup(msg.Chat.Id, msg.Chat.Title);
            }
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) => { };
            cHandler = (sender, e) =>
            {
                var d = e.CallbackQuery.Data;
                if ((!d.StartsWith("joinTimeout@") && !d.StartsWith("gameTimeout@")
                && !d.StartsWith("cancelgameAdmin@") && !d.StartsWith("closesettings@")
                && !d.StartsWith("autoEnd@"))
                || d.IndexOf("@") != d.LastIndexOf("@")
                || !long.TryParse(d.Substring(d.IndexOf("@") + 1), out long groupid)
                || groupid != msg.Chat.Id || e.CallbackQuery.Message == null) return;
                var action = d.Remove(d.IndexOf("@"));
                switch (action)
                {
                    case "joinTimeout":
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        Thread t1 = new Thread(() => SetJoinTimeout(groupid, msg.From.Id));
                        t1.Start();
                        currentThreads.Add(t1);
                        break;
                    case "gameTimeout":
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        Thread t2 = new Thread(() => SetGameTimeout(groupid, msg.From.Id));
                        t2.Start();
                        currentThreads.Add(t2);
                        break;
                    case "cancelgameAdmin":
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        Thread t3 = new Thread(() => SetCancelgame(groupid, msg.From.Id));
                        t3.Start();
                        currentThreads.Add(t3);
                        break;
                    case "autoEnd":
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        Thread t4 = new Thread(() => SetAutoEnd(groupid, msg.From.Id));
                        t4.Start();
                        currentThreads.Add(t4);
                        break;
                    case "closesettings":
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        EditLangMessage(e.CallbackQuery.Message.Chat.Id, groupid, e.CallbackQuery.Message.MessageId, Strings.Done, null);
                        OnCallbackQuery -= cHandler;
                        break;
                }
            };
            var joinTimeout = GetString(Strings.JoinTimeout, msg.Chat.Id);
            var gameTimeout = GetString(Strings.GameTimeout, msg.Chat.Id);
            var cancelgameAdmin = GetString(Strings.CancelgameAdmin, msg.Chat.Id);
            var autoEnd = GetString(Strings.AutoEnd, msg.Chat.Id);
            var close = GetString(Strings.Close, msg.Chat.Id);
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.SentPM);
            SendLangMessage(msg.From.Id, msg.Chat.Id, Strings.Settings, ReplyMarkupMaker.InlineSettings(msg.Chat.Id,
                joinTimeout, gameTimeout, cancelgameAdmin, autoEnd, close));
            OnCallbackQuery += cHandler;
        }
        #endregion
        #region /start
        private static void Start_Command(Message msg)
        {
            if (msg.Chat.Type != ChatType.Private) return;
            if (!UserExists(msg.From.Id))
            {
                AddUser(msg.From.Id, msg.From.LanguageCode, msg.From.FullName(), msg.From.Username);
                if (string.IsNullOrEmpty(msg.From.LanguageCode))
                {
                    SendLangMessage(msg.Chat.Id, Strings.Welcome);
                    Setlang_Command(msg);
                }
                else
                {
                    SendLangMessage(msg.Chat.Id, Strings.Welcome);
                }
            }
            else
            {
                var u = GetNodeUser(msg.From.Id);
                if (u.Name != msg.From.FullName() || u.Username != msg.From.Username)
                {
                    SetUserValue("Name", msg.From.FullName(), msg.From.Id);
                    SetUserValue("Username", msg.From.Username, msg.From.Id);
                }
                SendLangMessage(msg.Chat.Id, Strings.Welcome);
            }
        }
        #endregion
        #region /startgame
        private static void Startgame_Command(Message msg)
        {
            if (Maintenance && msg.Chat.Id != testingGroupId)
            {
                SendLangMessage(msg.Chat.Id, Strings.BotUnderMaintenance);
                return;
            }
            if (!msg.Chat.Type.IsGroup())
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInPrivate);
                return;
            }
            if (msg.Chat.Id == supportId)
            {
                client.SendTextMessageAsync(msg.Chat.Id, "No games in support chat!");
                return;
            }
            if (GameExists(msg.Chat.Id))
            {
                if (NodeGames.Exists(x => x.GroupId == msg.Chat.Id))
                {
                    SendLangMessage(msg.Chat.Id, Strings.GameRunning);
                }
                return;
            }
            if (UserExists(msg.From.Id))
            {
                var u = GetNodeUser(msg.From.Id);
                if (u.Name != msg.From.FullName() || u.Username != msg.From.Username)
                {
                    SetUserValue("Name", msg.From.FullName(), msg.From.Id);
                    SetUserValue("Username", msg.From.Username, msg.From.Id);
                }
            }
            else
            {
                AddUser(msg.From.Id, msg.From.LanguageCode, msg.From.FullName(), msg.From.Username);
            }
            if (!GroupExists(msg.Chat.Id))
            {
                AddGroup(msg.Chat.Id, msg.Chat.Title);
            }
            else
            {
                if (GetGroupValue<string>("Name", msg.Chat.Id) != msg.Chat.Title)
                {
                    SetGroupValue("Name", msg.Chat.Title, msg.Chat.Id);
                }
            }
            AddGame(msg.Chat.Id);
            NodeGame g = new NodeGame(GetGameValue<long>("Id", msg.Chat.Id, GameIdType.GroupId), msg.Chat.Id,
                msg.Chat.Title, new NodeGroup(msg.Chat.Id)
                {
                    Name = GetGroupValue<string>("Name", msg.Chat.Id),
                    LangKey = GetGroupValue<string>("LangKey", msg.Chat.Id),
                    CancelgameAdmin = GetGroupValue<bool>("CancelgameAdmin", msg.Chat.Id),
                    GameTimeout = GetGroupValue<int>("GameTimeout", msg.Chat.Id),
                    JoinTimeout = GetGroupValue<int>("JoinTimeout", msg.Chat.Id),
                    AutoEnd = (AutoEndSetting)GetGroupValue<int>("AutoEnd", msg.Chat.Id)
                });
            NodeGames.Add(g);
            if (NextgameExists(msg.Chat.Id))
            {
                var cmd = new SQLiteCommand("SELECT Id FROM Nextgame WHERE GroupId=@id", sqliteConn);
                cmd.Parameters.AddWithValue("id", msg.Chat.Id);
                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    SendLangMessage((long)reader["Id"], msg.Chat.Id, Strings.NewGameStarting, null, msg.Chat.Title);
                }
                var cmd2 = new SQLiteCommand("DELETE FROM Nextgame WHERE GroupId = @id", sqliteConn);
                cmd2.Parameters.AddWithValue("id", msg.Chat.Id);
            }
            SendLangMessage(msg.Chat.Id, Strings.GameStarted);
            SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, Strings.PlayerList, null, out Message m, out var u1, "");
            g.PlayerlistMessage = m;
            AddPlayer(g, new NodePlayer(msg.From.Id, msg.From.FullName()));
            Timer t = new Timer(x =>
            {
                CancelGame(g);
                SendLangMessage(g.GroupId, Strings.GameTimedOut);
            }, null, g.Group.JoinTimeout * 60 * 1000, Timeout.Infinite);
            g.InactivityTimer = t;
        }
        #endregion
        #region /stats
        private static void Stats_Command(Message msg)
        {
            long winCount = 0;
            var cmd = new SQLiteCommand("SELECT count(*) FROM GamesFinished WHERE Winnerid=@id", sqliteConn);
            cmd.Parameters.AddWithValue("id", msg.From.Id);
            var res = cmd.ExecuteScalar();
            winCount = res == null ? 0 : (long)res;
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.Stats, null, msg.From.FullName(), winCount.ToString());
        }
        #endregion
        #region /sql
        private static void SQL_Command(Message msg)
        {
            if (!GlobalAdminExists(msg.From.Id)) return;
            try
            {
                string commandText;
                if (msg.ReplyToMessage != null) commandText = msg.ReplyToMessage.Text;
                else
                {
                    client.SendTextMessageAsync(msg.Chat.Id, "SQL queries only work by reply to avoid unfinished queries to be executed.");
                    return;
                }
                string response = ExecuteSqlRaw(commandText);
                if (!string.IsNullOrEmpty(response))
                {
                    foreach (var s in response.Split(2000)) client.SendTextMessageAsync(msg.Chat.Id, s, parseMode: ParseMode.Html).Wait();
                }
            }
            catch (Exception e)
            {
                client.SendTextMessageAsync(msg.Chat.Id, $"Exception: {e.Message}\n{e.StackTrace}").Wait();
                while (e.InnerException != null)
                {
                    e = e.InnerException;
                    client.SendTextMessageAsync(msg.Chat.Id, $"Inner Exception: {e.Message}\n{e.StackTrace}").Wait();
                }
            }
        }
        #endregion
        #region /uploadlang
        private static void Uploadlang_Command(Message msg)
        {
            if (msg.ReplyToMessage == null || msg.ReplyToMessage.Type != MessageType.DocumentMessage) return;
            if (!GlobalAdminExists(msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NoGlobalAdmin);
                return;
            }
            var now = DateTime.Now;
            var path = $"{now.Hour}-{now.Minute}-{now.Second}-{now.Millisecond}.temp";
            using (var str = File.OpenWrite(path))
            {
                client.GetFileAsync(msg.ReplyToMessage.Document.FileId, str).Wait();
            }
            string text = File.ReadAllText(path);
            LangFile lf = null;
            try
            {
                ManualResetEvent mre = new ManualResetEvent(false);
                lf = JsonConvert.DeserializeObject<LangFile>(text);
                Message sent = null;
                bool permit = false;
                //check if lang exists
                EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
                {
                    if (!GlobalAdminExists(e.CallbackQuery.From.Id)
                    || e.CallbackQuery.Message.MessageId != sent.MessageId
                    || e.CallbackQuery.Message.Chat.Id != sent.Chat.Id) return;
                    if (e.CallbackQuery.Data == "yes") permit = true;
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                    mre.Set();
                };
                var cmd = new SQLiteCommand("SELECT key FROM ExistingLanguages WHERE Key=@key", sqliteConn);
                cmd.Parameters.AddWithValue("key", lf.LangKey);
                var res = cmd.ExecuteScalar();
                string yes = GetString(Strings.Yes, LangCode(msg.Chat.Id));
                string no = GetString(Strings.No, LangCode(msg.Chat.Id));
                if (res == null)
                {
                    //create new language
                    var cmdDefault = new SQLiteCommand($"SELECT key, value FROM '{defaultLangCode}'", sqliteConn);
                    var queryDefault = new List<JString>();
                    using (var reader = cmdDefault.ExecuteReader())
                    {
                        while (reader.Read()) queryDefault.Add(new JString((string)reader["key"], (string)reader["value"]));
                    }
                    var missing = new List<string>();
                    foreach (var js in queryDefault)
                    {
                        if (!lf.Strings.Exists(x => x.Key == js.Key)) missing.Add(js.Key);
                    }
                    var toRemove = new List<JString>();
                    foreach (var js in lf.Strings)
                    {
                        if (!queryDefault.Exists(x => x.Key == js.Key))
                        {
                            toRemove.Add(js);
                            continue;
                        }
                        var enStr = queryDefault.Find(x => x.Key == js.Key).Value;
                        int extras = 0;
                        while (true)
                        {
                            string tag = "{" + extras + "}";
                            if (enStr.Contains(tag))
                            {
                                extras++;
                                if (!js.Value.Contains(tag))
                                {
                                    missing.Add($"{tag} in {js.Key}");
                                    if (!toRemove.Exists(x => x.Key == js.Key)) toRemove.Add(js);
                                }
                            }
                            else break;
                        }
                    }
                    foreach (var js in toRemove)
                    {
                        lf.Strings.Remove(js);
                    }
                    SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, Strings.CreateLang,
                        ReplyMarkupMaker.InlineYesNo(yes, "yes", no, "no"), out sent, out var u,
                        lf.LangKey, lf.Name, lf.Strings.Count.ToString(), "\n" + missing.ToStringList());
                    try
                    {
                        OnCallbackQuery += cHandler;
                        mre.WaitOne();
                    }
                    finally
                    {
                        OnCallbackQuery -= cHandler;
                    }
                    if (permit)
                    {
                        var cmd2 = new SQLiteCommand("INSERT INTO ExistingLanguages(Key, Name) VALUES(@key, @name)", sqliteConn);
                        SQLiteParameter[] par = new SQLiteParameter[]
                        { new SQLiteParameter("key", lf.LangKey), new SQLiteParameter("name", lf.Name)};
                        cmd2.Parameters.AddRange(par);
                        cmd2.ExecuteNonQuery();
                        new SQLiteCommand($"CREATE TABLE '{lf.LangKey}'(Key varchar primary key, Value varchar)", sqliteConn).ExecuteNonQuery();
                        foreach (var js in lf.Strings)
                        {
                            if (!queryDefault.Exists(x => x.Key == js.Key)) continue;
                            if (missing.Exists(x => x.EndsWith(js.Key))) continue;
                            var cmd3 = new SQLiteCommand($"INSERT INTO '{lf.LangKey}' VALUES(@key, @value)");
                            cmd3.Parameters.AddRange(par);
                            cmd3.ExecuteNonQuery();
                        }
                    }
                }
                else
                {
                    //update old lang
                    var cmd2 = new SQLiteCommand($"SELECT Key, Value FROM '{lf.LangKey}'", sqliteConn);
                    var query = new List<JString>();
                    using (var reader = cmd2.ExecuteReader())
                    {
                        while (reader.Read()) query.Add(new JString((string)reader["key"], (string)reader["value"]));
                    }
                    var cmdDefault = new SQLiteCommand($"SELECT key, value FROM '{defaultLangCode}'", sqliteConn);
                    var queryDefault = new List<JString>();
                    using (var reader = cmdDefault.ExecuteReader())
                    {
                        while (reader.Read()) queryDefault.Add(new JString((string)reader["key"], (string)reader["value"]));
                    }
                    var missing = new List<string>();
                    int added = 0;
                    int changed = 0;
                    var deleted = new List<string>();
                    foreach (var js in queryDefault)
                    {
                        if (!lf.Strings.Exists(x => x.Key == js.Key)) missing.Add(js.Key);
                    }
                    var toRemove = new List<JString>();
                    foreach (var js in lf.Strings)
                    {
                        if (!queryDefault.Exists(x => x.Key == js.Key) && lf.LangKey != defaultLangCode)
                        {
                            toRemove.Add(js);
                            continue;
                        }
                        if (!query.Exists(x => x.Key == js.Key)) added++;
                        else if (query.Find(x => x.Key == js.Key).Value != js.Value) changed++;
                        var enStr = queryDefault.Exists(x => x.Key == js.Key) ? queryDefault.Find(x => x.Key == js.Key).Value : "";
                        int extras = 0;
                        while (true)
                        {
                            string tag = "{" + extras + "}";
                            if (enStr.Contains(tag))
                            {
                                extras++;
                                if (!js.Value.Contains(tag))
                                {
                                    missing.Add($"{tag} in {js.Key}");
                                    if (!toRemove.Exists(x => x.Key == js.Key)) toRemove.Add(js);
                                }
                            }
                            else break;
                        }
                    }
                    foreach (var row in query) if (!lf.Strings.Exists(x => x.Key == row.Key)) deleted.Add(row.Key);
                    foreach (var js in toRemove) lf.Strings.Remove(js);
                    SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, Strings.UpdateLang,
                        ReplyMarkupMaker.InlineYesNo(yes, "yes", no, "no"), out sent, out var u, lf.LangKey, lf.Name,
                        added.ToString(), changed.ToString(), "\n" + deleted.ToStringList(), "\n" + missing.ToStringList());
                    try
                    {
                        OnCallbackQuery += cHandler;
                        mre.WaitOne();
                    }
                    finally
                    {
                        OnCallbackQuery -= cHandler;
                    }
                    if (permit)
                    {
                        foreach (var js in lf.Strings)
                        {
                            var par1 = new Dictionary<string, object>()
                            {
                                { "key", js.Key },
                                { "value", js.Value }
                            };
                            var cmd3 = new SQLiteCommand(sqliteConn);
                            cmd3.Parameters.AddRange(new SQLiteParameter[]
                            { new SQLiteParameter("key", js.Key), new SQLiteParameter("value", js.Value) });
                            if (query.Exists(x => x.Key == js.Key))
                            {
                                cmd3.CommandText = $"UPDATE '{lf.LangKey}' SET Value=@value WHERE Key=@key";
                            }
                            else
                            {
                                cmd3.CommandText = $"INSERT INTO '{lf.LangKey}' VALUES(@key, @value)";
                            }
                            cmd3.ExecuteNonQuery();
                        }
                        foreach (var js in toRemove)
                        {
                            var cmd3 = new SQLiteCommand($"DELETE FROM '{lf.LangKey}' WHERE key=@key", sqliteConn);
                            cmd3.Parameters.AddWithValue("key", js.Key);
                            cmd3.ExecuteNonQuery();
                        }
                        foreach (var key in deleted)
                        {
                            var cmd3 = new SQLiteCommand($"DELETE FROM '{lf.LangKey}' WHERE key=@key", sqliteConn);
                            cmd3.Parameters.AddWithValue("key", key);
                            cmd3.ExecuteNonQuery();
                        }
                    }
                }
                if (permit) EditLangMessage(msg.Chat.Id, msg.Chat.Id, sent.MessageId, Strings.LangUploaded, null, "", out var u1, out var u2);
                else EditLangMessage(msg.Chat.Id, msg.Chat.Id, sent.MessageId, Strings.UploadCancelled, null, "", out var u1, out var u2);
            }
            catch (Exception x)
            {
                string mess = $"{x.GetType().Name}\n{x.Message}\n";
                if (x.InnerException != null)
                    mess += $"{x.InnerException.Message}";
                SendLangMessage(msg.Chat.Id, Strings.ErrorOcurred, null,
                    mess);
            }
        }
        #endregion
        #endregion

        #region Game Flow
        #region Add player
        private static void AddPlayer(NodeGame game, NodePlayer player)
        {
            if (game.State != GameState.Joining)
            {
                SendLangMessage(game.GroupId, Strings.GameNotJoining);
                return;
            }
            if (game.Players.Exists(x => x.Id == player.Id))
            {
                SendLangMessage(game.GroupId, Strings.AlreadyInGame, null, player.Name);
                return;
            }
            if (!SendLangMessage(player.Id, game.GroupId, Strings.JoinedGamePM, null, game.GroupName))
            {
                SendLangMessage(game.GroupId, Strings.PmMe, ReplyMarkupMaker.InlineStartMe(Username), player.Name);
                return;
            }
            game.Players.Add(player);
            if (game.Players.Count > 1) SendLangMessage(game.GroupId, Strings.PlayerJoinedGame, null, player.Name);
            else SendLangMessage(game.GroupId, Strings.PlayerJoinedCantStart, null, player.Name);
            EditLangMessage(game.PlayerlistMessage.Chat.Id, game.GroupId, game.PlayerlistMessage.MessageId,
                Strings.PlayerList, null, "", out var u, out var u1, game.GetPlayerList());
        }
        #endregion
        #region Start game flow
        private static void StartGameFlow(object gameObject)
        {
            if (!(gameObject is NodeGame)) return;
            NodeGame game = (NodeGame)gameObject;
            #region Preparation phase
            SendLangMessage(game.GroupId, Strings.GameFlowStarted);
            game.State = GameState.Running;
            game.TotalPlayers = new List<NodePlayer>();
            foreach (var p in game.Players)
            {
                game.TotalPlayers.Add(p);
            }
            game.Players.Shuffle();
            for (int i = 0; i < game.Players.Count; i++)
            {
                int next = (i == game.Players.Count - 1) ? 0 : i + 1;
                SendLangMessage(game.Players[i].Id, game.GroupId, Strings.ChooseRoleFor, null, game.Players[next].Name);
            }
            ManualResetEvent mre = new ManualResetEvent(false);
            EventHandler<MessageEventArgs> eHandler = (sender, e) =>
            {
                if (!game.Players.Exists(x => x.Id == e.Message.From.Id)
                || e.Message.Type != MessageType.TextMessage || e.Message.Chat.Type != ChatType.Private) return;
                NodePlayer p = game.Players.Find(x => x.Id == e.Message.From.Id);
                int pIndex = game.Players.IndexOf(p);
                int nextIndex = (pIndex == game.Players.Count - 1) ? 0 : pIndex + 1;
                NodePlayer next = game.Players[nextIndex];
                if (game.RoleIdDict.ContainsKey(next.Id))
                {
                    SendLangMessage(p.Id, game.GroupId, Strings.AlreadySentRole, null, next.Name);
                }
                else
                {
                    game.RoleIdDict.Add(next.Id, e.Message.Text);
                    SendLangMessage(p.Id, game.GroupId, Strings.SetRole, null, next.Name, e.Message.Text);
                    if (game.DictFull()) mre.Set();
                }
            };
            try    //we don't wanna have that handler there if the thread is aborted, do we?
            {
                OnMessage += eHandler;
                mre.WaitOne();
            }
            finally
            {
                OnMessage -= eHandler;
            }
            SendLangMessage(game.GroupId, Strings.AllRolesSet);
            game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
            foreach (NodePlayer p in game.Players)
            {
                string message = "\n";
                foreach (var kvp in game.RoleIdDict)
                {
                    if (kvp.Key != p.Id) message += $"{game.Players.Find(x => x.Id == kvp.Key).Name}: {kvp.Value}\n";
                }
                SendLangMessage(p.Id, game.GroupId, Strings.RolesAre, null, message);
            }
            #endregion
            int turn = 0;
            NodePlayer atTurn = null;
            EventHandler<AfkEventArgs> afkGlobal = (sender, e) =>
            {
                if (e.Game.Id != game.Id || (atTurn != null && e.Player.Id == atTurn.Id)) return;
                var p = game.Players.Find(x => x.Id == e.Player.Id);
                p.GaveUp = true;
                SendLangMessage(game.GroupId, Strings.GaveUp, null, e.Player.Name, game.RoleIdDict[e.Player.Id]);
            };
            OnAfk += afkGlobal;
            #region Player turns
            while (true)
            {
                // do players turns until everything is finished, then break;
                if (turn >= game.Players.Count) turn = 0;
                atTurn = game.Players[turn];
                game.Turn = atTurn;
                if (atTurn.GaveUp)
                {
                    game.Players.Remove(atTurn);
                    if (game.Players.Count > 0) continue;
                    else break;
                }
                SendAndGetLangMessage(game.GroupId, game.GroupId, Strings.PlayerTurn, null, out Message sentGroupMessage, out string uselessS, atTurn.Name);
                #region Ask Question
                string sentMessageText = "";
                Message sentMessage = null;
                EventHandler<MessageEventArgs> qHandler = (sender, e) =>
                {
                    if (e.Message.From.Id != atTurn.Id || e.Message.Chat.Type != ChatType.Private) return;
                    game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
                    client.DeleteMessageAsync(sentMessage.Chat.Id, sentMessage.MessageId);
                    SendAndGetLangMessage(atTurn.Id, game.GroupId, Strings.QuestionReceived, null,
                                out sentMessage, out var u);
                    string yes = GetString(Strings.Yes, LangCode(game.GroupId));
                    string idk = GetString(Strings.Idk, LangCode(game.GroupId));
                    string no = GetString(Strings.No, LangCode(game.GroupId));
                    client.DeleteMessageAsync(sentGroupMessage.Chat.Id, sentGroupMessage.MessageId);
                    SendAndGetLangMessage(game.GroupId, game.GroupId, Strings.QuestionAsked,
                        ReplyMarkupMaker.InlineYesNoIdk(yes, $"yes@{game.Id}", no, $"no@{game.Id}", idk, $"idk@{game.Id}"),
                        out sentGroupMessage, out sentMessageText,
                        $"<b>{WebUtility.HtmlEncode(atTurn.Name)}</b>", $"<i>{WebUtility.HtmlEncode(e.Message.Text)}</i>");
                    mre.Set();
                };
                bool guess = false;
                bool endloop = false;
                EventHandler<AfkEventArgs> afkHandler = (sender, e) =>
                {
                    if (e.Game.Id != game.Id || e.Player.Id != atTurn.Id) return;
                    endloop = true;
                    SendLangMessage(game.GroupId, Strings.GaveUp, null, e.Player.Name, game.RoleIdDict[e.Player.Id]);
                    turn++;
                    game.Players.Remove(game.Players.Find(x => x.Id == e.Player.Id));
                    mre.Set();
                };
                #region Guess handler
                EventHandler<MessageEventArgs> guessHandler = (sender, e) =>
                {
                    if (e.Message.From.Id != atTurn.Id || e.Message.Chat.Type != ChatType.Private) return;
                    game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
                    client.DeleteMessageAsync(sentMessage.Chat.Id, sentMessage.MessageId);
                    SendAndGetLangMessage(atTurn.Id, game.GroupId, Strings.QuestionReceived, null, out sentMessage, out var u);
                    string yes = GetString(Strings.Yes, LangCode(game.GroupId));
                    string no = GetString(Strings.No, LangCode(game.GroupId));
                    client.DeleteMessageAsync(sentGroupMessage.Chat.Id, sentGroupMessage.MessageId);
                    SendAndGetLangMessage(game.GroupId, game.GroupId, Strings.PlayerGuessed,
                        ReplyMarkupMaker.InlineYesNo(yes, $"yes@{game.Id}", no, $"no@{game.Id}"),
                        out sentGroupMessage, out sentMessageText,
                        atTurn.Name, e.Message.Text);
                    mre.Set();
                };
                #endregion
                #region Callback Handler
                Dictionary<long, string> clicked = new Dictionary<long, string>();
                EventHandler<CallbackQueryEventArgs> c1Handler = (sender, e) =>
                {
                    if (!game.Players.Exists(x => x.Id == e.CallbackQuery.From.Id)
                    || (!e.CallbackQuery.Data.StartsWith("guess@") && !e.CallbackQuery.Data.StartsWith("giveup@"))
                    || e.CallbackQuery.Data.IndexOf('@') != e.CallbackQuery.Data.LastIndexOf('@')) return;
                    string answer = e.CallbackQuery.Data.Split('@')[0];
                    long gameId = Convert.ToInt64(e.CallbackQuery.Data.Split('@')[1]);
                    if (gameId != game.Id) return;
                    if (e.CallbackQuery.Message == null) return;
                    game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
                    Message cmsg = e.CallbackQuery.Message;
                    switch (answer)
                    {
                        case "guess":
                            #region Guess
                            if (clicked.ContainsKey(e.CallbackQuery.From.Id) && clicked[e.CallbackQuery.From.Id] == "guess")
                            {
                                guess = true;
                                mre.Set();
                                EditLangMessage(e.CallbackQuery.From.Id, game.GroupId, sentMessage.MessageId, Strings.PleaseGuess, null, "",
                                    out Message uselessM, out string uselessSS);
                                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                                client.EditMessageReplyMarkupAsync(cmsg.Chat.Id, cmsg.MessageId);
                            }
                            else
                            {
                                if (clicked.ContainsKey(e.CallbackQuery.From.Id)) clicked[e.CallbackQuery.From.Id] = "guess";
                                else clicked.Add(e.CallbackQuery.From.Id, "guess");
                                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, GetString(Strings.Confirm, game.GroupId));
                                Timer t = new Timer(x => clicked[e.CallbackQuery.From.Id] = null);
                            }
                            #endregion
                            break;
                        case "giveup":
                            #region Give Up
                            if (clicked.ContainsKey(e.CallbackQuery.From.Id) && clicked[e.CallbackQuery.From.Id] == "giveup")
                            {
                                endloop = true;
                                NodePlayer p = game.Players.Find(x => x.Id == e.CallbackQuery.From.Id);
                                SendLangMessage(game.GroupId, Strings.GaveUp, null,
                                    p.Name,
                                    game.RoleIdDict[e.CallbackQuery.From.Id]);
                                SendLangMessage(p.Id, game.GroupId, Strings.YouGaveUp);
                                client.EditMessageReplyMarkupAsync(sentMessage.Chat.Id, sentMessage.MessageId);
                                game.Players.Remove(p);
                                client.EditMessageReplyMarkupAsync(cmsg.Chat.Id, cmsg.MessageId);
                                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                                mre.Set();
                            }
                            else
                            {
                                if (clicked.ContainsKey(e.CallbackQuery.From.Id)) clicked[e.CallbackQuery.From.Id] = "giveup";
                                else clicked.Add(e.CallbackQuery.From.Id, "giveup");
                                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, GetString(Strings.Confirm, game.GroupId));
                                Timer t = new Timer(x => clicked[e.CallbackQuery.From.Id] = null);
                            }
                            #endregion
                            break;
                    }
                };
                #endregion
                string guess1 = GetString(Strings.Guess, LangCode(game.GroupId));
                string giveUp1 = GetString(Strings.GiveUp, LangCode(game.GroupId));
                SendAndGetLangMessage(atTurn.Id, game.GroupId, Strings.AskQuestion,
                    ReplyMarkupMaker.InlineGuessGiveUp(guess1, $"guess@{game.Id}", giveUp1, $"giveup@{game.Id}"),
                    out sentMessage, out string useless);
                mre.Reset();
                try
                {
                    OnMessage += qHandler;
                    OnCallbackQuery += c1Handler;
                    OnAfk += afkHandler;
                    mre.WaitOne();
                }
                finally
                {
                    OnMessage -= qHandler;
                    OnCallbackQuery -= c1Handler;
                    OnAfk -= afkHandler;
                }
                mre.Reset();
                game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
                if (guess)
                {
                    try
                    {
                        OnMessage += guessHandler;
                        OnAfk += afkHandler;
                        mre.WaitOne();
                    }
                    finally
                    {
                        OnMessage -= guessHandler;
                        OnAfk -= afkHandler;
                    }
                }
                #endregion
                bool breakMe = false;
                switch (game.Group.AutoEnd)
                {
                    case AutoEndSetting.None:
                        breakMe = game.Players.Count < 1;
                        break;
                    case AutoEndSetting.OnePlayerGuessed:
                        breakMe = game.Winner != null;
                        break;
                    case AutoEndSetting.OnePlayerLeft:
                        breakMe = game.Players.Count < 2;
                        break;
                }
                if (breakMe) break;
                if (endloop) continue;
                #region Answer Question
                EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
                {
                    if (!game.TotalPlayers.Exists(x => x.Id == e.CallbackQuery.From.Id)
                    || (!e.CallbackQuery.Data.StartsWith("yes@") && !e.CallbackQuery.Data.StartsWith("no@") && !e.CallbackQuery.Data.StartsWith("idk@"))
                    || e.CallbackQuery.Data.IndexOf('@') != e.CallbackQuery.Data.LastIndexOf('@')) return;
                    if (e.CallbackQuery.Message == null) return;
                    string answer = e.CallbackQuery.Data.Split('@')[0];
                    if (!long.TryParse(e.CallbackQuery.Data.Split('@')[1], out long gameId)) return;
                    if (gameId != game.Id) return;
                    if (e.CallbackQuery.From.Id == atTurn?.Id && game.GroupId != testingGroupId)
                    {
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, GetString(Strings.NoAnswerSelf, LangCode(game.GroupId)), showAlert: true);
                        return;
                    }
                    game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                    Message cmsg = e.CallbackQuery.Message;
                    string pname = game.TotalPlayers.Find(x => x.Id == e.CallbackQuery.From.Id).Name;
                    switch (answer)
                    {
                        case "yes":
                            EditLangMessage(game.GroupId, game.GroupId, cmsg.MessageId, Strings.AnsweredYes, null, sentMessageText + "\n",
                                out Message uselessM, out string uselessSS, pname);
                            EditLangMessage(sentMessage.Chat.Id, game.GroupId, sentMessage.MessageId,
                                Strings.AnsweredYes, null, "", out var u, out var u2, pname);
                            if (guess)
                            {
                                game.Players.Remove(atTurn);
                                game.TrySetWinner(atTurn);
                                SendLangMessage(game.GroupId, Strings.PlayerFinished, null,
                                    atTurn.Name);
                            }
                            break;
                        case "idk":
                            EditLangMessage(game.GroupId, game.GroupId, cmsg.MessageId, Strings.AnsweredIdk, null, sentMessageText + "\n",
                                out Message uselessM2, out string uselessS2, pname);
                            EditLangMessage(sentMessage.Chat.Id, game.GroupId, sentMessage.MessageId,
                                 Strings.AnsweredIdk, null, "", out var u3, out var u4, pname);
                            break;
                        case "no":
                            EditLangMessage(game.GroupId, game.GroupId, cmsg.MessageId, Strings.AnsweredNo, null, sentMessageText + "\n",
                                out Message uselessM1, out string uselessS1, pname);
                            EditLangMessage(sentMessage.Chat.Id, game.GroupId, sentMessage.MessageId,
                                 Strings.AnsweredNo, null, "", out var u5, out var u6, pname);
                            turn++;
                            break;
                    }
                    mre.Set();
                };
                mre.Reset();
                try
                {
                    OnCallbackQuery += cHandler;
                    OnAfk += afkHandler;
                    mre.WaitOne();
                }
                finally
                {
                    OnCallbackQuery -= cHandler;
                    OnAfk -= afkHandler;
                }
                game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
                #endregion
                switch (game.Group.AutoEnd)
                {
                    case AutoEndSetting.None:
                        breakMe = game.Players.Count < 1;
                        break;
                    case AutoEndSetting.OnePlayerGuessed:
                        breakMe = game.Winner != null;
                        break;
                    case AutoEndSetting.OnePlayerLeft:
                        breakMe = game.Players.Count < 2;
                        break;
                }
                if (breakMe) break;
            }
            #endregion
            #region Finish game
            OnAfk -= afkGlobal;
            game.InactivityTimer.Change(Timeout.Infinite, Timeout.Infinite);
            var cmd = new SQLiteCommand("DELETE FROM Games WHERE Id=@id", sqliteConn);
            cmd.Parameters.AddWithValue("id", game.Id);
            cmd.ExecuteNonQuery();
            long winnerId = game.Winner == null ? 0 : game.Winner.Id;
            string winnerName = game.Winner == null ? GetString(Strings.Nobody, LangCode(game.GroupId)) : game.Winner.Name;
            AddGameFinished(game.GroupId, winnerId, winnerName);
            SendLangMessage(game.GroupId, Strings.GameFinished, null, winnerName);
            SendLangMessage(game.GroupId, Strings.RolesWere, null, game.GetRolesAsString());
            NodeGames.Remove(game);
            GameFinished?.Invoke(null, new GameFinishedEventArgs(game));
            #endregion
        }
        #endregion
        #region Cancel game
        private static void CancelGame(NodeGame g)
        {
            var cmd = new SQLiteCommand("DELETE FROM Games WHERE Id=@id", sqliteConn);
            cmd.Parameters.AddWithValue("id", g.Id);
            cmd.ExecuteNonQuery();
            g.Thread?.Abort();
            g.InactivityTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            NodeGames.Remove(g);
            GameFinished?.Invoke(null, new GameFinishedEventArgs(g));
        }
        #endregion
        #endregion

        #region SQLite
        #region Execute SQLite Query
        private static string ExecuteSqlRaw(string commandText)
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

        /*private static List<List<string>> ExecuteSql(string commandText, Dictionary<string, object> parameters, bool checkPars = true)
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
                    else if (commandText.Contains("@") && checkPars) client.SendTextMessageAsync(Flom, $"Missing parameters in {commandText}");
                    try
                    {
                        using (var reader = comm.ExecuteReader())
                        {
                            if (!raw)
                            {
                                if (reader.RecordsAffected >= 0) r += $"_{reader.RecordsAffected} records affected_\n";
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    r += $"{reader.GetName(i)} ({reader.GetFieldType(i).Name})";
                                    if (i < reader.FieldCount - 1) r += " | ";
                                }
                                r += "\n\n";
                            }
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
        }*/
        #endregion
        #endregion
    }

    public enum AutoEndSetting
    {
        None = -1,
        OnePlayerGuessed = 0,
        OnePlayerLeft = 1
    }

    public enum NodeState
    {
        Primary,
        Stopping,
        Stopped
    }
}
