using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Web;

namespace Ziks.WebServer.Html
{
    /// <summary>
    /// Delegate type for <see cref="HtmlAttribute"/> defining syntax abuse.
    /// </summary>
    /// <param name="name">Dummy parameter used for naming attributes.</param>
    /// <returns>Attribute value.</returns>
    public delegate object AttribFunc( object name );

    internal delegate HtmlElement AppendFunc( params HtmlElement[] htmlElements );

    /// <summary>
    /// Represents a HTML attribute name and value.
    /// </summary>
    public class HtmlAttribute : IHtmlSerializable
    {
        /// <summary>
        /// Attribute name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Raw attribute value.
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Creates a new <see cref="HtmlAttribute"/> with the given name and value.
        /// </summary>
        /// <param name="name">Attribute name.</param>
        /// <param name="value">Raw attribute value.</param>
        public HtmlAttribute( string name, object value )
        {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// Returns a string representing the attribute in the form {name}="{value}".
        /// </summary>
        public override string ToString()
        {
            return $"{Name}=\"{HttpUtility.HtmlAttributeEncode( Value.ToString() )}\"";
        }

        /// <summary>
        /// Write a representation of this instance to the given <see cref="IHtmlSerializer"/>.
        /// </summary>
        /// <param name="serializer"><see cref="IHtmlSerializer"/> to write to.</param>
        public void Serialize( IHtmlSerializer serializer )
        {
            serializer.Write( ToString() );
        }
    }

    /// <summary>
    /// Base class for HTML elements.
    /// </summary>
    public abstract class HtmlElement : IHtmlSerializable
    {
        internal virtual bool SuggestNewlineWhenSerialized => false;

        /// <summary>
        /// Implicit conversion from a <see cref="string"/> to a <see cref="StringHtmlElement"/>.
        /// </summary>
        /// <param name="value">String to convert.</param>
        public static implicit operator HtmlElement( string value )
        {
            return new StringHtmlElement( value );
        }

        /// <summary>
        /// Write a representation of this instance to the given <see cref="IHtmlSerializer"/>.
        /// </summary>
        /// <param name="serializer"><see cref="IHtmlSerializer"/> to write to.</param>
        public abstract void Serialize( IHtmlSerializer serializer );
    }

    /// <summary>
    /// Represents a HTML string entity.
    /// </summary>
    public class EntityHtmlElement : HtmlElement
    {
        /// <summary>
        /// Non-breaking space HTML entity.
        /// </summary>
        public static EntityHtmlElement Nbsp { get; } = new EntityHtmlElement( "nbsp" );

        /// <summary>
        /// Entity name or hash code.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Creates a <see cref="EntityHtmlElement"/> from the given entity name.
        /// </summary>
        /// <param name="name">HTML entity name.</param>
        public EntityHtmlElement( string name )
        {
            Value = name;
        }

        /// <summary>
        /// Creates a <see cref="EntityHtmlElement"/> from the given entity hash code.
        /// </summary>
        /// <param name="code">HTML entity hash code.</param>
        public EntityHtmlElement( int code )
        {
            Value = $"#{code}";
        }

        /// <summary>
        /// Returns a string representing the entity in the form &amp;{value};".
        /// </summary>
        public override string ToString()
        {
            return $"&{Value};";
        }

        /// <summary>
        /// Write a representation of this instance to the given <see cref="IHtmlSerializer"/>.
        /// </summary>
        /// <param name="serializer"><see cref="IHtmlSerializer"/> to write to.</param>
        public override void Serialize( IHtmlSerializer serializer )
        {
            serializer.Write( ToString() );
        }
    }

    /// <summary>
    /// Represents a nested string within a HTML document.
    /// </summary>
    public class StringHtmlElement : HtmlElement
    {
        internal override bool SuggestNewlineWhenSerialized => Value.Contains( '\n' );

        /// <summary>
        /// Raw string value.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Creates a new <see cref="StringHtmlElement"/> from the given raw string.
        /// </summary>
        /// <param name="value">Raw stromg value.</param>
        public StringHtmlElement( string value )
        {
            Value = value;
        }

