using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using MimeTypes;

namespace Ziks.WebServer
{
    [DefaultPriority( DefaultPriority )]
    public class StaticFileController : Controller
    {
        public const float DefaultPriority = -100f;

        public string RootPath { get; set; }
        public List<string> ExtensionWhitelist { get; } = new List<string>();

        public StaticFileController( string rootPath, params string[] extensionWhitelist )
        {
            RootPath = new FileInfo( rootPath ).FullName;
            ExtensionWhitelist.AddRange( extensionWhitelist );
        }

        private bool IsWhitelistedExtension( string ext )
        {
            return ExtensionWhitelist.Count == 0 ||
                ExtensionWhitelist
                    .Any( x => StringComparer.InvariantCultureIgnoreCase.Compare( x, ext ) == 0 );
        }

        [Get( MatchAllUrl = false )]
        public void Get()
        {
            var ext = Path.GetExtension( Request.Url.AbsolutePath );
            var requested = new Uri( Request.Url.GetLeftPart( UriPartial.Path ) );

            if ( string.IsNullOrEmpty( ext ) )
            {
                if ( !requested.AbsolutePath.EndsWith( "/" ) )
                {
                    requested = new Uri( requested + "/" );
                }

                ext = ".html";
                requested = new Uri( requested, "index.html" );
            }
            
            if ( !IsWhitelistedExtension( ext ) ) throw NotFoundException();

            var matched = MatchedUrl;
            if ( !matched.AbsolutePath.EndsWith( "/" ) ) matched = new Uri( matched + "/" );

            var relativePath = matched.MakeRelativeUri( requested );
            var filePath = Path.Combine( RootPath, relativePath.OriginalString );

            if ( !File.Exists( filePath ) ) throw NotFoundException();

            var info = new FileInfo( filePath );

            DateTime time;

            Response.ContentType = MimeTypeMap.GetMimeType( ext );
            Response.Headers.Add( "Cache-Control", "public, max-age=31556736" );
            Response.Headers.Add( "Last-Modified", info.LastWriteTimeUtc.ToString( "R" ) );

            var modifiedSince = Request.Headers["If-Modified-Since"];
            if ( modifiedSince != null && DateTime.TryParseExact( modifiedSince, "R",
                CultureInfo.InvariantCulture.DateTimeFormat,
                DateTimeStyles.AdjustToUniversal, out time )
                 && time < info.LastWriteTimeUtc )
            {
                Response.StatusCode = (int) HttpStatusCode.NotModified;
                Response.OutputStream.Close();
                return;
            }

            using ( var stream = File.Open( filePath, FileMode.Open, FileAccess.Read, FileShare.Read ) )
            {
                Response.ContentLength64 = stream.Length;
                if ( !IsHead ) stream.CopyTo( Response.OutputStream );
            }

            Response.OutputStream.Close();
        }

        public override string ToString()
        {
            return $"filesystem {{ {RootPath} }}";
        }
    }
}
