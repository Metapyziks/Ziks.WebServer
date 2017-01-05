using System;
using System.IO;
using System.Text;

namespace Ziks.WebServer.Html
{
    public interface IHtmlSerializable
    {
        void Serialize( IHtmlSerializer serializer );
    }

    public interface IHtmlSerializer
    {
        void Write( string value );
        void SuggestNewline();
        void BeginBlock();
        void EndBlock();
    }

    public class HtmlSerializer : IHtmlSerializer, IDisposable
    {
        public int IndentationWidth { get; set; } = 2;
        public int MaxLineWidth { get; set; } = 80;

        public TextWriter BaseWriter { get; }
        public bool OwnsWriter { get; }

        private readonly StringBuilder _lineBuffer = new StringBuilder();

        private bool _lineOverflow;
        private int _blockDepth;

        public HtmlSerializer( TextWriter writer, bool ownsWriter = true )
        {
            BaseWriter = writer;
            OwnsWriter = ownsWriter;
        }

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

            var indent = (_blockDepth + (_lineOverflow ? 1 : 0))*IndentationWidth;
            for ( var i = 0; i < indent; ++i )
            {
                BaseWriter.Write( " " );
            }

            BaseWriter.Write( _lineBuffer );
            BaseWriter.Write( "\r\n" );
            _lineBuffer.Remove( 0, _lineBuffer.Length );
        }

        public void Write( string value )
        {
            if ( _lineBuffer.Length + value.Length > MaxLineWidth )
            {
                FlushLine();
                _lineOverflow = true;
            }

            _lineBuffer.Append( value );
        }

        public void SuggestNewline()
        {
            FlushLine();
            _lineOverflow = false;
        }

        public void BeginBlock()
        {
            SuggestNewline();
            ++_blockDepth;
        }

        public void EndBlock()
        {
            SuggestNewline();
            --_blockDepth;
        }
    }
}
