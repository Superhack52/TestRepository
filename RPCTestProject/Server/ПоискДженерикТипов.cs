using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
namespace NetObjectToNative
{

    public class ДанныеОДженерикМетоде
    {

        Type[] GenericArguments;
        List<List<int>> ПараметрыДляВыводаТипа;
        bool МожноВывести;
        internal MethodInfo MI;
        ИнформацияОТипеПараметра[] Параметры;

        public ДанныеОДженерикМетоде(MethodInfo MI, ИнформацияОТипеПараметра[] Параметры)
        {
            this.Параметры = Параметры;
            GenericArguments = MI.GetGenericArguments();
            МожноВывести = НайтиПараметрыДляВывода();
            this.MI = MI;

        }


        public static bool CanCreateInstanceUsingDefaultConstructor(TypeInfo type)
        {

            return type.IsValueType || (!type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null);
        }

        private static bool ContainsAnyFlag(GenericParameterAttributes attributes, GenericParameterAttributes flags)
        {
            return (attributes & flags) != GenericParameterAttributes.None;
        }

        public static bool ПроверитьПараметрНаОграничения(TypeInfo t, TypeInfo параметр)
        {
            // List the variance and special constraint flags. 

            GenericParameterAttributes gpa = t.GenericParameterAttributes;
            GenericParameterAttributes variance = gpa &
                GenericParameterAttributes.VarianceMask;

            // Select the variance flags.
            //  if (variance == GenericParameterAttributes.None)
            //      return true;
            //else
            //{
            //    if ((variance & GenericParameterAttributes.Covariant) != 0)
            //        retval = "Covariant;";
            //    else
            //        retval = "Contravariant;";
            //}


            GenericParameterAttributes constraints = gpa &
                GenericParameterAttributes.SpecialConstraintMask;

            if (constraints == GenericParameterAttributes.None)
                return true;
            else
            {
                if (ContainsAnyFlag(constraints, GenericParameterAttributes.ReferenceTypeConstraint))
                {
                    if (!параметр.IsClass && !параметр.IsInterface)
                    {
                        return false;
                    }
                }


                if (ContainsAnyFlag(constraints, GenericParameterAttributes.NotNullableValueTypeConstraint))
                {
                    if (!параметр.IsValueType)
                    {
                        return false;
                    }

                    if (параметр.IsGenericType && параметр.GetGenericTypeDefinition() != typeof(Nullable<>))
                    {
                        return false;
                    }
                }

                if (ContainsAnyFlag(constraints, GenericParameterAttributes.DefaultConstructorConstraint))
                {
                    if (!CanCreateInstanceUsingDefaultConstructor(параметр))
                    {
                        return false;
                    }
                }

            }

            return true;
        }



        public static bool ПодходитДженерикПараметр(Type ДженерикТип, Type тип)
        {

            var TI = ДженерикТип.GetTypeInfo();

            if (ДженерикТип.IsConstructedGenericType && TI.ContainsGenericParameters)
                return тип.IsGenericTypeOf(TI, ДженерикТип);

            if (!TI.IsGenericParameter) return false;

            bool ЕстьОграничения = false;
            Type[] tpConstraints = TI.GetGenericParameterConstraints();
            foreach (Type tpc in tpConstraints)
            {
                ЕстьОграничения = true;
                var tpcTI = tpc.GetTypeInfo();

                if (tpcTI.ContainsGenericParameters)
                {
                    if (ПодходитДженерикПараметр(tpc,тип)) return true;

                }
                else if (tpc.IsAssignableFrom(тип)) return true;

            }

            if (ЕстьОграничения) return false;



            return ПроверитьПараметрНаОграничения(TI, тип.GetTypeInfo());
        }
        public bool НайтиПараметрыДляВывода()
        {

            bool res = true;
            ПараметрыДляВыводаТипа = new List<List<int>>();


            foreach (Type tParam in GenericArguments)
            {
                var matchingParam = false;
                var index = new List<int>();
                ПараметрыДляВыводаТипа.Add(index);
                int i = 0;
                foreach (var Param in Параметры)
                {

                    if (tParam.IsSimilarType(Param.Тип))
                    {
                        matchingParam = true;
                        index.Add(i);

                    }
                    i++;

                }

                if (!matchingParam)
                {
                    // ПараметрыДляВыводаТипа = null;
                    // return false;
                    res = false;
                }
            }
            return res;
        }


        static Type GetRealType(Type ТипАргумента, Type ТипПарамтра, Type RealType)
        {
            // Ignore any 'ref' types




            if (ТипАргумента == ТипПарамтра)
                return RealType;

            // Handle array types
            if (ТипПарамтра.IsArray)
                return GetRealType(ТипАргумента, ТипПарамтра.GetElementType(), RealType.GetElementType());

            // Handle any generic arguments
            if (ТипПарамтра.GetTypeInfo().IsGenericType)
            {
                Type[] arguments = ТипПарамтра.GetTypeInfo().GetGenericArguments();
                Type[] argumentsReal = RealType.GetTypeInfo().GetGenericArguments();
                for (int i = 0; i < arguments.Length; ++i)
                {

                    var res = GetRealType(ТипАргумента, arguments[i], argumentsReal[i]);
                    if (res != null)
                        return res;
                }


            }

            return null;
        }


