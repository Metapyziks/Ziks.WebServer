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

            server.Prefixes.Add( "http://+:8080/" );
            server.Controllers.Add( Assembly.GetExecutingAssembly() );

            Task.Run( () => server.Run() );

            Console.ReadKey( true );

            server.Stop();
        }
    }
}
