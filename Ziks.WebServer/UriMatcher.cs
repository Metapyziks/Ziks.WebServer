using System;
using System.Text.RegularExpressions;

namespace Ziks.WebServer
{
    public struct UriMatch
    {
        public static readonly UriMatch Failure = new UriMatch( 0, 0 );

        public bool Success => Length > 0;
        public int EndIndex => Index + Length;

        public readonly int Index;
        public readonly int Length;

        public UriMatch( int index, int length )
        {
            Index = index;
            Length = length;
        }
    }

    public abstract class UriMatcher
    {
        public static implicit operator UriMatcher( string prefix )
        {
            return new PrefixMatcher( prefix );
        }
        
        public static implicit operator UriMatcher( Regex regex )
        {
            return new RegexMatcher( regex );
        }

        public abstract UriMatch Match( Uri uri, int startIndex = 0 );
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method)]
    public class UriPrefixAttribute : Attribute
    {
        public string Value { get; set; }

        public UriPrefixAttribute( string value )
        {
            Value = value;
        }
    }

    public abstract class ControllerActionAttribute : UriPrefixAttribute
    {
        protected ControllerActionAttribute( string prefix = "/" )
            : base ( prefix ) { }
    }

    public class GetActionAttribute : ControllerActionAttribute
    {
        public GetActionAttribute( string prefix = "/" )
            : base ( prefix ) { }
    }

    public class PostActionAttribute : ControllerActionAttribute
    {
        public PostActionAttribute( string prefix = "/" )
            : base ( prefix ) { }
    }

    public class PrefixMatcher : UriMatcher
    {
        public string Prefix { get; }

        public PrefixMatcher( string prefix )
        {
            Prefix = prefix;
        }

        public override UriMatch Match( Uri uri, int startIndex = 0 )
        {
            var absolute = uri.AbsolutePath;
            if ( Prefix.Length > absolute.Length + startIndex ) return UriMatch.Failure;

            for ( var i = 0; i < Prefix.Length; ++i )
            {
                if ( absolute[i + startIndex] != Prefix[i] ) return UriMatch.Failure;
            }

            return new UriMatch( startIndex, Prefix.Length );
        }
    }

    public class RegexMatcher : UriMatcher
    {
        public Regex Regex { get; }

        public RegexMatcher( Regex regex )
        {
            Regex = regex;
        }

        public override UriMatch Match( Uri uri, int startIndex = 0 )
        {
            var match = Regex.Match( uri.AbsolutePath, startIndex );
            return match.Success && match.Index == startIndex
                ? new UriMatch( startIndex, match.Length )
                : UriMatch.Failure;
        }
    }
}
