using System;
using System.Collections.Generic;
using System.IO;

namespace Union
{
    public class WorkWithVariant
    {
        internal static Dictionary<Type, EnumVar> MatchTypes;

        static WorkWithVariant()
        {
            MatchTypes = new Dictionary<Type, EnumVar>()
            {
                { typeof(Int16),EnumVar.VtypeI2 },
                {typeof(Int32),EnumVar.VtypeI4 },
                {typeof(float),EnumVar.VtypeR4 },
                {typeof(double),EnumVar.VtypeR8},
                {typeof(decimal),EnumVar.VtypeDecimal},
                {typeof(bool),EnumVar.VtypeBool },
                {typeof(sbyte),EnumVar.VtypeI1},
                {typeof(byte),EnumVar.VtypeUi1 },
                {typeof(UInt16),EnumVar.VtypeUi2},
                {typeof(UInt32),EnumVar.VtypeUi4},
                {typeof(Int64),EnumVar.VtypeI8},
                {typeof(UInt64),EnumVar.VtypeUi8},
                {typeof(char),EnumVar.VtypeChar},
                {typeof(string),EnumVar.VtypePwstr},
                {typeof(byte[]),EnumVar.VtypeBlob},
                {typeof(DateTime),EnumVar.VtypeDate},
                {typeof(AutoWrap),EnumVar.VtypeAutoWrap},
                {typeof(Guid),EnumVar.VtypeGuid}
            };
        }

        public static DateTime ReadDateTime(BinaryReader stream) => DateTime.FromBinary(stream.ReadInt64());

        public static void WriteDateTime(DateTime value, BinaryWriter stream) => stream.Write(value.ToBinary());

        public static byte[] ReadByteArray(BinaryReader stream) => stream.ReadBytes(stream.ReadInt32());

        public static object GetObject(BinaryReader stream)
        {
            EnumVar тип = (EnumVar)stream.ReadByte();

            switch (тип)
            {
                case EnumVar.VtypeEmpty:
                case EnumVar.VtypeNull: return null;
                case EnumVar.VtypeI2: return stream.ReadInt16();
                case EnumVar.VtypeI4: return stream.ReadInt32();
                case EnumVar.VtypeR4: return stream.ReadSingle();
                case EnumVar.VtypeR8: return stream.ReadDouble();
                case EnumVar.VtypeDecimal: return stream.ReadDecimal();
                case EnumVar.VtypeBool: return stream.ReadBoolean();
                case EnumVar.VtypeI1: return stream.ReadSByte();
                case EnumVar.VtypeUi1: return stream.ReadByte();
                case EnumVar.VtypeUi2: return stream.ReadUInt16();

                case EnumVar.VtypeUi4: return stream.ReadUInt32();

                case EnumVar.VtypeI8: return stream.ReadInt64();
                case EnumVar.VtypeUi8: return stream.ReadUInt64();
                case EnumVar.VtypeChar: return stream.ReadChar();
                case EnumVar.VtypePwstr: return stream.ReadString();

                case EnumVar.VtypeBlob: return ReadByteArray(stream);
                case EnumVar.VtypeDate: return ReadDateTime(stream);
                case EnumVar.VtypeGuid: return new Guid(stream.ReadBytes(16));
                case EnumVar.VtypeAutoWrap:
                    var target = stream.ReadInt32();
                    var autoWrap = AutoWrap.ObjectsList.GetValue(target);
                    return autoWrap.Object;
            }
            return null;
        }

        public static bool WriteObject(object obj, BinaryWriter stream)
        {
            if (obj == null)
            {
                stream.Write((byte)EnumVar.VtypeNull);
                return true;
            }

            var res = MatchTypes.TryGetValue(obj.GetType(), out var type);

            if (!res) return false;

            stream.Write((byte)type);
            switch (type)
            {
                case EnumVar.VtypeI2: stream.Write((Int16)obj); break;
                case EnumVar.VtypeI4: stream.Write((Int32)obj); break;
                case EnumVar.VtypeR4: stream.Write((float)obj); break;
                case EnumVar.VtypeR8: stream.Write((double)obj); break;
                case EnumVar.VtypeDecimal: stream.Write((decimal)obj); break;
                case EnumVar.VtypeBool: stream.Write((bool)obj); break;
                case EnumVar.VtypeI1: stream.Write((sbyte)obj); break;
                case EnumVar.VtypeUi1: stream.Write((byte)obj); break;
                case EnumVar.VtypeUi2: stream.Write((UInt16)obj); break;

                case EnumVar.VtypeUi4: stream.Write((UInt32)obj); break;

                case EnumVar.VtypeI8: stream.Write((Int64)obj); break;
                case EnumVar.VtypeUi8: stream.Write((UInt64)obj); break;
                case EnumVar.VtypeChar: stream.Write((char)obj); break;
                case EnumVar.VtypePwstr: stream.Write((string)obj); break;

                case EnumVar.VtypeBlob: stream.Write((byte[])obj); break;
                case EnumVar.VtypeDate: WriteDateTime((DateTime)obj, stream); break;
                case EnumVar.VtypeGuid: stream.Write(((Guid)obj).ToByteArray()); break;
                case EnumVar.VtypeAutoWrap:
                    stream.Write(((AutoWrap)obj).IndexInStorage);
                    break;
            }
            return true;
        }
    }
}