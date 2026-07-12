using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using RocketMQ.Transport.Grpc.Protos;
using Microsoft.Extensions.Logging;

namespace RocketMQ.Client;

public class Consumer : IConsumer
{
    private readonly RocketMQ.Transport.Grpc.Protos.Consumer.ConsumerClient _client;
    private readonly ILogger<Consumer> _logger;
    private Task? _backgroundTask;
    private CancellationTokenSource? _cts;

    public Consumer(RocketMQ.Transport.Grpc.Protos.Consumer.ConsumerClient client, ILogger<Consumer> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Task StartConsumingAsync(string queueName, Func<MessageContext, Task<ConsumeResult>> handler, CancellationToken ct = default)
    {
        if (_backgroundTask != null)
        {
            throw new InvalidOperationException("Consumer is already running.");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _backgroundTask = Task.Run(() => ConsumeLoopAsync(queueName, handler, _cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    private async Task ConsumeLoopAsync(string queueName, Func<MessageContext, Task<ConsumeResult>> handler, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var request = new LeaseRequest
                {
                    QueueName = queueName ?? string.Empty,
                    VisibilityTimeoutSeconds = 30
                };

                var response = await _client.LeaseNextAsync(request, cancellationToken: ct);

                if (string.IsNullOrEmpty(response.LeaseId))
                {
                    // No messages available, delay and continue
                    await Task.Delay(1000, ct);
                    continue;
                }

                var context = new MessageContext(
                    response.LeaseId,
                    response.Payload.Memory,
                    response.DeliveryCount,
                    response.CorrelationId
                );

                ConsumeResult result;
                try
                {
                    result = await handler(context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message {LeaseId}", response.LeaseId);
                    result = ConsumeResult.Requeue; // Default on exception
                }

                if (result == ConsumeResult.Success)
                {
                    await _client.AckAsync(new AckRequest { LeaseId = response.LeaseId }, cancellationToken: ct);
                }
                else
                {
                    bool requeue = result == ConsumeResult.Requeue;
                    await _client.NackAsync(new NackRequest { LeaseId = response.LeaseId, Requeue = requeue }, cancellationToken: ct);
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
                if (ct.IsCancellationRequested) break;
                _logger.LogError(ex, "Error in consumer loop");
                await Task.Delay(2000, ct); // Delay before retrying loop on unhandled exception
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
                catch (OperationCanceledException) { }
            }
            _cts.Dispose();
        }
    }
}
