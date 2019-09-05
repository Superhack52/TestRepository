namespace ServerRPC
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public enum EnumVar : byte
    {
        VTYPE_EMPTY = 0,
        VTYPE_NULL,
        VTYPE_I2,                   //int16_t
        VTYPE_I4,                   //int32_t
        VTYPE_R4,                   //float
        VTYPE_R8,                   //double
        VTYPE_Decimal,              //Decimal
        VTYPE_DATE,                 //DATE (double)
        VTYPE_BOOL,                 //bool
        VTYPE_I1,                   //int8_t
        VTYPE_UI1,                  //uint8_t
        VTYPE_UI2,                  //uint16_t
        VTYPE_UI4,                  //uint32_t
        VTYPE_I8,                   //int64_t
        VTYPE_UI8,                  //uint64_t
        VTYPE_INT,                  //int   Depends on architecture
        VTYPE_CHAR,                 //char
        VTYPE_PWSTR,                //struct wstr
        VTYPE_BLOB,                 //means in struct str binary data contain
        VTYPE_GUID,                //UUID
        VTYPE_AutoWrap, // Net Object
        VTYPE_JSObject

        // Хотя может использоваться отдельно от 1С
    };

    public class WorkWithVariant
    {
        internal static Dictionary<Type, EnumVar> MatchTypes;

        static WorkWithVariant()
        {
            MatchTypes = new Dictionary<Type, EnumVar>()
            {
                { typeof(Int16),EnumVar.VTYPE_I2 },
                {typeof(Int32),EnumVar.VTYPE_I4 },
                {typeof(float),EnumVar.VTYPE_R4 },
                {typeof(double),EnumVar.VTYPE_R8 },
                {typeof(decimal),EnumVar.VTYPE_Decimal},
                {typeof(bool),EnumVar.VTYPE_BOOL },
                {typeof(sbyte),EnumVar.VTYPE_I1 },
                {typeof(byte),EnumVar.VTYPE_UI1 },
                {typeof(UInt16),EnumVar.VTYPE_UI2},
                {typeof(UInt32),EnumVar.VTYPE_UI4},
                {typeof(Int64),EnumVar.VTYPE_I8},
                {typeof(UInt64),EnumVar.VTYPE_UI8},
                {typeof(char),EnumVar.VTYPE_CHAR},
                {typeof(string),EnumVar.VTYPE_PWSTR},
                {typeof(byte[]),EnumVar.VTYPE_BLOB},
                {typeof(DateTime),EnumVar.VTYPE_DATE},
                {typeof(NetObjectToNative.AutoWrap),EnumVar.VTYPE_AutoWrap},
                {typeof(Guid),EnumVar.VTYPE_GUID}
            };
        }

        public static DateTime ReadDateTime(BinaryReader stream)
        {
            long nVal = stream.ReadInt64();
            //get 64bit binary
            return DateTime.FromBinary(nVal);
        }

        public static void WriteDateTime(DateTime value, BinaryWriter stream)
        {
            long nVal = value.ToBinary();
            //get 64bit binary
            stream.Write(nVal);
        }

        public static byte[] ReadByteArray(BinaryReader stream)
        {
            var length = stream.ReadInt32();
            return stream.ReadBytes(length);
        }

        public static object GetObject(BinaryReader stream)
        {
            EnumVar тип = (EnumVar)stream.ReadByte();

            switch (тип)
            {
                case EnumVar.VTYPE_EMPTY:
                case EnumVar.VTYPE_NULL: return null;
                case EnumVar.VTYPE_I2: return stream.ReadInt16();
                case EnumVar.VTYPE_I4: return stream.ReadInt32();
                case EnumVar.VTYPE_R4: return stream.ReadSingle();
                case EnumVar.VTYPE_R8: return stream.ReadDouble();
                case EnumVar.VTYPE_Decimal: return stream.ReadDecimal();
                case EnumVar.VTYPE_BOOL: return stream.ReadBoolean();
                case EnumVar.VTYPE_I1: return stream.ReadSByte();
                case EnumVar.VTYPE_UI1: return stream.ReadByte();
                case EnumVar.VTYPE_UI2: return stream.ReadUInt16();

                case EnumVar.VTYPE_UI4: return stream.ReadUInt32();

                case EnumVar.VTYPE_I8: return stream.ReadInt64();
                case EnumVar.VTYPE_UI8: return stream.ReadUInt64();
                case EnumVar.VTYPE_CHAR: return stream.ReadChar();
                case EnumVar.VTYPE_PWSTR: return stream.ReadString();

                case EnumVar.VTYPE_BLOB: return ReadByteArray(stream);
                case EnumVar.VTYPE_DATE: return ReadDateTime(stream);
                case EnumVar.VTYPE_GUID: return new Guid(stream.ReadBytes(16));
                case EnumVar.VTYPE_AutoWrap:
                    var target = stream.ReadInt32();
                    var autoWrap = NetObjectToNative.AutoWrap.ObjectsList.GetValue(target);
                    return autoWrap.Object;
            }
            return null;
        }

        public static bool WriteObject(object obj, BinaryWriter stream)
        {
            if (obj == null)
            {
                stream.Write((byte)EnumVar.VTYPE_NULL);
                return true;
            }

            var res = MatchTypes.TryGetValue(obj.GetType(), out var type);

            if (!res) return false;

            stream.Write((byte)type);
            switch (type)
            {
                case EnumVar.VTYPE_I2: stream.Write((Int16)obj); break;
                case EnumVar.VTYPE_I4: stream.Write((Int32)obj); break;
                case EnumVar.VTYPE_R4: stream.Write((float)obj); break;
                case EnumVar.VTYPE_R8: stream.Write((double)obj); break;
                case EnumVar.VTYPE_Decimal: stream.Write((decimal)obj); break;
                case EnumVar.VTYPE_BOOL: stream.Write((bool)obj); break;
                case EnumVar.VTYPE_I1: stream.Write((sbyte)obj); break;
                case EnumVar.VTYPE_UI1: stream.Write((byte)obj); break;
                case EnumVar.VTYPE_UI2: stream.Write((UInt16)obj); break;

                case EnumVar.VTYPE_UI4: stream.Write((UInt32)obj); break;

                case EnumVar.VTYPE_I8: stream.Write((Int64)obj); break;
                case EnumVar.VTYPE_UI8: stream.Write((UInt64)obj); break;
                case EnumVar.VTYPE_CHAR: stream.Write((char)obj); break;
                case EnumVar.VTYPE_PWSTR: stream.Write((string)obj); break;

                case EnumVar.VTYPE_BLOB: stream.Write((byte[])obj); break;
                case EnumVar.VTYPE_DATE: WriteDateTime((DateTime)obj, stream); break;
                case EnumVar.VTYPE_GUID: stream.Write(((Guid)obj).ToByteArray()); break;
                case EnumVar.VTYPE_AutoWrap:
                    stream.Write(((NetObjectToNative.AutoWrap)obj).IndexInStorage);
                    break;
            }
            return true;
        }
    }
}