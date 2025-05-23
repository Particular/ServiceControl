namespace ServiceControl.Transports.ASBS;

class QueueSearcher : AbstractQueueSearcher
{
    readonly ServiceBusAdministrationClient managementClient;

    public QueueSearcher(ServiceBusAdministrationClient managementClient)
    {
        this.managementClient = managementClient;
    }

    public async Task<IEnumerable<string>> Search(Regex regex, CancellationToken cancellationToken = default)
    {
        var queues = new List<string>();

        await foreach (var queue in managementClient.GetQueuesAsync(cancellationToken: cancellationToken))
        {
            if (regex.IsMatch(queue.Name))
            {
                queues.Add(queue.Name);
            }
        }

        return queues;
    }
}