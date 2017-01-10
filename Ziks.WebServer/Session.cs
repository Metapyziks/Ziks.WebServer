using System;
using System.Collections.Generic;
using System.Net;

namespace Ziks.WebServer
{
    /// <summary>
    /// Used to associate any requests from the same client within the same session. Contains
    /// a <see cref="ComponentCollection"/> so that extra state can be associated with a session.
    /// </summary>
    public sealed class Session : IComponentContainer
    {
        /// <summary>
        /// Globally unique identifier for this session.
        /// </summary>
        public Guid Guid { get; private set; }

        /// <summary>
        /// The IP address of the client this session belongs to.
        /// </summary>
        public IPAddress RemoteAddress { get; private set; }

        /// <summary>
        /// Can contain component instances used to associate additional state to this session.
        /// </summary>
        public ComponentCollection Components { get; } = new ComponentCollection( false );

        private readonly List<Controller> _controllers = new List<Controller>();

        internal Session( IPAddress remoteAddress )
        {
            Guid = Guid.NewGuid();
            RemoteAddress = remoteAddress;
        }

        /// <summary>
        /// Tests to see if the given request should belong to this session.
        /// </summary>
        /// <param name="request">Request to test.</param>
        public bool MatchesRequest( HttpListenerRequest request )
        {
            if ( Guid == Guid.Empty ) return false;
            if ( RemoteAddress == null || request.RemoteEndPoint == null ) return false;
            if ( !request.RemoteEndPoint.Address.Equals( RemoteAddress ) ) return false;
            
            return request.GetSessionGuid() == Guid;
        }

        internal bool TryGetController( UrlMatcher matcher, out Controller controller )
        {
            foreach ( var active in _controllers )
            {
                if ( active.ControllerMatcher != matcher ) continue;

                controller = active;
                return true;
            }

            controller = null;
            return false;
        }

        internal void AddController( Controller controller )
        {
            _controllers.Add( controller );
        }
    }
}
