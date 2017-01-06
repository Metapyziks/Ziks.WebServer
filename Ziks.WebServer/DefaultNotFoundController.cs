using Ziks.WebServer.Html;

namespace Ziks.WebServer
{
    internal class DefaultNotFoundController : Controller
    {
        [Get( MatchAllUrl = false )]
        public Element Get()
        {
            throw NotFoundException( true );
        }
    }
}
