namespace Scynapse.Runtime;

internal static class InstrumentNames
{
    // Networking
    public const string NETWORKING_SOCKETS_CLOSED = "scynapse-networking-sockets-closed";
    public const string NETWORKING_SOCKETS_OPENED = "scynapse-networking-sockets-opened";

    // Messaging
    public const string MESSAGING_SENT_MESSAGES_SIZE = "scynapse-messaging-sent-messages-size";
    public const string MESSAGING_RECEIVED_MESSAGES_SIZE = "scynapse-messaging-received-messages-size";
    public const string MESSAGING_SENT_BYTES_HEADER = "scynapse-messaging-sent-header-size";
    public const string MESSAGING_SENT_FAILED = "scynapse-messaging-sent-failed";
    public const string MESSAGING_SENT_DROPPED = "scynapse-messaging-sent-dropped";
    public const string MESSAGING_RECEIVED_BYTES_HEADER = "scynapse-messaging-received-header-size";

    public const string MESSAGING_DISPATCHER_RECEIVED = "scynapse-messaging-processing-dispatcher-received";
    public const string MESSAGING_DISPATCHER_PROCESSED = "scynapse-messaging-processing-dispatcher-processed";
    public const string MESSAGING_DISPATCHER_FORWARDED = "scynapse-messaging-processing-dispatcher-forwarded";
    public const string MESSAGING_IMA_RECEIVED = "scynapse-messaging-processing-ima-received";
    public const string MESSAGING_IMA_ENQUEUED = "scynapse-messaging-processing-ima-enqueued";
    public const string MESSAGING_PROCESSING_ACTIVATION_DATA_ALL = "scynapse-messaging-processing-activation-data";
    public const string MESSAGING_PINGS_SENT = "scynapse-messaging-pings-sent";
    public const string MESSAGING_PINGS_RECEIVED = "scynapse-messaging-pings-received";
    public const string MESSAGING_PINGS_REPLYRECEIVED = "scynapse-messaging-pings-reply-received";
    public const string MESSAGING_PINGS_REPLYMISSED = "scynapse-messaging-pings-reply-missed";
    public const string MESSAGING_EXPIRED = "scynapse-messaging-expired";
    public const string MESSAGING_REJECTED = "scynapse-messaging-rejected";
    public const string MESSAGING_REROUTED = "scynapse-messaging-rerouted";
    public const string MESSAGING_SENT_LOCALMESSAGES = "scynapse-messaging-sent-local";

    // Gateway
    public const string GATEWAY_CONNECTED_CLIENTS = "scynapse-gateway-connected-clients";
    public const string GATEWAY_SENT = "scynapse-gateway-sent";
    public const string GATEWAY_RECEIVED = "scynapse-gateway-received";
    public const string GATEWAY_LOAD_SHEDDING = "scynapse-gateway-load-shedding";

    // Runtime
    public const string SCHEDULER_NUM_LONG_RUNNING_TURNS = "scynapse-scheduler-long-running-turns";

    // Catalog
    public const string CATALOG_ACTIVATION_COUNT = "scynapse-catalog-activations";
    public const string CATALOG_ACTIVATION_WORKING_SET = "scynapse-catalog-activation-working-set";
    public const string CATALOG_ACTIVATION_CREATED = "scynapse-catalog-activation-created";
    public const string CATALOG_ACTIVATION_DESTROYED = "scynapse-catalog-activation-destroyed";
    public const string CATALOG_ACTIVATION_FAILED_TO_ACTIVATE = "scynapse-catalog-activation-failed-to-activate";
    public const string CATALOG_ACTIVATION_COLLECTION_NUMBER_OF_COLLECTIONS = "scynapse-catalog-activation-collections";
    public const string CATALOG_ACTIVATION_SHUTDOWN = "scynapse-catalog-activation-shutdown";
    public const string CATALOG_ACTIVATION_NON_EXISTENT_ACTIVATIONS = "scynapse-catalog-activation-non-existent";
    public const string CATALOG_ACTIVATION_CONCURRENT_REGISTRATION_ATTEMPTS = "scynapse-catalog-activation-concurrent-registration-attempts";

