using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using MimeTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ziks.WebServer.Html;

namespace Ziks.WebServer
{
    public sealed class ResponseWriterAttribute : System.Attribute { }

    public abstract class Controller
    {
        private readonly ControllerActionMap _actionMap;

        public UriMatcher UriMatcher { get; private set; }

        public DateTime LastRequest { get; private set; }
        public bool IsAlive => true;

        internal bool Initialized => Server != null;

        protected Server Server { get; private set; }

        protected internal HttpListenerRequest Request { get; private set; }
        protected internal HttpListenerResponse Response { get; private set; }

        protected Session Session { get; private set; }

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
    }
}
