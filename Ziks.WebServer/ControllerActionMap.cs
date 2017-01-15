using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ziks.WebServer
{
    /// <summary>
    /// Base class for attributes that specify where the value of a controller
    /// action method parameter should be found.
    /// </summary>
    [AttributeUsage( AttributeTargets.Parameter )]
    public abstract class ActionParameterAttribute : Attribute
    {
        internal ActionParameterMethod Method { get; }

        internal ActionParameterAttribute( ActionParameterMethod method )
        {
            Method = method;
        }
    }

    internal enum ActionParameterMethod
    {
        Query,
        Form,
        Body,
        Url
    }

    /// <summary>
    /// Attribute that specifies that a controller action parameter comes
    /// from the query part of a request's URL.
    /// </summary>
    public sealed class QueryAttribute : ActionParameterAttribute
    {
        /// <summary>Creates a new <see cref="QueryAttribute"/>.</summary>
        public QueryAttribute() : base( ActionParameterMethod.Query ) { }
    }
    
    /// <summary>
    /// Attribute that specifies that a controller action parameter comes
    /// from a field in a form submitted using POST.
    /// </summary>
    public sealed class FormAttribute : ActionParameterAttribute
    {
        /// <summary>Creates a new <see cref="FormAttribute"/>.</summary>
        public FormAttribute() : base( ActionParameterMethod.Form ) { }
    }
    
    /// <summary>
    /// Attribute that specifies that a controller action parameter is the
    /// body of a POST request.
    /// </summary>
    public sealed class BodyAttribute : ActionParameterAttribute
    {
        /// <summary>
        /// If true, the parameter is expected to be JSON encoded.
        /// </summary>
        public bool Json { get; set; }

        /// <summary>Creates a new <see cref="BodyAttribute"/>.</summary>
        public BodyAttribute() : base( ActionParameterMethod.Body ) { }
    }
    
    /// <summary>
    /// Attribute that specifies that a controller action parameter comes
    /// from a named capture in the matched URL of the action or controller.
    /// </summary>
    public sealed class UrlAttribute : ActionParameterAttribute
    {
        /// <summary>Creates a new <see cref="UrlAttribute"/>.</summary>
        public UrlAttribute() : base( ActionParameterMethod.Url ) { }
    }

    internal class ControllerActionMap
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private delegate void ControllerAction( Controller controller );

        private class BoundAction : IComparable<BoundAction>
        {
            public readonly UrlMatcher Matcher;
            public readonly bool MatchAllUrl;
            public readonly HttpMethod HttpMethod;
            public readonly ControllerAction Action;
            
            private readonly float _priority;
            private readonly string _actionName;

            public BoundAction( UrlMatcher matcher, bool matchAllUrl, float priority,
                HttpMethod httpMethod, ControllerAction action, string name )
            {
                Matcher = matcher;
                MatchAllUrl = matchAllUrl;
                HttpMethod = httpMethod;
                Action = action;
                
                _priority = priority;
                _actionName = name;
            }

            public int CompareTo( BoundAction other )
            {
                var compared = Matcher.CompareTo( other.Matcher );
                if ( compared != 0 ) return compared;
                return _priority > other._priority ? 1 : _priority < other._priority ? -1 : 0;
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

        /// <remarks>
        /// From http://stackoverflow.com/a/30081250
        /// </remarks>
        private static bool HasImplicitConversion( Type source, Type destination )
        {
            var sourceCode = Type.GetTypeCode( source );
            var destinationCode = Type.GetTypeCode( destination );
            switch ( sourceCode )
            {
                case TypeCode.SByte:
                    switch ( destinationCode )
                    {
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.Byte:
                    switch ( destinationCode )
                    {
                        case TypeCode.Int16:
                        case TypeCode.UInt16:
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.Int16:
                    switch ( destinationCode )
                    {
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.UInt16:
                    switch ( destinationCode )
                    {
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.Int32:
                    switch ( destinationCode )
                    {
                        case TypeCode.Int64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.UInt32:
                    switch ( destinationCode )
                    {
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    switch ( destinationCode )
                    {
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.Char:
                    switch ( destinationCode )
                    {
                        case TypeCode.UInt16:
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    return false;
                case TypeCode.Single:
                    return (destinationCode == TypeCode.Double);
            }
            return false;
        }

        private static bool CanUseResponseWriter( Type paramType, Type returnType, out int separation )
        {
            const int typeParamSeparation = 1;
            const int implicitConversionSeparation = 1;
            const int inheritanceSeparation = 256;

            separation = 0;

            if ( paramType == returnType )
            {
                return true;
            }

            if ( returnType.IsValueType && paramType.IsValueType && HasImplicitConversion( returnType, paramType ) )
            {
                separation += implicitConversionSeparation;
                return true;
            }

            if ( paramType.ContainsGenericParameters && returnType.IsConstructedGenericType && returnType.GetGenericTypeDefinition() == paramType.GetGenericTypeDefinition() )
            {
                try
                {
                    var returnTypeParams = returnType.GetGenericArguments();
                    var paramTypeParams = paramType.GetGenericArguments();

                    for ( var i = 0; i < paramTypeParams.Length; ++i )
                    {
                        if ( !paramTypeParams[i].IsGenericParameter )
                        {
                            if ( paramTypeParams[i] != returnTypeParams[i] ) return false;
                            continue;
                        }

                        paramTypeParams[i] = returnTypeParams[i];
                        separation += typeParamSeparation;
                    }

                    var generic = paramType.GetGenericTypeDefinition().MakeGenericType( paramTypeParams );
                    if ( generic.IsAssignableFrom( returnType ) ) return true;
                }
                catch { /* Jon Skeet said this was okay */ }
            }

            if ( returnType.BaseType == null ) return false;
            if ( !CanUseResponseWriter( paramType, returnType.BaseType, out separation ) ) return false;

            separation += inheritanceSeparation;
            return true;
        }

        private bool TryExtendWriterCache( Type responseType, out MethodInfo action )
        {
            var bestScore = int.MaxValue;
            action = null;

            foreach ( var pair in _writerCache )
            {
                int score;
                if ( !CanUseResponseWriter( pair.Key, responseType, out score ) || score >= bestScore ) continue;

                bestScore = score;
                action = pair.Value;
            }

            return action != null;
        }

        internal MethodInfo GetResponseWriter( Type responseType )
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
            if ( TryExtendWriterCache( responseType, out action ) ) return action;

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
            public static JObject ReadJObject( Controller controller, ActionParameterMethod method, string name, JObject @default )
            {
                var str = GetRawValue( controller, method, name );
                return !string.IsNullOrEmpty( str ) ? JObject.Parse( str ) : @default;
            }

            public static JArray ReadJArray( Controller controller, ActionParameterMethod method, string name, JArray @default )
            {
                var str = GetRawValue( controller, method, name );
                return !string.IsNullOrEmpty( str ) ? JArray.Parse( str ) : @default;
            }

            public static object ReadJson<T>( Controller controller, ActionParameterMethod method, string name, T @default )
            {
                var str = GetRawValue( controller, method, name );
                return  !string.IsNullOrEmpty( str ) ? JsonConvert.DeserializeObject( str, typeof(T) ) : @default;
            }

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
            if ( method == null )
            {
                var bodyAttrib = methodAttrib as BodyAttribute;
                if ( bodyAttrib == null || !bodyAttrib.Json )
                {
                    throw new NotImplementedException();
                }
                
                method = typeof (ParameterParsers).GetMethod( "ReadJson", flags );
                method = method.MakeGenericMethod( param.ParameterType );
            }

            var @default = param.HasDefaultValue ? param.DefaultValue
                : type.IsValueType ? Activator.CreateInstance( type ) : null;
            
            var methodConst = Expression.Constant( paramMethod );
            var nameConst = Expression.Constant( param.Name );
            var defaultConst = Expression.Constant( @default, type );

            Expression call = Expression.Call( method, controllerParam, methodConst, nameConst, defaultConst );

            if ( method.ReturnType != param.ParameterType )
            {
                call = Expression.Convert( call, param.ParameterType );
            }

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

            Expression call = Expression.Call( convertedController, method, queryParsers );

            if ( responseWriter != null )
            {
                var paramType = responseWriter.GetParameters()[0].ParameterType;

                if ( !paramType.IsAssignableFrom( method.ReturnType ) || paramType.IsByRef && method.ReturnType.IsValueType )
                {
                    call = Expression.Convert( call, paramType );
                }

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

        private static bool IsMatchingAllUrl( UrlMatch match, Uri uri )
        {
            return match.EndIndex == uri.AbsolutePath.TrimEnd( '/' ).Length;
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
                if ( bound.MatchAllUrl && !IsMatchingAllUrl( match, request.Url ) ) continue;

                controller.SetMatchedActionUrl( bound.Matcher, match );
                bound.Action( controller );
                return true;
            }

            return false;
        }
    }
}
