using System;
using Union;

namespace ClientRun
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            int serverPort = 6891;
            Console.WriteLine("Hello Client!");
            TcpConnector client = new TcpConnector();
            client.Open(TcpConnector.GetAvailablePort(serverPort), 2);
            client.Connect("127.0.0.1", serverPort);

            _wrap = AutoWrapClient.GetProxy(client);
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

            // Удаления из хранилища на стороне сервера происходит пачками по 50 элементов
            // Отрправим оставшиеся
            client.ClearDeletedObject();

            // Отключимся от сервера, закроем все соединения, Tcp/Ip сервер на клиенте
            client.Close();

            Console.Write("Close server? [y][n]: ");
            if (Console.ReadLine().ToLower().Equals("y")) client.CloseServer();
            Console.WriteLine("Press any key...");
            Console.ReadKey();
        }

        private static dynamic _wrap;
    }
}
