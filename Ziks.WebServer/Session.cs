using System;
using System.Collections.Generic;
using System.Net;

namespace Ziks.WebServer
{
    public sealed class Session : IComponentContainer
    {
        public Guid Guid { get; private set; }
        public IPAddress RemoteAddress { get; private set; }

        public ComponentCollection Components { get; } = new ComponentCollection( false );

        private readonly List<Controller> _controllers = new List<Controller>();

        public Session( IPAddress remoteAddress )
        {
            Guid = Guid.NewGuid();
            RemoteAddress = remoteAddress;
        }

        public bool MatchesRequest( HttpListenerRequest request )
        {
            if ( Guid == Guid.Empty ) return false;
            if ( RemoteAddress == null || request.RemoteEndPoint == null ) return false;
            if ( !request.RemoteEndPoint.Address.Equals( RemoteAddress ) ) return false;
            
            return request.GetSessionGuid() == Guid;
        }

        public bool TryGetController( UrlMatcher matcher, out Controller controller )
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

        public void AddController( Controller controller )
        {
            _controllers.Add( controller );
        }
    }
}
