using TelegramBotApi.Types;

namespace WhoAmIBotSpace.Helpers
{
    public class Flom
    {
        public static implicit operator ChatId(Flom flom)
        {
            return 267376056;
        }

        public static bool operator ==(Flom f, int i)
        {
            return i == 267376056 || i == 295152997;
        }

        public static bool operator !=(Flom f, int i)
        {
            return i != 267376056 && i != 295152997;
        }

        public static bool operator ==(int i, Flom f)
        {
            return i == 267376056 || i == 295152997;
        }

        public static bool operator !=(int i, Flom f)
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
