using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ziks.WebServer.Html
{
    /// <summary>
    /// Interface for types that are serialized to aspects of a HTML document.
    /// </summary>
    public interface IHtmlSerializable
    {
        /// <summary>
        /// Write a representation of this instance to the given <see cref="IHtmlSerializer"/>.
        /// </summary>
        /// <param name="serializer"><see cref="IHtmlSerializer"/> to write to.</param>
        void Serialize( IHtmlSerializer serializer );
    }

    /// <summary>
    /// Interface for types that a HTML document can be written to.
    /// </summary>
    public interface IHtmlSerializer
    {
        /// <summary>
        /// Write the given string verbatim to the document.
        /// </summary>
        /// <param name="value">String to write.</param>
        void Write( string value );

        /// <summary>
        /// Used to suggest a line break after the last written value.
        /// </summary>
        void SuggestNewline();

        /// <summary>
        /// Used to mark the start of a region of code that can be optionally indented.
        /// Must be exactly matched with one <see cref="EndBlock"/> call following it.
        /// </summary>
        /// <param name="allowIndentation">If true, the block can be indented.</param>
        void BeginBlock( bool allowIndentation = true );

        /// <summary>
        /// Used to mark the end of a region of code that can be indented.
        /// </summary>
        void EndBlock();
    }

    /// <summary>
    /// Default basic implementation of a <see cref="IHtmlSerializer"/>.
    /// </summary>
    public class HtmlSerializer : IHtmlSerializer, IDisposable
    {
        /// <summary>
        /// Desired number of spaces used for each indentation level.
        /// </summary>
        public int IndentationWidth { get; set; } = 2;

        /// <summary>
        /// Hinted desired maximum line width in characters before wrapping is used.
        /// </summary>
        public int MaxLineWidth { get; set; } = 80;

        /// <summary>
        /// <see cref="TextWriter"/> the HTML document will be written to.
        /// </summary>
        public TextWriter BaseWriter { get; }

        /// <summary>
        /// If true, closing this instance will also close <see cref="BaseWriter"/>.
        /// </summary>
        public bool OwnsWriter { get; }

        private readonly StringBuilder _lineBuffer = new StringBuilder();
        private readonly Stack<bool> _indentationAllowed = new Stack<bool>(); 

        private bool _lineOverflow;
        private int _blockDepth;

        /// <summary>
        /// Creates a new <see cref="HtmlSerializer"/> that writes to the given <see cref="TextWriter"/>.
        /// </summary>
        /// <param name="writer"><see cref="TextWriter"/> the HTML document will be written to.</param>
        /// <param name="ownsWriter">If true, closing this instance will also close <see cref="BaseWriter"/>.</param>
        public HtmlSerializer( TextWriter writer, bool ownsWriter = true )
        {
            BaseWriter = writer;
            OwnsWriter = ownsWriter;
        }

        /// <summary>
        /// Flushes any internal buffers. Also closes <see cref="BaseWriter"/> if <see cref="OwnsWriter"/> is true.
        /// </summary>
        public void Close()
        {
            FlushLine();
            if (OwnsWriter) BaseWriter.Close();
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        private void FlushLine()
        {
            if ( _lineBuffer.Length == 0 ) return;

            if ( _indentationAllowed.Count == 0 || _indentationAllowed.Peek() )
            {
                var indent = (_blockDepth + (_lineOverflow ? 1 : 0))*IndentationWidth;
                for ( var i = 0; i < indent; ++i )
                {
                    BaseWriter.Write( " " );
                }
            }

            BaseWriter.Write( _lineBuffer );
            BaseWriter.Write( "\r\n" );
            _lineBuffer.Remove( 0, _lineBuffer.Length );
        }

        /// <summary>
        /// Write the given string verbatim to the document.
        /// </summary>
        /// <param name="value">String to write.</param>
        public void Write( string value )
        {
            if ( _lineBuffer.Length + value.Length > MaxLineWidth )
            {
                FlushLine();
                _lineOverflow = true;
            }

            _lineBuffer.Append( value );
        }

        /// <summary>
        /// Used to suggest a line break after the last written value.
        /// </summary>
        public void SuggestNewline()
        {
            FlushLine();
            _lineOverflow = false;
        }

        /// <summary>
        /// Used to mark the start of a region of code that can be optionally indented.
        /// Must be exactly matched with one <see cref="IHtmlSerializer.EndBlock"/> call following it.
        /// </summary>
        /// <param name="allowIndentation">If true, the block can be indented.</param>
        public void BeginBlock( bool allowIndentation = true )
        {
            SuggestNewline();
            ++_blockDepth;
            _indentationAllowed.Push( allowIndentation );
        }

        /// <summary>
        /// Used to mark the end of a region of code that can be indented.
        /// </summary>
        public void EndBlock()
        {
            _indentationAllowed.Pop();
            SuggestNewline();
            --_blockDepth;
        }
    }
}
