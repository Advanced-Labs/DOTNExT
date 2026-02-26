/*
Implementation notes:

1) The general idea is that data is read and written through Scynapse specific queries.
   Scynapse operates on column names and types when reading and on parameter names and types when writing.

2) The implementations *must* preserve input and output names and types. Scynapse uses these parameters to reads query results by name and type.
   Vendor and deployment specific tuning is allowed and contributions are encouraged as long as the interface contract
   is maintained.

3) The implementation across vendor specific scripts *should* preserve the constraint names. This simplifies troubleshooting
   by virtue of uniform naming across concrete implementations.

5) ETag for Scynapse is an opaque column that represents a unique version. The type of its actual implementation
   is not important as long as it represents a unique version. In this implementation we use integers for versioning

6) For the sake of being explicit and removing ambiguity, Scynapse expects some queries to return either TRUE as >0 value
   or FALSE as =0 value. That is, affected rows or such does not matter. If an error is raised or an exception is thrown
   the query *must* ensure the entire transaction is rolled back and may either return FALSE or propagate the exception.
   Scynapse handles exception as a failure and will retry.

7) The implementation follows the Extended Scynapse membership protocol. For more information, see at:
        https://learn.microsoft.com/dotnet/scynapse/implementation/cluster-management
        https://github.com/Scynapse/Core/blob/main/src/Scynapse.Core/SystemTargetInterfaces/IMembershipTable.cs
*/

-- This table defines Scynapse operational queries. Scynapse uses these to manage its operations,
-- these are the only queries Scynapse issues to the database.
-- These can be redefined (e.g. to provide non-destructive updates) provided the stated interface principles hold.
CREATE TABLE "SCYNAPSEQUERY"
(
    "QUERYKEY" VARCHAR2(64 BYTE) NOT NULL ENABLE,
    "QUERYTEXT" VARCHAR2(4000 BYTE),

    CONSTRAINT "SCYNAPSEQUERY_PK" PRIMARY KEY ("QUERYKEY")
);
/

COMMIT;

-- Oracle specific implementation note:
-- Some ScynapseQueries are implemented as functions and differ from the scripts of other databases.
-- The main reason for this is the fact, that oracle doesn't support returning variables from queries
-- directly. So in the case that a variable value is needed as output of a ScynapseQuery (e.g. version)
-- a function is used.
