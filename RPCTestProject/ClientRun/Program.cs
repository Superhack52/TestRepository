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
            TcpConnector connector;
            connector = new TcpConnector("127.0.0.1", port, false);
            port = TcpConnector.GetAvailablePort(6892);
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

            var testClass = (_wrap.GetType("ServerRun.TestClass, ServerRun, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"))._new();
            var text = testClass.Name();
            Console.WriteLine("Текст из клиента + " + text);
            console.WriteLine(text);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            Console.WriteLine("Press any key");
            Console.ReadKey();

            // Удаления из хранилища на стороне сервера происходит пачками по 50 элементов
            // Отрправим оставшиеся
            connector.ClearDeletedObject();

            // Отключимся от сервера, закроем все соединения, Tcp/Ip сервер на клиенте
            connector.CloseServer();

            // Если мы запустили процесс сервера
            // То выгрузим его
            //if (loadLocalServer)
            connector.CloseServerClient();

            Console.ReadKey();
        }

        private static dynamic _wrap;
    }
}
