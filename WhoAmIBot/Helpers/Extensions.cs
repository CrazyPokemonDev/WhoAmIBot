using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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
            while (temp.Count > 0)
            {
                T t = temp[rnd.Next(0, temp.Count - 1)];
                list.Add(t);
                temp.Remove(t);
            }
        }

        public static List<string> Split(this string s, int chars)
        {
            var split = new List<string>();
            var sout2 = new List<string>();
            var sout = new List<string>();
            foreach (var line in s.Split('\n'))
            {
                if (string.Join("\n", sout).Length + line.Length < chars) sout.Add(line);
                else
                {
                    sout2.Add(string.Join("\n", sout));
                    sout.Clear();
                }
            }
            foreach (var l in sout2)
            {
                string s2 = l;
                while (s2.Length > chars)
                {
                    split.Add(s2.Remove(chars));
                    s2 = s2.Substring(chars);
                }
                split.Add(s2);
            }
            return split;
        }

        public static bool IsGroup(this ChatType ct) => ct == ChatType.Group || ct == ChatType.Supergroup;

        public static string ToStringList<T>(this List<T> list) => string.Join("\n", list.Select(x => x.ToString()));
    }

    public static class Help
    {
        public static List<T> Longer<T>(List<T> l1, List<T> l2)
        {
            return l2.Count > l1.Count ? l2 : l1;
        }
    }
}
