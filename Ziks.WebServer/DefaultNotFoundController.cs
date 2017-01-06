using Ziks.WebServer.Html;

namespace Ziks.WebServer
{
    internal class DefaultNotFoundController : Controller
    {
        [Get]
        public Element Get()
        {
            throw NotFoundException( true );
        }
    }
}
