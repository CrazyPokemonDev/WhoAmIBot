using Telegram.Bot.Types;

namespace WhoAmIBotSpace.Classes
{
    public class MessageEventArgs
    {
        public Message Message { get; set; }
        public MessageEventArgs(Message msg)
        {
            Message = msg;
        }
    }

    public class CallbackQueryEventArgs
    {
        public CallbackQuery CallbackQuery { get; set; }
        public CallbackQueryEventArgs(CallbackQuery cq)
        {
            CallbackQuery = cq;
        }
    }
}
