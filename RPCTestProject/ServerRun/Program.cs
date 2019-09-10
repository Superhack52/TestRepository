using System;
using Union;

namespace ServerRun
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello Server!");
            //    Console.ReadLine();
            int port = 6891;
            if (args.Length > 0)
            {
                port = int.Parse(args[0]);
            }

            Console.WriteLine("Port=" + port);

            var server = new TcpConnector();
            server.OpenServer(port);

            server.WaitIsRunning.Task.Wait();
        }
    }
}
