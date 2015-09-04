using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Practices.IoTJourney.Monitoring.EventProcessor
{
    public static class EventHubMonitorFactory
    {
        public static async Task<PartitionMonitor> CreateAsync(
            Configuration configuration,
            CancellationToken cancellationToken)
        {
            var samplingRate = TimeSpan.FromSeconds(1);

            var nsm = CreateNamespaceManager(configuration, samplingRate);

            Func<string, Task<PartitionDescription>> getEventHubPartitionAsync =
                partitionId => nsm.GetEventHubPartitionAsync(configuration.EventHubName, configuration.ConsumerGroupName, partitionId);

            var eventhub = await nsm.GetEventHubAsync(configuration.EventHubName).ConfigureAwait(false);

            var storageAccount = CloudStorageAccount.Parse(configuration.CheckpointStorageAccount);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var checkpointContainer = blobClient.GetContainerReference(configuration.EventHubName);

            var checkpoints = new PartitionCheckpointManager(
                configuration.ConsumerGroupName,
                eventhub.PartitionIds,
                checkpointContainer
                );

            return new PartitionMonitor(
                eventhub.PartitionIds,
                checkpoints.GetLastCheckpointAsync,
                getEventHubPartitionAsync,
                samplingRate,
                TimeSpan.FromSeconds(3));
        }

        private static NamespaceManager CreateNamespaceManager(Configuration configuration, TimeSpan samplingRate)
        {
            var endpoint = ServiceBusEnvironment.CreateServiceUri("sb", configuration.EventHubNamespace, string.Empty);
            var connectionString = ServiceBusConnectionStringBuilder.CreateUsingSharedAccessKey(endpoint,
                configuration.EventHubSasKeyName,
                configuration.EventHubSasKey);

            var nsm = NamespaceManager.CreateFromConnectionString(connectionString);
            //nsm.Settings.OperationTimeout = samplingRate;
            return nsm;
        }
    }
}