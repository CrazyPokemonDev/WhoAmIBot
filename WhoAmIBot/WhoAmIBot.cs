﻿using FlomBotFactory;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using UpdateEventArgs = TelegramBotApi.Types.Events.UpdateEventArgs;
using File = System.IO.File;
using WhoAmIBotSpace.Classes;
using WhoAmIBotSpace.Helpers;
using System.Linq;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using TelegramBotApi.Enums;
using TelegramBotApi.Types.Events;
using TelegramBotApi.Types;
using TelegramBotApi.Types.Markup;

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
        private static readonly string appDataBaseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WhoAmIBot\\");
        private const string dateTimeFileFormat = "yyyy-MM-dd-HH-mm-ss";
        private static readonly string defaultNodeDirectory = Path.Combine(appDataBaseDir, "default\\");
        private static readonly string controlUpdaterPath = Path.Combine(appDataBaseDir,
            "git\\WhoAmIBot\\ControlUpdater\\bin\\Release\\ControlUpdater.exe");
        private static readonly string gitDirectory = Path.Combine(appDataBaseDir, "git\\");
        private const long tracingChannelId = -1001383830638;
        #endregion
        #region Fields
        private SQLiteConnection sqliteConn;
        private List<Node> Nodes = new List<Node>();
        private bool updating = false;
        #endregion
        #region Events
        public event EventHandler<RestartEventArgs> Restart;
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
        public WhoAmIBot(string token) : base(token)
        {
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

        public override bool StartBot()
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
            client.OnCallbackQuery += Client_OnCallbackQuery;
            client.OnCallbackQuery += Client_OnCallbackQueryChecker;
            /*var dir = defaultNodeDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "WhoAmIBotNode.exe");
            Node n = new Node(path);
            n.NodeStopped += (sender, node) => Nodes.Remove(n);
            n.Start(Token);
            Nodes.Add(n);*/
            Update(client.SendTextMessageAsync(testingGroupId, "Updating node and starting...").Result);
            return base.StartBot();
        }

        public override bool StopBot()
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
            client.OnCallbackQuery -= Client_OnCallbackQuery;
            client.OnCallbackQuery -= Client_OnCallbackQueryChecker;
            return base.StopBot();
        }
        #endregion
        #region On Update
        protected override void Client_OnUpdate(object sender, UpdateEventArgs e)
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
            if (e.Update.Type == UpdateType.Message && e.Update.Message.Type == MessageType.Text)
            {
                if (e.Update.Message.Entities.Length > 0 && e.Update.Message.Entities[0].Type == MessageEntityType.BotCommand
                    && e.Update.Message.Entities[0].Offset == 0)
                {
                    var cmd = e.Update.Message.Entities[0].Value;
                    cmd = cmd.ToLower();
                    cmd = cmd.Contains($"@{Username.ToLower()}") ? cmd.Remove(cmd.IndexOf($"@{Username.ToLower()}")) : cmd;
                    if (cmd == "/update")
                    {
                        if (e.Update.Message.From.Id == Flom)
                        {
                            var t = client.SendTextMessageAsync(e.Update.Message.Chat.Id, "Updating...");
                            t.Wait();
                            Update(t.Result);
                            return;
                        }
                    }
                    else if (cmd == "/updatecontrol")
                    {
                        if (e.Update.Message.From.Id == Flom)
                        {
                            var t = client.SendTextMessageAsync(e.Update.Message.Chat.Id, "Updating Control.");
                            t.Wait();
                            UpdateControl(t.Result);
                            return;
                        }
                    }
                    if (StandaloneCommandExists(cmd))
                    {
                        var node = Nodes.FirstOrDefault(x => x.State == NodeState.Primary);
                        if (node == null && !updating)
                        {
                            Update(client.SendTextMessageAsync(testingGroupId, "No node found. Starting a new one...").Result, true);
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

        private void Client_OnCallbackQueryChecker(object sender, CallbackQueryEventArgs e)
        {
            if ((e.CallbackQuery.Data != "update" && e.CallbackQuery.Data != "dontUpdate" && e.CallbackQuery.Data != "updatecontrol")
                || e.CallbackQuery.From.Id != Flom || e.CallbackQuery.Message == null) return;
            Message cmsg = e.CallbackQuery.Message;
            switch (e.CallbackQuery.Data)
            {
                case "update":
                    Update(cmsg);
                    break;
                case "updatecontrol":
                    client.EditMessageTextAsync(cmsg.Chat.Id, cmsg.MessageId, "Updating Control.");
                    UpdateControl(cmsg);
                    break;
                case "dontUpdate":
                    client.EditMessageTextAsync(cmsg.Chat.Id, cmsg.MessageId, "Okay, no work for me :)");
                    break;
            }
        }
        #endregion

        #region Updater
        private void Update(Message toEdit, bool wait=false)
        {
            updating = true;
            ParameterizedThreadStart pts = new ParameterizedThreadStart(UpdateThread);
            Thread t = new Thread(pts);
            t.Start(toEdit);
            if (wait)
                t.Join();
        }

        private void UpdateThread(object obj)
        {
            try
            {
                if (!(obj is Message))
                {
                    return;
                }
                Message toEdit = (Message)obj;
                var t = client.EditMessageTextAsync(toEdit.Chat.Id, toEdit.MessageId, toEdit.Text + "\nPulling git...");
                t.Wait();
                toEdit = t.Result;
                UpdateGit();
                t = client.EditMessageTextAsync(toEdit.Chat.Id, toEdit.MessageId, toEdit.Text + "\nCopying files to node directory...");
                t.Wait();
                toEdit = t.Result;
                string newDir = Path.Combine(appDataBaseDir, $"WhoAmIBotNode_{DateTime.Now.ToString(dateTimeFileFormat)}\\");
                if (!Directory.Exists(newDir)) Directory.CreateDirectory(newDir);
                string gitDirToCopy = Path.Combine(gitDirectory, "WhoAmIBot\\WhoAmIBotNode\\bin\\Release");
                DeepCopy(new DirectoryInfo(gitDirToCopy), new DirectoryInfo(newDir));
                t = client.EditMessageTextAsync(toEdit.Chat.Id, toEdit.MessageId, toEdit.Text + $"\nStarting node at {newDir}...");
                t.Wait();
                toEdit = t.Result;
                Node n = new Node(Path.Combine(newDir, "WhoAmIBotNode.exe"));
                foreach (var sNode in Nodes.FindAll(x => x.State == NodeState.Primary))
                {
                    sNode.SoftStop();
                }
                n.Start(Token);
                n.NodeStopped += (sender, node) => Nodes.Remove(n);
                Nodes.Add(n);
                t = client.EditMessageTextAsync(toEdit.Chat.Id, toEdit.MessageId, toEdit.Text + "\nFinished.");
                t.Wait();
                toEdit = t.Result;
                updating = false;
            }
            catch (Exception ex)
            {
                client.SendTextMessageAsync(Flom, ex.ToString());
                updating = false;
            }
        }

        private static void UpdateGit()
        {
            if (!Directory.Exists(appDataBaseDir)) Directory.CreateDirectory(appDataBaseDir);
            if (!Directory.Exists(gitDirectory)) Directory.CreateDirectory(gitDirectory);
            string firstDir = Path.Combine(gitDirectory, "first.bat");
            if (!File.Exists(firstDir)) File.Copy("Updater\\first.bat", firstDir);
            string runDir = Path.Combine(gitDirectory, "run.bat");
            if (!File.Exists(runDir)) File.Copy("Updater\\run.bat", runDir);
            Process first = new Process();
            first.StartInfo.FileName = firstDir;
            first.StartInfo.UseShellExecute = false;
            first.StartInfo.WorkingDirectory = Path.GetDirectoryName(firstDir);
            first.Start();
            first.WaitForExit();
            Process run = new Process();
            run.StartInfo.FileName = runDir;
            run.StartInfo.UseShellExecute = false;
            run.StartInfo.WorkingDirectory = Path.GetDirectoryName(runDir);
            run.Start();
            run.WaitForExit();
        }

        public static void DeepCopy(DirectoryInfo source, DirectoryInfo target)
        {
            if (target.FullName.Contains(source.FullName))
                throw new Exception("Cannot perform DeepCopy: Ancestry conflict detected");
            // Recursively call the DeepCopy Method for each Directory
            foreach (DirectoryInfo dir in source.GetDirectories())
                DeepCopy(dir, target.CreateSubdirectory(dir.Name));

            // Go ahead and copy each file in "source" to the "target" directory
            foreach (FileInfo file in source.GetFiles())
                file.CopyTo(Path.Combine(target.FullName, file.Name), true);
        }

        public void UpdateControl(Message toEdit)
        {
            ParameterizedThreadStart pts = new ParameterizedThreadStart(UpdateControlThread);
            Thread t = new Thread(pts);
            t.Start(toEdit);
        }

        private void UpdateControlThread(object obj)
        {
            try
            {
                if (!(obj is Message toEdit)) return;
                var t = client.EditMessageTextAsync(toEdit.Chat.Id, toEdit.MessageId, "Updating control");
                t.Wait();
                toEdit = t.Result;
                t = client.EditMessageTextAsync(toEdit.Chat.Id, toEdit.MessageId, toEdit.Text + "\nWaiting for games to stop...");
                t.Wait();
                toEdit = t.Result;
                string newestNodePath = null;
                if (Nodes.Any(x => x.State == NodeState.Primary))
                {
                    Node node = Nodes.Find(x => x.State == NodeState.Primary);
                    if (node.Path != Path.Combine(defaultNodeDirectory, "WhoAmIBotNode.exe")) newestNodePath = node.Path;
                }
                StopBot();

                string path = Assembly.GetExecutingAssembly().CodeBase;
                if (path.StartsWith("file:///")) path = path.Substring(8).Replace("/", "\\");
                t = client.EditMessageTextAsync(toEdit.Chat.Id, toEdit.MessageId, toEdit.Text + "\nUpdating git...");
                t.Wait();
                toEdit = t.Result;
                #region Update git
                UpdateGit();
                string newDir = Path.Combine(appDataBaseDir, $"WhoAmIBotControl_{DateTime.Now.ToString(dateTimeFileFormat)}\\");
                if (!Directory.Exists(newDir)) Directory.CreateDirectory(newDir);
                string gitDirToCopy = Path.Combine(gitDirectory, "WhoAmIBot\\WhoAmIBot\\bin\\Release");
                DeepCopy(new DirectoryInfo(gitDirToCopy), new DirectoryInfo(newDir));
                #endregion
                if (newestNodePath != null)
                {
                    DeepCopy(new DirectoryInfo(Path.GetDirectoryName(newestNodePath)), new DirectoryInfo(defaultNodeDirectory));
                }
                newDir = Path.Combine(newDir, "WhoAmIBot.dll");
                t = client.EditMessageTextAsync(toEdit.Chat.Id, toEdit.MessageId, toEdit.Text + "\nInvoking restart event...");
                t.Wait();
                toEdit = t.Result;
                ProcessStartInfo psi = new ProcessStartInfo(controlUpdaterPath, "\"" + path.Trim('"') + "\" \"" + newDir.Trim('"') + "\"");
                Process.Start(psi);
                Thread.Sleep(500);
                Restart?.Invoke(this, new RestartEventArgs(path, newDir));
            }
            catch (Exception ex)
            {
                client.SendTextMessageAsync(Flom, ex.ToString());
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
