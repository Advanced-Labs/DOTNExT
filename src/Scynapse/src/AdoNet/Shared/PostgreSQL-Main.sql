-- requires Postgres 9.5 (or perhaps higher)

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
        https://github.com/dotnet/scynapse/blob/main/src/Scynapse.Core/SystemTargetInterfaces/IMembershipTable.cs
*/



-- This table defines Scynapse operational queries. Scynapse uses these to manage its operations,
-- these are the only queries Scynapse issues to the database.
-- These can be redefined (e.g. to provide non-destructive updates) provided the stated interface principles hold.
CREATE TABLE ScynapseQuery
(
    QueryKey varchar(64) NOT NULL,
    QueryText varchar(8000) NOT NULL,

    CONSTRAINT ScynapseQuery_Key PRIMARY KEY(QueryKey)
);
