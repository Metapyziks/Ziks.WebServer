using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ziks.WebServer.Test
{
    [TestClass]
    public class UrlMatchers
    {
        [TestMethod]
        public void SimplePrefix1()
        {
            var matcher = UrlMatcher.Parse( "/foo/bar" );
            var prefix = new Uri( "http://localhost:8080" );
            
            Assert.IsFalse( matcher.IsMatch( new Uri( prefix, "/" ) ) );
            Assert.IsFalse( matcher.IsMatch( new Uri( prefix, "/foo" ) ) );
            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/foo/bar" ) ) );
            Assert.IsFalse( matcher.IsMatch( new Uri( prefix, "/foo/baz" ) ) );
            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/foo/bar/baz" ) ) );
            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/foo/bar/baz/biz" ) ) );
            Assert.IsFalse( matcher.IsMatch( new Uri( prefix, "/foo/barry" ) ) );
            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/foo/bar/hello.txt" ) ) );
        }
        
        [TestMethod]
        public void SimplePrefix2()
        {
            var matcher = UrlMatcher.Parse( "/" );
            var prefix = new Uri( "http://localhost:8080" );

            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/" ) ) );
            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/foo/bar" ) ) );
            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/foo/bar/hello.txt" ) ) );
        }
        
        [TestMethod]
        public void SimplePrefix3()
        {
            var matcher = UrlMatcher.Parse( "/foo/bar" );
            var prefix = new Uri( "http://localhost:8080" );
            
            Assert.AreEqual( "foo", matcher.GetSegments( new Uri( prefix, "/foo/bar/baz" ) )[0] );
            Assert.AreEqual( "bar", matcher.GetSegments( new Uri( prefix, "/foo/bar/baz" ) )[1] );
            Assert.AreEqual( "baz", matcher.GetSegments( new Uri( prefix, "/foo/bar/baz" ) )[2] );
        }
        
        [TestMethod]
        public void CapturingPrefix1()
        {
            var matcher = UrlMatcher.Parse( "/foo/{bar}" );
            var prefix = new Uri( "http://localhost:8080" );
            
            Assert.IsFalse( matcher.IsMatch( new Uri( prefix, "/" ) ) );
            Assert.IsFalse( matcher.IsMatch( new Uri( prefix, "/foo" ) ) );
            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/foo/bar" ) ) );
            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/foo/baz" ) ) );
            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/foo/bar/baz" ) ) );
            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/foo/bar/baz/biz" ) ) );
            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/foo/barry" ) ) );
            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/foo/bar/hello.txt" ) ) );
        }
        
        [TestMethod]
        public void CapturingPrefix2()
        {
            var matcher = UrlMatcher.Parse( "/foo/{bar}" );
            var prefix = new Uri( "http://localhost:8080" );

            Assert.AreEqual( "test1", matcher.GetSegments( new Uri( prefix, "/foo/test1" ) )["bar"] );
            Assert.AreEqual( "test2", matcher.GetSegments( new Uri( prefix, "/foo/test2/baz" ) )["bar"] );
            Assert.AreEqual( "test.txt", matcher.GetSegments( new Uri( prefix, "/foo/test.txt" ) )["bar"] );
        }
        
        [TestMethod]
        public void CapturingPrefix3()
        {
            var matcher = UrlMatcher.Parse( "/foo/{bar}/{baz}/boo" );
            var prefix = new Uri( "http://localhost:8080" );
            
            Assert.IsFalse( matcher.IsMatch( new Uri( prefix, "/" ) ) );
            Assert.IsFalse( matcher.IsMatch( new Uri( prefix, "/foo" ) ) );
            Assert.IsFalse( matcher.IsMatch( new Uri( prefix, "/foo/bar" ) ) );
            Assert.IsFalse( matcher.IsMatch( new Uri( prefix, "/foo/test1/test2" ) ) );
            Assert.IsTrue( matcher.IsMatch( new Uri( prefix, "/foo/test1/test2/boo" ) ) );
            Assert.IsFalse( matcher.IsMatch( new Uri( prefix, "/foo/test1/test2/boop" ) ) );
        }
        
        [TestMethod]
        public void CapturingPrefix4()
        {
            var matcher = UrlMatcher.Parse( "/foo/{bar}/{baz}/boo" );
            var prefix = new Uri( "http://localhost:8080" );

            Assert.AreEqual( "test1", matcher.GetSegments( new Uri( prefix, "/foo/test1/test2/boo" ) )["bar"] );
            Assert.AreEqual( "test2", matcher.GetSegments( new Uri( prefix, "/foo/test1/test2/boo" ) )["baz"] );
        }
        
        [TestMethod]
        public void Extensions1()
        {
            var png = UrlMatcher.Parse( "/foo/bar/{fileName}", ".png" );
            var json = UrlMatcher.Parse( "/foo/bar/{fileName}", ".json" );
            var any = UrlMatcher.Parse( "/foo/bar/{fileName}" );

            var matchers = new[]
            {
                json,
                png,
                any
            };

            Array.Sort( matchers );

            Assert.AreEqual( any, matchers[0] );
        }
        
        [TestMethod]
        public void Extensions2()
        {
            var png = UrlMatcher.Parse( "/foo/bar/{fileName}", ".png" );

            var prefix = new Uri( "http://localhost:8080" );
            Assert.IsTrue( png.IsMatch( new Uri( prefix, "/foo/bar/test.png" ) ) );
            Assert.IsFalse( png.IsMatch( new Uri( prefix, "/foo/bar/test.json" ) ) );
        }
    }
}