        private Type[] ВывестиТипы(Type[] параметры)
        {
            // Сравним параметры без вывода

            int Length = Math.Min(параметры.Length, Параметры.Length);

            for (var i = 0; i < Length; i++)
            {


                var параметр = параметры[i];

                if (параметр == null) continue;

                var Параметр = Параметры[i];
                var ТипПараметра = Параметр.Тип;
                bool подходит = false;


                if (Параметр.IsGenericType)
                {
                    if (параметр.GetTypeInfo().IsGenericType)
                        подходит = параметр.IsGenericTypeOf(ТипПараметра.GetTypeInfo(), ТипПараметра);
                    else
                        подходит = ПодходитДженерикПараметр(ТипПараметра, параметр);
                }
                else
                    подходит = Параметр.Равняется(параметр);


                if (!подходит) return null;
            }

            var РеальныеТипыАргументов = new Type[GenericArguments.Length];

            for (var i = 0; i < GenericArguments.Length; i++)
            {
                var аргумент = GenericArguments[i];
                var ИндексыПараметровДляВывода = ПараметрыДляВыводаТипа[i];

                if (ИндексыПараметровДляВывода.Count == 0)
                    continue;

                foreach (var индекс in ИндексыПараметровДляВывода)
                {

                    var РеальныйТип = GetRealType(аргумент, Параметры[индекс].Тип, параметры[индекс]);

                    if (РеальныйТип != null)
                    {

                        if (РеальныеТипыАргументов[i] != null && РеальныеТипыАргументов[i] != РеальныйТип) return null;

                        РеальныеТипыАргументов[i] = РеальныйТип;

                    }


                }

                if (РеальныеТипыАргументов[i] == null) return null;

            }

            return РеальныеТипыАргументов;


        }
        public MethodInfo ПолучитьРеальныйМетод(Type[] параметры)
        {

            if (!МожноВывести) return null;

            int Length = Параметры.Length;
            // Сравним параметры без вывода
            for (var i = 0; i < Length; i++)
            {
                var параметр = параметры[i];
                var Параметр = Параметры[i];
                var ТипПараметра = Параметр.Тип;
                bool подходит = false;
                if (Параметр.IsGenericType)
                {
                    if (параметр.GetTypeInfo().IsGenericType)
                        подходит = параметр.IsGenericTypeOf(ТипПараметра.GetTypeInfo(), ТипПараметра);
                    else
                        подходит = ПодходитДженерикПараметр(ТипПараметра, параметр);
                }
                else
                    подходит = Параметр.Равняется(параметр);


                if (!подходит) return null;
            }

            var РеальныеТипыАргументов = new Type[GenericArguments.Length];

            for (var i = 0; i < GenericArguments.Length; i++)
            {
                var аргумент = GenericArguments[i];
                var ИндексыПараметровДляВывода = ПараметрыДляВыводаТипа[i];

                foreach (var индекс in ИндексыПараметровДляВывода)
                {

                    var РеальныйТип = GetRealType(аргумент, Параметры[индекс].Тип, параметры[индекс]);

                    if (РеальныйТип != null)
                    {

                        if (РеальныеТипыАргументов[i] != null && РеальныеТипыАргументов[i] != РеальныйТип) return null;

                        РеальныеТипыАргументов[i] = РеальныйТип;

                    }


                }

                if (РеальныеТипыАргументов[i] == null) return null;

            }

            try
            {
                var res = MI.MakeGenericMethod(РеальныеТипыАргументов);
                return res;
            }
            catch (Exception)
            {
                return null;


            }
        }


