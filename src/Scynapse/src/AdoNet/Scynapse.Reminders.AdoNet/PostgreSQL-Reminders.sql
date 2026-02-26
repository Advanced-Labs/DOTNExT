-- Scynapse Reminders table - https://learn.microsoft.com/dotnet/scynapse/grains/timers-and-reminders
CREATE TABLE ScynapseRemindersTable
(
    ServiceId varchar(150) NOT NULL,
    GrainId varchar(150) NOT NULL,
    ReminderName varchar(150) NOT NULL,
    StartTime timestamptz(3) NOT NULL,
    Period bigint NOT NULL,
    GrainHash integer NOT NULL,
    Version integer NOT NULL,

    CONSTRAINT PK_RemindersTable_ServiceId_GrainId_ReminderName PRIMARY KEY(ServiceId, GrainId, ReminderName)
);

CREATE FUNCTION upsert_reminder_row(
    ServiceIdArg    ScynapseRemindersTable.ServiceId%TYPE,
    GrainIdArg      ScynapseRemindersTable.GrainId%TYPE,
    ReminderNameArg ScynapseRemindersTable.ReminderName%TYPE,
    StartTimeArg    ScynapseRemindersTable.StartTime%TYPE,
    PeriodArg       ScynapseRemindersTable.Period%TYPE,
    GrainHashArg    ScynapseRemindersTable.GrainHash%TYPE
  )
  RETURNS TABLE(version integer) AS
$func$
DECLARE
    VersionVar int := 0;
BEGIN

    INSERT INTO ScynapseRemindersTable
    (
        ServiceId,
        GrainId,
        ReminderName,
        StartTime,
        Period,
        GrainHash,
        Version
    )
    SELECT
        ServiceIdArg,
        GrainIdArg,
        ReminderNameArg,
        StartTimeArg,
        PeriodArg,
        GrainHashArg,
        0
    ON CONFLICT (ServiceId, GrainId, ReminderName)
        DO UPDATE SET
            StartTime = excluded.StartTime,
            Period = excluded.Period,
            GrainHash = excluded.GrainHash,
            Version = ScynapseRemindersTable.Version + 1
    RETURNING
        ScynapseRemindersTable.Version INTO STRICT VersionVar;

    RETURN QUERY SELECT VersionVar AS versionr;

END
$func$ LANGUAGE plpgsql;

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'UpsertReminderRowKey','
    SELECT * FROM upsert_reminder_row(
        @ServiceId,
        @GrainId,
        @ReminderName,
        @StartTime,
        @Period,
        @GrainHash
    );
');

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'ReadReminderRowsKey','
    SELECT
        GrainId,
        ReminderName,
        StartTime,
        Period,
        Version
    FROM ScynapseRemindersTable
    WHERE
        ServiceId = @ServiceId AND @ServiceId IS NOT NULL
        AND GrainId = @GrainId AND @GrainId IS NOT NULL;
');

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'ReadReminderRowKey','
    SELECT
        GrainId,
        ReminderName,
        StartTime,
        Period,
        Version
    FROM ScynapseRemindersTable
    WHERE
        ServiceId = @ServiceId AND @ServiceId IS NOT NULL
        AND GrainId = @GrainId AND @GrainId IS NOT NULL
        AND ReminderName = @ReminderName AND @ReminderName IS NOT NULL;
');

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'ReadRangeRows1Key','
    SELECT
        GrainId,
        ReminderName,
        StartTime,
        Period,
        Version
    FROM ScynapseRemindersTable
    WHERE
        ServiceId = @ServiceId AND @ServiceId IS NOT NULL
        AND GrainHash > @BeginHash AND @BeginHash IS NOT NULL
        AND GrainHash <= @EndHash AND @EndHash IS NOT NULL;
');

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'ReadRangeRows2Key','
    SELECT
        GrainId,
        ReminderName,
        StartTime,
        Period,
        Version
    FROM ScynapseRemindersTable
    WHERE
        ServiceId = @ServiceId AND @ServiceId IS NOT NULL
        AND ((GrainHash > @BeginHash AND @BeginHash IS NOT NULL)
        OR (GrainHash <= @EndHash AND @EndHash IS NOT NULL));
');

CREATE FUNCTION delete_reminder_row(
    ServiceIdArg    ScynapseRemindersTable.ServiceId%TYPE,
    GrainIdArg      ScynapseRemindersTable.GrainId%TYPE,
    ReminderNameArg ScynapseRemindersTable.ReminderName%TYPE,
    VersionArg      ScynapseRemindersTable.Version%TYPE
)
  RETURNS TABLE(row_count integer) AS
$func$
DECLARE
    RowCountVar int := 0;
BEGIN


    DELETE FROM ScynapseRemindersTable
    WHERE
        ServiceId = ServiceIdArg AND ServiceIdArg IS NOT NULL
        AND GrainId = GrainIdArg AND GrainIdArg IS NOT NULL
        AND ReminderName = ReminderNameArg AND ReminderNameArg IS NOT NULL
        AND Version = VersionArg AND VersionArg IS NOT NULL;

    GET DIAGNOSTICS RowCountVar = ROW_COUNT;

    RETURN QUERY SELECT RowCountVar;

END
$func$ LANGUAGE plpgsql;

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'DeleteReminderRowKey','
    SELECT * FROM delete_reminder_row(
        @ServiceId,
        @GrainId,
        @ReminderName,
        @Version
    );
');

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'DeleteReminderRowsKey','
    DELETE FROM ScynapseRemindersTable
    WHERE
        ServiceId = @ServiceId AND @ServiceId IS NOT NULL;
');
