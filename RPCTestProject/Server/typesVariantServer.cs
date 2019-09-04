using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using System.Net;
namespace ServerRPC
{

   
    
    //    struct _tVariant
    //    {
    //        _ANONYMOUS_UNION union
    //        {
    //            int8_t i8Val;
    //            int16_t shortVal;
    //            int32_t lVal;
    //        int intVal;
    //        unsigned int uintVal;
    //        int64_t llVal;
    //        uint8_t ui8Val;
    //        uint16_t ushortVal;
    //        uint32_t ulVal;
    //        uint64_t ullVal;
    //        int32_t errCode;
    //        long hRes;
    //        float fltVal;
    //        double dblVal;
    //        bool bVal;
    //        char chVal;
    //        wchar_t wchVal;
    //        DATE date;
    //        IID IDVal;
    //        struct _tVariant *pvarVal;
    //        struct tm      tmVal;
    //        _ANONYMOUS_STRUCT struct
    //        {
    //            void* pInterfaceVal;
    //        IID InterfaceID;
    //    }
    //    __VARIANT_NAME_2/*iface*/;
    //        _ANONYMOUS_STRUCT struct
    //        {
    //            char* pstrVal;
    //    uint32_t strLen; //count of bytes
    //}
    //__VARIANT_NAME_3/*str*/;
    //        _ANONYMOUS_STRUCT struct
    //        {
    //            WCHAR_T* pwstrVal;
    //uint32_t wstrLen; //count of symbol
    //        } __VARIANT_NAME_4/*wstr*/;
    //    } __VARIANT_NAME_1;
    //    uint32_t cbElements;    //Dimension for an one-dimensional array in pvarVal
    //TYPEVAR vt;
    //};
    public enum EnumVar:byte
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

    public class WorkWhithVariant
    {
        internal static Dictionary<Type, EnumVar> MatchTypes;
        
        static WorkWhithVariant()
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
                
                //,
             //   {typeof(AutoWrap),EnumVar.VTYPE_JSObject}
            };


        }

     public static DateTime ReadDateTime(BinaryReader stream)
        {
            long nVal = stream.ReadInt64();
            //get 64bit binary
            return DateTime.FromBinary(nVal);


        }

        public static void WriteDateTime(DateTime value,BinaryWriter stream)
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
      public static  object GetObject(BinaryReader stream)
        {
            
            EnumVar тип =(EnumVar)stream.ReadByte();

            switch (тип)
            {
                case EnumVar.VTYPE_EMPTY:
                case EnumVar.VTYPE_NULL: return null;
                case EnumVar.VTYPE_I2: return stream.ReadInt16();
                case EnumVar.VTYPE_I4: return stream.ReadInt32();
                case EnumVar.VTYPE_R4: return stream.ReadSingle();
                case EnumVar.VTYPE_R8: return stream.ReadDouble();
                case EnumVar.VTYPE_Decimal: return stream.ReadDecimal();
                case EnumVar.VTYPE_BOOL:return stream.ReadBoolean();
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
                        var Target= stream.ReadInt32();
                        var AW = NetObjectToNative.AutoWrap.ObjectsList.GetValue(Target);


                     return AW.O;
            }
            return null;
            }


    
        public static bool WriteObject(object Объект, BinaryWriter stream)
        {

            

            if (Объект == null)
            {

                stream.Write((byte)EnumVar.VTYPE_NULL);
                 return true;

            }


            EnumVar type;

            var res = MatchTypes.TryGetValue(Объект.GetType(), out type);

            if (!res) return false;


            stream.Write((byte)type);
            switch (type)
            {
                case EnumVar.VTYPE_I2:  stream.Write((Int16) Объект); break;
                case EnumVar.VTYPE_I4: stream.Write((Int32)Объект); break;
                case EnumVar.VTYPE_R4: stream.Write((float)Объект); break;
                case EnumVar.VTYPE_R8: stream.Write((double)Объект); break;
                case EnumVar.VTYPE_Decimal: stream.Write((decimal)Объект); break;
                case EnumVar.VTYPE_BOOL: stream.Write((bool)Объект); break;
                case EnumVar.VTYPE_I1:  stream.Write((sbyte)Объект); break;
                case EnumVar.VTYPE_UI1: stream.Write((byte)Объект); break;
                case EnumVar.VTYPE_UI2: stream.Write((UInt16)Объект); break;

                case EnumVar.VTYPE_UI4: stream.Write((UInt32)Объект); break;

                case EnumVar.VTYPE_I8: stream.Write((Int64)Объект); break;
                case EnumVar.VTYPE_UI8: stream.Write((UInt64)Объект); break;
                case EnumVar.VTYPE_CHAR: stream.Write((char)Объект); break;
                case EnumVar.VTYPE_PWSTR: stream.Write((string)Объект); break;

                case EnumVar.VTYPE_BLOB: stream.Write((byte[])Объект); break;
                case EnumVar.VTYPE_DATE: WriteDateTime((DateTime)Объект, stream);  break;
                case EnumVar.VTYPE_GUID: stream.Write(((Guid)Объект).ToByteArray()); break;
                case EnumVar.VTYPE_AutoWrap: stream.Write(((NetObjectToNative.AutoWrap)Объект).IndexInStorage);
                    break;
                    
            }
            return true;
        }

    }

   

}

