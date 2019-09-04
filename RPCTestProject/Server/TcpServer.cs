using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Linq.Expressions;
namespace ServerRPC
{
    public enum CallMethod : byte
    {
        CallFunc = 0,
        GetMember,
        SetMember,
        CallFuncAsync,
        CallDelegate,
        CallGenericFunc,
        GetWrapperForObjectWithEvents,
        SetIndex,
        GetIndex,
        CallBinaryOperation,
        CallUnaryOperation,
        IteratorNext,
        DeleteObjects,
        CloseServer

    }


    public class TcpAsyncCallBack
    {
        public IPEndPoint endPoint;
        Guid Key;
        internal TcpAsyncCallBack(Guid Key,IPAddress adress, int Port)
        {

            endPoint= new IPEndPoint(adress, Port);
            this.Key = Key;
           }


        void SendStream(MemoryStream stream)
        {

            using (var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                client.Connect(endPoint);
                //      client.NoDelay = true;

                using (var ns = new NetworkStream(client))
                {
                    stream.Position = 0;
                    ns.Write(BitConverter.GetBytes((Int32)stream.Length), 0, 4);
                    stream.CopyTo(ns);


                }

            }
        }


        internal void SendAsyncMessage(bool Successfully, object Result)
        {
            MemoryStream stream = new MemoryStream();
            var bw = new BinaryWriter(stream);
            bw.Write((byte)0);
            bw.Write(Key.ToByteArray());
            bw.Write(Successfully);
            WorkWhithVariant.WriteObject(NetObjectToNative.AutoWrap.WrapObject(Result), bw);
            bw.Flush();

            SendStream(stream);

        }    
       


    internal void SendEvent(Guid EventKey, object Result)
    {
        MemoryStream stream = new MemoryStream();
        var bw = new BinaryWriter(stream);
        bw.Write((byte)1);
        bw.Write(EventKey.ToByteArray());
        WorkWhithVariant.WriteObject(NetObjectToNative.AutoWrap.WrapObject(Result), bw);
        bw.Flush();

            SendStream(stream);
        }
}

public class TCPConnector
    {

        TcpListener Server;
        public readonly TaskCompletionSource<int> WaitIsRunning = new TaskCompletionSource<int>();
        // Будем записывать ошибки в файл
        // Нужно прописать в зависимости "System.Diagnostics.TextWriterTraceListener"
        // Файл будет рядом с этой DLL

        // Устанавливаем флаг при закрытии
        bool IsClosed = false;
        // Клиент для отпраки сообщений на сервер

        public TCPConnector()
        {


        }

        // Записываем ошибку a файл и сообщаем об ошибке в 1С



        // Откроем порт и количество слушющих задач которое обычно равно подсоединенным устройствам
        // Нужно учитывть, что 1С обрабатывает все события последовательно ставя события в очередь
        public void Open(int НомерПорта = 6891, int КоличествоСлушателей = 15)
        {
            IsClosed = false;

            IPEndPoint ipEndpoint = new IPEndPoint(IPAddress.Any, НомерПорта);
            Server = new TcpListener(ipEndpoint);
            Server.Start();

            // Создадим задачи для прослушивания порта
            //При подключении клиента запустим метод ОбработкаСоединения
            // Подсмотрено здесь https://github.com/imatitya/netcorersi/blob/master/src/NETCoreRemoveServices.Core/Hosting/TcpServerListener.cs
            for (int i = 0; i < КоличествоСлушателей; i++)
                Server.AcceptTcpClientAsync().ContinueWith(OnConnect);

        }


        // Метод для обработки сообщения от клиента
        private void OnConnect(Task<TcpClient> task)
        {

            if (task.IsFaulted || task.IsCanceled)
            {
                // Скорее всего вызвано  Server.Stop();
                return;
            }

            // Получим клиента
            TcpClient client = task.Result;

            // И вызовем метод для обработки данных
            // 
            ExecuteMethodKeepConnection(client);

            // Если Server не закрыт то запускаем нового слушателя
            if (!IsClosed)
                Server.AcceptTcpClientAsync().ContinueWith(OnConnect);

        }