        bool CompareParam(Type параметр, ИнформацияОТипеПараметра Параметр)
        {

            var ТипПараметра = Параметр.Тип;
            bool подходит = false;
            if (Параметр.IsGenericType)
            {
                if (параметр.GetTypeInfo().IsGenericType)
                    подходит = параметр.IsGenericTypeOf(ТипПараметра.GetTypeInfo(), ТипПараметра);
                else
                    подходит = ПодходитДженерикПараметр(ТипПараметра, параметр);
            }
            else
                подходит = Параметр.Равняется(параметр);


            if (!подходит) return false;

            return true;
        }
        public MethodInfo ПолучитьРеальныйМетодСПарамс(Type[] параметры, ИнфoрмацияОМетоде ИОМ)
        {

            
            if (!МожноВывести) return null;

            if (ИОМ.HasDefaultValue  )
            {
                if ((параметры.Length < ИОМ.FirstDefaultParams) || параметры.Length > ИОМ.КоличествоПараметров)
                    return null;

            }
           int Length =Math.Min(Параметры.Length, параметры.Length);
             if (ИОМ.hasParams)
            {
                Length = ИОМ.КоличествоПараметров-1;

                if (параметры.Length>= ИОМ.КоличествоПараметров)
                {

                    if (!CompareParam(параметры[параметры.Length - 1], ИОМ.ИнформацияОТипеЭлемента))
                        return null;
                }

            }

            

                // Сравним параметры без вывода
                for (var i = 0; i < Length; i++)
            {
                var параметр = параметры[i];
                var Параметр = Параметры[i];

                if (!CompareParam(параметр, Параметр)) return null;

            }

            var РеальныеТипыАргументов = new Type[GenericArguments.Length];

            for (var i = 0; i < GenericArguments.Length; i++)
            {
                var аргумент = GenericArguments[i];
                var ИндексыПараметровДляВывода = ПараметрыДляВыводаТипа[i];

                foreach (var индекс in ИндексыПараметровДляВывода)
                {

                    var РеальныйТип = GetRealType(аргумент, Параметры[индекс].Тип, параметры[индекс]);

                    if (РеальныйТип != null)
                    {

                        if (РеальныеТипыАргументов[i] != null && РеальныеТипыАргументов[i] != РеальныйТип) return null;

                        РеальныеТипыАргументов[i] = РеальныйТип;

                    }


                }

                if (РеальныеТипыАргументов[i] == null) return null;

            }

            try
            {
                var res = MI.MakeGenericMethod(РеальныеТипыАргументов);
                return res;
            }
            catch (Exception)
            {
                return null;


            }
        }


        public MethodInfo ПолучитьРеальныйМетод2(Type[] ДженерикТипы, Type[] параметры)
        {
            if (ДженерикТипы.Length != GenericArguments.Length)
                return null;

            var РеальныеТипыАргументов =  ВывестиТипы(параметры);
            if (РеальныеТипыАргументов == null) return null;

            for (int i = 0; i < ДженерикТипы.Length; i++)
            {
                var тип = РеальныеТипыАргументов[i];
                if (тип != null && тип != ДженерикТипы[i])
                    return null;

            }


            try
            {
                var res = MI.MakeGenericMethod(ДженерикТипы);
                return res;
            }
            catch (Exception)
            {
                return null;


            }
        }

    }



    public static class ПоискДженерикТипов
    {





        public static bool IsGenericTypeOf(this Type t, TypeInfo TI, Type genericDefinition)
        {
            Type InterfaceType;
            return IsGenericTypeOf(t, genericDefinition, TI, out InterfaceType);

        }
        public static bool IsGenericTypeOf(this Type t, Type genericDefinition, TypeInfo TI, out Type InterfaceType)
        {
            InterfaceType = null;
            if (t == null) return false;



            if (t.GetTypeInfo().IsGenericType && t.GetGenericTypeDefinition() == genericDefinition.GetGenericTypeDefinition()) return true;

            if (t.GetTypeInfo().BaseType != null)
            {
                var BT = t.GetTypeInfo().BaseType;
                if (BT != null && BT.IsGenericTypeOf(genericDefinition, TI, out InterfaceType))
                {
                    if (InterfaceType == null) InterfaceType = BT;
                    return true;
                }
            }

            if (TI.IsInterface)
            {

                foreach (var i in t.GetTypeInfo().GetInterfaces())
                {
                    if (i.IsGenericTypeOf(genericDefinition, TI, out InterfaceType))
                    {
                        if (InterfaceType == null) InterfaceType = i;
                        return true;

                    }
                }
            }


            return false;
        }



        public static bool IsSimilarType(this Type thisType, Type type)
        {
            // Ignore any 'ref' types

            if (type.IsByRef)
                type = type.GetElementType();

            // Handle array types
            if (type.IsArray)
                return thisType.IsSimilarType(type.GetElementType());

            if (thisType == type)
                return true;

            // Handle any generic arguments
            if (type.GetTypeInfo().IsGenericType)
            {
                Type[] arguments = type.GetTypeInfo().GetGenericArguments();

                for (int i = 0; i < arguments.Length; ++i)
                {
                    if (thisType.IsSimilarType(arguments[i]))
                        return true;
                }


            }

            return false;
        }


        public static bool НайтиПараметрыДляВывода(this MethodInfo methodInfo, out List<List<int>> res)
        {

            res = new List<List<int>>();
            var genericTypes = methodInfo.GetGenericArguments();
            ParameterInfo[] parameterInfos = methodInfo.GetParameters();

            foreach (Type tParam in genericTypes)
            {
                var matchingParam = false;
                var index = new List<int>();
                res.Add(index);
                int i = 0;
                foreach (var Param in parameterInfos)
                {

                    if (tParam.IsSimilarType(Param.ParameterType))
                    {
                        matchingParam = true;
                        index.Add(i);

                    }
                    i++;

                }

                if (!matchingParam)
                {
                    res = null;
                    return false;

                }
            }
            return true;
        }

    }
}
