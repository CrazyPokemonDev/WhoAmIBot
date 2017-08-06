using System;
using System.Collections.Generic;
using Telegram.Bot.Types;

namespace WhoAmIBotSpace.Helpers
{
    public static class Extensions
    {
        public static string FullName(this User user)
        {
            return $"{user.FirstName} {user.LastName}".Trim();
        }

        public static void Shuffle<T>(this List<T> list)
        {
            var temp = new List<T>();
            foreach (T i in list)
            {
                temp.Add(i);
            }
            list.Clear();
            Random rnd = new Random();
            for (int i=0; i < temp.Count; i++)
            {
                T t = temp[rnd.Next(0, temp.Count - 1)];
                list.Add(t);
                temp.Remove(t);
            }
        }
    }
}