        /// <summary>
        /// Returns a HTML encoded representation of the string.
        /// </summary>
        public override string ToString()
        {
            return HttpUtility.HtmlEncode( Value );
        }

        /// <summary>
        /// Write a representation of this instance to the given <see cref="IHtmlSerializer"/>.
        /// </summary>
        /// <param name="serializer"><see cref="IHtmlSerializer"/> to write to.</param>
        public override void Serialize( IHtmlSerializer serializer )
        {
            serializer.Write( ToString() );
        }
    }

    /// <summary>
    /// Represents a HTML tag with a name and optionally a set of attributes.
    /// </summary>
    public class NamedHtmlElement : HtmlElement, IEnumerable
    {
        private readonly List<HtmlAttribute> _attributes = new List<HtmlAttribute>();

        /// <summary>
        /// Gets the set of <see cref="HtmlAttribute"/>s contained in this element.
        /// </summary>
        public IEnumerable<HtmlAttribute> Attributes => _attributes;

        internal override bool SuggestNewlineWhenSerialized => Name == "br";

        /// <summary>
        /// Gets a string representation of all the <see cref="HtmlAttribute"/>s contained in this element.
        /// </summary>
        public string AttributeString => string.Join( " ", _attributes );

        /// <summary>
        /// Tag name of this element.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// If true, the opening tag for this element should include a trailing '/'.
        /// </summary>
        public bool TrailingSlash { get; set; }

        /// <summary>
        /// Creates a new <see cref="NamedHtmlElement"/> with the given name and set of attributes.
        /// </summary>
        /// <param name="name">Tag name of this element.</param>
        /// <param name="attribs">
        /// A set of attribute lambda functions where the parameter name is used as the attribute name,
        /// and the returned value is the attribute value.
        /// </param>
        public NamedHtmlElement( string name, params Expression<AttribFunc>[] attribs )
        {
            Name = name;
            TrailingSlash = true;

            foreach ( var expression in attribs )
            {
                Add( new HtmlAttribute( expression.Parameters[0].Name, expression.Compile()( null ) ) );
            }
        }

        /// <summary>
        /// Adds a <see cref="HtmlAttribute"/> to this element.
        /// </summary>
        /// <param name="attrib"><see cref="HtmlAttribute"/> to add.</param>
        public void Add( HtmlAttribute attrib )
        {
            _attributes.Add( attrib );
        }

        /// <summary>
        /// Removes a <see cref="HtmlAttribute"/> from this element.
        /// </summary>
        /// <param name="attrib"><see cref="HtmlAttribute"/> to remove.</param>
        public void Remove( HtmlAttribute attrib )
        {
            _attributes.Remove( attrib );
        }

        /// <summary>
        /// Removes all <see cref="HtmlAttribute"/>s from this element.
        /// </summary>
        public void ClearAttributes()
        {
            _attributes.Clear();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
        
        /// <summary>
        /// Returns the HTML representing this element.
        /// </summary>
        public override string ToString()
        {
            return $"<{Name} {AttributeString}/>";
        }

        /// <summary>
        /// Serializes the attributes in this element to the given <see cref="IHtmlSerializer"/>.
        /// </summary>
        /// <param name="serializer"><see cref="IHtmlSerializer"/> to write to.</param>
        protected void SerializeAttributes( IHtmlSerializer serializer )
        {
            foreach ( var attribute in Attributes )
            {
                serializer.Write( " " );
                attribute.Serialize( serializer );
            }
        }

        /// <summary>
        /// Write a representation of this instance to the given <see cref="IHtmlSerializer"/>.
        /// </summary>
        /// <param name="serializer"><see cref="IHtmlSerializer"/> to write to.</param>
        public override void Serialize( IHtmlSerializer serializer )
        {
            serializer.Write( $"<{Name}" );
            SerializeAttributes( serializer );
            serializer.Write( TrailingSlash ? " />" : ">" );
        }
    }

    /// <summary>
    /// Represents a named HTML tag that can contain nested elements.
    /// </summary>
    public class ContainerHtmlElement : NamedHtmlElement, IEchoDestination
    {
        private readonly List<HtmlElement> _children = new List<HtmlElement>();
        
