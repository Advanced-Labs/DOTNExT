-- For each deployment, there will be only one (active) membership version table version column which will be updated periodically.
CREATE TABLE ScynapseMembershipVersionTable
(
    DeploymentId NVARCHAR(150) NOT NULL,
    Timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    Version INT NOT NULL DEFAULT 0,

    CONSTRAINT PK_ScynapseMembershipVersionTable_DeploymentId PRIMARY KEY(DeploymentId)
);

-- Every silo instance has a row in the membership table.
CREATE TABLE ScynapseMembershipTable
(
    DeploymentId NVARCHAR(150) NOT NULL,
    Address VARCHAR(45) NOT NULL,
    Port INT NOT NULL,
    Generation INT NOT NULL,
    SiloName NVARCHAR(150) NOT NULL,
    HostName NVARCHAR(150) NOT NULL,
    Status INT NOT NULL,
    ProxyPort INT NULL,
    SuspectTimes VARCHAR(8000) NULL,
    StartTime DATETIME NOT NULL,
    IAmAliveTime DATETIME NOT NULL,

    CONSTRAINT PK_MembershipTable_DeploymentId PRIMARY KEY(DeploymentId, Address, Port, Generation),
    CONSTRAINT FK_MembershipTable_MembershipVersionTable_DeploymentId FOREIGN KEY (DeploymentId) REFERENCES ScynapseMembershipVersionTable (DeploymentId)
);

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'UpdateIAmAlivetimeKey','
    -- This is expected to never fail by Scynapse, so return value
    -- is not needed nor is it checked.
    UPDATE ScynapseMembershipTable
    SET
        IAmAliveTime = @IAmAliveTime
    WHERE
        DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL
        AND Address = @Address AND @Address IS NOT NULL
        AND Port = @Port AND @Port IS NOT NULL
        AND Generation = @Generation AND @Generation IS NOT NULL;
');

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'InsertMembershipVersionKey','
    INSERT INTO ScynapseMembershipVersionTable
    (
        DeploymentId
    )
    SELECT * FROM ( SELECT @DeploymentId ) AS TMP
    WHERE NOT EXISTS
    (
    SELECT 1
    FROM
        ScynapseMembershipVersionTable
    WHERE
        DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL
    );

    SELECT ROW_COUNT();
');

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'InsertMembershipKey','
    call InsertMembershipKey(@DeploymentId, @Address, @Port, @Generation,
    @Version, @SiloName, @HostName, @Status, @ProxyPort, @StartTime, @IAmAliveTime);'
);

DELIMITER $$

CREATE PROCEDURE InsertMembershipKey(
    in    _DeploymentId NVARCHAR(150),
    in    _Address VARCHAR(45),
    in    _Port INT,
    in    _Generation INT,
    in    _Version INT,
    in    _SiloName NVARCHAR(150),
    in    _HostName NVARCHAR(150),
    in    _Status INT,
    in    _ProxyPort INT,
    in    _StartTime DATETIME,
    in    _IAmAliveTime DATETIME
)
BEGIN
    DECLARE _ROWCOUNT INT;
    START TRANSACTION;
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
    SELECT * FROM ( SELECT
        _DeploymentId,
        _Address,
        _Port,
        _Generation,
        _SiloName,
        _HostName,
        _Status,
        _ProxyPort,
        _StartTime,
        _IAmAliveTime) AS TMP
    WHERE NOT EXISTS
    (
    SELECT 1
    FROM
        ScynapseMembershipTable
    WHERE
        DeploymentId = _DeploymentId AND _DeploymentId IS NOT NULL
        AND Address = _Address AND _Address IS NOT NULL
        AND Port = _Port AND _Port IS NOT NULL
        AND Generation = _Generation AND _Generation IS NOT NULL
    );

    UPDATE ScynapseMembershipVersionTable
    SET
        Version = Version + 1
    WHERE
        DeploymentId = _DeploymentId AND _DeploymentId IS NOT NULL
        AND Version = _Version AND _Version IS NOT NULL
        AND ROW_COUNT() > 0;

    SET _ROWCOUNT = ROW_COUNT();

    IF _ROWCOUNT = 0
    THEN
        ROLLBACK;
    ELSE
        COMMIT;
    END IF;
    SELECT _ROWCOUNT;
END$$

DELIMITER ;

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'UpdateMembershipKey','
    START TRANSACTION;

    UPDATE ScynapseMembershipVersionTable
    SET
        Version = Version + 1
    WHERE
        DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL
        AND Version = @Version AND @Version IS NOT NULL;

    UPDATE ScynapseMembershipTable
    SET
        Status = @Status,
        SuspectTimes = @SuspectTimes,
        IAmAliveTime = @IAmAliveTime
    WHERE
        DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL
        AND Address = @Address AND @Address IS NOT NULL
        AND Port = @Port AND @Port IS NOT NULL
        AND Generation = @Generation AND @Generation IS NOT NULL
        AND ROW_COUNT() > 0;

    SELECT ROW_COUNT();
    COMMIT;
');

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'GatewaysQueryKey','
    SELECT
        Address,
        ProxyPort,
        Generation
    FROM
        ScynapseMembershipTable
    WHERE
        DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL
        AND Status = @Status AND @Status IS NOT NULL
        AND ProxyPort > 0;
');

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'MembershipReadRowKey','
    SELECT
        v.DeploymentId,
        m.Address,
        m.Port,
        m.Generation,
        m.SiloName,
        m.HostName,
        m.Status,
        m.ProxyPort,
        m.SuspectTimes,
        m.StartTime,
        m.IAmAliveTime,
        v.Version
    FROM
        ScynapseMembershipVersionTable v
        -- This ensures the version table will returned even if there is no matching membership row.
        LEFT OUTER JOIN ScynapseMembershipTable m ON v.DeploymentId = m.DeploymentId
        AND Address = @Address AND @Address IS NOT NULL
        AND Port = @Port AND @Port IS NOT NULL
        AND Generation = @Generation AND @Generation IS NOT NULL
    WHERE
        v.DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;
');

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'MembershipReadAllKey','
    SELECT
        v.DeploymentId,
        m.Address,
        m.Port,
        m.Generation,
        m.SiloName,
        m.HostName,
        m.Status,
        m.ProxyPort,
        m.SuspectTimes,
        m.StartTime,
        m.IAmAliveTime,
        v.Version
    FROM
        ScynapseMembershipVersionTable v LEFT OUTER JOIN ScynapseMembershipTable m
        ON v.DeploymentId = m.DeploymentId
    WHERE
        v.DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;
');

INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
    'DeleteMembershipTableEntriesKey','
    DELETE FROM ScynapseMembershipTable
    WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;
    DELETE FROM ScynapseMembershipVersionTable
    WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;
');
