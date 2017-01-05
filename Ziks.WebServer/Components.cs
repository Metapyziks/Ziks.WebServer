using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ziks.WebServer
{
    public interface IComponentContainer
    {
        ComponentCollection Components { get; }
    }

    public sealed class ComponentCollection
    {
        private readonly Dictionary<Type, object> _components = new Dictionary<Type, object>();

        public bool RequireInterfaces { get; }

        public ComponentCollection( bool requireInterfaces )
        {
            RequireInterfaces = requireInterfaces;
        }

        public void Clear()
        {
            _components.Clear();
        }
        
        public void Add<TInterface>( TInterface implementation )
            where TInterface : class
        {
            var type = typeof (TInterface);

            Debug.Assert( !RequireInterfaces || type.IsInterface, "Expected TInterface to be an interface." );

            if ( _components.ContainsKey( type ) )
            {
                _components[type] = implementation;
            }
            else
            {
                _components.Add( type, implementation );
            }
        }

        public bool Remove<TInterface>()
            where TInterface : class
        {
            if ( _components.ContainsKey( typeof (TInterface) ) )
            {
                _components.Remove( typeof (TInterface) );
                return true;
            }

            return false;
        }

        public TInterface Get<TInterface>()
            where TInterface : class
        {
            object value;
            return _components.TryGetValue( typeof (TInterface), out value ) ? (TInterface) value : null;
        }
    }
}
