// Copyright 2007-2016 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.Pipeline.Filters
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Util;


    /// <summary>
    /// Dispatches the ConsumeContext to the consumer method for the specified message type
    /// </summary>
    /// <typeparam name="TConsumer">The consumer type</typeparam>
    /// <typeparam name="TMessage">The message type</typeparam>
    public class MethodConsumerMessageFilter<TConsumer, TMessage> :
        IConsumerMessageFilter<TConsumer, TMessage>
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        void IProbeSite.Probe(ProbeContext context)
        {
            var scope = context.CreateScope("consume");
            scope.Add("method", $"Consume(ConsumeContext<{TypeMetadataCache<TMessage>.ShortName}> context)");
        }

        [DebuggerNonUserCode]
        Task IFilter<ConsumerConsumeContext<TConsumer, TMessage>>.Send(ConsumerConsumeContext<TConsumer, TMessage> context,
            IPipe<ConsumerConsumeContext<TConsumer, TMessage>> next)
        {
            var messageConsumer = context.Consumer as IConsumer<TMessage>;
            if (messageConsumer == null)
            {
                string message =
                    $"Consumer type {TypeMetadataCache<TConsumer>.ShortName} is not a consumer of message type {TypeMetadataCache<TMessage>.ShortName}";

                throw new ConsumerMessageException(message);
            }

            return messageConsumer.Consume(context);
        }
    }
}