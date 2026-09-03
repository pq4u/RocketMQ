using System.Diagnostics;
using Grpc.Core;
using RocketMQ.Core.Abstractions;
using RocketMQ.Core.Diagnostics;
using RocketMQ.Core.Models;
using RocketMQ.Transport.Grpc.Protos;

namespace RocketMQ.Transport.Grpc.Services;

public sealed class ProducerService : Producer.ProducerBase
{
    private readonly IMessagePublisher _publisher;

    public ProducerService(IMessagePublisher publisher) => _publisher = publisher;

    public override async Task<PublishResponse> Publish(PublishRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ExchangeName)) throw InvalidArgument("Exchange name is required.");
        if (!string.IsNullOrWhiteSpace(request.CorrelationId) && !Guid.TryParse(request.CorrelationId, out _)) throw InvalidArgument("Correlation ID must be a valid GUID.");
        if (!string.IsNullOrWhiteSpace(request.PublishId) && !Guid.TryParse(request.PublishId, out _)) throw InvalidArgument("Publish ID must be a valid GUID.");

        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid() : Guid.Parse(request.CorrelationId);
        var publishId = string.IsNullOrWhiteSpace(request.PublishId) ? Guid.NewGuid() : Guid.Parse(request.PublishId);
        var envelope = new Envelope(request.ExchangeName, request.RoutingKey, new InboundMessage(Guid.NewGuid(), correlationId, request.Payload.Memory, DateTimeOffset.UtcNow));
        using var diagnosticsActivity = request.IncludeDiagnostics
            ? new Activity("RocketMQ.PublishDiagnostics").Start()
            : null;
        diagnosticsActivity?.SetTag(PublishDiagnosticTags.Enabled, true);
        var serverStarted = Stopwatch.GetTimestamp();
        try
        {
            var result = await _publisher.PublishAsync(publishId, envelope, context.CancellationToken);
            var response = new PublishResponse
            {
                Success = result.Status == PublishStatus.Accepted,
                MessageId = result.MessageId.ToString(),
                PublishId = result.PublishId.ToString(),
                Status = result.Status.ToString()
            };
            response.DestinationQueues.AddRange(result.DestinationQueues);
            if (diagnosticsActivity is not null)
            {
                response.Diagnostics = CreateDiagnostics(diagnosticsActivity, Stopwatch.GetElapsedTime(serverStarted).TotalMilliseconds);
            }
            return response;
        }
        catch (KeyNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Publish ID", StringComparison.Ordinal))
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, ex.Message));
        }
    }

    private static PublishDiagnostics CreateDiagnostics(Activity activity, double serverTotalMilliseconds) => new()
    {
        ServerTotalMs = serverTotalMilliseconds,
        WriterWaitMs = ReadMilliseconds(activity, PublishDiagnosticTags.WriterWaitMilliseconds),
        ConnectionOpenMs = ReadMilliseconds(activity, PublishDiagnosticTags.ConnectionOpenMilliseconds),
        TransactionBeginMs = ReadMilliseconds(activity, PublishDiagnosticTags.TransactionBeginMilliseconds),
        TransactionWorkMs = ReadMilliseconds(activity, PublishDiagnosticTags.TransactionWorkMilliseconds),
        TransactionCommitMs = ReadMilliseconds(activity, PublishDiagnosticTags.TransactionCommitMilliseconds),
        CleanupMs = ReadMilliseconds(activity, PublishDiagnosticTags.CleanupMilliseconds),
        FingerprintMs = ReadMilliseconds(activity, PublishDiagnosticTags.FingerprintMilliseconds),
        IdempotencyLookupMs = ReadMilliseconds(activity, PublishDiagnosticTags.IdempotencyLookupMilliseconds),
        ExchangeLookupMs = ReadMilliseconds(activity, PublishDiagnosticTags.ExchangeLookupMilliseconds),
        RoutingMs = ReadMilliseconds(activity, PublishDiagnosticTags.RoutingMilliseconds),
        PublicationInsertMs = ReadMilliseconds(activity, PublishDiagnosticTags.PublicationInsertMilliseconds),
        EnqueueMs = ReadMilliseconds(activity, PublishDiagnosticTags.EnqueueMilliseconds),
        ResultReadMs = ReadMilliseconds(activity, PublishDiagnosticTags.ResultReadMilliseconds),
        BatchSize = ReadInt32(activity, PublishDiagnosticTags.BatchSize),
        BatchAssemblyMs = ReadMilliseconds(activity, PublishDiagnosticTags.BatchAssemblyMilliseconds)
    };

    private static double ReadMilliseconds(Activity activity, string tag)
        => activity.GetTagItem(tag) is double value ? value : 0;

    private static int ReadInt32(Activity activity, string tag)
        => activity.GetTagItem(tag) is int value ? value : 0;

    private static RpcException InvalidArgument(string message) => new(new Status(StatusCode.InvalidArgument, message));
}
