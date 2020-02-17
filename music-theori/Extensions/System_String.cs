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

        /// <summary>
        /// Performs equality checking using behaviour similar to that of SQL's LIKE.
        /// </summary>
        /// <param name="s">The string to check for equality.</param>
        /// <param name="match">The mask to check the string against.</param>
        /// <param name="CaseInsensitive">True if the check should be case insensitive.</param>
        /// <returns>Returns true if the string matches the mask.</returns>
        /// <remarks>
        /// All matches are case-insensitive in the invariant culture.
        /// % acts as a multi-character wildcard.
        /// * acts as a multi-character wildcard.
        /// _ acts as a single-character wildcard.
        /// Backslash acts as an escape character.  It needs to be doubled if you wish to
        /// check for an actual backslash.
        /// [abc] searches for multiple characters.
        /// [^abc] matches any character that is not a,b or c
        /// [a-c] matches a, b or c
        /// Published on CodeProject: http://www.codeproject.com/Articles/
        ///         608266/A-Csharp-LIKE-implementation-that-mimics-SQL-LIKE
        /// </remarks>
        public static bool Like(this string s, string? match, bool CaseInsensitive = true)
        {
            //Nothing matches a null mask or null input string
            if (match == null || s == null)
                return false;

            //Null strings are treated as empty and get checked against the mask.
            //If checking is case-insensitive we convert to uppercase to facilitate this.
            if (CaseInsensitive)
            {
                s = s.ToUpperInvariant();
                match = match.ToUpperInvariant();
            }

            //if (s.Contains(match)) return true;

            //Keeps track of our position in the primary string - s.
            int j = 0;
            //Used to keep track of multi-character wildcards.
            bool matchanymulti = false;
            //Used to keep track of multiple possibility character masks.
            string? multicharmask = null;
            bool inversemulticharmask = false;
            for (int i = 0; i < match.Length; i++)
            {
                //If this is the last character of the mask and its a % or * we are done
                if (i == match.Length - 1 && (match[i] == '%' || match[i] == '*'))
                    return true;
                //A direct character match allows us to proceed.
                var charcheck = true;
                //Backslash acts as an escape character.  If we encounter it, proceed
                //to the next character.
                if (match[i] == '\\')
                {
                    i++;
                    if (i == match.Length)
                        i--;
                }
                else
                {
                    //If this is a wildcard mask we flag it and proceed with the next character
                    //in the mask.
                    if (match[i] == '%' || match[i] == '*')
                    {
                        matchanymulti = true;
                        continue;
                    }
                    //If this is a single character wildcard advance one character.
                    if (match[i] == '_')
                    {
                        //If there is no character to advance we did not find a match.
                        if (j == s.Length)
                            return false;
                        j++;
                        continue;
                    }
                    if (match[i] == '[')
                    {
                        var endbracketidx = match.IndexOf(']', i);
                        //Get the characters to check for.
                        multicharmask = match.Substring(i + 1, endbracketidx - i - 1);
                        //Check for inversed masks
                        inversemulticharmask = multicharmask.StartsWith("^");
                        //Remove the inversed mask character
                        if (inversemulticharmask)
                            multicharmask = multicharmask.Remove(0, 1);
                        //Unescape \^ to ^
                        multicharmask = multicharmask.Replace("\\^", "^");

                        //Prevent direct character checking of the next mask character
                        //and advance to the next mask character.
                        charcheck = false;
                        i = endbracketidx;
                        //Detect and expand character ranges
                        if (multicharmask.Length == 3 && multicharmask[1] == '-')
                        {
                            var newmask = "";
                            var first = multicharmask[0];
                            var last = multicharmask[2];
                            if (last < first)
                            {
                                first = last;
                                last = multicharmask[0];
                            }
                            var c = first;
                            while (c <= last)
                            {
                                newmask += c;
                                c++;
                            }
                            multicharmask = newmask;
                        }
                        //If the mask is invalid we cannot find a mask for it.
                        if (endbracketidx == -1)
                            return false;
                    }
                }
                //Keep track of match finding for this character of the mask.
                var matched = false;
                while (j < s.Length)
                {
                    //This character matches, move on.
                    if (charcheck && s[j] == match[i])
                    {
                        j++;
                        matched = true;
                        break;
                    }
                    //If we need to check for multiple charaters to do.
                    if (multicharmask != null)
                    {
                        var ismatch = multicharmask.Contains(s[j]);
                        //If this was an inverted mask and we match fail the check for this string.
                        //If this was not an inverted mask check and we did not match fail for this string.
                        if (inversemulticharmask && ismatch ||
                            !inversemulticharmask && !ismatch)
                        {
                            //If we have a wildcard preceding us we ignore this failure
                            //and continue checking.
                            if (matchanymulti)
                            {
                                j++;
                                continue;
                            }
                            return false;
                        }
                        j++;
                        matched = true;
                        //Consumse our mask.
                        multicharmask = null;
                        break;
                    }
                    //We are in an multiple any-character mask, proceed to the next character.
                    if (matchanymulti)
                    {
                        j++;
                        continue;
                    }
                    break;
                }
                //We've found a match - proceed.
                if (matched)
                {
                    matchanymulti = false;
                    continue;
                }

                //If no match our mask fails
                return false;
            }
            //Some characters are left - our mask check fails.
            if (j < s.Length)
                return false;
            //We've processed everything - this is a match.
            return true;
        }
    }
}
