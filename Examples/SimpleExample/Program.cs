using System;
using System.Reflection;
using System.Threading.Tasks;
using Ziks.WebServer;

namespace SimpleExample
{
    public class Program
    {
        [STAThread]
        static void Main( string[] args )
        {
            var server = new Server();

            server.AddPrefix( "http://+:8080/" );
            server.AddControllers( Assembly.GetExecutingAssembly() );

            Task.Run( () => server.Run() );

            Console.ReadKey( true );

            server.Stop();
        }
    }
}
