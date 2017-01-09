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
    using static HtmlDocumentHelper;

    /// <summary>
    /// Attribute used to annotate methods that write a return value of a
    /// particular type to a <see cref="HttpListenerResponse"/>.
    /// </summary>
    public sealed class ResponseWriterAttribute : System.Attribute { }

    /// <summary>
    /// An exception thrown while handling a HTTP request routed to a <see cref="Controller"/>
    /// action method.
    /// </summary>
    public class ControllerActionException : Exception
    {
        /// <summary>
        /// The <see cref="HttpListenerRequest"/> that triggered the exception.
        /// </summary>
        public HttpListenerRequest Request { get; }

        /// <summary>
        /// If false, the server will attempt to look for another <see cref="Controller"/> to
        /// route the request to.
        /// </summary>
        public bool RequestHandled { get; }

        /// <summary>
        /// <see cref="HttpStatusCode"/> corresponding to this exception.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Creates an exception from the given HTTP request, status code and message.
        /// </summary>
        /// <param name="request">HTTP request that triggered the exception.</param>
        /// <param name="handled">If true, treat the HTTP request as handled.</param>
        /// <param name="statusCode"><see cref="HttpStatusCode"/> corresponding to this exception.</param>
        /// <param name="message">A message that describes the exception.</param>
        public ControllerActionException( HttpListenerRequest request, bool handled,
            HttpStatusCode statusCode, string message )
            : base( message )
        {
            Request = request;
            RequestHandled = handled;
            StatusCode = statusCode;
        }

        /// <summary>
        /// Creates an exception from the given HTTP request, status code, message and inner exception.
        /// </summary>
        /// <param name="request">HTTP request that triggered the exception.</param>
        /// <param name="handled">If true, treat the HTTP request as handled.</param>
        /// <param name="statusCode"><see cref="HttpStatusCode"/> corresponding to this exception.</param>
        /// <param name="message">A message that describes the exception.</param>
        /// <param name="inner">The exception that caused this exception to be thrown.</param>
        public ControllerActionException( HttpListenerRequest request, bool handled,
            HttpStatusCode statusCode, string message, Exception inner )
            : base( message, inner )
        {
            Request = request;
            RequestHandled = handled;
            StatusCode = statusCode;
        }
    }

    /// <summary>
    /// Base class for types that contain methods for handling HTTP requests, grouped by a
    /// common URL prefix. Controller instances will persist for each user's session.
    /// </summary>
    public abstract class Controller
    {
        private const string FormContentType = "application/x-www-form-urlencoded";

        private readonly ControllerActionMap _actionMap;

        private string _entityBodyString;
        private NameValueCollection _formData;

        private UrlMatch _controllerMatch;
        private UrlMatch _actionMatch;
        private UrlSegmentCollection _urlSegments;

        /// <summary>
        /// The <see cref="UrlMatcher"/> for this <see cref="Controller"/> instance that
        /// matched the current <see cref="Request"/>.
        /// </summary>
        public UrlMatcher ControllerMatcher { get; private set; }
        
        /// <summary>
        /// The <see cref="UrlMatcher"/> for the invoked action method that
        /// matched the current <see cref="Request"/>.
        /// </summary>
        public UrlMatcher ActionMatcher { get; private set; }

        /// <summary>
        /// The concatenation of <see cref="ControllerMatcher"/> and <see cref="ActionMatcher"/>.
        /// </summary>
        public UrlMatcher UrlMatcher { get; private set; }

        /// <summary>
        /// The path segments that the URL of the currently handled request is comprised of.
        /// </summary>
        public UrlSegmentCollection UrlSegments
            => _urlSegments ?? (_urlSegments = UrlMatcher.GetSegments( Request.Url ));

        /// <summary>
        /// UTC time of the last request handled by this instance.
        /// </summary>
        public DateTime LastRequestTime { get; private set; }

        /// <summary>
        /// If false, this controller has expired and should no longer handle requests.
        /// </summary>
        public virtual bool IsAlive => true;

        internal bool Initialized => Server != null;

        /// <summary>
        /// The <see cref="Server"/> instance that received the request being handled.
        /// </summary>
        protected Server Server { get; private set; }

        /// <summary>
        /// The <see cref="HttpListenerRequest"/> currently being handled.
        /// </summary>
        protected internal HttpListenerRequest Request { get; private set; }

        /// <summary>
        /// The <see cref="HttpListenerResponse"/> for the current request.
        /// </summary>
        protected internal HttpListenerResponse Response { get; private set; }

        /// <summary>
        /// The <see cref="HttpMethod"/> for the current request.
        /// </summary>
        protected internal HttpMethod HttpMethod { get; private set; }

        /// <summary>
        /// The <see cref="Session"/> for the current request.
        /// </summary>
        protected internal Session Session { get; private set; }

        /// <summary>
        /// If true, the current request's <see cref="HttpMethod"/> is 'HEAD'.
        /// </summary>
        protected bool IsHead => HttpMethod == HttpMethod.Head;

        /// <summary>
        /// If true, the current request's <see cref="HttpMethod"/> is either 'GET' or 'HEAD'.
        /// </summary>
        protected bool IsGetOrHead => HttpMethod == HttpMethod.Get || IsHead;
        
        /// <summary>
        /// If true, the current request's <see cref="HttpMethod"/> is 'POST'.
        /// </summary>
        protected bool IsPost => HttpMethod == HttpMethod.Post;

        /// <summary>
        /// The substring of the requested URL that was matched by <see cref="ControllerMatcher"/>.
        /// </summary>
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

        /// <summary>
        /// If true, the current request had a body with URL encoded form data.
        /// </summary>
        public bool HasFormData => HasEntityBody && Request.ContentType == FormContentType;

        /// <summary>
        /// If true, the current request had a body containing some data.
        /// </summary>
        public bool HasEntityBody => Request.HasEntityBody;

        /// <summary>
        /// Base constructor for <see cref="Controller"/>.
        /// </summary>
        protected Controller()
        {
            _actionMap = ControllerActionMap.GetActionMap( GetType() );
        }

        internal void Initialize( UrlMatcher matcher, Server server )
        {
            ControllerMatcher = matcher;
            Server = server;
            
            LastRequestTime = DateTime.UtcNow;
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

            LastRequestTime = DateTime.UtcNow;

            _entityBodyString = null;
            _formData = null;
            _urlSegments = null;

            _controllerMatch = ControllerMatcher.Match( Request.Url );
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
#if !DEBUG
            catch ( Exception e )
            {
                OnUnhandledException( new ControllerActionException( Request, false, HttpStatusCode.InternalServerError, e.Message, e ) );
                return true;
            }
#endif
        }

        internal void SetMatchedActionUrl( UrlMatcher matcher, UrlMatch match )
        {
            _actionMatch = match;
            
            ActionMatcher = matcher;
            UrlMatcher = new ConcatenatedPrefixMatcher( ControllerMatcher, ActionMatcher );
        }

        /// <summary>
        /// Helper that throws a <see cref="ControllerActionException"/> representing a 404 error.
        /// </summary>
        /// <param name="handled">
        /// If false, the server will attempt to find another controller to handle the current request.
        /// </param>
        /// <returns>Never returns.</returns>
        /// <exception cref="ControllerActionException">Always thrown.</exception>
        protected ControllerActionException NotFoundException( bool handled = false )
        {
            const string message = "The requested resource was not found.";
            throw new ControllerActionException( Request, handled, HttpStatusCode.NotFound, message );
        }

        /// <summary>
        /// Called when an unhandled <see cref="ControllerActionException"/> is thrown.
        ///
        /// The default implementation will write a simple HTML response describing the error.
        /// </summary>
        /// <param name="e">The exception that was thrown.</param>
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

        /// <summary>
        /// Response writer for action methods that return an <see cref="HtmlElement"/>.
        /// </summary>
        /// <param name="document"><see cref="HtmlElement"/> to be written.</param>
        [ResponseWriter]
        protected virtual void OnServiceHtml( HtmlElement document )
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
        
        /// <summary>
        /// Response writer for action methods that return a <see cref="JToken"/>.
        /// </summary>
        /// <param name="token"><see cref="JToken"/> to be written.</param>
        [ResponseWriter]
        protected virtual void OnServiceJson( JToken token )
        {
            Response.ContentType = MimeTypeMap.GetMimeType( ".json" );

            using ( var writer = new StreamWriter( Response.OutputStream ) )
            {
                writer.WriteLine( token.ToString( Formatting.Indented ) );
            }
        }
        
        /// <summary>
        /// Response writer for action methods that return a <see cref="string"/>.
        /// </summary>
        /// <param name="text"><see cref="string"/> to be written.</param>
        [ResponseWriter]
        protected virtual void OnServiceText( string text )
        {
            Response.ContentType = MimeTypeMap.GetMimeType( ".txt" );

            using ( var writer = new StreamWriter( Response.OutputStream ) )
            {
                writer.WriteLine( text );
            }
        }

        /// <summary>
        /// Gets a <see cref="NameValueCollection"/> for a URL encoded form included
        /// in the current request body.
        /// </summary>
        public NameValueCollection GetFormData()
        {
            if ( _formData != null ) return _formData;
            if ( !HasFormData ) return _formData = new NameValueCollection();

            _formData = HttpUtility.ParseQueryString( GetEntityBodyString() );

            return _formData;
        }

        /// <summary>
        /// Gets the entire body sent in the current request as a string.
        /// </summary>
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
