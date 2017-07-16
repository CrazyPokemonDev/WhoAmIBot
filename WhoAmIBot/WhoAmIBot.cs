using FlomBotFactory;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using Telegram.Bot.Types;
using UpdateEventArgs = Telegram.Bot.Args.UpdateEventArgs;
using File = System.IO.File;
using Telegram.Bot.Types.Enums;

namespace WhoAmIBotSpace
{
    public class WhoAmIBot : FlomBot
    {
        #region Properties
        public override string Name => "Who am I bot";
        #endregion
        #region Constants
        private const string baseFilePath = "C:\\Olfi01\\WhoAmIBot\\";
        private const string sqliteFilePath = baseFilePath + "db.sqlite";
        private const string connectionString = "Data Source=\"" + sqliteFilePath + "\";";
        //TODO: Add default db file
        private static readonly Dictionary<string, Action<Message>> commands = new Dictionary<string, Action<Message>>();
        #endregion
        #region Fields
        private SQLiteConnection sqliteConn;
        #endregion

        #region Constructors
        public WhoAmIBot(string token) : base(token)
        {
            if (!Directory.Exists(baseFilePath)) Directory.CreateDirectory(baseFilePath);
            if (!File.Exists(sqliteFilePath)) SQLiteConnection.CreateFile(sqliteFilePath);
            sqliteConn = new SQLiteConnection(connectionString);
            sqliteConn.Open();
            ReadCommands();
        }

        /*public WhoAmIBot()
        {
            
        }*/
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
                        cmd = cmd.Contains("@") ? cmd.Remove(cmd.IndexOf('@')) : cmd;
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
        private string GetString(string key, string langCode = "en-US")
        {
            return ExecuteSql($"SELECT 'value' FROM '{langCode}' WHERE 'key'='{key}'", raw: true);
        }
        #endregion
        #endregion

        #region Command Methods
        #region Read Commands
        private void ReadCommands()
        {
            commands.Add("/sql", new Action<Message>(SQL_Command));
        }
        #endregion

        #region /sql
        private void SQL_Command(Message msg)
        {
            string commandText;
            if (msg.ReplyToMessage != null) commandText = msg.ReplyToMessage.Text;
            else commandText = msg.Text.Substring(msg.Entities.Find(x => x.Offset == 0).Length).Trim();
            string response = ExecuteSql(commandText);
            if (!string.IsNullOrEmpty(response)) client.SendTextMessageAsync(msg.Chat.Id, response);
        }
        #endregion
        #endregion

        #region SQLite
        #region Execute SQLite Query
        private string ExecuteSql(string commandText, bool raw = false)
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
        #endregion
    }
}
