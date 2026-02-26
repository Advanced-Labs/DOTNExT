INSERT INTO ScynapseQuery(QueryKey, QueryText)
VALUES
(
  'DeleteMembershipTableEntriesKey','
  BEGIN
    DELETE FROM ScynapseMembershipTable
    WHERE DeploymentId = :DeploymentId
        AND :DeploymentId IS NOT NULL
        AND IAmAliveTime < :IAmAliveTime
        AND Status != 3;
  END;
');
/