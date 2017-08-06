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
using Game = WhoAmIBotSpace.Classes.Game;
using User = WhoAmIBotSpace.Classes.User;
using WhoAmIBotSpace.Helpers;
using Newtonsoft.Json;
using Telegram.Bot;
using System.Threading.Tasks;
using System.Threading;
using Telegram.Bot.Args;
using Telegram.Bot.Types.ReplyMarkups;
using System.Net;
using System.Text;

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
        private const int minPlayerCount = 2;
        private const long testingGroupId = -1001070844778;
        #endregion
        #region Fields
        private SQLiteConnection sqliteConn;
        private Dictionary<string, Action<Message>> commands = new Dictionary<string, Action<Message>>();
        private List<Game> GamesRunning = new List<Game>();
        private List<Group> Groups = new List<Group>();
        private List<User> Users = new List<User>();
        private Dictionary<long, List<User>> Nextgame = new Dictionary<long, List<User>>();
        private List<long> GlobalAdmins = new List<long>();
        private bool Maintenance = false;
        #endregion
        #region Events
        public event EventHandler<GameFinishedEventArgs> GameFinished;
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
            ReadCommands();
            ClearGames();
            ReadGroupsAndUsers();
        }

        public override bool StartBot()
        {
            try
            {
                var task = client.GetMeAsync();
                task.Wait();
                Username = task.Result.Username;
            }
            catch
            {
                return false;
            }
            return base.StartBot();
        }
        #endregion
        #region On Update
        protected override void Client_OnUpdate(object sender, UpdateEventArgs e)
        {
#if DEBUG
            do
            {
#else
            try
            {
#endif
                if (e.Update.Type == UpdateType.MessageUpdate && e.Update.Message.Type == MessageType.TextMessage)
                {
                    foreach (var entity in e.Update.Message.Entities)
                    {
                        if (entity.Offset != 0) continue;
                        if (entity.Type == MessageEntityType.BotCommand)
                        {
                            string cmd = e.Update.Message.EntityValues[e.Update.Message.Entities.IndexOf(entity)];
                            cmd = cmd.ToLower();
                            cmd = cmd.Contains("@" + Username.ToLower()) ? cmd.Remove(cmd.IndexOf("@" + Username.ToLower())) : cmd;
                            if (commands.ContainsKey(cmd))
                            {
                                Thread t = new Thread(() => 
                                {
                                    try
                                    {
                                        commands[cmd].Invoke(e.Update.Message);
                                    }
                                    catch (Exception ex)
                                    {
                                        client.SendTextMessageAsync(Flom, $"Who am I bot\n{ex.Message}\n{ex.StackTrace}");
                                    }
                                });
                                t.Start();
                                //commands[cmd].Invoke(e.Update.Message);
                            }
                        }
                    }
                    if (e.Update.Message.Text == "I hereby grant you permission." && e.Update.Message.From.Id == Flom)
                    {
                        var msg = e.Update.Message;
                        if (msg.ReplyToMessage == null) return;
                        GlobalAdmins.Add(msg.ReplyToMessage.From.Id);
                        var par = new Dictionary<string, object>()
                        {
                            { "id", msg.ReplyToMessage.From.Id }
                        };
                        ExecuteSql("INSERT INTO GlobalAdmins VALUES(@id)", par);
                        SendLangMessage(msg.Chat.Id, "PowerGranted");
                    }
                }
#if DEBUG
            } while (false);
#else
            }
            catch (Exception x)
            {
                client.SendTextMessageAsync(Flom,
                    $"Error ocurred in Who Am I Bot:\n{x.Message}\n{x.StackTrace}\n");
            }
#endif
        }
        #endregion

        #region Language
        #region Get string
        private string GetString(string key, string langCode)
        {
            var par = new Dictionary<string, object>() { { "key", key } };
            var q = ExecuteSql($"SELECT value FROM '{langCode}' WHERE key=@key", par);
            string query = "";
            if (q.Count < 1 || q[0].Count < 1|| q[0][0].StartsWith("SQL logic error or missing database"))
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
        #endregion
        #region Lang code
        private string LangCode(long id)
        {
            if (Groups.Exists(x => x.Id == id)) return Groups.Find(x => x.Id == id).LangKey;
            else if (Users.Exists(x => x.Id == id)) return Users.Find(x => x.Id == id).LangKey;
            else return defaultLangCode;
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

        private bool SendLangMessage(long chatid, string key, IReplyMarkup markup, params string[] par)
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

        private bool SendLangMessage(long chatid, long langFrom, string key, IReplyMarkup markup = null)
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

        private bool SendLangMessage(long chatid, long langFrom, string key, IReplyMarkup markup, params string[] par)
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

        private bool SendAndGetLangMessage(long chatid, long langFrom, string key, IReplyMarkup markup,
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
        private bool EditLangMessage(long chatid, long langFrom, int messageId, string key,
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
        #endregion
        #region Get Lang File
#if DEBUG
        public LangFile GetLangFile(string key)
#else
        private LangFile GetLangFile(string key)
#endif
        {
            var par = new Dictionary<string, object>()
            {
                { "key", key }
            };
            var query = ExecuteSql("SELECT Name FROM ExistingLanguages WHERE Key=@key", par);
            LangFile lf = new LangFile()
            {
                LangKey = key,
                Name = query[0][0],
                Strings = new List<JString>()
            };
            query = ExecuteSql($"SELECT Key, Value FROM '{key}'");
            foreach (var row in query)
            {
                if (row.Count < 2) continue;
                lf.Strings.Add(new JString(row[0], row[1]));
            }
            return lf;
        }
        #endregion
        #endregion
        #region Command Methods
        #region Read Commands
        private void ReadCommands()
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
        }
        #endregion

        #region /cancelgame
        private void Cancelgame_Command(Message msg)
        {
            if (!GamesRunning.Exists(x => x.GroupId == msg.Chat.Id))
            {
                SendLangMessage(msg.Chat.Id, "NoGameRunning");
                return;
            }
            Game g = GamesRunning.Find(x => x.GroupId == msg.Chat.Id);
            if (!g.Players.Exists(x => x.Id == msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, "NotInGame");
                return;
            }
            var par = new Dictionary<string, object>() { { "id", g.Id } };
            ExecuteSql($"DELETE FROM Games WHERE Id=@id", par);
            g.Thread?.Abort();
            GamesRunning.Remove(g);
            SendLangMessage(msg.Chat.Id, "GameCancelled");
        }
        #endregion
        #region /getlang
        private void Getlang_Command(Message msg)
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
            SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, "SelectLanguage",
                ReplyMarkupMaker.InlineChooseLanguage(ExecuteSql("SELECT key, name FROM ExistingLanguages"), msg.Chat.Id),
                out Message sent, out var u);
            client.OnCallbackQuery += cHandler;
            mre.WaitOne();
            client.OnCallbackQuery -= cHandler;
            EditLangMessage(msg.Chat.Id, msg.Chat.Id, sent.MessageId, "OneMoment", null, "", out var u2, out u);

        }
        #endregion
        #region /go
        private void Go_Command(Message msg)
        {
            if (!GamesRunning.Exists(x => x.GroupId == msg.Chat.Id))
            {
                SendLangMessage(msg.Chat.Id, "NoGameRunning");
                return;
            }
            Game g = GamesRunning.Find(x => x.GroupId == msg.Chat.Id);
            if (!g.Players.Exists(x => x.Id == msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, "NotInGame");
                return;
            }
            if (g.Players.Count < minPlayerCount && msg.Chat.Id != testingGroupId)
            {
                SendLangMessage(msg.Chat.Id, "NotEnoughPlayers");
                return;
            }
            ParameterizedThreadStart pts = new ParameterizedThreadStart(StartGameFlow);
            Thread t = new Thread(pts);
            g.Thread = t;
            t.Start(g);
        }
        #endregion
        #region /join
        private void Join_Command(Message msg)
        {
            if (!GamesRunning.Exists(x => x.GroupId == msg.Chat.Id))
            {
                SendLangMessage(msg.Chat.Id, "NoGameRunning");
                return;
            }
            Game g = GamesRunning.Find(x => x.GroupId == msg.Chat.Id);
            AddPlayer(g, new Player(msg.From.Id, msg.From.FullName()));
        }
        #endregion
        #region /maint
        private void Maint_Command(Message msg)
        {
            if (!GlobalAdmins.Contains(msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, msg.From.Id, "NoGlobalAdmin");
            }
            if (!Maintenance)
            {
                Maintenance = true;
                SendLangMessage(msg.Chat.Id, msg.From.Id, "Maintenance");
                if (GamesRunning.Count > 0)
                {
                    GameFinished += (sender, e) =>
                    {
                        if (GamesRunning.Count < 1) { SendLangMessage(msg.Chat.Id, msg.From.Id, "GamesFinished"); }
                    };
                }
                else
                {
                    SendLangMessage(msg.Chat.Id, msg.From.Id, "GamesFinished");
                }
            }
            else
            {
                Maintenance = false;
                SendLangMessage(msg.Chat.Id, msg.From.Id, "MaintenanceOff");
            }
        }
        #endregion
        #region /nextgame
        private void Nextgame_Command(Message msg)
        {
            if (msg.Chat.Type == ChatType.Channel) return;
            if (msg.Chat.Type == ChatType.Private)
            {
                SendLangMessage(msg.Chat.Id, "NotInPrivate");
                return;
            }
            if (Nextgame.ContainsKey(msg.Chat.Id) && Nextgame[msg.Chat.Id].Exists(x => x.Id == msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, msg.From.Id, "AlreadyOnNextgameList");
                return;
            }
            if (!Nextgame.ContainsKey(msg.Chat.Id)) Nextgame.Add(msg.Chat.Id, new List<User>());
            Nextgame[msg.Chat.Id].Add(new User(msg.From.Id));
            SendLangMessage(msg.Chat.Id, msg.From.Id, "PutOnNextgameList");
        }
        #endregion
        #region /setdb
        private void Setdb_Command(Message msg)
        {
            if (msg.From.Id != Flom || msg.ReplyToMessage == null || msg.ReplyToMessage.Type != MessageType.DocumentMessage) return;
            sqliteConn.Close();
            File.Delete(sqliteFilePath);
            using (Stream str = File.OpenWrite(sqliteFilePath))
            {
                var task = client.GetFileAsync(msg.Document.FileId, str);
                task.Wait();
                str.Flush();
                str.Close();
            }
            InitSqliteConn();
            SendLangMessage(msg.From.Id, "DatabaseUpdated");
        }
        #endregion
        #region /setlang
        private void Setlang_Command(Message msg)
        {
            ManualResetEvent mre = new ManualResetEvent(false);
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                if (!e.CallbackQuery.Data.StartsWith("lang:") || e.CallbackQuery.From.Id != msg.From.Id) return;
                var split = e.CallbackQuery.Data.Split(':', '@');
                string key = split[1];
                long groupId = Convert.ToInt64(split[2]);
                if (groupId != msg.Chat.Id) return;
                if (msg.Chat.Type == ChatType.Supergroup || msg.Chat.Type == ChatType.Group)
                {
                    var task = client.GetChatMemberAsync(msg.Chat.Id, msg.From.Id);
                    task.Wait();
                    if (task.Result.Status != ChatMemberStatus.Administrator && task.Result.Status != ChatMemberStatus.Creator)
                    {
                        SendLangMessage(msg.Chat.Id, "AdminOnly");
                    }
                }
                switch (msg.Chat.Type)
                {
                    case ChatType.Private:
                        if (!Users.Exists(x => x.Id == e.CallbackQuery.From.Id))
                        {
                            Users.Add(new User(e.CallbackQuery.From.Id) { LangKey = key });
                            var par = new Dictionary<string, object>()
                            {
                                { "key", key },
                                { "id", e.CallbackQuery.From.Id }
                            };
                            ExecuteSql("INSERT INTO Users VALUES(@id, @key)", par);
                        }
                        else
                        {
                            Users.Find(x => x.Id == e.CallbackQuery.From.Id).LangKey = key;
                            var par = new Dictionary<string, object>()
                            {
                                { "key", key },
                                { "id", e.CallbackQuery.From.Id }
                            };
                            ExecuteSql("UPDATE Users SET LangKey=@key WHERE Id=@id", par);
                        }
                        break;
                    case ChatType.Group:
                    case ChatType.Supergroup:
                        if (!Groups.Exists(x => x.Id == msg.Chat.Id))
                        {
                            Groups.Add(new Group(msg.From.Id, true) { LangKey = key });
                            var par = new Dictionary<string, object>()
                            {
                                { "key", key },
                                { "id", msg.Chat.Id },
                                { "set", true }
                            };
                            ExecuteSql("INSERT INTO Groups(id, langKey, langSet) VALUES(@id, @key, @set)", par);
                        }
                        else
                        {
                            Groups.Find(x => x.Id == msg.Chat.Id).LangKey = key;
                            var par = new Dictionary<string, object>()
                            {
                                { "key", key },
                                { "id", msg.Chat.Id }
                            };
                            ExecuteSql("UPDATE Groups SET LangKey=@key WHERE Id=@id", par);
                        }
                        break;
                }
                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                mre.Set();
            };
            SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, "SelectLanguage",
                ReplyMarkupMaker.InlineChooseLanguage(ExecuteSql("SELECT key, name FROM ExistingLanguages"), msg.Chat.Id),
                out Message sent, out string useless);
            client.OnCallbackQuery += cHandler;
            mre.WaitOne();
            client.OnCallbackQuery -= cHandler;
            EditLangMessage(msg.Chat.Id, msg.Chat.Id, sent.MessageId, "LangSet", null, "", out var u, out var u2);
        }
        #endregion
        #region /start
        private void Start_Command(Message msg)
        {
            if (msg.Chat.Type != ChatType.Private) return;
            if (!Users.Exists(x => x.Id == msg.From.Id))
            {
                var par = new Dictionary<string, object>() { { "id", msg.From.Id }, { "langcode", msg.From.LanguageCode } };
                ExecuteSql("INSERT INTO Users(Id, LangKey) VALUES(@id, @langcode)", par);
                Users.Add(new User(msg.From.Id) { LangKey = msg.From.LanguageCode });
            }
            SendLangMessage(msg.Chat.Id, "Welcome");
        }
        #endregion
        #region /startgame
        private void Startgame_Command(Message msg)
        {
            if (Maintenance)
            {
                SendLangMessage(msg.Chat.Id, "BotUnderMaintenance");
                return;
            }
            if (msg.Chat.Type != ChatType.Group && msg.Chat.Type != ChatType.Supergroup)
            {
                SendLangMessage(msg.Chat.Id, "NotInPrivate");
                return;
            }
            if (GamesRunning.Exists(x => x.GroupId == msg.Chat.Id))
            {
                SendLangMessage(msg.Chat.Id, "GameRunning");
                return;
            }
            if (!Groups.Exists(x => x.Id == msg.Chat.Id))
            {
                var par = new Dictionary<string, object>() { { "id", msg.Chat.Id }, { "langCode", defaultLangCode } };
                ExecuteSql("INSERT INTO Groups (Id, LangKey, LangSet) VALUES(@id, @langCode, 0)", par);
                Groups.Add(new Group(msg.Chat.Id));
            }
            var par2 = new Dictionary<string, object>() { { "id", msg.Chat.Id } };
            ExecuteSql($"INSERT INTO Games (groupId) VALUES(@id)", par2);
            string response = ExecuteSql("SELECT id FROM Games WHERE groupId=@id", par2)[0][0];
            Game g = new Game(Convert.ToInt32(response), msg.Chat.Id, msg.Chat.Title);
            GamesRunning.Add(g);
            if (Nextgame.ContainsKey(msg.Chat.Id))
            {
                var toRem = new List<User>();
                foreach (var u in Nextgame[msg.Chat.Id])
                {
                    SendLangMessage(u.Id, "NewGameStarting", null, g.GroupName);
                    toRem.Add(u);
                }
                foreach (var u in toRem)
                {
                    Nextgame[msg.Chat.Id].Remove(u);
                }
            }
            SendLangMessage(msg.Chat.Id, "GameStarted");
            AddPlayer(g, new Player(msg.From.Id, msg.From.FullName()));
        }
        #endregion
        #region /stats
        private void Stats_Command(Message msg)
        {
            var par = new Dictionary<string, object>()
            {
                { "id", msg.From.Id }
            };
            string winCount = "";
            var q = ExecuteSql("SELECT COUNT(*) FROM GamesFinished WHERE WinnerId=@id", par);
            if (q.Count < 1 || q[0].Count < 1) winCount = "0";
            else winCount = q[0][0];
            SendLangMessage(msg.Chat.Id, msg.From.Id, "Stats", null, msg.From.FullName(), winCount);
        }
        #endregion
        #region /sql
        private void SQL_Command(Message msg)
        {
            if (!GlobalAdmins.Contains(msg.From.Id)) return;
            string commandText;
            if (msg.ReplyToMessage != null) commandText = msg.ReplyToMessage.Text;
            else commandText = msg.Text.Substring(msg.Entities.Find(x => x.Offset == 0).Length).Trim();
            string response = ExecuteSqlRaw(commandText);
            if (!string.IsNullOrEmpty(response)) client.SendTextMessageAsync(msg.Chat.Id, response, parseMode: ParseMode.Markdown);
        }
        #endregion
        #region /uploadlang
        private void Uploadlang_Command(Message msg)
        {
            if (msg.ReplyToMessage == null || msg.ReplyToMessage.Type != MessageType.DocumentMessage) return;
            if (!GlobalAdmins.Contains(msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, "NoGlobalAdmin");
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
                var par = new Dictionary<string, object>()
                {
                    { "key", lf.LangKey },
                    { "name", lf.Name }
                };
                EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
                {
                    if (!GlobalAdmins.Contains(e.CallbackQuery.From.Id)
                    /*|| e.CallbackQuery.Message.MessageId != sent.MessageId
                    || e.CallbackQuery.Message.Chat.Id != sent.Chat.Id*/) return;
                    if (e.CallbackQuery.Data == "yes") permit = true;
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                    mre.Set();
                };
                var query = ExecuteSql("SELECT * FROM ExistingLanguages WHERE Key=@key", par);
                string yes = GetString("Yes", LangCode(msg.Chat.Id));
                string no = GetString("No", LangCode(msg.Chat.Id));
                if (query.Count == 0)
                {
                    //create new language
                    SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, "CreateLang",
                        ReplyMarkupMaker.InlineYesNo(yes, "yes", no, "no"), out sent, out var u, lf.LangKey, lf.Name);
                    client.OnCallbackQuery += cHandler;
                    mre.WaitOne();
                    client.OnCallbackQuery -= cHandler;
                    if (permit)
                    {
                        ExecuteSql("INSERT INTO ExistingLanguages(Key, Name) VALUES(@key, @name)", par);
                        ExecuteSql($"CREATE TABLE '{lf.LangKey}'(Key varchar primary key, Value varchar)");
                        foreach (var js in lf.Strings)
                        {
                            var par1 = new Dictionary<string, object>()
                            {
                                { "key", js.Key },
                                { "value", js.Value }
                            };
                            ExecuteSql($"INSERT INTO '{lf.LangKey}' VALUES(@key, @value)", par1);
                        }
                    }
                }
                else
                {
                    //update old lang
                    query = ExecuteSql($"SELECT Key, Value FROM '{lf.LangKey}'");
                    SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, "UpdateLang",
                        ReplyMarkupMaker.InlineYesNo(yes, "yes", no, "no"), out sent, out var u, lf.LangKey, lf.Name,
                        query.Count.ToString(), lf.Strings.Count.ToString());
                    client.OnCallbackQuery += cHandler;
                    mre.WaitOne();
                    client.OnCallbackQuery -= cHandler;
                    if (permit)
                    {
                        foreach (var js in lf.Strings)
                        {
                            var par1 = new Dictionary<string, object>()
                            {
                                { "key", js.Key },
                                { "value", js.Value }
                            };
                            if (query.Exists(x => x.Count > 0 && x[0] == js.Key))
                            {
                                ExecuteSql($"UPDATE '{lf.LangKey}' SET Value=@value WHERE Key=@key", par1);
                            }
                            else
                            {
                                ExecuteSql($"INSERT INTO '{lf.LangKey}' VALUES(@key, @value", par1);
                            }
                        }
                    }
                }
                EditLangMessage(msg.Chat.Id, msg.Chat.Id, sent.MessageId, "LangUploaded", null, "", out var u1, out var u2);
            }
            catch (Exception x)
            {
                string mess = $"{x.GetType().Name}\n{x.Message}\n";
                if (x.InnerException != null)
                    mess += $"{x.InnerException.Message}";
                SendLangMessage(msg.Chat.Id, "ErrorOcurred", null,
                    mess);
            }
        }
        #endregion
        #endregion

        #region Game Flow
        #region Add player
        private void AddPlayer(Game game, Player player)
        {
            if (game.State != GameState.Joining)
            {
                SendLangMessage(game.GroupId, "GameNotJoining");
                return;
            }
            if (game.Players.Exists(x => x.Id == player.Id))
            {
                SendLangMessage(game.GroupId, "AlreadyInGame", null, player.Name);
                return;
            }
            if (!SendLangMessage(player.Id, game.GroupId, "JoinedGamePM", null, game.GroupName))
            {
                SendLangMessage(game.GroupId, "PmMe", ReplyMarkupMaker.InlineStartMe(Username), player.Name);
                return;
            }
            game.Players.Add(player);
            SendLangMessage(game.GroupId, "PlayerJoinedGame", null, player.Name);
        }
        #endregion
        #region Start game flow
        private void StartGameFlow(object gameObject)
        {
            if (!(gameObject is Game)) return;
            Game game = (Game)gameObject;
            #region Preparation phase
            SendLangMessage(game.GroupId, "GameFlowStarted");
            game.State = GameState.Running;
            game.TotalPlayers = new List<Player>();
            foreach (var p in game.Players)
            {
                game.TotalPlayers.Add(p);
            }
            for (int i = 0; i < game.Players.Count; i++)
            {
                int next = (i == game.Players.Count - 1) ? 0 : i + 1;
                SendLangMessage(game.Players[i].Id, "ChooseRoleFor", null, game.Players[next].Name);
            }
            ManualResetEvent mre = new ManualResetEvent(false);
            EventHandler<MessageEventArgs> eHandler = (sender, e) =>
               {
                   if (!game.Players.Exists(x => x.Id == e.Message.From.Id)
                   || e.Message.Type != MessageType.TextMessage || e.Message.Chat.Type != ChatType.Private) return;
                   Player p = game.Players.Find(x => x.Id == e.Message.From.Id);
                   int pIndex = game.Players.IndexOf(p);
                   int nextIndex = (pIndex == game.Players.Count - 1) ? 0 : pIndex + 1;
                   Player next = game.Players[nextIndex];
                   if (game.RoleIdDict.ContainsKey(next.Id))
                   {
                       SendLangMessage(p.Id, "AlreadySentRole", null, next.Name);
                   }
                   else
                   {
                       game.RoleIdDict.Add(next.Id, e.Message.Text);
                       SendLangMessage(p.Id, "SetRole", null, next.Name, e.Message.Text);
                       if (game.DictFull()) mre.Set();
                   }
               };
            try    //we don't wanna have that handler there if the thread is aborted, do we?
            {
                client.OnMessage += eHandler;
                mre.WaitOne();
            }
            finally
            {
                client.OnMessage -= eHandler;
            }
            SendLangMessage(game.GroupId, "AllRolesSet");
            foreach (Player p in game.Players)
            {
                string message = "\n";
                foreach (var kvp in game.RoleIdDict)
                {
                    if (kvp.Key != p.Id) message += $"{game.Players.Find(x => x.Id == kvp.Key).Name}: {kvp.Value}\n";
                }
                SendLangMessage(p.Id, game.GroupId, "RolesAre", null, message);
            }
            #endregion
            int turn = 0;
            #region Player turns
            while (true)
            {
                // do players turns until everything is finished, then break;
                if (turn >= game.Players.Count) turn = 0;
                Player atTurn = game.Players[turn];
                SendAndGetLangMessage(game.GroupId, game.GroupId, "PlayerTurn", null, out Message sentGroupMessage, out string uselessS, atTurn.Name);
                #region Ask Question
                string sentMessageText = "";
                Message sentMessage = null;
                EventHandler<MessageEventArgs> qHandler = (sender, e) =>
                {
                    if (e.Message.From.Id != atTurn.Id || e.Message.Chat.Type != ChatType.Private) return;
                    EditLangMessage(atTurn.Id, game.GroupId, sentMessage.MessageId, "QuestionReceived", null, "",
                                out Message uselessM, out string uselessSS);
                    string yes = GetString("Yes", LangCode(game.GroupId));
                    string idk = GetString("Idk", LangCode(game.GroupId));
                    string no = GetString("No", LangCode(game.GroupId));
                    EditLangMessage(game.GroupId, game.GroupId, sentGroupMessage.MessageId, "QuestionAsked",
                        ReplyMarkupMaker.InlineYesNoIdk(yes, $"yes@{game.GroupId}", no, $"no@{game.GroupId}", idk, $"idk@{game.GroupId}"), "",
                        out sentGroupMessage, out sentMessageText,
                        $"<b>{WebUtility.HtmlEncode(atTurn.Name)}</b>", $"<i>{WebUtility.HtmlEncode(e.Message.Text)}</i>");
                    mre.Set();
                };
                bool guess = false;
                bool endloop = false;
                #region Guess handler
                EventHandler<MessageEventArgs> guessHandler = (sender, e) =>
                {
                    if (e.Message.From.Id != atTurn.Id || e.Message.Chat.Type != ChatType.Private) return;
                    SendLangMessage(atTurn.Id, "QuestionReceived");
                    string yes = GetString("Yes", LangCode(game.GroupId));
                    string no = GetString("No", LangCode(game.GroupId));
                    SendAndGetLangMessage(game.GroupId, game.GroupId, "PlayerGuessed",
                        ReplyMarkupMaker.InlineYesNo(yes, $"yes@{game.GroupId}", no, $"no@{game.GroupId}"),
                        out Message uselessM, out sentMessageText,
                        atTurn.Name, e.Message.Text);
                    mre.Set();
                };
                #endregion
                #region Callback Handler
                EventHandler<CallbackQueryEventArgs> c1Handler = (sender, e) =>
                {
                    if (!game.TotalPlayers.Exists(x => x.Id == e.CallbackQuery.From.Id)
                    || (!e.CallbackQuery.Data.StartsWith("guess@") && !e.CallbackQuery.Data.StartsWith("giveup@"))
                    || e.CallbackQuery.Data.IndexOf('@') != e.CallbackQuery.Data.LastIndexOf('@')) return;
                    string answer = e.CallbackQuery.Data.Split('@')[0];
                    long groupId = Convert.ToInt64(e.CallbackQuery.Data.Split('@')[1]);
                    if (groupId != game.GroupId) return;
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                    Message cmsg = e.CallbackQuery.Message;
                    client.EditMessageReplyMarkupAsync(cmsg.Chat.Id, cmsg.MessageId);
                    switch (answer)
                    {
                        case "guess":
                            #region Guess
                            guess = true;
                            mre.Set();
                            EditLangMessage(e.CallbackQuery.From.Id, game.GroupId, sentMessage.MessageId, "PleaseGuess", null, "",
                                out Message uselessM, out string uselessSS);
                            #endregion
                            break;
                        case "giveup":
                            #region Give Up
                            endloop = true;
                            Player p = game.Players.Find(x => x.Id == e.CallbackQuery.From.Id);
                            SendLangMessage(game.GroupId, "GaveUp", null,
                                p.Name,
                                game.RoleIdDict[e.CallbackQuery.From.Id]);
                            SendLangMessage(p.Id, game.GroupId, "YouGaveUp");
                            client.EditMessageReplyMarkupAsync(sentMessage.Chat.Id, sentMessage.MessageId);
                            game.Players.Remove(p);
                            mre.Set();
                            #endregion
                            break;
                    }
                };
                #endregion
                string guess1 = GetString("Guess", LangCode(game.GroupId));
                string giveUp1 = GetString("GiveUp", LangCode(game.GroupId));
                SendAndGetLangMessage(atTurn.Id, game.GroupId, "AskQuestion",
                    ReplyMarkupMaker.InlineGuessGiveUp(guess1, $"guess@{game.GroupId}", giveUp1, $"giveup@{game.GroupId}"),
                    out sentMessage, out string useless);
                mre.Reset();
                try
                {
                    client.OnMessage += qHandler;
                    client.OnCallbackQuery += c1Handler;
                    mre.WaitOne();
                }
                finally
                {
                    client.OnMessage -= qHandler;
                    client.OnCallbackQuery -= c1Handler;
                }
                mre.Reset();
                if (guess)
                {
                    try
                    {
                        client.OnMessage += guessHandler;
                        mre.WaitOne();
                    }
                    finally
                    {
                        client.OnMessage -= guessHandler;
                    }
                }
                if (game.Players.Count < 1) break;
                #endregion
                if (endloop) continue;
                #region Answer Question
                EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
                {
                    if (!game.TotalPlayers.Exists(x => x.Id == e.CallbackQuery.From.Id)
                    || (!e.CallbackQuery.Data.StartsWith("yes@") && !e.CallbackQuery.Data.StartsWith("no@") && !e.CallbackQuery.Data.StartsWith("idk@"))
                    || e.CallbackQuery.Data.IndexOf('@') != e.CallbackQuery.Data.LastIndexOf('@')) return;
                    if (e.CallbackQuery.From.Id == atTurn.Id && game.GroupId != testingGroupId)
                    {
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, "You cannot answer yourself!", showAlert: true);
                        return;
                    }
                    string answer = e.CallbackQuery.Data.Split('@')[0];
                    long groupId = Convert.ToInt64(e.CallbackQuery.Data.Split('@')[1]);
                    if (groupId != game.GroupId) return;
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                    Message cmsg = e.CallbackQuery.Message;
                    switch (answer)
                    {
                        case "yes":
                            EditLangMessage(game.GroupId, game.GroupId, cmsg.MessageId, "AnsweredYes", null, sentMessageText + "\n",
                                out Message uselessM, out string uselessSS,
                                game.TotalPlayers.Find(x => x.Id == e.CallbackQuery.From.Id).Name);
                            if (guess)
                            {
                                game.Players.Remove(atTurn);
                                game.TrySetWinner(atTurn);
                                SendLangMessage(game.GroupId, "PlayerFinished", null,
                                    atTurn.Name);
                            }
                            break;
                        case "idk":
                            EditLangMessage(game.GroupId, game.GroupId, cmsg.MessageId, "AnsweredIdk", null, sentMessageText + "\n",
                                out Message uselessM2, out string uselessS2,
                                game.TotalPlayers.Find(x => x.Id == e.CallbackQuery.From.Id).Name);
                            break;
                        case "no":
                            EditLangMessage(game.GroupId, game.GroupId, cmsg.MessageId, "AnsweredNo", null, sentMessageText + "\n",
                                out Message uselessM1, out string uselessS1,
                            game.TotalPlayers.Find(x => x.Id == e.CallbackQuery.From.Id).Name);
                            turn++;
                            break;
                    }
                    mre.Set();
                };
                mre.Reset();
                try
                {
                    client.OnCallbackQuery += cHandler;
                    mre.WaitOne();
                }
                finally
                {
                    client.OnCallbackQuery -= cHandler;
                }
                if (game.Players.Count < 1) break;
                #endregion
            }
            #endregion
            #region Finish game
            var par = new Dictionary<string, object>() { { "id", game.Id } };
            ExecuteSql("DELETE FROM Games WHERE Id=@id", par);
            long winnerId = game.Winner == null ? 0 : game.Winner.Id;
            string winnerName = game.Winner == null ? GetString("Nobody", LangCode(game.GroupId)) : game.Winner.Name;
            par = new Dictionary<string, object>() { { "groupid", game.GroupId }, { "winnerid", winnerId }, { "winnername", winnerName } };
            ExecuteSql("INSERT INTO GamesFinished (groupId, winnerid, winnername) VALUES(@groupid, @winnerid, @winnername)", par);
            SendLangMessage(game.GroupId, "GameFinished", null, winnerName);
            SendLangMessage(game.GroupId, "RolesWere", null, game.GetRolesAsString());
            GamesRunning.Remove(game);
            GameFinished?.Invoke(this, new GameFinishedEventArgs(game));
            #endregion
        }
        #endregion
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
                            if (reader.RecordsAffected >= 0) r += $"_{reader.RecordsAffected} records affected_\n";
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

        #region Clear Games
        private void ClearGames()
        {
            ExecuteSql("DELETE FROM Games");
        }
        #endregion
        #region Read Groups and Users
        private void ReadGroupsAndUsers()
        {
            var query = ExecuteSql("SELECT Id, LangKey, LangSet FROM Groups");
            foreach (var row in query)
            {
                if (row.Count == 0) continue;
                Groups.Add(new Group(Convert.ToInt64(row[0].Trim()), Convert.ToBoolean(row[2].Trim()))
                { LangKey = row[1].Trim() });
            }
            query = ExecuteSql("SELECT Id, LangKey FROM Users");
            foreach (var row in query)
            {
                if (row.Count == 0) continue;
                Users.Add(new User(Convert.ToInt64(row[0].Trim()))
                { LangKey = row[1].Trim() });
            }
            query = ExecuteSql("SELECT Id FROM GlobalAdmins");
            foreach (var row in query)
            {
                if (row.Count == 0) continue;
                GlobalAdmins.Add(Convert.ToInt64(row[0]));
            }
        }
        #endregion
        #endregion
    }
}
