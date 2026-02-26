INSERT INTO ScynapseQuery(QueryKey, QueryText)
SELECT
    'CleanupDefunctSiloEntriesKey',
    'DELETE FROM ScynapseMembershipTable
    WHERE DeploymentId = @DeploymentId
        AND @DeploymentId IS NOT NULL
        AND IAmAliveTime < @IAmAliveTime
        AND Status != 3;
    '
WHERE NOT EXISTS 
( 
    SELECT 1 
    FROM ScynapseQuery oqt
    WHERE oqt.[QueryKey] = 'CleanupDefunctSiloEntriesKey'
);
