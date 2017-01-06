using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;

namespace Ziks.WebServer
{
    public sealed class ControllerMap
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
        
        private readonly List<BoundController> _controllers = new List<BoundController>();
        private readonly Server _server;

        internal ControllerMap( Server server )
        {
            _server = server;
        }

        public void Add( Assembly assembly )
        {
            foreach ( var type in assembly.GetTypes() )
            {
                if ( !typeof (Controller).IsAssignableFrom( type ) ) continue;

                var attribs = type.GetCustomAttributes<PrefixAttribute>().AsArray();
                if ( attribs.Length == 0 ) continue;

                var ctor = type.GetConstructor( Type.EmptyTypes );
                if ( ctor == null ) continue;

                var ctorCall = Expression.New( ctor );
                var lambda = Expression.Lambda<Func<Controller>>( ctorCall ).Compile();

                foreach ( var attrib in attribs )
                {
                    Add( attrib.Value, lambda );
                }
            }
        }

        public void Add<TController>( UriMatcher matcher )
            where TController : Controller, new()
        {
            Add( matcher, () => new TController() );
        }

        public void Add( UriMatcher matcher, Func<Controller> ctor )
        {
            _controllers.Add( new BoundController( matcher, ctor ) );
        }

        public IEnumerable<Controller> GetMatching( HttpListenerRequest request )
        {
            for ( var i = _controllers.Count - 1; i >= 0; -- i )
            {
                var bound = _controllers[i];
                if ( !bound.Matcher.Match( request.Url ).Success ) continue;

                var controller = bound.Ctor();
                controller.Initialize( bound.Matcher, _server );

                yield return controller;
            }
        } 
    }
}
