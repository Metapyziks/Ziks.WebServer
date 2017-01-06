using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Web;

namespace Ziks.WebServer.Html
{
    public delegate object AttribFunc( object name );
    public delegate Element AppendFunc( params Element[] elements );

    public class Attribute : IHtmlSerializable
    {
        public static implicit operator Attribute( Expression<AttribFunc> expression )
        {
            return new Attribute( expression.Parameters[0].Name, expression.Compile()(null) );
        }

        public string Name { get; set; }
        public object Value { get; set; }

        public Attribute( string name, object value )
        {
            Name = name;
            Value = value;
        }

        public override string ToString()
        {
            return $"{Name}=\"{HttpUtility.HtmlAttributeEncode( Value.ToString() )}\"";
        }

        public void Serialize( IHtmlSerializer serializer )
        {
            serializer.Write( ToString() );
        }
    }

    public abstract class Element : IHtmlSerializable
    {
        internal virtual bool SuggestNewlineWhenSerialized => false;

        public static implicit operator Element( string value )
        {
            return new StringElement( value );
        }

        public static implicit operator string( Element element )
        {
            return element.ToString();
        }

        public abstract void Serialize( IHtmlSerializer serializer );
    }

    public class EntityElement : Element
    {
        public static EntityElement Nbsp { get; } = new EntityElement( "nbsp" );

        public string Value { get; set; }

        public EntityElement( string value )
        {
            Value = value;
        }

        public EntityElement( int code )
        {
            Value = $"#{code}";
        }

        public override string ToString()
        {
            return $"&{Value};";
        }

        public override void Serialize( IHtmlSerializer serializer )
        {
            serializer.Write( ToString() );
        }
    }

    public class StringElement : Element
    {
        internal override bool SuggestNewlineWhenSerialized => Value.Contains( '\n' );

        public string Value { get; set; }

        public StringElement( string value )
        {
            Value = value;
        }

        public override string ToString()
        {
            return HttpUtility.HtmlEncode( Value );
        }

        public override void Serialize( IHtmlSerializer serializer )
        {
            serializer.Write( ToString() );
        }
    }

    public class NamedElement : Element, IEnumerable
    {
        private readonly List<Attribute> _attributes = new List<Attribute>();

        public IEnumerable<Attribute> Attributes => _attributes;

        internal override bool SuggestNewlineWhenSerialized => Name == "br";

        public string AttributeString => string.Join( " ", _attributes );

        public string Name { get; set; }
        public bool TrailingSlash { get; set; }

        public NamedElement( string name, params Expression<AttribFunc>[] attribs )
        {
            Name = name;
            TrailingSlash = true;

            foreach ( var expression in attribs )
            {
                Add( expression );
            }
        }

        public void Add( Attribute attrib )
        {
            _attributes.Add( attrib );
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return $"<{Name} {AttributeString}/>";
        }

        protected void SerializeAttributes( IHtmlSerializer serializer )
        {
            foreach ( var attribute in Attributes )
            {
                serializer.Write( " " );
                attribute.Serialize( serializer );
            }
        }

        public override void Serialize( IHtmlSerializer serializer )
        {
            serializer.Write( $"<{Name}" );
            SerializeAttributes( serializer );
            serializer.Write( TrailingSlash ? " />" : ">" );
        }
    }

    public class ContainerElement : NamedElement, IEchoDestination
    {
        private readonly List<Element> _children = new List<Element>();
        
        public IEnumerable<Element> Children => _children;
        public IEnumerable<NamedElement> NamedChildren => _children.OfType<NamedElement>();

        public AppendFunc AppendFunc => elements =>
        {
            foreach ( var element in elements )
            {
                Add( element );
            }

            return this;
        };
        
        internal virtual bool AllowIndentation => true;
        internal virtual bool Verbatim => false;
        internal override bool SuggestNewlineWhenSerialized => true;

        public ContainerElement( string name, params Expression<AttribFunc>[] attribs )
            : base( name, attribs )
        {
            TrailingSlash = false;
        }

        public void Add( Element element )
        {
            _children.Add( element );
        }
        
        public void Add( Action action )
        {
            DocumentHelper.PushEchoDestination( this );
            action();
            DocumentHelper.PopEchoDestination();
        }

        public override string ToString()
        {
            var writer = new StringWriter();

            using ( var serializer = new HtmlSerializer( writer ) )
            {
                Serialize( serializer );
            }

            return writer.ToString();
        }

        public override void Serialize( IHtmlSerializer serializer )
        {
            base.Serialize( serializer );

            var oneLiner = _children.All( x => !x.SuggestNewlineWhenSerialized ) && _children.Any( x => x is StringElement );

            if ( !oneLiner ) serializer.BeginBlock( AllowIndentation );

            foreach ( var element in Children )
            {
                if ( Verbatim && element is StringElement ) serializer.Write( ((StringElement) element).Value );
                else element.Serialize( serializer );
                if ( element.SuggestNewlineWhenSerialized || Verbatim ) serializer.SuggestNewline();
            }
            
            if ( !oneLiner ) serializer.EndBlock();

            serializer.Write( $"</{Name}>" );
        }
    }
}
