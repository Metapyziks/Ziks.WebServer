using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;

namespace Ziks.WebServer
{
    public sealed class ControllerMap
    {
        private class BoundController : IComparable<BoundController>
        {
            public readonly UrlMatcher Matcher;
            public readonly float Priority;
            public readonly Func<Controller> Ctor;

            private readonly string _protoName;

            public BoundController( UrlMatcher matcher, float priority, Func<Controller> ctor )
            {
                Matcher = matcher;
                Priority = priority;
                Ctor = ctor;

                _protoName = ctor().ToString();
            }

            public int CompareTo( BoundController other )
            {
                var compared = Matcher.CompareTo( other.Matcher );
                if ( compared != 0 ) return compared;
                return Priority > other.Priority ? 1 : Priority < other.Priority ? -1 : 0;
            }

            public override string ToString()
            {
                return $"{Matcher} => {_protoName}";
            }
        }
        
        private readonly List<BoundController> _controllers = new List<BoundController>();
        private readonly Server _server;

        private bool _sorted;

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
                    var matcher = UrlMatcher.Parse( attrib.Value );
                    Add( matcher, attrib.Priority, lambda );
                }
            }
        }

        public void Add<TController>( UrlMatcher matcher, float priority = 0f )
            where TController : Controller, new()
        {
            Add( matcher, priority, () => new TController() );
        }

        public void Add( UrlMatcher matcher, Func<Controller> ctor )
        {
            Add( matcher, 0f, ctor );
        }

        public void Add( UrlMatcher matcher, float priority, Func<Controller> ctor )
        {
            _controllers.Add( new BoundController( matcher, priority, ctor ) );
            _sorted = false;
        }

        private void Sort()
        {
            if ( _sorted ) return;

            _controllers.Sort();
            _sorted = true;
        }

        public IEnumerable<Controller> GetMatching( Session session, HttpListenerRequest request )
        {
            Sort();

            for ( var i = _controllers.Count - 1; i >= 0; -- i )
            {
                var bound = _controllers[i];
                if ( !bound.Matcher.Match( request.Url ).Success ) continue;

                Controller controller;
                if ( !session.TryGetController( bound.Matcher, out controller ) )
                {
                    controller = bound.Ctor();
                    controller.Initialize( bound.Matcher, _server );
                }

                yield return controller;
            }
        } 
    }
}
