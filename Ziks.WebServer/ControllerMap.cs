using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;

namespace Ziks.WebServer
{
    /// <summary>
    /// Controls a mapping of <see cref="UrlMatcher"/>s to <see cref="Controller"/> constructors.
    /// </summary>
    public sealed class ControllerMap
    {
        private class BoundController : IComparable<BoundController>
        {
            public readonly UrlMatcher Matcher;
            public readonly Func<Controller> Ctor;
            
            private readonly float _priority;
            private readonly Type _controllerType;

            public BoundController( UrlMatcher matcher, float priority, Type controllerType, Func<Controller> ctor )
            {
                Matcher = matcher;
                Ctor = ctor;
                
                _priority = priority;
                _controllerType = controllerType;
            }

            public int CompareTo( BoundController other )
            {
                var compared = Matcher.CompareTo( other.Matcher );
                if ( compared != 0 ) return compared;
                return _priority > other._priority ? 1 : _priority < other._priority ? -1 : 0;
            }

            public override string ToString()
            {
                return $"{Matcher} => {_controllerType}";
            }
        }
        
        private readonly List<BoundController> _controllers = new List<BoundController>();
        private readonly Server _server;

        private bool _sorted;

        internal ControllerMap( Server server )
        {
            _server = server;
        }

        /// <summary>
        /// Adds all <see cref="Controller"/> types found in the given assembly that
        /// are annotated with a <see cref="PrefixAttribute"/>.
        /// </summary>
        /// <param name="assembly">Assembly to search for <see cref="Controller"/> types.</param>
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
                    Add( matcher, attrib.Priority, type, lambda );
                }
            }
        }

        /// <summary>
        /// Maps the default constructor of the given controller type to the given
        /// <see cref="UrlMatcher"/> with an optional priority.
        /// </summary>
        /// <typeparam name="TController">Type of the controller constructor to map.</typeparam>
        /// <param name="matcher"><see cref="UrlMatcher"/> that will map to the given controller constructor.</param>
        /// <param name="priority">Optional priority used when sorting controllers.</param>
        public void Add<TController>( UrlMatcher matcher, float priority = float.NaN )
            where TController : Controller, new()
        {
            Add( matcher, priority, typeof(TController), () => new TController() );
        }
        
        /// <summary>
        /// Maps the given controller constructor to the given <see cref="UrlMatcher"/> with a specific priority.
        /// </summary>
        /// <typeparam name="TController">Type of the controller constructor to map.</typeparam>
        /// <param name="matcher"><see cref="UrlMatcher"/> that will map to the given controller constructor.</param>
        /// <param name="priority">Priority used when sorting controllers.</param>
        /// <param name="ctor">Controller constructor to map.</param>
        public void Add<TController>( UrlMatcher matcher, float priority, Func<TController> ctor )
            where TController : Controller
        {
            Add( matcher, priority, typeof(TController), ctor );
        }
        
        /// <summary>
        /// Maps the given controller constructor to the given <see cref="UrlMatcher"/>.
        /// </summary>
        /// <typeparam name="TController">Type of the controller constructor to map.</typeparam>
        /// <param name="matcher"><see cref="UrlMatcher"/> that will map to the given controller constructor.</param>
        /// <param name="ctor">Controller constructor to map.</param>
        public void Add<TController>( UrlMatcher matcher, Func<TController> ctor )
            where TController : Controller
        {
            Add( matcher, float.NaN, ctor );
        }

        /// <summary>
        /// Maps the given controller constructor to the given <see cref="UrlMatcher"/> with a specific priority.
        /// </summary>
        /// <param name="matcher"><see cref="UrlMatcher"/> that will map to the given controller constructor.</param>
        /// <param name="priority">Priority used when sorting controllers.</param>
        /// <param name="controllerType">Type of the controller constructor to map.</param>
        /// <param name="ctor">Controller constructor to map.</param>
        public void Add( UrlMatcher matcher, float priority, Type controllerType, Func<Controller> ctor )
        {
            if ( float.IsNaN( priority ) )
            {
                var defaultPriority = controllerType.GetCustomAttribute<DefaultPriorityAttribute>();
                priority = defaultPriority == null ? 0f : defaultPriority.Value;
            }

            _controllers.Add( new BoundController( matcher, priority, controllerType, ctor ) );
            _sorted = false;
        }

        private void Sort()
        {
            if ( _sorted ) return;

            _controllers.Sort();
            _sorted = true;
        }

        /// <summary>
        /// Constructs <see cref="Controller"/> instances that match the given request's URL.
        /// </summary>
        /// <param name="session">Current client session.</param>
        /// <param name="request"><see cref="HttpListenerRequest"/> to match.</param>
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
