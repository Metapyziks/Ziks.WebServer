using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ziks.WebServer
{
    public struct UrlMatch
    {
        public static readonly UrlMatch Failure = new UrlMatch( 0, -1 );

        public bool Success => Length != -1;
        public int EndIndex => Index + Length;

        public readonly int Index;
        public readonly int Length;

        public UrlMatch( int index, int length )
        {
            Index = index;
            Length = length;
        }
    }

    public sealed class UrlSegmentCollection
    {
        public static UrlSegmentCollection Empty { get; } = new UrlSegmentCollection();

        [ThreadStatic]
        private static List<UrlMatch> _sMatchBuffer;

        private readonly string _absolutePath;
        private readonly UrlMatch[] _segments;
        private string[] _names;

        public UrlSegmentCollection() { }

        public UrlSegmentCollection( Uri uri )
        {
            _absolutePath = uri.AbsolutePath;
            var nextIndex = _absolutePath.IndexOf( '/' );

            if ( _sMatchBuffer == null ) _sMatchBuffer = new List<UrlMatch>();
            else _sMatchBuffer.Clear();
            
            while ( nextIndex < _absolutePath.Length )
            {
                var prevIndex = nextIndex + 1;
                nextIndex = _absolutePath.IndexOf( '/', prevIndex );
                if ( nextIndex == -1 ) nextIndex = _absolutePath.Length;
                if ( nextIndex == prevIndex ) break;

                _sMatchBuffer.Add( new UrlMatch( prevIndex, nextIndex - prevIndex ) );
            }

            _segments = new UrlMatch[_sMatchBuffer.Count];
            for ( var i = 0; i < _segments.Length; ++i )
            {
                _segments[i] = _sMatchBuffer[i];
            }
        }

        public int Count => _segments == null ? 0 : _segments.Length;

        public string this[ int index ]
        {
            get
            {
                var match = _segments[index];
                return _absolutePath.Substring( match.Index, match.Length );
            }
        }

        public string this[ string name ]
        {
            get
            {
                if ( name == null ) throw new ArgumentNullException();
                if ( _names == null ) throw new ArgumentException();

                for ( var i = 0; i < _names.Length; ++i )
                {
                    if ( _names[i] == name ) return this[i];
                }

                throw new ArgumentException();
            }
        }

        public int GetFirstIndex( UrlMatch match )
        {
            if ( Count == 0 ) return -1;
            for ( var i = 0; i < _segments.Length; ++i )
            {
                if ( _segments[i].Index < match.Index ) continue;
                if ( _segments[i].EndIndex <= match.EndIndex ) return i;
                return -1;
            }

            return -1;
        }

        public int GetLastIndex( UrlMatch match )
        {
            if ( Count == 0 ) return -1;
            for ( var i = _segments.Length - 1; i >= 0; --i )
            {
                if ( _segments[i].EndIndex > match.EndIndex ) continue;
                if ( _segments[i].Index >= match.Index ) return i;
                return -1;
            }

            return -1;
        }

        internal void SetIndexName( int index, string name )
        {
            if ( index < 0 || index >= Count ) throw new IndexOutOfRangeException();
            if ( _names == null ) _names = new string[_segments.Length];

            _names[index] = name;
        }
    }

    public abstract class UrlMatcher : IComparable<UrlMatcher>
    {
        public static UrlMatcher Parse( string prefix )
        {
            if ( SimplePrefixMatcher.IsValidPrefix( prefix ) ) return new SimplePrefixMatcher( prefix );
            if ( CapturingPrefixMatcher.IsValidPrefix( prefix ) ) return new CapturingPrefixMatcher( prefix );
            throw new Exception( "Prefix string is badly formed." );
        }

        public static implicit operator UrlMatcher( string prefix )
        {
            return Parse( prefix );
        }

        public abstract int SegmentCount { get; }

        public bool IsMatch( Uri uri, int startIndex = 0 ) => Match( uri, startIndex ).Success;

        public abstract UrlMatch Match( Uri uri, int startIndex = 0 );

        public UrlSegmentCollection GetSegments( Uri uri, int startIndex = 0 )
        {
            var segments = new UrlSegmentCollection( uri );
            FurnishMatchedSegments( uri, segments, ref startIndex );
            return segments;
        }

        internal virtual void FurnishMatchedSegments( Uri uri, UrlSegmentCollection collection, ref int startIndex )
        {
            startIndex = Match( uri, startIndex ).EndIndex;
        }

        public int CompareTo( UrlMatcher other )
        {
            return SegmentCount - other.SegmentCount;
        }
    }

    public class ConcatenatedPrefixMatcher : UrlMatcher
    {
        private readonly UrlMatcher _first;
        private readonly UrlMatcher _second;

        public ConcatenatedPrefixMatcher( UrlMatcher first, UrlMatcher second )
        {
            _first = first;
            _second = second;
        }

        public override int SegmentCount => _first.SegmentCount + _second.SegmentCount;

        public override UrlMatch Match( Uri uri, int startIndex = 0 )
        {
            var first = _first.Match( uri, startIndex );
            if ( !first.Success ) return UrlMatch.Failure;

            var second = _second.Match( uri, first.EndIndex );
            if ( !second.Success ) return UrlMatch.Failure;

            return new UrlMatch( first.Index, second.EndIndex - first.Index );
        }

        internal override void FurnishMatchedSegments( Uri uri, UrlSegmentCollection collection, ref int startIndex )
        {
            _first.FurnishMatchedSegments( uri, collection, ref startIndex );
            _second.FurnishMatchedSegments( uri, collection, ref startIndex );
        }

        public override string ToString()
        {
            var first = _first.ToString();
            if ( !first.EndsWith( "/" ) ) return first + _second;
            return first.Substring( 0, first.Length - 1 ) + _second;
        }
    }

    [AttributeUsage( AttributeTargets.Class
        | AttributeTargets.Property
        | AttributeTargets.Method,
        AllowMultiple = true)]
    public class PrefixAttribute : Attribute
    {
        public string Value { get; set; }
        public float Priority { get; set; }

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

    public abstract class PrefixMatcher : UrlMatcher
    {
        public string OriginalPrefix { get; }
        public string[] RawSegments { get; }

        public override int SegmentCount => RawSegments.Length;

        protected PrefixMatcher( string prefix, Regex segmentRegex )
        {
            OriginalPrefix = prefix;
            
            var match = segmentRegex.Match( prefix );
            if ( !match.Success ) throw new ArgumentException();

            var segments = match.Groups["segment"];

            RawSegments = new string[segments.Captures.Count];

            var index = 0;
            foreach ( var group in segments.Captures.Cast<Capture>() )
            {
                RawSegments[index++] = group.Value;
            }
        }

        protected virtual bool MatchSegment( int segmentIndex, string path, ref int startIndex )
        {
            var segment = segmentIndex == -1 ? "" : RawSegments[segmentIndex];

            if ( path.Length < startIndex + 1 + segment.Length ) return false;
            if ( path[startIndex] != '/' ) return false;
            if ( segment.Length > 0 ) ++startIndex;

            for ( var i = 0; i < segment.Length; ++i )
            {
                if ( path[startIndex] != segment[i] ) return false;
                ++startIndex;
            }

            return true;
        }

        public override UrlMatch Match( Uri uri, int startIndex = 0 )
        {
            var absolute = uri.AbsolutePath;
            var matchedIndex = startIndex;

            if ( RawSegments.Length == 0 )
            {
                if ( !MatchSegment( -1, absolute, ref matchedIndex ) ) return UrlMatch.Failure;
            }
            else
            {
                for ( var i = 0; i < RawSegments.Length; ++i )
                {
                    if ( !MatchSegment( i, absolute, ref matchedIndex ) ) return UrlMatch.Failure;
                }
            }

            if ( matchedIndex < absolute.Length && absolute[matchedIndex] != '/') return UrlMatch.Failure;

            return new UrlMatch( startIndex, matchedIndex - startIndex );
        }

        public override string ToString()
        {
            return OriginalPrefix;
        }
    }

    public class SimplePrefixMatcher : PrefixMatcher
    {
        private static readonly Regex _sSimplePrefixRegex
            = new Regex( @"^((/(?<segment>[~a-z0-9_.-]+))+|/)?$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase );

        public static bool IsValidPrefix( string str )
        {
            return _sSimplePrefixRegex.IsMatch( str );
        }

        public SimplePrefixMatcher( string prefix )
            : base( prefix, _sSimplePrefixRegex ) { }
    }

    public class CapturingPrefixMatcher : PrefixMatcher
    {
        private static readonly Regex _sCapturingPrefixRegex
            = new Regex( @"^((/(?<segment>[~a-z0-9_.-]+|\{[a-z_][a-z0-9_]*\}))+|/)?$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase );

        private static readonly Regex _sCaptureRegex
            = new Regex( @"^\{(?<name>[a-z_][a-z0-9_]*)\}$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase );

        public static bool IsValidPrefix( string str )
        {
            return _sCapturingPrefixRegex.IsMatch( str );
        }

        private readonly string[] _captureNames;

        public CapturingPrefixMatcher( string prefix )
            : base( prefix, _sCapturingPrefixRegex )
        {
            _captureNames = new string[RawSegments.Length];

            for ( var i = 0; i < RawSegments.Length; ++i )
            {
                var match = _sCaptureRegex.Match( RawSegments[i] );
                if ( !match.Success ) continue;

                _captureNames[i] = match.Groups["name"].Value;
            }
        }

        protected override bool MatchSegment( int segmentIndex, string path, ref int startIndex )
        {
            if ( _captureNames[segmentIndex] == null )
            {
                return base.MatchSegment( segmentIndex, path, ref startIndex );
            }

            if ( path.Length < startIndex + 2 ) return false;
            if ( path[startIndex] != '/' ) return false;

            var endIndex = path.IndexOf( '/', startIndex + 1 );
            if ( endIndex == -1 ) endIndex = path.Length;

            startIndex = endIndex;
            return true;
        }

        internal override void FurnishMatchedSegments( Uri uri, UrlSegmentCollection collection, ref int startIndex )
        {
            var match = Match( uri, startIndex );
            var first = collection.GetFirstIndex( match );
            var last = collection.GetLastIndex( match );

            for ( var i = first; i <= last; ++i )
            {
                if ( _captureNames[i - first] == null ) continue;
                collection.SetIndexName( i, _captureNames[i - first] );
            }

            startIndex = match.EndIndex;
        }
    }
}
