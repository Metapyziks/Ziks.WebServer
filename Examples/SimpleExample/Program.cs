using System;
using System.Reflection;
using System.Threading.Tasks;
using Ziks.WebServer;

namespace SimpleExample
{
    public interface IProgram
    {
        void WriteLine( string message );
    }

    public class Program : IProgram
    {
        [STAThread]
        static void Main( string[] args )
        {
            var program = new Program();
            program.Run();
        }

        public void Run()
        {
            var server = new Server();

            server.AddPrefix( "http://+:8080/" );

            server.Components.Add<IProgram>( this );
            server.AddControllers( Assembly.GetExecutingAssembly() );

            Task.Run( () => server.Run() );

            Console.ReadKey( true );

            server.Stop();
        }

        public void WriteLine( string message )
        {
            Console.WriteLine( message );
        }
    }
}
