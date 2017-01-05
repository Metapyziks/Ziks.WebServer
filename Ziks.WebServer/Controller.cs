using System.Net;

namespace Ziks.WebServer
{
    public abstract class Controller
    {
        public UriMatcher UriMatcher { get; private set; }

        protected Server Server { get; private set; }

        protected HttpListenerRequest Request { get; private set; }
        protected HttpListenerResponse Response { get; private set; }

        protected Session Session { get; private set; }

        internal void Initialize( UriMatcher matcher, Server server )
        {
            UriMatcher = matcher;
            Server = server;
        }

        internal void BeginService( HttpListenerRequest request, HttpListenerResponse response, Session session )
        {
            Request = request;
            Response = response;
            Session = session;
        }

        internal void Service( HttpListenerRequest request, HttpListenerResponse response, Session session )
        {
            
        }
    }
}
