using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace Ziks.WebServer
{
    internal class ControllerActionMap
    {
        const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private delegate void ControllerAction( Controller controller );

        private struct BoundAction
        {
            public readonly UriMatcher Matcher;
            public readonly HttpMethod Method;
            public readonly ControllerAction Action;

            public BoundAction( UriMatcher matcher, HttpMethod method, ControllerAction action )
            {
                Matcher = matcher;
                Method = method;
                Action = action;
            }
        }

        private static readonly Dictionary<Type, ControllerActionMap> _sCache
            = new Dictionary<Type, ControllerActionMap>();

        public static ControllerActionMap GetActionMap( Type controllerType )
        {
            Debug.Assert( typeof (Controller).IsAssignableFrom( controllerType ) );

            ControllerActionMap map;
            if ( _sCache.TryGetValue( controllerType, out map ) ) return map;

            map = new ControllerActionMap( controllerType );
            _sCache.Add( controllerType, map );
            return map;
        }

        public Type ControllerType { get; }

        private readonly List<BoundAction> _actions = new List<BoundAction>();

        private Dictionary<Type, MethodInfo> _writerCache;

        private void BuildWriterCache()
        {
            _writerCache = new Dictionary<Type, MethodInfo>();

            foreach ( var method in ControllerType.GetMethods( InstanceFlags ) )
            {
                var attribs = method.GetCustomAttributes<ResponseWriterAttribute>( true ).AsArray();
                if ( attribs.Length == 0 ) continue;

                var args = method.GetParameters();
                Debug.Assert( args.Length == 1 );
                
                var type = args[0].ParameterType;

                if ( _writerCache.ContainsKey( type ) ) _writerCache[type] = method;
                else _writerCache.Add( type, method );
            }
        }

        private MethodInfo GetResponseWriter( Type responseType )
        {
            if ( responseType == typeof (void) )
            {
                return null;
            }

            if ( _writerCache == null )
            {
                BuildWriterCache();
                Debug.Assert( _writerCache != null, "_writerCache != null" );
            }

            MethodInfo action;
            if ( _writerCache.TryGetValue( responseType, out action ) ) return action;
                
            throw new NotImplementedException(
                $"No response writer implemented for values of type {responseType}" );
        }

        private static class QueryParsers
        {
            public static string ReadString( Controller controller, string name, string @default )
            {
                return controller.Request.QueryString[name] ?? @default;
            }

            public static int ReadInt32( Controller controller, string name, int @default )
            {
                int value;
                return int.TryParse( controller.Request.QueryString[name], out value ) ? value : @default;
            }

            public static long ReadInt64( Controller controller, string name, long @default )
            {
                long value;
                return long.TryParse( controller.Request.QueryString[name], out value ) ? value : @default;
            }

            public static bool ReadBoolean( Controller controller, string name, bool @default )
            {
                bool value;
                return bool.TryParse( controller.Request.QueryString[name], out value ) ? value : @default;
            }

            public static float ReadSingle( Controller controller, string name, float @default )
            {
                float value;
                return float.TryParse( controller.Request.QueryString[name], out value ) ? value : @default;
            }

            public static double ReadDouble( Controller controller, string name, double @default )
            {
                double value;
                return double.TryParse( controller.Request.QueryString[name], out value ) ? value : @default;
            }
        }

        private Expression GenerateQueryParser( ParameterInfo param, Expression controllerParam )
        {
            var type = param.ParameterType;
            var methodName = $"Read{type.Name}";

            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public;

            var method = typeof (QueryParsers).GetMethod( methodName, flags );
            if ( method == null ) throw new NotImplementedException();

            var @default = param.HasDefaultValue ? param.DefaultValue
                : type.IsValueType ? Activator.CreateInstance( type ) : null;

            var nameConst = Expression.Constant( param.Name );
            var defaultConst = Expression.Constant( @default, type );
            var call = Expression.Call( method, controllerParam, nameConst, defaultConst );

            return call;
        }

        private ControllerAction GenerateAction( MethodInfo method )
        {
            var responseWriter = GetResponseWriter( method.ReturnType );
            var parameters = method.GetParameters();

            var controllerParam = Expression.Parameter( typeof (Controller), "controller" );
            var convertedController = Expression.Convert( controllerParam, ControllerType );

            var queryParsers = new Expression[parameters.Length];
            for ( var i = 0; i < parameters.Length; ++i )
            {
                queryParsers[i] = GenerateQueryParser( parameters[i], controllerParam );
            }

            var call = Expression.Call( convertedController, method, queryParsers );

            if ( responseWriter != null )
            {
                call = Expression.Call( convertedController, responseWriter, call );
            }

            return Expression.Lambda<ControllerAction>( call, controllerParam ).Compile();
        }

        private ControllerActionMap( Type controllerType )
        {
            ControllerType = controllerType;

            foreach ( var method in ControllerType.GetMethods( InstanceFlags ) )
            {
                var attribs = method.GetCustomAttributes<ControllerActionAttribute>( true ).AsArray();
                if ( attribs.Length == 0 ) continue;

                var action = GenerateAction( method );

                foreach ( var attrib in attribs )
                {
                    var matcher = new PrefixMatcher( attrib.Value );
                    var httpMethod = attrib is GetActionAttribute ? HttpMethod.Get
                        : attrib is PostActionAttribute ? HttpMethod.Post : null;

                    _actions.Add( new BoundAction( matcher, httpMethod, action ) );
                }
            }
        }

        public bool TryInvokeAction( Controller controller, HttpListenerRequest request )
        {
            var prefixMatch = controller.UriMatcher.Match( request.Url );
            Debug.Assert( prefixMatch.Success );

            var lastChar = request.Url.AbsolutePath[prefixMatch.Index + prefixMatch.Length - 1];
            if ( lastChar == '/' ) prefixMatch = new UriMatch( prefixMatch.Index, prefixMatch.Length - 1 );

            for ( var i = _actions.Count - 1; i >= 0; --i )
            {
                var bound = _actions[i];
                if ( bound.Method.Method != request.HttpMethod ) continue;
                if ( !bound.Matcher.Match( request.Url, prefixMatch.EndIndex ).Success ) continue;

                bound.Action( controller );
                return true;
            }

            return false;
        }
    }
}
