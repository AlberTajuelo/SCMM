﻿using Azure.Messaging.ServiceBus.Administration;
using SCMM.Shared.Azure.ServiceBus.Attributes;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SCMM.Shared.Azure.ServiceBus.Extensions
{
    public static class ServiceBusAdministrationClientExtensions
    {
        public static Task<bool> SubscriptionExistsAsync<T>(this ServiceBusAdministrationClient client) where T : IMessage
        {
            return SubscriptionExistsAsync(client, typeof(T));
        }

        public static async Task<bool> SubscriptionExistsAsync(this ServiceBusAdministrationClient client, Type messageType)
        {
            var topicName = messageType.GetCustomAttribute<TopicAttribute>()?.Name;
            if (!String.IsNullOrEmpty(topicName))
            {
                var subscriptionExists = await client.SubscriptionExistsAsync(topicName, Assembly.GetEntryAssembly().GetName().Name);
                return subscriptionExists.Value;
            }

            throw new ArgumentException(nameof(messageType), "Message type must have a [Topic] attribute declaration");
        }

        public static Task<SubscriptionProperties> CreateSubscriptionAsync<T>(this ServiceBusAdministrationClient client, Action<CreateSubscriptionOptions> optionsAction = null) where T : IMessage
        {
            return CreateSubscriptionAsync(client, typeof(T), optionsAction);
        }

        public static async Task<SubscriptionProperties> CreateSubscriptionAsync(this ServiceBusAdministrationClient client, Type messageType, Action<CreateSubscriptionOptions> optionsAction = null)
        {
            var topicName = messageType.GetCustomAttribute<TopicAttribute>()?.Name;
            if (!String.IsNullOrEmpty(topicName))
            {
                var options = new CreateSubscriptionOptions(topicName, Assembly.GetEntryAssembly().GetName().Name);
                optionsAction?.Invoke(options);
                var subscriptionProperties = await client.CreateSubscriptionAsync(options);
                return subscriptionProperties.Value;
            }

            throw new ArgumentException(nameof(messageType), "Message type must have a [Topic] attribute declaration");
        }
    }
}