    // Directory
    // not used...
    public const string DIRECTORY_LOOKUPS_LOCAL_ISSUED = "scynapse-directory-lookups-local-issued";
    // not used...
    public const string DIRECTORY_LOOKUPS_LOCAL_SUCCESSES = "scynapse-directory-lookups-local-successes";
    public const string DIRECTORY_LOOKUPS_FULL_ISSUED = "scynapse-directory-lookups-full-issued";
    public const string DIRECTORY_LOOKUPS_REMOTE_SENT = "scynapse-directory-lookups-remote-sent";
    public const string DIRECTORY_LOOKUPS_REMOTE_RECEIVED = "scynapse-directory-lookups-remote-received";
    public const string DIRECTORY_LOOKUPS_LOCALDIRECTORY_ISSUED = "scynapse-directory-lookups-local-directory-issued";
    public const string DIRECTORY_LOOKUPS_LOCALDIRECTORY_SUCCESSES = "scynapse-directory-lookups-local-directory-successes";
    // not used
    public const string DIRECTORY_LOOKUPS_CACHE_ISSUED = "scynapse-directory-lookups-cache-issued";
    // not used
    public const string DIRECTORY_LOOKUPS_CACHE_SUCCESSES = "scynapse-directory-lookups-cache-successes";
    public const string DIRECTORY_VALIDATIONS_CACHE_SENT = "scynapse-directory-validations-cache-sent";
    public const string DIRECTORY_VALIDATIONS_CACHE_RECEIVED = "scynapse-directory-validations-cache-received";
    public const string DIRECTORY_PARTITION_SIZE = "scynapse-directory-partition-size";
    public const string DIRECTORY_CACHE_SIZE = "scynapse-directory-cache-size";
    public const string DIRECTORY_RING_RINGSIZE = "scynapse-directory-ring-size";
    public const string DIRECTORY_RING_MYPORTION_RINGDISTANCE = "scynapse-directory-ring-local-portion-distance";
    public const string DIRECTORY_RING_MYPORTION_RINGPERCENTAGE = "scynapse-directory-ring-local-portion-percentage";
    public const string DIRECTORY_RING_MYPORTION_AVERAGERINGPERCENTAGE = "scynapse-directory-ring-local-portion-average-percentage";
    public const string DIRECTORY_REGISTRATIONS_SINGLE_ACT_ISSUED = "scynapse-directory-registrations-single-act-issued";
    public const string DIRECTORY_REGISTRATIONS_SINGLE_ACT_LOCAL = "scynapse-directory-registrations-single-act-local";
    public const string DIRECTORY_REGISTRATIONS_SINGLE_ACT_REMOTE_SENT = "scynapse-directory-registrations-single-act-remote-sent";
    public const string DIRECTORY_REGISTRATIONS_SINGLE_ACT_REMOTE_RECEIVED = "scynapse-directory-registrations-single-act-remote-received";
    public const string DIRECTORY_UNREGISTRATIONS_ISSUED = "scynapse-directory-unregistrations-issued";
    public const string DIRECTORY_UNREGISTRATIONS_LOCAL = "scynapse-directory-unregistrations-local";
    public const string DIRECTORY_UNREGISTRATIONS_REMOTE_SENT = "scynapse-directory-unregistrations-remote-sent";
    public const string DIRECTORY_UNREGISTRATIONS_REMOTE_RECEIVED = "scynapse-directory-unregistrations-remote-received";
    public const string DIRECTORY_UNREGISTRATIONS_MANY_ISSUED = "scynapse-directory-unregistrations-many-issued";
    public const string DIRECTORY_UNREGISTRATIONS_MANY_REMOTE_SENT = "scynapse-directory-unregistrations-many-remote-sent";
    public const string DIRECTORY_UNREGISTRATIONS_MANY_REMOTE_RECEIVED = "scynapse-directory-unregistrations-many-remote-received";

