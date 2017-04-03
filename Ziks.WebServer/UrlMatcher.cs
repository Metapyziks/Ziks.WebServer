using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ziks.WebServer
{
    internal struct UrlMatch
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

    /// <summary>
    /// Contains an indexable sequence of URL path segments from a parsed
    /// URL. Can also be indexed by capture names in the case of a capturing
    /// URL prefix matcher being used (see <see cref="UrlMatcher.Parse"/>).
    /// </summary>
    public sealed class UrlSegmentCollection
    {
        /// <summary>
        /// An empty <see cref="UrlSegmentCollection"/>.
        /// </summary>
        public static UrlSegmentCollection Empty { get; } = new UrlSegmentCollection();

        [ThreadStatic]
        private static List<UrlMatch> _sMatchBuffer;

        private readonly string _absolutePath;
        private readonly UrlMatch[] _segments;
        private string[] _names;
        private int[] _prefixes;
        private int[] _postfixes;
        
        /// <summary>
        /// Creates a new empty <see cref="UrlSegmentCollection"/>.
        /// </summary>
        public UrlSegmentCollection() { }

        /// <summary>
        /// Creates a new <see cref="UrlSegmentCollection"/> parsed from the given URL.
        /// </summary>
        /// <param name="uri">URL to parse.</param>
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

        /// <summary>
        /// Number of segments this collection contains.
        /// </summary>
        public int Count => _segments == null ? 0 : _segments.Length;

        /// <summary>
        /// Gets a segment from this collection by numerical index.
        /// Index must be at least 0, and less than <see cref="Count"/>.
        /// </summary>
        /// <param name="index">Segment index to retrieve.</param>
        public string this[ int index ]
        {
            get
            {
                var match = _segments[index];
                return _absolutePath.Substring( match.Index, match.Length );
            }
        }

        /// <summary>
        /// Gets a segment from this collection by captured segment name. Only
        /// applicable for names specified by a capturing URL matcher.
        /// </summary>
        /// <param name="name">Captured segment name.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentException">If no captures exist with the given name.</exception>
        public string this[ string name ]
        {
            get
            {
                if ( name == null ) throw new ArgumentNullException();
                if ( _names == null ) throw new ArgumentException();

                for ( var i = 0; i < _names.Length; ++i )
                {
                    if ( _names[i] == name )
                    {
                        var value = this[i];

                        if ( _prefixes != null ) value = value.Substring( _prefixes[i] );
                        if ( _postfixes != null ) value = value.Substring( 0, value.Length - _postfixes[i] );

                        return value;
                    }
                }

                throw new ArgumentException();
            }
        }

        internal int GetFirstIndex( UrlMatch match )
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

        internal int GetLastIndex( UrlMatch match )
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

        internal void SetIndexName( int index, string name, int prefixLength = 0, int postfixLength = 0 )
        {
            if ( index < 0 || index >= Count ) throw new IndexOutOfRangeException();
            if ( _names == null ) _names = new string[_segments.Length];

            _names[index] = name;

            if ( prefixLength != 0 )
            {
                if ( _prefixes == null ) _prefixes = new int[_segments.Length];
                _prefixes[index] = prefixLength;
            }

            if ( postfixLength != 0 )
            {
                if (_postfixes == null) _postfixes = new int[_segments.Length];
                _postfixes[index] = postfixLength;
            }
        }
    }

    /// <summary>
    /// Base class for types that test if a given <see cref="Uri"/> matches some condition. Used
    /// for routing HTTP requests to action methods in <see cref="Controller"/> instances.
    /// </summary>
    public abstract class UrlMatcher : IComparable<UrlMatcher>
    {
        /// <summary>
        /// Parses a URL prefix to a URL prefix matcher. Path segments can be either plain strings or
        /// named capture groups (see examples).
        /// </summary>
        /// <example>
        /// Simple prefix matching URLs starting with "/foo/bar":
        /// 
        /// <code>var matcher = UrlMatcher.Parse("/foo/bar");</code>
        /// </example>
        /// <example>
        /// Simple prefix matching all URLs:
        /// 
        /// <code>var matcher = UrlMatcher.Parse("/");</code>
        /// </example>
        /// <example>
        /// Capturing prefix matching URLs starting with "/foo/bar", where the third
        /// URL segment matched is assigned the name "example":
        /// 
        /// <code>var matcher = UrlMatcher.Parse("/foo/bar/{example}");</code>
        /// </example>
        /// <param name="prefix">URL prefix to match. See examples.</param>
        /// <param name="extension">Optional file extension to match.</param>
        /// <exception cref="Exception">Thrown if the prefix string is not well formed.</exception>
        public static UrlMatcher Parse( string prefix, string extension = null )
        {
            if ( SimplePrefixMatcher.IsValidPrefix( prefix ) ) return new SimplePrefixMatcher( prefix, extension );
            if ( CapturingPrefixMatcher.IsValidPrefix( prefix ) ) return new CapturingPrefixMatcher( prefix, extension );
            throw new Exception( "Prefix string is badly formed." );
        }

        /// <summary>
        /// Implicit conversion from a <see cref="string"/> to <see cref="UrlMatcher"/>. Implemented
        /// with a call to <see cref="Parse"/>.
        /// </summary>
        /// <param name="prefix">Prefix string to parse and convert.</param>
        public static implicit operator UrlMatcher( string prefix )
        {
            return Parse( prefix );
        }

        /// <summary>
        /// The number of segments that this matcher attempts to match, used for sorting matchers.
        /// </summary>
        public abstract int SegmentCount { get; }

        /// <summary>
        /// Used to sort <see cref="UrlMatcher"/>s that have the same <see cref="SegmentCount"/>.
        /// </summary>
        protected virtual float Priority => 0f;

        /// <summary>
        /// Tests the given URL to see if its <see cref="Uri.AbsolutePath"/> matches the condition of this
        /// matcher, with an optional offset to start matching from.
        /// </summary>
        /// <param name="uri">URL to attempt to match.</param>
        /// <param name="startIndex">Index to start matching from.</param>
        /// <returns>True if the URL matches the condition of this matcher.</returns>
        public bool IsMatch( Uri uri, int startIndex = 0 ) => Match( uri, startIndex ).Success;

        internal abstract UrlMatch Match( Uri uri, int startIndex = 0 );

        /// <summary>
        /// Parses the given URL into segments, and if this matcher has any named captures specified
        /// it adds them to the collection to be indexed.
        /// </summary>
        /// <param name="uri">URL to parse and match.</param>
        /// <param name="startIndex">Index to start matching from.</param>
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

        /// <summary>
        /// Compares two <see cref="UrlMatcher"/>s based on their <see cref="SegmentCount"/>.
        /// </summary>
        /// <param name="other">Other <see cref="UrlMatcher"/> to compare to.</param>
        public int CompareTo( UrlMatcher other )
        {
            return SegmentCount > other.SegmentCount ? 1
                : SegmentCount < other.SegmentCount ? -1
                : Priority > other.Priority ? 1
                : Priority < other.Priority ? -1 : 0;
        }
    }

    internal class ConcatenatedPrefixMatcher : UrlMatcher
    {
        private readonly UrlMatcher _first;
        private readonly UrlMatcher _second;

        public ConcatenatedPrefixMatcher( UrlMatcher first, UrlMatcher second )
        {
            _first = first;
            _second = second;
        }

        public override int SegmentCount => _first.SegmentCount + _second.SegmentCount;

        internal override UrlMatch Match( Uri uri, int startIndex = 0 )
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

    internal abstract class PrefixMatcher : UrlMatcher
    {
        public string OriginalPrefix { get; }
        public string Extension { get; }
        public string[] RawSegments { get; }

        public override int SegmentCount => RawSegments.Length + (string.IsNullOrEmpty( Extension ) ? 0 : 1);

        protected PrefixMatcher( string prefix, string extension, Regex segmentRegex )
        {
            OriginalPrefix = prefix;
            Extension = extension;
            
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

        internal override UrlMatch Match( Uri uri, int startIndex = 0 )
        {
            var absolute = uri.AbsolutePath;
            var matchedIndex = startIndex;

            if ( !string.IsNullOrEmpty( Extension ) )
            {
                if ( !absolute.EndsWith( Extension, StringComparison.InvariantCultureIgnoreCase ) )
                {
                    return UrlMatch.Failure;
                }
            }

            if ( RawSegments.Length == 0 )
            {
                if ( startIndex == absolute.Length ) return new UrlMatch( startIndex, 0 );
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
            return OriginalPrefix + Extension;
        }
    }

    internal class SimplePrefixMatcher : PrefixMatcher
    {
        private static readonly Regex _sSimplePrefixRegex
            = new Regex( @"^((/(?<segment>[~a-z0-9_.-]+))+|/)?$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase );

        public static bool IsValidPrefix( string str )
        {
            return _sSimplePrefixRegex.IsMatch( str );
        }

        public SimplePrefixMatcher( string prefix, string extension )
            : base( prefix, extension, _sSimplePrefixRegex ) { }
    }

    internal class CapturingPrefixMatcher : PrefixMatcher
    {
        private static readonly Regex _sCapturingPrefixRegex
            = new Regex(@"^((/(?<segment>[~a-z0-9_.-]+|[~a-z0-9_.-]*\{[a-z_][a-z0-9_]*\}[~a-z0-9_.-]*))+|/)?$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase );

        private static readonly Regex _sCaptureRegex
            = new Regex(@"^[~a-z0-9_.-]*\{(?<name>[a-z_][a-z0-9_]*)\}[~a-z0-9_.-]*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase );

        public static bool IsValidPrefix( string str )
        {
            return _sCapturingPrefixRegex.IsMatch( str );
        }

        private class CaptureSegment
        {
            public string Name;
            public string Prefix;
            public string Postfix;
        }

        private readonly CaptureSegment[] _captures;

        protected override float Priority => -1f;

        public CapturingPrefixMatcher( string prefix, string extension )
            : base( prefix, extension, _sCapturingPrefixRegex )
        {
            _captures = new CaptureSegment[RawSegments.Length];

            for ( var i = 0; i < RawSegments.Length; ++i )
            {
                var match = _sCaptureRegex.Match( RawSegments[i] );
                if ( !match.Success ) continue;

                var group = match.Groups["name"];

                _captures[i] = new CaptureSegment
                {
                    Name = group.Value,
                    Prefix = RawSegments[i].Substring( 0, group.Index - 1 ),
                    Postfix = RawSegments[i].Substring( group.Index + group.Length + 1 )
                };
            }
        }

        protected override bool MatchSegment( int segmentIndex, string path, ref int startIndex )
        {
            if (_captures[segmentIndex] == null )
            {
                return base.MatchSegment( segmentIndex, path, ref startIndex );
            }

            var capture = _captures[segmentIndex];

            if ( path.Length < startIndex + 2 ) return false;
            if ( path[startIndex] != '/' ) return false;

            var endIndex = path.IndexOf( '/', ++startIndex );
            if ( endIndex == -1 ) endIndex = path.Length;

            if ( endIndex - startIndex <= capture.Prefix.Length + capture.Postfix.Length ) return false;

            for ( var i = 0; i < capture.Prefix.Length; ++i )
            {
                if ( path[startIndex++] != capture.Prefix[i] ) return false;
            }

            startIndex = endIndex - capture.Postfix.Length;

            for (var i = 0; i < capture.Postfix.Length; ++i)
            {
                if (path[startIndex++] != capture.Postfix[i]) return false;
            }

            return true;
        }

        internal override void FurnishMatchedSegments( Uri uri, UrlSegmentCollection collection, ref int startIndex )
        {
            var match = Match( uri, startIndex );
            var first = collection.GetFirstIndex( match );
            var last = collection.GetLastIndex( match );

            for ( var i = first; i <= last; ++i )
            {
                var capture = _captures[i - first];
                if ( capture == null ) continue;
                collection.SetIndexName( i, capture.Name, capture.Prefix.Length, capture.Postfix.Length );
            }

            startIndex = match.EndIndex;
        }
    }
}
