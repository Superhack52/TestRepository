using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
namespace NetObjectToNative
{
    public interface IPropertyOrFieldInfo
    {

        object GetValue(object obj);
        void SetValue(object obj, object value);

        Type GetPropertyType();
    }


    public class FieldInfoAction : IPropertyOrFieldInfo
    {

       internal FieldInfo FI;
        // Нужен для присвоения типа интерфейса
       internal Type ReturnType;
       
        public FieldInfoAction(FieldInfo FI)
        {

            this.FI = FI;
            ReturnType = FI.FieldType.GetTypeInfo().IsInterface ? FI.FieldType : null;
        }

        Type IPropertyOrFieldInfo.GetPropertyType()

        {
           return FI.FieldType;
        }

        object IPropertyOrFieldInfo.GetValue(object obj)
        {
            var res= FI.GetValue(obj);
            if (res!=null && ReturnType != null) res = new AutoWrap(res, ReturnType);
            return res;
        }

        void IPropertyOrFieldInfo.SetValue(object obj, object value)
        {
            FI.SetValue(obj, value);
        }

        
    }

    public class PropertyInfoAction : IPropertyOrFieldInfo
    {

        internal PropertyInfo PI;
        // Нужен для присвоения типа интерфейса
        internal Type ReturnType;
        public PropertyInfoAction(PropertyInfo PI)
        {

            this.PI = PI;
            ReturnType = PI.PropertyType.GetTypeInfo().IsInterface ? PI.PropertyType : null;
        }
        object IPropertyOrFieldInfo.GetValue(object obj)
        {
            var res= PI.GetValue(obj);
            if (res != null &&  ReturnType != null) res = new AutoWrap(res, ReturnType);
            return res;
        }

        void IPropertyOrFieldInfo.SetValue(object obj, object value)
        {
            PI.SetValue(obj, value);
        }

        Type IPropertyOrFieldInfo.GetPropertyType()
        {
            return PI.PropertyType;
        }
    }
   public class СвойстваИМетодыПоТипу
    {

        Type Тип;
        public  Dictionary<String, IPropertyOrFieldInfo> Свойства = new Dictionary<string, IPropertyOrFieldInfo>();
        public  Dictionary<String, AllMethodsForName> Методы = new Dictionary<string, AllMethodsForName>();
        //ВсеМетодыПоИмени<ConstructorInfo> Конструкторы=null;

        public Dictionary<String, string> Синонимы = null;
        public СвойстваИМетодыПоТипу(Type Тип)
        {

            this.Тип = Тип;

        }

        internal void ПроверитьНаСиноним(ref string СинонимИлиИмяМетода)
        {
            string ИмяМетода;
            if (Синонимы!=null && Синонимы.TryGetValue(СинонимИлиИмяМетода, out ИмяМетода))
            {
                СинонимИлиИмяМетода = ИмяМетода;
            }


        }

        public void УстановитьСиноним(string Синоним, string ИмяМетода)
        {


            if (Синонимы == null)
                 Синонимы = new Dictionary<string, string>();

            Синонимы[Синоним] = ИмяМетода;




        }
        public   AllMethodsForName НайтиВсеМетодыПоИмени(string ИмяМетода)
        {
            ПроверитьНаСиноним(ref ИмяМетода);
            AllMethodsForName всеМетодыПоИмени;
            if (!Методы.TryGetValue(ИмяМетода, out всеМетодыПоИмени))
            {
               var методы= Тип.GetTypeInfo().GetMethods().Where(x => x.Name == ИмяМетода).ToArray();

                if (методы.Length == 0) return null;

                всеМетодыПоИмени = new AllMethodsForName(методы);
                Методы[ИмяМетода] = всеМетодыПоИмени;

            }

            return всеМетодыПоИмени;
        }


        public  ИнфoрмацияОМетоде НайтиМетод(String ИмяМетода, bool IsStatic,object[] ПараметрыМетода)
        {

            var всеМетодыПоИмени = НайтиВсеМетодыПоИмени(ИмяМетода);
            if (всеМетодыПоИмени==null) return null;

            return всеМетодыПоИмени.НайтиМетод(IsStatic,ПараметрыМетода);

        }

        public ИнфoрмацияОМетоде НайтиДженерикМетод(String ИмяМетода, bool IsStatic,Type[] ДженерикПараметры, Type[] ПараметрыМетода)
        {

            var всеМетодыПоИмени = НайтиВсеМетодыПоИмени(ИмяМетода);
            if (всеМетодыПоИмени == null) return null;

            return всеМетодыПоИмени.НайтиДженерикМетод2(IsStatic, ДженерикПараметры, ПараметрыМетода);

        }

