using System;
using System.Collections;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;

namespace Union
{
    public enum CallMethod : byte
    {
        CallFunc = 0,
        GetMember,
        SetMember,
        CallFuncAsync,
        CallDelegate,
        CallGenericFunc,
        SetIndex,
        GetIndex,
        CallBinaryOperation,
        CallUnaryOperation,
        IteratorNext,
        DeleteObjects,
        Close
    }

    public class AutoWapEnumerator : IEnumerator
    {
        private readonly TcpConnector _connector;
        private readonly AutoWrapClient _enumerator;
        public object Current { get; set; }

        public AutoWapEnumerator(AutoWrapClient target, TcpConnector connector)
        {
            _connector = connector;

            if (!AutoWrapClient.TryInvokeMember(
                0,
                "GetIterator",
                new object[] { target },
                out var result,
                _connector
            ))
                throw new Exception(_connector.LastError);

            _enumerator = (AutoWrapClient)result;
        }

        public bool MoveNext()
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.IteratorNext);
            bw.Write(_enumerator.Target);
            bw.Flush();

            var res = _connector.SendMessage(ms);
            var resCall = res.ReadBoolean();

            if (!resCall) throw new Exception(res.ReadString());

            var resNext = res.ReadBoolean();

            if (!resNext)
            {
                GC.SuppressFinalize(_enumerator);
                return false;
            }

