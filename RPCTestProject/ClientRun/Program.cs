using System;
using Union;

namespace ClientRun
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            int port = 6891;
            Console.WriteLine("Hello Client!");
            //bool loadLocalServer = false;
            TCPClientConnector connector;

            //string dir = string.Empty;
            //if (loadLocalServer)
            //    connector = TCPClientConnector.LoadAndConnectToLocalServer(
            //GetParentDir(dir, 4) + $@"\Server\bin\Debug\netcoreapp2.2\Server.dll");
            //else
            //{
            //3 параметр отвечает за признак  постоянного соединения с сервером
            //Используется пул из 5 соединений
            connector = new TCPClientConnector("127.0.0.1", port, false);
            port = TCPClientConnector.GetAvailablePort(6892);
            connector.Open(port, 2);
            //}

            _wrap = AutoWrapClient.GetProxy(connector);
            // Выведем сообщение в консоли сервера
            string typeStr = typeof(Console).AssemblyQualifiedName;
            var console = _wrap.GetType(typeStr);
            console.WriteLine("Hello from Client");

            var str = string.Empty;
            while (!str.ToLower().Equals("exit"))
            {
                Console.Write("Write text to server: ");
                str = Console.ReadLine();
                console.WriteLine(str);
            }

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
            //if (loadLocalServer)
            connector.CloseServer();

            Console.ReadKey();
        }

        private static dynamic _wrap;
    }
}
