using System;

namespace mj.compiler.symbol
{
    public enum TypeTag
    {
        INT,
        LONG,
        FLOAT,
        DOUBLE,
        BOOLEAN,
        VOID,
        METHOD,
        NONE,
        ERROR
    }

    public static class TypeTagExtensions
    {
        public static String asString(this TypeTag typeTag)
        {
            switch (typeTag) {
                case TypeTag.INT: return "int";
                case TypeTag.LONG: return "long";
                case TypeTag.FLOAT: return "float";
                case TypeTag.DOUBLE: return "double";
                case TypeTag.BOOLEAN: return "boolean";
                case TypeTag.VOID: return "void";
                case TypeTag.METHOD: return "<method>";
                case TypeTag.NONE: return "<none>";
                case TypeTag.ERROR: return "<error>";
                default:
                    throw new ArgumentOutOfRangeException(nameof(typeTag), typeTag, null);
            }
        }
    }
}
