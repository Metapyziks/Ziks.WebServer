using System.Collections.Generic;
using Ziks.WebServer;
using Ziks.WebServer.Html;

namespace SimpleExample
{
    using static HtmlDocumentHelper;
    
    [Prefix( "/simple" )]
    public class SimpleController : Controller
    {
        private readonly List<string> _history = new List<string>();

        [Get( "/echo/{value}" )]
        public HtmlElement Echo( [Url] string value = "nothing" )
        {
            _history.Add( value );

            return new html( lang => "en" )
            {
                new head
                {
                    new title {"Echo Example"}
                },
                new body
                {
                    new p {$"You said: {value}"},
                    new p {new a( href => "/simple/history" ) {"History"}}
                }
            };
        }

        [Get( "/history" )]
        public HtmlElement History() =>
            new html( lang => "en" )
            {
                new head
                {
                    new title {"Echo History"}
                },
                new body
                {
                    new p {"Here's what you've said before:"},
                    new ul
                    {
                        Foreach( _history, item => new li {item} )
                    }
                }
            };

        [Get( "/form-test" )]
        public HtmlElement FormTest() =>
            new html( lang => "en" )
            {
                new head
                {
                    new title {"Form Test"}
                },
                new body
                {
                    new form( action => Request.Url.AbsolutePath, method => "post" )
                    {
                        "First name:", new input( type => "text", name => "firstName", value => "John" ), br,
                        "Last name:", new input( type => "text", name => "lastName", value => "Doe" ), br, br,
                        new input( type => "submit", value => "Submit" )
                    }
                }
            };

        [Post( "/form-test", Form = true )]
        public HtmlElement FormTest( string firstName, string lastName ) =>
            new html( lang => "en" )
            {
                new head
                {
                    new title {"Form Test Result"}
                },
                new body
                {
                    new p {"Here's what you submitted:"},
                    new ul
                    {
                        new li {$"First name: {firstName}"},
                        new li {$"Last name: {lastName}"}
                    },
                    new p {new a( href => "form-test" ) {"Again!"}}
                }
            };
    }
}
