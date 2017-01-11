using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ziks.WebServer.Test
{
    [TestClass]
    public class ResponseWriters
    {
        private class TestController : Controller
        {
            [ResponseWriter]
            // ReSharper disable MemberCanBeMadeStatic.Local
            // ReSharper disable UnusedParameter.Local
            public void OnServiceObject( object val ) { }
            
            [ResponseWriter]
            public void OnServiceRandom( Random val ) { }

            [ResponseWriter]
            public void OnServiceInt32( int val ) { }

            [ResponseWriter]
            public void OnServiceKeyValue1<T>( KeyValuePair<string, T> val ) { }

            [ResponseWriter]
            public void OnServiceKeyValue2<T1, T2>( KeyValuePair<T1, T2> val ) { }
            // ReSharper restore UnusedParameter.Local
            // ReSharper restore MemberCanBeMadeStatic.Local
        }

        private static void AssertMethodName<T>( string expected )
        {
            Assert.AreEqual( expected, ControllerActionMap.GetActionMap( typeof(TestController) ).GetResponseWriter( typeof(T) ).Name );
        }

        [TestMethod]
        public void ExactMatching1()
        {
            AssertMethodName<int>( nameof( TestController.OnServiceInt32 ) );
        }

        [TestMethod]
        public void ExactMatching2()
        {
            AssertMethodName<Random>( nameof( TestController.OnServiceRandom ) );
        }
        
        [TestMethod]
        public void BaseMatching1()
        {
            AssertMethodName<float>( nameof( TestController.OnServiceObject ) );
        }
        
        [TestMethod]
        public void BaseMatching2()
        {
            AssertMethodName<short>( nameof( TestController.OnServiceInt32 ) );
        }
        
        [TestMethod]
        public void GenericMatching1()
        {
            AssertMethodName<KeyValuePair<string, int>>( nameof( TestController.OnServiceKeyValue1 ) );
        }
        
        [TestMethod]
        public void GenericMatching2()
        {
            AssertMethodName<KeyValuePair<int, int>>( nameof( TestController.OnServiceKeyValue2 ) );
        }
    }
}
