// Copyright 2007-2008 The Apache Software Foundation.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.LegacySupport.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using NUnit.Framework;
    using SerializationCustomization;
    using Subscriptions;

    [TestFixture]
    public class ListTest
    {
        [Test]
        public void HowToTest()
        {
            using (var stream = new MemoryStream())
            {
                var writer = new BinaryFormatter();

                var x = new Bob();
                x.Numbers.Add(1);
                writer.Serialize(stream, x);
                stream.Position = 1;

                var reader = new BinaryFormatter();
                var ls = new LegacySurrogateSelector();
                //reader.SurrogateSelector = ls;
                var a = (Bob) reader.Deserialize(stream);
            }
        }
    }

    [Serializable]
    public class Bob
    {
        public Bob()
        {
            Numbers = new List<int>();
        }
        public IList<int> Numbers { get; set; }
    }
}