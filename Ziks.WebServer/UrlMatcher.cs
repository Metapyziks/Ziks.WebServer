using System;
using System.Text.RegularExpressions;

namespace Ziks.WebServer
{
    public struct UrlMatch
    {
        public static readonly UrlMatch Failure = new UrlMatch( 0, 0 );

        public bool Success => Length > 0;
        public int EndIndex => Index + Length;

        public readonly int Index;
        public readonly int Length;

        public UrlMatch( int index, int length )
        {
            Index = index;
            Length = length;
        }
    }

    public abstract class UrlMatcher
    {
        public static implicit operator UrlMatcher( string prefix )
        {
            return new PrefixMatcher( prefix );
        }
        
        public static implicit operator UrlMatcher( Regex regex )
        {
            return new RegexMatcher( regex );
        }

        public abstract UrlMatch Match( Uri uri, int startIndex = 0 );
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method)]
    public class PrefixAttribute : Attribute
    {
        public string Value { get; set; }

        public PrefixAttribute( string value )
        {
            Value = value;
        }
    }

    public abstract class ControllerActionAttribute : PrefixAttribute
    {
        public bool MatchAllUrl { get; set; } = true;

        protected ControllerActionAttribute( string prefix = "/" )
            : base ( prefix ) { }

        public abstract ActionParameterMethod DefaultParameterMethod { get; }
    }

    public class GetAttribute : ControllerActionAttribute
    {
        public override ActionParameterMethod DefaultParameterMethod => ActionParameterMethod.Query;

        public GetAttribute( string prefix = "/" )
            : base ( prefix ) { }
    }

    public class PostAttribute : ControllerActionAttribute
    {
        public bool Form { get; set; } = true;
        
        public override ActionParameterMethod DefaultParameterMethod => Form ? ActionParameterMethod.Form : ActionParameterMethod.Query;

        public PostAttribute( string prefix = "/" )
            : base ( prefix ) { }
    }

    public class PrefixMatcher : UrlMatcher
    {
        public string Prefix { get; }

        public PrefixMatcher( string prefix )
        {
            Prefix = prefix;
        }

        public override UrlMatch Match( Uri uri, int startIndex = 0 )
        {
            var absolute = uri.AbsolutePath;
            if ( Prefix.Length > absolute.Length - startIndex ) return UrlMatch.Failure;

            for ( var i = 0; i < Prefix.Length; ++i )
            {
                if ( absolute[i + startIndex] != Prefix[i] ) return UrlMatch.Failure;
            }

            return new UrlMatch( startIndex, Prefix.Length );
        }
    }

    public class RegexMatcher : UrlMatcher
    {
        public Regex Regex { get; }

        public RegexMatcher( Regex regex )
        {
            Regex = regex;
        }

        public override UrlMatch Match( Uri uri, int startIndex = 0 )
        {
            var match = Regex.Match( uri.AbsolutePath, startIndex );
            return match.Success && match.Index == startIndex
                ? new UrlMatch( startIndex, match.Length )
                : UrlMatch.Failure;
        }
    }
}
