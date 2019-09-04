namespace Client
{
    using System;
    using System.IO;

    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            int port = 6891;
            Console.WriteLine("Hello Client!");
            bool loadLocalServer = false;
            TCPClientConnector connector;

            string dir = string.Empty;
            if (loadLocalServer)
                connector = TCPClientConnector.LoadAndConnectToLocalServer(
                    GetParentDir(dir, 4) + $@"\Server\bin\Debug\netcoreapp2.2\Server.dll");
            else
            {
                //3 параметр отвечает за признак  постоянного соединения с сервером
                //Используется пул из 5 соединений
                connector = new TCPClientConnector("127.0.0.1", port, false);
                port = TCPClientConnector.GetAvailablePort(6892);
                connector.Open(port, 2);
            }

            _wrap = AutoWrapClient.GetProxy(connector);
            // Выведем сообщение в консоли сервера
            string typeStr = typeof(Console).AssemblyQualifiedName;
            var console = _wrap.GetType(typeStr);
            console.WriteLine("Hello from Client");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            Console.WriteLine("Press any key");
            Console.ReadKey();

            // Удаления из хранилища на стороне сервера происходит пачками по 50 элементов
            // Отрправим оставшиеся
            connector.ClearDeletedObject();

            // Отключимся от сервера, закроем все соединения, Tcp/Ip сервер на клиенте
            connector.Close();

            // Если мы запустили процесс сервера
            // То выгрузим его
            if (loadLocalServer) connector.CloseServer();
            Console.WriteLine("Press any key");
            Console.ReadKey();
            //    server.Kill();
        }

        private static string GetParentDir(string dir, int levelUp)
        {
            int start = dir.Length - 1; ;
            int pos = dir.LastIndexOf(Path.DirectorySeparatorChar, start);

            while (pos > 0 && levelUp > 0)
            {
                start = pos - 1;
                pos = dir.LastIndexOf(Path.DirectorySeparatorChar, start);
                levelUp--;
            }
            if (pos > 0)
                return dir.Substring(0, pos);

            return dir;
        }

        private static dynamic _wrap;
    }
}