        public  int КоличествоПараметровМетода(String ИмяМетода)
        {

            var всеМетодыПоИмени = НайтиВсеМетодыПоИмени(ИмяМетода);
            if (всеМетодыПоИмени == null) return -1;

            return всеМетодыПоИмени.MaxParamCount;

        }

        public  IPropertyOrFieldInfo GetInfoMember(String ИмяСвойства)
        {
            ПроверитьНаСиноним(ref ИмяСвойства);
            var members = Тип.GetTypeInfo().GetMember(ИмяСвойства);
            
            if (members.Length >0)
            {

                var mi = members[0];

                FieldInfo fi = mi as FieldInfo;

                if (fi != null) return new FieldInfoAction(fi);

                PropertyInfo pi = mi as PropertyInfo;
                if (pi != null) return new PropertyInfoAction(pi);
            }

            return null;

        }
        public   IPropertyOrFieldInfo НайтиСвойствоПоИмени(String ИмяСвойства)
        {
            ПроверитьНаСиноним(ref ИмяСвойства);
            IPropertyOrFieldInfo свойство;
            if (!Свойства.TryGetValue(ИмяСвойства, out свойство))
            {

                свойство = GetInfoMember(ИмяСвойства);

                if (свойство == null)   return null;


                Свойства[ИмяСвойства] = свойство;

            }

            return свойство;
        }

      


       
    }

    public static class InformationOnTheTypes
    {

        public static Dictionary<Type, СвойстваИМетодыПоТипу> СвойстваИМетодыПоТипам = new Dictionary<Type, СвойстваИМетодыПоТипу>();
        public static Dictionary<Type, ИнформацияОТипеПараметра> ИнформацияПоТипу = new Dictionary<Type, ИнформацияОТипеПараметра>();

        public static ИнформацияОТипеПараметра ПолучитьИнформациюОТипе(Type type)
        {
            ИнформацияОТипеПараметра ИТ = null;
            if (!ИнформацияПоТипу.TryGetValue(type, out ИТ))
            {
                ИТ = new ИнформацияОТипеПараметра(type);
                ИнформацияПоТипу[type] = ИТ;
            }
            return ИТ;
        }


        
        public static СвойстваИМетодыПоТипу НайтиСвойстваИМетодыПоТипу(Type Тип)
        {
            СвойстваИМетодыПоТипу смт = null;
            if (!СвойстваИМетодыПоТипам.TryGetValue(Тип, out смт))
            {
                смт = new СвойстваИМетодыПоТипу(Тип);
                СвойстваИМетодыПоТипам[Тип] = смт;
            }
            return смт;



        }


      public static string ИмяМетодаПоСинониму(Type Тип,String СинонимИлиИмяМетода)
        {
            СвойстваИМетодыПоТипу смт = null;
            if (!СвойстваИМетодыПоТипам.TryGetValue(Тип, out смт)) return СинонимИлиИмяМетода;

            смт.ПроверитьНаСиноним(ref СинонимИлиИмяМетода);

            return СинонимИлиИмяМетода;

        }

        public static void УстановитьСиноним(Type Тип, string Синоним,string ИмяМетода)
        {
            var свойстваИМетодыПоТипу = НайтиСвойстваИМетодыПоТипу(Тип);
            свойстваИМетодыПоТипу.УстановитьСиноним(Синоним, ИмяМетода);


        }
        public static ИнфoрмацияОМетоде FindMethod(Type Тип,bool IsStatic, string ИмяМетода,params object[] ПараметрыМетода)
        {

           var свойстваИМетодыПоТипу= НайтиСвойстваИМетодыПоТипу(Тип);

            return свойстваИМетодыПоТипу.НайтиМетод(ИмяМетода, IsStatic, ПараметрыМетода);

        }

        public static ИнфoрмацияОМетоде FindGenericMethodsWithGenericArguments(Type Тип, bool IsStatic, string MethodName, Type[] GenericArguments, Type[] MethodParams)
        {

            var свойстваИМетодыПоТипу = НайтиСвойстваИМетодыПоТипу(Тип);
            
            return свойстваИМетодыПоТипу.НайтиДженерикМетод(MethodName, IsStatic, GenericArguments,MethodParams);

        }

        public static int КоличествоПараметровДляМетода(Type Тип, string ИмяМетода)
        {

            var свойстваИМетодыПоТипу = НайтиСвойстваИМетодыПоТипу(Тип);
            return свойстваИМетодыПоТипу.КоличествоПараметровМетода(ИмяМетода);


        }
        public static IPropertyOrFieldInfo НайтиСвойство(Type Тип,string ИмяСвойства)
        {

            var свойстваИМетодыПоТипу = НайтиСвойстваИМетодыПоТипу(Тип);

            return свойстваИМетодыПоТипу.НайтиСвойствоПоИмени(ИмяСвойства);

        }

    }
}