        /// <summary>
        /// Gets the nested <see cref="HtmlElement"/>s contained within this element.
        /// </summary>
        public IEnumerable<HtmlElement> Children => _children;


        /// <summary>
        /// Gets only nested <see cref="NamedHtmlElement"/>s contained within this element.
        /// </summary>
        public IEnumerable<NamedHtmlElement> NamedChildren => _children.OfType<NamedHtmlElement>();

        internal AppendFunc AppendFunc => elements =>
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

        /// <summary>
        /// Helper property to get or set the text value contained in a <see cref="ContainerHtmlElement"/>.
        /// </summary>
        public virtual string Text
        {
            get
            {
                return string.Join( " ", _children
                    .Select( x => x is StringHtmlElement ? x.ToString()
                        : x is EntityHtmlElement ? HttpUtility.HtmlDecode( x.ToString() )
                        : x is ContainerHtmlElement ? ((ContainerHtmlElement) x).Text
                        : "" ) );
            }
            set
            {
                if ( _children.Count == 1 && _children[0] is StringHtmlElement )
                {
                    ((StringHtmlElement) _children[0]).Value = value;
                    return;
                }

                _children.Clear();
                _children.Add( new StringHtmlElement( value ) );
            }
        }
        
        /// <summary>
        /// Creates a new <see cref="ContainerHtmlElement"/> with the given name and set of attributes.
        /// </summary>
        /// <param name="name">Tag name of this element.</param>
        /// <param name="attribs">
        /// A set of attribute lambda functions where the parameter name is used as the attribute name,
        /// and the returned value is the attribute value.
        /// </param>
        public ContainerHtmlElement( string name, params Expression<AttribFunc>[] attribs )
            : base( name, attribs )
        {
            TrailingSlash = false;
        }

        /// <summary>
        /// Adds a nested <see cref="HtmlElement"/> to this element.
        /// </summary>
        /// <param name="htmlElement"><see cref="HtmlElement"/> to add.</param>
        public void Add( HtmlElement htmlElement )
        {
            _children.Add( htmlElement );
        }

        /// <summary>
        /// Removes the given child element from this instance.
        /// </summary>
        /// <param name="htmlElement">Child element to remove.</param>
        public void Remove( HtmlElement htmlElement )
        {
            _children.Remove( htmlElement );
        }

        /// <summary>
        /// Removes all child elements from this instance.
        /// </summary>
        public void ClearChildren()
        {
            _children.Clear();
        }
        
        /// <summary>
        /// Invokes the given <paramref name="action"/> in a context where calls to
        /// <see cref="HtmlDocumentHelper.Echo"/> will append elements to this instance.
        /// </summary>
        /// <param name="action"><see cref="Action"/> to invoke.</param>
        public void Add( Action action )
        {
            HtmlDocumentHelper.PushEchoDestination( this );
            action();
            HtmlDocumentHelper.PopEchoDestination();
        }
        
        /// <summary>
        /// Returns the HTML representing this element.
        /// </summary>
        public override string ToString()
        {
            var writer = new StringWriter();

            using ( var serializer = new HtmlSerializer( writer ) )
            {
                Serialize( serializer );
            }

            return writer.ToString();
        }

        /// <summary>
        /// Write a representation of this instance to the given <see cref="IHtmlSerializer"/>.
        /// </summary>
        /// <param name="serializer"><see cref="IHtmlSerializer"/> to write to.</param>
        public override void Serialize( IHtmlSerializer serializer )
        {
            base.Serialize( serializer );

            var oneLiner = _children.All( x => !x.SuggestNewlineWhenSerialized ) && _children.Any( x => x is StringHtmlElement );

            if ( !oneLiner ) serializer.BeginBlock( AllowIndentation );

            foreach ( var element in Children )
            {
                if ( Verbatim && element is StringHtmlElement ) serializer.Write( ((StringHtmlElement) element).Value );
                else element.Serialize( serializer );
                if ( element.SuggestNewlineWhenSerialized || Verbatim ) serializer.SuggestNewline();
            }
            
            if ( !oneLiner ) serializer.EndBlock();

            serializer.Write( $"</{Name}>" );
        }
    }
}
