using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ziks.WebServer
{
    /// <summary>
    /// Interface for types containing a <see cref="ComponentCollection"/>.
    /// </summary>
    public interface IComponentContainer
    {
        /// <summary>
        /// Gets a <see cref="ComponentCollection"/> contained in this instance.
        /// </summary>
        ComponentCollection Components { get; }
    }

    /// <summary>
    /// Holds a set of component interface implementations.
    /// </summary>
    public sealed class ComponentCollection
    {
        private readonly Dictionary<Type, object> _components = new Dictionary<Type, object>();

        /// <summary>
        /// If true, only instances with a specified interface type can be added.
        /// </summary>
        public bool RequireInterfaces { get; }

        /// <summary>
        /// Creates a new empty <see cref="ComponentCollection"/>.
        /// </summary>
        /// <param name="requireInterfaces">
        /// If true, only instances with a specified interface type can be added.
        /// </param>
        public ComponentCollection( bool requireInterfaces )
        {
            RequireInterfaces = requireInterfaces;
        }

        /// <summary>
        /// Removes all interface implementations from this instance.
        /// </summary>
        public void Clear()
        {
            _components.Clear();
        }
        
        /// <summary>
        /// Add a component interface implementation. Will replace instances previously
        /// added of the same interface type.
        /// </summary>
        /// <param name="implementation">Interface instance to add.</param>
        /// <typeparam name="TInterface">Interface type of the instance.</typeparam>
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

        /// <summary>
        /// Remove a component interface implementation.
        /// </summary>
        /// <typeparam name="TInterface">Interface type to remove.</typeparam>
        /// <returns>True if an element was removed.</returns>
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

        /// <summary>
        /// Tries to get an added implementation of the given interface type.
        /// Returns null if none is found.
        /// </summary>
        /// <typeparam name="TInterface">Interface type to retrieve.</typeparam>
        /// <returns>An interface implementation, or null if no instance is found.</returns>
        public TInterface Get<TInterface>()
            where TInterface : class
        {
            object value;
            return _components.TryGetValue( typeof (TInterface), out value ) ? (TInterface) value : null;
        }
    }
}
