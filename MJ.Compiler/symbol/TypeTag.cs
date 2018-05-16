using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace mj.compiler.symbol
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TypeTag
    {
        // Order of numeric enum constants coresponds to assignability. 
        // Eg. FLOAT is more assignable than INT, becase INT and LONG can
        // be assigned to FLOAT (but not DOUBLE).
        INT,
        LONG,
        FLOAT,
        DOUBLE,
        
        BOOLEAN,
        STRING,
        VOID,
        METHOD,
        CLASS,
        ARRAY,
        NONE,
        ERROR,
    }

    public static class TypeTagExtensions
    {
        public static bool isNumeric(this TypeTag typeTag)
        {
            return typeTag >= TypeTag.INT && typeTag <= TypeTag.DOUBLE;
        }
        
        public static bool isNumericAssignableFrom(this TypeTag typeTag, TypeTag other)
        {
            return typeTag >= other;
        }
        
        public static String asString(this TypeTag typeTag)
        {
            switch (typeTag) {
                case TypeTag.INT: return "int";
                case TypeTag.LONG: return "long";
                case TypeTag.FLOAT: return "float";
                case TypeTag.DOUBLE: return "double";
                case TypeTag.BOOLEAN: return "boolean";
                case TypeTag.STRING: return "string";
                case TypeTag.VOID: return "void";
                case TypeTag.METHOD: return "<method>";
                case TypeTag.CLASS: return "<class>";
                case TypeTag.ARRAY: return "<array>";
                case TypeTag.NONE: return "<none>";
                case TypeTag.ERROR: return "<error>";
                default:
                    throw new ArgumentOutOfRangeException(nameof(typeTag), typeTag, null);
            }
        }
    }
}
