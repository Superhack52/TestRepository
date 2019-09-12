namespace Client
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    public class TCPClientConnector
    {
        //todo
        internal string LastError;

        public TCPClientConnector(string serverAddress, int port) : this(serverAddress, port, true)
        {
        }

        public TCPClientConnector(string serverAddress, int port, bool keepConnection)
        {
            _keepConnection = keepConnection;
            // адрес сервера
            _ipEndpoint = new IPEndPoint(IPAddress.Parse(serverAddress), port);
            AsyncDictionary = new Dictionary<Guid, TaskCompletionSource<object>>();
            _deletedObjects = new List<int>();

            //todo зачем 5 коннектов?
            //for (var i = 0; i < 5; i++)
            //{
            var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            client.Connect(_ipEndpoint);
            var ns = new NetworkStream(client);
            _nsQueue = new BlockingCollection<NetworkStream> { ns };
            //}
        }

        internal Dictionary<Guid, WrapperObjectWithEvents> EventDictionary =>
            _eventDictionary ?? (_eventDictionary = new Dictionary<Guid, WrapperObjectWithEvents>());

        //public static TCPClientConnector LoadAndConnectToLocalServer(string fileName)
        //{
        //    int port = 1025;

        //    port = GetAvailablePort(port);
        //    ProcessStartInfo startInfo = new ProcessStartInfo("dotnet.exe")
        //    {
        //        Arguments = @"""" + fileName + $@""" {port}"
        //    };

        //    Console.WriteLine(startInfo.Arguments);
        //    Console.WriteLine(Process.Start(startInfo));

        //    port++;
        //    port = GetAvailablePort(port);
        //    var connector = new TCPClientConnector("127.0.0.1", port);
        //    connector.Open(port, 2);

        //    return connector;
        //}

        public static int GetAvailablePort(int port)
        {
            var set = new HashSet<int>(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections()
                    .Select(tcp => tcp.LocalEndPoint.Port)
            );

            for (var i = port; i < 49152; i++)
            {
                if (!set.Contains(i)) return i;
            }

            return port;
        }

        // Откроем порт и количество слушющих задач которое обычно равно подсоединенным устройствам
        public void Open(int port, int countListener)
        {
            _isClosed = false;
            PortForCallBack = port;
            _server = new TcpListener(new IPEndPoint(IPAddress.Any, port));
            _server.Start();

            // Создадим задачи для прослушивания порта
            // При подключении клиента запустим метод ОбработкаСоединения
            //for (int i = 0; i < countListener; i++) _server.AcceptTcpClientAsync().ContinueWith(OnConnect);
        }

        // Закроем ресурсы
        public void Close()
        {
            if (_server != null)
            {
                CloseEvents();
                _isClosed = true;
                _server.Stop();
                _server = null;
            }

            CloseConnection();
        }

        private void CloseConnection()
        {
            if (_keepConnection)
            {
                _keepConnection = false;
                while (_nsQueue.TryTake(out var ns))
                {
                    ns.WriteByte(1); // Признак того, что соединение  разрывать
                    ns.Write(BitConverter.GetBytes((int)0), 0, 4);
                    ns.Flush();
                    ns.Dispose();
                }
            }
        }

        internal BinaryReader SendMessage(MemoryStream stream)
        {
            if (_keepConnection)
            {
                var ns = _nsQueue.Take();
                var res = SendMessageKeepConnection(stream, ns);
                _nsQueue.Add(ns);
                return res;
            }

            return SendMessageOne(stream);
        }

        public bool ServerIsClosed { get; set; }

        public void CloseServer()
        {
            CloseEvents();

            ServerIsClosed = true;
            SendCloseServer();
        }

        // Отошлем все ссылки для удаления объектов из  хранилища

        private void CloseEvents()
        {
            foreach (var eventWrap in EventDictionary.Values.Distinct().ToArray())
                eventWrap.Close();
        }

        public void ClearDeletedObject()
        {
            if (ServerIsClosed) return;

            lock (syncForDelete)
            {
                if (_deletedObjects.Count > 0) SendDeleteObjects();
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
            if (ServerIsClosed) return;

            lock (syncForDelete)
            {
                _deletedObjects.Add(Object.Target);
                if (_deletedObjects.Count > _countDeletedObjects) SendDeleteObjects();
            }
        }

        private void SendCloseServer()
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write((byte)CallMethod.CloseServer);
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
            ExecuteMethod(client);

            // Если Server не закрыт то запускаем нового слушателя
            if (!_isClosed) _server.AcceptTcpClientAsync().ContinueWith(OnConnect);
        }

        private void ExecuteMethod(TcpClient client)
        {
            using (NetworkStream ns = client.GetStream())
            {
                //Получим данные с клиента и на основании этих данных
                //Создадим ДанныеДляКлиета1С котрый кроме данных содержит
                //TcpClient для отправки ответа
                using (var br = new BinaryReader(ns))
                {
                    RunMethod(new MemoryStream(br.ReadBytes(br.ReadInt32())) { Position = 0 });
                }
            }
        }

        private void RunMethod(MemoryStream ms)
        {
            using (BinaryReader br = new BinaryReader(ms))
            {
                switch (br.ReadByte())
                {
                    case 0: SetAsyncResult(br); break;
                    case 1: SetEvent(br); break;
                }
            }
        }

        private void SetAsyncResult(BinaryReader br)
        {
            Guid key = new Guid(br.ReadBytes(16));
            var res = br.ReadBoolean();
            var resObj = WorkVariants.GetObject(br, this);
            TaskCompletionSource<object> value;
            if (AsyncDictionary.TryGetValue(key, out value))
            {
                if (res) value.SetResult(resObj);
                else value.TrySetException(new Exception((string)resObj));
            }
        }

        private void SetEvent(BinaryReader br)
        {
            Guid key = new Guid(br.ReadBytes(16));
            var res = WorkVariants.GetObject(br, this);
            WrapperObjectWithEvents value;
            if (EventDictionary.TryGetValue(key, out value))
                value.RaiseEvent(key, res);
        }

        // Нужен для синхронизации доступа к DeletedObjects
        private object syncForDelete = new object();

        private bool _keepConnection;
        private bool _isClosed;
        public int PortForCallBack;
        private IPEndPoint _ipEndpoint;
        private TcpListener _server;
        private BlockingCollection<NetworkStream> _nsQueue;
        internal Dictionary<Guid, TaskCompletionSource<object>> AsyncDictionary;
        private Dictionary<Guid, WrapperObjectWithEvents> _eventDictionary;

        // Список для удаления объектов
        //Для уменьшения затрат на межпроцессное взаимодействие будем отправлть
        //Запрос на удаление из хранилища не по 1 объект а пачками количество указанным  в CountDeletedObjects
        private List<int> _deletedObjects;

        private int _countDeletedObjects;
    }
}