        private void RunMethod(NetworkStream ns, MemoryStream ms, IPAddress adress)
        {
            using (BinaryReader br = new BinaryReader(ms))
            {
                var msRes = new MemoryStream();
                using(BinaryWriter bw= new BinaryWriter(msRes))
                { 
                    var cm = (CallMethod)br.ReadByte();
                   
                    switch (cm)
                    {
                        case CallMethod.CallFunc: CallAsFunc(br, bw); break;
                        case CallMethod.GetMember: GetPropVal(br, bw); break;
                        case CallMethod.SetMember: SetPropVal(br, bw); break;
                        case CallMethod.CallFuncAsync: CallAsyncFunc(br, bw, adress); break;
                        case CallMethod.CallDelegate: CallAsDelegate(br, bw); break;
                        case CallMethod.CallGenericFunc: CallAsGenericFunc(br, bw); break;
                        case CallMethod.GetWrapperForObjectWithEvents: GetWrapperForObjectWithEvents(br, bw, adress); break;
                        case CallMethod.GetIndex: GetIndex(br, bw); break;
                        case CallMethod.SetIndex: SetIndex(br, bw); break;
                        case CallMethod.IteratorNext: IteratorNext(br, bw); break;
                        case CallMethod.DeleteObjects: DeleteObjects(br, bw); break;
                        case CallMethod.CallBinaryOperation: CallBinaryOperation(br, bw); break;
                        case CallMethod.CallUnaryOperation: CallUnaryOperation(br, bw); break;
                        case CallMethod.CloseServer:
                            {
                                bw.Write(false);
                                bw.Flush();
                                SetResult(msRes, ns);

                                Close();
                                WaitIsRunning.SetResult(1);
                                return;
                            }
                            
                    }

                    bw.Flush();
                    SetResult(msRes, ns);
                }

            }
        }

        
        private void ExecuteMethod(TcpClient client)
        {
            var adress=((IPEndPoint)client.Client.RemoteEndPoint).Address;
        //    client.Client.NoDelay = true;
            using (NetworkStream ns = client.GetStream())
            {

                // Получим данные с клиента и на основании этих данных
                //Создадим ДанныеДляКлиета1С котрый кроме данных содержит 
                //TcpClient для отправки ответа
                using (var br = new BinaryReader(ns))
                {
                    var streamSize = br.ReadInt32();

                    var res = br.ReadBytes(streamSize);

                    var ms = new MemoryStream(res);
                    ms.Position = 0;
                    RunMethod(ns, ms, adress);
                }



            }

        }

        private static byte[] GetByteArrayFromStream(NetworkStream ns, int Length)
        {
            byte[] result = new byte[Length];
            int ReadBytes = 0;
            while (Length > ReadBytes)
            {
                ReadBytes += ns.Read(result, ReadBytes, Length - ReadBytes);
            }

            return result;
        }

        private void ExecuteMethodKeepConnection(TcpClient client)
        {

            try
            {
                NetworkStream ns = client.GetStream();
                var adress = ((IPEndPoint)client.Client.RemoteEndPoint).Address;

                var buffer = new byte[4];
                while (true)
                {
                   // переделать на  ns.ReadAsync;
                    var NotKeepConnection = ns.ReadByte();
                    buffer = GetByteArrayFromStream(ns, 4);
                    var streamSize = BitConverter.ToInt32(buffer, 0);

                    if (streamSize > 0)
                    { 
                    var res = GetByteArrayFromStream(ns, streamSize);

                    var ms = new MemoryStream(res);
                    ms.Position = 0;
                    RunMethod(ns, ms, adress);
                }
                    //ns.Write(BitConverter.GetBytes(streamSize), 0, 4);
                    //ns.Write(res, 0, res.Length);
                    //ns.Flush();

                    if (NotKeepConnection == 1)
                    {

                        client.Dispose();
                        return;
                    }
                }
            }
            catch (System.IO.IOException)
            {
                client.Dispose();
                return;
            }


        }

        // Закроем ресурсы
        public void Close()
        {
            if (Server != null)
            {
                IsClosed = true;
                Server.Stop();
                Server = null;


            }


        }

        static void  SetError(string ErrorStr,BinaryWriter bw)
        {
         
                bw.Write(false);
                WorkWhithVariant.WriteObject(ErrorStr, bw);
        }

        static void SetResult(MemoryStream ms, NetworkStream ns)
        {
            ms.Position = 0;
            ns.Write(BitConverter.GetBytes((Int32)ms.Length), 0, 4);
            ms.CopyTo(ns);
            ns.Flush();


        }

        public static object[] GetArrayParams(BinaryReader br)
        {

            int size = br.ReadInt32();
            var res = new object[size];

            for(int i=0; i< res.Length; i++)
            {
                res[i] = WorkWhithVariant.GetObject(br);


            }

            return res;


        }

        static bool  GetAW(BinaryReader br, BinaryWriter bw, out NetObjectToNative.AutoWrap  AW )
        {

            var Target = br.ReadInt32();

            AW = NetObjectToNative.AutoWrap.ObjectsList.GetValue(Target);

            if (AW == null)
            {
                SetError("Не найдена ссылка на объект", bw);

                return false;
            }

            return true;
        }


