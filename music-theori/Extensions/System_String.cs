using System.Linq;
using System.Text;

namespace System
{
    public static class System_String_Extensions
    {
        public static string CamelStringToSeparated(this string camel, char separator = ' ')
        {
            var builder = new StringBuilder();
            for (int i = 0; i < camel.Length; i++)
            {
                char c = camel[i];
                if (char.IsUpper(c) && i > 0)
                    builder.Append(separator);
                builder.Append(c);
            }
            return builder.ToString();
        }

        public static string FormatJson(this string json)
        {
            const string INDENT_STRING = "    ";
            int indentation = 0;
            int quoteCount = 0;
            var result =
                from ch in json
                let quotes = ch == '"' ? quoteCount++ : quoteCount
                let lineBreak = ch == ',' && quotes % 2 == 0 ? ch + Environment.NewLine + string.Concat(Enumerable.Repeat(INDENT_STRING, indentation)) : null
                let openChar = ch == '{' || ch == '[' ? ch + Environment.NewLine + string.Concat(Enumerable.Repeat(INDENT_STRING, ++indentation)) : ch.ToString()
                let closeChar = ch == '}' || ch == ']' ? Environment.NewLine + string.Concat(Enumerable.Repeat(INDENT_STRING, --indentation)) + ch : ch.ToString()
                select lineBreak ?? (openChar.Length > 1 ? openChar : closeChar);

            return string.Concat(result);
        }

        #region Repeat

        public static string Repeat(this string s, int n)
        {
            if (n <= 0) return "";
            return new StringBuilder(s.Length * n).AppendJoin(s, new string[n + 1]).ToString();
        }

#endregion

        #region Split

        public static bool TrySplit(this string s, char sep, out string v0, out string v1)
        {
            v0 = v1 = "";
            string[] results = s.Split(new[] { sep }, 2);
            if (results.Length != 2)
                return false;
            (v0, v1) = results;
            return true;
        }

        public static bool TrySplit(this string s, char sep, out string v0, out string v1, out string v2)
        {
            v0 = v1 = v2 = "";
            string[] results = s.Split(new[] { sep }, 3);
            if (results.Length != 3)
                return false;
            (v0, v1, v2) = results;
            return true;
        }

        public static bool TrySplit(this string s, char sep, out string v0, out string v1, out string v2, out string v3)
        {
            v0 = v1 = v2 = v3 = "";
            string[] results = s.Split(new[] { sep }, 4);
            if (results.Length != 4)
                return false;
            (v0, v1, v2, v3) = results;
            return true;
        }

        public static bool TrySplit(this string s, char sep, out string v0, out string v1, out string v2, out string v3, out string v4)
        {
            v0 = v1 = v2 = v3 = v4 = "";
            string[] results = s.Split(new[] { sep }, 5);
            if (results.Length != 5)
                return false;
            (v0, v1, v2, v3, v4) = results;
            return true;
        }

        public static bool TrySplit(this string s, char sep, out string v0, out string v1, out string v2, out string v3, out string v4, out string v5)
        {
            v0 = v1 = v2 = v3 = v4 = v5 = "";
            string[] results = s.Split(new[] { sep }, 6);
            if (results.Length != 6)
                return false;
            (v0, v1, v2, v3, v4, v5) = results;
            return true;
        }

        public static bool TrySplit(this string s, char sep, out string v0, out string v1, out string v2, out string v3, out string v4, out string v5, out string v6)
        {
            v0 = v1 = v2 = v3 = v4 = v5 = v6 = "";
            string[] results = s.Split(new[] { sep }, 7);
            if (results.Length != 7)
                return false;
            (v0, v1, v2, v3, v4, v5, v6) = results;
            return true;
        }

        public static bool TrySplit(this string s, char sep, out string v0, out string v1, out string v2, out string v3, out string v4, out string v5, out string v6, out string v7)
        {
            v0 = v1 = v2 = v3 = v4 = v5 = v6 = v7 = "";
            string[] results = s.Split(new[] { sep }, 8);
            if (results.Length != 8)
                return false;
            (v0, v1, v2, v3, v4, v5, v6, v7) = results;
            return true;
        }

        public static bool TrySplit(this string s, char sep, out string v0, out string v1, out string v2, out string v3, out string v4, out string v5, out string v6, out string v7, out string v8)
        {
            v0 = v1 = v2 = v3 = v4 = v5 = v6 = v7 = v8 = "";
            string[] results = s.Split(new[] { sep }, 9);
            if (results.Length != 9)
                return false;
            (v0, v1, v2, v3, v4, v5, v6, v7, v8) = results;
            return true;
        }

        public static bool TrySplit(this string s, char sep, out string v0, out string v1, out string v2, out string v3, out string v4, out string v5, out string v6, out string v7, out string v8, out string v9)
        {
            v0 = v1 = v2 = v3 = v4 = v5 = v6 = v7 = v8 = v9 = "";
            string[] results = s.Split(new[] { sep }, 10);
            if (results.Length != 10)
                return false;
            (v0, v1, v2, v3, v4, v5, v6, v7, v8, v9) = results;
            return true;
        }

        #endregion
    }
}
