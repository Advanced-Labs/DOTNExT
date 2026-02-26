-- Run this migration for upgrading the PostgreSQL clustering table and routines for deployments created before 3.6.0

BEGIN;

-- Change date type

ALTER TABLE ScynapseMembershipVersionTable
ALTER COLUMN Timestamp TYPE TIMESTAMPTZ(3) USING Timestamp AT TIME ZONE 'UTC';

ALTER TABLE ScynapseMembershipTable
ALTER COLUMN StartTime TYPE TIMESTAMPTZ(3) USING StartTime AT TIME ZONE 'UTC',
ALTER COLUMN IAmAliveTime TYPE TIMESTAMPTZ(3) USING IAmAliveTime AT TIME ZONE 'UTC';

-- Recreate routines

CREATE OR REPLACE FUNCTION update_i_am_alive_time(
    deployment_id ScynapseMembershipTable.DeploymentId%TYPE,
    address_arg ScynapseMembershipTable.Address%TYPE,
    port_arg ScynapseMembershipTable.Port%TYPE,
    generation_arg ScynapseMembershipTable.Generation%TYPE,
    i_am_alive_time ScynapseMembershipTable.IAmAliveTime%TYPE)
  RETURNS void AS
$func$
BEGIN
    -- This is expected to never fail by Scynapse, so return value
    -- is not needed nor is it checked.
    UPDATE ScynapseMembershipTable as d
    SET
        IAmAliveTime = i_am_alive_time
    WHERE
        d.DeploymentId = deployment_id AND deployment_id IS NOT NULL
        AND d.Address = address_arg AND address_arg IS NOT NULL
        AND d.Port = port_arg AND port_arg IS NOT NULL
        AND d.Generation = generation_arg AND generation_arg IS NOT NULL;
END
$func$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION insert_membership(
    DeploymentIdArg ScynapseMembershipTable.DeploymentId%TYPE,
    AddressArg      ScynapseMembershipTable.Address%TYPE,
    PortArg         ScynapseMembershipTable.Port%TYPE,
    GenerationArg   ScynapseMembershipTable.Generation%TYPE,
    SiloNameArg     ScynapseMembershipTable.SiloName%TYPE,
    HostNameArg     ScynapseMembershipTable.HostName%TYPE,
    StatusArg       ScynapseMembershipTable.Status%TYPE,
    ProxyPortArg    ScynapseMembershipTable.ProxyPort%TYPE,
    StartTimeArg    ScynapseMembershipTable.StartTime%TYPE,
    IAmAliveTimeArg ScynapseMembershipTable.IAmAliveTime%TYPE,
    VersionArg      ScynapseMembershipVersionTable.Version%TYPE)
  RETURNS TABLE(row_count integer) AS
$func$
DECLARE
    RowCountVar int := 0;
BEGIN

    BEGIN
        INSERT INTO ScynapseMembershipTable
        (
            DeploymentId,
            Address,
            Port,
            Generation,
            SiloName,
            HostName,
            Status,
            ProxyPort,
            StartTime,
            IAmAliveTime
        )
        SELECT
            DeploymentIdArg,
            AddressArg,
            PortArg,
            GenerationArg,
            SiloNameArg,
            HostNameArg,
            StatusArg,
            ProxyPortArg,
            StartTimeArg,
            IAmAliveTimeArg
        ON CONFLICT (DeploymentId, Address, Port, Generation) DO
            NOTHING;


        GET DIAGNOSTICS RowCountVar = ROW_COUNT;

        UPDATE ScynapseMembershipVersionTable
        SET
            Timestamp = now(),
            Version = Version + 1
        WHERE
            DeploymentId = DeploymentIdArg AND DeploymentIdArg IS NOT NULL
            AND Version = VersionArg AND VersionArg IS NOT NULL
            AND RowCountVar > 0;

        GET DIAGNOSTICS RowCountVar = ROW_COUNT;

        ASSERT RowCountVar <> 0, 'no rows affected, rollback';


        RETURN QUERY SELECT RowCountVar;
    EXCEPTION
    WHEN assert_failure THEN
        RETURN QUERY SELECT RowCountVar;
    END;

END
$func$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION update_membership(
    DeploymentIdArg ScynapseMembershipTable.DeploymentId%TYPE,
    AddressArg      ScynapseMembershipTable.Address%TYPE,
    PortArg         ScynapseMembershipTable.Port%TYPE,
    GenerationArg   ScynapseMembershipTable.Generation%TYPE,
    StatusArg       ScynapseMembershipTable.Status%TYPE,
    SuspectTimesArg ScynapseMembershipTable.SuspectTimes%TYPE,
    IAmAliveTimeArg ScynapseMembershipTable.IAmAliveTime%TYPE,
    VersionArg      ScynapseMembershipVersionTable.Version%TYPE
  )
  RETURNS TABLE(row_count integer) AS
$func$
DECLARE
    RowCountVar int := 0;
BEGIN

    BEGIN

    UPDATE ScynapseMembershipVersionTable
    SET
        Timestamp = now(),
        Version = Version + 1
    WHERE
        DeploymentId = DeploymentIdArg AND DeploymentIdArg IS NOT NULL
        AND Version = VersionArg AND VersionArg IS NOT NULL;


    GET DIAGNOSTICS RowCountVar = ROW_COUNT;

    UPDATE ScynapseMembershipTable
    SET
        Status = StatusArg,
        SuspectTimes = SuspectTimesArg,
        IAmAliveTime = IAmAliveTimeArg
    WHERE
        DeploymentId = DeploymentIdArg AND DeploymentIdArg IS NOT NULL
        AND Address = AddressArg AND AddressArg IS NOT NULL
        AND Port = PortArg AND PortArg IS NOT NULL
        AND Generation = GenerationArg AND GenerationArg IS NOT NULL
        AND RowCountVar > 0;


        GET DIAGNOSTICS RowCountVar = ROW_COUNT;

        ASSERT RowCountVar <> 0, 'no rows affected, rollback';


        RETURN QUERY SELECT RowCountVar;
    EXCEPTION
    WHEN assert_failure THEN
        RETURN QUERY SELECT RowCountVar;
    END;

END
$func$ LANGUAGE plpgsql;

COMMIT;