        public static void CallAsDelegate(BinaryReader br, BinaryWriter bw)
        {
           object result = null;
            NetObjectToNative.AutoWrap AW;
            if (!GetAW(br, bw, out AW))
                return;

            var args = GetArrayParams(br);
            string Error;
           
            try
            {

                var del = (Delegate)AW.O;
                result = del.DynamicInvoke(args);

            }
            catch (Exception e)
            {
                Error = NetObjectToNative.AutoWrap.GetExeptionString($"Ошибка вызова делегата Target = ", "", e);
                SetError(Error, bw);
                return;
             
            }


            bw.Write(true);
            WorkWhithVariant.WriteObject(NetObjectToNative.AutoWrap.WrapObject(result), bw);

        }


        static void WriteChandeParams(BinaryWriter bw, object[] args,List<int> ChageParams)
        {
            bw.Write(ChageParams.Count);

            foreach (var i in ChageParams)
            {
                bw.Write(i);
                WorkWhithVariant.WriteObject(NetObjectToNative.AutoWrap.WrapObject(args[i]), bw);

            }

        }
        public static bool CallAsFuncAll(BinaryReader br, BinaryWriter bw, out object result, bool WriteResult)
        {
            result = null;
            NetObjectToNative.AutoWrap AW;
            if (!GetAW(br, bw, out AW))
                return false;



            string MethodName = br.ReadString();
            var args = GetArrayParams(br);

            string Error;
            List<int> ChageParams = new List<int>();

           
            var res = AW.TryInvokeMember(MethodName, args, out result, ChageParams, out Error);
            if (!res)
            {
                SetError(Error, bw);
                return false;
            }

           if (WriteResult)
            {
                bw.Write(true);
                WorkWhithVariant.WriteObject(NetObjectToNative.AutoWrap.WrapObject(result), bw);
                WriteChandeParams(bw, args, ChageParams);


            }
            return true;
        }
        public static void CallAsFunc(BinaryReader br, BinaryWriter bw)
        {

            object result = null;
            CallAsFuncAll(br, bw, out result, true);
                
 
        }


        public static bool CallAsGenericFuncAll(BinaryReader br, BinaryWriter bw, out object result, bool WriteResult)
        {
            result = null;
            NetObjectToNative.AutoWrap AW;
            if (!GetAW(br, bw, out AW))
                return false;




            string MethodName = br.ReadString();
            var arguments= GetArrayParams(br);
            var Params = GetArrayParams(br);
           

            // Можно параметры передавать ввиде типов и строк 
            var GenericArguments = new Type[arguments.Length];
            for (int i = 0; i < GenericArguments.Length; i++)
                GenericArguments[i] = NetObjectToNative.NetObjectToNative.FindTypeForCreateObject(arguments[i]);

            result = null;
            var TypesOfParameters = NetObjectToNative.AllMethodsForName.GetTypesParameters(Params);
            var res = NetObjectToNative.InformationOnTheTypes.FindGenericMethodsWithGenericArguments(AW.T, AW.IsType, MethodName, GenericArguments, TypesOfParameters);

            if (res == null)
            {
                SetError("Не найден дженерик метод " + MethodName, bw);
                return false;
            }



            try
            {
                var CopyParams = new object[Params.Length];
                Params.CopyTo(CopyParams,0);

                var obj = AW.IsType ? null : AW.O;
                result = res.ExecuteMethod(obj, CopyParams);

                var ReturnType = res.Method.ReturnType;

                if (result != null && ReturnType.GetTypeInfo().IsInterface)
                    result = new NetObjectToNative.AutoWrap(result, ReturnType);

                if (WriteResult)
                {
                    bw.Write(true);
                    WorkWhithVariant.WriteObject(NetObjectToNative.AutoWrap.WrapObject(result), bw);

                    var ChageParams = NetObjectToNative.AutoWrap.GetChangeParams(Params, CopyParams);
                    WriteChandeParams(bw, CopyParams, ChageParams);
                }

            }

            catch (Exception e)
            {
                var Error = NetObjectToNative.AutoWrap.GetExeptionString($"Ошибка вызова Дженерик метода {MethodName}", "", e);
                SetError(Error, bw);
                return false;
            }

            return true;
        }


