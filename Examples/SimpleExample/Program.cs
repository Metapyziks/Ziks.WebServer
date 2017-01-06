using System;
using System.IO;
using System.Reflection;
using Ziks.WebServer;

namespace SimpleExample
{
    public class Program
    {
        [STAThread]
        static void Main( string[] args )
        {
            var server = new Server();

            var assemblyDir = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            var resourcesDir = Path.Combine( assemblyDir, "..", "..", "Resources" );

            server.Prefixes.Add( "http://+:8080/" );

            server.Controllers.Add( "/", () => new StaticFileController( resourcesDir ) );
            server.Controllers.Add( Assembly.GetExecutingAssembly() );

            server.Run();
        }
    }
}
