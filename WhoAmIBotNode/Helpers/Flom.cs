using Telegram.Bot.Types;

namespace WhoAmIBotSpace.Helpers
{
    public class Flom
    {
        public static implicit operator ChatId(Flom _)
        {
            return 267376056;
        }

        public static bool operator ==(Flom _, long i)
        {
            return i == 267376056 || i == 295152997;
        }

        public static bool operator !=(Flom _, long i)
        {
            return i != 267376056 && i != 295152997;
        }

        public static bool operator ==(long i, Flom _)
        {
            return i == 267376056 || i == 295152997;
        }

        public static bool operator !=(long i, Flom _)
        {
            return i != 267376056 && i != 295152997;
        }

        public override bool Equals(object obj)
        {
            return obj is Flom && base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