    public const string DIRECTORY_RANGE_SNAPSHOT_TRANSFER_COUNT = "scynapse-directory-snapshot-transfer-count";
    public const string DIRECTORY_RANGE_SNAPSHOT_TRANSFER_DURATION = "scynapse-directory-snapshot-transfer-duration";
    public const string DIRECTORY_RANGE_RECOVERY_COUNT = "scynapse-directory-recovery-count";
    public const string DIRECTORY_RANGE_RECOVERY_DURATION = "scynapse-directory-recovery-duration";
    public const string DIRECTORY_RANGE_LOCK_HELD_DURATION = "scynapse-directory-range-lock-held-duration";

    // ConsistentRing
    public const string CONSISTENTRING_SIZE = "scynapse-consistent-ring-size";
    public const string CONSISTENTRING_LOCAL_SIZE_PERCENTAGE = "scynapse-consistent-ring-range-percentage-local";
    public const string CONSISTENTRING_AVERAGE_SIZE_PERCENTAGE = "scynapse-consistent-ring-range-percentage-average";

    // Watchdog
    public const string WATCHDOG_NUM_HEALTH_CHECKS = "scynapse-watchdog-health-checks";
    public const string WATCHDOG_NUM_FAILED_HEALTH_CHECKS = "scynapse-watchdog-health-checks-failed";

    // Client
    public const string CLIENT_CONNECTED_GATEWAY_COUNT = "scynapse-client-connected-gateways";

    // Misc
    public const string GRAIN_COUNTS = "scynapse-grains";
    public const string SYSTEM_TARGET_COUNTS = "scynapse-system-targets";

    // App requests
    public const string APP_REQUESTS_LATENCY_HISTOGRAM = "scynapse-app-requests-latency";
    public const string APP_REQUESTS_TIMED_OUT = "scynapse-app-requests-timedout";
    public const string APP_REQUESTS_CANCELED = "scynapse-app-requests-canceled";

    // Reminders
    public const string REMINDERS_TARDINESS = "scynapse-reminders-tardiness";
    public const string REMINDERS_NUMBER_ACTIVE_REMINDERS = "scynapse-reminders-active";
    public const string REMINDERS_COUNTERS_TICKS_DELIVERED = "scynapse-reminders-ticks-delivered";

    // Storage
    public const string STORAGE_READ_ERRORS = "scynapse-storage-read-errors";
    public const string STORAGE_WRITE_ERRORS = "scynapse-storage-write-errors";
    public const string STORAGE_CLEAR_ERRORS = "scynapse-storage-clear-errors";
    public const string STORAGE_READ_LATENCY = "scynapse-storage-read-latency";
    public const string STORAGE_WRITE_LATENCY = "scynapse-storage-write-latency";
    public const string STORAGE_CLEAR_LATENCY = "scynapse-storage-clear-latency";

    // Streams
    public const string STREAMS_PUBSUB_PRODUCERS_ADDED = "scynapse-streams-pubsub-producers-added";
    public const string STREAMS_PUBSUB_PRODUCERS_REMOVED = "scynapse-streams-pubsub-producers-removed";
    public const string STREAMS_PUBSUB_PRODUCERS_TOTAL = "scynapse-streams-pubsub-producers";
    public const string STREAMS_PUBSUB_CONSUMERS_ADDED = "scynapse-streams-pubsub-consumers-added";
    public const string STREAMS_PUBSUB_CONSUMERS_REMOVED = "scynapse-streams-pubsub-consumers-removed";
    public const string STREAMS_PUBSUB_CONSUMERS_TOTAL = "scynapse-streams-pubsub-consumers";

    public const string STREAMS_PERSISTENT_STREAM_NUM_PULLING_AGENTS = "scynapse-streams-persistent-stream-pulling-agents";
    public const string STREAMS_PERSISTENT_STREAM_NUM_READ_MESSAGES = "scynapse-streams-persistent-stream-messages-read";
    public const string STREAMS_PERSISTENT_STREAM_NUM_SENT_MESSAGES = "scynapse-streams-persistent-stream-messages-sent";
    public const string STREAMS_PERSISTENT_STREAM_PUBSUB_CACHE_SIZE = "scynapse-streams-persistent-stream-pubsub-cache-size";

