-- For each deployment, there will be only one (active) membership version table version column which will be updated periodically.
IF OBJECT_ID(N'[ScynapseMembershipVersionTable]', 'U') IS NULL
CREATE TABLE ScynapseMembershipVersionTable
(
	DeploymentId NVARCHAR(150) NOT NULL,
	Timestamp DATETIME2(3) NOT NULL DEFAULT GETUTCDATE(),
	Version INT NOT NULL DEFAULT 0,

	CONSTRAINT PK_ScynapseMembershipVersionTable_DeploymentId PRIMARY KEY(DeploymentId)
);

-- Every silo instance has a row in the membership table.
IF OBJECT_ID(N'[ScynapseMembershipTable]', 'U') IS NULL
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
	StartTime DATETIME2(3) NOT NULL,
	IAmAliveTime DATETIME2(3) NOT NULL,

	CONSTRAINT PK_MembershipTable_DeploymentId PRIMARY KEY(DeploymentId, Address, Port, Generation),
	CONSTRAINT FK_MembershipTable_MembershipVersionTable_DeploymentId FOREIGN KEY (DeploymentId) REFERENCES ScynapseMembershipVersionTable (DeploymentId)
);

INSERT INTO ScynapseQuery(QueryKey, QueryText)
SELECT
	'UpdateIAmAlivetimeKey',
	'-- This is expected to never fail by Scynapse, so return value
	-- is not needed nor is it checked.
	SET NOCOUNT ON;
	UPDATE ScynapseMembershipTable
	SET
		IAmAliveTime = @IAmAliveTime
	WHERE
		DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL
		AND Address = @Address AND @Address IS NOT NULL
		AND Port = @Port AND @Port IS NOT NULL
		AND Generation = @Generation AND @Generation IS NOT NULL;
	'
WHERE NOT EXISTS 
( 
    SELECT 1 
    FROM ScynapseQuery oqt
    WHERE oqt.[QueryKey] = 'UpdateIAmAlivetimeKey'
);

INSERT INTO ScynapseQuery(QueryKey, QueryText)
SELECT 
	'InsertMembershipVersionKey',
	'SET NOCOUNT ON;
	INSERT INTO ScynapseMembershipVersionTable
	(
		DeploymentId
	)
	SELECT @DeploymentId
	WHERE NOT EXISTS
	(
		SELECT 1
		FROM
			ScynapseMembershipVersionTable WITH(HOLDLOCK, XLOCK, ROWLOCK)
		WHERE
			DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL
	);
	
	SELECT @@ROWCOUNT;
	'
WHERE NOT EXISTS 
( 
    SELECT 1 
    FROM ScynapseQuery oqt
    WHERE oqt.[QueryKey] = 'InsertMembershipVersionKey'
);

INSERT INTO ScynapseQuery(QueryKey, QueryText)
SELECT
	'InsertMembershipKey',
	'SET XACT_ABORT, NOCOUNT ON;
	DECLARE @ROWCOUNT AS INT;
	BEGIN TRANSACTION;
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
		@DeploymentId,
		@Address,
		@Port,
		@Generation,
		@SiloName,
		@HostName,
		@Status,
		@ProxyPort,
		@StartTime,
		@IAmAliveTime
	WHERE NOT EXISTS
	(
		SELECT 1
		FROM
			ScynapseMembershipTable WITH(HOLDLOCK, XLOCK, ROWLOCK)
		WHERE
			DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL
			AND Address = @Address AND @Address IS NOT NULL
			AND Port = @Port AND @Port IS NOT NULL
			AND Generation = @Generation AND @Generation IS NOT NULL
	);

	UPDATE ScynapseMembershipVersionTable
	SET
		Timestamp = GETUTCDATE(),
		Version = Version + 1
	WHERE
		DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL
		AND Version = @Version AND @Version IS NOT NULL
		AND @@ROWCOUNT > 0;
	
	SET @ROWCOUNT = @@ROWCOUNT;
	
	IF @ROWCOUNT = 0
		ROLLBACK TRANSACTION
	ELSE
		COMMIT TRANSACTION
	SELECT @ROWCOUNT;
	'
WHERE NOT EXISTS 
( 
    SELECT 1 
    FROM ScynapseQuery oqt
    WHERE oqt.[QueryKey] = 'InsertMembershipKey'
);

INSERT INTO ScynapseQuery(QueryKey, QueryText)
SELECT
	'UpdateMembershipKey',
	'SET XACT_ABORT, NOCOUNT ON;
	BEGIN TRANSACTION;
	
	UPDATE ScynapseMembershipVersionTable
	SET
		Timestamp = GETUTCDATE(),
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
		AND @@ROWCOUNT > 0;
	
	SELECT @@ROWCOUNT;
	COMMIT TRANSACTION;
	'
WHERE NOT EXISTS 
( 
    SELECT 1 
    FROM ScynapseQuery oqt
    WHERE oqt.[QueryKey] = 'UpdateMembershipKey'
);

INSERT INTO ScynapseQuery(QueryKey, QueryText)
SELECT
	'GatewaysQueryKey',
	'SELECT
		Address,
		ProxyPort,
		Generation
	FROM
		ScynapseMembershipTable
	WHERE
		DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL
		AND Status = @Status AND @Status IS NOT NULL
		AND ProxyPort > 0;
	'
WHERE NOT EXISTS 
( 
    SELECT 1 
    FROM ScynapseQuery oqt
    WHERE oqt.[QueryKey] = 'GatewaysQueryKey'
);

INSERT INTO ScynapseQuery(QueryKey, QueryText)
SELECT
	'MembershipReadRowKey',
	'SELECT
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
	'
WHERE NOT EXISTS 
( 
    SELECT 1 
    FROM ScynapseQuery oqt
    WHERE oqt.[QueryKey] = 'MembershipReadRowKey'
);

INSERT INTO ScynapseQuery(QueryKey, QueryText)
SELECT
	'MembershipReadAllKey',
	'SELECT
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
	'
WHERE NOT EXISTS 
( 
    SELECT 1 
    FROM ScynapseQuery oqt
    WHERE oqt.[QueryKey] = 'MembershipReadAllKey'
);

INSERT INTO ScynapseQuery(QueryKey, QueryText)
SELECT
	'DeleteMembershipTableEntriesKey',
	'DELETE FROM ScynapseMembershipTable
	WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;
	DELETE FROM ScynapseMembershipVersionTable
	WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;
	'
WHERE NOT EXISTS 
( 
    SELECT 1 
    FROM ScynapseQuery oqt
    WHERE oqt.[QueryKey] = 'DeleteMembershipTableEntriesKey'
);
