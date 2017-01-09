using Ziks.WebServer.Html;

namespace Ziks.WebServer
{
    [DefaultPriority( DefaultPriority )]
    internal class DefaultNotFoundController : Controller
    {
        public const float DefaultPriority = -1000f;

        [Get( MatchAllUrl = false )]
        public HtmlElement Get()
        {
            throw NotFoundException( true );
        }
    }
}
