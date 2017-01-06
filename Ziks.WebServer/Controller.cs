using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Web;
using MimeTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ziks.WebServer.Html;

namespace Ziks.WebServer
{
    public sealed class ResponseWriterAttribute : System.Attribute { }

    public abstract class Controller
    {
        private const string FormContentType = "application/x-www-form-urlencoded";

        private readonly ControllerActionMap _actionMap;

        private string _entityBodyString;
        private NameValueCollection _formData;

        public UriMatcher UriMatcher { get; private set; }

        public DateTime LastRequest { get; private set; }
        public bool IsAlive => true;

        internal bool Initialized => Server != null;

        protected Server Server { get; private set; }

        protected internal HttpListenerRequest Request { get; private set; }
        protected internal HttpListenerResponse Response { get; private set; }

        protected Session Session { get; private set; }

        public bool HasFormData => HasEntityBody && Request.ContentType == FormContentType;
        public bool HasEntityBody => Request.HasEntityBody;

        protected Controller()
        {
            _actionMap = ControllerActionMap.GetActionMap( GetType() );
        }

        internal void Initialize( UriMatcher matcher, Server server )
        {
            UriMatcher = matcher;
            Server = server;
            
            LastRequest = DateTime.UtcNow;
        }

        internal bool Service( HttpListenerContext context, Session session )
        {
            Debug.Assert( session == Session || Session == null );

            Request = context.Request;
            Response = context.Response;
            Session = session;

            LastRequest = DateTime.UtcNow;

            _entityBodyString = null;
            _formData = null;

            return _actionMap.TryInvokeAction( this, context.Request );
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
