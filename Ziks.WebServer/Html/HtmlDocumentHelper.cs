using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Ziks.WebServer.Html
{
    internal interface IEchoDestination
    {
        void Add( HtmlElement htmlElement );
    }

    /// <summary>
    /// Helper static class containing types and methods that aid in HTML document creation.
    /// </summary>
    public static class HtmlDocumentHelper
    {
        [ThreadStatic] private static Stack<IEchoDestination> _sEchoDestinations; 

        internal static void PushEchoDestination( IEchoDestination dest )
        {
            if (_sEchoDestinations == null) _sEchoDestinations = new Stack<IEchoDestination>();

            _sEchoDestinations.Push( dest );
        }

        internal static void PopEchoDestination()
        {
            _sEchoDestinations.Pop();
        }
        
        /// <summary>
        /// When used inside an <see cref="Action"/> added to a <see cref="HtmlElement"/>, appends
        /// the given <paramref name="htmlElements"/> to the parent <see cref="HtmlElement"/>. 
        /// </summary>
        /// <param name="htmlElements"></param>
        public static void Echo( params HtmlElement[] htmlElements )
        {
            var dest = _sEchoDestinations.Peek();

            foreach ( var element in htmlElements )
            {
                dest.Add( element );
            }
        }

        /// <summary>
        /// Returns an <see cref="Action"/> that can be added to an <see cref="HtmlElement"/> that adds an
        /// element for each item in <paramref name="items"/> as described by <paramref name="selector"/>.
        /// </summary>
        /// <param name="items">Collection of items to add <see cref="HtmlElement"/>s for.</param>
        /// <param name="selector">Function that maps a <typeparamref name="T"/> to an <see cref="HtmlElement"/>.</param>
        /// <typeparam name="T">Type of each item in <paramref name="items"/>.</typeparam>
        public static Action Foreach<T>( IEnumerable<T> items, Func<T, HtmlElement> selector )
        {
            return () =>
            {
                foreach ( var item in items ) Echo( selector( item ) );
            };
        }

        /// <summary>
        /// Returns an <see cref="Action"/> that can be added to an <see cref="HtmlElement"/> that runs the
        /// given <paramref name="action"/> on each item in <paramref name="items"/>.
        /// </summary>
        /// <param name="items">Collection of items evaluate <paramref name="action"/> on.</param>
        /// <param name="action">Action to evaluate for each item.</param>
        /// <typeparam name="T">Type of each item in <paramref name="items"/>.</typeparam>
        public static Action Foreach<T>( IEnumerable<T> items, Action<T> action )
        {
            return () =>
            {
                foreach ( var item in items ) action( item );
            };
        }

        /// <summary>
        /// Returns an <see cref="Action"/> that can be added to an <see cref="HtmlElement"/> that will
        /// only append the given <see cref="HtmlElement"/> if <paramref name="condition"/> evaluates to
        /// true.
        /// </summary>
        /// <param name="condition">Condition to evaluate.</param>
        /// <param name="htmlElement">Element to add.</param>
        public static Action If( bool condition, HtmlElement htmlElement )
        {
            return () =>
            {
                if ( condition ) Echo( htmlElement );
            };
        }
        
#pragma warning disable 1591
        // ReSharper disable InconsistentNaming
        public static NamedHtmlElement doctype( string value )
        {
            return new NamedHtmlElement( "doctype", html => null )
            {
                TrailingSlash = false
            };
        }

        public class html : ContainerHtmlElement
        {
            public override string Text
            {
                get { return string.Join( " ", Children.OfType<body>().Select( x => x.Text ) ); }
                set
                {
                    var bodies = Children.OfType<body>().ToArray();
                    if ( bodies.Count() == 1 )
                    {
                        bodies.First().Text = value;
                    }
                    else
                    {
                        foreach ( var body in bodies ) Remove( body );
                        Add( new body {value} );
                    }
                }
            }

            public html( params Expression<AttribFunc>[] attribs ) : base( "html", attribs ) { }
        }
        
        public class head : ContainerHtmlElement
        {
            public head( params Expression<AttribFunc>[] attribs ) : base( "head", attribs ) { }
        }
        
        public class body : ContainerHtmlElement
        {
            public body( params Expression<AttribFunc>[] attribs ) : base( "body", attribs ) { }
        }

        public class script : ContainerHtmlElement
        {
            internal override bool Verbatim => true;
            public script( params Expression<AttribFunc>[] attribs ) : base( "script", attribs ) { }
        }

        public class link : NamedHtmlElement
        {
            public link( params Expression<AttribFunc>[] attribs ) : base( "link", attribs ) { }
        }
        
        public class div : ContainerHtmlElement
        {
            public div( params Expression<AttribFunc>[] attribs ) : base( "div", attribs ) { }
        }
        
        public class title : ContainerHtmlElement
        {
            public title( params Expression<AttribFunc>[] attribs ) : base( "title", attribs ) { }
        }
        
        public class span : ContainerHtmlElement
        {
            public span( params Expression<AttribFunc>[] attribs ) : base( "span", attribs ) { }
        }
        
        public class a : ContainerHtmlElement
        {
            public a( params Expression<AttribFunc>[] attribs ) : base( "a", attribs ) { }
        }
        
        public class h1 : ContainerHtmlElement
        {
            public h1( params Expression<AttribFunc>[] attribs ) : base( "h1", attribs ) { }
        }
        
        public class h2 : ContainerHtmlElement
        {
            public h2( params Expression<AttribFunc>[] attribs ) : base( "h2", attribs ) { }
        }
        
        public class h3 : ContainerHtmlElement
        {
            public h3( params Expression<AttribFunc>[] attribs ) : base( "h3", attribs ) { }
        }
        
        public class h4 : ContainerHtmlElement
        {
            public h4( params Expression<AttribFunc>[] attribs ) : base( "h4", attribs ) { }
        }
        
        public class h5 : ContainerHtmlElement
        {
            public h5( params Expression<AttribFunc>[] attribs ) : base( "h5", attribs ) { }
        }
        
        public class h6 : ContainerHtmlElement
        {
            public h6( params Expression<AttribFunc>[] attribs ) : base( "h6", attribs ) { }
        }
        
        public class p : ContainerHtmlElement
        {
            public p( params Expression<AttribFunc>[] attribs ) : base( "p", attribs ) { }
        }

        public class ul : ContainerHtmlElement
        {
            public ul( params Expression<AttribFunc>[] attribs ) : base( "ul", attribs ) { }
        }

        public class li : ContainerHtmlElement
        {
            public li( params Expression<AttribFunc>[] attribs ) : base( "li", attribs ) { }
        }
        
        public static EntityHtmlElement nbsp => EntityHtmlElement.Nbsp;
        public static NamedHtmlElement br { get; } = new NamedHtmlElement( "br" );

        public class code : ContainerHtmlElement
        {
            internal override bool AllowIndentation => false;
            public code( params Expression<AttribFunc>[] attribs ) : base( "code", attribs ) { }
        }
        
        public class form : ContainerHtmlElement
        {
            public form( params Expression<AttribFunc>[] attribs ) : base( "form", attribs ) { }
        }
        
        public class input : NamedHtmlElement
        {
            public input( params Expression<AttribFunc>[] attribs ) : base( "input", attribs ) { }
        }
        // ReSharper restore InconsistentNaming
#pragma warning restore 1591
    }
}
