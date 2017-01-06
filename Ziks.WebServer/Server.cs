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
            Controllers.Add<DefaultNotFoundController>( "/" );

            Components = new ComponentCollection( true );
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
                var matched = Controllers
                    .GetMatching( context.Request )
                    .FirstOrDefault( matching => matching.Service( context, session ) );

                if ( matched == null ) throw new NotImplementedException();

                session.AddController( matched );
                return;
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
