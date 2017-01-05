using System.Collections.Generic;
using Ziks.WebServer.Html;

namespace Ziks.WebServer
{
    using static DocumentMethods;
    
    [Prefix("/simple")]
    public class SimpleController : Controller
    {
        private readonly List<string> _history = new List<string>();

        [Prefix("/echo")]
        public Element GetEcho( string value )
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
        
        [Prefix("/history")]
        public Element GetHistory()
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
