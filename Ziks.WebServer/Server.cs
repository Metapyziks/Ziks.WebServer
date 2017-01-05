using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace Ziks.WebServer
{
    public class Server : IComponentContainer
    {
        private struct BoundController
        {
            public readonly UriMatcher Matcher;
            public readonly Func<Controller> Ctor;

            public BoundController( UriMatcher matcher, Func<Controller> ctor )
            {
                Matcher = matcher;
                Ctor = ctor;
            }
        }

        private readonly HttpListener _listener = new HttpListener();

        private readonly Dictionary<Guid, Session> _sessions = new Dictionary<Guid, Session>();
        private readonly List<BoundController> _controllers = new List<BoundController>();

        private bool _running;
        private TaskCompletionSource<bool> _stopEvent;

        public HttpListenerPrefixCollection Prefixes => _listener.Prefixes;
        public ComponentCollection Components { get; } = new ComponentCollection( true );

        public Server()
        {
            AddController<DefaultNotFoundController>( "/" );
        }

        public void AddPrefix( string prefix )
        {
            _listener.Prefixes.Add( prefix );
        }

        public void AddControllers( Assembly assembly )
        {
            foreach ( var type in assembly.GetTypes() )
            {
                if ( !typeof (Controller).IsAssignableFrom( type ) ) continue;

                var attribs = type.GetCustomAttributes<UriPrefixAttribute>().AsArray();
                if ( attribs.Length == 0 ) continue;

                var ctor = type.GetConstructor( Type.EmptyTypes );
                if ( ctor == null ) continue;

                var ctorCall = Expression.New( ctor );
                var lambda = Expression.Lambda<Func<Controller>>( ctorCall ).Compile();

                foreach ( var attrib in attribs )
                {
                    AddController( attrib.Value, lambda );
                }
            }
        }

        public void AddController<TController>( UriMatcher matcher )
            where TController : Controller, new()
        {
            AddController( matcher, () => new TController() );
        }

        public void AddController( UriMatcher matcher, Func<Controller> ctor )
        {
            _controllers.Add( new BoundController( matcher, ctor ) );
        }

        public void Start()
        {
            if ( _running ) return;

            _running = true;
            _stopEvent = new TaskCompletionSource<bool>();

            _listener.Start();
        }

        public async void Run()
        {
            Start();

            while ( true )
            {
                var contextTask = _listener.GetContextAsync();
                var completed = await Task.WhenAny( contextTask, _stopEvent.Task );

                if ( completed != contextTask ) break;

                OnGetContext( contextTask.Result );
            }
        }

        private void OnGetContext( HttpListenerContext context )
        {
            var guid = context.Request.GetSessionGuid();

            Session session;
            if ( guid == Guid.Empty || !_sessions.TryGetValue( guid, out session ) )
            {
                session = new Session( context.Request.RemoteEndPoint?.Address );
                context.Response.SetSessionGuid( session.Guid );
                _sessions.Add( session.Guid, session );
            }

            Controller controller;
            if ( !session.TryGetController( context.Request, out controller ) )
            {
                for ( var i = _controllers.Count - 1; i >= 0; -- i )
                {
                    var bound = _controllers[i];
                    if ( !bound.Matcher.Match( context.Request.Url ).Success ) continue;

                    controller = bound.Ctor();
                    controller.Initialize( bound.Matcher, this );

                    if ( !controller.Service( context, session ) ) continue;

                    session.AddController( controller );
                    return;
                }

                throw new NotImplementedException();
            }

            controller.Service( context, session );
        }

        public void Stop()
        {
            if ( !_running ) return;

            _stopEvent.SetResult( true );
            _running = false;

            _listener.Stop();
        }
    }
}
