namespace RocketMQ.Core.Diagnostics;

/// <summary>Activity tags used for opt-in publish timing diagnostics.</summary>
public static class PublishDiagnosticTags
{
    public const string Enabled = "rocketmq.publish.diagnostics.enabled";
    public const string WriterWaitMilliseconds = "rocketmq.publish.writer_wait_ms";
    public const string ConnectionOpenMilliseconds = "rocketmq.publish.connection_open_ms";
    public const string TransactionBeginMilliseconds = "rocketmq.publish.transaction_begin_ms";
    public const string TransactionWorkMilliseconds = "rocketmq.publish.transaction_work_ms";
    public const string TransactionCommitMilliseconds = "rocketmq.publish.transaction_commit_ms";
    public const string CleanupMilliseconds = "rocketmq.publish.cleanup_ms";
    public const string FingerprintMilliseconds = "rocketmq.publish.fingerprint_ms";
    public const string IdempotencyLookupMilliseconds = "rocketmq.publish.idempotency_lookup_ms";
    public const string ExchangeLookupMilliseconds = "rocketmq.publish.exchange_lookup_ms";
    public const string RoutingMilliseconds = "rocketmq.publish.routing_ms";
    public const string PublicationInsertMilliseconds = "rocketmq.publish.publication_insert_ms";
    public const string EnqueueMilliseconds = "rocketmq.publish.enqueue_ms";
    public const string ResultReadMilliseconds = "rocketmq.publish.result_read_ms";
}
