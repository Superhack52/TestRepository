using System;
using TestLibrary;

internal class Program
{
    private static void Main(string[] args)
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

        var server = new ServerRPC.TCPConnector();
        server.Open(port);
        server.WaitIsRunning.Task.Wait();
    }
}

public class TestClass //: ITestClassInterface
{
    public int Id { get; set; }

    public void Name()
    {
        Console.WriteLine("Значение из тестового примера.");
    }
}