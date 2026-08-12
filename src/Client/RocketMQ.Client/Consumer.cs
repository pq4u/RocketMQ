using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Client;

public class Consumer : IConsumer
{
    private readonly RocketMQ.Transport.Grpc.Protos.Consumer.ConsumerClient _client;
    private readonly ILogger<Consumer> _logger;
    private Task? _backgroundTask;
    private CancellationTokenSource? _cts;

    public Consumer(
        RocketMQ.Transport.Grpc.Protos.Consumer.ConsumerClient client,
        ILogger<Consumer> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Task StartConsumingAsync(
        string queueName,
        Func<MessageContext, Task<ConsumeResult>> handler,
        CancellationToken ct = default)
        => StartConsumingAsync(queueName, handler, new ConsumerOptions(), ct);

    public Task StartConsumingAsync(
        string queueName,
        Func<MessageContext, Task<ConsumeResult>> handler,
        ConsumerOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);

        if (_backgroundTask != null)
        {
            throw new InvalidOperationException("Consumer is already running.");
        }

        _ = options.VisibilityTimeoutSeconds;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _backgroundTask = Task.Run(
            () => ConsumeLoopAsync(queueName, handler, options, _cts.Token),
            _cts.Token);

        return Task.CompletedTask;
    }

    private async Task ConsumeLoopAsync(
        string queueName,
        Func<MessageContext, Task<ConsumeResult>> handler,
        ConsumerOptions options,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var request = new LeaseRequest
                {
                    QueueName = queueName ?? string.Empty,
                    VisibilityTimeoutSeconds = options.VisibilityTimeoutSeconds
                };

                var response = await _client.LeaseNextAsync(request, cancellationToken: ct);

                if (string.IsNullOrEmpty(response.LeaseId))
                {
                    await Task.Delay(1000, ct);
                    continue;
                }

                var context = new MessageContext(
                    response.LeaseId,
                    response.Payload.Memory,
                    response.DeliveryCount,
                    response.CorrelationId)
                {
                    MessageId = response.MessageId
                };

                ConsumeResult result;
                try
                {
                    result = await handler(context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message {LeaseId}", response.LeaseId);
                    result = ConsumeResult.Requeue;
                }

                if (result == ConsumeResult.Success)
                {
                    await _client.AckAsync(
                        new AckRequest { LeaseId = response.LeaseId },
                        cancellationToken: ct);
                }
                else
                {
                    var requeue = result == ConsumeResult.Requeue;
                    await _client.NackAsync(
                        new NackRequest { LeaseId = response.LeaseId, Requeue = requeue },
                        cancellationToken: ct);
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                _logger.LogError(ex, "Error in consumer loop");
                await Task.Delay(2000, ct);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            if (_backgroundTask != null)
            {
                try
                {
                    await _backgroundTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            _cts.Dispose();
        }
    }
}
