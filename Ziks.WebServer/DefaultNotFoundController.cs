using Ziks.WebServer.Html;

namespace Ziks.WebServer
{
    using static DocumentHelper;

    internal class DefaultNotFoundController : Controller
    {
        [Get]
        public Element Get()
        {
            Response.StatusCode = 404;

            return new html( lang => "en" ) {
                new head {
                    new title { "404: Resource not found" }
                },
                new body {
                    "The document or resource you requested was not found.", br,
                    "Requested URL: ", new code { Request.Url.AbsolutePath }
                }
            };
        }
    }
}
