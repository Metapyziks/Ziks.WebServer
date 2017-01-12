using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ziks.WebServer.Html;

namespace Ziks.WebServer.Test
{
    using static HtmlDocumentHelper;

    [TestClass]
    public class ContainerHtmlElements
    {
        [TestMethod]
        public void Text1()
        {
            var title = new title {Text = "Hello world!"};
            Assert.AreEqual( "<title>Hello world!</title>", title.ToString().Trim() );
        }
        
        [TestMethod]
        public void Text2()
        {
            var title = new title {"Hello world!"};
            Assert.AreEqual( "Hello world!", title.Text );
        }
    }
}
