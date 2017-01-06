using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web;
using MimeTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ziks.WebServer.Html;

namespace Ziks.WebServer
{
    using static DocumentHelper;

    public sealed class ResponseWriterAttribute : System.Attribute { }

    public class ControllerActionException : Exception
    {
        public HttpListenerRequest Request { get; }
        public bool RequestHandled { get; }
        public HttpStatusCode StatusCode { get; }

        public ControllerActionException( HttpListenerRequest request, bool handled,
            HttpStatusCode statusCode, string message )
            : base( message )
        {
            Request = request;
            RequestHandled = handled;
            StatusCode = statusCode;
        }

        public ControllerActionException( HttpListenerRequest request, bool handled,
            HttpStatusCode statusCode, string message, Exception inner )
            : base( message, inner )
        {
            Request = request;
            RequestHandled = handled;
            StatusCode = statusCode;
        }
    }

    public abstract class Controller
    {
        private const string FormContentType = "application/x-www-form-urlencoded";

        private readonly ControllerActionMap _actionMap;

        private string _entityBodyString;
        private NameValueCollection _formData;

        private UrlMatch _controllerMatch;
        private UrlMatch _actionMatch;

        public UrlMatcher UrlMatcher { get; private set; }

        public DateTime LastRequest { get; private set; }
        public bool IsAlive => true;

        internal bool Initialized => Server != null;

        protected Server Server { get; private set; }

        protected internal HttpListenerRequest Request { get; private set; }
        protected internal HttpListenerResponse Response { get; private set; }

        protected internal HttpMethod HttpMethod { get; private set; }
        protected internal Session Session { get; private set; }

        protected bool IsHead => HttpMethod == HttpMethod.Head;
        protected bool IsGetOrHead => HttpMethod == HttpMethod.Get || IsHead;
        protected bool IsPost => HttpMethod == HttpMethod.Post;

        protected Uri MatchedUrl
        {
            get
            {
                var min = Math.Min( _controllerMatch.Index, _actionMatch.Index );
                var max = Math.Max( _controllerMatch.EndIndex, _actionMatch.EndIndex );

                return new Uri( new Uri( Request.Url.GetLeftPart( UriPartial.Authority ) ),
                    Request.Url.AbsolutePath.Substring( min, max - min ) );
            }
        }

        public bool HasFormData => HasEntityBody && Request.ContentType == FormContentType;
        public bool HasEntityBody => Request.HasEntityBody;

        protected Controller()
        {
            _actionMap = ControllerActionMap.GetActionMap( GetType() );
        }

        internal void Initialize( UrlMatcher matcher, Server server )
        {
            UrlMatcher = matcher;
            Server = server;
            
            LastRequest = DateTime.UtcNow;
        }

        internal bool Service( HttpListenerContext context, Session session )
        {
            Debug.Assert( session == Session || Session == null );
            if ( Session == null ) session.AddController( this );

            Request = context.Request;
            Response = context.Response;
            Session = session;

            switch ( Request.HttpMethod )
            {
                case "GET": HttpMethod = HttpMethod.Get; break;
                case "POST": HttpMethod = HttpMethod.Post; break;
                case "HEAD": HttpMethod = HttpMethod.Head; break;
                case "DELETE": HttpMethod = HttpMethod.Delete; break;
                case "OPTIONS": HttpMethod = HttpMethod.Options; break;
                case "PUT": HttpMethod = HttpMethod.Put; break;
                case "TRACE": HttpMethod = HttpMethod.Trace; break;
                default: HttpMethod = null; break;
            }

            LastRequest = DateTime.UtcNow;

            _entityBodyString = null;
            _formData = null;

            _controllerMatch = UrlMatcher.Match( Request.Url );
            _actionMatch = UrlMatch.Failure;

            try
            {
                return _actionMap.TryInvokeAction( this, context.Request );
            }
            catch ( ControllerActionException e )
            {
                if ( !e.RequestHandled ) return false;

                OnUnhandledException( e );
                return true;
            }
            catch ( Exception e )
            {
                OnUnhandledException( new ControllerActionException( Request, false, HttpStatusCode.InternalServerError, e.Message, e ) );
                return true;
            }
        }

        internal void SetMatchedActionUrl( UrlMatch match )
        {
            _actionMatch = match;
        }

        protected ControllerActionException NotFoundException( bool handled = false )
        {
            var message = $"The requested resource was not found.";
            throw new ControllerActionException( Request, handled, HttpStatusCode.NotFound, message );
        }

        protected virtual void OnUnhandledException( ControllerActionException e )
        {
            Response.StatusCode = (int) e.StatusCode;

            var title = $"{(int) e.StatusCode}: {e.StatusCode}";

            OnServiceHtml( new html
            {
                new head
                {
                    new title {title}
                },
                new body
                {
                    new h2 {title},
                    "Request:", nbsp, new code {Request.Url.ToString()}, br,
                    "Message:", nbsp, new code {e.Message}, br,
                    If( (int) e.StatusCode >= 500 && (int) e.StatusCode < 600 && e.InnerException != null,
                        new code (style=> "padding:8px;white-space:pre-wrap;display:block;") { e.ToString() }
                    )
                }
            } );
        }

        [ResponseWriter]
        protected virtual void OnServiceHtml( Element document )
        {
            Response.ContentType = MimeTypeMap.GetMimeType( ".html" );

            using ( var writer = new StreamWriter( Response.OutputStream ) )
            {
                writer.WriteLine("<!DOCTYPE html>");

                using ( var serializer = new HtmlSerializer( writer ) )
                {
                    document.Serialize( serializer );
                }
            }
        }
        
        [ResponseWriter]
        protected virtual void OnServiceJson( JToken token )
        {
            Response.ContentType = MimeTypeMap.GetMimeType( ".json" );

            using ( var writer = new StreamWriter( Response.OutputStream ) )
            {
                writer.WriteLine( token.ToString( Formatting.Indented ) );
            }
        }
        
        [ResponseWriter]
        protected virtual void OnServiceText( string text )
        {
            Response.ContentType = MimeTypeMap.GetMimeType( ".txt" );

            using ( var writer = new StreamWriter( Response.OutputStream ) )
            {
                writer.WriteLine( text );
            }
        }

        public NameValueCollection GetFormData()
        {
            if ( _formData != null ) return _formData;
            if ( !HasFormData ) return _formData = new NameValueCollection();

            _formData = HttpUtility.ParseQueryString( GetEntityBodyString() );

            return _formData;
        }

        public string GetEntityBodyString()
        {
            if ( _entityBodyString != null ) return _entityBodyString;
            if ( !HasEntityBody ) return _entityBodyString = "";

            using ( var reader = new StreamReader( Request.InputStream ) )
            {
                _entityBodyString = reader.ReadToEnd();
            }

            return _entityBodyString;
        }
    }
}
