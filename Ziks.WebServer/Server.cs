using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Ziks.WebServer
{
    /// <summary>
    /// Request status value returned by <see cref="Server.HandleRequest"/>.
    /// </summary>
    public enum HandleRequestResult
    {
        /// <summary>
        /// A request was successfully handled.
        /// </summary>
        Success = 0,

        /// <summary>
        /// An unhandled exception occurred while handling a request.
        /// For example, if the response stream was unexpectedly closed.
        /// </summary>
        UnhandledException = 1,

        /// <summary>
        /// The server was stopped while waiting for a request to handle.
        /// </summary>
        Stopped = 2
    }

    /// <summary>
    /// Wraps a <see cref="HttpListener"/> with a request routing system to methods in instances
    /// of <see cref="Controller"/>. Also keeps track of client sessions, so that <see cref="Controller"/>
    /// instances are persistent over multiple requests by the same client.
    /// </summary>
    public sealed class Server : IComponentContainer
    {
        private readonly HttpListener _listener = new HttpListener();

        private readonly Dictionary<Guid, Session> _sessions = new Dictionary<Guid, Session>();

        private bool _running;
        private TaskCompletionSource<bool> _stopEvent;

        /// <summary>
        /// Event invoked when an unhandled exception is thrown while attempting to
        /// handle an incoming request.
        /// </summary>
        public event EventHandler<UnhandledExceptionEventArgs> UnhandledException;

        /// <summary>
        /// Gets the <see cref="Uri"/> prefixes handled by the <see cref="HttpListener"/> wrapped
        /// by this instance.
        /// </summary>
        public HttpListenerPrefixCollection Prefixes => _listener.Prefixes;

        /// <summary>
        /// Gets the mapping of <see cref="UrlMatcher"/>s to <see cref="Controller"/> constructors
        /// used to route HTTP requests to <see cref="Controller"/> instances.
        /// </summary>
        public ControllerMap Controllers { get; }

        /// <summary>
        /// Gets a collection of components that can be used to provide interface implementations
        /// for use by <see cref="Controller"/>s.
        /// </summary>
        public ComponentCollection Components { get; }
        
        /// <summary>
        /// Creates a new <see cref="Server"/> that initially has no URI prefixes.
        /// </summary>
        public Server()
        {
            Controllers = new ControllerMap( this );
            Controllers.Add<DefaultNotFoundController>( "/", DefaultNotFoundController.DefaultPriority );

            Components = new ComponentCollection( true );

            AppDomain.CurrentDomain.DomainUnload += (sender, e) => Stop();
        }
        
        /// <summary>
        /// Creates a new <see cref="Server"/> that will listen for incoming HTTP requests on the
        /// given port.
        /// </summary>
        /// <param name="port">Port to listen for HTTP requests on.</param>
        public Server( int port )
            : this()
        {
            Prefixes.Add( $"http://+:{port}/" );
        }

        /// <summary>
        /// Start the <see cref="HttpListener"/>. To handle incoming requests, either repeatedly
        /// call <see cref="HandleRequest()"/> or call <see cref="Run"/> once.
        /// </summary>
        public void Start()
        {
            if ( _running ) return;

            _running = true;
            _stopEvent = new TaskCompletionSource<bool>();

            _listener.Start();
        }
        
        /// <summary>
        /// Blocking method that either handles an individual incoming HTTP request and returns true,
        /// or aborts and returns false if the server was stopped.
        /// </summary>
        /// <returns>True if a request was handled, or false if the server was stopped.</returns>
        public HandleRequestResult HandleRequest()
        {
            var task = HandleRequestAsync();
            task.Wait();
            return task.Result;
        }

        private static Exception ReduceException( Exception e )
        {
            var aggregate = e as AggregateException;
            if ( aggregate != null ) return ReduceException( aggregate.InnerExceptions.First() );

            return e.InnerException ?? e;
        }

        /// <summary>
        /// Asynchronously handles an individual incoming HTTP request, or aborts if the server was
        /// stopped.
        /// </summary>
        /// <returns>True if a request was handled, or false if the server was stopped.</returns>
        public async Task<HandleRequestResult> HandleRequestAsync()
        {
            var contextTask = _listener.GetContextAsync();

            try
            {
                await Task.WhenAny( contextTask, _stopEvent.Task );
            }
            catch ( Exception e )
            {
                UnhandledException?.Invoke( this, new UnhandledExceptionEventArgs( ReduceException( e ), false ) );
                return HandleRequestResult.UnhandledException;
            }

            if ( !contextTask.IsCompleted ) return HandleRequestResult.Stopped;

            try
            {
                OnGetContext( contextTask.Result );
            }
            catch ( Exception e )
            {
                UnhandledException?.Invoke( this, new UnhandledExceptionEventArgs( ReduceException( e ), false ) );
                return HandleRequestResult.UnhandledException;
            }

            return HandleRequestResult.Success;
        }

        /// <summary>
        /// Start the <see cref="HttpListener"/> and enter a loop listening for new requests.
        /// This will block until a call to <see cref="Stop"/> occurs.
        /// </summary>
        public void Run()
        {
            Start();
            while ( HandleRequest() != HandleRequestResult.Stopped ) { }
        }

        private void OnGetContext( HttpListenerContext context )
        {
            var guid = context.Request.GetSessionGuid();

            Session session;
            lock ( this )
            {
                if ( guid == Guid.Empty || !_sessions.TryGetValue( guid, out session ) )
                {
                    session = new Session( context.Request.RemoteEndPoint?.Address );
                    context.Response.SetSessionGuid( session.Guid );
                    _sessions.Add( session.Guid, session );
                }
            }

            var matched = Controllers
                .GetMatching( session, context.Request )
                .FirstOrDefault( matching => matching.Service( context, session ) );

            if ( matched == null ) throw new NotImplementedException();
        }

        /// <summary>
        /// Stops the wrapped <see cref="HttpListener"/>.
        /// </summary>
        public void Stop()
        {
            if ( !_running ) return;

            _stopEvent.SetResult( true );
            _running = false;

            _listener.Stop();
        }
    }
}
