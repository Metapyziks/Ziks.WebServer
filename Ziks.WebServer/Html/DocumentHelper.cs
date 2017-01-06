﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Ziks.WebServer.Html
{
    public interface IEchoDestination
    {
        void Add( Element element );
    }

    public static class DocumentHelper
    {
        [ThreadStatic] private static Stack<IEchoDestination> _sEchoDestinations; 

        public static void PushEchoDestination( IEchoDestination dest )
        {
            if (_sEchoDestinations == null) _sEchoDestinations = new Stack<IEchoDestination>();

            _sEchoDestinations.Push( dest );
        }

        public static void PopEchoDestination()
        {
            _sEchoDestinations.Pop();
        }
        
        // ReSharper disable InconsistentNaming
        public static void Echo( params Element[] elements )
        {
            var dest = _sEchoDestinations.Peek();

            foreach ( var element in elements )
            {
                dest.Add( element );
            }
        }

        public static Action Foreach<T>( IEnumerable<T> items, Func<T, Element> selector )
        {
            return () =>
            {
                foreach ( var item in items ) Echo( selector( item ) );
            };
        }

        public static Action Foreach<T>( IEnumerable<T> items, Action<T> action )
        {
            return () =>
            {
                foreach ( var item in items ) action( item );
            };
        }

        public static Element If( bool condition, Element element )
        {
            return condition ? element : null;
        }

        public static NamedElement doctype( string value )
        {
            return new NamedElement( "doctype", html => null )
            {
                TrailingSlash = false
            };
        }

        public class html : ContainerElement
        {
            public html( params Expression<AttribFunc>[] attribs ) : base( "html", attribs ) { }
        }
        
        public class head : ContainerElement
        {
            public head( params Expression<AttribFunc>[] attribs ) : base( "head", attribs ) { }
        }
        
        public class body : ContainerElement
        {
            public body( params Expression<AttribFunc>[] attribs ) : base( "body", attribs ) { }
        }
        
        public class div : ContainerElement
        {
            public div( params Expression<AttribFunc>[] attribs ) : base( "div", attribs ) { }
        }
        
        public class title : ContainerElement
        {
            public title( params Expression<AttribFunc>[] attribs ) : base( "title", attribs ) { }
        }
        
        public class span : ContainerElement
        {
            public span( params Expression<AttribFunc>[] attribs ) : base( "span", attribs ) { }
        }
        
        public class a : ContainerElement
        {
            public a( params Expression<AttribFunc>[] attribs ) : base( "a", attribs ) { }
        }
        
        public class p : ContainerElement
        {
            public p( params Expression<AttribFunc>[] attribs ) : base( "p", attribs ) { }
        }

        public class ul : ContainerElement
        {
            public ul( params Expression<AttribFunc>[] attribs ) : base( "ul", attribs ) { }
        }

        public class li : ContainerElement
        {
            public li( params Expression<AttribFunc>[] attribs ) : base( "li", attribs ) { }
        }

        public static NamedElement br { get; } = new NamedElement( "br" );

        public class code : ContainerElement
        {
            public code( params Expression<AttribFunc>[] attribs ) : base( "code", attribs ) { }
        }
        
        public class form : ContainerElement
        {
            public form( params Expression<AttribFunc>[] attribs ) : base( "form", attribs ) { }
        }
        
        public class input : NamedElement
        {
            public input( params Expression<AttribFunc>[] attribs ) : base( "input", attribs ) { }
        }
        // ReSharper restore InconsistentNaming
    }
}