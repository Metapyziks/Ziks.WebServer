using System;
using System.Reflection;

namespace Ziks.WebServer
{
    /// <summary>
    /// Annotation used for classes extending <see cref="Controller"/> so they
    /// can be automatically mapped to URLs prefixed with the given value when
    /// discovered with a call to <see cref="ControllerMap.Add(Assembly)"/>.
    /// </summary>
    [AttributeUsage( AttributeTargets.Class
        | AttributeTargets.Property
        | AttributeTargets.Method,
        AllowMultiple = true)]
    public class PrefixAttribute : Attribute
    {
        /// <summary>
        /// URL prefix to be matched.
        /// </summary>
        /// <example>
        /// Match URLs starting with '/foo' and capture the name of the second
        /// segment of the urL:
        /// <code>/foo/{bar}</code>
        /// </example>
        public string Value { get; set; }

        /// <summary>
        /// Optional file extension that should be matched.
        /// </summary>
        public string Extension { get; set; }

        /// <summary>
        /// Optional priority to use when sorting <see cref="UrlMatcher"/>s.
        /// </summary>
        public float Priority { get; set; } = float.NaN;

        /// <summary>
        /// Creates a new <see cref="PrefixAttribute"/> using the given prefix.
        /// </summary>
        /// <param name="value">URL prefix to be matched.</param>
        public PrefixAttribute( string value )
        {
            Value = value;
        }
    }
    
#pragma warning disable 1574
    /// <summary>
    /// Attribute used for specifying a default priority to use when sorting
    /// <see cref="Controller"/> types. Will be used when a priority isn't
    /// explicitly specified by a <see cref="PrefixAttribute"/> or when calling
    /// <see cref="ControllerMap.Add(UrlMatcher, float)"/>.
    /// </summary>
#pragma warning restore 1574
    [AttributeUsage( AttributeTargets.Class )]
    public class DefaultPriorityAttribute : Attribute
    {
        /// <summary>
        /// Priority value.
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// Creates a new <see cref="DefaultPriorityAttribute"/> with the given value.
        /// </summary>
        /// <param name="value">Priority value.</param>
        public DefaultPriorityAttribute( float value )
        {
            Value = value;
        }
    }

    /// <summary>
    /// Base class for attributes used to specify methods to use as controller actions
    /// that handle HTTP requests.
    /// </summary>
    public abstract class ControllerActionAttribute : PrefixAttribute
    {
        /// <summary>
        /// If true, the given prefix must match the whole of a request's absolute
        /// path rather than just the start of it.
        /// </summary>
        public bool MatchAllUrl { get; set; } = true;

        /// <summary>
        /// Creates a new <see cref="ControllerActionAttribute"/> with the given optional
        /// URL prefix to match.
        /// </summary>
        /// <param name="prefix">URL prefix to match.</param>
        protected ControllerActionAttribute( string prefix = "/" )
            : base( prefix )
        {
            Priority = 0f;
        }

        internal abstract ActionParameterMethod DefaultParameterMethod { get; }
    }

    /// <summary>
    /// Attribute to mark controller action methods that handle GET requests.
    /// </summary>
    public class GetAttribute : ControllerActionAttribute
    {
        internal override ActionParameterMethod DefaultParameterMethod => ActionParameterMethod.Query;
        
        /// <summary>
        /// Creates a new <see cref="GetAttribute"/> with the given optional
        /// URL prefix to match.
        /// </summary>
        /// <param name="prefix">URL prefix to match.</param>
        public GetAttribute( string prefix = "/" )
            : base ( prefix ) { }
    }
    
    /// <summary>
    /// Attribute to mark controller action methods that handle POST requests.
    /// </summary>
    public class PostAttribute : ControllerActionAttribute
    {
        /// <summary>
        /// If true, values submitted as a form in the body of a handled request are
        /// automatically fed into the parameters of the method this attribute annotates.
        /// </summary>
        public bool Form { get; set; } = true;
        
        internal override ActionParameterMethod DefaultParameterMethod => Form ? ActionParameterMethod.Form : ActionParameterMethod.Query;
        
        /// <summary>
        /// Creates a new <see cref="PostAttribute"/> with the given optional
        /// URL prefix to match.
        /// </summary>
        /// <param name="prefix">URL prefix to match.</param>
        public PostAttribute( string prefix = "/" )
            : base ( prefix ) { }
    }
}
