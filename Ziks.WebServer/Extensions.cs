using System;
using System.Net;

namespace Ziks.WebServer
{
    internal static class Extensions
    {
        public static Guid GetSessionGuid( this HttpListenerRequest request )
        {
            var sessionId = request.Cookies["Ziks.WebServer.Session"];
            if ( sessionId == null || sessionId.Expired ) return Guid.Empty;

            Guid parsed;
            if ( !Guid.TryParse( sessionId.Value, out parsed ) ) return Guid.Empty;

            return parsed;
        }
    }
}
