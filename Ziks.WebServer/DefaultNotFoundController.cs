using Ziks.WebServer.Html;

namespace Ziks.WebServer
{
    internal class DefaultNotFoundController : Controller
    {
        public const float DefaultPriority = -1000f;

        [Get( MatchAllUrl = false )]
        public Element Get()
        {
            throw NotFoundException( true );
        }
    }
}
