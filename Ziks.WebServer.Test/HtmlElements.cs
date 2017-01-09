using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ziks.WebServer.Html;

namespace Ziks.WebServer.Test
{
    using static HtmlDocumentHelper;

    [TestClass]
    public class HtmlElements
    {
        [TestMethod]
        public void TestMethod1()
        {
            const string whoToStop = "me";

            var document =
                new html( lang => "en" ) {
                    new head {
                        new title { "Is this absolutely disgusting or what?" }
                    },
                    new body {
                        new div( @class => "some-style", id => "main-container" )
                        {
                            new p { "Here is a paragraph." },
                            new p { "And another paragraph" },
                            Foreach( Enumerable.Range( 0, 5 ), i => new p { $"Procedural paragraph #{i}!" } ),
                            If( DateTime.Now.Year < 2016, new p { "Watch out!" } ),
                            If( DateTime.Now.Year > 2016, new p { "We are safe now!" } ),
                            new p {
                                "Somebody", new span( @class => "span-style" ) { "should" }, $"stop {whoToStop} from doing this."
                            }
                        }
                    }
                };

            Debug.WriteLine( document );
        }
    }
}
