using System;
using System.Collections.Generic;
using System.Net;

namespace Ziks.WebServer
{
    public sealed class Session
    {
        public Guid Guid { get; private set; }
        public IPEndPoint LastEndPoint { get; private set; }

        private readonly List<Controller> _controllers = new List<Controller>(); 

        public bool MatchesRequest( HttpListenerRequest request )
        {
            if ( Guid == Guid.Empty ) return false;
            if ( LastEndPoint == null || request.RemoteEndPoint == null ) return false;
            if ( !request.RemoteEndPoint.Address.Equals( LastEndPoint.Address ) ) return false;
            
            return request.GetSessionGuid() == Guid;
        }

        public bool TryGetController( HttpListenerRequest request, out Controller controller )
        {
            foreach ( var active in _controllers )
            {
                if ( active.UriMatcher.Matches( request.Url ) )
                {
                    controller = active;
                    return true;
                }
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
