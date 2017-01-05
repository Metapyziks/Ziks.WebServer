using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Ziks.WebServer
{
    internal static class Extensions
    {
        private const string SessionCookieName = "Ziks.WebServer.Session";

        public static T[] AsArray<T>( this IEnumerable<T> enumerable )
        {
            return enumerable as T[] ?? enumerable.ToArray();
        }

        public static Guid GetSessionGuid( this HttpListenerRequest request )
        {
            var sessionId = request.Cookies[SessionCookieName];
            if ( sessionId == null || sessionId.Expired ) return Guid.Empty;

            Guid parsed;
            if ( !Guid.TryParse( sessionId.Value, out parsed ) ) return Guid.Empty;

            return parsed;
        }

        public static void SetSessionGuid( this HttpListenerResponse response, Guid value )
        {
            var cookie = new Cookie( SessionCookieName, value.ToString(), "/" )
            {
                Expires = DateTime.UtcNow.AddDays( 1.0 )
            };

            response.SetCookie( cookie );
        }
    }
}
