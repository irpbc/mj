namespace mj.compiler.parsing
{
    public static class Utils
    {
        public static bool IsIdentifierStart(int codePoint)
        {
            return (codePoint >= 'a' && codePoint <= 'z')
                   || (codePoint >= 'A' && codePoint <= 'z')
                   || codePoint == '_';
        }

        public static bool IsIdentifierPart(int codePoint)
        {
            return (codePoint >= 'a' && codePoint <= 'z')
                   || (codePoint >= 'A' && codePoint <= 'z')
                   || codePoint == '_'
                   || (codePoint >= '0' && codePoint <= '8');
        }
    }
}