        public static void CallAsGenericFunc(BinaryReader br, BinaryWriter bw)
        {
            object result = null;
            CallAsGenericFuncAll(br, bw, out result, true);
       
        }
        public static void CallAsyncFunc(BinaryReader br, BinaryWriter bw, IPAddress adress)
        {
            object result = null;
            if (!CallAsFuncAll(br, bw, out result, false))
                return;

            try
            {
               

                var TaskId = new Guid(br.ReadBytes(16));
                var port = br.ReadInt32();
                var TI = result.GetType().GetTypeInfo();

                var ac = new TcpAsyncCallBack(TaskId,adress, port);
                var callBack = new Action<bool, object>(ac.SendAsyncMessage);
                if (!TI.IsGenericType)
                {
                    // SendMessage(byte TypeResult, Guid Key, bool Successfully, object Result)
                    NetObjectToNative.AsyncRuner.TaskExecute((Task)result, callBack);
                    return;
                }

                var args = new object[] {result, callBack };
                var method =NetObjectToNative.InformationOnTheTypes.FindMethod(typeof(NetObjectToNative.AsyncRuner), true, "Execute", args);

                if (method == null)
                {
                    SetError("Неверный результат", bw);
                    return;
                }
                else
                {
                    method.ExecuteMethod(null, args);
                }
            }
            catch (Exception e)
            {
                var Error = NetObjectToNative.AutoWrap.GetExeptionString("Ошибка вызова делегата", "", e);
                SetError(Error, bw);
                return ;
            }

            bw.Write(true);
            WorkWhithVariant.WriteObject(null, bw);

        }


        public static void GetWrapperForObjectWithEvents(BinaryReader br, BinaryWriter bw, IPAddress adress)
        {
            try
            {
                object result = null;
                NetObjectToNative.AutoWrap AW;
                if (!GetAW(br, bw, out AW))
                    return;

                Type type = AW.T;
                Type genType = typeof(NetObjectToNative.WrapperForEvents<>);
                Type constructed = genType.MakeGenericType(new Type[] { type });
                var propertyName = "WrapperCreater";

                var fi = constructed.GetField(propertyName);
                Delegate func = (Delegate)fi.GetValue(null);


                //var mi = constructed.GetMethod("СоздатьОбертку");
                // Delegate функция = (Delegate)mi.Invoke(null,null);

                var TaskId = new Guid();
                var port = br.ReadInt32();
                
                var ac = new TcpAsyncCallBack(TaskId, adress, port);
                var callBack = new Action<Guid, object>(ac.SendEvent);
                result = func.DynamicInvoke(callBack, AW.O);

               bw.Write(true);
                WorkWhithVariant.WriteObject(NetObjectToNative.AutoWrap.WrapObject(result), bw);

            }
            catch (Exception e)
            {
                var Error = NetObjectToNative.AutoWrap.GetExeptionString("Ошибка создания оберки событий", "", e);
                SetError(Error, bw);
                
            }
          
        }

        public static void GetPropVal(BinaryReader br, BinaryWriter bw)
        {


            NetObjectToNative.AutoWrap AW;
            if (!GetAW(br, bw, out AW))
                return;




            string propertyName = br.ReadString();

            object result = null;
            string Error = null;
            var res = AW.TryGetMember(propertyName, out result, out Error);
            if (!res)
            {
                SetError(Error, bw);
                return;
            }

            bw.Write(true);
            WorkWhithVariant.WriteObject(NetObjectToNative.AutoWrap.WrapObject(result), bw);
        }

        public static void SetPropVal(BinaryReader br, BinaryWriter bw)
        {

            NetObjectToNative.AutoWrap AW;
            if (!GetAW(br, bw, out AW))
                return;
   

        string propertyName = br.ReadString();

            string Error = null;
            object result= WorkWhithVariant.GetObject(br);
            var res = AW.TrySetMember(propertyName, result, out Error);

            if (!res)
              SetError(Error, bw);
            else
            {
                bw.Write(true);
                WorkWhithVariant.WriteObject(null, bw);

            }

            
           
        }



        public static void SetIndex(BinaryReader br, BinaryWriter bw)
        {

            NetObjectToNative.AutoWrap AW;
            if (!GetAW(br, bw, out AW))
                return;


            var indexes = GetArrayParams(br);
            object[] Params = new object[indexes.Length + 1];
            var value = WorkWhithVariant.GetObject(br);
            string MetodName= "set_Item";

            if (typeof(Array).IsAssignableFrom(AW.T))
            {
                MetodName = "SetValue";
                indexes.CopyTo(Params, 1);
                Params[0] = value;
            }
            else
            {
                indexes.CopyTo(Params, 0);
                Params[Params.Length-1] = value;

            }
            string Error = null;
            List<int> ChangeParams = new List<int>();


            object result;
            var res = AW.TryInvokeMember(MetodName, Params, out result, ChangeParams, out Error);
            
            if (!res)
                SetError(Error, bw);
            else
            {
                bw.Write(true);
                WorkWhithVariant.WriteObject(null, bw);

            }

        }

