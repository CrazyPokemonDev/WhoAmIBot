using Telegram.Bot.Types;

namespace WhoAmIBotSpace.Helpers
{
    public static class Extensions
    {
        public static string FullName(this User user)
        {
            return $"{user.FirstName} {user.LastName}".Trim();
        }
    }
}
