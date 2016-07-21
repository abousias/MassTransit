﻿// Copyright 2007-2015 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
#if !NETCORE
namespace MassTransit.Monitoring.Performance
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;


    public class ConsumerPerformanceCounters :
        PerformanceCounters
    {
        public const string CategoryName = "MassTransit Consumers";
        public const string CategoryHelp = "Consumers built using MassTransit";

        ConsumerPerformanceCounters()
            : base(CategoryName, CategoryHelp)
        {
        }

        public static CounterCreationData ConsumeRate => Cached.Instance.Value.Data[0];
        public static CounterCreationData TotalMessages => Cached.Instance.Value.Data[1];
        public static CounterCreationData Duration => Cached.Instance.Value.Data[2];
        public static CounterCreationData DurationBase => Cached.Instance.Value.Data[3];
        public static CounterCreationData TotalFaults => Cached.Instance.Value.Data[4];
        public static CounterCreationData FaultPercentage => Cached.Instance.Value.Data[5];
        public static CounterCreationData FaultPercentageBase => Cached.Instance.Value.Data[6];

        public static IPerformanceCounter CreateCounter(string counterName, string instanceName)
        {
            return Cached.Instance.Value.CreatePerformanceCounter(counterName, instanceName);
        }

        public static void Install()
        {
            ConsumerPerformanceCounters value = Cached.Instance.Value;
        }

        protected override IEnumerable<CounterCreationData> GetCounterData()
        {
            yield return
                new CounterCreationData("Messages/s", "Number of messages consumed per second", PerformanceCounterType.RateOfCountsPerSecond32);
            yield return
                new CounterCreationData("Total Messages", "Total number of messages consumed", PerformanceCounterType.NumberOfItems64);
            yield return
                new CounterCreationData("Average Duration", "The average time spent consuming a message", PerformanceCounterType.AverageCount64);
            yield return
                new CounterCreationData("Average Duration Base", "The average time spent consuming a message", PerformanceCounterType.AverageBase);
            yield return
                new CounterCreationData("Total Faults", "Total number of consumer faults generated", PerformanceCounterType.NumberOfItems64);
            yield return
                new CounterCreationData("Fault %", "The percentage of consumers generating faults", PerformanceCounterType.AverageCount64);
            yield return
                new CounterCreationData("Fault % Base", "The percentage of consumers generating faults", PerformanceCounterType.AverageBase);
        }


        static class Cached
        {
            internal static readonly Lazy<ConsumerPerformanceCounters> Instance = new Lazy<ConsumerPerformanceCounters>(() => new ConsumerPerformanceCounters());
        }
    }
}
#endif