    public const string STREAMS_QUEUE_INITIALIZATION_FAILURES = "scynapse-streams-queue-initialization-failures";
    public const string STREAMS_QUEUE_INITIALIZATION_DURATION = "scynapse-streams-queue-initialization-duration";
    public const string STREAMS_QUEUE_INITIALIZATION_EXCEPTIONS = "scynapse-streams-queue-initialization-exceptions";
    public const string STREAMS_QUEUE_READ_FAILURES = "scynapse-streams-queue-read-failures";
    public const string STREAMS_QUEUE_READ_DURATION = "scynapse-streams-queue-read-duration";
    public const string STREAMS_QUEUE_READ_EXCEPTIONS = "scynapse-streams-queue-read-exceptions";
    public const string STREAMS_QUEUE_SHUTDOWN_FAILURES = "scynapse-streams-queue-shutdown-failures";
    public const string STREAMS_QUEUE_SHUTDOWN_DURATION = "scynapse-streams-queue-shutdown-duration";
    public const string STREAMS_QUEUE_SHUTDOWN_EXCEPTIONS = "scynapse-streams-queue-shutdown-exceptions";
    public const string STREAMS_QUEUE_MESSAGES_RECEIVED = "scynapse-streams-queue-messages-received";
    public const string STREAMS_QUEUE_OLDEST_MESSAGE_ENQUEUE_AGE = "scynapse-streams-queue-oldest-message-enqueue-age";
    public const string STREAMS_QUEUE_NEWEST_MESSAGE_ENQUEUE_AGE = "scynapse-streams-queue-newest-message-enqueue-age";

    public const string STREAMS_BLOCK_POOL_TOTAL_MEMORY = "scynapse-streams-block-pool-total-memory";
    public const string STREAMS_BLOCK_POOL_AVAILABLE_MEMORY = "scynapse-streams-block-pool-available-memory";
    public const string STREAMS_BLOCK_POOL_CLAIMED_MEMORY = "scynapse-streams-block-pool-claimed-memory";
    public const string STREAMS_BLOCK_POOL_RELEASED_MEMORY = "scynapse-streams-block-pool-released-memory";
    public const string STREAMS_BLOCK_POOL_ALLOCATED_MEMORY = "scynapse-streams-block-pool-allocated-memory";

    public const string STREAMS_QUEUE_CACHE_SIZE = "scynapse-streams-queue-cache-size";
    public const string STREAMS_QUEUE_CACHE_LENGTH = "scynapse-streams-queue-cache-length";
    public const string STREAMS_QUEUE_CACHE_MESSAGES_ADDED = "scynapse-streams-queue-cache-messages-added";
    public const string STREAMS_QUEUE_CACHE_MESSAGES_PURGED = "scynapse-streams-queue-cache-messages-purged";
    public const string STREAMS_QUEUE_CACHE_MEMORY_ALLOCATED = "scynapse-streams-queue-cache-memory-allocated";
    public const string STREAMS_QUEUE_CACHE_MEMORY_RELEASED = "scynapse-streams-queue-cache-memory-released";
    public const string STREAMS_QUEUE_CACHE_OLDEST_TO_NEWEST_DURATION = "scynapse-streams-queue-cache-oldest-to-newest-duration";
    public const string STREAMS_QUEUE_CACHE_OLDEST_AGE = "scynapse-streams-queue-cache-oldest-age";
    public const string STREAMS_QUEUE_CACHE_PRESSURE = "scynapse-streams-queue-cache-pressure";
    public const string STREAMS_QUEUE_CACHE_UNDER_PRESSURE = "scynapse-streams-queue-cache-under-pressure";
    public const string STREAMS_QUEUE_CACHE_PRESSURE_CONTRIBUTION_COUNT = "scynapse-streams-queue-cache-pressure-contribution-count";

    public const string RUNTIME_MEMORY_TOTAL_PHYSICAL_MEMORY_MB = "scynapse-runtime-total-physical-memory";
    public const string RUNTIME_MEMORY_AVAILABLE_MEMORY_MB = "scynapse-runtime-available-memory";
}
