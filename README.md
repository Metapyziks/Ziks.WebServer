# Ziks.WebServer
Library for quickly making C# web apps.

## Example

```csharp
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
    
    using static DocumentMethods;
    
    [UriPrefix("/simple")]
    public class SimpleController : Controller
    {
        private readonly List<string> _history = new List<string>();

        [GetAction("/echo")]
        public Element Echo( string value = "nothing" )
        {
            _history.Add( value );

            return new html( lang => "en" ) {
                new head {
                    new title { "Echo Example" }
                },
                new body {
                    new p { $"You said: {value}" }
                }
            };
        }

        [GetAction("/history")]
        public Element History()
        {
            return new html( lang => "en" ) {
                new head {
                    new title { "Echo History" }
                },
                new body {
                    new p { "Here's what you've said before:" },
                    new ul {
                        @foreach( _history, item => new li { item } )
                    }
                }
            };
        }
    }
}
```
