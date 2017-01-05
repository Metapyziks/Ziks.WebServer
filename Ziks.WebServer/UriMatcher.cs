using System;
using System.Text.RegularExpressions;

namespace Ziks.WebServer
{
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

        public abstract bool Matches( Uri uri );
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

    public class PrefixMatcher : UriMatcher
    {
        public string Prefix { get; }

        public PrefixMatcher( string prefix )
        {
            Prefix = prefix;
        }

        public override bool Matches( Uri uri )
        {
            return uri.AbsolutePath.StartsWith( Prefix );
        }
    }

    public class RegexMatcher : UriMatcher
    {
        public Regex Regex { get; }

        public RegexMatcher( Regex regex )
        {
            Regex = regex;
        }

        public override bool Matches( Uri uri )
        {
            return Regex.IsMatch( uri.AbsolutePath );
        }
    }
}