            Current = WorkVariants.GetObject(res, _connector);
            return true;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }

    public class AutoWrapClient : DynamicObject, IEnumerable, IDisposable
    {
        public AutoWrapClient(int target, TcpConnector connector)
        {
            Target = target;
            Connector = connector;
        }

        public static dynamic GetProxy(TcpConnector connector)
        {
            return new AutoWrapClient(0, connector);
        }

        public IEnumerator GetEnumerator() => new AutoWapEnumerator(this, Connector);

        ~AutoWrapClient() => Dispose(false);

        public void Dispose()
        {
            // Не изменяйте этот код. Разместите код очистки выше, в методе Dispose(bool disposing).
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // TODO: освободить управляемое состояние (управляемые объекты).
                    GC.SuppressFinalize(this);
                }

                Connector.DeleteObject(this);
                // TODO: освободить неуправляемые ресурсы (неуправляемые объекты) и переопределить ниже метод завершения.
                // TODO: задать большим полям значение NULL.

                _isDisposed = true;
            }
        }

        internal static bool TryInvokeMember(int target, string methodName, object[] args, out object result,
            TcpConnector connector)
        {
            result = null;

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.CallFunc);
            bw.Write(target);
            bw.Write(methodName);
            bw.Write(args.Length);

            foreach (var arg in args) WorkVariants.WriteObject(arg, bw);

            bw.Flush();

            return GetResultWithChangeParams(connector.SendMessage(ms), ref result, connector, args);
        }

        internal static bool TryInvokeGenericMethod(int target, string methodName, object[] args, out object result, TcpConnector connector)
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.CallGenericFunc);
            bw.Write(target);
            bw.Write(methodName);

            var arguments = (object[])args[0];
            bw.Write(arguments.Length);

            foreach (var arg in arguments) WorkVariants.WriteObject(arg, bw);

            bw.Write(args.Length - 1);

            for (var i = 1; i < args.Length; i++) WorkVariants.WriteObject(args[i], bw);

            bw.Flush();

            var res = connector.SendMessage(ms);
            result = null;
            return GetResultWithChangeParams(res, ref result, connector, args, 1);
        }

        internal static bool GetResultWithChangeParams(BinaryReader res, ref object result, TcpConnector connector,
            object[] args, int offset = 0)
        {
            if (!GetResult(res, ref result, connector)) return false;
            var count = res.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                var index = res.ReadInt32();// Получим индекс измененного параметра
                object value = WorkVariants.GetObject(res, connector);// Получим значение измененного параметра

                // args[index + offset]= value;// Установим нужный параметр, для Generic методов с 0 индексом идет тип аргументов

                // Вариант с  RefParam
                object param = args[index + offset];
                if (param != null && param is RefParam refParam) refParam.Value = value;
            }

            return true;
        }

        internal static bool GetResult(BinaryReader res, ref object result, TcpConnector connector)
        {
            var resRun = res.ReadBoolean();
            var returnValue = WorkVariants.GetObject(res, connector);
            if (!resRun)
            {
                if (returnValue != null && returnValue is string stringValue) connector.LastError = stringValue;
                return false;
            }

            result = returnValue;
            return true;
        }

        internal static void GetAsyncResult(BinaryReader res, TaskCompletionSource<object> result,
            TcpConnector connector)
        {
            object asynchronous = null;
            if (!GetResult(res, ref asynchronous, connector)) result.SetException(new Exception(connector.LastError));
        }

        #region Dynamic override

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            result = null;
            //if (Connector.ServerIsClosed) return false;

            string methodName = binder.Name;
            if (methodName == "_new")
            {
                object[] newArgs = new object[args.Length + 1];
                args.CopyTo(newArgs, 1);
                newArgs[0] = this;
                return TryInvokeMember(0, "New", newArgs, out result, Connector);
            }
            if (args.Length > 0 && args[0] != null && args[0].GetType() == typeof(object[]))
                return TryInvokeGenericMethod(Target, methodName, args, out result, Connector);

            return TryInvokeMember(Target, methodName, args, out result, Connector);
        }

        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
        {
            result = null;
            //if (Connector.ServerIsClosed) return false;

            if (args.Length == 1 && ReferenceEquals(args[0], FlagDeleteObject))
            {
                Dispose(true);
                return true;
            }

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.CallDelegate);
            bw.Write(Target);

            bw.Write(args.Length);
            foreach (var arg in args) WorkVariants.WriteObject(arg, bw);

            bw.Flush();

            var res = Connector.SendMessage(ms);
            return GetResult(res, ref result, Connector);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = null;
            //if (Connector.ServerIsClosed) return false;

            var memberName = binder.Name;

            if (memberName == "async")
            {
                result = new AsyncAutoWrapClient(Target, Connector);
                return true;
            }

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.GetMember);
            bw.Write(Target);
            bw.Write(memberName);
            var res = Connector.SendMessage(ms);

            return GetResult(res, ref result, Connector);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            //if (Connector.ServerIsClosed) return false;

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.SetMember);
            bw.Write(Target);
            bw.Write(binder.Name);
            WorkVariants.WriteObject(value, bw);

            var res = Connector.SendMessage(ms);
            object result = null;
            return GetResult(res, ref result, Connector);
        }

        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            //if (Connector.ServerIsClosed) return false;

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.SetIndex);
            bw.Write(Target);
            bw.Write(indexes.Length);
            foreach (var arg in indexes)
                WorkVariants.WriteObject(arg, bw);

            WorkVariants.WriteObject(value, bw);

            bw.Flush();

            var res = Connector.SendMessage(ms);
            object result = null;
            return GetResult(res, ref result, Connector);
        }

        public override bool TryGetIndex(
            GetIndexBinder binder, object[] indexes, out object result)
        {
            result = null;
            //if (Connector.ServerIsClosed) return false;

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.GetIndex);
            bw.Write(Target);
            bw.Write(indexes.Length);
            foreach (var arg in indexes)
                WorkVariants.WriteObject(arg, bw);

            bw.Flush();

            var res = Connector.SendMessage(ms);
            return GetResult(res, ref result, Connector);
        }

        public override bool TryBinaryOperation(
            BinaryOperationBinder binder, object arg, out object result)
        {
            result = null;

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.CallBinaryOperation);
            bw.Write(Target);
            bw.Write((byte)binder.Operation);
            WorkVariants.WriteObject(arg, bw);

            bw.Flush();
            var res = Connector.SendMessage(ms);
            return GetResult(res, ref result, Connector);
        }

        public override bool TryUnaryOperation(UnaryOperationBinder binder, out object result)
        {
            result = null;

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.CallUnaryOperation);
            bw.Write(Target);
            bw.Write((byte)binder.Operation);

            bw.Flush();
            var res = Connector.SendMessage(ms);
            return GetResult(res, ref result, Connector);
        }

        public override bool Equals(object obj)
        {
            object[] args = { obj };

            if (TryInvokeMember(Target, "Equals", args, out var result, Connector))
                return (bool)result;

            throw new Exception(Connector.LastError);
        }

        public override string ToString()
        {
            object[] args = new object[0];

            if (TryInvokeMember(Target, "ToString", args, out var result, Connector))
                return (string)result;

            throw new Exception(Connector.LastError);
        }

        public override int GetHashCode()
        {
            object[] args = new object[0];

            if (TryInvokeMember(Target, "GetHashCode", args, out var result, Connector))
                return (int)result;

            throw new Exception(Connector.LastError);
        }

        #endregion Dynamic override

        public readonly int Target;
        protected readonly TcpConnector Connector;
        private bool _isDisposed;
        private static readonly object FlagDeleteObject = new object();
    }

    public class AsyncAutoWrapClient : AutoWrapClient
    {
        public AsyncAutoWrapClient(int target, TcpConnector connector) : base(target, connector)
            => GC.SuppressFinalize(this);

        private static bool TryAsyncInvokeMember(int target, string methodName, object[] args, out object result, TcpConnector connector)
        {
            var tcs = new TaskCompletionSource<object>();

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.CallFuncAsync);
            bw.Write(target);
            bw.Write(methodName);
            bw.Write(args.Length);

            foreach (var arg in args) WorkVariants.WriteObject(arg, bw);

            var guid = Guid.NewGuid();

            connector.AsyncDictionary.Add(guid, tcs);
            bw.Write(guid.ToByteArray());
            bw.Write(connector.Property);

            bw.Flush();

            var res = connector.SendMessage(ms);
            GetAsyncResult(res, tcs, connector);
            result = tcs.Task;
            return true;
        }

        internal object SetAsyncError()
        {
            var tcs = new TaskCompletionSource<object>();
            tcs.SetException(new Exception(Connector.LastError));
            return tcs.Task;
        }

        #region Override

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            result = null;
            //if (Connector.ServerIsClosed) return false;

            if (args.Length > 0 && args[0] != null && args[0].GetType() == typeof(object[]))
            {
                if (!TryInvokeGenericMethod(Target, binder.Name, args, out var resAsync, Connector))
                {
                    result = SetAsyncError();
                    return true;
                }

                return TryAsyncInvokeMember(0, "ReturnParam", new[] { resAsync }, out result, Connector);
            }
            return TryAsyncInvokeMember(Target, binder.Name, args, out result, Connector);
        }

        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
        {
            if (!base.TryInvoke(binder, args, out var resAsync))
            {
                result = SetAsyncError();
                return true;
            }

            return TryAsyncInvokeMember(0, "ReturnParam", new[] { resAsync }, out result, Connector);
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (!base.TryGetIndex(binder, indexes, out var resAsync))
            {
                result = SetAsyncError();
                return true;
            }

            return TryAsyncInvokeMember(0, "ReturnParam", new[] { resAsync }, out result, Connector);
        }

        #endregion Override

        protected override void Dispose(bool disposing)
        {
        }
    }

    public class RefParam
    {
        public dynamic Value;

        public RefParam(object value) => this.Value = value;

        public RefParam() => this.Value = null;

        public override string ToString() => Value?.ToString() ?? throw new InvalidOperationException();
    }
}