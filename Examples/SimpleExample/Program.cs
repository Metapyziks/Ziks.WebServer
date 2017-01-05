using System;
using System.Reflection;
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

            server.AddInterface<IProgram>( this );
            server.AddControllers( Assembly.GetExecutingAssembly() );

            server.Start();

            Console.ReadKey( true );

            server.Stop();
        }

        public void WriteLine( string message )
        {
            Console.WriteLine( message );
        }
    }
}
