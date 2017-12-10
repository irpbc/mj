using System;
using System.Globalization;

namespace mj.compiler.parsing
{
    public static class Utils
    {
        /*public static bool IsIdentifierStart(char highSurrogate, char lowSurrogate)
        {
            
        }

        public static bool IsIdentifierPart(char highSurrogate, char lowSurrogate)
        {
            return Char.IsLetterOrDigit((char)codePoint)
                   || codePoint == '_';
        }*/

        public static bool IsIdentifierStart(char c)
        {
            return Char.IsLetter(c) || c == '_';
        }

        public static bool IsIdentifierPart(char c)
        {
            return Char.IsLetterOrDigit(c) || c == '_';
        }
    }
}
