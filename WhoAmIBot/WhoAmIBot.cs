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
        #endregion
        #region Fields
        private SQLiteConnection sqliteConn;
        private Dictionary<string, Action<Message>> commands = new Dictionary<string, Action<Message>>();
        private List<Game> GamesRunning = new List<Game>();
        private List<Group> Groups = new List<Group>();
        private List<User> Users = new List<User>();
        #endregion

        #region Constructors and FlomBot stuff
        public WhoAmIBot(string token) : base(token)
        {
            if (!Directory.Exists(baseFilePath)) Directory.CreateDirectory(baseFilePath);
            if (!File.Exists(sqliteFilePath)) SQLiteConnection.CreateFile(sqliteFilePath);
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
#else
            try
            {
#endif
            if (e.Update.Type == UpdateType.MessageUpdate && e.Update.Message.Type == MessageType.TextMessage)
            {
                foreach (var entity in e.Update.Message.Entities)
                {
                    if (entity.Type == MessageEntityType.BotCommand)
                    {
                        string cmd = e.Update.Message.EntityValues[e.Update.Message.Entities.IndexOf(entity)];
                        cmd = cmd.Contains("@" + Username) ? cmd.Remove(cmd.IndexOf('@')) : cmd;
                        if (commands.ContainsKey(cmd))
                        {
                            commands[cmd].Invoke(e.Update.Message);
                        }
                    }
                }
            }
#if DEBUG
#else
            }
            catch (Exception x)
            {
                client.SendTextMessageAsync(Flom, 
                    $"Error ocurred in Who Am I Bot:\n{x.Message}\n{x.StackTrace}\n" +
                    $"{x.InnerException?.Message}\n{x.InnerException?.StackTrace}");
            }
#endif
        }
        #endregion

        #region Language
        #region Get string
        private string GetString(string key, string langCode)
        {
            string query = ExecuteSql($"SELECT value FROM '{langCode}' WHERE key='{key}'").Trim();
            if (query.StartsWith("SQL logic error or missing database") || string.IsNullOrWhiteSpace(query))
                query = ExecuteSql($"SELECT value FROM '{defaultLangCode}' WHERE key='{key}'").Trim();
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
        private bool SendLangMessage(long chatid, string key)
        {
            try
            {
                var task = client.SendTextMessageAsync(chatid, GetString(key, LangCode(chatid)));
                task.Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private bool SendLangMessage(long chatid, string key, params string[] par)
        {
            try
            {
                string toSend = GetString(key, LangCode(chatid));
                for (int i = 0; i < par.Length; i++)
                {
                    toSend = toSend.Replace("{" + i + "}", par[i]);
                }
                var task = client.SendTextMessageAsync(chatid, toSend);
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
            ExecuteSql($"DELETE FROM Games WHERE Id={g.Id}");
            GamesRunning.Remove(g);
            SendLangMessage(msg.Chat.Id, "GameCancelled");
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
            if (g.Players.Count < minPlayerCount)
            {
                SendLangMessage(msg.Chat.Id, "NotEnoughPlayers");
                return;
            }
            ParameterizedThreadStart pts = new ParameterizedThreadStart(StartGameFlow);
            Thread t = new Thread(pts);
            t.Start();
            StartGameFlow(g);
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
            SendLangMessage(msg.Chat.Id, AddPlayer(g, new Player(msg.From.Id)), msg.From.FullName());
        }
        #endregion
        #region /start
        private void Start_Command(Message msg)
        {
            if (msg.Chat.Type != ChatType.Private) return;
            if (!Users.Exists(x => x.Id == msg.From.Id))
            {
                ExecuteSql($"INSERT INTO Users(Id, LangKey) VALUES({msg.From.Id}, '{msg.From.LanguageCode}')");
                Users.Add(new User(msg.From.Id) { LangKey = msg.From.LanguageCode });
            }
            SendLangMessage(msg.Chat.Id, "Welcome");
        }
        #endregion
        #region /startgame
        private void Startgame_Command(Message msg)
        {
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
                ExecuteSql($"INSERT INTO Groups (Id, LangKey, LangSet) VALUES({msg.Chat.Id}, '{defaultLangCode}', 0)");
                Groups.Add(new Group(msg.Chat.Id));
            }
            ExecuteSql($"INSERT INTO Games (groupId) VALUES({msg.Chat.Id})");
            string response = ExecuteSql($"SELECT id FROM Games WHERE groupId={msg.Chat.Id}");
            Game g = new Game(Convert.ToInt32(response), msg.Chat.Id, msg.Chat.Title);
            GamesRunning.Add(g);
            SendLangMessage(msg.Chat.Id, "GameStarted");
            AddPlayer(g, new Player(msg.From.Id));
        }
        #endregion
        #region /sql
        private void SQL_Command(Message msg)
        {
            string commandText;
            if (msg.ReplyToMessage != null) commandText = msg.ReplyToMessage.Text;
            else commandText = msg.Text.Substring(msg.Entities.Find(x => x.Offset == 0).Length).Trim();
            string response = ExecuteSql(commandText, raw: false);
            if (!string.IsNullOrEmpty(response)) client.SendTextMessageAsync(msg.Chat.Id, response);
        }
        #endregion
        #endregion

        #region Game Flow
        #region Add player
        private string AddPlayer(Game game, Player player)
        {
            if (game.Players.Exists(x => x.Id == player.Id))
            {
                return "AlreadyInGame";
            }
            if (!SendLangMessage(player.Id, "JoinedGamePM", game.GroupName)) return "PmMe";
            game.Players.Add(player);
            return "PlayerJoinedGame";
        }
        #endregion
        #region Start game flow
        private void StartGameFlow(object gameObject)
        {
            if (!(gameObject is Game)) return;
            Game game = (Game)gameObject;
            SendLangMessage(game.GroupId, "GameFlowStarted");
            
        }
        #endregion
        #endregion

        #region SQLite
        #region Execute SQLite Query
        private string ExecuteSql(string commandText, bool raw = true)
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
                            if (!raw)
                            {
                                if (reader.RecordsAffected >= 0) r += $"_{reader.RecordsAffected} records affected_\n";
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    r += $"{reader.GetName(i)} ({reader.GetFieldType(i).Name})";
                                    if (i < reader.FieldCount - 1) r += " - ";
                                }
                                r += "\n\n";
                            }
                            while (reader.HasRows)
                            {
                                if (!reader.Read()) break;
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    r += reader.GetValue(i);
                                    if (i < reader.FieldCount - 1) r += " - ";
                                }
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
            string query = ExecuteSql("SELECT Id, LangKey, LangSet FROM Groups");
            query = query.Trim();
            foreach (var row in query.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(query)) continue;
                var split = row.Split('-');
                Groups.Add(new Group(Convert.ToInt64(split[0].Trim()), Convert.ToBoolean(split[2].Trim()))
                    { LangKey = split[1].Trim() });
            }
            query = ExecuteSql("SELECT Id, LangKey FROM Users");
            query = query.Trim();
            foreach (var row in query.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(query)) continue;
                var split = row.Split('-');
                Users.Add(new User(Convert.ToInt64(split[0].Trim()))
                { LangKey = split[1].Trim() });
            }
        }
        #endregion
        #endregion
    }
}
