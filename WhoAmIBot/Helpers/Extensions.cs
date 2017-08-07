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
            while(temp.Count > 0)
            {
                T t = temp[rnd.Next(0, temp.Count - 1)];
                list.Add(t);
                temp.Remove(t);
            }
        }

        public static List<string> Split(this string s, int chars)
        {
            var split = new List<string>();
            while (s.Length > chars)
            {
                split.Add(s.Remove(chars));
                s = s.Substring(chars);
            }
            split.Add(s);
            return split;
        }
    }

    public static class Help
    {
        public static List<T> Longer<T>(List<T> l1, List<T> l2)
        {
            return l2.Count > l1.Count ? l2 : l1;
        }
    }
}