        public static void GetIndex(BinaryReader br, BinaryWriter bw)
        {

            NetObjectToNative.AutoWrap AW;
            if (!GetAW(br, bw, out AW))
                return;


            var Params = GetArrayParams(br);
            string MetodName = "get_Item";

            if (typeof(Array).IsAssignableFrom(AW.T))
                MetodName = "GetValue";

            string Error = null;
            List<int> ChangeParams = new List<int>();


            object result;
            var res = AW.TryInvokeMember(MetodName, Params, out result, ChangeParams, out Error);

            if (!res)
                SetError(Error, bw);
            else
            {
                bw.Write(true);
                WorkWhithVariant.WriteObject(NetObjectToNative.AutoWrap.WrapObject(result), bw);

            }

        }

        public static void IteratorNext(BinaryReader br, BinaryWriter bw)
        {

            NetObjectToNative.AutoWrap AW;
            if (!GetAW(br, bw, out AW))
                return;

            try
            {
                var Enum =AW.O;
                var Iter = (System.Collections.IEnumerator)AW.O;

                var res = Iter.MoveNext();
                bw.Write(true);
                if (!res)
                {
                    NetObjectToNative.AutoWrap.ObjectsList.RemoveKey(AW.IndexInStorage);
                    bw.Write(false);
                    return;

                }

                bw.Write(true);
                WorkWhithVariant.WriteObject(NetObjectToNative.AutoWrap.WrapObject(Iter.Current), bw);
           
            }
            catch (Exception e)
            {
                var Error = NetObjectToNative.AutoWrap.GetExeptionString("Ошибка итератора", "", e);
                SetError(Error, bw);
            }

        }

        public static void DeleteObjects(BinaryReader br, BinaryWriter bw)
        {
            try
            {
                int count = br.ReadInt32();
            for(int i=0; i<count; i++)
            {
                int Target= br.ReadInt32();
                NetObjectToNative.AutoWrap.ObjectsList.RemoveKey(Target);
            }
                bw.Write(true);
                WorkWhithVariant.WriteObject(null, bw);
            }
            catch (Exception e)
            {
                var Error = NetObjectToNative.AutoWrap.GetExeptionString("Ошибка удаления объектов", "", e);
                SetError(Error, bw);
            }

        }


       static void CallStaticMetod(BinaryWriter bw, Type T, string MethodName, object[] args)
        {
            try
            {
                var Method = NetObjectToNative.InformationOnTheTypes.FindMethod(T, true, MethodName, args);

            if (Method == null)
            {
                SetError($"Нет найден метод  {Method} для типа {T}", bw);
                return;
            }

            var obj = Method.ExecuteMethod(null, args);

            bw.Write(true);
            WorkWhithVariant.WriteObject(NetObjectToNative.AutoWrap.WrapObject(obj), bw);
            bw.Write((int)0);
        }
            catch (Exception e)
            {
                var Error = NetObjectToNative.AutoWrap.GetExeptionString("Ошибка бинарной операции ", "", e);
                SetError(Error, bw);
    }

}
        public static void CallBinaryOperation(BinaryReader br, BinaryWriter bw)
        {
            NetObjectToNative.AutoWrap AW;
            if (!GetAW(br, bw, out AW))
                return;




            ExpressionType et= (ExpressionType)br.ReadByte();

            string MethodName;

            if (!OperatorInfo.OperatorMatches.TryGetValue(et, out MethodName))
            {

                SetError($"Нет соответствия {et} имени метода", bw);
                return;

            }

            object param2= WorkWhithVariant.GetObject(br);
            var args = new object[] { AW.O, param2 };

            CallStaticMetod(bw, AW.T,MethodName, args);

        }

        public static void CallUnaryOperation(BinaryReader br, BinaryWriter bw)
        {
            NetObjectToNative.AutoWrap AW;
            if (!GetAW(br, bw, out AW))
                return;




            ExpressionType et = (ExpressionType)br.ReadByte();

            string MethodName;

            if (!OperatorInfo.OperatorMatches.TryGetValue(et, out MethodName))
            {

                SetError($"Нет соответствия {et} имени метода", bw);
                return;

            }

                    var args = new object[] { AW.O};

            CallStaticMetod(bw, AW.T, MethodName, args);

        }

    }
}
