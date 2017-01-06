using System;
using System.Globalization;
using System.IO;
using System.Net;
using MimeTypes;

namespace Ziks.WebServer
{
    public class StaticFileController : Controller
    {
        public string RootPath { get; set; }

        public StaticFileController( string rootPath )
        {
            RootPath = new FileInfo( rootPath ).FullName;
        }
        
        [Get]
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

            var relativePath = MatchedUrl.MakeRelativeUri( requested );
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
    }
}
