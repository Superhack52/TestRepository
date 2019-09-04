using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace NetObjectToNative
{

   

    public class ИнформацияОТипеПараметра: IComparable<ИнформацияОТипеПараметра>
    {
        public Type Тип;
        bool IsByRef;
        bool IsValue;
        int УровеньИерархии;
        bool IsNullable;
        public bool IsGenericType;
        public ИнформацияОТипеПараметра(Type type)
        {
            var TI = type.GetTypeInfo();
            IsByRef = TI.IsByRef;

            IsGenericType = (TI.IsGenericType && TI.IsGenericTypeDefinition) || TI.ContainsGenericParameters;

            if (IsByRef)
            {
                Тип = type.GetElementType();
                TI = Тип.GetTypeInfo();
                
            }
            else
                Тип = type;


            IsValue = TI.IsValueType;

            if (IsValue)
            {
                УровеньИерархии = 0;
                if (TI.IsGenericType && TI.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    IsNullable = true;

                    Тип = TI.GenericTypeArguments[0];
                }

            }
            else
                УровеньИерархии = НайтиУровень(0, Тип);


        }

        static int НайтиУровень(int Уровень, Type type)
        {
            if (type == null)
                return -1;// всякие char*

            if ( type == typeof(object))
                return Уровень;

            return НайтиУровень(Уровень + 1, type.GetTypeInfo().BaseType);

        }

       
        public  int CompareTo(ИнформацияОТипеПараметра elem)
        {
           

            int res = -IsByRef.CompareTo(elem.IsByRef);

            if (res != 0) return res;

            if (Тип == elem.Тип)
                return 0;

            res = -IsValue.CompareTo(elem.IsValue);

            if (res != 0) return res;


            if (IsValue && elem.IsValue)
            {
                res = IsNullable.CompareTo(elem.IsNullable);

                if (res != 0) return res;

            }

            res = -УровеньИерархии.CompareTo(elem.УровеньИерархии);

            if (res != 0) return res;

           

            return Тип.ToString().CompareTo(elem.Тип.ToString());
        }

        public bool Равняется(Type type)
        {

            if (type==null)
            {
                if (!IsValue)
                    return true;

                if (IsNullable)
                    return true;
                else
                    return false;

            }

            // или использовать IsInstanceOfType
            if (IsValue) return Тип == type;

                return Тип.GetTypeInfo().IsAssignableFrom(type);

        }
    }


    

    public class ИнфoрмацияОМетоде
    {
        public MethodInfo Method;
        public ИнформацияОТипеПараметра[] Параметры;
        public int КоличествоПараметров;
        public bool hasParams;
        public bool HasDefaultValue;
        public int FirstDefaultParams;
        public Type TypeParams;
        public int КоличествоПараметровПарамс;
        public ИнформацияОТипеПараметра ИнформацияОТипеЭлемента;
        public bool IsGeneric;
        public ДанныеОДженерикМетоде ДженерикМетод;
        public Type ReturnType;

       public ИнфoрмацияОМетоде(MethodInfo MI)
        {
            Method = MI;

            ParameterInfo[] parameters = Method.GetParameters();
            hasParams = false;
            КоличествоПараметров = parameters.Length;
            КоличествоПараметровПарамс = 0;
            if (КоличествоПараметров > 0)
            { 
                hasParams = parameters[parameters.Length - 1].GetCustomAttributes(typeof(ParamArrayAttribute), false).GetEnumerator().MoveNext();
            }

            if (hasParams)
            {
                TypeParams = parameters[parameters.Length - 1].ParameterType.GetElementType();
                ИнформацияОТипеЭлемента = InformationOnTheTypes.ПолучитьИнформациюОТипе(TypeParams);

            }

            Параметры = new ИнформацияОТипеПараметра[КоличествоПараметров];

            for(int i=0;i< parameters.Length;i++)
            {
               var param = parameters[i];
                Параметры[i] = InformationOnTheTypes.ПолучитьИнформациюОТипе(param.ParameterType);

                if (!HasDefaultValue && param.HasDefaultValue)
                {
                    HasDefaultValue = true;

                    FirstDefaultParams = i;

                }

            }

            IsGeneric = MI.IsGenericMethod && MI.IsGenericMethodDefinition;

            if (IsGeneric)
                ДженерикМетод = new ДанныеОДженерикМетоде(Method, Параметры);

            ReturnType = MI.ReturnType.GetTypeInfo().IsInterface ? MI.ReturnType : null;

        }

       public ИнфoрмацияОМетоде(ИнфoрмацияОМетоде ИМ, int КолПарам)
        {
            Method = ИМ.Method;
            КоличествоПараметров = КолПарам;
            КоличествоПараметровПарамс = ИМ.КоличествоПараметров;
            ReturnType = ИМ.ReturnType;

            Параметры = new ИнформацияОТипеПараметра[КолПарам];

            var count = ИМ.HasDefaultValue ? КолПарам : КоличествоПараметровПарамс - 1;
            for (int i = 0; i < count; i++)
            {

                Параметры[i] = ИМ.Параметры[i];

            }

            if (ИМ.HasDefaultValue)
            {

                HasDefaultValue = true;
                FirstDefaultParams = ИМ.FirstDefaultParams;
                return;

            }

            

            hasParams = true;
            TypeParams = ИМ.TypeParams;
            ИнформацияОТипеЭлемента = ИМ.ИнформацияОТипеЭлемента;

           

            
            var ИОТ= InformationOnTheTypes.ПолучитьИнформациюОТипе(ИМ.TypeParams);

            for (int i = КоличествоПараметровПарамс - 1; i < КолПарам; i++)
            {

                Параметры[i] = ИОТ;

            }
        }

        // Добавить парамс как обычный метод
        public ИнфoрмацияОМетоде(ИнфoрмацияОМетоде ИМ)
        {
            Method = ИМ.Method;
            КоличествоПараметров = ИМ.КоличествоПараметров;
            КоличествоПараметровПарамс = 0;
            hasParams = false;
            HasDefaultValue = false;
            ReturnType = ИМ.ReturnType;
            Параметры = ИМ.Параметры;
        }

        public bool Сравнить(Type[] параметры)
        {

            for (int i = 0; i < параметры.Length; i++)
            {

                if (!Параметры[i].Равняется(параметры[i]))
                      return false;

            }

            return true;
        }


       public bool СравнитьСпараметрамиПоУмолчанию(Type[] параметры)
        {
            if ((параметры.Length < FirstDefaultParams) || параметры.Length > КоличествоПараметров)
                return false;

            return Сравнить(параметры);




        }
        public bool СравнитьПарамс(Type[] параметры)
        {

            if (HasDefaultValue)
                return СравнитьСпараметрамиПоУмолчанию(параметры);

           var ПоследнийПарам = КоличествоПараметров - 1;

            if (параметры.Length < ПоследнийПарам)
                return false;

            for (int i = 0; i < ПоследнийПарам; i++)
            {

                if (!Параметры[i].Равняется(параметры[i]))
                    return false;

            }

            
            if (параметры.Length== КоличествоПараметров && параметры[ПоследнийПарам] == Параметры[КоличествоПараметров - 1].Тип)
                return true;

            
            for (int i = ПоследнийПарам; i < параметры.Length; i++)
            {

                if ( !ИнформацияОТипеЭлемента.Равняется(параметры[i]))
                    return false;

            }

            return true;
        }


        public object Invoke(object Target, object[] input)
        {

          
                    return Method.Invoke(Target, input);
              
        }

        

        public object ВыполнитьМетодСДефолтнымиПараметрами(object Target, object[] input,int КолПарам)
        {

           
                if (input.Length == КолПарам)
                    return Invoke(Target, input);


                object[] параметры = new object[КолПарам];
            ParameterInfo[] parameters = Method.GetParameters();
            Array.Copy(input, параметры, input.Length);
                
                for (int i = input.Length; i < parameters.Length; i++)
                {
                параметры[i] = parameters[i].RawDefaultValue;
                }
           
                var res= Invoke(Target, параметры);

            Array.Copy(параметры, input, input.Length);

            if (res != null && ReturnType != null) res = new AutoWrap(res, ReturnType);
            return res;
        }
        public object ExecuteMethod(object Target, object[] input)
        {

          
            if (!(hasParams || HasDefaultValue))
                  return Invoke(Target, input);

            int КолПарам = (КоличествоПараметровПарамс > 0) ? КоличествоПараметровПарамс : КоличествоПараметров;

            if (HasDefaultValue)
            {
                return ВыполнитьМетодСДефолтнымиПараметрами(Target, input, КолПарам);
            }

           

            int последняяПозиция = КолПарам - 1;

                object[] realParams = new object[КолПарам];
                for (int i = 0; i < последняяПозиция; i++)
                    realParams[i] = input[i];

                
                Array массивПараметров = Array.CreateInstance(TypeParams, input.Length - последняяПозиция);
                for (int i = 0; i < массивПараметров.Length; i++)
                массивПараметров.SetValue(input[i + последняяПозиция], i);

                realParams[последняяПозиция] = массивПараметров;



            var res= Invoke(Target, realParams);

            массивПараметров = (Array)realParams[последняяПозиция];
            for (int i = 0; i < массивПараметров.Length; i++)
                input[i + последняяПозиция] = массивПараметров.GetValue(i);

            if (res != null && ReturnType != null) res = new AutoWrap(res, ReturnType);
            return res;


        }

    }
}
