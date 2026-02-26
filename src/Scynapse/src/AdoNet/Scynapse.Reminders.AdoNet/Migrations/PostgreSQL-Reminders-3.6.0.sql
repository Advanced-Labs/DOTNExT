-- Run this migration for upgrading the PostgreSQL reminder table and routines for deployments created before 3.6.0

BEGIN;

-- Change date type

ALTER TABLE ScynapseRemindersTable
ALTER COLUMN StartTime TYPE TIMESTAMPTZ(3) USING StartTime AT TIME ZONE 'UTC';

-- Recreate routines

CREATE OR REPLACE FUNCTION upsert_reminder_row(
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

COMMIT;
