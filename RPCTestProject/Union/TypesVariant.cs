using System;
using System.Collections.Generic;
using System.IO;

namespace Union
{
    public enum EnumVar : byte
    {
        VtypeEmpty = 0,
        VtypeNull,
        VtypeI2, //int16
        VtypeI4, //int32
        VtypeR4, //float
        VtypeR8, //double
        VtypeDecimal, //Decimal
        VtypeDate, //DATE (double)
        VtypeBool, //bool
        VtypeI1, //int8
        VtypeUi1, //uint8
        VtypeUi2, //uint16
        VtypeUi4, //uint32
        VtypeI8, //int64
        VtypeUi8, //uint64
        VtypeInt, //int   Depends on architecture
        VtypeChar, //char
        VtypePwstr, //struct wstr
        VtypeBlob, //means in struct str binary data contain
        VtypeGuid, //Guid
        VtypeAutoWrap, // Net Object
        VtypeJsObject
    };

    // Класс WorkVariants осуществляет сериализацию и десериализацию объектов
    public class WorkVariants
    {
        internal static Dictionary<Type, EnumVar> MatchTypes;

        static WorkVariants()
        {
            // Напрямую сериализуются byte[],числа, строки,булево,Дата,char,Guid
            //Для AutoWrapClient передается индекс в хранилище
            MatchTypes = new Dictionary<Type, EnumVar>()
            {
                {typeof(Int16), EnumVar.VtypeI2},
                {typeof(Int32), EnumVar.VtypeI4},
                {typeof(float), EnumVar.VtypeR4},
                {typeof(double), EnumVar.VtypeR8},
                {typeof(decimal), EnumVar.VtypeDecimal},
                {typeof(bool), EnumVar.VtypeBool},
                {typeof(sbyte), EnumVar.VtypeI1},
                {typeof(byte), EnumVar.VtypeUi1},
                {typeof(UInt16), EnumVar.VtypeUi2},
                {typeof(UInt32), EnumVar.VtypeUi4},
                {typeof(Int64), EnumVar.VtypeI8},
                {typeof(UInt64), EnumVar.VtypeUi8},
                {typeof(char), EnumVar.VtypeChar},
                {typeof(string), EnumVar.VtypePwstr},
                {typeof(byte[]), EnumVar.VtypeBlob},
                {typeof(DateTime), EnumVar.VtypeDate},
                {typeof(AutoWrapClient), EnumVar.VtypeAutoWrap},
                {typeof(Guid), EnumVar.VtypeGuid}
            };
        }

        public static object GetObject(BinaryReader stream, TCPClientConnector connector)
        {
            // Считываем тип объекта
            EnumVar type = (EnumVar)stream.ReadByte();

            // В зависмости от типа считываем и преобразуем данные
            switch (type)
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

                case EnumVar.VtypeAutoWrap: return new AutoWrapClient(stream.ReadInt32(), connector);
            }
            return null;
        }

        public static bool WriteObject(object @object, BinaryWriter stream)
        {
            // Если null то записываем только VTYPE_NULL
            if (@object == null)
            {
                stream.Write((byte)EnumVar.VtypeNull);
                return true;
            }

            // Если это RefParam то сериализуем значение из Value
            // Нужен для возвращения out значения в Value
            if (@object.GetType() == typeof(RefParam)) return WriteObject(((RefParam)@object).Value, stream);

            // Ищем тип в словаре MatchTypes
            var res = MatchTypes.TryGetValue(@object.GetType(), out var type);

            // Если тип не поддерживаемый вызываем исключение
            if (!res) throw new Exception("Неверный тип " + @object.GetType());

            // Записываем тип объекта
            stream.Write((byte)type);

            // В зависимости от типа сериализуем объект
            switch (type)
            {
                case EnumVar.VtypeI2: stream.Write((Int16)@object); break;
                case EnumVar.VtypeI4: stream.Write((Int32)@object); break;
                case EnumVar.VtypeR4: stream.Write((float)@object); break;
                case EnumVar.VtypeR8: stream.Write((double)@object); break;
                case EnumVar.VtypeDecimal: stream.Write((decimal)@object); break;
                case EnumVar.VtypeBool: stream.Write((bool)@object); break;
                case EnumVar.VtypeI1: stream.Write((sbyte)@object); break;
                case EnumVar.VtypeUi1: stream.Write((byte)@object); break;
                case EnumVar.VtypeUi2: stream.Write((UInt16)@object); break;

                case EnumVar.VtypeUi4: stream.Write((UInt32)@object); break;

                case EnumVar.VtypeI8: stream.Write((Int64)@object); break;
                case EnumVar.VtypeUi8: stream.Write((UInt64)@object); break;
                case EnumVar.VtypeChar: stream.Write((char)@object); break;
                case EnumVar.VtypePwstr: stream.Write((string)@object); break;

                case EnumVar.VtypeBlob: stream.Write((byte[])@object); break;
                case EnumVar.VtypeDate: WriteDateTime((DateTime)@object, stream); break;
                case EnumVar.VtypeGuid: stream.Write(((Guid)@object).ToByteArray()); break;
                case EnumVar.VtypeAutoWrap:
                    stream.Write(((AutoWrapClient)@object).Target);
                    break;
            }
            return true;
        }

        private static byte[] ReadByteArray(BinaryReader stream) => stream.ReadBytes(stream.ReadInt32());

        private static DateTime ReadDateTime(BinaryReader stream) => DateTime.FromBinary(stream.ReadInt64());

        private static void WriteDateTime(DateTime value, BinaryWriter stream) => stream.Write(value.ToBinary());
    }
}