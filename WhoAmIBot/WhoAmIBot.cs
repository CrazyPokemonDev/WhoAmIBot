﻿using FlomBotFactory;
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
            var par = new Dictionary<string, object>() { { "key", key } };
            string query = ExecuteSql($"SELECT value FROM '{langCode}' WHERE key=@key", par).Trim();
            if (query.StartsWith("SQL logic error or missing database") || string.IsNullOrWhiteSpace(query))
            {
                par = new Dictionary<string, object>() { { "key", key } };
                query = ExecuteSql($"SELECT value FROM '{defaultLangCode}' WHERE key=@key", par).Trim();
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

        private bool SendAndGetLangMessage(long chatid, long langFrom, string key, IReplyMarkup markup, out Message message, params string[] par)
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
                return true;
            }
            catch
            {
                message = null;
                return false;
            }
        }
        #endregion
        #region Edit Lang Message
        private bool EditLangMessage(long chatid, long langFrom, int messageId, string key, 
            IReplyMarkup markup, string appendStart, params string[] par)
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
            commands.Add("/setlang", new Action<Message>(Setlang_Command));
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
        #region /setlang
        private void Setlang_Command(Message msg)
        {
            SendLangMessage(msg.Chat.Id, "NotImplemented");
            switch (msg.Chat.Type)
            {
                case ChatType.Private:

                    break;
                case ChatType.Group:
                case ChatType.Supergroup:

                    break;
            }
        }
        #endregion
        #region /start
        private void Start_Command(Message msg)
        {
            if (msg.Chat.Type != ChatType.Private) return;
            if (!Users.Exists(x => x.Id == msg.From.Id))
            {
                var par = new Dictionary<string, object>() { { "id", msg.From.Id }, { "langCode", msg.From.LanguageCode } };
                ExecuteSql("INSERT INTO Users(Id, LangKey) VALUES(@id, @langCode)", par);
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
                var par = new Dictionary<string, object>() { { "id", msg.Chat.Id }, { "langCode", defaultLangCode } };
                ExecuteSql("INSERT INTO Groups (Id, LangKey, LangSet) VALUES(@id, @langCode, 0)", par);
                Groups.Add(new Group(msg.Chat.Id));
            }
            var par2 = new Dictionary<string, object>() { { "id", msg.Chat.Id } };
            ExecuteSql($"INSERT INTO Games (groupId) VALUES(@id)", par2);
            string response = ExecuteSql("SELECT id FROM Games WHERE groupId=@id", par2);
            Game g = new Game(Convert.ToInt32(response), msg.Chat.Id, msg.Chat.Title);
            GamesRunning.Add(g);
            SendLangMessage(msg.Chat.Id, "GameStarted");
            AddPlayer(g, new Player(msg.From.Id, msg.From.FullName()));
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
                SendLangMessage(game.GroupId, "PmMe", null, player.Name);
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
                SendLangMessage(game.GroupId, "PlayerTurn", null, atTurn.Name);
                #region Ask Question
                Message sentMessage = null;
                EventHandler<MessageEventArgs> qHandler = (sender, e) =>
                {
                    if (e.Message.From.Id != atTurn.Id || e.Message.Chat.Type != ChatType.Private) return;
                    EditLangMessage(atTurn.Id, game.GroupId, sentMessage.MessageId, "QuestionReceived", null, "");
                    string yes = GetString("Yes", LangCode(game.GroupId));
                    string no = GetString("No", LangCode(game.GroupId));
                    SendLangMessage(game.GroupId, "QuestionAsked",
                        ReplyMarkupMaker.InlineYesNo(yes, $"yes@{game.GroupId}", no, $"no@{game.GroupId}"),
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
                    SendLangMessage(game.GroupId, "PlayerGuessed",
                        ReplyMarkupMaker.InlineYesNo(yes, $"yes@{game.GroupId}", no, $"no@{game.GroupId}"),
                        atTurn.Name, e.Message.Text);
                    mre.Set();
                };
                #endregion
                #region Callback Handler
                EventHandler<CallbackQueryEventArgs> c1Handler = (sender, e) =>
                {
                    if (!game.Players.Exists(x => x.Id == e.CallbackQuery.From.Id)
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
                            EditLangMessage(e.CallbackQuery.From.Id, game.GroupId, sentMessage.MessageId, "PleaseGuess", null, "");
                            #endregion
                            break;
                        case "giveup":
                            #region Give Up
                            endloop = true;
                            Player p = game.Players.Find(x => x.Id == e.CallbackQuery.From.Id);
                            SendLangMessage(game.GroupId, "GaveUp", null,
                                p.Name,
                                game.RoleIdDict[e.CallbackQuery.From.Id]);
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
                    ReplyMarkupMaker.InlineGuessGiveUp(guess1, $"guess@{game.GroupId}", giveUp1, $"giveup@{game.GroupId}"), out sentMessage);
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
                    if (!game.Players.Exists(x => x.Id == e.CallbackQuery.From.Id)
                    || (!e.CallbackQuery.Data.StartsWith("yes@") && !e.CallbackQuery.Data.StartsWith("no@"))
                    || e.CallbackQuery.Data.IndexOf('@') != e.CallbackQuery.Data.LastIndexOf('@')) return;
                    string answer = e.CallbackQuery.Data.Split('@')[0];
                    long groupId = Convert.ToInt64(e.CallbackQuery.Data.Split('@')[1]);
                    if (groupId != game.GroupId) return;
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                    Message cmsg = e.CallbackQuery.Message;
                    switch (answer)
                    {
                        case "yes":
                            EditLangMessage(game.GroupId, game.GroupId, cmsg.MessageId, "AnsweredYes", null, cmsg.Text,
                            game.Players.Find(x => x.Id == e.CallbackQuery.From.Id).Name);
                            if (guess)
                            {
                                game.Players.Remove(atTurn);
                                game.TrySetWinner(atTurn);
                            }
                            break;
                        case "no":
                            EditLangMessage(game.GroupId, game.GroupId, cmsg.MessageId, "AnsweredNo", null, cmsg.Text,
                            game.Players.Find(x => x.Id == e.CallbackQuery.From.Id).Name);
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
            string winnerName = game.Winner == null ? "Nobody" : game.Winner.Name;
            par = new Dictionary<string, object>() { { "groupid", game.GroupId }, { "winnerid", winnerId }, { "winnername", winnerName } };
            client.SendTextMessageAsync(Flom, ExecuteSql("INSERT INTO GamesFinished (groupId, winnerid, winnername) VALUES(@groupid, @winnerid, @winnername)", par));
            SendLangMessage(game.GroupId, "GameFinished", null, winnerName);
            GamesRunning.Remove(game);
            #endregion
        }
        #endregion
        #endregion

        #region SQLite
        #region Execute SQLite Query
        private string ExecuteSql(string commandText, Dictionary<string, object> parameters = null, bool raw = true)
        {
            string r = "";
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
            string query = ExecuteSql("SELECT Id, LangKey, LangSet FROM Groups");
            query = query.Trim();
            foreach (var row in query.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(query)) continue;
                var split = row.Split('|');
                Groups.Add(new Group(Convert.ToInt64(split[0].Trim()), Convert.ToBoolean(split[2].Trim()))
                { LangKey = split[1].Trim() });
            }
            query = ExecuteSql("SELECT Id, LangKey FROM Users");
            query = query.Trim();
            foreach (var row in query.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(query)) continue;
                var split = row.Split('|');
                Users.Add(new User(Convert.ToInt64(split[0].Trim()))
                { LangKey = split[1].Trim() });
            }
        }
        #endregion
        #endregion
    }
}