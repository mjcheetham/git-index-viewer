namespace Mjcheetham.Git.IndexViewer
{
    public static class StringExtensions
    {
        public static string Truncate(this string s, int maxLength)
        {
            if (s.Length <= maxLength) return s;
            return s.Substring(0, maxLength);
        }
    }
}
