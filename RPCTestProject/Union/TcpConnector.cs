namespace Union
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Threading.Tasks;

    public class TcpAsyncCallBack
    {
        internal TcpAsyncCallBack(Guid key, IPAddress address, int port)
        {
            _endPoint = new IPEndPoint(address, port);
            _key = key;
        }

        private void SendStream(MemoryStream stream)
        {
            using (var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                client.Connect(_endPoint);

                using (var ns = new NetworkStream(client))
                {
                    stream.Position = 0;
                    ns.Write(BitConverter.GetBytes((Int32)stream.Length), 0, 4);
                    stream.CopyTo(ns);
                }
            }
        }

        internal void SendAsyncMessage(bool successfully, object result)
        {
            MemoryStream stream = new MemoryStream();
            var bw = new BinaryWriter(stream);
            bw.Write((byte)0);
            bw.Write(_key.ToByteArray());
            bw.Write(successfully);
            WorkWithVariant.WriteObject(AutoWrap.WrapObject(result), bw);
            bw.Flush();

            SendStream(stream);
        }

        private readonly IPEndPoint _endPoint;
        private Guid _key;
    }

    public class TcpConnector
    {
        public bool Connect(string serverIp, int port)
        {
            _ipEndpoint = new IPEndPoint(IPAddress.Parse(serverIp), port);
            AsyncDictionary = new Dictionary<Guid, TaskCompletionSource<object>>();

            var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            client.Connect(_ipEndpoint);
            var ns = new NetworkStream(client);
            _nsQueue = new BlockingCollection<NetworkStream> { ns };

            return client.Connected;
        }

        public void Open(int port, int countListener)
        {
            _isClosed = false;
            Property = port;
            _server = new TcpListener(new IPEndPoint(IPAddress.Any, port));
            _server.Start();

            for (int i = 0; i < countListener; i++) _server.AcceptTcpClientAsync().ContinueWith(OnConnect);
        }

        private void OnConnect(Task<TcpClient> task)
        {
            if (task.IsFaulted || task.IsCanceled) return;
            TcpClient client = task.Result;
            ExecuteMethodKeepConnection(client);

            if (!_isClosed) _server.AcceptTcpClientAsync().ContinueWith(OnConnect);
        }

        private void ExecuteMethodKeepConnection(TcpClient client)
        {
            try
            {
                NetworkStream ns = client.GetStream();
                var address = ((IPEndPoint)client.Client.RemoteEndPoint).Address;

                while (true)
                {
                    var notKeepConnection = ns.ReadByte();
                    var streamSize = BitConverter.ToInt32(GetByteArrayFromStream(ns, 4), 0);

                    if (streamSize > 0)
                    {
                        var ms = new MemoryStream(GetByteArrayFromStream(ns, streamSize)) { Position = 0 };
                        RunMethod(ns, ms, address);
                    }

                    if (notKeepConnection == 1)
                    {
                        client.Dispose();
                        return;
                    }
                }
            }
            catch (IOException)
            {
                client.Dispose();
            }
        }

        public static int GetAvailablePort(int port)
        {
            var ports = new HashSet<int>(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(tcp => tcp.Port));
            for (var i = port; i < 49152; i++)
            {
                if (!ports.Contains(i)) return i;
            }

            return port;
        }

        private void RunMethod(NetworkStream ns, MemoryStream ms, IPAddress address)
        {
            using (BinaryReader br = new BinaryReader(ms))
            {
                var msRes = new MemoryStream();
                using (BinaryWriter bw = new BinaryWriter(msRes))
                {
                    var cm = (CallMethod)br.ReadByte();

                    switch (cm)
                    {
                        case CallMethod.CallFunc: CallAsFunc(br, bw); break;
                        case CallMethod.GetMember: GetPropVal(br, bw); break;
                        case CallMethod.SetMember: SetPropVal(br, bw); break;
                        case CallMethod.CallFuncAsync: CallAsyncFunc(br, bw, address); break;
                        case CallMethod.CallDelegate: CallAsDelegate(br, bw); break;
                        case CallMethod.CallGenericFunc: CallAsGenericFunc(br, bw); break;
                        case CallMethod.GetIndex: GetIndex(br, bw); break;
                        case CallMethod.SetIndex: SetIndex(br, bw); break;
                        case CallMethod.IteratorNext: IteratorNext(br, bw); break;
                        case CallMethod.DeleteObjects: DeleteObjects(br, bw); break;
                        case CallMethod.CallBinaryOperation: CallBinaryOperation(br, bw); break;
                        case CallMethod.CallUnaryOperation: CallUnaryOperation(br, bw); break;
                        case CallMethod.Close:
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

        private static byte[] GetByteArrayFromStream(NetworkStream ns, int length)
        {
            byte[] result = new byte[length];
            int readBytes = 0;
            while (length > readBytes)
            {
                readBytes += ns.Read(result, readBytes, length - readBytes);
            }

            return result;
        }

        // Закроем ресурсы
        public void Close()
        {
            if (_server != null)
            {
                _isClosed = true;
                _server.Stop();
                _server = null;
            }

            CloseConnection();
        }

        private void CloseConnection()
        {
            if(_nsQueue == null) return;
            while (_nsQueue.TryTake(out var ns))
            {
                ns.WriteByte(1); // Признак того, что соединение  разрывать
                ns.Write(BitConverter.GetBytes((int)0), 0, 4);
                ns.Flush();
                ns.Dispose();
            }
        }

        internal BinaryReader SendMessage(MemoryStream stream)
        {
            //var ns = _nsQueue.Take();
            //var res = SendMessageKeepConnection(stream, ns);
            //_nsQueue.Add(ns);
            //return res;
            return SendMessageOne(stream);
        }

        #region Client

        public void ClearDeletedObject()
        {
            //if (ServerIsClosed) return;

            lock (_syncForDelete)
            {
                if (_deletedObjects != null && _deletedObjects.Count > 0) SendDeleteObjects();
            }
        }

        // Отсылаем массив ссылок для удаления их из зранилища объектов на сервере
        private void SendDeleteObjects()
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.DeleteObjects);
            bw.Write(_deletedObjects.Count);

            foreach (var i in _deletedObjects) bw.Write(i);
            bw.Flush();

            _deletedObjects.Clear();
            var res = SendMessage(ms);

            object result = null;
            if (!AutoWrapClient.GetResult(res, ref result, this)) throw new Exception(LastError);
        }

        // Добавим ссылку на объект на сервере в буффер
        // И если в буфере количество больше заданного
        // То отсылается массив ссылок, а буфуе очищается
        // Сделано, для ускорения действи при межпроцессном взаимодействии
        public void DeleteObject(AutoWrapClient Object)
        {
            //if (ServerIsClosed) return;

            lock (_syncForDelete)
            {
                _deletedObjects.Add(Object.Target);
                if (_deletedObjects.Count > _countDeletedObjects) SendDeleteObjects();
            }
        }

        public void CloseServer()
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.Close);
            bw.Flush();

            SendMessage(ms);
        }

        private BinaryReader SendMessageKeepConnection(MemoryStream stream, NetworkStream ns)
        {
            stream.Position = 0;
            ns.WriteByte(0); // Признак того, что соединение не разрывать
            ns.Write(BitConverter.GetBytes((int)stream.Length), 0, 4);
            stream.CopyTo(ns);
            ns.Flush();

            var buffer = new byte[4];

            ns.Read(buffer, 0, 4);
            var streamSize = BitConverter.ToInt32(buffer, 0);
            var res = new byte[streamSize];
            ns.Read(res, 0, streamSize);
            return new BinaryReader(new MemoryStream(res) { Position = 0 });
        }

        // Отсылаем поток данных и считываем ответ
        private BinaryReader SendMessageOne(MemoryStream stream)
        {
            using (var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                client.Connect(_ipEndpoint);
                using (var ns = new NetworkStream(client))
                {
                    ns.WriteByte(1);// Сервер отрабатывает только 1 запрос
                    stream.Position = 0;
                    ns.Write(BitConverter.GetBytes((Int32)stream.Length), 0, 4);
                    stream.CopyTo(ns);

                    using (var br = new BinaryReader(ns))
                    {
                        return new BinaryReader(new MemoryStream(
                            br.ReadBytes(br.ReadInt32()))
                        { Position = 0 });
                    }
                }
            }
        }

        #endregion Client

        #region Call members

        private static void SetError(string errorStr, BinaryWriter bw)
        {
            bw.Write(false);
            WorkWithVariant.WriteObject(errorStr, bw);
        }

        private static void SetResult(MemoryStream ms, NetworkStream ns)
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

            for (int i = 0; i < res.Length; i++)
            {
                res[i] = WorkWithVariant.GetObject(br);
            }

            return res;
        }

        private static bool GetAW(BinaryReader br, BinaryWriter bw, out AutoWrap autoWrap)
        {
            var target = br.ReadInt32();

            autoWrap = AutoWrap.ObjectsList.GetValue(target);

            if (autoWrap == null)
            {
                SetError("Не найдена ссылка на объект", bw);
                return false;
            }

            return true;
        }

        public static void CallAsDelegate(BinaryReader br, BinaryWriter bw)
        {
            object result;
            if (!GetAW(br, bw, out var autoWrap))
                return;

            var args = GetArrayParams(br);
            try
            {
                var del = (Delegate)autoWrap.Object;
                result = del.DynamicInvoke(args);
            }
            catch (Exception e)
            {
                SetError(AutoWrap.GetExceptionString($"Ошибка вызова делегата Target = ", "", e), bw);
                return;
            }

            bw.Write(true);
            WorkWithVariant.WriteObject(AutoWrap.WrapObject(result), bw);
        }

        private static void WriteChangeParams(BinaryWriter bw, object[] args, List<int> changeParameters)
        {
            bw.Write(changeParameters.Count);

            foreach (var i in changeParameters)
            {
                bw.Write(i);
                WorkWithVariant.WriteObject(AutoWrap.WrapObject(args[i]), bw);
            }
        }

        public static bool CallAsFuncAll(BinaryReader br, BinaryWriter bw, out object result, bool writeResult)
        {
            result = null;
            if (!GetAW(br, bw, out var autoWrap)) return false;

            string methodName = br.ReadString();
            var args = GetArrayParams(br);

            List<int> changeParameters = new List<int>();

            var res = autoWrap.TryInvokeMember(methodName, args, out result, changeParameters, out var error);
            if (!res)
            {
                SetError(error, bw);
                return false;
            }

            if (writeResult)
            {
                bw.Write(true);
                WorkWithVariant.WriteObject(AutoWrap.WrapObject(result), bw);
                WriteChangeParams(bw, args, changeParameters);
            }
            return true;
        }

        public static void CallAsFunc(BinaryReader br, BinaryWriter bw)
        {
            CallAsFuncAll(br, bw, out var result, true);
        }

        public static bool CallAsGenericFuncAll(BinaryReader br, BinaryWriter bw, out object result, bool writeResult)
        {
            result = null; ;
            if (!GetAW(br, bw, out var autoWrap)) return false;

            string methodName = br.ReadString();
            var arguments = GetArrayParams(br);
            var Params = GetArrayParams(br);

            // Можно параметры передавать ввиде типов и строк
            var genericArguments = new Type[arguments.Length];
            for (int i = 0; i < genericArguments.Length; i++)
                genericArguments[i] = NetObjectToNative.FindTypeForCreateObject(arguments[i]);

            result = null;
            var typesOfParameters = AllMethodsForName.GetTypesParameters(Params);
            var res = InformationOnTheTypes.FindGenericMethodsWithGenericArguments(
                autoWrap.Type,
                autoWrap.IsType,
                methodName,
                genericArguments,
                typesOfParameters);

            if (res == null)
            {
                SetError("Не найден дженерик метод " + methodName, bw);
                return false;
            }

            try
            {
                var copyParams = new object[Params.Length];
                Params.CopyTo(copyParams, 0);

                var obj = autoWrap.IsType ? null : autoWrap.Object;
                result = res.ExecuteMethod(obj, copyParams);

                var returnType = res.Method.ReturnType;

                if (result != null && returnType.GetTypeInfo().IsInterface)
                    result = new AutoWrap(result, returnType);

                if (writeResult)
                {
                    bw.Write(true);
                    WorkWithVariant.WriteObject(AutoWrap.WrapObject(result), bw);
                    WriteChangeParams(bw, copyParams, AutoWrap.GetChangeParams(Params, copyParams));
                }
            }
            catch (Exception e)
            {
                SetError(AutoWrap.GetExceptionString($"Ошибка вызова Дженерик метода {methodName}", "", e), bw);
                return false;
            }

            return true;
        }

        public static void CallAsGenericFunc(BinaryReader br, BinaryWriter bw)
        {
            CallAsGenericFuncAll(br, bw, out var result, true);
        }

        public static void CallAsyncFunc(BinaryReader br, BinaryWriter bw, IPAddress address)
        {
            if (!CallAsFuncAll(br, bw, out var result, false)) return;
            try
            {
                var taskId = new Guid(br.ReadBytes(16));
                var port = br.ReadInt32();
                var typeInfo = result.GetType().GetTypeInfo();

                var asyncCallBack = new TcpAsyncCallBack(taskId, address, port);
                var callBack = new Action<bool, object>(asyncCallBack.SendAsyncMessage);
                if (!typeInfo.IsGenericType)
                {
                    AsyncRunner.TaskExecute((Task)result, callBack);
                    return;
                }

                var args = new[] { result, callBack };
                var method = InformationOnTheTypes.FindMethod(typeof(AsyncRunner), true, "Execute", args);

                if (method == null)
                {
                    SetError("Неверный результат", bw);
                    return;
                }

                method.ExecuteMethod(null, args);
            }
            catch (Exception e)
            {
                SetError(AutoWrap.GetExceptionString("Ошибка вызова делегата", "", e), bw);
                return;
            }

            bw.Write(true);
            WorkWithVariant.WriteObject(null, bw);
        }

        public static void GetPropVal(BinaryReader br, BinaryWriter bw)
        {
            if (!GetAW(br, bw, out var autoWrap)) return;

            string propertyName = br.ReadString();
            var res = autoWrap.TryGetMember(propertyName, out var result, out var error);
            if (!res)
            {
                SetError(error, bw);
                return;
            }

            bw.Write(true);
            WorkWithVariant.WriteObject(AutoWrap.WrapObject(result), bw);
        }

        public static void SetPropVal(BinaryReader br, BinaryWriter bw)
        {
            if (!GetAW(br, bw, out var autoWrap)) return;

            string propertyName = br.ReadString();
            object result = WorkWithVariant.GetObject(br);
            var res = autoWrap.TrySetMember(propertyName, result, out var error);

            if (!res) SetError(error, bw);
            else
            {
                bw.Write(true);
                WorkWithVariant.WriteObject(null, bw);
            }
        }

        public static void SetIndex(BinaryReader br, BinaryWriter bw)
        {
            if (!GetAW(br, bw, out var autoWrap)) return;

            var indexes = GetArrayParams(br);
            object[] Params = new object[indexes.Length + 1];
            var value = WorkWithVariant.GetObject(br);
            string methodName = "set_Item";

            if (typeof(Array).IsAssignableFrom(autoWrap.Type))
            {
                methodName = "SetValue";
                indexes.CopyTo(Params, 1);
                Params[0] = value;
            }
            else
            {
                indexes.CopyTo(Params, 0);
                Params[Params.Length - 1] = value;
            }

            var changeParams = new List<int>();
            var res = autoWrap.TryInvokeMember(methodName, Params, out var result, changeParams, out var error);

            if (!res) SetError(error, bw);
            else
            {
                bw.Write(true);
                WorkWithVariant.WriteObject(null, bw);
            }
        }

        public static void GetIndex(BinaryReader br, BinaryWriter bw)
        {
            if (!GetAW(br, bw, out var autoWrap)) return;

            var parameters = GetArrayParams(br);
            string methodName = "get_Item";
            if (typeof(Array).IsAssignableFrom(autoWrap.Type)) methodName = "GetValue";

            var changeParams = new List<int>();
            var res = autoWrap.TryInvokeMember(methodName, parameters, out var result, changeParams, out var error);

            if (!res) SetError(error, bw);
            else
            {
                bw.Write(true);
                WorkWithVariant.WriteObject(AutoWrap.WrapObject(result), bw);
            }
        }

        public static void IteratorNext(BinaryReader br, BinaryWriter bw)
        {
            if (!GetAW(br, bw, out var autoWrap)) return;

            try
            {
                var enumerator = (System.Collections.IEnumerator)autoWrap.Object;
                var res = enumerator.MoveNext();
                bw.Write(true);
                if (!res)
                {
                    AutoWrap.ObjectsList.RemoveKey(autoWrap.IndexInStorage);
                    bw.Write(false);
                    return;
                }

                bw.Write(true);
                WorkWithVariant.WriteObject(AutoWrap.WrapObject(enumerator.Current), bw);
            }
            catch (Exception e)
            {
                SetError(AutoWrap.GetExceptionString("Ошибка итератора", "", e), bw);
            }
        }

        public static void DeleteObjects(BinaryReader br, BinaryWriter bw)
        {
            try
            {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    int target = br.ReadInt32();
                    AutoWrap.ObjectsList.RemoveKey(target);
                }
                bw.Write(true);
                WorkWithVariant.WriteObject(null, bw);
            }
            catch (Exception e)
            {
                SetError(AutoWrap.GetExceptionString("Ошибка удаления объектов", "", e), bw);
            }
        }

        private static void CallStaticMethod(BinaryWriter bw, Type T, string methodName, object[] args)
        {
            try
            {
                var method = InformationOnTheTypes.FindMethod(T, true, methodName, args);

                if (method == null)
                {
                    SetError($"Нет найден метод  {method} для типа {T}", bw);
                    return;
                }

                var obj = method.ExecuteMethod(null, args);

                bw.Write(true);
                WorkWithVariant.WriteObject(AutoWrap.WrapObject(obj), bw);
                bw.Write((int)0);
            }
            catch (Exception e)
            {
                SetError(AutoWrap.GetExceptionString("Ошибка бинарной операции ", "", e), bw);
            }
        }

        public static void CallBinaryOperation(BinaryReader br, BinaryWriter bw)
        {
            if (!GetAW(br, bw, out var autoWrap)) return;
            var expressionType = (ExpressionType)br.ReadByte();

            if (!OperatorInfo.OperatorMatches.TryGetValue(expressionType, out var methodName))
            {
                SetError($"Нет соответствия {expressionType} имени метода", bw);
                return;
            }

            object param2 = WorkWithVariant.GetObject(br);
            var args = new[] { autoWrap.Object, param2 };

            CallStaticMethod(bw, autoWrap.Type, methodName, args);
        }

        public static void CallUnaryOperation(BinaryReader br, BinaryWriter bw)
        {
            if (!GetAW(br, bw, out var autoWrap)) return;

            var expressionType = (ExpressionType)br.ReadByte();
            if (!OperatorInfo.OperatorMatches.TryGetValue(expressionType, out var methodName))
            {
                SetError($"Нет соответствия {expressionType} имени метода", bw);
                return;
            }

            var args = new[] { autoWrap.Object };
            CallStaticMethod(bw, autoWrap.Type, methodName, args);

            #endregion Call members
        }

        //server
        private TcpListener _server;

        // Устанавливаем флаг при закрытии
        private bool _isClosed;

        public int Property { get; private set; }
        public readonly TaskCompletionSource<int> WaitIsRunning = new TaskCompletionSource<int>();

        // Client
        private object _syncForDelete = new object();

        private IPEndPoint _ipEndpoint;
        private BlockingCollection<NetworkStream> _nsQueue;
        internal Dictionary<Guid, TaskCompletionSource<object>> AsyncDictionary;

        // Список для удаления объектов
        //Для уменьшения затрат на межпроцессное взаимодействие будем отправлть
        //Запрос на удаление из хранилища не по 1 объект а пачками количество указанным  в CountDeletedObjects
        private List<int> _deletedObjects;

        private int _countDeletedObjects;
        internal string LastError;
    }
}