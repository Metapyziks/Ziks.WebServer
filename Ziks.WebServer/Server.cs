using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Ziks.WebServer
{
    public class Server : IComponentContainer
    {
        private readonly HttpListener _listener = new HttpListener();

        private readonly Dictionary<Guid, Session> _sessions = new Dictionary<Guid, Session>();

        private bool _running;
        private TaskCompletionSource<bool> _stopEvent;

        public HttpListenerPrefixCollection Prefixes => _listener.Prefixes;

        public ControllerMap Controllers { get; }
        public ComponentCollection Components { get; }

        public Server()
        {
            Controllers = new ControllerMap( this );
            Controllers.Add<DefaultNotFoundController>( "/", DefaultNotFoundController.DefaultPriority );

            Components = new ComponentCollection( true );

            AppDomain.CurrentDomain.DomainUnload += (sender, e) => Stop();
        }

        public void Start()
        {
            if ( _running ) return;

            _running = true;
            _stopEvent = new TaskCompletionSource<bool>();

            _listener.Start();
        }

        public void Run()
        {
            Start();

            while ( true )
            {
                var contextTask = _listener.GetContextAsync();
                Task.WhenAny( contextTask, _stopEvent.Task ).Wait();

                if ( !contextTask.IsCompleted ) break;

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
            
            var matched = Controllers
                .GetMatching( session, context.Request )
                .FirstOrDefault( matching => matching.Service( context, session ) );

            if ( matched == null ) throw new NotImplementedException();
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
