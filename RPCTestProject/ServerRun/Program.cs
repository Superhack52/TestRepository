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
            Console.WriteLine(typeof(TestClass).AssemblyQualifiedName);

            var server = new TcpConnector();
            server.Open(port, 2);

            server.WaitIsRunning.Task.Wait();
        }
    }
    public class TestClass //: ITestClassInterface
    {
        public int Id { get; set; }

        public string Name()
        {
            var value = "Значение из тестового примера.";
            Console.WriteLine(value);
            return value;
        }
    }
}
