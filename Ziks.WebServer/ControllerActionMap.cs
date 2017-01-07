using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace Ziks.WebServer
{
    [AttributeUsage( AttributeTargets.Parameter )]
    public abstract class ActionParameterAttribute : Attribute
    {
        public ActionParameterMethod Method { get; }

        protected ActionParameterAttribute( ActionParameterMethod method )
        {
            Method = method;
        }
    }

    public enum ActionParameterMethod
    {
        Query,
        Form,
        Body,
        Url
    }

    public sealed class QueryAttribute : ActionParameterAttribute
    {
        public QueryAttribute() : base(ActionParameterMethod.Query) { }
    }

    public sealed class FormAttribute : ActionParameterAttribute
    {
        public FormAttribute() : base( ActionParameterMethod.Form ) { }
    }

    public sealed class BodyAttribute : ActionParameterAttribute
    {
        public BodyAttribute() : base( ActionParameterMethod.Body ) { }
    }

    public sealed class UrlAttribute : ActionParameterAttribute
    {
        public UrlAttribute() : base(ActionParameterMethod.Url) { }
    }

    internal class ControllerActionMap
    {
        const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private delegate void ControllerAction( Controller controller );

        private class BoundAction : IComparable<BoundAction>
        {
            public readonly UrlMatcher Matcher;
            public readonly bool MatchAllUrl;
            public readonly float Priority;
            public readonly HttpMethod HttpMethod;
            public readonly ControllerAction Action;
            
            private readonly string _actionName;

            public BoundAction( UrlMatcher matcher, bool matchAllUrl, float priority,
                HttpMethod httpMethod, ControllerAction action, string name )
            {
                Matcher = matcher;
                MatchAllUrl = matchAllUrl;
                Priority = priority;
                HttpMethod = httpMethod;
                Action = action;
                
                _actionName = name;
            }

            public int CompareTo( BoundAction other )
            {
                var compared = Matcher.CompareTo( other.Matcher );
                if ( compared != 0 ) return compared;
                return Priority > other.Priority ? 1 : Priority < other.Priority ? -1 : 0;
            }

            public override string ToString()
            {
                return $"{Matcher} => {_actionName}";
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

        private static class ParameterParsers
        {
            private static string GetRawValue( Controller controller, ActionParameterMethod method, string name )
            {
                switch ( method )
                {
                    case ActionParameterMethod.Query: return controller.Request.QueryString[name];
                    case ActionParameterMethod.Form: return controller.HasFormData ? controller.GetFormData()[name] : null;
                    case ActionParameterMethod.Body: return controller.HasEntityBody ? controller.GetEntityBodyString() : null;
                    case ActionParameterMethod.Url: return controller.UrlSegments[name];
                    default: return null;
                }
            }

            // ReSharper disable UnusedMember.Local
            public static string ReadString( Controller controller, ActionParameterMethod method, string name, string @default )
            {
                return GetRawValue( controller, method, name ) ?? @default;
            }

            public static int ReadInt32( Controller controller, ActionParameterMethod method, string name, int @default )
            {
                int value;
                return int.TryParse( GetRawValue( controller, method, name ), out value ) ? value : @default;
            }

            public static long ReadInt64( Controller controller, ActionParameterMethod method, string name, long @default )
            {
                long value;
                return long.TryParse( GetRawValue( controller, method, name ), out value ) ? value : @default;
            }

            public static bool ReadBoolean( Controller controller, ActionParameterMethod method, string name, bool @default )
            {
                bool value;
                return bool.TryParse( GetRawValue( controller, method, name ), out value ) ? value : @default;
            }

            public static float ReadSingle( Controller controller, ActionParameterMethod method, string name, float @default )
            {
                float value;
                return float.TryParse( GetRawValue( controller, method, name ), out value ) ? value : @default;
            }

            public static double ReadDouble( Controller controller, ActionParameterMethod method, string name, double @default )
            {
                double value;
                return double.TryParse( GetRawValue( controller, method, name ), out value ) ? value : @default;
            }
            // ReSharper restore UnusedMember.Local
        }

        private static Expression GenerateParameterParser( ParameterInfo param, ActionParameterMethod paramMethod, Expression controllerParam )
        {
            var methodAttrib = param.GetCustomAttribute<ActionParameterAttribute>();
            if ( methodAttrib != null )
            {
                paramMethod = methodAttrib.Method;
            }

            var type = param.ParameterType;
            var methodName = $"Read{type.Name}";

            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public;

            var method = typeof (ParameterParsers).GetMethod( methodName, flags );
            if ( method == null ) throw new NotImplementedException();

            var @default = param.HasDefaultValue ? param.DefaultValue
                : type.IsValueType ? Activator.CreateInstance( type ) : null;
            
            var methodConst = Expression.Constant( paramMethod );
            var nameConst = Expression.Constant( param.Name );
            var defaultConst = Expression.Constant( @default, type );
            var call = Expression.Call( method, controllerParam, methodConst, nameConst, defaultConst );

            return call;
        }

        private ControllerAction GenerateAction( MethodInfo method, ControllerActionAttribute attrib )
        {
            var responseWriter = GetResponseWriter( method.ReturnType );
            var parameters = method.GetParameters();

            var controllerParam = Expression.Parameter( typeof (Controller), "controller" );
            var convertedController = Expression.Convert( controllerParam, ControllerType );

            var defaultMethod = attrib.DefaultParameterMethod;

            var queryParsers = new Expression[parameters.Length];
            for ( var i = 0; i < parameters.Length; ++i )
            {
                queryParsers[i] = GenerateParameterParser( parameters[i], defaultMethod, controllerParam );
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

                foreach ( var attrib in attribs )
                {
                    var action = GenerateAction( method, attrib );
                    var matcher = UrlMatcher.Parse( attrib.Value );
                    var httpMethod = attrib is GetAttribute ? HttpMethod.Get
                        : attrib is PostAttribute ? HttpMethod.Post : null;

                    _actions.Add( new BoundAction( matcher, attrib.MatchAllUrl,
                        attrib.Priority, httpMethod, action, method.Name ) );
                }
            }

            _actions.Sort();
        }

        private static bool IsMatchingHttpMethod( HttpMethod action, HttpMethod request )
        {
            return action == request || action == HttpMethod.Get && request == HttpMethod.Head;
        }

        public bool TryInvokeAction( Controller controller, HttpListenerRequest request )
        {
            var prefixMatch = controller.ControllerMatcher.Match( request.Url );
            Debug.Assert( prefixMatch.Success );

            for ( var i = _actions.Count - 1; i >= 0; --i )
            {
                var bound = _actions[i];
                if ( !IsMatchingHttpMethod( bound.HttpMethod, controller.HttpMethod ) ) continue;

                var match = bound.Matcher.Match( request.Url, prefixMatch.EndIndex );
                if ( !match.Success ) continue;
                if ( bound.MatchAllUrl && match.EndIndex < request.Url.AbsolutePath.Length ) continue;

                controller.SetMatchedActionUrl( bound.Matcher, match );
                bound.Action( controller );
                return true;
            }

            return false;
        }
    }
}
