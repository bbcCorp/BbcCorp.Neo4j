# BbcCorp.Neo4j.NeoGraphManager

A DotNet core version of NeoGraphManager. 

I hope this project can act as a springboard for C# developers starting to explore Neo4j.


## Querying Neo4j
NeoGraphManager includes a set of functions to execute Cypher queries and get results.

* ExecuteNonQuery   - Used to execute queries that do not return result
* ExecuteScalar<T>  - Execute query and return a single result of type T
* FetchRecords<T>   - Execute query and return a list of records of type T
* FetchRecordsAsStream<T>   - Execute query and returns an IAsyncEnumerable of a list of records of type T. Useful to stream large datasets


-------------------------
## Sample Application

You can run the sample application to check the api
```
$ dotnet run --project ./src/BbcCorp.Neo4j.SampleApp/BbcCorp.Neo4j.SampleApp.csproj
```

-------------------------
## Tests

There is an included test project to test the various apis. To run the tests, use the following command.
```
$ dotnet test ./src/BbcCorp.Neo4j.Tests/BbcCorp.Neo4j.Tests.csproj
```
-------------------------
