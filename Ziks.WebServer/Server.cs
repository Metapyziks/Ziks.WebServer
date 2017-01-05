using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;

namespace Ziks.WebServer
{
    public class Server
    {
        private readonly HttpListener _listener = new HttpListener();

        private readonly Dictionary<Type, object> _interfaces = new Dictionary<Type, object>();
        private readonly Dictionary<Guid, Session> _sessions = new Dictionary<Guid, Session>();
        private readonly Dictionary<UriMatcher, Func<Controller>> _controllerCtors = new Dictionary<UriMatcher, Func<Controller>>();

        public void AddPrefix( string prefix )
        {
            throw new NotImplementedException();
        }

        public void AddInterface<TInterface>( TInterface implementation )
            where TInterface : class
        {
            var type = typeof (TInterface);

            Debug.Assert( type.IsInterface, "Expected TInterface to be an interface." );

            if ( _interfaces.ContainsKey( type ) )
            {
                _interfaces[type] = implementation;
            }
            else
            {
                _interfaces.Add( type, implementation );
            }
        }

        public TInterface GetInterface<TInterface>()
            where TInterface : class
        {
            object value;
            return _interfaces.TryGetValue( typeof (TInterface), out value ) ? (TInterface) value : null;
        }

        public void AddControllers( Assembly assembly )
        {
            foreach ( var type in assembly.GetTypes() )
            {
                if ( !typeof (Controller).IsAssignableFrom( type ) ) continue;

                var ctor = type.GetConstructor( Type.EmptyTypes );
                if ( ctor == null ) continue;

                var ctorCall = Expression.New( ctor );
                var lambda = Expression.Lambda<Func<Controller>>( ctorCall ).Compile();

                foreach ( var attrib in ctor.GetCustomAttributes<PrefixAttribute>() )
                {
                    AddController( attrib.Value, lambda );
                }
            }
        }

        public void AddController<TController>( UriMatcher matcher )
            where TController : Controller, new()
        {
            AddController( matcher, () => new TController() );
        }

        public void AddController( UriMatcher matcher, Func<Controller> ctor )
        {
            _controllerCtors.Add( matcher, ctor );
        }

        public void Start()
        {
            _listener.Start();
        }

        public void Stop()
        {
            _listener.Stop();
        }
    }
}
